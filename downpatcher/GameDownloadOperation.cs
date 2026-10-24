using System.Text.Json;

namespace EternalDownpatcher.WinForms;

internal sealed record GameDownloadPlan(bool Standalone, bool CreateBackup, bool ValidateFiles,
    string OutputFolder, string? GameFolder, string[] ManifestIds, string[]? FilesToApply = null)
{
    internal bool Direct => !Standalone && !CreateBackup;
    internal string DownloadFolder => Direct ? GameFolder ?? throw new InvalidOperationException("Select the game folder.") : OutputFolder;
    internal bool VerifyExistingFiles => Direct || ValidateFiles;
}

internal sealed record GameDownloadResult(string? BackupFolder, int AppliedFiles);

internal static class GameDownloadOperation
{
    private const string PendingName = ".DepotDownloader/eternaldownpatcher-pending.json";
    private sealed record PendingUpdate(int FormatVersion, string[] ManifestIds);

    internal static string[]? ReadPending(string gameFolder)
    {
        try
        {
            string path = PathSafety.Resolve(gameFolder, PendingName);
            if (!File.Exists(path) || new FileInfo(path).Length > 4096) return null;
            PendingUpdate? pending = JsonSerializer.Deserialize<PendingUpdate>(File.ReadAllText(path));
            return pending is { FormatVersion: 1, ManifestIds.Length: 8 } &&
                pending.ManifestIds.All(id => ulong.TryParse(id, out ulong number) && number != 0) ? pending.ManifestIds : null;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or JsonException or InvalidOperationException)
        { return null; }
    }

    internal static void ClearPending(string gameFolder)
    {
        string path = PathSafety.Resolve(gameFolder, PendingName);
        if (File.Exists(path)) File.Delete(path);
    }

    internal static async Task<GameDownloadResult> ExecuteAsync(GameDownloadPlan plan,
        Func<string, bool, CancellationToken, Task> download,
        Func<string, CancellationToken, Task> verifyDownloaded,
        Action verifySource, Action<string> log, Action<int> progress, CancellationToken cancellationToken,
        Func<string, CancellationToken, Task<string[]>>? resolveAppliedFiles = null)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (plan.ManifestIds.Length != 8 || plan.ManifestIds.Any(id => !ulong.TryParse(id, out ulong number) || number == 0))
            throw new ArgumentException("Eight valid target manifests are required.");
        string folder = Path.GetFullPath(plan.DownloadFolder);
        PathSafety.RejectLinks(folder);
        if (plan.Direct)
        {
            ValidateDirectFolder(folder);
            string[]? pending = ReadPending(folder);
            if (pending is not null && !pending.SequenceEqual(plan.ManifestIds))
                throw new InvalidOperationException("Finish the interrupted version before selecting another target.");
            string state = PathSafety.Resolve(folder, PendingName);
            Directory.CreateDirectory(Path.GetDirectoryName(state)!);
            string temporary = state + ".tmp";
            try
            {
                File.WriteAllText(temporary, JsonSerializer.Serialize(new PendingUpdate(1, plan.ManifestIds)));
                File.Move(temporary, state, overwrite: true);
            }
            finally { if (File.Exists(temporary)) File.Delete(temporary); }
        }
        else if (!plan.Standalone)
        {
            PathSafety.RequireSeparate(folder, plan.GameFolder!);
        }
        if (plan.Standalone) SaveStandaloneState(folder, plan.ManifestIds);
        await download(folder, plan.VerifyExistingFiles, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        await verifyDownloaded(folder, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        if (plan.Direct)
        {
            ClearPending(folder);
            return new(null, 0);
        }
        if (plan.Standalone) return new(null, 0);
        string[]? filesToApply = plan.FilesToApply;
        if (filesToApply is null && resolveAppliedFiles is not null)
            filesToApply = await resolveAppliedFiles(folder, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        verifySource();
        string game = Path.GetFullPath(plan.GameFolder!);
        string backup = Path.Combine(game, "EternalDownpatcher_Backup_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + "_" + Guid.NewGuid().ToString("N")[..8]);
        log($"Backup folder: {backup}");
        log("Applying patch files...");
        int count = await Task.Run(() => PatchBackup.Apply(folder, game, backup, cancellationToken, progress, filesToApply), cancellationToken);
        return new(backup, count);
    }

    private const string StandaloneState = ".DepotDownloader/eternaldownpatcher-download.json";

    internal static bool CanResumeStandalone(string folder, string[] manifests)
    {
        try
        {
            string path = PathSafety.Resolve(folder, StandaloneState);
            if (!File.Exists(path) || new FileInfo(path).Length > 4096) return false;
            PendingUpdate? state = JsonSerializer.Deserialize<PendingUpdate>(File.ReadAllText(path));
            return state is { FormatVersion: 1, ManifestIds.Length: 8 } && state.ManifestIds.SequenceEqual(manifests);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or JsonException or InvalidOperationException) { return false; }
    }

    private static void SaveStandaloneState(string folder, string[] manifests)
    {
        string path = PathSafety.Resolve(folder, StandaloneState);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        string temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            File.WriteAllText(temporary, JsonSerializer.Serialize(new PendingUpdate(1, manifests)));
            File.Move(temporary, path, overwrite: true);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }

    private static void ValidateDirectFolder(string folder)
    {
        if (!Directory.Exists(folder)) throw new DirectoryNotFoundException("The selected game folder does not exist.");
        //DepotDownloader writes into this tree. do not follow links out of it
        Stack<string> directories = new(); directories.Push(folder);
        while (directories.Count > 0)
            foreach (string path in Directory.EnumerateFileSystemEntries(directories.Pop()))
            {
                PathSafety.RejectLinks(path);
                if (Directory.Exists(path)) directories.Push(path);
            }
    }
}