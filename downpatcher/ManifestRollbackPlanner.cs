using System.Security.Cryptography;
using Microsoft.Win32;

namespace EternalDownpatcher.WinForms;

internal sealed record ManifestSelection(string[] Files, ManifestFile[] TargetFiles, bool ComparedSource, long SelectedBytes, long TargetBytes);

internal sealed class SteamManifestCache
{
    internal static readonly int[] DepotIds = [782332, 782333, 782334, 782335, 782336, 782337, 782338, 782339];
    internal static string DefaultRoot => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "EternalDownpatcher", "manifests");
    private readonly string _root;
    private readonly string[] _searchFolders;
    private readonly Func<IReadOnlyList<ManifestRequest>, string, CancellationToken, Task> _fetch;
    private readonly Dictionary<ManifestRequest, SteamManifest> _loaded = [];

    internal SteamManifestCache(string root, IEnumerable<string> searchFolders,
        Func<IReadOnlyList<ManifestRequest>, string, CancellationToken, Task> fetch)
    {
        _root = Path.GetFullPath(root); _fetch = fetch;
        _searchFolders = new[] { _root }.Concat(searchFolders).Where(path => !string.IsNullOrWhiteSpace(path))
            .Select(Path.GetFullPath).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
    }

    internal static IEnumerable<string> FindExistingFolders(string? game, string output)
    {
        List<string> paths = [Path.Combine(output, ".DepotDownloader")];
        if (game is not null)
        {
            paths.Add(Path.Combine(game, ".DepotDownloader"));
            DirectoryInfo? common = Directory.GetParent(game);
            if (common?.Name.Equals("common", StringComparison.OrdinalIgnoreCase) == true && common.Parent?.Parent is DirectoryInfo library)
                paths.Add(Path.Combine(library.FullName, "depotcache"));
        }
        try
        {
            if (Registry.GetValue(@"HKEY_CURRENT_USER\Software\Valve\Steam", "SteamPath", null) is string steamPath && !string.IsNullOrWhiteSpace(steamPath))
                paths.Add(Path.Combine(steamPath, "depotcache"));
        }
        catch (Exception error) when (error is System.Security.SecurityException or UnauthorizedAccessException or IOException) { }
        return paths;
    }

    internal async Task<SteamManifest[]> SnapshotAsync(IReadOnlyList<string> ids, CancellationToken token)
    {
        string[] normalized = ids.Select(id => id?.Trim() ?? "").ToArray();
        ValidateSnapshot(normalized);
        ManifestRequest[] requests = DepotIds.Select((depot, index) => new ManifestRequest(depot, normalized[index])).ToArray();
        foreach (ManifestRequest request in requests)
        {
            token.ThrowIfCancellationRequested();
            if (_loaded.ContainsKey(request)) continue;
            SteamManifest? manifest = await Task.Run(() => Find(request, token), token);
            if (manifest is not null) _loaded[request] = manifest;
        }
        ManifestRequest[] missing = requests.Where(request => !_loaded.ContainsKey(request)).ToArray();
        if (missing.Length > 0)
        {
            PathSafety.RejectLinks(_root); Directory.CreateDirectory(_root);
            string staging = Path.Combine(_root, "download-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(staging);
            try
            {
                // Every batch contains a depot only once: DepotDownloader reject
                // duplicate depot IDs, even when paired with distinct manifests
                await _fetch(missing, staging, token);
                foreach (ManifestRequest request in missing)
                {
                    token.ThrowIfCancellationRequested();
                    string path = Path.Combine(staging, ".DepotDownloader", FileName(request));
                    SteamManifest parsed = await Task.Run(() => SteamManifestReader.Load(path, request, token), token);
                    byte[] bytes = await File.ReadAllBytesAsync(path, token);
                    await SaveAsync(request, bytes, token);
                    _loaded[request] = parsed;
                }
            }
            finally { Cleanup(staging); }
        }
        return requests.Select(request => _loaded[request]).ToArray();
    }

    private SteamManifest? Find(ManifestRequest request, CancellationToken token)
    {
        foreach (string folder in _searchFolders)
        {
            token.ThrowIfCancellationRequested();
            string path = Path.Combine(folder, FileName(request));
            try { if (File.Exists(path)) return SteamManifestReader.Load(path, request, token); }
            catch (Exception error) when (error is IOException or InvalidDataException or UnauthorizedAccessException or ArgumentException or InvalidOperationException) { }
        }
        return null;
    }
    private async Task SaveAsync(ManifestRequest request, byte[] bytes, CancellationToken token)
    {
        string path = PathSafety.Resolve(_root, FileName(request));
        string checksum = PathSafety.Resolve(_root, FileName(request) + ".sha");
        string temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        string checksumTemporary = temporary + ".sha";
        try
        {
            await File.WriteAllBytesAsync(temporary, bytes, token);
            await File.WriteAllBytesAsync(checksumTemporary, SHA1.HashData(bytes), token);
            token.ThrowIfCancellationRequested();
            File.Move(temporary, path, true);
            File.Move(checksumTemporary, checksum, true);
        }
        finally
        {
            if (File.Exists(temporary)) File.Delete(temporary);
            if (File.Exists(checksumTemporary)) File.Delete(checksumTemporary);
        }
    }
    private void Cleanup(string staging)
    {
        try
        {
            if (!PathSafety.Contains(_root, staging) || Path.GetFileName(staging) is not string name ||
                !name.StartsWith("download-", StringComparison.Ordinal) || name.Length != 41) return;
            PathSafety.RejectLinks(staging);
            foreach (string path in Directory.EnumerateFileSystemEntries(staging, "*", SearchOption.AllDirectories)) PathSafety.RejectLinks(path);
            Directory.Delete(staging, recursive: true);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or InvalidOperationException) { }
    }
    private static string FileName(ManifestRequest request) => $"{request.DepotId}_{request.ManifestId}.manifest";
    internal static void ValidateSnapshot(IReadOnlyList<string> ids)
    {
        if (ids.Count != DepotIds.Length || ids.Any(id => !ulong.TryParse(id, System.Globalization.NumberStyles.None,
                System.Globalization.CultureInfo.InvariantCulture, out ulong value) || value == 0))
            throw new ArgumentException("Eight valid manifest IDs are required.");
    }
}

internal static class ManifestRollbackPlanner
{
    internal static async Task<ManifestSelection> PrepareAsync(SteamManifestCache cache, string[]? sourceIds, string[] targetIds,
        string? game, string[] requiredPaths, Action<string> sourceUnavailable, CancellationToken token)
    {
        SteamManifest[] target = await cache.SnapshotAsync(targetIds, token);
        SteamManifest[]? source = null;
        if (sourceIds is not null)
        {
            try { source = await cache.SnapshotAsync(sourceIds, token); }
            catch (Exception error) when (!token.IsCancellationRequested && error is IOException or InvalidDataException or UnauthorizedAccessException or InvalidOperationException)
            { sourceUnavailable(error.Message); }
        }
        return await Task.Run(() => Compare(source, target, game, requiredPaths, token), token);
    }

    internal static ManifestSelection Compare(SteamManifest[]? source, SteamManifest[] target, string? game,
        string[] requiredPaths, CancellationToken token = default)
    {
        Dictionary<string, ManifestFile> desired = Merge(target), current = source is null ? new(StringComparer.OrdinalIgnoreCase) : Merge(source);
        HashSet<string> selected = new(StringComparer.OrdinalIgnoreCase);
        foreach (ManifestFile file in desired.Values)
        {
            token.ThrowIfCancellationRequested();
            bool changed = source is null || !current.TryGetValue(file.Path, out ManifestFile? old) || old.Size != file.Size || old.Sha1 != file.Sha1;
            if (!changed && game is not null)
            {
                string path = PathSafety.Resolve(game, file.Path);
                changed = !File.Exists(path) || new FileInfo(path).Length != file.Size;
            }
            if (changed) selected.Add(file.Path);
        }
        foreach (string required in requiredPaths)
        {
            string path = SteamManifestReader.NormalizePath(required);
            if (!desired.ContainsKey(path)) throw new InvalidDataException($"Required file missing from target manifests: {path}");
            selected.Add(path);
        }
        string[] files = selected.OrderBy(path => path, StringComparer.OrdinalIgnoreCase).ToArray();
        return new(files, desired.Values.OrderBy(file => file.Path, StringComparer.OrdinalIgnoreCase).ToArray(), source is not null,
            files.Sum(path => desired[path].Size), desired.Values.Sum(file => file.Size));
    }

    private static Dictionary<string, ManifestFile> Merge(SteamManifest[] manifests)
    {
        if (manifests.Length != SteamManifestCache.DepotIds.Length ||
            manifests.Where((manifest, index) => manifest.DepotId != SteamManifestCache.DepotIds[index]).Any())
            throw new InvalidDataException("A complete ordered set of eight depot manifests is required.");
        Dictionary<string, ManifestFile> files = new(StringComparer.OrdinalIgnoreCase);
        foreach (SteamManifest manifest in manifests)
            foreach (ManifestFile file in manifest.Files)
            {
                string path = SteamManifestReader.NormalizePath(file.Path);
                if (file.Size < 0 || file.Sha1.Length != 40 || file.Sha1.Any(character => !Uri.IsHexDigit(character)))
                    throw new InvalidDataException("Invalid manifest file identity.");
                // Match DepotDownloader's last-depot-wins handling of shared paths
                files[path] = file with { Path = path, Sha1 = file.Sha1.ToUpperInvariant() };
            }
        if (files.Count == 0) throw new InvalidDataException("The target manifests contain no files.");
        return files;
    }
}
