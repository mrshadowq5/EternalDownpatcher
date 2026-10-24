using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace EternalDownpatcher.WinForms;

public sealed partial class MainForm : Form
{
    #region Download and patch workflow

    private async void RestoreBackupButton_Click(object? sender, EventArgs e)
    {
        if (_isRunning)
        {
            return;
        }

        _versionDataCancellation?.Cancel();
        string? targetFolder = _doomRootFolder;
        if (string.IsNullOrWhiteSpace(targetFolder) || !IsValidDoomRootFolder(targetFolder))
        {
            using FolderBrowserDialog targetDialog = new()
            {
                Description = "Select DOOM Eternal root folder to restore backup into",
                UseDescriptionForTitle = true
            };
            if (targetDialog.ShowDialog(this) != DialogResult.OK)
            {
                return;
            }

            targetFolder = Path.GetFullPath(targetDialog.SelectedPath);
            if (!IsValidDoomRootFolder(targetFolder))
            {
                ShowErrorKey("SelectedTargetFolderInvalid");
                return;
            }
        }

        using FolderBrowserDialog backupDialog = new()
        {
            Description = "Select EternalDownpatcher backup folder",
            UseDescriptionForTitle = true,
            InitialDirectory = targetFolder
        };
        if (backupDialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        string backupFolder = Path.GetFullPath(backupDialog.SelectedPath);
        if (!Directory.Exists(backupFolder) || !Directory.EnumerateFileSystemEntries(backupFolder).Any())
        {
            ShowErrorKey("BackupFolderInvalid");
            return;
        }

        DialogResult result = MessageBox.Show(this, $"Restore this backup into DOOM Eternal folder?\n\nBackup:\n{backupFolder}\n\nTarget:\n{targetFolder}" + (File.Exists(Path.Combine(backupFolder, PatchBackup.ManifestName)) ? "" : "\n\n" + L("LegacyBackupWarning")), "Restore backup", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
        if (result != DialogResult.Yes)
        {
            WriteLog("Restore backup cancelled.");
            return;
        }

        try
        {
            bool completeBackup = await RestoreBackupFilesAsync(backupFolder, targetFolder);
            if (!completeBackup)
                WriteLog(L("LegacyBackupWarning"));
            WriteLog("Backup restored successfully.");
            MessageBox.Show(this, completeBackup ? L("BackupRestored") : L("BackupRestored") + "\n\n" + L("LegacyBackupWarning"), "EternalDownpatcher", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception exception)
        {
            ShowErrorKey("RestoreBackupFailed", exception.Message);
        }
    }

    private async Task<bool> RestoreBackupFilesAsync(string backupFolder, string targetFolder)
    {
        if (_isRunning)
            throw new InvalidOperationException("Another operation is already running.");

        _versionDataCancellation?.Cancel();
        // Create the UI handle before worker-thread diagnostics marshal back here.
        _ = Handle;
        _isRestoringBackup = true;
        _doomRootFolder = targetFolder;
        _installedVersion = null;
        _installedExecutableSize = null;
        SetRunningState(true);
        _restoreProgressBar.Visible = true;
        _gameFolderStatusLabel.Text = L("RestorePreparing");
        ApplyStatusLabelColors(GetThemePalette(_isDarkTheme));
        WriteLog($"Restoring backup: {backupFolder}");
        WriteLog($"Restore target: {targetFolder}");
        bool reportFiles = true;
        var progress = new Progress<PatchBackup.RestoreProgress>(update =>
        {
            if (!reportFiles || !_isRestoringBackup || IsDisposed || Disposing) return;
            _gameFolderStatusLabel.Text = LF("RestoreProgress", update.CompletedFiles, update.TotalFiles);
            if (update.CurrentFile is not null) WriteLog(LF("RestoreCurrentFile", update.CurrentFile));
        });
        try
        {
            bool complete = await PatchBackup.RestoreAsync(backupFolder, targetFolder, progress);
            _gameFolderStatusLabel.Text = L("RestoreCheckingVersion");
            // Queued progress from file copying must not overwrite the verification stage.
            reportFiles = false;
            _installedVersion = await Task.Run(() => DetectInstalledVersion(targetFolder));
            GameDownloadOperation.ClearPending(targetFolder);
            return complete;
        }
        finally
        {
            _isRestoringBackup = false;
            _restoreProgressBar.Visible = false;
            RefreshDownpatchVersions();
            ApplyLanguage();
            SetRunningState(false);
        }
    }

    private async void StartButton_Click(object? sender, EventArgs e)
    {
        if (_isRunning)
        {
            return;
        }

        bool downloadAllFiles = _downloadAllFilesRadioButton.Checked;
        bool createBackup = _userSettings.CreateBackup;
        bool directUpdate = !downloadAllFiles && !createBackup;
        List<string> missingFields = [];
        if (_versionComboBox.SelectedItem is null)
        {
            missingFields.Add(L("MissingGameVersion"));
        }

        if (string.IsNullOrWhiteSpace(_steamUsernameTextBox.Text))
        {
            missingFields.Add(L("MissingSteamLogin"));
        }

        if (string.IsNullOrWhiteSpace(_steamPasswordTextBox.Text))
        {
            missingFields.Add(L("MissingSteamPassword"));
        }

        if (!downloadAllFiles && string.IsNullOrWhiteSpace(_doomRootFolder))
        {
            missingFields.Add(L("MissingDoomRoot"));
        }

        if (!directUpdate && string.IsNullOrWhiteSpace(_downpatchFolderTextBox.Text))
        {
            missingFields.Add(downloadAllFiles ? L("MissingDownloadFolder") : L("MissingPatchFolder"));
        }

        if (missingFields.Count > 0)
        {
            _requiredFieldsLabel.Text = LF("MissingPrefix", string.Join(", ", missingFields));
            ApplyStatusLabelColors(GetThemePalette(_isDarkTheme));
            WriteLog($"ERROR: {L("RequiredFieldsLog")}");
            return;
        }

        // Keep the active metadata unchanged through confirmations and downloads.
        _versionDataCancellation?.Cancel();
        DownpatchVersion? selectedVersion = GetSelectedVersion();
        if (selectedVersion is null)
        {
            ShowErrorKey("SelectedVersionInvalid");
            return;
        }

        if (selectedVersion.ManifestIds.Length != DepotIds.Length)
        {
            ShowErrorKey("InvalidManifestData");
            return;
        }

        string? fileListPath = null;
        if (!downloadAllFiles)
        {
            // Steam or another tool may have changed the executable since folder selection.
            try
            {
                _installedVersion = DetectInstalledVersion(_doomRootFolder!);
            }
            catch (Exception exception)
            {
                ShowError(exception.Message);
                return;
            }

            DownpatchVersion? pending = FindPendingVersion(_doomRootFolder!);
            if (pending is not null && (!directUpdate || !pending.ManifestIds.SequenceEqual(selectedVersion.ManifestIds)))
            {
                ShowErrorKey("PendingDirectError", pending.Name);
                return;
            }
            if (_installedVersion is null && !directUpdate)
            {
                ApplyLanguage();
                OfferFullDownloadForUnknownVersion();
                return;
            }

            int targetIndex = GetVersionIndex(selectedVersion.Name);
            int installedIndex = _installedVersion is null ? -1 : GetVersionIndex(_installedVersion.Name);
            if (targetIndex < 0 || (!directUpdate && installedIndex < 0))
            {
                ShowErrorKey("UnableCompareVersions");
                return;
            }

            if (pending is null && installedIndex >= 0 && targetIndex >= installedIndex)
            {
                ShowErrorKey("TargetMustBeOlder");
                return;
            }

        }

        string? depotDownloaderPath = FindDepotDownloaderPath();
        if (depotDownloaderPath is null)
        {
            ShowErrorKey("DepotDownloaderMissing");
            return;
        }

        string outputFolder;
        try
        {
            outputFolder = Path.GetFullPath(directUpdate ? _doomRootFolder! : _downpatchFolderTextBox.Text.Trim());
            if (directUpdate) PathSafety.RejectLinks(outputFolder);
            else PathSafety.ValidateOutput(outputFolder, GetAppWorkingFolder(), _doomRootFolder);
        }
        catch (Exception exception)
        {
            ShowError(exception.Message);
            return;
        }

        string? doomRootFolder = null;
        if (!downloadAllFiles)
        {
            doomRootFolder = Path.GetFullPath(_doomRootFolder!);
            if (!IsUsableGameFolder(doomRootFolder))
            {
                ShowErrorKey("InvalidDoomRootFolder");
                return;
            }

            if (!directUpdate && AreSameDirectory(outputFolder, doomRootFolder))
            {
                ShowErrorKey("PatchFolderSameAsGame");
                return;
            }

            if (!directUpdate && IsSubdirectoryOf(doomRootFolder, outputFolder))
            {
                ShowErrorKey("DoomFolderInsidePatchFolder");
                return;
            }
        }
        else
        {
            if (IsValidDoomRootFolder(outputFolder) && !GameDownloadOperation.CanResumeStandalone(outputFolder, selectedVersion.ManifestIds))
            {
                ShowErrorKey("DownloadFolderIsDoomRoot");
                return;
            }

            if (!string.IsNullOrWhiteSpace(_doomRootFolder) && AreSameDirectory(outputFolder, _doomRootFolder))
            {
                ShowErrorKey("DownloadFolderSameAsRoot");
                return;
            }
        }

        bool reuseExistingOutput = false;
        if (!directUpdate && !PathSafety.TryPrepareOutput(outputFolder,
            () => MessageBox.Show(this, downloadAllFiles ? L("DownloadFolderDoesNotExistCreate") : L("PatchFolderDoesNotExistCreate"),
                downloadAllFiles ? L("CreateDownloadFolder") : L("CreatePatchFolder"), MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes,
            out reuseExistingOutput, out string? preparationError))
        {
            if (preparationError is null) WriteLog("Operation cancelled. Output folder was not created.");
            else ShowError(preparationError);
            return;
        }
        if (reuseExistingOutput)
            WriteLog(L("ReuseVerifiedDownload"));

        DialogResult result = MessageBox.Show(this, GetDownloadConfirmation(selectedVersion, downloadAllFiles, createBackup),
            downloadAllFiles ? "Download all files" : "Start downpatch", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
        if (result != DialogResult.Yes)
        {
            WriteLog("Operation cancelled before download.");
            return;
        }

        _requiredFieldsLabel.Text = L("Ready");
        ApplyStatusLabelColors(GetThemePalette(_isDarkTheme));
        _operationCancellation = new CancellationTokenSource();
        bool validateFiles = _validateFilesCheckBox.Checked || reuseExistingOutput;
        DownpatchVersion? sourceVersion = downloadAllFiles ? null : _installedVersion;
        SetRunningState(true);
        try
        {
            SaveSteamCredentials();
            WriteLog($"Mode: {(downloadAllFiles ? "Download All Files" : "Patch existing install")}");
            WriteLog($"Selected version: {selectedVersion.Name}");
            WriteLog($"Steam account: {_steamUsernameTextBox.Text.Trim()}");
            WriteLog($"DepotDownloader: {depotDownloaderPath}");
            if (downloadAllFiles)
            {
                WriteLog("Filelist: disabled");
                WriteLog($"Download folder: {outputFolder}");
            }
            else
            {
                WriteLog($"Patch files folder: {outputFolder}");
                WriteLog($"DOOM Eternal root folder: {doomRootFolder}");
            }

            if (directUpdate) WriteLog(L("DirectUpdateLog"));
            string username = _steamUsernameTextBox.Text.Trim(), password = _steamPasswordTextBox.Text;
            var manifests = new SteamManifestCache(SteamManifestCache.DefaultRoot,
                SteamManifestCache.FindExistingFolders(doomRootFolder, outputFolder),
                (requests, cacheFolder, token) => RunDepotDownloaderAsync(depotDownloaderPath,
                    requests.Select(request => request.DepotId).ToArray(), requests.Select(request => request.ManifestId).ToArray(),
                    null, false, false, username, password, _userSettings.RememberPassword, cacheFolder, token, manifestOnly: true));
            ManifestSelection? selection = null;
            _gameFolderStatusLabel.Text = L("ManifestPlanning");
            WriteLog(L("ManifestPlanning"));
            string[] required = selectedVersion.BuildId is null ? ["DOOMEternalx64vk.exe"] :
                ["DOOMEternalx64vk.exe", "launcher/idTechLauncher.exe", "launcher/resources/app.asar"];
            try
            {
                bool interrupted = doomRootFolder is not null && FindPendingVersion(doomRootFolder) is not null;
                selection = await ManifestRollbackPlanner.PrepareAsync(manifests,
                    downloadAllFiles || interrupted ? null : sourceVersion?.ManifestIds,
                    selectedVersion.ManifestIds, downloadAllFiles ? null : doomRootFolder, required,
                    message => WriteLogThreadSafe(LF("ManifestSourceFallback", message)), _operationCancellation.Token);
                WriteLog(LF("ManifestSelectionLog", selection.Files.Length, selection.TargetFiles.Length,
                    selection.SelectedBytes / 1_000_000_000d, selection.TargetBytes / 1_000_000_000d));
            }
            catch (Exception error) when (!_operationCancellation.IsCancellationRequested &&
                error is IOException or InvalidDataException or UnauthorizedAccessException or ArgumentException or InvalidOperationException)
            {
                WriteLog(LF("ManifestTargetFallback", error.Message));
            }
            _operationCancellation.Token.ThrowIfCancellationRequested();
            ApplyLanguage();
            if (!downloadAllFiles)
            {
                fileListPath = Path.Combine(Path.GetTempPath(), "EternalDownpatcher-" + Guid.NewGuid().ToString("N") + ".filelist.txt");
                await File.WriteAllLinesAsync(fileListPath, selection is null ? ["regex:.*"] : selection.Files,
                    _operationCancellation.Token);
                WriteLog($"Filelist: {fileListPath}");
            }
            WriteLog("Downloading with 16 parallel chunks in one Steam session.");
            string[]? filesToApply = selection?.Files;
            if (filesToApply is not null && selectedVersion.BuildId is not null)
                filesToApply = filesToApply.Append(VersionReceiptName).ToArray();
            var plan = new GameDownloadPlan(downloadAllFiles, createBackup, validateFiles, outputFolder, doomRootFolder,
                selectedVersion.ManifestIds, filesToApply);
            GameDownloadResult completed = await GameDownloadOperation.ExecuteAsync(plan,
                (folder, verifyExisting, token) => RunDepotDownloaderAsync(depotDownloaderPath, DepotIds, selectedVersion.ManifestIds,
                    fileListPath, !downloadAllFiles, verifyExisting, _steamUsernameTextBox.Text.Trim(),
                    _steamPasswordTextBox.Text, _userSettings.RememberPassword, folder, token),
                async (folder, token) =>
                {
                    // A successful downloader exit can still omit a depot. Resolve
                    // all target metadata even when initial preparation fell back.
                    if (selection is null)
                    {
                        SteamManifest[] target = await manifests.SnapshotAsync(selectedVersion.ManifestIds, token);
                        selection = await Task.Run(() => ManifestRollbackPlanner.Compare(null, target, null, required, token), token);
                    }
                    _gameFolderStatusLabel.Text = L("VerifyingDownload");
                    WriteLog(L("VerifyingDownload"));
                    await Task.Run(() => DownloadedPayload.VerifyAsync(folder, selection, token,
                        (count, total, path) => WriteLogThreadSafe(LF("VerifyDownloadFile", count, total, path))), token);
                    await Task.Run(() =>
                    {
                        string downloadedExe = Path.Combine(folder, "DOOMEternalx64vk.exe");
                        if (!long.TryParse(selectedVersion.Size, out long expectedSize) || new FileInfo(downloadedExe).Length != expectedSize)
                            throw new InvalidOperationException(L(directUpdate ? "DirectExecutableMismatch" : "DownloadedExecutableMismatch"));
                        SaveVersionReceipt(folder, selectedVersion, token);
                    }, token);
                },
                () =>
                {
                    _installedVersion = DetectInstalledVersion(doomRootFolder!);
                    if (_installedVersion is null || _installedVersion.Name != sourceVersion!.Name)
                        throw new InvalidOperationException(L("SourceVersionChanged"));
                }, WriteLogThreadSafe, count =>
                {
                    if (count % 100 == 0) WriteLogThreadSafe($"Copied files: {count}");
                }, _operationCancellation.Token, (_, token) =>
                {
                    token.ThrowIfCancellationRequested();
                    // The verification callback resolved this set before writing a
                    // receipt; only those verified paths may reach the game folder.
                    string[] verified = selection?.Files ?? throw new InvalidOperationException("Target files were not verified.");
                    return Task.FromResult(selectedVersion.BuildId is null ? verified : verified.Append(VersionReceiptName).ToArray());
                });
            WriteLog("Download completed.");
            if (downloadAllFiles)
            {
                EnsureSteamAppIdFile(outputFolder);
                WriteLog("steam_appid.txt created.");
                WriteLog("Download All Files completed successfully.");
                MessageBox.Show(this, "Download All Files completed successfully.\n\nYou can add DOOMEternalx64vk.exe as a Non-Steam Game.", "EternalDownpatcher", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            int copiedFiles = completed.AppliedFiles;
            _installedVersion = DetectInstalledVersion(doomRootFolder!);
            RefreshDownpatchVersions();
            ApplyLanguage();
            if (directUpdate) WriteLog("Direct update completed in the selected game folder.");
            else WriteLog($"Completed. Files copied: {copiedFiles}");
            WriteLog("Downpatch completed successfully.");
            MessageBox.Show(this, "Downpatch completed successfully.", "EternalDownpatcher", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (OperationCanceledException)
        {
            WriteLog("Operation cancelled.");
            if (directUpdate && FindPendingVersion(doomRootFolder!) is not null) WriteLog(L("DirectInterrupted"));
        }
        catch (Exception exception)
        {
            WriteLog($"ERROR: {exception.Message}");
            string message = directUpdate && FindPendingVersion(doomRootFolder!) is not null
                ? exception.Message + "\n\n" + L("DirectInterrupted") : exception.Message;
            MessageBox.Show(this, message, "EternalDownpatcher", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            if (fileListPath is not null)
            {
                try { File.Delete(fileListPath); }
                catch (Exception error) when (error is IOException or UnauthorizedAccessException) { }
            }
            _operationCancellation?.Dispose();
            _operationCancellation = null;
            if (directUpdate && doomRootFolder is not null)
            {
                try { _installedVersion = DetectInstalledVersion(doomRootFolder); }
                catch (Exception) { _installedVersion = null; }
                RefreshDownpatchVersions();
                ApplyLanguage();
            }
            SetRunningState(false);
        }
    }

    private void CancelButton_Click(object? sender, EventArgs e)
    {
        _operationCancellation?.Cancel();
        WriteLog("Cancelling operation.");
    }

    private void SelectGameFolderButton_Click(object? sender, EventArgs e)
    {
        using FolderBrowserDialog dialog = new()
        {
            Description = "Select DOOM Eternal root folder",
            UseDescriptionForTitle = true
        };
        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        string selectedPath = Path.GetFullPath(dialog.SelectedPath);
        if (!IsUsableGameFolder(selectedPath))
        {
            _doomRootFolder = null;
            _installedVersion = null;
            _gameFolderStatusLabel.Text = L("InvalidDoomRootFolderShort");
            ApplyStatusLabelColors(GetThemePalette(_isDarkTheme));
            RefreshDownpatchVersions();
            WriteLog($"ERROR: {L("InvalidFolderNoExe")}");
            MessageBox.Show(this, L("InvalidFolderNoExe"), "EternalDownpatcher", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }

        _doomRootFolder = selectedPath;
        try
        {
            _installedVersion = DetectInstalledVersion(selectedPath);
        }
        catch (Exception exception)
        {
            _installedVersion = null;
            RefreshDownpatchVersions();
            ApplyLanguage();
            ShowError(exception.Message);
            return;
        }

        if (_installedVersion is null)
        {
            _gameFolderStatusLabel.Text = L("RootSelectedButVersionNotDetected");
            ApplyStatusLabelColors(GetThemePalette(_isDarkTheme));
            RefreshDownpatchVersions();
            WriteLog($"DOOM Eternal folder: {selectedPath}");
            if (!IsDirectUpdate && FindPendingVersion(selectedPath) is null) OfferFullDownloadForUnknownVersion();
            ApplyLanguage();
            return;
        }

        _gameFolderStatusLabel.Text = string.Format(L("RootSelected"), _installedVersion.Name);
        ApplyStatusLabelColors(GetThemePalette(_isDarkTheme));
        WriteLog($"DOOM Eternal folder: {selectedPath}");
        WriteLog($"Installed DOOM Eternal version: {_installedVersion.Name}");
        RefreshDownpatchVersions();
        ApplyLanguage();
    }

    private void SelectDownpatchFolderButton_Click(object? sender, EventArgs e)
    {
        using FolderBrowserDialog dialog = new()
        {
            Description = "Select patch files folder",
            UseDescriptionForTitle = true,
            InitialDirectory = Directory.Exists(_downpatchFolderTextBox.Text) ? _downpatchFolderTextBox.Text : GetAppWorkingFolder()
        };
        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        _downpatchFolderTextBox.Text = Path.GetFullPath(dialog.SelectedPath);
        WriteLog($"Patch files folder: {_downpatchFolderTextBox.Text}");
    }

    private bool IsDirectUpdate => !_downloadAllFilesRadioButton.Checked && !_userSettings.CreateBackup;

    private DownpatchVersion? FindPendingVersion(string folder)
    {
        string[]? manifests = GameDownloadOperation.ReadPending(folder);
        return manifests is null ? null : _versions.FirstOrDefault(version => version.ManifestIds.SequenceEqual(manifests));
    }

    private bool IsUsableGameFolder(string folder) => IsValidDoomRootFolder(folder) || FindPendingVersion(folder) is not null;

    private void UpdateBackupMode()
    {
        bool direct = IsDirectUpdate;
        if (direct)
        {
            if (_savedOutputFolder is null)
            {
                _savedOutputFolder = _downpatchFolderTextBox.Text;
                _savedValidateFiles = _validateFilesCheckBox.Checked;
            }
            _downpatchFolderTextBox.Text = _doomRootFolder ?? "";
            _validateFilesCheckBox.Checked = true;
        }
        else if (_savedOutputFolder is not null)
        {
            _downpatchFolderTextBox.Text = _savedOutputFolder;
            _savedOutputFolder = null;
            _validateFilesCheckBox.Checked = _savedValidateFiles;
        }
        if (_downloadAllFilesRadioButton.Checked) _validateFilesCheckBox.Checked = false;
        _downpatchFolderTextBox.ReadOnly = direct;
        _downpatchFolderTextBox.Enabled = !_isRunning;
        _selectDownpatchFolderButton.Enabled = !_isRunning && !direct;
        _validateFilesCheckBox.Enabled = !_isRunning && !_downloadAllFilesRadioButton.Checked && !direct;
        if (direct) _folderLabel.Text = L("DirectGameFolder");
    }

    private string GetDownloadConfirmation(DownpatchVersion version, bool standalone, bool createBackup)
    {
        if (standalone)
            return "This will download a full standalone copy of the selected DOOM Eternal version.\n\nIt will not modify your selected Steam install.\n\nContinue?";
        if (!createBackup)
        {
            return L("ManifestPreparationNotice") + "\n\n" + LF("NoBackupConfirmation", version.Name, _doomRootFolder ?? "");
        }
        return L("ManifestPreparationNotice") + "\n\n" + L("BackupPatchConfirmation");
    }

    private void SetRunningState(bool isRunning)
    {
        _isRunning = isRunning;
        _startButton.Enabled = !isRunning && _versions.Count > 0;
        _cancelButton.Enabled = isRunning && !_isRestoringBackup;
        _selectGameFolderButton.Enabled = !isRunning && !_downloadAllFilesRadioButton.Checked;
        _selectDownpatchFolderButton.Enabled = !isRunning;
        _versionComboBox.Enabled = !isRunning;
        _steamUsernameTextBox.Enabled = !isRunning;
        _steamPasswordTextBox.Enabled = !isRunning;
        _downloadAllFilesRadioButton.Enabled = !isRunning;
        _patchExistingRadioButton.Enabled = !isRunning;
        _validateFilesCheckBox.Enabled = !isRunning && !_downloadAllFilesRadioButton.Checked;
        _restoreBackupButton.Enabled = !isRunning;
        _openOutputFolderButton.Enabled = !isRunning;
        _themeToggleButton.Enabled = !isRunning;
        _settingsButton.Enabled = !isRunning;
        UpdateBackupMode();
    }

    private void DownloadMode_CheckedChanged(object? sender, EventArgs e)
    {
        if (_downloadAllFilesRadioButton.Checked)
        {
            _startButton.Text = L("Download");
            _folderLabel.Text = L("DownloadFolder");
            _validateFilesCheckBox.Checked = false;
            _validateFilesCheckBox.Enabled = false;
            _selectGameFolderButton.Enabled = false;
            _gameFolderStatusLabel.Text = L("DownloadAllFilesStatus");
            ApplyStatusLabelColors(GetThemePalette(_isDarkTheme));
            WriteLog("Download All Files mode enabled.");
        }
        else
        {
            _startButton.Text = L("Downpatch");
            _folderLabel.Text = L("PatchFilesFolder");
            _validateFilesCheckBox.Enabled = !_isRunning;
            _selectGameFolderButton.Enabled = !_isRunning;
            if (_doomRootFolder is null)
            {
                _gameFolderStatusLabel.Text = L("RootNotSelected");
            }
            else if (_installedVersion is not null)
            {
                _gameFolderStatusLabel.Text = string.Format(L("RootSelected"), _installedVersion.Name);
            }

            ApplyStatusLabelColors(GetThemePalette(_isDarkTheme));
            WriteLog("Patch existing install mode enabled.");
        }

        RefreshDownpatchVersions();
        ApplyLanguage();
    }

    #endregion

    #region Versions and file lists

    private void LoadVersions()
    {
        string? cachedFolder = VersionDataCache.FindCurrentFolder(VersionDataCache.CacheRoot);
        string[] folders = [Path.Combine(GetAppWorkingFolder(), "data"), Path.Combine(AppContext.BaseDirectory, "data"), cachedFolder ?? ""];
        foreach (string folder in folders.Where(folder => !string.IsNullOrEmpty(folder)).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (!File.Exists(Path.Combine(folder, "versions.json")))
                continue;
            try
            {
                ApplyVersionData(folder);
            }
            catch (Exception exception)
            {
                WriteLog(LF("LoadVersionsFailed", exception.Message));
            }
        }

        if (_versions.Count > 0) return;

        // The startup update can recover even if the bundled data folder is missing.
        _startButton.Enabled = false;
        WriteLog(L("VersionsJsonNotFound"));
    }

    private bool ApplyVersionData(string folder)
    {
        string path = Path.Combine(folder, "versions.json");
        List<DownpatchVersion> versions = ReadVersionsJson(path);
        // Several public builds share one EXE. Preserve every known depot snapshot,
        // while allowing a catalog to rename a build without losing coverage.
        HashSet<string> incomingSnapshots = versions.Select(version => string.Join(",", version.ManifestIds)).ToHashSet(StringComparer.Ordinal);
        if (_versions.Any(version => !incomingSnapshots.Contains(string.Join(",", version.ManifestIds))))
        {
            WriteLog(L("VersionDataOlder"));
            return false;
        }
        // An older GitHub snapshot may know the same builds but omit verified
        // rollback lists bundled with this binary. Retain those exact pairs.
        for (int index = 0; index < versions.Count; index++)
        {
            DownpatchVersion incoming = versions[index];
            DownpatchVersion? known = _versions.FirstOrDefault(version => version.ManifestIds.SequenceEqual(incoming.ManifestIds));
            var lists = (incoming.VerifiedRollbackFileLists ?? []).ToList();
            foreach (VerifiedRollbackFileList list in known?.VerifiedRollbackFileLists ?? [])
                if (!lists.Any(existing => existing.SourceManifestIds.SequenceEqual(list.SourceManifestIds))) lists.Add(list);
            versions[index] = incoming with { VerifiedRollbackFileLists = lists.ToArray() };
        }
        _versionDataFolder = Path.GetFullPath(folder);
        _versions.Clear();
        _versions.AddRange(versions);
        if (_doomRootFolder is not null)
            _installedVersion = DetectInstalledVersion(_doomRootFolder);
        RefreshDownpatchVersions();
        ApplyLanguage();
        _startButton.Enabled = !_isRunning && _versions.Count > 0;
        WriteLog(LF("LoadedVersionsLog", _versions.Count));
        WriteLog(LF("VersionsSourceLog", path));
        return true;
    }

    private async Task UpdateVersionDataAsync(Func<CancellationToken, Task<string>>? refreshVersionData = null)
    {
        if (_versionDataCancellation is not null || IsDisposed || Disposing)
            return;
        using CancellationTokenSource cancellation = new();
        _versionDataCancellation = cancellation;
        WriteLog(L("CheckingVersionData"));
        try
        {
            string folder = await (refreshVersionData ?? DownloadVersionDataAsync)(cancellation.Token);
            // Cancellation can race with a completed download queued on the UI thread.
            if (cancellation.IsCancellationRequested || _isRunning || IsDisposed || Disposing)
                return;
            if (ApplyVersionData(folder))
                WriteLog(L("VersionDataReady"));
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
            // Starting a game operation or closing the window cancels only this check.
        }
        catch (Exception exception)
        {
            if (cancellation.IsCancellationRequested || IsDisposed || Disposing)
                return;
            string reason = exception is OperationCanceledException ? L("VersionDataCancelled") : exception.Message;
            WriteLog(LF("VersionDataFallback", reason));
            if (_versions.Count == 0)
                ShowErrorKey("VersionsJsonNotFound");
        }
        finally
        {
            if (ReferenceEquals(_versionDataCancellation, cancellation))
                _versionDataCancellation = null;
        }
    }

    private static async Task<string> DownloadVersionDataAsync(CancellationToken cancellationToken)
    {
        using HttpClient client = new() { Timeout = TimeSpan.FromSeconds(15) };
        client.DefaultRequestHeaders.UserAgent.ParseAdd($"EternalDownpatcher/{AppVersion}");
        return await VersionDataCache.RefreshAsync(VersionDataCache.CacheRoot, client, cancellationToken).ConfigureAwait(false);
    }

    private void EnsureDefaultVersionSelection()
    {
        if (_versionComboBox.Items.Count <= 0)
        {
            _versionComboBox.SelectedIndex = -1;
            return;
        }

        if (_versionComboBox.SelectedIndex >= 0)
        {
            return;
        }

        int defaultIndex = _versionComboBox.Items.IndexOf("1.1"); //default downpatch version
        _versionComboBox.SelectedIndex = defaultIndex >= 0 ? defaultIndex : 0;
    }

    private string? FindVersionsJsonPath()
    {
        return _versionDataFolder is null ? null : Path.Combine(_versionDataFolder, "versions.json");
    }

    private static List<DownpatchVersion> ReadVersionsJson(string path, bool strict = false)
    {
        string json = File.ReadAllText(path);
        using JsonDocument document = JsonDocument.Parse(json);
        JsonElement versionsElement;
        if (document.RootElement.ValueKind == JsonValueKind.Array)
        {
            versionsElement = document.RootElement;
        }
        else if (document.RootElement.ValueKind == JsonValueKind.Object && document.RootElement.TryGetProperty("versions", out JsonElement nestedVersions))
        {
            versionsElement = nestedVersions;
        }
        else
        {
            throw new InvalidOperationException("versions.json has unsupported structure.");
        }

        List<DownpatchVersion> versions = [];
        foreach (JsonElement element in versionsElement.EnumerateArray())
        {
            string? name = GetStringProperty(element, "name", "version", "versionName");
            if (string.IsNullOrWhiteSpace(name))
            {
                continue;
            }

            if (!TryGetProperty(element, out JsonElement manifestIdsElement, "manifestIds", "manifests"))
            {
                continue;
            }

            if (manifestIdsElement.ValueKind != JsonValueKind.Array)
            {
                continue;
            }

            List<string> manifestIds = [];
            foreach (JsonElement manifestIdElement in manifestIdsElement.EnumerateArray())
            {
                if (manifestIdElement.ValueKind == JsonValueKind.String)
                {
                    string? manifestId = manifestIdElement.GetString();
                    if (!string.IsNullOrWhiteSpace(manifestId))
                    {
                        manifestIds.Add(manifestId.Trim());
                    }
                }
                else if (manifestIdElement.ValueKind == JsonValueKind.Number)
                {
                    manifestIds.Add(manifestIdElement.GetRawText());
                }
            }

            if (manifestIds.Count != DepotIds.Length)
            {
                continue;
            }

            string size = GetStringProperty(element, "size") ?? "";
            DetectionFile[] detectionFiles = [];
            if (element.TryGetProperty("detectionFiles", out JsonElement detection))
            {
                if (detection.ValueKind != JsonValueKind.Array || detection.GetArrayLength() > 8)
                    throw new InvalidDataException("Invalid build detection files.");
                detectionFiles = detection.EnumerateArray().Select(file => new DetectionFile(
                    GetStringProperty(file, "path") ?? "", GetStringProperty(file, "size") ?? "",
                    GetStringProperty(file, "sha1") ?? "")).ToArray();
                foreach (DetectionFile file in detectionFiles)
                {
                    ValidateFileListPath(file.Path);
                    if (!long.TryParse(file.Size, out long fileSize) || fileSize <= 0 ||
                        !Regex.IsMatch(file.Sha1, @"\A[0-9a-fA-F]{40}\z"))
                        throw new InvalidDataException("Invalid build detection fingerprint.");
                }
            }
            string? buildId = GetStringProperty(element, "buildId");
            if (buildId is not null && (!ulong.TryParse(buildId, out ulong build) || build == 0))
                throw new InvalidDataException("Invalid Steam build ID.");
            VerifiedRollbackFileList[] verifiedLists = [];
            if (element.TryGetProperty("verifiedRollbackFileLists", out JsonElement listData))
            {
                if (listData.ValueKind != JsonValueKind.Array || listData.GetArrayLength() > 64)
                    throw new InvalidDataException("Invalid verified rollback lists.");
                verifiedLists = listData.Deserialize<VerifiedRollbackFileList[]>(new JsonSerializerOptions
                    { PropertyNameCaseInsensitive = true }) ?? [];
                HashSet<string> sources = new(StringComparer.Ordinal);
                foreach (VerifiedRollbackFileList list in verifiedLists)
                {
                    if (list is null || list.SourceManifestIds is not { Length: 8 } ||
                        list.SourceManifestIds.Any(id => !ulong.TryParse(id, out ulong value) || value == 0) ||
                        !sources.Add(string.Join(",", list.SourceManifestIds)) ||
                        list.Files is not { Length: > 0 and <= 32768 })
                        throw new InvalidDataException("Invalid verified rollback pair.");
                    HashSet<string> paths = new(StringComparer.OrdinalIgnoreCase);
                    foreach (string file in list.Files)
                    {
                        if (file is null) throw new InvalidDataException("Missing verified rollback path.");
                        ValidateFileListPath(file);
                        if (!paths.Add(file.Replace('\\', '/')))
                            throw new InvalidDataException("Duplicate verified rollback path.");
                    }
                    if (!paths.Contains("DOOMEternalx64vk.exe"))
                        throw new InvalidDataException("Verified rollback must include the game executable.");
                }
            }
            versions.Add(new DownpatchVersion(name.Trim(), size.Trim(), manifestIds.ToArray(),
                element.TryGetProperty("requiresCompleteFileList", out JsonElement completeFileList) &&
                completeFileList.ValueKind == JsonValueKind.True, buildId, detectionFiles, verifiedLists));
        }

        if (versions.Count == 0)
        {
            throw new InvalidOperationException("No valid versions were found in versions.json.");
        }

        if (strict)
        {
            HashSet<string> names = new(StringComparer.OrdinalIgnoreCase);
            if (versions.Count != versionsElement.GetArrayLength())
                throw new InvalidDataException("versions.json contains incomplete version entries.");
            foreach (DownpatchVersion version in versions)
            {
                if (!Regex.IsMatch(version.Name, @"\A(?:[0-9]+(?:\.[0-9]+)*|Build [0-9]+)\z") || !names.Add(version.Name) ||
                    !long.TryParse(version.Size, out long size) || size <= 0 ||
                    version.ManifestIds.Any(id => !ulong.TryParse(id, out ulong value) || value == 0))
                    throw new InvalidDataException($"Invalid version metadata: {version.Name}");
            }
        }

        return versions;
    }

    internal static void ValidateVersionDataFolder(string folder)
    {
        string jsonPath = PathSafety.Resolve(folder, "versions.json");
        if (!File.Exists(jsonPath) || new FileInfo(jsonPath).Length > 1024 * 1024)
            throw new InvalidDataException("Missing or oversized versions.json.");
        List<DownpatchVersion> versions = ReadVersionsJson(jsonPath, strict: true);
        IEnumerable<string> requiredLists = versions.Skip(1).Where(version => !version.RequiresCompleteFileList).Select(version => version.Name);
        // 1.0 uses this additional list; there is deliberately no 1.0.txt.
        if (versions.Any(version => version.Name == "1.0"))
            requiredLists = requiredLists.Append("1.1.0");
        foreach (string name in requiredLists.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            string path = PathSafety.Resolve(folder, name + ".txt");
            if (!File.Exists(path) || new FileInfo(path).Length > 1024 * 1024)
                throw new InvalidDataException($"Missing or oversized filelist: {name}.txt");
            string[] files = ReadFileListEntries(path).ToArray();
            if (files.Length == 0)
                throw new InvalidDataException($"Empty filelist: {name}.txt");
            foreach (string file in files)
                ValidateFileListPath(file);
        }
    }

    private static void ValidateFileListPath(string file)
    {
        // These are game-relative names in a text file, not paths to open in the cache.
        // Check their syntax without probing the filesystem for every game file.
        string[] parts = file.Split(['/', '\\']);
        if (string.IsNullOrWhiteSpace(file) || Path.IsPathRooted(file) ||
            parts.Any(part => part is "" or "." or ".." || part.EndsWith(' ') || part.EndsWith('.') ||
                part.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0))
            throw new InvalidDataException($"Invalid filelist path: {file}");
    }

    private void RefreshDownpatchVersions()
    {
        string? selectedBefore = _versionComboBox.SelectedItem?.ToString();
        _versionComboBox.Items.Clear();
        DownpatchVersion? pending = _doomRootFolder is null ? null : FindPendingVersion(_doomRootFolder);
        if (!_downloadAllFilesRadioButton.Checked && pending is not null)
        {
            _versionComboBox.Items.Add(pending.Name);
            _versionComboBox.SelectedIndex = 0;
            return;
        }
        if (_installedVersion is null || _downloadAllFilesRadioButton.Checked)
        {
            foreach (DownpatchVersion version in _versions)
            {
                _versionComboBox.Items.Add(version.Name);
            }

            if (!string.IsNullOrWhiteSpace(selectedBefore) && _versionComboBox.Items.Contains(selectedBefore))
            {
                _versionComboBox.SelectedItem = selectedBefore;
            }
            else
            {
                EnsureDefaultVersionSelection();
            }

            return;
        }

        int installedIndex = GetVersionIndex(_installedVersion.Name);
        if (installedIndex <= 0)
        {
            _versionComboBox.SelectedIndex = -1;
            WriteLog("No older downpatch versions are available.");
            return;
        }

        for (int i = 0; i < installedIndex; i++)
        {
            _versionComboBox.Items.Add(_versions[i].Name);
        }

        if (!string.IsNullOrWhiteSpace(selectedBefore) && _versionComboBox.Items.Contains(selectedBefore))
        {
            _versionComboBox.SelectedItem = selectedBefore;
        }
        else
        {
            EnsureDefaultVersionSelection();
        }

        WriteLog($"Detected installed version: {_installedVersion.Name}");
        WriteLog($"Available downpatch versions: {_versionComboBox.Items.Count}");
    }

    private int GetVersionIndex(string versionName)
    {
        return _versions.FindIndex(version => string.Equals(version.Name, versionName, StringComparison.OrdinalIgnoreCase));
    }

    private DownpatchVersion? DetectInstalledVersion(string doomRootFolder)
    {
        string exePath = Path.Combine(doomRootFolder, "DOOMEternalx64vk.exe");
        _installedExecutableSize = null;
        if (!File.Exists(exePath))
        {
            return null;
        }

        long exeSize = new FileInfo(exePath).Length;
        _installedExecutableSize = exeSize;
        WriteLogThreadSafe($"DOOM Eternal executable: {exePath}");
        WriteLogThreadSafe($"Executable size: {exeSize} bytes");
        WriteLogThreadSafe($"Versions source: {FindVersionsJsonPath() ?? "not found"}");
        DownpatchVersion[] candidates = _versions.Where(version =>
            long.TryParse(version.Size, out long size) && size == exeSize).ToArray();
        if (candidates.Length <= 1)
            return candidates.SingleOrDefault();

        // A receipt is written only after a successful Steam download. Its recorded
        // files must still match, so a stale receipt cannot identify a changed copy.
        DownpatchVersion? receiptVersion = DetectReceiptVersion(doomRootFolder, candidates);
        if (receiptVersion is not null) return receiptVersion;
        Dictionary<string, string?> hashes = new(StringComparer.OrdinalIgnoreCase);
        string? FileHash(string relative)
        {
            if (hashes.TryGetValue(relative, out string? cached)) return cached;
            try
            {
                using FileStream file = File.OpenRead(PathSafety.Resolve(doomRootFolder, relative));
                return hashes[relative] = Convert.ToHexString(SHA1.HashData(file));
            }
            catch (IOException) { return hashes[relative] = null; }
            catch (UnauthorizedAccessException) { return hashes[relative] = null; }
        }
        DownpatchVersion[] matches = candidates.Where(version => version.DetectionFiles is { Length: > 0 } &&
            version.DetectionFiles.All(file => string.Equals(FileHash(file.Path), file.Sha1, StringComparison.OrdinalIgnoreCase))).ToArray();
        if (matches.Length == 1) return matches[0];
        DownpatchVersion? steamVersion = DetectSteamSnapshot(doomRootFolder, candidates.Where(version => version.DetectionFiles is not { Length: > 0 }).ToArray());
        if (steamVersion is not null) return steamVersion;
        WriteLogThreadSafe(L("AmbiguousBuild"));
        return null;
    }

    private static readonly string[] ReceiptPaths = ["DOOMEternalx64vk.exe", "launcher/idTechLauncher.exe", "launcher/resources/app.asar"];
    private const string VersionReceiptName = "eternaldownpatcher-version.json";

    private static DownpatchVersion? DetectReceiptVersion(string folder, DownpatchVersion[] candidates)
    {
        try
        {
            string path = PathSafety.Resolve(folder, VersionReceiptName);
            if (!File.Exists(path) || new FileInfo(path).Length > 16 * 1024) return null;
            VersionReceipt? receipt = JsonSerializer.Deserialize<VersionReceipt>(File.ReadAllText(path));
            if (receipt is not { FormatVersion: 1, Files.Length: 3, ManifestIds.Length: 8 } ||
                receipt.Files.Any(file => file is null) || !receipt.Files.Select(file => file.Path).SequenceEqual(ReceiptPaths)) return null;
            DownpatchVersion? version = candidates.FirstOrDefault(candidate => candidate.ManifestIds.SequenceEqual(receipt.ManifestIds));
            if (version is null) return null;
            foreach (ReceiptFile file in receipt.Files)
            {
                string filePath = PathSafety.Resolve(folder, file.Path);
                if (!File.Exists(filePath) || new FileInfo(filePath).Length != file.Size) return null;
                using FileStream input = File.OpenRead(filePath);
                if (!string.Equals(Convert.ToHexString(SHA256.HashData(input)), file.Sha256, StringComparison.OrdinalIgnoreCase)) return null;
            }
            return version;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException or InvalidOperationException)
        { return null; }
    }

    private static void SaveVersionReceipt(string folder, DownpatchVersion version, CancellationToken cancellationToken)
    {
        if (version.BuildId is null) return;
        ReceiptFile[] files = ReceiptPaths.Select(relative =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            string path = PathSafety.Resolve(folder, relative);
            using FileStream input = File.OpenRead(path);
            return new ReceiptFile(relative, input.Length, Convert.ToHexString(SHA256.HashData(input)));
        }).ToArray();
        cancellationToken.ThrowIfCancellationRequested();
        File.WriteAllText(PathSafety.Resolve(folder, VersionReceiptName),
            JsonSerializer.Serialize(new VersionReceipt(1, version.ManifestIds, files)));
    }

    private static DownpatchVersion? DetectSteamSnapshot(string folder, DownpatchVersion[] candidates)
    {
        try
        {
            DirectoryInfo? common = Directory.GetParent(Path.GetFullPath(folder));
            if (common is null || !common.Name.Equals("common", StringComparison.OrdinalIgnoreCase) || common.Parent is null) return null;
            string path = PathSafety.Resolve(common.Parent.FullName, "appmanifest_782330.acf");
            if (!File.Exists(path) || new FileInfo(path).Length > 1024 * 1024) return null;
            string text = File.ReadAllText(path);
            string installDir = Regex.Match(text, "\"installdir\"\\s+\"([^\"]+)\"").Groups[1].Value;
            if (!string.Equals(installDir, Path.GetFileName(Path.TrimEndingDirectorySeparator(folder)), StringComparison.OrdinalIgnoreCase)) return null;
            // Read only the InstalledDepots block, never shared, staged or download depots.
            Match installed = Regex.Match(text, "\"InstalledDepots\"\\s*\\{(?<body>(?:[^{}]|\\{[^{}]*\\})*)\\}");
            if (!installed.Success) return null;
            Dictionary<string, string> manifests = Regex.Matches(installed.Groups["body"].Value,
                "\"(?<depot>[0-9]+)\"\\s*\\{[^{}]*?\"manifest\"\\s*\"(?<manifest>[0-9]+)\"[^{}]*\\}")
                .ToDictionary(match => match.Groups["depot"].Value, match => match.Groups["manifest"].Value);
            DownpatchVersion[] matches = candidates.Where(version => DepotIds.Select((depot, i) =>
                manifests.TryGetValue(depot.ToString(), out string? manifest) && manifest == version.ManifestIds[i]).All(match => match)).ToArray();
            return matches.Length == 1 ? matches[0] : null;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidOperationException or ArgumentException)
        { return null; }
    }

    private void OfferFullDownloadForUnknownVersion()
    {
        string message = LF("UnknownVersionRecovery", _installedExecutableSize?.ToString() ?? "?", _versions.LastOrDefault()?.Name ?? "?");
        WriteLog(message.Replace("\n", " "));
        if (MessageBox.Show(this, message, "EternalDownpatcher", MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2) == DialogResult.Yes)
            _downloadAllFilesRadioButton.Checked = true;
    }

    private string BuildGeneratedFileListPath(DownpatchVersion targetVersion)
    {
        int targetIndex = GetVersionIndex(targetVersion.Name);
        int installedIndex = _installedVersion is null ? -1 : GetVersionIndex(_installedVersion.Name);
        if (targetIndex < 0) throw new InvalidOperationException("Target version was not found in versions list.");
        if (!IsDirectUpdate && installedIndex < 0) throw new InvalidOperationException(L("InstalledVersionNotDetected"));
        DownpatchVersion? pending = _doomRootFolder is null ? null : FindPendingVersion(_doomRootFolder);
        if (pending is not null && (!IsDirectUpdate || !pending.ManifestIds.SequenceEqual(targetVersion.ManifestIds)))
            throw new InvalidOperationException(LF("PendingDirectError", pending.Name));
        if (pending is null && installedIndex >= 0 && targetIndex >= installedIndex)
            throw new InvalidOperationException(L("TargetMustBeOlder"));
        if (RequiresCompleteFileList(targetVersion))
        {
            string completePath = Path.Combine(GetAppWorkingFolder(), "filelist.txt");
            File.WriteAllText(completePath, "regex:.*" + Environment.NewLine);
            WriteLog(L(IsDirectUpdate ? "DirectTargetCheck" : "CompleteTargetDownloadWarning"));
            return completePath;
        }

        if (FindVerifiedRollbackFileList(targetVersion) is VerifiedRollbackFileList verified)
        {
            string path = Path.Combine(GetAppWorkingFolder(), "filelist.txt");
            // DepotDownloader normalizes manifest names to forward slashes.
            File.WriteAllLines(path, verified.Files.OrderBy(file => file, StringComparer.OrdinalIgnoreCase)
                .Select(file => "regex:^" + Regex.Escape(file.Replace('\\', '/')) + "$"));
            WriteLog(LF("VerifiedRollbackFiles", verified.Files.Length));
            return path;
        }

        HashSet<string> aggregatedFiles = new(StringComparer.OrdinalIgnoreCase);
        List<string> includedFileLists = [];
        WriteLog($"Target version: {targetVersion.Name}");
        WriteLog($"Installed version: {_installedVersion!.Name}");
        if (string.Equals(targetVersion.Name, "1.0", StringComparison.OrdinalIgnoreCase) && FindRawFileListPath("1.1.0")is not null)
        {
            includedFileLists.Add("1.1.0");
        }

        for (int i = targetIndex + 1; i <= installedIndex; i++)
        {
            includedFileLists.Add(_versions[i].Name);
        }

        WriteLog("Included filelists:");
        foreach (string intermediateName in includedFileLists)
        {
            string? fileListPath = FindRawFileListPath(intermediateName);
            if (fileListPath is null)
            {
                throw new InvalidOperationException($"Filelist for intermediate version {intermediateName} was not found.");
            }

            int addedFromThisFile = 0;
            foreach (string file in ReadFileListEntries(fileListPath))
            {
                if (aggregatedFiles.Add(file))
                {
                    addedFromThisFile++;
                }
            }

            WriteLog($"- {intermediateName}: {addedFromThisFile} new entries");
        }

        if (aggregatedFiles.Count == 0)
        {
            throw new InvalidOperationException("Generated filelist is empty.");
        }

        string generatedFileListPath = Path.Combine(GetAppWorkingFolder(), "filelist.txt");
        using StreamWriter writer = new(generatedFileListPath, false);
        foreach (string file in aggregatedFiles.OrderBy(file => file, StringComparer.OrdinalIgnoreCase))
        {
            writer.WriteLine("regex:" + Regex.Escape(file));
        }

        WriteLog($"Generated filelist: {generatedFileListPath}");
        WriteLog($"Generated filelist entries: {aggregatedFiles.Count}");
        return generatedFileListPath;
    }

    private VerifiedRollbackFileList? FindVerifiedRollbackFileList(DownpatchVersion targetVersion)
    {
        if (_installedVersion is null ||
            (_doomRootFolder is not null && FindPendingVersion(_doomRootFolder) is not null)) return null;
        return targetVersion.VerifiedRollbackFileLists?.FirstOrDefault(list =>
            list.SourceManifestIds.SequenceEqual(_installedVersion.ManifestIds));
    }

    private bool RequiresCompleteFileList(DownpatchVersion targetVersion)
    {
        if (IsDirectUpdate && (_installedVersion is null || _installedVersion.Name == targetVersion.Name ||
            (_doomRootFolder is not null && FindPendingVersion(_doomRootFolder) is not null))) return true;
        if (_installedVersion is null || FindVerifiedRollbackFileList(targetVersion) is not null) return false;
        int target = GetVersionIndex(targetVersion.Name);
        int installed = GetVersionIndex(_installedVersion.Name);
        return target >= 0 && installed > target && _versions.Skip(target + 1).Take(installed - target)
            .Any(version => version.RequiresCompleteFileList);
    }

    private static IEnumerable<string> ReadFileListEntries(string fileListPath)
    {
        string text = File.ReadAllText(fileListPath);
        string[] entries = text.Split([' ', '\r', '\n', '\t'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        foreach (string entry in entries)
        {
            string file = entry.Trim();
            if (string.IsNullOrWhiteSpace(file))
            {
                continue;
            }

            if (file.StartsWith("regex:", StringComparison.OrdinalIgnoreCase))
            {
                file = file["regex:".Length..];
            }

            yield return file;
        }
    }

    private string? FindRawFileListPath(string versionName)
    {
        string[] dataFolders = _versionDataFolder is null ? [] : [_versionDataFolder];
        string[] wantedNames = [versionName, versionName + ".txt"];
        foreach (string dataFolder in dataFolders)
        {
            if (!Directory.Exists(dataFolder))
            {
                continue;
            }

            foreach (string wantedName in wantedNames)
            {
                string directPath = Path.Combine(dataFolder, wantedName);
                if (File.Exists(directPath))
                {
                    return Path.GetFullPath(directPath);
                }
            }

            foreach (string file in Directory.EnumerateFiles(dataFolder))
            {
                string fileName = Path.GetFileName(file);
                foreach (string wantedName in wantedNames)
                {
                    if (string.Equals(fileName, wantedName, StringComparison.OrdinalIgnoreCase))
                    {
                        return Path.GetFullPath(file);
                    }
                }
            }
        }

        return null;
    }

    private static string? GetStringProperty(JsonElement element, params string[] names)
    {
        foreach (string name in names)
        {
            if (!element.TryGetProperty(name, out JsonElement property))
            {
                continue;
            }

            return property.ValueKind switch
            {
                JsonValueKind.String => property.GetString(),
                JsonValueKind.Number => property.GetRawText(),
                _ => null
            };
        }

        return null;
    }

    private static bool TryGetProperty(JsonElement element, out JsonElement value, params string[] names)
    {
        foreach (string name in names)
        {
            if (element.TryGetProperty(name, out value))
            {
                return true;
            }
        }

        value = default;
        return false;
    }

    private DownpatchVersion? GetSelectedVersion()
    {
        string? selectedName = _versionComboBox.SelectedItem?.ToString();
        if (string.IsNullOrWhiteSpace(selectedName))
        {
            return null;
        }

        return _versions.FirstOrDefault(version => string.Equals(version.Name, selectedName, StringComparison.OrdinalIgnoreCase));
    }

    private sealed record DetectionFile(string Path, string Size, string Sha1);
    private sealed record ReceiptFile(string Path, long Size, string Sha256);
    private sealed record VersionReceipt(int FormatVersion, string[] ManifestIds, ReceiptFile[] Files);
    private sealed record VerifiedRollbackFileList(string[] SourceManifestIds, string[] Files);
    private sealed record DownpatchVersion(string Name, string Size, string[] ManifestIds, bool RequiresCompleteFileList = false,
        string? BuildId = null, DetectionFile[]? DetectionFiles = null, VerifiedRollbackFileList[]? VerifiedRollbackFileLists = null);

    #endregion

    #region Files and folders

    private void OpenOutputFolderButton_Click(object? sender, EventArgs e)
    {
        string folder = _downpatchFolderTextBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(folder))
        {
            return;
        }

        if (!Directory.Exists(folder))
        {
            ShowErrorKey("OutputFolderDoesNotExist");
            return;
        }

        Process.Start(new ProcessStartInfo { FileName = folder, UseShellExecute = true });
    }

    internal static string GetAppWorkingFolder()
    {
        string current = Environment.CurrentDirectory;
        if (Directory.Exists(Path.Combine(current, "data")) || Directory.EnumerateFiles(current, "*.csproj").Any())
        {
            return current;
        }

        return AppContext.BaseDirectory;
    }

    private static string GetDefaultPatchFilesFolder()
    {
        return Path.Combine(GetAppWorkingFolder(), "DOWNPATCH_FILES");
    }

    private static void ClearDirectory(string folder)
    {
        foreach (string file in Directory.EnumerateFiles(folder))
        {
            File.Delete(file);
        }

        foreach (string directory in Directory.EnumerateDirectories(folder))
        {
            Directory.Delete(directory, true);
        }
    }

    private static void EnsureSteamAppIdFile(string folder)
    {
        File.WriteAllText(Path.Combine(folder, "steam_appid.txt"), "782330");
    }

    private static bool IsValidDoomRootFolder(string folder)
    {
        return Directory.Exists(folder) && File.Exists(Path.Combine(folder, "DOOMEternalx64vk.exe"));
    }

    private static bool AreSameDirectory(string first, string second)
    {
        string firstFull = NormalizeDirectoryPath(first);
        string secondFull = NormalizeDirectoryPath(second);
        return string.Equals(firstFull, secondFull, StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsSubdirectoryOf(string possibleChild, string possibleParent)
    {
        string child = NormalizeDirectoryPath(possibleChild);
        string parent = NormalizeDirectoryPath(possibleParent);
        return child.StartsWith(parent, StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizeDirectoryPath(string path)
    {
        return Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
    }

    #endregion
}

#region GitHub version data cache

internal static class VersionDataCache
{
    internal const string ArchiveUrl = "https://codeload.github.com/mrshadowq5/EternalDownpatcher/zip/refs/heads/main";
    private const int MaxArchiveBytes = 32 * 1024 * 1024;
    private const int MaxMetadataBytes = 8 * 1024 * 1024;
    private const int MaxFileBytes = 1024 * 1024;

    internal static string CacheRoot => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "EternalDownpatcher", "version-data");

    internal static string? FindCurrentFolder(string cacheRoot)
    {
        try
        {
            string pointer = PathSafety.Resolve(cacheRoot, "current.txt");
            if (!File.Exists(pointer) || new FileInfo(pointer).Length > 100)
                return null;
            string name = File.ReadAllText(pointer).Trim();
            if (!Regex.IsMatch(name, @"\Asnapshot-[0-9a-f]{32}\z"))
                return null;
            string folder = PathSafety.Resolve(cacheRoot, name);
            MainForm.ValidateVersionDataFolder(folder);
            return folder;
        }
        catch (Exception)
        {
            // A missing, inaccessible or damaged cache must not prevent using bundled data.
            return null;
        }
    }

    internal static async Task<string> RefreshAsync(string cacheRoot, HttpClient client, CancellationToken cancellationToken)
    {
        // One archive represents one repository snapshot, even if main changes during the download.
        using HttpResponseMessage response = await client.GetAsync(ArchiveUrl, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        if (response.Content.Headers.ContentLength > MaxArchiveBytes)
            throw new InvalidDataException("The GitHub archive is too large.");
        using MemoryStream download = new();
        using (Stream network = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false))
            await CopyLimitedAsync(network, download, MaxArchiveBytes, cancellationToken).ConfigureAwait(false);
        download.Position = 0;
        using ZipArchive archive = new(download, ZipArchiveMode.Read);
        ZipArchiveEntry[] versionEntries = archive.Entries.Where(entry =>
            Regex.IsMatch(entry.FullName, @"\A[^/]+/data/versions\.json\z")).ToArray();
        if (versionEntries.Length != 1)
            throw new InvalidDataException("The GitHub archive must contain data/versions.json.");
        string prefix = versionEntries[0].FullName[..^"versions.json".Length];
        ZipArchiveEntry[] entries = archive.Entries.Where(entry =>
            entry.FullName.StartsWith(prefix, StringComparison.Ordinal) &&
            entry.FullName == prefix + entry.Name &&
            (entry.Name == "versions.json" || Regex.IsMatch(entry.Name, @"\A[0-9]+(?:\.[0-9]+)*\.txt\z"))).ToArray();
        if (entries.Length > 512 || entries.Sum(entry => entry.Length) > MaxMetadataBytes ||
            entries.Select(entry => entry.Name).Distinct(StringComparer.OrdinalIgnoreCase).Count() != entries.Length)
            throw new InvalidDataException("The GitHub metadata exceeds its limits or contains duplicate files.");

        PathSafety.RejectLinks(cacheRoot);
        Directory.CreateDirectory(cacheRoot);
        string staging = Path.Combine(cacheRoot, "staging-" + Guid.NewGuid().ToString("N"));
        string pointerTemp = Path.Combine(cacheRoot, "current-" + Guid.NewGuid().ToString("N") + ".tmp");
        string? uncommittedSnapshot = null;
        try
        {
            Directory.CreateDirectory(staging);
            foreach (ZipArchiveEntry entry in entries)
            {
                if (entry.Length > MaxFileBytes)
                    throw new InvalidDataException($"Metadata file is too large: {entry.Name}");
                // Only the approved basename is used; no archive paths are extracted.
                using Stream input = entry.Open();
                using FileStream output = new(Path.Combine(staging, entry.Name), FileMode.CreateNew, FileAccess.Write);
                await CopyLimitedAsync(input, output, MaxFileBytes, cancellationToken).ConfigureAwait(false);
            }
            cancellationToken.ThrowIfCancellationRequested();
            MainForm.ValidateVersionDataFolder(staging);
            string? current = FindCurrentFolder(cacheRoot);
            if (current is not null && Fingerprint(current).SequenceEqual(Fingerprint(staging)))
                return current;

            string name = "snapshot-" + Guid.NewGuid().ToString("N");
            string snapshot = Path.Combine(cacheRoot, name);
            Directory.Move(staging, snapshot);
            uncommittedSnapshot = snapshot;
            await File.WriteAllTextAsync(pointerTemp, name, cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            string pointer = PathSafety.Resolve(cacheRoot, "current.txt");
            // Publish only after every required file has been downloaded and validated.
            File.Move(pointerTemp, pointer, overwrite: true);
            uncommittedSnapshot = null;
            return snapshot;
        }
        finally
        {
            TryDeleteTemporary(staging);
            if (uncommittedSnapshot is not null)
                TryDeleteTemporary(uncommittedSnapshot);
            try { File.Delete(pointerTemp); }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
    }

    private static byte[] Fingerprint(string folder)
    {
        using IncrementalHash hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        foreach (string file in Directory.EnumerateFiles(folder).OrderBy(Path.GetFileName, StringComparer.Ordinal))
        {
            hash.AppendData(System.Text.Encoding.UTF8.GetBytes(Path.GetFileName(file) + "\0"));
            hash.AppendData(File.ReadAllBytes(file));
        }
        return hash.GetHashAndReset();
    }

    private static async Task CopyLimitedAsync(Stream input, Stream output, int limit, CancellationToken cancellationToken)
    {
        byte[] buffer = new byte[64 * 1024];
        int total = 0;
        int count;
        while ((count = await input.ReadAsync(buffer, cancellationToken).ConfigureAwait(false)) > 0)
        {
            total += count;
            if (total > limit)
                throw new InvalidDataException("The downloaded data exceeds its size limit.");
            await output.WriteAsync(buffer.AsMemory(0, count), cancellationToken).ConfigureAwait(false);
        }
    }

    private static void TryDeleteTemporary(string folder)
    {
        try
        {
            PathSafety.RejectLinks(folder);
            if (Directory.Exists(folder))
                Directory.Delete(folder, recursive: true);
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
        catch (InvalidOperationException) { }
    }
}

#endregion
