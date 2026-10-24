using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Xml;
using System.Xml.Linq;

namespace EternalDownpatcher.WinForms;

#region Discovery installation and launch

public sealed partial class MainForm : Form
{
    private static string? FindDepotDownloaderPath()
    {
        return DepotDownloaderSetup.FindInstalled(GetAppWorkingFolder(), AppContext.BaseDirectory);
    }

    private async Task RunDepotDownloaderAsync(string depotDownloaderPath, IReadOnlyList<int> depotIds, IReadOnlyList<string> manifestIds, string? fileListPath, bool useFileList, bool validateFiles, string username, string password, bool rememberPassword, string outputFolder, CancellationToken cancellationToken, bool manifestOnly = false)
    {
        if (useFileList && string.IsNullOrWhiteSpace(fileListPath))
            throw new InvalidOperationException("Filelist path is empty.");
        ProcessStartInfo startInfo = DepotDownloadCommand.Create(depotDownloaderPath, depotIds,
            manifestIds, useFileList ? fileListPath : null, validateFiles, username, rememberPassword, outputFolder, manifestOnly);
        int exitCode = await DepotDownloaderRunner.RunAsync(startInfo, password, RequestSteamGuardCodeAsync,
            message => WriteLogThreadSafe($"DepotDownloader> {message}"), cancellationToken);
        if (exitCode != 0)
        {
            throw new InvalidOperationException(LF("DepotFailed", exitCode));
        }
    }
}

#endregion

#region Release discovery and installation

internal sealed record DepotDownloaderRelease(string Tag, string Name, string Notes, DateTimeOffset? Published)
{
    public string ReleaseUrl => $"{DepotDownloaderSetup.RepositoryUrl}/releases/tag/{Uri.EscapeDataString(Tag)}";
    public string DownloadUrl => $"{DepotDownloaderSetup.RepositoryUrl}/releases/download/{Uri.EscapeDataString(Tag)}/DepotDownloader-windows-x64.zip";
}

internal sealed record DepotSetupProgress(string Stage, int? Percent = null);

internal static class DepotDownloaderSetup
{
    internal const string RepositoryUrl = "https://github.com/SteamRE/DepotDownloader";
    private const string MarkerName = ".eternaldownpatcher-release.json";
    private const long MaxArchiveBytes = 128L * 1024 * 1024;
    private const long MaxExtractedBytes = 512L * 1024 * 1024;
    private sealed record InstalledRelease(string Tag, string ExecutableSha256);

    internal static HttpClient CreateClient()
    {
        HttpClient client = new() { Timeout = TimeSpan.FromMinutes(5) };
        client.DefaultRequestHeaders.UserAgent.ParseAdd($"EternalDownpatcher/{MainForm.AppVersion}");
        return client;
    }

    internal static void EnsureStartupFolder(string root) =>
        Directory.CreateDirectory(Path.Combine(root, "DOWNPATCH_FILES"));

    internal static string? FindInstalled(string root, string? baseDirectory = null)
    {
        // Prefer the managed installation after upgrading a legacy standalone executable.
        string[] candidates = [Path.Combine(root, "DepotDownloader", "DepotDownloader.exe"),
            Path.Combine(root, "DepotDownloader.exe"), Path.Combine(root, "DepotDownloader-2.4.6", "DepotDownloader.exe"),
            Path.Combine(baseDirectory ?? root, "DepotDownloader", "DepotDownloader.exe"),
            Path.Combine(baseDirectory ?? root, "DepotDownloader.exe")];
        return candidates.FirstOrDefault(File.Exists) is string path ? Path.GetFullPath(path) : null;
    }

    internal static async Task<DepotDownloaderRelease> GetLatestReleaseAsync(HttpClient client,
        bool includeNotes, CancellationToken cancellationToken)
    {
        // GitHub's release pages and Atom feed do not use the REST API quota.
        using HttpResponseMessage response = await client.GetAsync(RepositoryUrl + "/releases/latest",
            HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        Uri? location = response.RequestMessage?.RequestUri;
        const string prefix = "/SteamRE/DepotDownloader/releases/tag/";
        if (location is null || location.Host != "github.com" ||
            !location.AbsolutePath.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("GitHub did not return a DepotDownloader release page.");
        string tag = Uri.UnescapeDataString(location.AbsolutePath[prefix.Length..]);
        if (!Regex.IsMatch(tag, @"\A[A-Za-z0-9._+\-]+\z"))
            throw new InvalidDataException("GitHub returned an invalid release tag.");
        DepotDownloaderRelease release = new(tag, tag, "", null);
        if (!includeNotes) return release;
        try
        {
            using HttpResponseMessage feedResponse = await client.GetAsync(RepositoryUrl + "/releases.atom",
                HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
            feedResponse.EnsureSuccessStatusCode();
            using MemoryStream buffer = new();
            await CopyResponseAsync(feedResponse, buffer, 2 * 1024 * 1024, null, cancellationToken).ConfigureAwait(false);
            buffer.Position = 0;
            using XmlReader reader = XmlReader.Create(buffer, new XmlReaderSettings
                { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null });
            XDocument feed = XDocument.Load(reader);
            XNamespace atom = "http://www.w3.org/2005/Atom";
            XElement? entry = feed.Descendants(atom + "entry").FirstOrDefault(item =>
                item.Elements(atom + "link").Any(link => (string?)link.Attribute("href") == release.ReleaseUrl));
            if (entry is not null)
            {
                string notes = (string?)entry.Element(atom + "content") ?? "";
                notes = Regex.Replace(notes, @"<li\b[^>]*>", "- ", RegexOptions.IgnoreCase);
                notes = Regex.Replace(notes, @"<br\s*/?>|</(?:p|div|li|h[1-6]|pre)>", "\n", RegexOptions.IgnoreCase);
                notes = WebUtility.HtmlDecode(Regex.Replace(notes, "<[^>]+>", "")).Trim();
                DateTimeOffset? published = DateTimeOffset.TryParse((string?)entry.Element(atom + "published") ??
                    (string?)entry.Element(atom + "updated"), out DateTimeOffset date) ? date : null;
                string name = (string?)entry.Element(atom + "title") ?? tag;
                release = release with { Name = name[..Math.Min(200, name.Length)],
                    Notes = notes[..Math.Min(40000, notes.Length)], Published = published };
            }
        }
        catch (Exception error) when (!cancellationToken.IsCancellationRequested &&
            error is HttpRequestException or XmlException or InvalidDataException)
        {
            // The release link still gives access to the full notes if the feed is unavailable.
        }
        return release;
    }

    internal static string? GetInstalledVersion(string executable)
    {
        if (!File.Exists(executable)) return null;
        try
        {
            string markerPath = Path.Combine(Path.GetDirectoryName(executable)!, MarkerName);
            if (File.Exists(markerPath))
            {
                InstalledRelease? marker = JsonSerializer.Deserialize<InstalledRelease>(File.ReadAllText(markerPath));
                if (marker is not null && marker.ExecutableSha256 == HashExecutable(executable))
                    return marker.Tag;
            }
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or JsonException) { }
        try
        {
            string? version = FileVersionInfo.GetVersionInfo(executable).ProductVersion;
            return ParseVersion(version) is not null ? version : null;
        }
        catch (Exception error) when (error is IOException or System.ComponentModel.Win32Exception) { return null; }
    }

    internal static bool HasUpdate(string? installed, DepotDownloaderRelease latest)
    {
        if (string.Equals(installed, latest.Tag, StringComparison.OrdinalIgnoreCase)) return false;
        Version? currentVersion = ParseVersion(installed);
        Version? latestVersion = ParseVersion(latest.Tag);
        return currentVersion is null || latestVersion is null || latestVersion > currentVersion;
    }

    private static Version? ParseVersion(string? value)
    {
        Match match = Regex.Match(value ?? "", @"\d+\.\d+(?:\.\d+){0,2}");
        return match.Success && Version.TryParse(match.Value, out Version? version) ? version : null;
    }

    internal static async Task<string> PrepareAsync(string root, HttpClient client,
        IProgress<DepotSetupProgress>? progress, CancellationToken cancellationToken)
    {
        EnsureStartupFolder(root);
        string? existing = FindInstalled(root, AppContext.BaseDirectory);
        if (existing is not null) return existing;
        progress?.Report(new("DepotDownloading"));
        DepotDownloaderRelease release = await GetLatestReleaseAsync(client, false, cancellationToken).ConfigureAwait(false);
        return await InstallAsync(root, release, client, progress, cancellationToken).ConfigureAwait(false);
    }

    internal static async Task<string> InstallAsync(string root, DepotDownloaderRelease release, HttpClient client,
        IProgress<DepotSetupProgress>? progress, CancellationToken cancellationToken)
    {
        root = Path.GetFullPath(root);
        string staging = Path.Combine(root, ".DepotDownloader-install-" + Guid.NewGuid().ToString("N"));
        string backup = Path.Combine(root, ".DepotDownloader-backup-" + Guid.NewGuid().ToString("N"));
        string target = Path.Combine(root, "DepotDownloader");
        PathSafety.RejectLinks(root);
        PathSafety.RejectLinks(target);
        Directory.CreateDirectory(staging);
        try
        {
            string zipPath = Path.Combine(staging, "download.zip");
            using (HttpResponseMessage response = await client.GetAsync(release.DownloadUrl,
                HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false))
            {
                response.EnsureSuccessStatusCode();
                await using FileStream zip = File.Create(zipPath);
                await CopyResponseAsync(response, zip, MaxArchiveBytes, progress, cancellationToken).ConfigureAwait(false);
            }
            progress?.Report(new("DepotInstalling"));
            string extracted = Path.Combine(staging, "extracted");
            await Task.Run(() => ExtractArchive(zipPath, extracted, cancellationToken), cancellationToken).ConfigureAwait(false);
            string[] executables = Directory.GetFiles(extracted, "DepotDownloader.exe", SearchOption.AllDirectories);
            if (executables.Length != 1 || new FileInfo(executables[0]).Length == 0)
                throw new InvalidDataException("The archive must contain one non-empty DepotDownloader.exe.");
            string payload = Path.GetDirectoryName(executables[0])!;
            string? previous = FindInstalled(root, AppContext.BaseDirectory);
            if (previous is not null)
            {
                string account = Path.Combine(Path.GetDirectoryName(previous)!, "account.config");
                if (File.Exists(account))
                {
                    PathSafety.RejectLinks(account);
                    File.Copy(account, Path.Combine(payload, "account.config"), true);
                }
            }
            string marker = JsonSerializer.Serialize(new InstalledRelease(release.Tag, HashExecutable(executables[0])));
            await File.WriteAllTextAsync(Path.Combine(payload, MarkerName), marker, cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            // Commit only a complete installation. A failed replacement restores the previous directory.
            bool movedPrevious = Directory.Exists(target);
            if (movedPrevious) Directory.Move(target, backup);
            try { Directory.Move(payload, target); }
            catch
            {
                if (movedPrevious) Directory.Move(backup, target);
                throw;
            }
            TryDeleteDirectory(backup);
            return Path.Combine(target, "DepotDownloader.exe");
        }
        finally { TryDeleteDirectory(staging); }
    }

    private static async Task CopyResponseAsync(HttpResponseMessage response, Stream destination, long limit,
        IProgress<DepotSetupProgress>? progress, CancellationToken cancellationToken)
    {
        long? expected = response.Content.Headers.ContentLength;
        if (expected > limit) throw new InvalidDataException("The GitHub download is too large.");
        await using Stream source = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        byte[] buffer = new byte[65536];
        long total = 0;
        int? previousPercent = -1;
        int read;
        while ((read = await source.ReadAsync(buffer, cancellationToken).ConfigureAwait(false)) > 0)
        {
            total += read;
            if (total > limit) throw new InvalidDataException("The GitHub download is too large.");
            await destination.WriteAsync(buffer.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
            int? percent = expected > 0 ? (int)Math.Min(100, total * 100 / expected.Value) : null;
            if (percent != previousPercent)
            {
                progress?.Report(new("DepotDownloading", percent));
                previousPercent = percent;
            }
        }
        if (expected is not null && total != expected)
            throw new InvalidDataException("The GitHub download is incomplete.");
    }

    private static void ExtractArchive(string zipPath, string destination, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(destination);
        using ZipArchive archive = ZipFile.OpenRead(zipPath);
        if (archive.Entries.Count > 512) throw new InvalidDataException("The archive contains too many files.");
        string prefix = Path.GetFullPath(destination) + Path.DirectorySeparatorChar;
        long total = 0;
        foreach (ZipArchiveEntry entry in archive.Entries)
        {
            cancellationToken.ThrowIfCancellationRequested();
            total += entry.Length;
            string path = Path.GetFullPath(Path.Combine(destination, entry.FullName));
            if (!path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) ||
                ((entry.ExternalAttributes >> 16) & 0xF000) == 0xA000 || total > MaxExtractedBytes ||
                entry.FullName.Contains(':') || Path.IsPathRooted(entry.FullName))
                throw new InvalidDataException("The archive contains an unsafe entry.");
            if (entry.FullName.EndsWith('/') || entry.FullName.EndsWith('\\')) Directory.CreateDirectory(path);
            else
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                entry.ExtractToFile(path, false);
            }
        }
    }

    private static string HashExecutable(string path)
    {
        using FileStream file = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(file));
    }

    private static void TryDeleteDirectory(string path)
    {
        try { if (Directory.Exists(path)) Directory.Delete(path, true); }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException) { }
    }
}

#endregion

#region Startup and update windows

internal static class DepotDownloaderStartup
{
    internal static bool Prepare(string root, UserSettings settings)
    {
        DepotDownloaderSetup.EnsureStartupFolder(root);
        if (DepotDownloaderSetup.FindInstalled(root, AppContext.BaseDirectory) is not null) return true;
        while (true)
        {
            using DepotDownloaderProgressForm progress = new(settings.Language, settings.DarkMode,
                async (status, cancellation) =>
                {
                    using HttpClient client = DepotDownloaderSetup.CreateClient();
                    await DepotDownloaderSetup.PrepareAsync(root, client, status, cancellation);
                });
            DialogResult result = progress.ShowDialog();
            if (result == DialogResult.OK) return true;
            if (result == DialogResult.Cancel) return false;
            string message = string.Format(MainForm.T(settings.Language, "DepotDownloaderPrepareFailed"), progress.Error?.Message);
            if (MessageBox.Show(message, MainForm.T(settings.Language, "DepotSetupTitle"),
                MessageBoxButtons.RetryCancel, MessageBoxIcon.Error) != DialogResult.Retry) return false;
        }
    }
}

public sealed partial class MainForm : Form
{
    private CancellationTokenSource? _depotUpdateCancellation;
    private readonly System.Windows.Forms.Timer _depotUpdateTimer = new() { Interval = 500 };
    private DepotDownloaderRelease? _pendingDepotRelease;
    private bool _depotUpdatePromptOpen;

    private async Task CheckDepotDownloaderUpdateAsync(
        Func<CancellationToken, Task<DepotDownloaderRelease>>? getLatest = null)
    {
        if (_depotUpdateCancellation is not null || IsDisposed || Disposing) return;
        using CancellationTokenSource cancellation = new(TimeSpan.FromSeconds(20));
        _depotUpdateCancellation = cancellation;
        WriteLog(L("DepotCheckingUpdates"));
        try
        {
            using HttpClient client = DepotDownloaderSetup.CreateClient();
            DepotDownloaderRelease latest = await (getLatest is null
                ? DepotDownloaderSetup.GetLatestReleaseAsync(client, true, cancellation.Token)
                : getLatest(cancellation.Token));
            if (cancellation.IsCancellationRequested || IsDisposed || Disposing) return;
            string? path = FindDepotDownloaderPath();
            if (path is null) return;
            string? installed = await Task.Run(() => DepotDownloaderSetup.GetInstalledVersion(path), cancellation.Token);
            if (cancellation.IsCancellationRequested || IsDisposed || Disposing) return;
            if (DepotDownloaderSetup.HasUpdate(installed, latest))
            {
                _pendingDepotRelease = latest;
                _depotUpdateTimer.Start();
            }
            else WriteLog(L("DepotAlreadyUpdated"));
        }
        catch (Exception error)
        {
            if (IsDisposed || Disposing || !ReferenceEquals(_depotUpdateCancellation, cancellation)) return;
            WriteLog(LF("DepotUpdateCheckFailed", error is OperationCanceledException ? L("DepotCheckTimeout") : error.Message));
        }
        finally
        {
            if (ReferenceEquals(_depotUpdateCancellation, cancellation)) _depotUpdateCancellation = null;
        }
    }

    private void ShowPendingDepotDownloaderUpdate(object? sender, EventArgs e)
    {
        if (_pendingDepotRelease is null || _isRunning || _depotUpdatePromptOpen ||
            !Visible || !Enabled || IsDisposed || Disposing || OwnedForms.Length > 0) return;
        _depotUpdateTimer.Stop();
        DepotDownloaderRelease release = _pendingDepotRelease;
        _pendingDepotRelease = null;
        _depotUpdatePromptOpen = true;
        try
        {
            string? current = FindDepotDownloaderPath();
            string? installed = current is null ? null : DepotDownloaderSetup.GetInstalledVersion(current);
            // Another app instance or a manual update may have installed this release already.
            if (!DepotDownloaderSetup.HasUpdate(installed, release)) return;
            using DepotDownloaderUpdateForm prompt = new(_userSettings.Language, _isDarkTheme, installed, release);
            if (prompt.ShowDialog(this) != DialogResult.OK) return;
            using DepotDownloaderProgressForm progress = new(_userSettings.Language, _isDarkTheme,
                async (status, cancellation) =>
                {
                    using HttpClient client = DepotDownloaderSetup.CreateClient();
                    await DepotDownloaderSetup.InstallAsync(GetAppWorkingFolder(), release, client, status, cancellation);
                });
            DialogResult result = progress.ShowDialog(this);
            if (result == DialogResult.OK) WriteLog(L("DepotUpdateSuccess"));
            else if (result == DialogResult.Abort) ShowErrorKey("DepotDownloaderPrepareFailed", progress.Error?.Message ?? "");
        }
        catch (Exception error) { ShowErrorKey("DepotDownloaderPrepareFailed", error.Message); }
        finally { _depotUpdatePromptOpen = false; }
    }

    private void CancelDepotDownloaderUpdateCheck()
    {
        _depotUpdateCancellation?.Cancel();
        _depotUpdateCancellation = null;
        _depotUpdateTimer.Stop();
        _pendingDepotRelease = null;
    }
}

internal sealed class DepotDownloaderProgressForm : Form
{
    private readonly CancellationTokenSource _cancellation = new();
    private bool _finished;
    internal Exception? Error { get; private set; }

    internal DepotDownloaderProgressForm(string language, bool dark,
        Func<IProgress<DepotSetupProgress>, CancellationToken, Task> operation)
    {
        string T(string key) => MainForm.T(language, key);
        Text = T("DepotSetupTitle");
        StartPosition = FormStartPosition.CenterScreen;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ClientSize = new Size(460, 145);
        Font = new Font("Segoe UI", 9F);
        BackColor = dark ? Color.FromArgb(31, 34, 40) : Color.FromArgb(245, 247, 250);
        ForeColor = dark ? Color.WhiteSmoke : Color.FromArgb(25, 28, 33);
        Label status = new() { Text = T("DepotDownloading"), ForeColor = ForeColor };
        status.SetBounds(16, 16, 428, 35);
        ProgressBar bar = new() { Style = ProgressBarStyle.Marquee };
        bar.SetBounds(16, 57, 428, 20);
        Button cancel = new() { Text = T("SteamGuardCancel"), BackColor = BackColor, ForeColor = ForeColor, FlatStyle = FlatStyle.Flat };
        cancel.SetBounds(344, 96, 100, 30);
        void Cancel()
        {
            _cancellation.Cancel();
            cancel.Enabled = false;
            status.Text = T("DepotCancelling");
        }
        cancel.Click += (_, _) => Cancel();
        FormClosing += (_, e) => { if (!_finished) { e.Cancel = true; Cancel(); } };
        Controls.AddRange([status, bar, cancel]);
        Shown += async (_, _) =>
        {
            DialogResult result;
            Progress<DepotSetupProgress> progress = new(value =>
            {
                if (IsDisposed || _cancellation.IsCancellationRequested) return;
                status.Text = T(value.Stage) + (value.Percent is int percent ? $" {percent}%" : "");
                bar.Style = value.Percent is null ? ProgressBarStyle.Marquee : ProgressBarStyle.Continuous;
                if (value.Percent is int number) bar.Value = Math.Clamp(number, 0, 100);
            });
            try { await operation(progress, _cancellation.Token); result = DialogResult.OK; }
            catch (OperationCanceledException) when (_cancellation.IsCancellationRequested) { result = DialogResult.Cancel; }
            catch (Exception error) { Error = error; result = DialogResult.Abort; }
            _finished = true;
            if (!IsDisposed) DialogResult = result;
        };
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) { _cancellation.Cancel(); _cancellation.Dispose(); }
        base.Dispose(disposing);
    }
}

internal sealed class DepotDownloaderUpdateForm : Form
{
    internal DepotDownloaderUpdateForm(string language, bool dark, string? installed, DepotDownloaderRelease release)
    {
        string T(string key) => MainForm.T(language, key);
        Text = T("DepotUpdateTitle");
        StartPosition = FormStartPosition.CenterParent;
        ClientSize = new Size(570, 460);
        MinimumSize = new Size(586, 499);
        MaximizeBox = false;
        MinimizeBox = false;
        Font = new Font("Segoe UI", 9F);
        BackColor = dark ? Color.FromArgb(31, 34, 40) : Color.FromArgb(245, 247, 250);
        ForeColor = dark ? Color.WhiteSmoke : Color.FromArgb(25, 28, 33);
        Label title = new() { Text = release.Name, Font = new Font(Font, FontStyle.Bold), ForeColor = ForeColor,
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right };
        title.SetBounds(16, 16, 538, 27);
        Label details = new() { Text = string.Format(T("DepotUpdateDetails"), installed ?? T("DepotUnknownVersion"),
            release.Tag, release.Published?.ToLocalTime().ToString("g") ?? T("DepotUnknownVersion")), ForeColor = ForeColor,
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right };
        details.SetBounds(16, 50, 538, 67);
        TextBox notes = new() { Text = string.IsNullOrWhiteSpace(release.Notes) ? T("DepotUpdateNoNotes") :
                release.Notes.Replace("\n", Environment.NewLine), Multiline = true, ReadOnly = true,
            ScrollBars = ScrollBars.Vertical, BackColor = dark ? Color.FromArgb(21, 23, 28) : Color.White,
            ForeColor = ForeColor, Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom };
        notes.SetBounds(16, 125, 538, 240);
        Label action = new() { Text = T("DepotUpdateAction"), ForeColor = ForeColor,
            Anchor = AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right };
        action.SetBounds(16, 375, 538, 34);
        LinkLabel link = new() { Text = T("DepotUpdateRelease"), AutoSize = true,
            LinkColor = dark ? Color.FromArgb(100, 190, 255) : Color.FromArgb(0, 90, 170),
            Anchor = AnchorStyles.Bottom | AnchorStyles.Left };
        link.SetBounds(16, 420, 260, 24);
        link.LinkClicked += (_, _) => Process.Start(new ProcessStartInfo(release.ReleaseUrl) { UseShellExecute = true });
        Button ok = new() { Text = "OK", DialogResult = DialogResult.OK };
        ok.SetBounds(344, 413, 100, 30);
        Button ignore = new() { Text = T("DepotUpdateIgnore"), DialogResult = DialogResult.Cancel };
        ignore.SetBounds(454, 413, 100, 30);
        foreach (Button button in new[] { ok, ignore })
        {
            button.BackColor = BackColor;
            button.ForeColor = ForeColor;
            button.FlatStyle = FlatStyle.Flat;
            button.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
        }
        AcceptButton = ok;
        CancelButton = ignore;
        Controls.AddRange([title, details, notes, action, link, ok, ignore]);
        Shown += (_, _) => { notes.Select(0, 0); ActiveControl = ignore; };
    }
}

#endregion

#region Command arguments

internal static class DepotDownloadCommand
{
    public static ProcessStartInfo Create(string executable, IReadOnlyList<int> depotIds, IReadOnlyList<string> manifestIds,
        string? fileList, bool validate, string username, bool rememberPassword, string outputFolder, bool manifestOnly = false)
    {
        ArgumentNullException.ThrowIfNull(depotIds);
        ArgumentNullException.ThrowIfNull(manifestIds);
        if (depotIds.Count == 0 || depotIds.Count != manifestIds.Count)
            throw new ArgumentException("Each depot must have a matching manifest.", nameof(manifestIds));
        if (depotIds.Any(id => id <= 0) || manifestIds.Any(id =>
                !ulong.TryParse(id?.Trim(), System.Globalization.NumberStyles.None,
                    System.Globalization.CultureInfo.InvariantCulture, out ulong value) || value == 0))
            throw new ArgumentException("Depot and manifest IDs must be positive numbers.");

        ProcessStartInfo info = new()
        {
            FileName = executable,
            WorkingDirectory = Path.GetDirectoryName(Path.GetFullPath(executable))!,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardInputEncoding = new System.Text.UTF8Encoding(false),
            StandardOutputEncoding = System.Text.Encoding.UTF8,
            StandardErrorEncoding = System.Text.Encoding.UTF8
        };
        info.ArgumentList.Add("-max-downloads");
        info.ArgumentList.Add("16");
        info.ArgumentList.Add("-app");
        info.ArgumentList.Add("782330");
        // DepotDownloader pairs each depot with the manifest at the same index.
        info.ArgumentList.Add("-depot");
        foreach (int depotId in depotIds)
            info.ArgumentList.Add(depotId.ToString(System.Globalization.CultureInfo.InvariantCulture));
        info.ArgumentList.Add("-manifest");
        foreach (string manifestId in manifestIds)
            info.ArgumentList.Add(manifestId.Trim());
        foreach (string argument in new[] { "-username", username, "-dir", outputFolder })
            info.ArgumentList.Add(argument);
        if (rememberPassword)
            info.ArgumentList.Add("-remember-password");
        if (fileList is not null)
        {
            info.ArgumentList.Add("-filelist");
            info.ArgumentList.Add(fileList);
        }
        if (manifestOnly)
            info.ArgumentList.Add("-manifest-only");
        if (validate)
            info.ArgumentList.Add("-validate");
        return info;
    }
}

#endregion

#region Process communication

internal static class DepotDownloaderRunner
{
    private enum InputPrompt { None, Password, SteamGuard }

    public static async Task<int> RunAsync(ProcessStartInfo startInfo, string password,
        Func<CancellationToken, Task<string?>> requestGuardCode, Action<string> output,
        CancellationToken cancellationToken)
    {
        if (password.Contains('\r') || password.Contains('\n'))
            throw new ArgumentException("The Steam password must be a single line.", nameof(password));
        cancellationToken.ThrowIfCancellationRequested();

        using Process process = new() { StartInfo = startInfo };
        using CancellationTokenSource lifetime = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        using CancellationTokenSource interaction = CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token);
        using SemaphoreSlim inputLock = new(1, 1);
        SecretRedactor redactor = new(password);
        process.Start();
        process.StandardInput.AutoFlush = true;
        using CancellationTokenRegistration registration = lifetime.Token.Register(() => Kill(process));

        async Task RespondAsync(InputPrompt prompt)
        {
            await inputLock.WaitAsync(interaction.Token).ConfigureAwait(false);
            try
            {
                string answer;
                if (prompt == InputPrompt.Password)
                {
                    answer = password;
                }
                else
                {
                    answer = await requestGuardCode(interaction.Token).ConfigureAwait(false)
                        ?? throw new OperationCanceledException("Steam Guard input was cancelled.");
                    if (string.IsNullOrWhiteSpace(answer) || answer.Contains('\r') || answer.Contains('\n'))
                        throw new InvalidOperationException("Steam Guard code must be a non-empty single line.");
                    redactor.Add(answer);
                }
                await process.StandardInput.WriteLineAsync(answer.AsMemory(), lifetime.Token).ConfigureAwait(false);
            }
            finally
            {
                inputLock.Release();
            }
        }

        async Task PumpAsync(StreamReader reader)
        {
            StringBuilder line = new();
            char[] buffer = new char[1024];
            void Emit()
            {
                string text = RemoveAnsi(line.ToString()).Trim();
                line.Clear();
                if (text.Length > 0)
                    output(redactor.Redact(text));
            }
            try
            {
                int count;
                while ((count = await reader.ReadAsync(buffer.AsMemory(), lifetime.Token).ConfigureAwait(false)) > 0)
                {
                    for (int i = 0; i < count; i++)
                    {
                        char character = buffer[i];
                        if (character is '\r' or '\n')
                        {
                            Emit();
                            continue;
                        }
                        line.Append(character);
                        if (line.Length > 65536)
                            throw new InvalidDataException("DepotDownloader produced an excessively long output line.");
                        if (character != ':')
                            continue;
                        InputPrompt prompt = RecognizePrompt(RemoveAnsi(line.ToString()));
                        if (prompt == InputPrompt.None)
                            continue;
                        Emit();
                        await RespondAsync(prompt).ConfigureAwait(false);
                    }
                }
                Emit();
            }
            catch
            {
                lifetime.Cancel();
                throw;
            }
        }

        async Task WaitForExitAsync()
        {
            try
            {
                await process.WaitForExitAsync(lifetime.Token).ConfigureAwait(false);
            }
            finally
            {
                interaction.Cancel();
            }
        }

        try
        {
            await Task.WhenAll(PumpAsync(process.StandardOutput), PumpAsync(process.StandardError),
                WaitForExitAsync()).ConfigureAwait(false);
            return process.ExitCode;
        }
        finally
        {
            Kill(process);
        }
    }

    private static InputPrompt RecognizePrompt(string text)
    {
        string value = text.TrimEnd();
        if (!value.EndsWith(':'))
            return InputPrompt.None;
        if (value.Contains("Enter account password for ", StringComparison.OrdinalIgnoreCase))
            return InputPrompt.Password;
        if (value.Contains("Please enter", StringComparison.OrdinalIgnoreCase) &&
            (value.Contains("auth code", StringComparison.OrdinalIgnoreCase) ||
             value.Contains("authentication code", StringComparison.OrdinalIgnoreCase)))
            return InputPrompt.SteamGuard;
        return InputPrompt.None;
    }

    private static string RemoveAnsi(string text) =>
        Regex.Replace(text, @"\x1B\[[0-?]*[ -/]*[@-~]", "");

    private static void Kill(Process process)
    {
        try
        {
            if (!process.HasExited)
                process.Kill(entireProcessTree: true);
        }
        catch (Exception error) when (error is InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            // The process may already have exited between the check and Kill.
        }
    }

    private sealed class SecretRedactor(string password)
    {
        private readonly List<string> _secrets = [password];

        public void Add(string secret)
        {
            lock (_secrets)
                _secrets.Add(secret);
        }

        public string Redact(string text)
        {
            lock (_secrets)
            {
                foreach (string secret in _secrets)
                    if (!string.IsNullOrEmpty(secret))
                        text = text.Replace(secret, "[redacted]", StringComparison.Ordinal);
                return text;
            }
        }
    }
}

#endregion
