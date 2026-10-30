using System.Collections;
using System.Reflection;
using System.Text.Json.Nodes;
using EternalDownpatcher.WinForms;
using System.IO.Compression;
using System.Net;
using System.Net.Http;
using System.Text.Json;

internal static class Checks
{
    private const BindingFlags Hidden = BindingFlags.Instance | BindingFlags.Static | BindingFlags.NonPublic;
    private static int _assertions;

    [STAThread]
    private static void Main(string[] args)
    {
        string root = Path.GetFullPath(args.Length > 0 ? args[0] : ".");
        Environment.CurrentDirectory = root;
        string data = Path.Combine(root, "data");
        string scratch = Path.Combine(root, "checks", "VersionLabels", "obj", "fixtures-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(scratch);
        Call(null, "ValidateVersionDataFolder", data);
        var catalog = JsonNode.Parse(File.ReadAllText(Path.Combine(data, "versions.json")))!;
        var entries = catalog["versions"]!.AsArray();
        var versions = (IList)Call(null, "ReadVersionsJson", Path.Combine(data, "versions.json"), true)!;
        Assert(versions.Count == entries.Count, "All catalog snapshots load");

        using MainForm form = (MainForm)Activator.CreateInstance(typeof(MainForm), Hidden, null,
            [new UserSettings(), (Func<UserSettings, bool>)(_ => true)], null)!;
        var loaded = Field<IList>(form, "_versions");
        loaded.Clear();
        Assert((bool)Call(form, "ApplyVersionData", data)!, "Catalog applies");
        var combo = Field<ComboBox>(form, "_versionComboBox");
        var fullDownload = Field<RadioButton>(form, "_downloadAllFilesRadioButton");
        fullDownload.Checked = true;
        Assert(Value<string>(Call(form, "GetSelectedVersion")!, "Name") == "1.1", "Default selection remains 1.1");
        for (int i = 0; i < entries.Count; i++)
        {
            combo.SelectedIndex = i;
            object selected = Call(form, "GetSelectedVersion")!;
            string name = entries[i]!["name"]!.GetValue<string>();
            string label = entries[i]!["displayName"]?.GetValue<string>() ?? name;
            Assert(Value<string>(selected, "Name") == name, "Selection identity: " + name);
            Assert(selected.ToString() == label, "Visible label: " + name);
            Assert(Value<string[]>(selected, "ManifestIds").SequenceEqual(
                entries[i]!["manifestIds"]!.AsArray().Select(x => x!.GetValue<string>().Trim())), "Selection manifests: " + name);
        }
        Assert(loaded.Cast<object>().Select(v => Value<string>(v, "DisplayName")).Distinct().Count() == entries.Count,
            "Every displayed label is unique");
        object last = combo.SelectedItem!;
        string lastName = Value<string>(last, "Name");
        string lastLabel = Value<string>(last, "DisplayName");

        // A catalog refresh can replace every record and change a label; selection must follow the stable key.
        catalog["versions"]!.AsArray().Last()!["displayName"] = "6.66 Rev 3.2 (Build 25216728) updated";
        string renamedFolder = WriteCatalog(scratch, "renamed", catalog);
        Assert((bool)Call(form, "ApplyVersionData", renamedFolder)!, "Renamed catalog applies");
        Assert(Value<string>(Call(form, "GetSelectedVersion")!, "Name") == lastName, "Selection survives label changes");
        Assert(combo.Text.EndsWith("updated"), "Refreshed label reaches the combo box");
        Assert(Field<ToolTip>(form, "_versionToolTip").GetToolTip(combo) == combo.Text, "Tooltip shows the complete label");
        Call(form, "ApplyVersionData", data);

        // Old clients/catalogs use only name. New builds retain known labels for identical manifests.
        foreach (JsonNode? entry in catalog["versions"]!.AsArray()) entry!.AsObject().Remove("displayName");
        string legacy = WriteCatalog(scratch, "legacy", catalog);
        var legacyVersions = (IList)Call(null, "ReadVersionsJson", Path.Combine(legacy, "versions.json"), true)!;
        Assert(legacyVersions.Cast<object>().All(v => Value<string>(v, "DisplayName") == Value<string>(v, "Name")),
            "Catalogs without displayName still parse");
        Call(form, "ApplyVersionData", legacy);
        Assert(Value<string>(Call(form, "GetSelectedVersion")!, "DisplayName") == lastLabel, "Old remote catalog does not erase known labels");

        // Receipt resolution must distinguish all three builds even though they share one game EXE.
        var buildVersions = loaded.Cast<object>().Where(v => Value<string>(v, "Name").StartsWith("Build ")).ToArray();
        Assert(buildVersions.Length == 3, "Three distinct late builds remain");
        var candidates = Array.CreateInstance(last.GetType(), buildVersions.Length);
        for (int i = 0; i < buildVersions.Length; i++) candidates.SetValue(buildVersions[i], i);
        string fakeGame = Path.Combine(scratch, "game");
        Directory.CreateDirectory(Path.Combine(fakeGame, "launcher", "resources"));
        foreach (string file in new[] { "DOOMEternalx64vk.exe", "launcher/idTechLauncher.exe", "launcher/resources/app.asar" })
            File.WriteAllText(Path.Combine(fakeGame, file), "test fixture");
        foreach (object build in buildVersions)
        {
            Call(null, "SaveVersionReceipt", fakeGame, build, CancellationToken.None);
            object found = Call(null, "DetectReceiptVersion", fakeGame, candidates)!;
            Assert(Value<string>(found, "Name") == Value<string>(build, "Name"), "Receipt resolves " + Value<string>(build, "Name"));
        }

        // Display labels never become file paths or alter downpatch ordering.
        Call(form, "ApplyVersionData", data);
        string fileList = (string)Call(form, "FindRawFileListPath", "6.66.4")!;
        Assert(Path.GetFileName(fileList) == "6.66.4.txt", "Rev 3.1 retains the original filelist");
        typeof(MainForm).GetField("_installedVersion", Hidden)!.SetValue(form, loaded[loaded.Count - 1]);
        fullDownload.Checked = false;
        Call(form, "RefreshDownpatchVersions");
        Assert(combo.Items.Count == entries.Count - 1, "Only older snapshots appear in downpatch mode");
        Assert(combo.Items.Cast<object>().All(v => Value<string>(v, "Name") != lastName), "Installed snapshot stays excluded");

        var invalid = JsonNode.Parse(File.ReadAllText(Path.Combine(data, "versions.json")))!;
        invalid["versions"]![0]!["displayName"] = invalid["versions"]![1]!["name"]!.GetValue<string>();
        RejectCatalog(scratch, "duplicate", invalid);
        invalid["versions"]![0]!["displayName"] = "Bad\nlabel";
        RejectCatalog(scratch, "control", invalid);
        invalid["versions"]![0]!["displayName"] = " ";
        RejectCatalog(scratch, "blank", invalid);
        CheckCatalogUpdates(data, scratch);
        Console.WriteLine($"PASS: {_assertions} assertions; labels, new versions, archive updates, restart, filelists and build receipts verified.");
    }

    private static string WriteCatalog(string scratch, string name, JsonNode catalog)
    {
        string folder = Path.Combine(scratch, name);
        Directory.CreateDirectory(folder);
        File.WriteAllText(Path.Combine(folder, "versions.json"), catalog.ToJsonString());
        return folder;
    }

    private static void RejectCatalog(string scratch, string name, JsonNode catalog)
    {
        string folder = WriteCatalog(scratch, name, catalog);
        try { Call(null, "ReadVersionsJson", Path.Combine(folder, "versions.json"), true); }
        catch (InvalidDataException) { _assertions++; return; }
        throw new Exception("Invalid catalog was accepted: " + name);
    }

    private static object? Call(MainForm? form, string name, params object?[] args)
    {
        try { return typeof(MainForm).GetMethod(name, Hidden)!.Invoke(form, args); }
        catch (TargetInvocationException error) when (error.InnerException is not null) { throw error.InnerException; }
    }
    private static T Field<T>(MainForm form, string name) => (T)typeof(MainForm).GetField(name, Hidden)!.GetValue(form)!;
    private static T Value<T>(object item, string name) => (T)item.GetType().GetProperty(name)!.GetValue(item)!;
    private static void Assert(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
        _assertions++;
    }

    private static readonly Type CacheType = typeof(MainForm).Assembly.GetType("EternalDownpatcher.WinForms.VersionDataCache")!;

    private static void CheckCatalogUpdates(string data, string scratch)
    {
        string cache = Path.Combine(scratch, "archive-cache");
        JsonNode baseline = JsonNode.Parse(File.ReadAllText(Path.Combine(data, "versions.json")))!;
        int originalCount = baseline["versions"]!.AsArray().Count;
        string current = DownloadCatalog(cache, data, baseline.ToJsonString());
        Assert(CurrentCache(cache) == current, "A downloaded archive becomes the active cache");

        using MainForm form = NewCheckForm();
        Field<IList>(form, "_versions").Clear();
        Call(form, "ApplyVersionData", current);
        var combo = Field<ComboBox>(form, "_versionComboBox");
        Field<RadioButton>(form, "_downloadAllFilesRadioButton").Checked = true;
        combo.SelectedItem = combo.Items.Cast<object>().Single(v => Value<string>(v, "Name") == "Build 24785517");

        // Synthetic future data: no Steam downloads and no real catalog modifications.
        JsonNode extended = baseline.DeepClone();
        var future = new JsonObject
        {
            ["name"] = "Build 99900001",
            ["displayName"] = "Future revision (Build 99900001)",
            ["size"] = "999999",
            ["manifestIds"] = new JsonArray(Enumerable.Range(0, 8)
                .Select(i => (JsonNode?)JsonValue.Create((900000000000000000L + i).ToString())).ToArray()),
            ["requiresCompleteFileList"] = true,
            ["buildId"] = "99900001"
        };
        extended["versions"]!.AsArray().Add(future);
        current = DownloadCatalog(cache, data, extended.ToJsonString());
        Assert((bool)Call(form, "ApplyVersionData", current)!, "A new Build entry applies without a new executable");
        Assert(combo.Items.Count == originalCount + 1, "New Build appears in the list");
        Assert(Value<string>(Call(form, "GetSelectedVersion")!, "Name") == "Build 24785517", "Adding a version preserves selection");
        combo.SelectedIndex = combo.Items.Count - 1;
        Assert(Value<string>(Call(form, "GetSelectedVersion")!, "BuildId") == "99900001", "New label selects the correct Build ID");
        Assert(Value<string[]>(Call(form, "GetSelectedVersion")!, "ManifestIds").SequenceEqual(
            future["manifestIds"]!.AsArray().Select(x => x!.GetValue<string>())), "New label selects all eight correct manifests");

        future["displayName"] = "Renamed future revision";
        current = DownloadCatalog(cache, data, extended.ToJsonString());
        Call(form, "ApplyVersionData", current);
        Assert(combo.Text == "Renamed future revision", "Renaming through the downloaded archive updates the UI");
        Assert(Value<string>(Call(form, "GetSelectedVersion")!, "Name") == "Build 99900001", "Renaming preserves the selected Build identity");
        Assert(Value<string[]>(Call(form, "GetSelectedVersion")!, "ManifestIds").SequenceEqual(
            future["manifestIds"]!.AsArray().Select(x => x!.GetValue<string>())), "Renaming preserves its manifests");
        using (MainForm restarted = NewCheckForm())
        {
            Field<IList>(restarted, "_versions").Clear();
            Call(restarted, "ApplyVersionData", data);
            Call(restarted, "ApplyVersionData", CurrentCache(cache));
            var reloaded = Field<IList>(restarted, "_versions");
            Assert(reloaded.Count == originalCount + 1, "New version survives reload from disk");
            Assert(Value<string>(reloaded[reloaded.Count - 1]!, "DisplayName") == "Renamed future revision", "Renamed label survives reload from disk");
        }

        // Numeric keys with incremental lists need their matching .txt in the same archive.
        JsonNode numeric = future.DeepClone();
        numeric["name"] = "7.0";
        numeric["displayName"] = "7.0 test release";
        numeric["size"] = "999998";
        numeric.AsObject().Remove("buildId");
        numeric["requiresCompleteFileList"] = false;
        numeric["manifestIds"] = new JsonArray(Enumerable.Range(0, 8)
            .Select(i => (JsonNode?)JsonValue.Create((910000000000000000L + i).ToString())).ToArray());
        extended["versions"]!.AsArray().Add(numeric);
        RejectArchive(cache, data, extended.ToJsonString(), null, "A missing new filelist is rejected");
        var extra = new Dictionary<string, string> { ["7.0.txt"] = "DOOMEternalx64vk.exe\n" };
        current = DownloadCatalog(cache, data, extended.ToJsonString(), extra);
        Assert((bool)Call(form, "ApplyVersionData", current)!, "A new numeric version and its filelist apply together");
        Assert(combo.Items.Count == originalCount + 2, "Both new snapshots remain selectable");
        Assert(File.ReadAllText((string)Call(form, "FindRawFileListPath", "7.0")!) == extra["7.0.txt"], "New version resolves its own filelist");
        combo.SelectedIndex = combo.Items.Count - 1;
        Assert(Value<string[]>(Call(form, "GetSelectedVersion")!, "ManifestIds").SequenceEqual(
            numeric["manifestIds"]!.AsArray().Select(x => x!.GetValue<string>())), "Numeric version selects its own manifests");
        Assert(DownloadCatalog(cache, data, extended.ToJsonString(), extra) == current, "Unchanged archives reuse the current snapshot");

        JsonNode unlabeled = extended.DeepClone();
        foreach (JsonNode? entry in unlabeled["versions"]!.AsArray()) entry!.AsObject().Remove("displayName");
        current = DownloadCatalog(cache, data, unlabeled.ToJsonString(), extra);
        using (MainForm restarted = NewCheckForm())
        {
            Field<IList>(restarted, "_versions").Clear();
            Call(restarted, "ApplyVersionData", data);
            Call(restarted, "ApplyVersionData", CurrentCache(cache));
            object futureAfterRestart = Field<IList>(restarted, "_versions").Cast<object>()
                .Single(v => Value<string>(v, "Name") == "Build 99900001");
            Assert(Value<string>(futureAfterRestart, "DisplayName") == "Renamed future revision",
                "A legacy catalog cannot erase a downloaded version label after restart");
        }

        future["displayName"] = "Build 99900001";
        current = DownloadCatalog(cache, data, extended.ToJsonString(), extra);
        Call(form, "ApplyVersionData", current);
        object explicitlyReset = Field<IList>(form, "_versions").Cast<object>()
            .Single(v => Value<string>(v, "Name") == "Build 99900001");
        Assert(Value<string>(explicitlyReset, "DisplayName") == "Build 99900001", "An explicit label overrides a preserved label");
        future["displayName"] = "Renamed future revision";
        current = DownloadCatalog(cache, data, extended.ToJsonString(), extra);
        Call(form, "ApplyVersionData", current);

        JsonNode invalid = extended.DeepClone();
        invalid["versions"]!.AsArray().Last()!["displayName"] = "Renamed future revision";
        RejectArchive(cache, data, invalid.ToJsonString(), extra, "Duplicate labels do not replace the cache");
        invalid = extended.DeepClone();
        invalid["versions"]!.AsArray().Last()!["manifestIds"]!.AsArray().RemoveAt(0);
        RejectArchive(cache, data, invalid.ToJsonString(), extra, "Incomplete manifests do not replace the cache");
        RejectArchive(cache, data, "{ invalid JSON", extra, "Malformed JSON does not replace the cache");
        using (var offline = new HttpClient(new ArchiveHandler([], HttpStatusCode.ServiceUnavailable)))
        {
            bool failed = false;
            try { RefreshCache(cache, offline, CancellationToken.None); }
            catch (HttpRequestException) { failed = true; }
            Assert(failed && CurrentCache(cache) == current, "A network failure preserves the cached catalog");
        }
        using (var client = new HttpClient(new ArchiveHandler(Archive(data, extended.ToJsonString(), extra))))
        {
            bool cancelled = false;
            try { RefreshCache(cache, client, new CancellationToken(true)); }
            catch (OperationCanceledException) { cancelled = true; }
            Assert(cancelled && CurrentCache(cache) == current, "Cancellation preserves the cached catalog");
        }

        // Regression: the UI must not reject a stale catalog after the cache pointer has already been overwritten.
        RejectArchive(cache, data, baseline.ToJsonString(), null, "An older archive cannot discard newly added versions from disk");
        Assert((bool)Call(form, "ApplyVersionData", CurrentCache(cache))!, "The current cache still contains every known snapshot");
        Assert(Field<IList>(form, "_versions").Count == originalCount + 2, "Both additions survive a stale update attempt");
        Assert(!Directory.EnumerateDirectories(cache, "staging-*").Any(), "Completed and rejected updates leave no staging folders");
    }

    private static MainForm NewCheckForm() => (MainForm)Activator.CreateInstance(typeof(MainForm), Hidden, null,
        [new UserSettings(), (Func<UserSettings, bool>)(_ => true)], null)!;

    private static string DownloadCatalog(string cache, string data, string json, Dictionary<string, string>? extra = null)
    {
        using var client = new HttpClient(new ArchiveHandler(Archive(data, json, extra)));
        return RefreshCache(cache, client, CancellationToken.None);
    }

    private static string RefreshCache(string cache, HttpClient client, CancellationToken token) =>
        ((Task<string>)CacheType.GetMethod("RefreshAsync", Hidden)!.Invoke(null, [cache, client, token])!).GetAwaiter().GetResult();

    private static string? CurrentCache(string cache) => (string?)CacheType.GetMethod("FindCurrentFolder", Hidden)!.Invoke(null, [cache]);

    private static void RejectArchive(string cache, string data, string json, Dictionary<string, string>? extra, string message)
    {
        string? before = CurrentCache(cache);
        bool rejected = false;
        try { DownloadCatalog(cache, data, json, extra); }
        catch (Exception error) when (error is InvalidDataException or JsonException) { rejected = true; }
        Assert(rejected && CurrentCache(cache) == before, message);
    }

    private static byte[] Archive(string data, string json, Dictionary<string, string>? extra)
    {
        using var bytes = new MemoryStream();
        using (var zip = new ZipArchive(bytes, ZipArchiveMode.Create, leaveOpen: true))
        {
            void Add(string name, string content)
            {
                using var writer = new StreamWriter(zip.CreateEntry("EternalDownpatcher-main/data/" + name).Open());
                writer.Write(content);
            }
            Add("versions.json", json);
            foreach (string file in Directory.EnumerateFiles(data, "*.txt")) Add(Path.GetFileName(file), File.ReadAllText(file));
            if (extra is not null) foreach (var pair in extra) Add(pair.Key, pair.Value);
        }
        return bytes.ToArray();
    }

    private sealed class ArchiveHandler(byte[] archive, HttpStatusCode status = HttpStatusCode.OK) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (request.RequestUri?.AbsoluteUri != "https://codeload.github.com/mrshadowq5/EternalDownpatcher/zip/refs/heads/main")
                throw new InvalidOperationException("Unexpected catalog URL.");
            return Task.FromResult(new HttpResponseMessage(status) { Content = new ByteArrayContent(archive) });
        }
    }
}
