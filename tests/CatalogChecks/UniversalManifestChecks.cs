using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using EternalDownpatcher.WinForms;

internal static class UniversalManifestChecks
{
    private static int checks;
    private static readonly string[] Required = ["DOOMEternalx64vk.exe"];
    private static void Check(bool value, string message) { if (!value) throw new Exception(message); checks++; }
    private static T Wait<T>(Task<T> task)
    {
        var watch = System.Diagnostics.Stopwatch.StartNew();
        while (!task.IsCompleted)
        {
            if (watch.Elapsed > TimeSpan.FromSeconds(45)) throw new TimeoutException("Manifest preparation stalled.");
            Application.DoEvents(); Thread.Sleep(1);
        }
        return task.GetAwaiter().GetResult();
    }
    private static void Reject(Action action, string message)
    {
        bool rejected = false;
        try { action(); }
        catch (Exception error) when (error is IOException or InvalidDataException or InvalidOperationException or ArgumentException or OperationCanceledException) { rejected = true; }
        Check(rejected, message);
    }
    private static void Number(Stream stream, int field, ulong value) { Varint(stream, (ulong)(field << 3)); Varint(stream, value); }
    private static void Bytes(Stream stream, int field, byte[] bytes) { Varint(stream, (ulong)((field << 3) | 2)); Varint(stream, (ulong)bytes.Length); stream.Write(bytes); }
    private static void Varint(Stream stream, ulong value)
    {
        while (value >= 128) { stream.WriteByte((byte)(value | 128)); value >>= 7; }
        stream.WriteByte((byte)value);
    }
    private static byte[] Encode(ManifestRequest request, ManifestFile[] files, bool encrypted = false)
    {
        using var payload = new MemoryStream();
        foreach (ManifestFile file in files)
        {
            using var mapping = new MemoryStream();
            Bytes(mapping, 1, Encoding.UTF8.GetBytes(file.Path)); Number(mapping, 2, (ulong)file.Size);
            Bytes(mapping, 5, Convert.FromHexString(file.Sha1)); Bytes(payload, 1, mapping.ToArray());
        }
        using var metadata = new MemoryStream(); Number(metadata, 1, (ulong)request.DepotId); Number(metadata, 2, ulong.Parse(request.ManifestId));
        Number(metadata, 4, encrypted ? 1ul : 0ul);
        byte[] length = new byte[4]; BinaryPrimitives.WriteInt32LittleEndian(length, (int)payload.Length);
        Number(metadata, 9, SteamManifestReader.Crc32(length, payload.ToArray()));
        using var result = new MemoryStream(); using var writer = new BinaryWriter(result);
        writer.Write(0x71F617D0u); writer.Write((int)payload.Length); writer.Write(payload.ToArray());
        writer.Write(0x1F4812BEu); writer.Write((int)metadata.Length); writer.Write(metadata.ToArray());
        writer.Write(0x1B81B817u); writer.Write(0); writer.Write(0x32C415ABu); return result.ToArray();
    }
    private static string Hash(string value) => Convert.ToHexString(SHA1.HashData(Encoding.UTF8.GetBytes(value)));
    private static ManifestFile[] Files(ManifestRequest request)
    {
        var files = new List<ManifestFile>
        {
            new($"base/stable-{request.DepotId}.dat", 8, Hash("stable-" + request.DepotId)),
            new($"base/versioned-{request.DepotId}.dat", 13 + (long)(ulong.Parse(request.ManifestId) % 17), Hash(request.ManifestId)),
            new("base/shared-path.dat", 10, Hash(request.DepotId + ":" + request.ManifestId))
        };
        if (request.DepotId == 782332) files.Add(new("DOOMEternalx64vk.exe", 55, Hash("exe:" + request.ManifestId)));
        if (ulong.Parse(request.ManifestId) % 2 == 0) files.Add(new($"base/optional-{request.DepotId}.dat", 3, Hash("optional:" + request.ManifestId)));
        return files.ToArray();
    }
    private static Dictionary<string, ManifestFile> Effective(string[] ids)
    {
        var files = new Dictionary<string, ManifestFile>(StringComparer.OrdinalIgnoreCase);
        for (int index = 0; index < 8; index++) foreach (var file in Files(new(SteamManifestCache.DepotIds[index], ids[index]))) files[file.Path] = file;
        return files;
    }
    private static void WriteCache(string directory, ManifestRequest request, byte[] data)
    {
        Directory.CreateDirectory(directory); string path = Path.Combine(directory, $"{request.DepotId}_{request.ManifestId}.manifest");
        File.WriteAllBytes(path, data); File.WriteAllBytes(path + ".sha", SHA1.HashData(data));
    }
    private static string[] Ids(JsonNode version) => version["manifestIds"]!.AsArray().Select(id => id!.ToString().Trim()).ToArray();

    internal static int Run(string root, string[] args)
    {
        string work = Path.Combine(root, "universal-manifests"); Directory.CreateDirectory(work);
        Check(SteamManifestReader.Crc32([], "123456789"u8) == 0xcbf43926u, "CRC-32 does not match the standard test vector.");
        JsonArray catalog = JsonNode.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "data", "versions.json")))!["versions"]!.AsArray();
        int fetches = 0;
        Task Fetch(IReadOnlyList<ManifestRequest> requests, string output, CancellationToken token)
        {
            fetches++;
            Check(requests.Select(request => request.DepotId).Distinct().Count() == requests.Count, "A manifest-only invocation repeats a depot ID.");
            foreach (var request in requests) { token.ThrowIfCancellationRequested(); WriteCache(Path.Combine(output, ".DepotDownloader"), request, Encode(request, Files(request))); }
            return Task.CompletedTask;
        }
        string cacheRoot = Path.Combine(work, "cache");
        var cache = new SteamManifestCache(cacheRoot, [], Fetch); int pairs = 0;
        for (int source = 1; source < catalog.Count; source++)
        for (int target = 0; target < source; target++)
        {
            string[] from = Ids(catalog[source]!), to = Ids(catalog[target]!);
            ManifestSelection result = Wait(ManifestRollbackPlanner.PrepareAsync(cache, from, to, null, Required, _ => throw new Exception("Synthetic source unavailable."), CancellationToken.None));
            var before = Effective(from); var desired = Effective(to);
            string[] expected = desired.Values.Where(file => !before.TryGetValue(file.Path, out var old) || old.Size != file.Size || old.Sha1 != file.Sha1)
                .Select(file => file.Path).Append(Required[0]).Distinct(StringComparer.OrdinalIgnoreCase).Order(StringComparer.OrdinalIgnoreCase).ToArray();
            Check(result.ComparedSource && result.Files.SequenceEqual(expected), $"Wrong selection for {source} -> {target}.");
            Check(result.SelectedBytes == expected.Sum(path => desired[path].Size) && result.TargetBytes == desired.Values.Sum(file => file.Size), "Incorrect selection byte counts.");
            pairs++;
        }
        Check(pairs == 300, "Universal planner did not cover every catalog pair.");
        int callsBefore = fetches;
        Wait(ManifestRollbackPlanner.PrepareAsync(cache, Ids(catalog[24]!), Ids(catalog[1]!), null, Required, _ => { }, CancellationToken.None));
        Check(fetches == callsBefore, "Repeated pair needlessly fetched metadata.");
        var diskCache = new SteamManifestCache(cacheRoot, [], (_, _, _) => throw new Exception("Persisted cache was ignored."));
        Check(Wait(diskCache.SnapshotAsync(Ids(catalog[1]!), CancellationToken.None)).Length == 8, "Persistent manifest cache cannot be reused.");
        string[] future = Enumerable.Range(1, 8).Select(value => (9000000000000000000L + value).ToString()).ToArray();
        Check(Wait(ManifestRollbackPlanner.PrepareAsync(cache, future, Ids(catalog[1]!), null, Required, _ => { }, CancellationToken.None)).ComparedSource, "Future catalog snapshot requires a hardcoded version pair.");
        var full = Wait(ManifestRollbackPlanner.PrepareAsync(cache, null, Ids(catalog[1]!), null, Required, _ => { }, CancellationToken.None));
        Check(full.Files.Length == full.TargetFiles.Length && full.SelectedBytes == full.TargetBytes, "Unknown/interrupted source used a partial list.");
        string targetOnly = Path.Combine(work, "target-only");
        for (int index = 0; index < 8; index++) { var request = new ManifestRequest(SteamManifestCache.DepotIds[index], Ids(catalog[1]!)[index]); WriteCache(targetOnly, request, Encode(request, Files(request))); }
        int fallbackMessages = 0;
        var noSource = new SteamManifestCache(Path.Combine(work, "fallback"), [targetOnly], (_, _, _) => throw new IOException("Source unavailable"));
        var fallback = Wait(ManifestRollbackPlanner.PrepareAsync(noSource, future, Ids(catalog[1]!), null, Required, _ => fallbackMessages++, CancellationToken.None));
        Check(!fallback.ComparedSource && fallback.Files.Length == fallback.TargetFiles.Length && fallbackMessages == 1, "Unavailable source did not fall back to full target.");
        using (var cancel = new CancellationTokenSource())
        {
            var cancelled = new SteamManifestCache(Path.Combine(work, "cancel"), [targetOnly], (_, _, _) => { cancel.Cancel(); return Task.FromCanceled(cancel.Token); });
            Reject(() => Wait(ManifestRollbackPlanner.PrepareAsync(cancelled, future, Ids(catalog[1]!), null, Required, _ => throw new Exception("Cancellation triggered fallback."), cancel.Token)), "Manifest cancellation was ignored.");
        }

        var sampleRequest = new ManifestRequest(782332, Ids(catalog[1]!)[0]); byte[] valid = Encode(sampleRequest, Files(sampleRequest));
        Check(SteamManifestReader.Parse(valid, sampleRequest).Files.Length == Files(sampleRequest).Length, "Binary manifest parsing lost files.");
        Check(SteamManifestReader.Parse(Encode(sampleRequest, []), sampleRequest).Files.Length == 0, "An empty depot manifest cannot be parsed.");
        Reject(() => SteamManifestReader.Parse(valid, sampleRequest with { ManifestId = "1" }), "Wrong manifest ID accepted.");
        Reject(() => SteamManifestReader.Parse(Encode(sampleRequest, Files(sampleRequest), true), sampleRequest), "Encrypted file names accepted.");
        Reject(() => SteamManifestReader.Parse(valid[..^1], sampleRequest), "Truncated manifest accepted.");
        byte[] corrupt = (byte[])valid.Clone(); corrupt[15] ^= 1;
        Reject(() => SteamManifestReader.Parse(corrupt, sampleRequest), "Payload corruption escaped CRC verification.");
        Reject(() => SteamManifestReader.Parse(Encode(sampleRequest, [new("../outside.exe", 1, Hash("x"))]), sampleRequest), "Traversal file accepted.");
        Reject(() => SteamManifestReader.Parse(Encode(sampleRequest, [new(".DepotDownloader/state", 1, Hash("x"))]), sampleRequest), "Manifest can overwrite downloader state.");
        Reject(() => SteamManifestReader.Parse(Encode(sampleRequest, [new("same", 1, Hash("x")), new("SAME", 1, Hash("y"))]), sampleRequest), "Duplicate depot paths accepted.");
        string corruptFolder = Path.Combine(work, "corrupt"); WriteCache(corruptFolder, sampleRequest, valid);
        File.WriteAllBytes(Path.Combine(corruptFolder, $"{sampleRequest.DepotId}_{sampleRequest.ManifestId}.manifest.sha"), new byte[20]);
        Reject(() => SteamManifestReader.Load(Path.Combine(corruptFolder, $"{sampleRequest.DepotId}_{sampleRequest.ManifestId}.manifest"), sampleRequest), "Invalid sidecar SHA-1 accepted.");
        int repairFetch = 0;
        var repairing = new SteamManifestCache(Path.Combine(work, "repaired"), [corruptFolder, targetOnly], (requests, output, token) => { repairFetch++; return Fetch(requests, output, token); });
        Check(Wait(repairing.SnapshotAsync(Ids(catalog[1]!), CancellationToken.None)).Length == 8, "Invalid cache prevented fallback to another valid cache.");
        Check(repairFetch == 0, "Valid alternative cache was not reused.");
        var cmd = DepotDownloadCommand.Create("DepotDownloader.exe", [782332], [sampleRequest.ManifestId], null, false, "fixture", false, "metadata-cache", manifestOnly: true);
        Check(cmd.ArgumentList.Contains("-manifest-only") && !cmd.ArgumentList.Contains("-filelist") && !cmd.ArgumentList.Contains("-password"), "Metadata preparation command is not manifest-only or leaks a password.");

        // Independent real fingerprint fixture (from all eight source/target depots).
        JsonNode evidence = JsonNode.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "fixtures", "rollback-25216728-to-1.1.json")))!;
        SteamManifest[] beforeReal = [], afterReal = [];
        var sourceSet = new List<SteamManifest>(); var targetSet = new List<SteamManifest>();
        foreach (int depot in SteamManifestCache.DepotIds)
        {
            var rows = evidence["files"]!.AsArray().Where(row => row!["depot"]!.GetValue<int>() == depot).Select(row => row!).ToArray();
            int index = depot - 782332;
            var targetRequest = new ManifestRequest(depot, evidence["targetManifestIds"]![index]!.ToString());
            var sourceRequest = new ManifestRequest(depot, evidence["sourceManifestIds"]![index]!.ToString());
            ManifestFile[] a = rows.Select(row => new ManifestFile(row["path"]!.ToString(), row["targetSize"]!.GetValue<long>(), row["targetSha1"]!.ToString())).ToArray();
            ManifestFile[] b = rows.Where(row => row["sourceSize"] is not null).Select(row => new ManifestFile(row["path"]!.ToString(), row["sourceSize"]!.GetValue<long>(), row["sourceSha1"]!.ToString())).ToArray();
            targetSet.Add(SteamManifestReader.Parse(Encode(targetRequest, a), targetRequest)); sourceSet.Add(SteamManifestReader.Parse(Encode(sourceRequest, b), sourceRequest));
        }
        beforeReal = sourceSet.ToArray(); afterReal = targetSet.ToArray();
        var realResult = ManifestRollbackPlanner.Compare(beforeReal, afterReal, null, Required);
        Check(realResult.Files.Length == 101 && realResult.SelectedBytes == 10360988279L && realResult.TargetBytes == 47037741036L, "Universal parser/planner differs from independently verified 1.1 data.");
        string game = Path.Combine(work, "repair-game"); Directory.CreateDirectory(game);
        foreach (var file in Effective(Ids(catalog[1]!)).Values) { string path = Path.Combine(game, file.Path); Directory.CreateDirectory(Path.GetDirectoryName(path)!); using var f = File.Create(path); f.SetLength(file.Size); }
        string missingPath = "base/stable-782333.dat"; File.Delete(Path.Combine(game, missingPath));
        var repairedSelection = Wait(ManifestRollbackPlanner.PrepareAsync(cache, Ids(catalog[1]!), Ids(catalog[1]!), game, Required, _ => { }, CancellationToken.None));
        Check(repairedSelection.Files.Contains(missingPath) && repairedSelection.Files.Contains(Required[0]), "Missing unchanged file was not selected for repair.");
        // Late target-manifest fallback must also filter stale staging files.
        string stage = Path.Combine(work, "late-stage"), targetGame = Path.Combine(work, "late-game"); Directory.CreateDirectory(stage); Directory.CreateDirectory(targetGame);
        File.WriteAllText(Path.Combine(targetGame, "DOOMEternalx64vk.exe"), "original"); File.WriteAllText(Path.Combine(stage, "stale.dat"), "stale");
        var plan = new GameDownloadPlan(false, true, true, stage, targetGame, Ids(catalog[1]!));
        var late = Wait(GameDownloadOperation.ExecuteAsync(plan, (folder, _, _) => { File.WriteAllText(Path.Combine(folder, Required[0]), "target"); return Task.CompletedTask; }, (_, _) => Task.CompletedTask,
            () => { }, _ => { }, _ => { }, CancellationToken.None, (_, _) => Task.FromResult(Required)));
        Check(late.AppliedFiles == 1 && !File.Exists(Path.Combine(targetGame, "stale.dat")), "Late manifest fallback applied unselected files.");
        var standalone = plan with { Standalone = true, GameFolder = null };
        using (var interrupted = new CancellationTokenSource())
        {
            Reject(() => Wait(GameDownloadOperation.ExecuteAsync(standalone, (folder, _, token) => { interrupted.Cancel(); return Task.FromCanceled(token); }, (_, _) => Task.CompletedTask,
                () => { }, _ => { }, _ => { }, interrupted.Token)), "Standalone cancellation reported success.");
        }
        Check(GameDownloadOperation.CanResumeStandalone(stage, Ids(catalog[1]!)) && !GameDownloadOperation.CanResumeStandalone(stage, Ids(catalog[0]!)), "Standalone resume accepts the wrong target or loses its marker.");
        foreach (string language in new[] { "en", "ru", "es" })
            foreach (string key in new[] { "ManifestPlanning", "ManifestSourceFallback", "ManifestTargetFallback", "ManifestSelectionLog", "ManifestPreparationNotice", "BackupPatchConfirmation" }) Check(MainForm.T(language, key) != key, "Missing universal preparation translation.");

        int local = Array.IndexOf(args, "--local-manifests");
        if (local >= 0)
        {
            string from = args[local + 1], to = args[local + 2]; var aa = new List<SteamManifest>(); var bb = new List<SteamManifest>();
            for (int index = 0; index < 8; index++)
            {
                var a = new ManifestRequest(SteamManifestCache.DepotIds[index], evidence["sourceManifestIds"]![index]!.ToString());
                var b = new ManifestRequest(SteamManifestCache.DepotIds[index], evidence["targetManifestIds"]![index]!.ToString());
                aa.Add(SteamManifestReader.Load(Path.Combine(from, $"{a.DepotId}_{a.ManifestId}.manifest"), a));
                bb.Add(SteamManifestReader.Load(Path.Combine(to, $"{b.DepotId}_{b.ManifestId}.manifest"), b));
            }
            var actual = ManifestRollbackPlanner.Compare(aa.ToArray(), bb.ToArray(), null, Required);
            Check(actual.Files.SequenceEqual(realResult.Files) && actual.SelectedBytes == realResult.SelectedBytes, "Real local Steam manifests disagree with the independent comparison.");
            Console.WriteLine("PASS: 16 real cached manifests read as data; source/target IDs, CRC, hashes and 101-file result match.");
        }
        Console.WriteLine($"PASS: {checks} universal manifest checks; {pairs} pairs, future snapshots, cache reuse, corruption, cancellation, repair and standalone resume. No Steam process started.");
        return checks;
    }
}
