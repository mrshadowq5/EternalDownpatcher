using System.Collections;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using EternalDownpatcher.WinForms;

internal static class DownloadVerificationChecks
{
    private static int checks;
    private static readonly string[] Ids = Enumerable.Range(1, 8).Select(i => i.ToString()).ToArray();
    private static readonly Dictionary<string, string> Payload = new()
    {
        ["DOOMEternalx64vk.exe"] = "good-exe",
        ["base/resource.dat"] = "GOOD_RESOURCE",
        ["launcher/idTechLauncher.exe"] = "target-launcher",
        ["launcher/resources/app.asar"] = "target-asar"
    };
    private static void Check(bool value, string message) { if (!value) throw new Exception(message); checks++; }
    private static void Wait(Task task)
    {
        var timer = System.Diagnostics.Stopwatch.StartNew();
        while (!task.IsCompleted)
        {
            if (timer.Elapsed > TimeSpan.FromSeconds(30)) throw new TimeoutException("Payload verification stalled.");
            Application.DoEvents(); Thread.Sleep(1);
        }
        task.GetAwaiter().GetResult();
    }
    private static void Put(string folder, string path, string contents)
    {
        string full = Path.Combine(folder, path); Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        File.WriteAllText(full, contents, new UTF8Encoding(false));
    }
    private static ManifestSelection Selection()
    {
        ManifestFile[] files = Payload.Select(pair => new ManifestFile(pair.Key, Encoding.UTF8.GetByteCount(pair.Value),
            Convert.ToHexString(SHA1.HashData(Encoding.UTF8.GetBytes(pair.Value))))).ToArray();
        // A shared path in an earlier depot deliberately contains another identity.
        SteamManifest[] depots = SteamManifestCache.DepotIds.Select((depot, index) => new SteamManifest(depot, Ids[index],
            index == 0 ? [files[0] with { Sha1 = new string('0', 40) }] : index == 7 ? files : [])).ToArray();
        return ManifestRollbackPlanner.Compare(null, depots, null, ["DOOMEternalx64vk.exe"]);
    }
    internal static int Run(string root)
    {
        string work = Path.Combine(root, "download-verification"); Directory.CreateDirectory(work);
        string versionFile = Path.Combine(work, "version.json");
        File.WriteAllText(versionFile, JsonSerializer.Serialize(new { versions = new[] {
            new { name = "1.0", size = "8", buildId = "1", manifestIds = Ids } } }));
        const BindingFlags Static = BindingFlags.Static | BindingFlags.NonPublic;
        object version = ((IList)typeof(MainForm).GetMethod("ReadVersionsJson", Static)!.Invoke(null, [versionFile, false])!)[0]!;
        MethodInfo saveReceipt = typeof(MainForm).GetMethod("SaveVersionReceipt", Static)!;
        foreach (string mode in new[] { "staged", "direct", "standalone" })
        foreach (string fault in new[] { "missing", "hash", "size", "cancel", "valid", "fallback" })
        {
            string scenario = Path.Combine(work, mode + "-" + fault), game = Path.Combine(scenario, "game"), output = Path.Combine(scenario, "output");
            Directory.CreateDirectory(game); Directory.CreateDirectory(output);
            Put(game, "DOOMEternalx64vk.exe", "original");
            bool standalone = mode == "standalone", backup = mode == "staged";
            string destination = mode == "direct" ? game : output;
            foreach (var pair in Payload) Put(destination, pair.Key, pair.Value);
            Put(destination, "unrelated-user.txt", "keep");
            if (fault == "missing") File.Delete(Path.Combine(destination, "base/resource.dat"));
            if (fault == "hash") Put(destination, "DOOMEternalx64vk.exe", "wrongexe"); // Same length.
            if (fault == "size") Put(destination, "base/resource.dat", "short");
            ManifestSelection? selection = fault == "fallback" ? null : Selection();
            string[]? selected = selection?.Files.Append("eternaldownpatcher-version.json").ToArray();
            var plan = new GameDownloadPlan(standalone, backup, true, output, standalone ? null : game, Ids, selected);
            using var cancellation = new CancellationTokenSource();
            int receipts = 0, verifications = 0;
            Exception? failure = null;
            try
            {
                Wait(GameDownloadOperation.ExecuteAsync(plan,
                    (_, _, _) => Task.CompletedTask, // Exit zero without downloading: simulate omitted depots.
                    async (folder, token) =>
                    {
                        selection ??= Selection(); // Full target set after metadata-preparation fallback.
                        await DownloadedPayload.VerifyAsync(folder, selection, token, (_, _, _) =>
                        { verifications++; if (fault == "cancel") cancellation.Cancel(); });
                        saveReceipt.Invoke(null, [folder, version, token]); receipts++;
                    }, () => { }, _ => { }, _ => { }, cancellation.Token,
                    (_, _) => Task.FromResult(selection!.Files.Append("eternaldownpatcher-version.json").ToArray())));
            }
            catch (Exception downloadError) when (downloadError is IOException or InvalidDataException or OperationCanceledException) { failure = downloadError; }
            bool success = fault is "valid" or "fallback";
            Check((failure is null) == success, $"{mode}/{fault}: invalid payload accepted or valid payload rejected.");
            Check(receipts == (success ? 1 : 0), $"{mode}/{fault}: receipt written before validation finished.");
            Check(File.Exists(Path.Combine(destination, "eternaldownpatcher-version.json")) == success,
                $"{mode}/{fault}: incorrect version receipt on disk.");
            if (mode == "direct")
                Check((GameDownloadOperation.ReadPending(game) is null) == success, $"{fault}: unfinished direct update lost pending state.");
            if (backup)
            {
                Check(Directory.EnumerateDirectories(game, "EternalDownpatcher_Backup_*").Any() == success,
                    $"{fault}: backup/apply began before payload verification.");
                Check(File.ReadAllText(Path.Combine(game, "DOOMEternalx64vk.exe")) == (success ? "good-exe" : "original"),
                    $"{fault}: failed staged download modified the game.");
                Check(!File.Exists(Path.Combine(game, "unrelated-user.txt")), "Unselected staged file reached the game.");
            }
            if (standalone) Check(File.ReadAllText(Path.Combine(game, "DOOMEternalx64vk.exe")) == "original", "Standalone changed installed files.");
            if (fault == "cancel") Check(failure is OperationCanceledException && verifications == 1, "Hash verification ignored cancellation.");
        }
        // Verification only reads selected paths; a delta does not require another
        // full copy of the unchanged game resources in the staging folder.
        string partial = Path.Combine(work, "delta-only"); Put(partial, "DOOMEternalx64vk.exe", "good-exe");
        ManifestSelection delta = Selection() with { Files = ["DOOMEternalx64vk.exe"] };
        Wait(DownloadedPayload.VerifyAsync(partial, delta, CancellationToken.None)); checks++;
        bool unknownRejected = false;
        try { Wait(DownloadedPayload.VerifyAsync(partial, delta with { Files = ["unlisted.dat"] }, CancellationToken.None)); }
        catch (InvalidDataException) { unknownRejected = true; }
        Check(unknownRejected, "A selected path without a target identity was accepted.");
        foreach (string language in new[] { "en", "ru", "es" })
        foreach (string key in new[] { "VerifyingDownload", "VerifyDownloadFile" })
            Check(MainForm.T(language, key) != key, "Missing verification translation.");

        string app = Path.Combine(work, "app"), existing = Path.Combine(work, "output-is-file.txt"); Put(work, "output-is-file.txt", "keep");
        bool fileRejected = false;
        try { PathSafety.ValidateOutput(existing, app, null); } catch (IOException) { fileRejected = true; }
        Check(fileRejected, "Output validator accepted a regular file.");
        Check(!PathSafety.TryPrepareOutput(existing, () => throw new Exception("Should not prompt for a file."), out bool reuse, out string? error)
            && error is not null && !reuse, "Existing output file escaped controlled preparation.");
        Check(File.ReadAllText(existing) == "keep", "Preparing output changed a user's file.");
        string cancelled = Path.Combine(work, "cancelled-folder");
        Check(!PathSafety.TryPrepareOutput(cancelled, () => false, out reuse, out error) && error is null && !Directory.Exists(cancelled),
            "Declined folder creation created files or reported an error.");
        string blocked = Path.Combine(existing, "child");
        Check(!PathSafety.TryPrepareOutput(blocked, () => true, out reuse, out error) && error is not null,
            "Directory creation error escaped controlled preparation.");
        string race = Path.Combine(work, "race");
        Check(!PathSafety.TryPrepareOutput(race, () => { File.WriteAllText(race, "raced"); return true; }, out reuse, out error) && error is not null,
            "A file created between confirmation and folder creation escaped the error handler.");
        string fresh = Path.Combine(work, "fresh");
        Check(PathSafety.TryPrepareOutput(fresh, () => true, out reuse, out error) && !reuse && error is null, "New output preparation failed.");
        File.WriteAllText(Path.Combine(fresh, "resume.dat"), "resume");
        Check(PathSafety.TryPrepareOutput(fresh, () => throw new Exception("Existing folder prompted."), out reuse, out error) && reuse && error is null,
            "Existing payload not preserved for resume.");
        Console.WriteLine($"PASS: {checks} download verification/output checks: all modes, missing/stale payloads, exact manifest hashes, fallback, receipts, cancellation and controlled path errors.");
        return checks;
    }
}
