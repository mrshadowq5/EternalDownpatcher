using System.Security.Cryptography;

namespace EternalDownpatcher.WinForms;

internal static class DownloadedPayload
{
    internal static async Task VerifyAsync(string folder, ManifestSelection selection, CancellationToken token,
        Action<int, int, string>? progress = null)
    {
        token.ThrowIfCancellationRequested();
        if (selection.Files.Length == 0) throw new InvalidDataException("No target files were selected for verification.");
        var target = selection.TargetFiles.ToDictionary(file => file.Path, StringComparer.OrdinalIgnoreCase);
        // Resolve the complete set before reading any payload, including path checks.
        var files = selection.Files.Select(relative =>
        {
            if (!target.TryGetValue(relative, out ManifestFile? file))
                throw new InvalidDataException($"Selected file missing from target manifests: {relative}");
            return (File: file, FullPath: PathSafety.Resolve(folder, relative));
        }).ToArray();
        for (int index = 0; index < files.Length; index++)
        {
            token.ThrowIfCancellationRequested();
            (ManifestFile file, string path) = files[index];
            progress?.Invoke(index + 1, files.Length, file.Path);
            token.ThrowIfCancellationRequested();
            await using FileStream input = new(path, FileMode.Open, FileAccess.Read, FileShare.Read,
                bufferSize: 128 * 1024, FileOptions.Asynchronous | FileOptions.SequentialScan);
            if (input.Length != file.Size)
                throw new InvalidDataException($"Downloaded file size does not match the target manifest: {file.Path}");
            string sha1 = Convert.ToHexString(await SHA1.HashDataAsync(input, token).ConfigureAwait(false));
            if (!sha1.Equals(file.Sha1, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException($"Downloaded file hash does not match the target manifest: {file.Path}");
        }
        token.ThrowIfCancellationRequested();
    }
}
