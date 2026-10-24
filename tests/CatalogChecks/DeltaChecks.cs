using System.Collections;
using System.Reflection;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using EternalDownpatcher.WinForms;

internal static class DeltaChecks
{
    private static int checks;
    private const BindingFlags Private = BindingFlags.NonPublic | BindingFlags.Instance;
    private static void Check(bool value, string message) { if (!value) throw new Exception(message); checks++; }
    private static T Field<T>(MainForm form, string name) => (T)typeof(MainForm).GetField(name, Private)!.GetValue(form)!;
    private static void Set(MainForm form, string name, object? value) => typeof(MainForm).GetField(name, Private)!.SetValue(form, value);
    private static object? Call(MainForm form, string name, params object?[] args) => typeof(MainForm).GetMethod(name, Private)!.Invoke(form, args);
    private static T Wait<T>(Task<T> task)
    {
        var watch = System.Diagnostics.Stopwatch.StartNew();
        while (!task.IsCompleted)
        {
            if (watch.Elapsed > TimeSpan.FromSeconds(30)) throw new TimeoutException("Delta check stalled.");
            Application.DoEvents(); Thread.Sleep(1);
        }
        return task.GetAwaiter().GetResult();
    }
    private static string[] Strings(JsonNode node) => node.AsArray().Select(item => item!.ToString()).ToArray();
    private static void Reject(Action action, string message)
    {
        bool failed = false;
        try { action(); }
        catch (Exception error) when (error is IOException or InvalidDataException or InvalidOperationException || error is TargetInvocationException { InnerException: IOException or InvalidDataException or InvalidOperationException }) { failed = true; }
        Check(failed, message);
    }
    internal static int Run(string root, MainForm form)
    {
        string work = Path.Combine(root, "verified-delta"); Directory.CreateDirectory(work);
        JsonNode evidence = JsonNode.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "fixtures", "rollback-25216728-to-1.1.json")))!;
        var expected = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var unique = new Dictionary<string, JsonNode>(StringComparer.OrdinalIgnoreCase);
        foreach (JsonNode? node in evidence["files"]!.AsArray())
        {
            JsonNode row = node!;
            string path = row["path"]!.ToString(); unique.TryAdd(path, row);
            if (row["targetSize"]!.ToString() != row["sourceSize"]?.ToString() || row["targetSha1"]!.ToString() != row["sourceSha1"]?.ToString()) expected.Add(path);
        }
        Check(expected.Count == 101 && unique.Count == 261, "Independent manifest fixture has unexpected file counts.");
        Check(unique.Values.Sum(row => row["targetSize"]!.GetValue<long>()) == 47037741036L, "Full target size changed.");
        Check(expected.Sum(path => unique[path]["targetSize"]!.GetValue<long>()) == 10360988279L, "Changed target size changed.");
        IList versions = Field<IList>(form, "_versions");
        var settings = Field<UserSettings>(form, "_userSettings");
        bool savedBackup = settings.CreateBackup;
        object? savedInstalled = Field<object?>(form, "_installedVersion");
        string? savedGame = Field<string?>(form, "_doomRootFolder");
        string data = Field<string>(form, "_versionDataFolder");
        try
        {
            settings.CreateBackup = true; Set(form, "_doomRootFolder", null); Set(form, "_installedVersion", versions[24]);
            string generated = (string)Call(form, "BuildGeneratedFileListPath", versions[1])!;
            string[] lines = File.ReadAllLines(generated);
            Regex[] patterns = lines.Select(line => new Regex(line[6..], RegexOptions.CultureInvariant)).ToArray();
            Check(lines.Length == 101 && !lines.Contains("regex:.*"), "Exact build pair still downloads the full target.");
            Check(expected.All(path => patterns.Any(regex => regex.IsMatch(path))), "A changed target file is omitted.");
            Check(unique.Keys.Except(expected, StringComparer.OrdinalIgnoreCase).All(path => !patterns.Any(regex => regex.IsMatch(path))), "An unchanged file is still selected.");
            Check(!patterns.Any(regex => regex.IsMatch("DOOMEternalx64vk.exe.unrelated")), "Verified paths are not anchored.");
            var sourceIds = (string[])versions[24]!.GetType().GetProperty("ManifestIds")!.GetValue(versions[24])!;
            Check(sourceIds.SequenceEqual(Strings(evidence["sourceManifestIds"]!)), "Source snapshot does not match manifest evidence.");
            Check(((string[])versions[1]!.GetType().GetProperty("ManifestIds")!.GetValue(versions[1])!).SequenceEqual(Strings(evidence["targetManifestIds"]!)), "Target snapshot does not match manifest evidence.");
            Set(form, "_installedVersion", versions[23]);
            Check(File.ReadAllLines((string)Call(form, "BuildGeneratedFileListPath", versions[1])!).SequenceEqual(new[] { "regex:.*" }), "A different launcher snapshot reused a source-specific delta.");
            settings.CreateBackup = false; Set(form, "_installedVersion", null);
            Field<RadioButton>(form, "_patchExistingRadioButton").Checked = true;
            Check(File.ReadAllLines((string)Call(form, "BuildGeneratedFileListPath", versions[1])!).SequenceEqual(new[] { "regex:.*" }), "Unknown source used a partial delta.");
            settings.CreateBackup = true; Set(form, "_installedVersion", versions[24]);
            JsonNode catalog = JsonNode.Parse(File.ReadAllText(Path.Combine(data, "versions.json")))!;
            string oldSnapshot = Path.Combine(work, "old-github"); Directory.CreateDirectory(oldSnapshot);
            foreach (string file in Directory.GetFiles(data)) File.Copy(file, Path.Combine(oldSnapshot, Path.GetFileName(file)));
            JsonNode older = catalog.DeepClone(); older["versions"]![1]!.AsObject().Remove("verifiedRollbackFileLists");
            File.WriteAllText(Path.Combine(oldSnapshot, "versions.json"), older.ToJsonString());
            Check((bool)Call(form, "ApplyVersionData", oldSnapshot)!, "An otherwise complete older GitHub snapshot was rejected.");
            Set(form, "_installedVersion", versions[24]);
            Check(File.ReadAllLines((string)Call(form, "BuildGeneratedFileListPath", versions[1])!).SequenceEqual(lines), "An older GitHub snapshot discarded the bundled verified pair.");
            foreach (string failure in new[] { "traversal", "missing-exe", "duplicate", "source-count", "null-path" })
            {
                string invalidFolder = Path.Combine(work, failure); Directory.CreateDirectory(invalidFolder);
                foreach (string file in Directory.GetFiles(data)) File.Copy(file, Path.Combine(invalidFolder, Path.GetFileName(file)));
                JsonNode invalid = catalog.DeepClone(); JsonNode delta = invalid["versions"]![1]!["verifiedRollbackFileLists"]![0]!;
                if (failure == "traversal") delta["files"]![0] = "../outside.exe";
                if (failure == "missing-exe") delta["files"] = new JsonArray("base/small.dat");
                if (failure == "duplicate") delta["files"]!.AsArray().Add(delta["files"]![0]!.DeepClone());
                if (failure == "source-count") delta["sourceManifestIds"]!.AsArray().RemoveAt(7);
                if (failure == "null-path") delta["files"]![0] = null;
                File.WriteAllText(Path.Combine(invalidFolder, "versions.json"), invalid.ToJsonString());
                Reject(() => typeof(MainForm).GetMethod("ValidateVersionDataFolder", BindingFlags.Static | BindingFlags.NonPublic)!.Invoke(null, [invalidFolder]), "Invalid verified delta accepted: " + failure);
            }
            Call(form, "ApplyVersionData", data);
            foreach (string language in new[] { "en", "ru", "es" }) Check(MainForm.T(language, "VerifiedRollbackFiles") != "VerifiedRollbackFiles", "Missing delta notice translation.");

            string stage = Path.Combine(work, "staging"), game = Path.Combine(work, "game"); Directory.CreateDirectory(stage); Directory.CreateDirectory(game);
            File.WriteAllText(Path.Combine(game, "DOOMEternalx64vk.exe"), "original-exe");
            File.WriteAllText(Path.Combine(game, "changed.dat"), "original-data");
            File.WriteAllText(Path.Combine(game, "unchanged.dat"), "keep");
            File.WriteAllText(Path.Combine(stage, "unchanged.dat"), "stale-full-download");
            var plan = new GameDownloadPlan(false, true, false, stage, game, sourceIds, ["DOOMEternalx64vk.exe", "changed.dat"]);
            var result = Wait(GameDownloadOperation.ExecuteAsync(plan, (folder, _, _) =>
            {
                File.WriteAllText(Path.Combine(folder, "DOOMEternalx64vk.exe"), "target-exe");
                File.WriteAllText(Path.Combine(folder, "changed.dat"), "target-data"); return Task.CompletedTask;
            }, (_, _) => Task.CompletedTask, () => { }, _ => { }, _ => { }, CancellationToken.None));
            Check(result.AppliedFiles == 2 && File.ReadAllText(Path.Combine(game, "unchanged.dat")) == "keep", "Reused staging applied an unselected file.");
            Check(!File.Exists(Path.Combine(result.BackupFolder!, "files", "unchanged.dat")), "Unselected files still get backed up.");
            PatchBackup.Restore(result.BackupFolder!, game);
            Check(File.ReadAllText(Path.Combine(game, "DOOMEternalx64vk.exe")) == "original-exe" && File.ReadAllText(Path.Combine(game, "changed.dat")) == "original-data", "Filtered rollback backup cannot restore originals.");
            var missing = plan with { FilesToApply = ["DOOMEternalx64vk.exe", "missing.dat"] };
            Reject(() => Wait(GameDownloadOperation.ExecuteAsync(missing, (_, _, _) => Task.CompletedTask, (_, _) => Task.CompletedTask, () => { }, _ => { }, _ => { }, CancellationToken.None)), "Missing selected payload was applied.");
            Check(File.ReadAllText(Path.Combine(game, "DOOMEternalx64vk.exe")) == "original-exe", "Missing selected payload changed the game before validation.");
            var unsafePlan = plan with { FilesToApply = ["../outside.exe"] };
            Reject(() => Wait(GameDownloadOperation.ExecuteAsync(unsafePlan, (_, _, _) => Task.CompletedTask, (_, _) => Task.CompletedTask, () => { }, _ => { }, _ => { }, CancellationToken.None)), "Unsafe selected payload path accepted.");
        }
        finally
        {
            settings.CreateBackup = savedBackup; Call(form, "ApplyVersionData", data);
            Set(form, "_doomRootFolder", savedGame); Set(form, "_installedVersion", savedInstalled);
            Field<RadioButton>(form, "_downloadAllFilesRadioButton").Checked = true; Call(form, "ApplyLanguage");
        }
        Console.WriteLine($"PASS: {checks} delta checks: independent eight-manifest comparison, exact pair matching, 101 changed files, retained metadata and filtered staging/backup.");
        return checks;
    }
}
