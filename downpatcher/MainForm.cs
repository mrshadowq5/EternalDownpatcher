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
    #region State and lifecycle

    internal const string AppVersion = "1.3"; // Idea by mrshadow, coded by Deepseek 
    private readonly UserSettings _userSettings;
    private readonly Button _themeToggleButton = new();
    private readonly Button _settingsButton = new();
    private readonly Label _titleLabel = new();
    private readonly Label _subtitleLabel = new();
    private readonly Label _versionLabel = new();
    private readonly Label _loginLabel = new();
    private readonly Label _passwordLabel = new();
    private readonly Label _consoleLabel = new();
    private readonly ComboBox _versionComboBox = new();
    private readonly TextBox _steamUsernameTextBox = new();
    private readonly TextBox _steamPasswordTextBox = new();
    private readonly TextBox _downpatchFolderTextBox = new();
    private readonly Button _startButton = new();
    private readonly Button _cancelButton = new();
    private readonly Button _selectGameFolderButton = new();
    private readonly Button _selectDownpatchFolderButton = new();
    private readonly Button _copyLogButton = new();
    private readonly Panel _downloadModePanel = new();
    private readonly RadioButton _downloadAllFilesRadioButton = new();
    private readonly RadioButton _patchExistingRadioButton = new();
    private readonly CheckBox _validateFilesCheckBox = new();
    private readonly Func<UserSettings, bool> _saveCredentialSettings;
    private readonly Label _gameFolderStatusLabel = new();
    private readonly Label _requiredFieldsLabel = new();
    private readonly RichTextBox _consoleBox = new();
    private readonly Label _folderLabel = new();
    private readonly Button _openOutputFolderButton = new();
    private readonly Button _restoreBackupButton = new();
    private readonly System.Windows.Forms.Timer _themeTransitionTimer = new();
    private bool _isDarkTheme;
    private bool _targetDarkTheme;
    private int _themeTransitionFrame;
    private const int ThemeTransitionFrames = 16;
    private ThemePalette _themeTransitionFrom;
    private ThemePalette _themeTransitionTo;
    private readonly List<DownpatchVersion> _versions = [];
    private string? _versionDataFolder;
    private CancellationTokenSource? _versionDataCancellation;
    private string? _doomRootFolder;
    private DownpatchVersion? _installedVersion;
    private long? _installedExecutableSize;
    private bool _isRunning;
    private CancellationTokenSource? _operationCancellation;
    private static readonly int[] DepotIds = [782332, 782333, 782334, 782335, 782336, 782337, 782338, 782339];
    public MainForm() : this(UserSettingsStore.Load(), UserSettingsStore.Save)
    {
    }

    internal MainForm(UserSettings userSettings, Func<UserSettings, bool> saveCredentialSettings)
    {
        _userSettings = userSettings;
        _saveCredentialSettings = saveCredentialSettings;
        Text = "EternalDownpatcher";
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(720, 455);
        MinimumSize = new Size(736, 494);
        FormBorderStyle = FormBorderStyle.FixedSingle;
        MaximizeBox = false;
        Font = new Font("Segoe UI", 9F);
        Icon? appIcon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
        if (appIcon is not null)
        {
            Icon = appIcon;
        }

        BuildInterface();
        RestoreSteamCredentials();
        ConfigureThemeTransition();
        _isDarkTheme = _userSettings.DarkMode;
        ApplyLanguage();
        ApplyTheme(_userSettings.DarkMode);
        LoadVersions();
        // Apply the default mode after the controls and their event handlers are ready.
        _downloadAllFilesRadioButton.Checked = true;
        WriteLog(L("WelcomeLog"));
        WriteLog(LF("VersionLog", AppVersion));
        WriteLog(L("SelectVersionLog"));
        if (!_downloadAllFilesRadioButton.Checked)
            WriteLog(L("SelectRootFolderLog"));
        WriteLog(LF("WorkingFolderLog", GetAppWorkingFolder()));
        WriteLog(LF("DefaultPatchFolderLog", GetDefaultPatchFilesFolder()));
        _depotUpdateTimer.Tick += ShowPendingDepotDownloaderUpdate;
        Shown += async (_, _) =>
        {
            ActiveControl = _startButton;
            await Task.WhenAll(UpdateVersionDataAsync(), CheckDepotDownloaderUpdateAsync());
        };
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        _versionDataCancellation?.Cancel();
        CancelDepotDownloaderUpdateCheck();
        if (_isRunning)
        {
            e.Cancel = true;
            _operationCancellation?.Cancel();
            WriteLog(L("WaitBeforeClosing"));
        }

        base.OnFormClosing(e);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            CancelDepotDownloaderUpdateCheck();
            _depotUpdateTimer.Dispose();
            DisposeButtonIcons();
            CancellationTokenSource? versionCancellation = _versionDataCancellation;
            _versionDataCancellation = null;
            // The async updater owns and disposes this source when its request ends.
            versionCancellation?.Cancel();
            CancellationTokenSource? cancellation = _operationCancellation;
            _operationCancellation = null;
            cancellation?.Cancel();
            cancellation?.Dispose();
        }

        base.Dispose(disposing);
    }

    #endregion

    #region Log and errors

    private void CopyLogButton_Click(object? sender, EventArgs e)
    {
        if (string.IsNullOrWhiteSpace(_consoleBox.Text))
        {
            return;
        }

        Clipboard.SetText(_consoleBox.Text);
        WriteLog("Console log copied to clipboard.");
    }

    private void ShowError(string message)
    {
        WriteLog($"ERROR: {message}");
        MessageBox.Show(this, message, "EternalDownpatcher", MessageBoxButtons.OK, MessageBoxIcon.Error);
    }

    private void ShowErrorKey(string key, params object[] args)
    {
        string message = LF(key, args);
        WriteLog($"ERROR: {message}");
        MessageBox.Show(this, message, "EternalDownpatcher", MessageBoxButtons.OK, MessageBoxIcon.Error);
    }

    private void WriteLogThreadSafe(string message)
    {
        if (IsDisposed)
        {
            return;
        }

        if (InvokeRequired)
        {
            BeginInvoke(new Action(() => WriteLog(message)));
            return;
        }

        WriteLog(message);
    }

    private void WriteLog(string message)
    {
        string timestamp = DateTime.Now.ToString("HH:mm:ss");
        _consoleBox.AppendText($"[{timestamp}] > {message}{Environment.NewLine}");
        _consoleBox.SelectionStart = _consoleBox.TextLength;
        _consoleBox.ScrollToCaret();
    }

    #endregion

    #region Saved credentials

    private void RestoreSteamCredentials()
    {
        _steamUsernameTextBox.Text = _userSettings.RememberLogin ? _userSettings.SteamLogin : "";
        _steamPasswordTextBox.Text = _userSettings.RememberPassword
            ? SteamPasswordProtection.Decrypt(_userSettings.SteamPassword)
            : "";
    }

    private void SaveSteamCredentials()
    {
        bool saved;
        try
        {
            SteamCredentialSettings.Capture(_userSettings, _steamUsernameTextBox.Text,
                _steamPasswordTextBox.Text, _userSettings.RememberLogin, _userSettings.RememberPassword);
            saved = _saveCredentialSettings(_userSettings);
        }
        catch (CryptographicException)
        {
            saved = false;
        }

        if (!saved)
        {
            MessageBox.Show(this, L("CredentialSaveFailed"), "EternalDownpatcher",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    #endregion

    #region Steam Guard requests

    private Task<string?> RequestSteamGuardCodeAsync(CancellationToken cancellationToken)
    {
        TaskCompletionSource<string?> completion = new(TaskCreationOptions.RunContinuationsAsynchronously);

        void ShowPrompt()
        {
            if (cancellationToken.IsCancellationRequested || IsDisposed)
            {
                completion.TrySetCanceled();
                return;
            }
            try
            {
                using SteamGuardForm dialog = new(_isDarkTheme, L("SteamGuardPrompt"),
                    L("SteamGuardConfirm"), L("SteamGuardCancel"));
                void ClosePrompt()
                {
                    try
                    {
                        if (dialog.IsHandleCreated && !dialog.IsDisposed)
                            dialog.BeginInvoke(new Action(dialog.Close));
                    }
                    catch (InvalidOperationException)
                    {
                        // The dialog may have closed while cancellation was delivered.
                    }
                }
                using CancellationTokenRegistration registration = cancellationToken.Register(ClosePrompt);
                dialog.Shown += (_, _) =>
                {
                    if (cancellationToken.IsCancellationRequested)
                        dialog.Close();
                };
                DialogResult result = dialog.ShowDialog(this);
                if (cancellationToken.IsCancellationRequested)
                    completion.TrySetCanceled(cancellationToken);
                else
                    completion.TrySetResult(result == DialogResult.OK ? dialog.Code : null);
            }
            catch (Exception error)
            {
                completion.TrySetException(error);
            }
        }

        try
        {
            if (InvokeRequired)
                BeginInvoke(new Action(ShowPrompt));
            else
                ShowPrompt();
        }
        catch (InvalidOperationException)
        {
            completion.TrySetCanceled();
        }
        return completion.Task;
    }

    #endregion
}
