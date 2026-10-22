using System.Text.Json;

namespace EternalDownpatcher.WinForms;

#region Backup apply and restore

internal static class PatchBackup
{
    internal const string ManifestName = "eternaldownpatcher-backup.json";

    internal sealed record Entry(string Path, bool Existed);
    internal sealed record Manifest(int FormatVersion, Entry[] Files, string[] CreatedDirectories);

    public static int Apply(string source, string target, string backup,
        CancellationToken cancellationToken, Action<int>? progress = null)
    {
        PathSafety.RequireSeparate(source, target);
        PathSafety.RejectLinks(source);
        PathSafety.RejectLinks(target);
        PathSafety.RejectLinks(backup);
        if (!Directory.Exists(target) || !PathSafety.Contains(target, backup) ||
            string.Equals(Path.GetFullPath(target), Path.GetFullPath(backup), StringComparison.OrdinalIgnoreCase) ||
            Directory.Exists(backup))
            throw new InvalidOperationException("A new backup folder inside the game folder is required.");

        Entry[] files = EnumeratePayload(source).Select(path =>
        {
            string destination = PathSafety.Resolve(target, path);
            if (PathSafety.Contains(backup, destination) || PathSafety.Contains(destination, backup))
                throw new IOException("A patch file overlaps the backup folder.");
            if (Directory.Exists(destination))
                throw new IOException($"A folder blocks the destination file: {destination}");
            return new Entry(path, File.Exists(destination));
        }).ToArray();
        if (files.Length == 0)
            throw new InvalidOperationException("No game files were downloaded. The installation was not changed.");

        HashSet<string> newDirectories = new(StringComparer.OrdinalIgnoreCase);
        foreach (Entry file in files)
        {
            for (string? parent = Path.GetDirectoryName(PathSafety.Resolve(target, file.Path));
                 parent is not null && !Directory.Exists(parent); parent = Path.GetDirectoryName(parent))
                newDirectories.Add(Path.GetRelativePath(target, parent));
        }

        string originals = Path.Combine(backup, "files");
        // Finish every backup before replacing any game file.
        Directory.CreateDirectory(originals);
        Manifest manifest = new(1, files, newDirectories.OrderByDescending(path => path.Length).ToArray());
        // Mark the new format before copying, so an interrupted backup is never treated as legacy.
        File.WriteAllText(Path.Combine(backup, ManifestName), JsonSerializer.Serialize(manifest,
            new JsonSerializerOptions { WriteIndented = true }));
        foreach (Entry file in files.Where(file => file.Existed))
        {
            cancellationToken.ThrowIfCancellationRequested();
            CopyFile(PathSafety.Resolve(target, file.Path), PathSafety.Resolve(originals, file.Path));
        }
        List<Entry> completed = [];
        try
        {
            foreach (Entry file in files)
            {
                cancellationToken.ThrowIfCancellationRequested();
                CopyFile(PathSafety.Resolve(source, file.Path), PathSafety.Resolve(target, file.Path));
                completed.Add(file);
                progress?.Invoke(completed.Count);
            }
            cancellationToken.ThrowIfCancellationRequested();
            return completed.Count;
        }
        catch (Exception applyError)
        {
            try
            {
                RestoreManifest(backup, target, manifest with { Files = completed.ToArray() });
            }
            catch (Exception restoreError)
            {
                throw new IOException($"Patch failed and automatic restoration could not finish. " +
                    $"Keep the backup at {backup}. Patch: {applyError.Message}; restore: {restoreError.Message}", applyError);
            }
            throw;
        }
    }

    public static bool Restore(string backup, string target)
    {
        PathSafety.RejectLinks(backup);
        PathSafety.RejectLinks(target);
        if (PathSafety.Contains(backup, target))
            throw new InvalidOperationException("The restore target cannot be inside the backup folder.");
        string manifestPath = Path.Combine(backup, ManifestName);
        if (File.Exists(manifestPath))
        {
            PathSafety.RejectLinks(manifestPath);
            Manifest manifest = JsonSerializer.Deserialize<Manifest>(File.ReadAllText(manifestPath))
                ?? throw new InvalidOperationException("Invalid backup manifest.");
            RestoreManifest(backup, target, manifest);
            return true;
        }

        // Older backups contain originals directly, without a record of added files.
        string[] legacyFiles = EnumeratePayload(backup).ToArray();
        if (legacyFiles.Length == 0)
            throw new InvalidOperationException("The backup contains no files.");
        foreach (string file in legacyFiles)
            PathSafety.Resolve(target, file);
        foreach (string file in legacyFiles)
            CopyFile(PathSafety.Resolve(backup, file), PathSafety.Resolve(target, file));
        return false;
    }

    private static void RestoreManifest(string backup, string target, Manifest manifest)
    {
        if (manifest.FormatVersion != 1 || manifest.Files is null || manifest.CreatedDirectories is null)
            throw new InvalidOperationException("Unsupported or incomplete backup manifest.");
        string originals = Path.Combine(backup, "files");
        HashSet<string> paths = new(StringComparer.OrdinalIgnoreCase);
        // Validate the whole plan before changing the installation.
        foreach (Entry file in manifest.Files)
        {
            string destination = PathSafety.Resolve(target, file.Path);
            if (!paths.Add(destination) || Directory.Exists(destination) || PathSafety.Contains(backup, destination))
                throw new InvalidOperationException($"Invalid restore destination: {file.Path}");
            if (file.Existed && !File.Exists(PathSafety.Resolve(originals, file.Path)))
                throw new FileNotFoundException($"Missing backup file: {file.Path}");
        }
        foreach (string directory in manifest.CreatedDirectories)
        {
            string destination = PathSafety.Resolve(target, directory);
            if (PathSafety.Contains(backup, destination))
                throw new InvalidOperationException("The backup cannot be a restore destination.");
        }
        foreach (Entry file in manifest.Files.Reverse())
        {
            string destination = PathSafety.Resolve(target, file.Path);
            if (file.Existed)
                CopyFile(PathSafety.Resolve(originals, file.Path), destination);
            else if (File.Exists(destination))
                File.Delete(destination);
        }
        foreach (string directory in manifest.CreatedDirectories.OrderByDescending(path => path.Length))
        {
            string path = PathSafety.Resolve(target, directory);
            if (Directory.Exists(path) && !Directory.EnumerateFileSystemEntries(path).Any())
                Directory.Delete(path);
        }
    }

    private static IEnumerable<string> EnumeratePayload(string root)
    {
        foreach (string path in Directory.EnumerateFileSystemEntries(root).Order(StringComparer.OrdinalIgnoreCase))
        {
            if (string.Equals(Path.GetFileName(path), ".DepotDownloader", StringComparison.OrdinalIgnoreCase))
                continue;
            PathSafety.RejectLinks(path);
            if (Directory.Exists(path))
            {
                foreach (string file in EnumeratePayload(path))
                    yield return Path.GetRelativePath(root, Path.Combine(path, file));
            }
            else
                yield return Path.GetRelativePath(root, path);
        }
    }

    private static void CopyFile(string source, string target)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(target)!);
        string temporary = target + ".eternaldownpatcher-" + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            File.Copy(source, temporary);
            File.Move(temporary, target, true);
        }
        finally
        {
            if (File.Exists(temporary))
            {
                File.SetAttributes(temporary, File.GetAttributes(temporary) & ~FileAttributes.ReadOnly);
                File.Delete(temporary);
            }
        }
    }
}

#endregion

#region Path checks

internal static class PathSafety
{
    public static bool Contains(string parent, string path)
    {
        string root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(parent));
        string candidate = Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));
        return string.Equals(root, candidate, StringComparison.OrdinalIgnoreCase) ||
            candidate.StartsWith(Path.EndsInDirectorySeparator(root) ? root : root + Path.DirectorySeparatorChar,
                StringComparison.OrdinalIgnoreCase);
    }

    public static void RequireSeparate(string first, string second)
    {
        if (Contains(first, second) || Contains(second, first))
            throw new InvalidOperationException("Choose separate folders that are not inside each other.");
    }

    public static void RejectLinks(string path)
    {
        for (string? current = Path.GetFullPath(path); current is not null;
             current = Path.GetDirectoryName(current))
        {
            if ((File.Exists(current) || Directory.Exists(current)) &&
                (File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
                throw new InvalidOperationException($"Linked files and folders are not supported: {current}");
        }
    }

    public static string Resolve(string root, string relativePath)
    {
        string[] parts = relativePath.Split(['/', '\\']);
        if (string.IsNullOrWhiteSpace(relativePath) || Path.IsPathRooted(relativePath) ||
            parts.Any(part => part is "" or "." or ".." || part.EndsWith(' ') || part.EndsWith('.') ||
                part.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0))
            throw new InvalidOperationException($"Invalid backup path: {relativePath}");

        string path = Path.GetFullPath(Path.Combine(root, relativePath));
        if (!Contains(root, path))
            throw new InvalidOperationException($"Path is outside its folder: {relativePath}");
        RejectLinks(path);
        return path;
    }

    public static void ValidateOutput(string output, string appFolder, string? gameFolder)
    {
        string full = Path.GetFullPath(output);
        if (string.Equals(Path.TrimEndingDirectorySeparator(full),
                Path.TrimEndingDirectorySeparator(Path.GetPathRoot(full)!), StringComparison.OrdinalIgnoreCase) ||
            Contains(full, appFolder))
            throw new InvalidOperationException("Choose an output folder separate from the application folder and drive root.");
        RejectLinks(full);
        if (gameFolder is not null)
            RequireSeparate(full, gameFolder);
    }
}

#endregion
