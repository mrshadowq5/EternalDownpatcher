using System.Collections;
using System.IO.Compression;
using System.Net;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using EternalDownpatcher.WinForms;

internal static class Program
{
    private static int checks;
    private const BindingFlags Instance = BindingFlags.Instance | BindingFlags.NonPublic;
    private const BindingFlags Static = BindingFlags.Static | BindingFlags.NonPublic;
    private static readonly Type Cache = typeof(MainForm).Assembly.GetType("EternalDownpatcher.WinForms.VersionDataCache")!;
    private static void Check(bool condition, string message) { if (!condition) throw new Exception(message); checks++; }
    private static object? Call(MainForm form, string name, params object?[] arguments) => typeof(MainForm).GetMethod(name, Instance)!.Invoke(form, arguments);
    private static object? CallStatic(string name, params object?[] arguments) => typeof(MainForm).GetMethod(name, Static)!.Invoke(null, arguments);
    private static T Field<T>(MainForm form, string name) => (T)typeof(MainForm).GetField(name, Instance)!.GetValue(form)!;
    private static void Set(MainForm form, string name, object? value) => typeof(MainForm).GetField(name, Instance)!.SetValue(form, value);
    private static void Wait(Task task)
    {
        var timeout = System.Diagnostics.Stopwatch.StartNew();
        while (!task.IsCompleted)
        {
            if (timeout.Elapsed > TimeSpan.FromSeconds(45)) throw new TimeoutException("Catalog check did not finish.");
            Application.DoEvents();
            Thread.Sleep(1);
        }
        task.GetAwaiter().GetResult();
    }
    private static string Name(object value) => value.GetType().GetProperty("Name")!.GetValue(value)!.ToString()!;
    private static JsonArray Versions(JsonNode json) => json["versions"]!.AsArray();
    private static void WriteJson(string folder, JsonNode json) => File.WriteAllText(Path.Combine(folder, "versions.json"), json.ToJsonString());
    private static string CopyData(string source, string destination)
    {
        Directory.CreateDirectory(destination);
        foreach (string file in Directory.GetFiles(source)) File.Copy(file, Path.Combine(destination, Path.GetFileName(file)));
        return destination;
    }
    private static byte[] Archive(string data)
    {
        using MemoryStream bytes = new();
        using (ZipArchive archive = new(bytes, ZipArchiveMode.Create, true))
        {
            foreach (string file in Directory.GetFiles(data))
            {
                using Stream output = archive.CreateEntry("repo/data/" + Path.GetFileName(file)).Open();
                output.Write(File.ReadAllBytes(file));
            }
            using Stream junk = archive.CreateEntry("repo/data/../unapproved.exe").Open();
            junk.Write([1, 2, 3]);
        }
        return bytes.ToArray();
    }
    private static async Task<string> Refresh(string root, HttpClient client, CancellationToken token = default) =>
        await ((Task<string>)Cache.GetMethod("RefreshAsync", Static)!.Invoke(null, [root, client, token])!).ConfigureAwait(false);
    private sealed class Handler(byte[] body, HttpStatusCode status = HttpStatusCode.OK) : HttpMessageHandler
    {
        internal int Requests;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
        {
            token.ThrowIfCancellationRequested(); Requests++;
            Check(request.RequestUri!.Host == "codeload.github.com", "Unexpected catalog host.");
            return Task.FromResult(new HttpResponseMessage(status) { Content = new ByteArrayContent(body) });
        }
    }
    [STAThread]
    private static int Main(string[] args)
    {
        string originalCwd = Environment.CurrentDirectory;
        string root = Path.Combine(AppContext.BaseDirectory, "fixtures-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try { Run(root, args); return 0; }
        catch (Exception error) { Console.Error.WriteLine(error); return 1; }
        finally
        {
            Environment.CurrentDirectory = originalCwd;
            string absolute = Path.GetFullPath(root);
            if (absolute.StartsWith(Path.GetFullPath(AppContext.BaseDirectory) + "fixtures-", StringComparison.OrdinalIgnoreCase))
                Directory.Delete(absolute, true);
        }
    }
    private static void Run(string root, string[] args)
    {
        string bundled = Path.Combine(AppContext.BaseDirectory, "data");
        string data = CopyData(bundled, Path.Combine(root, "current", "data"));
        JsonNode json = JsonNode.Parse(File.ReadAllText(Path.Combine(data, "versions.json")))!;
        JsonArray entries = Versions(json);
        Check(entries.Count == 25, "Expected original 22 + three public builds.");
        Check(entries.Skip(22).Select(v => v!["buildId"]!.ToString()).SequenceEqual(new[] { "24695697", "24785517", "25216728" }), "New build ordering.");
        Check(entries.Skip(22).All(v => v!["size"]!.ToString() == "77207984"), "Shared executable size.");
        Check(entries.Skip(22).Select(v => v!["manifestIds"]![7]!.ToString()).SequenceEqual(new[] { "7935278702241685666", "8044268869220163134", "963513668557023129" }), "Launcher manifest mapping.");
        CallStatic("ValidateVersionDataFolder", data); checks++;
        string old = CopyData(data, Path.Combine(root, "old"));
        JsonNode oldJson = json.DeepClone(); while (Versions(oldJson).Count > 22) Versions(oldJson).RemoveAt(22); WriteJson(old, oldJson);
        string previous = CopyData(data, Path.Combine(root, "previous"));
        JsonNode previousJson = json.DeepClone(); Versions(previousJson).RemoveAt(22); Versions(previousJson).RemoveAt(22); WriteJson(previous, previousJson);
        Environment.CurrentDirectory = Path.GetDirectoryName(data)!;
        var settings = new UserSettings { RememberLogin = false, RememberPassword = false };
        var ctor = typeof(MainForm).GetConstructors(Instance).Single(c => c.GetParameters().Length == 2);
        using var form = (MainForm)ctor.Invoke([settings, (Func<UserSettings, bool>)(_ => true)]);
        IList versions = Field<IList>(form, "_versions"); versions.Clear(); Call(form, "ApplyVersionData", data);
        ComboBox combo = Field<ComboBox>(form, "_versionComboBox");
        Check(versions.Count == 25 && combo.Items.Count == 25 && combo.SelectedItem?.ToString() == "1.1", "Startup list/default.");
        Check(!(bool)Call(form, "ApplyVersionData", old)!, "Old 22-entry catalog replaced new list.");
        Check(!(bool)Call(form, "ApplyVersionData", previous)!, "Same EXE sizes hid missing depot snapshots.");
        Wait((Task)Call(form, "UpdateVersionDataAsync", (Func<CancellationToken, Task<string>>)(_ => Task.FromResult(previous)))!);
        Check(versions.Count == 25, "Async refresh discarded intermediate builds.");
        int pairs = 0;
        for (int installed = 1; installed < versions.Count; installed++)
        {
            Set(form, "_installedVersion", versions[installed]);
            for (int target = 0; target < installed; target++)
            {
                string generated = (string)Call(form, "BuildGeneratedFileListPath", versions[target])!; pairs++;
                if (installed == 24 && target == 1)
                {
                    var selected = entries[target]!["verifiedRollbackFileLists"]![0]!["files"]!.AsArray().Select(file => file!.ToString());
                    Check(File.ReadAllLines(generated).SequenceEqual(selected.OrderBy(file => file, StringComparer.OrdinalIgnoreCase)
                        .Select(file => "regex:^" + Regex.Escape(file) + "$")), "Verified rollback pair changed.");
                }
                else if (installed >= 22)
                    Check(File.ReadAllLines(generated).SequenceEqual(new[] { "regex:.*" }), "Unverified new build rollback omitted files.");
                else
                {
                    var expected = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    var names = Enumerable.Range(target + 1, installed - target).Select(i => Name(versions[i]!)).ToList();
                    if (target == 0) names.Insert(0, "1.1.0");
                    foreach (string name in names)
                        foreach (string token in File.ReadAllText(Path.Combine(data, name + ".txt")).Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries))
                            expected.Add(token.StartsWith("regex:", StringComparison.OrdinalIgnoreCase) ? token[6..] : token);
                    Check(File.ReadAllLines(generated).SequenceEqual(expected.OrderBy(s => s, StringComparer.OrdinalIgnoreCase).Select(s => "regex:" + Regex.Escape(s))), "Legacy partial list changed.");
                }
            }
        }
        Check(pairs == 300, "Missing rollback pair.");
        string game = Path.Combine(root, "library", "steamapps", "common", "DOOMEternal"); Directory.CreateDirectory(game);
        string exe = Path.Combine(game, "DOOMEternalx64vk.exe");
        using (FileStream input = File.Create(exe)) input.SetLength(77207984);
        Check(Call(form, "DetectInstalledVersion", game) is null, "Ambiguous EXE guessed a launcher build.");
        string steamManifest = Path.Combine(root, "library", "steamapps", "appmanifest_782330.acf");
        string manifests = string.Join("\n", entries[23]!["manifestIds"]!.AsArray().Select((id, i) => $"\"{782332 + i}\" {{ \"manifest\" \"{id}\" }}"));
        File.WriteAllText(steamManifest, "\"AppState\" { \"installdir\" \"DOOMEternal\" \"InstalledDepots\" { " + manifests + " } }");
        Check(Name(Call(form, "DetectInstalledVersion", game)!) == "Build 24785517", "Steam depot snapshot identification.");
        File.WriteAllText(steamManifest, "\"AppState\" { \"installdir\" \"DifferentGame\" \"InstalledDepots\" { " + manifests + " } }");
        Check(Call(form, "DetectInstalledVersion", game) is null, "Unrelated Steam manifest accepted.");
        string launcher = Path.Combine(game, "launcher"); Directory.CreateDirectory(Path.Combine(launcher, "resources"));
        File.WriteAllText(Path.Combine(launcher, "idTechLauncher.exe"), "fixture data only");
        File.WriteAllText(Path.Combine(launcher, "resources", "app.asar"), "fixture data only");
        CallStatic("SaveVersionReceipt", game, versions[23], CancellationToken.None);
        Check(Name(Call(form, "DetectInstalledVersion", game)!) == "Build 24785517", "Standalone copy receipt identification.");
        File.AppendAllText(Path.Combine(launcher, "resources", "app.asar"), "changed");
        Check(Call(form, "DetectInstalledVersion", game) is null, "Stale receipt accepted changed launcher.");
        File.WriteAllText(Path.Combine(game, "eternaldownpatcher-version.json"), "{\"FormatVersion\":1,\"ManifestIds\":[\"1\",\"1\",\"1\",\"1\",\"1\",\"1\",\"1\",\"1\"],\"Files\":[null,null,null]}");
        Check(Call(form, "DetectInstalledVersion", game) is null, "Malformed receipt crashed detection.");
        string fingerprints = CopyData(data, Path.Combine(root, "fingerprints")); JsonNode fingerprintJson = json.DeepClone();
        for (int i = 22; i <= 24; i += 2)
        {
            byte[] contents = Encoding.UTF8.GetBytes("build fixture " + i);
            Versions(fingerprintJson)[i]!["detectionFiles"] = new JsonArray(new JsonObject { ["path"] = "launcher/resources/app.asar", ["size"] = contents.Length, ["sha1"] = Convert.ToHexString(SHA1.HashData(contents)) });
        }
        WriteJson(fingerprints, fingerprintJson); Call(form, "ApplyVersionData", fingerprints);
        foreach (int i in new[] { 22, 24 })
        {
            File.WriteAllText(Path.Combine(launcher, "resources", "app.asar"), "build fixture " + i);
            Check(Name(Call(form, "DetectInstalledVersion", game)!) == "Build " + entries[i]!["buildId"], "Shared EXE launcher fingerprint identification.");
        }
        Call(form, "ApplyVersionData", data);
        Set(form, "_installedVersion", versions[24]); Set(form, "_doomRootFolder", null);
        Field<RadioButton>(form, "_patchExistingRadioButton").Checked = true; Call(form, "RefreshDownpatchVersions");
        Check(combo.Items.Count == 24 && combo.Items.Contains("Build 24785517") && !combo.Items.Contains("Build 25216728"), "Older target filter.");
        Field<RadioButton>(form, "_downloadAllFilesRadioButton").Checked = true;
        Check(combo.Items.Count == 25, "Full download list.");
        foreach (string language in new[] { "en", "ru", "es" })
        {
            settings.Language = language;
            foreach (string key in new[] { "AmbiguousBuild", "CompleteTargetDownloadWarning", "VersionDataOlder" })
                Check((string)Call(form, "L", key)! != key, "Missing translated notice.");
        }
        string future = CopyData(data, Path.Combine(root, "future")); JsonNode futureJson = json.DeepClone();
        JsonNode extra = entries[24]!.DeepClone(); extra["name"] = "Build 99999999"; extra["buildId"] = "99999999"; extra["manifestIds"]![7] = "99999999";
        Versions(futureJson).Add(extra); WriteJson(future, futureJson);
        Check((bool)Call(form, "ApplyVersionData", future)! && versions.Count == 26, "Future build with the same EXE was rejected.");
        versions.Clear(); Call(form, "ApplyVersionData", data);
        string cacheRoot = Path.Combine(root, "cache");
        using var handler = new Handler(Archive(data)); using var client = new HttpClient(handler);
        string snapshot = Refresh(cacheRoot, client).GetAwaiter().GetResult();
        Check((bool)Call(form, "ApplyVersionData", snapshot)! && versions.Count == 25, "GitHub archive did not update catalog.");
        Check(handler.Requests == 1 && !File.Exists(Path.Combine(cacheRoot, "unapproved.exe")), "Metadata request/extraction scope.");
        Check(Refresh(cacheRoot, client).GetAwaiter().GetResult() == snapshot, "Identical snapshot duplicated cache.");
        using var failing = new HttpClient(new Handler([], HttpStatusCode.ServiceUnavailable));
        Wait((Task)Call(form, "UpdateVersionDataAsync", (Func<CancellationToken, Task<string>>)(token => Refresh(cacheRoot, failing, token)))!);
        Check(versions.Count == 25, "Offline fallback lost catalog.");
        string bad = CopyData(data, Path.Combine(root, "bad")); JsonNode badJson = json.DeepClone();
        Versions(badJson)[24]!["detectionFiles"]![0]!["path"] = "../outside.exe"; WriteJson(bad, badJson);
        using var invalid = new HttpClient(new Handler(Archive(bad))); bool rejected = false;
        try { Refresh(cacheRoot, invalid).GetAwaiter().GetResult(); } catch (TargetInvocationException) { rejected = true; } catch (InvalidDataException) { rejected = true; }
        Check(rejected && (string?)Cache.GetMethod("FindCurrentFolder", Static)!.Invoke(null, [cacheRoot]) == snapshot, "Invalid update replaced working cache.");
        if (args.Contains("--online"))
        {
            using var online = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
            online.DefaultRequestHeaders.UserAgent.ParseAdd("EternalDownpatcher-CatalogChecks/1.0");
            string downloaded = Refresh(Path.Combine(root, "online-cache"), online).GetAwaiter().GetResult();
            Call(form, "ApplyVersionData", downloaded);
            Check(versions.Count >= 25, "Public catalog removed local builds.");
            Console.WriteLine("Live GitHub metadata request succeeded; bundled catalog retained if remote is older.");
        }
        checks += UniversalManifestChecks.Run(root, args);
        checks += DeltaChecks.Run(root, form);
        checks += SpeedChecks.Run(root, form, args);
        checks += RestoreChecks.Run(root, form, args);
        Console.WriteLine($"PASS: {checks} checks; all {pairs} rollback pairs, shared-EXE recognition, receipts, UI, GitHub snapshots and offline/invalid updates. No game or DepotDownloader executed.");
    }
}