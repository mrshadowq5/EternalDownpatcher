using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace EternalDownpatcher.WinForms;

public sealed class MainForm : Form
{
    private readonly UserSettings _userSettings;

    private readonly Button _themeToggleButton = new();
    private readonly Button _settingsButton = new();

    private readonly Label _titleLabel = new();
    private readonly Label _subtitleLabel = new();
    private readonly Label _versionLabel = new();
    private readonly Label _usernameLabel = new();
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
    private readonly Button _helpButton = new();
    private readonly Button _copyLogButton = new();
    private readonly CheckBox _downloadAllFilesCheckBox = new();
    private readonly CheckBox _validateFilesCheckBox = new();
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

    private Image? _settingsIconImage;

    private readonly List<DownpatchVersion> _versions = [];
    private string? _doomRootFolder;
    private DownpatchVersion? _installedVersion;

    private bool _isRunning;
    private CancellationTokenSource? _operationCancellation;

    private static readonly int[] DepotIds =
    [
        782332,
        782333,
        782334,
        782335,
        782336,
        782337,
        782338,
        782339
    ];

    public MainForm()
    {
        _userSettings = UserSettingsStore.Load();

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
        ConfigureThemeTransition();

        _isDarkTheme = _userSettings.DarkMode;

        ApplyLanguage();
        ApplyTheme(_userSettings.DarkMode);
        LoadVersions();

        WriteLog(L("WelcomeLog"));
        WriteLog(LF("VersionLog", "1.2")); //EternalDownpatcher version
        WriteLog(L("SelectVersionLog"));
        WriteLog(L("SelectRootFolderLog"));
        WriteLog(LF("WorkingFolderLog", GetAppWorkingFolder()));
        WriteLog(LF("DefaultPatchFolderLog", GetDefaultPatchFilesFolder()));
        WriteLog(L("ClickHelpLog"));

        Shown += (_, _) => ActiveControl = _startButton;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _settingsIconImage?.Dispose();
            _operationCancellation?.Dispose();
        }

        base.Dispose(disposing);
    }

    private void BuildInterface()
    {
        _titleLabel.Text = "EternalDownpatcher";
        _titleLabel.AutoSize = false;
        _titleLabel.Font = new Font("Segoe UI", 11F, FontStyle.Italic);
        _titleLabel.SetBounds(12, 8, 260, 22);

        _subtitleLabel.Text = "";
        _subtitleLabel.AutoSize = false;
        _subtitleLabel.ForeColor = Color.DimGray;
        _subtitleLabel.SetBounds(12, 30, 260, 18);

        _themeToggleButton.SetBounds(626, 10, 34, 28);
        _themeToggleButton.Font = new Font("Segoe UI Symbol", 10F);
        _themeToggleButton.Text = "☀";
        _themeToggleButton.Click += ThemeToggleButton_Click;

        _settingsButton.SetBounds(672, 10, 34, 28);
        _settingsButton.Text = "";
        _settingsButton.ImageAlign = ContentAlignment.MiddleCenter;
        _settingsButton.Click += SettingsButton_Click;

        try
        {
            _settingsIconImage = LoadButtonImage("settings.png", 16);
            _settingsButton.Image = _settingsIconImage;
        }
        catch
        {
            _settingsButton.Text = "⚙";
            _settingsButton.Font = new Font("Segoe UI Symbol", 10F);
        }

        _gameFolderStatusLabel.SetBounds(12, 55, 455, 22);
        _gameFolderStatusLabel.Text = "DOOM Eternal root folder has not been selected.";
        _gameFolderStatusLabel.ForeColor = Color.Red;
        _gameFolderStatusLabel.TextAlign = ContentAlignment.MiddleLeft;

        _selectGameFolderButton.SetBounds(472, 53, 170, 25);
        _selectGameFolderButton.Text = "Select DOOM Eternal Folder";
        _selectGameFolderButton.Click += SelectGameFolderButton_Click;

        _helpButton.SetBounds(650, 53, 58, 25);
        _helpButton.Text = "Help";
        _helpButton.Click += HelpButton_Click;

        _versionLabel.Text = "Game Version *";
        _versionLabel.AutoSize = false;
        _versionLabel.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
        _versionLabel.SetBounds(12, 92, 140, 20);

        _usernameLabel.Text = "Steam Username *";
        _usernameLabel.AutoSize = false;
        _usernameLabel.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
        _usernameLabel.SetBounds(160, 92, 160, 20);

        _passwordLabel.Text = "Steam Password *";
        _passwordLabel.AutoSize = false;
        _passwordLabel.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
        _passwordLabel.SetBounds(330, 92, 160, 20);

        _versionComboBox.SetBounds(12, 114, 140, 25);
        _versionComboBox.DropDownStyle = ComboBoxStyle.DropDownList;

        _steamUsernameTextBox.SetBounds(160, 114, 160, 25);

        _steamPasswordTextBox.SetBounds(330, 114, 160, 25);
        _steamPasswordTextBox.UseSystemPasswordChar = true;

        _requiredFieldsLabel.SetBounds(500, 117, 205, 20);
        _requiredFieldsLabel.Text = "* Required fields missing";
        _requiredFieldsLabel.ForeColor = Color.Red;

        _folderLabel.Text = "Patch Files Folder";
        _folderLabel.Font = new Font(Font, FontStyle.Bold);
        _folderLabel.SetBounds(12, 155, 180, 20);

        _downpatchFolderTextBox.SetBounds(12, 177, 590, 25);
        _downpatchFolderTextBox.Text = GetDefaultPatchFilesFolder();

        _selectDownpatchFolderButton.SetBounds(610, 176, 98, 26);
        _selectDownpatchFolderButton.Text = "Browse";
        _selectDownpatchFolderButton.Click += SelectDownpatchFolderButton_Click;

        _startButton.SetBounds(12, 219, 112, 27);
        _startButton.Text = "Downpatch";
        _startButton.Click += StartButton_Click;

        _cancelButton.SetBounds(130, 219, 75, 27);
        _cancelButton.Text = "Cancel";
        _cancelButton.Enabled = false;
        _cancelButton.Click += CancelButton_Click;

        _downloadAllFilesCheckBox.SetBounds(215, 223, 125, 22);
        _downloadAllFilesCheckBox.Text = "Download All Files";
        _downloadAllFilesCheckBox.Checked = false;
        _downloadAllFilesCheckBox.Enabled = true;
        _downloadAllFilesCheckBox.CheckedChanged += DownloadAllFilesCheckBox_CheckedChanged;

        _validateFilesCheckBox.SetBounds(345, 223, 145, 22);
        _validateFilesCheckBox.Text = "Validate files";
        _validateFilesCheckBox.Checked = true;

        _consoleLabel.Text = "Output Log";
        _consoleLabel.Font = new Font(Font, FontStyle.Bold);
        _consoleLabel.SetBounds(12, 261, 120, 20);

        _copyLogButton.SetBounds(610, 256, 98, 27);
        _copyLogButton.Text = "Copy Log";
        _copyLogButton.Click += CopyLogButton_Click;

        _consoleBox.SetBounds(12, 287, 696, 125);
        _consoleBox.ReadOnly = true;
        _consoleBox.BackColor = Color.Black;
        _consoleBox.ForeColor = Color.White;
        _consoleBox.Font = new Font("Consolas", 9.5F);
        _consoleBox.BorderStyle = BorderStyle.FixedSingle;
        _consoleBox.ScrollBars = RichTextBoxScrollBars.Vertical;

        _restoreBackupButton.SetBounds(365, 421, 125, 24);
        _restoreBackupButton.Text = "Restore Backup";
        _restoreBackupButton.Click += RestoreBackupButton_Click;

        _openOutputFolderButton.SetBounds(500, 421, 98, 24);
        _openOutputFolderButton.Text = "Open Folder";
        _openOutputFolderButton.Click += OpenOutputFolderButton_Click;

        Controls.Add(_titleLabel);
        Controls.Add(_subtitleLabel);
        Controls.Add(_themeToggleButton);
        Controls.Add(_settingsButton);
        Controls.Add(_gameFolderStatusLabel);
        Controls.Add(_selectGameFolderButton);
        Controls.Add(_helpButton);
        Controls.Add(_versionLabel);
        Controls.Add(_usernameLabel);
        Controls.Add(_passwordLabel);
        Controls.Add(_versionComboBox);
        Controls.Add(_steamUsernameTextBox);
        Controls.Add(_steamPasswordTextBox);
        Controls.Add(_requiredFieldsLabel);
        Controls.Add(_folderLabel);
        Controls.Add(_downpatchFolderTextBox);
        Controls.Add(_selectDownpatchFolderButton);
        Controls.Add(_startButton);
        Controls.Add(_cancelButton);
        Controls.Add(_downloadAllFilesCheckBox);
        Controls.Add(_validateFilesCheckBox);
        Controls.Add(_consoleLabel);
        Controls.Add(_copyLogButton);
        Controls.Add(_consoleBox);
        Controls.Add(_restoreBackupButton);
        Controls.Add(_openOutputFolderButton);
    }

    private static Image LoadButtonImage(string fileName, int size)
    {
        string path = Path.Combine(GetAppWorkingFolder(), "assets", fileName);

        using Image source = Image.FromFile(path);

        Bitmap bitmap = new(size, size);

        using Graphics graphics = Graphics.FromImage(bitmap);
        graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
        graphics.Clear(Color.Transparent);
        graphics.DrawImage(source, new Rectangle(0, 0, size, size));

        return bitmap;
    }

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

        Process.Start(new ProcessStartInfo
        {
            FileName = folder,
            UseShellExecute = true
        });
    }

    private void RestoreBackupButton_Click(object? sender, EventArgs e)
    {
        if (_isRunning)
        {
            return;
        }

        string? targetFolder = _doomRootFolder;

        if (string.IsNullOrWhiteSpace(targetFolder) ||
            !IsValidDoomRootFolder(targetFolder))
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

        if (!Directory.Exists(backupFolder) ||
            !Directory.EnumerateFileSystemEntries(backupFolder).Any())
        {
            ShowErrorKey("BackupFolderInvalid");
            return;
        }

        DialogResult result = MessageBox.Show(
            this,
            $"Restore this backup into DOOM Eternal folder?\n\nBackup:\n{backupFolder}\n\nTarget:\n{targetFolder}",
            "Restore backup",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Warning);

        if (result != DialogResult.Yes)
        {
            WriteLog("Restore backup cancelled.");
            return;
        }

        try
        {
            WriteLog($"Restoring backup: {backupFolder}");
            WriteLog($"Restore target: {targetFolder}");

            CopyDirectory(backupFolder, targetFolder);

            WriteLog("Backup restored successfully.");

            MessageBox.Show(
                this,
                "Backup restored successfully.",
                "EternalDownpatcher",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }
        catch (Exception exception)
        {
            ShowErrorKey("RestoreBackupFailed", exception.Message);
        }
    }

    private static string GetAppWorkingFolder()
    {
        string current = Environment.CurrentDirectory;

        if (Directory.Exists(Path.Combine(current, "data")) ||
            Directory.EnumerateFiles(current, "*.csproj").Any())
        {
            return current;
        }

        return AppContext.BaseDirectory;
    }

    private static string GetDefaultPatchFilesFolder()
    {
        return Path.Combine(GetAppWorkingFolder(), "DOWNPATCH_FILES");
    }

    private void LoadVersions()
    {
        string? versionsJsonPath = FindVersionsJsonPath();

        if (versionsJsonPath is null)
        {
            ShowErrorKey("VersionsJsonNotFound");
            return;
        }

        try
        {
            _versions.Clear();
            _versionComboBox.Items.Clear();

            foreach (DownpatchVersion version in ReadVersionsJson(versionsJsonPath))
            {
                _versions.Add(version);
                _versionComboBox.Items.Add(version.Name);
            }

            RefreshDownpatchVersions();
            EnsureDefaultVersionSelection();

            WriteLog(LF("LoadedVersionsLog", _versions.Count));
            WriteLog(LF("VersionsSourceLog", versionsJsonPath));
        }
        catch (Exception exception)
        {
            ShowErrorKey("LoadVersionsFailed", exception.Message);
        }
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

        _versionComboBox.SelectedIndex = 0;
    }

    private static string? FindVersionsJsonPath()
    {
        string root = GetAppWorkingFolder();

        string[] candidates =
        [
            Path.Combine(root, "data", "versions.json"),
            Path.Combine(AppContext.BaseDirectory, "data", "versions.json")
        ];

        foreach (string candidate in candidates)
        {
            if (File.Exists(candidate))
            {
                return Path.GetFullPath(candidate);
            }
        }

        return null;
    }

    private static List<DownpatchVersion> ReadVersionsJson(string path)
    {
        string json = File.ReadAllText(path);

        using JsonDocument document = JsonDocument.Parse(json);

        JsonElement versionsElement;

        if (document.RootElement.ValueKind == JsonValueKind.Array)
        {
            versionsElement = document.RootElement;
        }
        else if (document.RootElement.ValueKind == JsonValueKind.Object &&
                 document.RootElement.TryGetProperty("versions", out JsonElement nestedVersions))
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

            versions.Add(
                new DownpatchVersion(
                    name.Trim(),
                    size.Trim(),
                    manifestIds.ToArray()));
        }

        if (versions.Count == 0)
        {
            throw new InvalidOperationException("No valid versions were found in versions.json.");
        }

        return versions;
    }

    private void RefreshDownpatchVersions()
    {
        string? selectedBefore = _versionComboBox.SelectedItem?.ToString();

        _versionComboBox.Items.Clear();

        if (_installedVersion is null || _downloadAllFilesCheckBox.Checked)
        {
            foreach (DownpatchVersion version in _versions)
            {
                _versionComboBox.Items.Add(version.Name);
            }

            if (!string.IsNullOrWhiteSpace(selectedBefore) &&
                _versionComboBox.Items.Contains(selectedBefore))
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

        if (!string.IsNullOrWhiteSpace(selectedBefore) &&
            _versionComboBox.Items.Contains(selectedBefore))
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
        return _versions.FindIndex(
            version => string.Equals(
                version.Name,
                versionName,
                StringComparison.OrdinalIgnoreCase));
    }

    private DownpatchVersion? DetectInstalledVersion(string doomRootFolder)
    {
        string exePath = Path.Combine(doomRootFolder, "DOOMEternalx64vk.exe");

        if (!File.Exists(exePath))
        {
            return null;
        }

        long exeSize = new FileInfo(exePath).Length;

        foreach (DownpatchVersion version in _versions)
        {
            if (!long.TryParse(version.Size.Trim(), out long versionSize))
            {
                continue;
            }

            if (versionSize == exeSize)
            {
                return version;
            }
        }

        return null;
    }

    private string BuildGeneratedFileListPath(DownpatchVersion targetVersion)
    {
        if (_installedVersion is null)
        {
            throw new InvalidOperationException(L("InstalledVersionNotDetected"));
        }

        int targetIndex = GetVersionIndex(targetVersion.Name);
        int installedIndex = GetVersionIndex(_installedVersion.Name);

        if (targetIndex < 0)
        {
            throw new InvalidOperationException("Target version was not found in versions list.");
        }

        if (installedIndex < 0)
        {
            throw new InvalidOperationException("Installed version was not found in versions list.");
        }

        if (targetIndex >= installedIndex)
        {
            throw new InvalidOperationException(L("TargetMustBeOlder"));
        }

        HashSet<string> aggregatedFiles = new(StringComparer.OrdinalIgnoreCase);
        List<string> includedFileLists = [];

        WriteLog($"Target version: {targetVersion.Name}");
        WriteLog($"Installed version: {_installedVersion.Name}");

        if (string.Equals(targetVersion.Name, "1.0", StringComparison.OrdinalIgnoreCase) &&
            FindRawFileListPath("1.1.0") is not null)
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
                throw new InvalidOperationException(
                    $"Filelist for intermediate version {intermediateName} was not found.");
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

        string generatedFileListPath = Path.Combine(
            GetAppWorkingFolder(),
            "filelist.txt");

        using StreamWriter writer = new(generatedFileListPath, false);

        foreach (string file in aggregatedFiles.OrderBy(file => file, StringComparer.OrdinalIgnoreCase))
        {
            writer.WriteLine("regex:" + Regex.Escape(file));
        }

        WriteLog($"Generated filelist: {generatedFileListPath}");
        WriteLog($"Generated filelist entries: {aggregatedFiles.Count}");

        return generatedFileListPath;
    }

    private static IEnumerable<string> ReadFileListEntries(string fileListPath)
    {
        string text = File.ReadAllText(fileListPath);

        string[] entries = text.Split(
            [' ', '\r', '\n', '\t'],
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

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

    private static string? FindRawFileListPath(string versionName)
    {
        string root = GetAppWorkingFolder();

        string[] dataFolders =
        [
            Path.Combine(root, "data"),
            Path.Combine(AppContext.BaseDirectory, "data")
        ];

        string[] wantedNames =
        [
            versionName,
            versionName + ".txt"
        ];

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

    private static string? GetStringProperty(
        JsonElement element,
        params string[] names)
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

    private static bool TryGetProperty(
        JsonElement element,
        out JsonElement value,
        params string[] names)
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

        return _versions.FirstOrDefault(
            version => string.Equals(
                version.Name,
                selectedName,
                StringComparison.OrdinalIgnoreCase));
    }

    private async void StartButton_Click(object? sender, EventArgs e)
    {
        if (_isRunning)
        {
            return;
        }

        bool downloadAllFiles = _downloadAllFilesCheckBox.Checked;

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

        if (string.IsNullOrWhiteSpace(_downpatchFolderTextBox.Text))
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
            if (_installedVersion is null)
            {
                ShowErrorKey("InstalledVersionNotDetected");
                return;
            }

            int targetIndex = GetVersionIndex(selectedVersion.Name);
            int installedIndex = GetVersionIndex(_installedVersion.Name);

            if (targetIndex < 0 || installedIndex < 0)
            {
                ShowErrorKey("UnableCompareVersions");
                return;
            }

            if (targetIndex >= installedIndex)
            {
                ShowErrorKey("TargetMustBeOlder");
                return;
            }

            try
            {
                fileListPath = BuildGeneratedFileListPath(selectedVersion);
            }
            catch (Exception exception)
            {
                ShowErrorKey("GenerateFilelistFailed", exception.Message);
                return;
            }
        }

        string? existingDepotDownloaderPath = FindDepotDownloaderPath();

        if (existingDepotDownloaderPath is null)
        {
            DialogResult downloadDepotResult = MessageBox.Show(
            this,
            L("DepotDownloaderNotFoundQuestion"),
            L("DownloadDepotDownloaderTitle"),
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Question);

            if (downloadDepotResult != DialogResult.Yes)
            {
                WriteLog("Operation cancelled. DepotDownloader was not downloaded.");
                return;
            }
        }

        string depotDownloaderPath;

        try
        {
            depotDownloaderPath = await FindOrDownloadDepotDownloaderAsync();
            WriteLog($"DepotDownloader ready: {depotDownloaderPath}");
        }
        catch (Exception exception)
        {
            ShowErrorKey("DepotDownloaderPrepareFailed", exception.Message);
            return;
        }

        string outputFolder = Path.GetFullPath(_downpatchFolderTextBox.Text.Trim());
        string? doomRootFolder = null;

        if (!downloadAllFiles)
        {
            doomRootFolder = Path.GetFullPath(_doomRootFolder!);

            if (!IsValidDoomRootFolder(doomRootFolder))
            {
                ShowErrorKey("InvalidDoomRootFolder");
                return;
            }

            if (AreSameDirectory(outputFolder, doomRootFolder))
            {
                ShowErrorKey("PatchFolderSameAsGame");
                return;
            }

            if (IsSubdirectoryOf(doomRootFolder, outputFolder))
            {
                ShowErrorKey("DoomFolderInsidePatchFolder");
                return;
            }
        }
        else
        {
            if (IsValidDoomRootFolder(outputFolder))
            {
                ShowErrorKey("DownloadFolderIsDoomRoot");
                return;
            }

            if (!string.IsNullOrWhiteSpace(_doomRootFolder) &&
                AreSameDirectory(outputFolder, _doomRootFolder))
            {
                ShowErrorKey("DownloadFolderSameAsRoot");
                return;
            }
        }

        if (!Directory.Exists(outputFolder))
        {
    DialogResult createFolderResult = MessageBox.Show(
        this,
        downloadAllFiles
            ? L("DownloadFolderDoesNotExistCreate")
            : L("PatchFolderDoesNotExistCreate"),
    downloadAllFiles ? L("CreateDownloadFolder") : L("CreatePatchFolder"),
    MessageBoxButtons.YesNo,
    MessageBoxIcon.Question);

            if (createFolderResult != DialogResult.Yes)
            {
                WriteLog("Operation cancelled. Output folder was not created.");
                return;
            }

            Directory.CreateDirectory(outputFolder);
            WriteLog($"Created output folder: {outputFolder}");
        }

        if (Directory.EnumerateFileSystemEntries(outputFolder).Any())
        {
            DialogResult clearResult = MessageBox.Show(
                this,
                downloadAllFiles
                    ? "The selected download folder is not empty.\n\nDownload All Files should use an empty folder.\n\nClear it now?"
                    : "The selected patch files folder is not empty.\n\nIt should be cleared before downloading new downpatch files.\n\nClear it now?",
                "Clear output folder",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning);

            if (clearResult != DialogResult.Yes)
            {
                WriteLog("Operation cancelled. Output folder was not cleared.");
                return;
            }

            ClearDirectory(outputFolder);
            WriteLog("Output folder cleared.");
        }

        DialogResult result = MessageBox.Show(
            this,
            downloadAllFiles
                ? "This will download a full standalone copy of the selected DOOM Eternal version.\n\nIt will not modify your selected Steam install.\n\nContinue?"
                : "This will download downpatch files using DepotDownloader and then apply them to the selected DOOM Eternal root folder.\n\nExisting replaced files will be backed up first.\n\nContinue?",
            downloadAllFiles ? "Download all files" : "Start downpatch",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Warning);

        if (result != DialogResult.Yes)
        {
            WriteLog("Operation cancelled before download.");
            return;
        }

        _requiredFieldsLabel.Text = L("Ready");
        ApplyStatusLabelColors(GetThemePalette(_isDarkTheme));

        _operationCancellation = new CancellationTokenSource();

        SetRunningState(true);

        try
        {
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
                WriteLog($"Filelist: {fileListPath}");
                WriteLog($"Patch files folder: {outputFolder}");
                WriteLog($"DOOM Eternal root folder: {doomRootFolder}");
            }

            for (int i = 0; i < DepotIds.Length; i++)
            {
                _operationCancellation.Token.ThrowIfCancellationRequested();

                int depotId = DepotIds[i];
                string manifestId = selectedVersion.ManifestIds[i];

                WriteLog($"Downloading depot {depotId}.");

                await RunDepotDownloaderAsync(
                    depotDownloaderPath,
                    depotId,
                    manifestId,
                    fileListPath,
                    !downloadAllFiles,
                    _steamUsernameTextBox.Text.Trim(),
                    _steamPasswordTextBox.Text,
                    outputFolder,
                    _operationCancellation.Token);

                WriteLog($"Depot {depotId} completed.");
            }

            if (!Directory.EnumerateFileSystemEntries(outputFolder).Any())
            {
                throw new InvalidOperationException(L("OutputFolderEmptyAfterDownload"));
            }

            WriteLog("Download completed.");

            if (downloadAllFiles)
            {
                EnsureSteamAppIdFile(outputFolder);

                WriteLog("steam_appid.txt created.");
                WriteLog("Download All Files completed successfully.");

                MessageBox.Show(
                    this,
                    "Download All Files completed successfully.\n\nYou can add DOOMEternalx64vk.exe as a Non-Steam Game.",
                    "EternalDownpatcher",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);

                return;
            }

            string backupFolder = Path.Combine(
                doomRootFolder!,
                "EternalDownpatcher_Backup_" + DateTime.Now.ToString("yyyyMMdd_HHmmss"));

            WriteLog($"Backup folder: {backupFolder}");
            WriteLog("Applying patch files...");

            int copiedFiles = await Task.Run(
                () => ApplyDownpatchFiles(
                    outputFolder,
                    doomRootFolder!,
                    backupFolder),
                _operationCancellation.Token);

            WriteLog($"Completed. Files copied: {copiedFiles}");
            WriteLog("Downpatch completed successfully.");

            MessageBox.Show(
                this,
                "Downpatch completed successfully.",
                "EternalDownpatcher",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }
        catch (OperationCanceledException)
        {
            WriteLog("Operation cancelled.");
        }
        catch (Exception exception)
        {
            WriteLog($"ERROR: {exception.Message}");

            MessageBox.Show(
                this,
                exception.Message,
                "EternalDownpatcher",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
        finally
        {
            _operationCancellation?.Dispose();
            _operationCancellation = null;

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

        if (!IsValidDoomRootFolder(selectedPath))
        {
            _doomRootFolder = null;
            _installedVersion = null;

            _gameFolderStatusLabel.Text = L("InvalidDoomRootFolderShort");
            ApplyStatusLabelColors(GetThemePalette(_isDarkTheme));

            RefreshDownpatchVersions();

            WriteLog($"ERROR: {L("InvalidFolderNoExe")}");

            MessageBox.Show(
                this,
                L("InvalidFolderNoExe"),
                "EternalDownpatcher",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);

            return;
        }

        _doomRootFolder = selectedPath;
        _installedVersion = DetectInstalledVersion(selectedPath);

        if (_installedVersion is null)
        {
            _gameFolderStatusLabel.Text = L("RootSelectedButVersionNotDetected");
            ApplyStatusLabelColors(GetThemePalette(_isDarkTheme));

            RefreshDownpatchVersions();

            WriteLog($"DOOM Eternal folder: {selectedPath}");
            WriteLog($"ERROR: {L("DetectVersionFailed")}");

            MessageBox.Show(
                this,
                L("DetectVersionFailed"),
                "EternalDownpatcher",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);

            return;
        }

        _gameFolderStatusLabel.Text =
            string.Format(L("RootSelected"), _installedVersion.Name);

        ApplyStatusLabelColors(GetThemePalette(_isDarkTheme));

        WriteLog($"DOOM Eternal folder: {selectedPath}");
        WriteLog($"Installed DOOM Eternal version: {_installedVersion.Name}");

        RefreshDownpatchVersions();
    }

    private void SelectDownpatchFolderButton_Click(object? sender, EventArgs e)
    {
        using FolderBrowserDialog dialog = new()
        {
            Description = "Select patch files folder",
            UseDescriptionForTitle = true,
            InitialDirectory = Directory.Exists(_downpatchFolderTextBox.Text)
                ? _downpatchFolderTextBox.Text
                : GetAppWorkingFolder()
        };

        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        _downpatchFolderTextBox.Text = Path.GetFullPath(dialog.SelectedPath);

        WriteLog($"Patch files folder: {_downpatchFolderTextBox.Text}");
    }

private void HelpButton_Click(object? sender, EventArgs e)
{
    using HelpForm helpForm = new(_isDarkTheme, _userSettings.Language);
    helpForm.ShowDialog(this);
}

    private void CopyLogButton_Click(object? sender, EventArgs e)
    {
        if (string.IsNullOrWhiteSpace(_consoleBox.Text))
        {
            return;
        }

        Clipboard.SetText(_consoleBox.Text);
        WriteLog("Console log copied to clipboard.");
    }

    private static string? FindDepotDownloaderPath()
    {
        string root = GetAppWorkingFolder();

        string[] candidates =
        [
            Path.Combine(root, "DepotDownloader.exe"),
            Path.Combine(root, "DepotDownloader", "DepotDownloader.exe"),
            Path.Combine(root, "DepotDownloader-2.4.6", "DepotDownloader.exe"),
            Path.Combine(AppContext.BaseDirectory, "DepotDownloader.exe"),
            Path.Combine(AppContext.BaseDirectory, "DepotDownloader", "DepotDownloader.exe")
        ];

        foreach (string candidate in candidates)
        {
            if (File.Exists(candidate))
            {
                return Path.GetFullPath(candidate);
            }
        }

        return null;
    }

    private static async Task<string> FindOrDownloadDepotDownloaderAsync()
    {
        string? existingPath = FindDepotDownloaderPath();

        if (existingPath is not null)
        {
            return existingPath;
        }

        string installFolder = Path.Combine(
            GetAppWorkingFolder(),
            "DepotDownloader");

        string zipPath = Path.Combine(
            installFolder,
            "DepotDownloader_latest.zip");

        string extractFolder = Path.Combine(
            installFolder,
            "_extract");

        Directory.CreateDirectory(installFolder);

        if (Directory.Exists(extractFolder))
        {
            Directory.Delete(extractFolder, true);
        }

        if (File.Exists(zipPath))
        {
            File.Delete(zipPath);
        }

        string downloadUrl =
            await GetLatestDepotDownloaderWindowsZipUrlAsync();

        using HttpClient httpClient = new();

        httpClient.DefaultRequestHeaders.UserAgent.ParseAdd(
            "EternalDownpatcher");

        byte[] zipBytes =
            await httpClient.GetByteArrayAsync(downloadUrl);

        await File.WriteAllBytesAsync(
            zipPath,
            zipBytes);

        ZipFile.ExtractToDirectory(
            zipPath,
            extractFolder,
            true);

        string? extractedExe =
            Directory
                .EnumerateFiles(
                    extractFolder,
                    "DepotDownloader.exe",
                    SearchOption.AllDirectories)
                .FirstOrDefault();

        if (extractedExe is null)
        {
            throw new InvalidOperationException(
                "DepotDownloader.exe was not found inside the downloaded archive.");
        }

        string extractedExeFolder =
            Path.GetDirectoryName(extractedExe)
            ?? throw new InvalidOperationException("Invalid DepotDownloader archive structure.");

        CopyDirectory(
            extractedExeFolder,
            installFolder);

        Directory.Delete(extractFolder, true);
        File.Delete(zipPath);

        string finalExePath = Path.Combine(
            installFolder,
            "DepotDownloader.exe");

        if (!File.Exists(finalExePath))
        {
            throw new InvalidOperationException(
                "DepotDownloader.exe was not installed correctly.");
        }

        return finalExePath;
    }

    private static async Task<string> GetLatestDepotDownloaderWindowsZipUrlAsync()
    {
        using HttpClient httpClient = new();

        httpClient.DefaultRequestHeaders.UserAgent.ParseAdd(
            "EternalDownpatcher");

        string json =
            await httpClient.GetStringAsync(
                "https://api.github.com/repos/SteamRE/DepotDownloader/releases/latest");

        using JsonDocument document =
            JsonDocument.Parse(json);

        JsonElement assets =
            document.RootElement.GetProperty("assets");

        string? fallbackZip = null;

        foreach (JsonElement asset in assets.EnumerateArray())
        {
            string? name = asset.GetProperty("name").GetString();
            string? url = asset.GetProperty("browser_download_url").GetString();

            if (string.IsNullOrWhiteSpace(name) ||
                string.IsNullOrWhiteSpace(url))
            {
                continue;
            }

            bool isZip =
                name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase);

            if (!isZip)
            {
                continue;
            }

            bool isWindows =
                name.Contains("windows", StringComparison.OrdinalIgnoreCase) ||
                name.Contains("win", StringComparison.OrdinalIgnoreCase);

            bool isX64 =
                name.Contains("x64", StringComparison.OrdinalIgnoreCase);

            if (isWindows && isX64)
            {
                return url;
            }

            fallbackZip ??= url;
        }

        if (fallbackZip is not null)
        {
            return fallbackZip;
        }

        throw new InvalidOperationException(
            "DepotDownloader zip archive was not found in GitHub release assets.");
    }

    private static void CopyDirectory(
        string sourceFolder,
        string targetFolder)
    {
        Directory.CreateDirectory(targetFolder);

        foreach (string directory in Directory.EnumerateDirectories(
                     sourceFolder,
                     "*",
                     SearchOption.AllDirectories))
        {
            string relativePath =
                Path.GetRelativePath(sourceFolder, directory);

            string targetDirectory =
                Path.Combine(targetFolder, relativePath);

            Directory.CreateDirectory(targetDirectory);
        }

        foreach (string file in Directory.EnumerateFiles(
                     sourceFolder,
                     "*",
                     SearchOption.AllDirectories))
        {
            string relativePath =
                Path.GetRelativePath(sourceFolder, file);

            string targetFile =
                Path.Combine(targetFolder, relativePath);

            string? targetDirectory =
                Path.GetDirectoryName(targetFile);

            if (!string.IsNullOrWhiteSpace(targetDirectory))
            {
                Directory.CreateDirectory(targetDirectory);
            }

            File.Copy(file, targetFile, true);
        }
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
        File.WriteAllText(
            Path.Combine(folder, "steam_appid.txt"),
            "782330");
    }

    private async Task RunDepotDownloaderAsync(
        string depotDownloaderPath,
        int depotId,
        string manifestId,
        string? fileListPath,
        bool useFileList,
        string username,
        string password,
        string outputFolder,
        CancellationToken cancellationToken)
    {
        ProcessStartInfo startInfo = new()
        {
            FileName = depotDownloaderPath,
            WorkingDirectory = Path.GetDirectoryName(depotDownloaderPath) ?? GetAppWorkingFolder(),
            UseShellExecute = false,
            CreateNoWindow = false
        };

        startInfo.ArgumentList.Add("-app");
        startInfo.ArgumentList.Add("782330");
        startInfo.ArgumentList.Add("-depot");
        startInfo.ArgumentList.Add(depotId.ToString());
        startInfo.ArgumentList.Add("-manifest");
        startInfo.ArgumentList.Add(manifestId);
        startInfo.ArgumentList.Add("-username");
        startInfo.ArgumentList.Add(username);
        startInfo.ArgumentList.Add("-password");
        startInfo.ArgumentList.Add(password);
        startInfo.ArgumentList.Add("-remember-password");

        if (useFileList)
        {
            if (string.IsNullOrWhiteSpace(fileListPath))
            {
                throw new InvalidOperationException("Filelist path is empty.");
            }

            startInfo.ArgumentList.Add("-filelist");
            startInfo.ArgumentList.Add(fileListPath);
        }

        startInfo.ArgumentList.Add("-dir");
        startInfo.ArgumentList.Add(outputFolder);

        using Process process = new()
        {
            StartInfo = startInfo
        };

        process.Start();

        using CancellationTokenRegistration registration = cancellationToken.Register(
            () =>
            {
                try
                {
                    if (!process.HasExited)
                    {
                        process.Kill(true);
                    }
                }
                catch
                {
                }
            });

        await process.WaitForExitAsync(cancellationToken);

        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException(
                LF("DepotFailed", depotId, process.ExitCode));
        }
    }

    private int ApplyDownpatchFiles(
        string sourceFolder,
        string targetFolder,
        string backupFolder)
    {
        int copiedFiles = 0;

        foreach (string directory in Directory.EnumerateDirectories(
                     sourceFolder,
                     "*",
                     SearchOption.AllDirectories))
        {
            string relativePath = Path.GetRelativePath(sourceFolder, directory);
            string targetDirectory = Path.Combine(targetFolder, relativePath);

            Directory.CreateDirectory(targetDirectory);
        }

        foreach (string sourceFile in Directory.EnumerateFiles(
                     sourceFolder,
                     "*",
                     SearchOption.AllDirectories))
        {
            string relativePath = Path.GetRelativePath(sourceFolder, sourceFile);
            string targetFile = Path.Combine(targetFolder, relativePath);
            string? targetDirectory = Path.GetDirectoryName(targetFile);

            if (!string.IsNullOrWhiteSpace(targetDirectory))
            {
                Directory.CreateDirectory(targetDirectory);
            }

            if (File.Exists(targetFile))
            {
                string backupFile = Path.Combine(backupFolder, relativePath);
                string? backupDirectory = Path.GetDirectoryName(backupFile);

                if (!string.IsNullOrWhiteSpace(backupDirectory))
                {
                    Directory.CreateDirectory(backupDirectory);
                }

                File.Copy(targetFile, backupFile, true);
            }

            File.Copy(sourceFile, targetFile, true);

            copiedFiles++;

            if (copiedFiles % 100 == 0)
            {
                WriteLogThreadSafe($"Copied files: {copiedFiles}");
            }
        }

        return copiedFiles;
    }

    private static bool IsValidDoomRootFolder(string folder)
    {
        return Directory.Exists(folder) &&
               File.Exists(Path.Combine(folder, "DOOMEternalx64vk.exe"));
    }

    private static bool AreSameDirectory(string first, string second)
    {
        string firstFull = NormalizeDirectoryPath(first);
        string secondFull = NormalizeDirectoryPath(second);

        return string.Equals(
            firstFull,
            secondFull,
            StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsSubdirectoryOf(string possibleChild, string possibleParent)
    {
        string child = NormalizeDirectoryPath(possibleChild);
        string parent = NormalizeDirectoryPath(possibleParent);

        return child.StartsWith(
            parent,
            StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizeDirectoryPath(string path)
    {
        return Path.GetFullPath(path)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            + Path.DirectorySeparatorChar;
    }

    private void ShowError(string message)
    {
        WriteLog($"ERROR: {message}");

        MessageBox.Show(
            this,
            message,
            "EternalDownpatcher",
            MessageBoxButtons.OK,
            MessageBoxIcon.Error);
    }

    private string L(string key)
    {
        string language = string.IsNullOrWhiteSpace(_userSettings.Language)
            ? "en"
            : _userSettings.Language;

        return T(language, key);
    }

    private string LF(string key, params object[] args)
    {
        return string.Format(L(key), args);
    }

    private void ShowErrorKey(string key, params object[] args)
    {
        string message = LF(key, args);

        WriteLog($"ERROR: {message}");

        MessageBox.Show(
            this,
            message,
            "EternalDownpatcher",
            MessageBoxButtons.OK,
            MessageBoxIcon.Error);
    }

    private void SetRunningState(bool isRunning)
    {
        _isRunning = isRunning;

        _startButton.Enabled = !isRunning;
        _cancelButton.Enabled = isRunning;

        _selectGameFolderButton.Enabled =
            !isRunning && !_downloadAllFilesCheckBox.Checked;

        _selectDownpatchFolderButton.Enabled = !isRunning;
        _versionComboBox.Enabled = !isRunning;
        _steamUsernameTextBox.Enabled = !isRunning;
        _steamPasswordTextBox.Enabled = !isRunning;

        _downloadAllFilesCheckBox.Enabled = !isRunning;

        _validateFilesCheckBox.Enabled =
            !isRunning && !_downloadAllFilesCheckBox.Checked;

        _restoreBackupButton.Enabled = !isRunning;
        _openOutputFolderButton.Enabled = !isRunning;
        _themeToggleButton.Enabled = !isRunning;
        _settingsButton.Enabled = !isRunning;
    }

    private readonly record struct ThemePalette(
        Color FormBack,
        Color TextColor,
        Color MutedTextColor,
        Color InputBack,
        Color InputFore,
        Color InputBorder,
        Color ButtonBack,
        Color ButtonFore,
        Color ButtonHover,
        Color ButtonDown,
        Color ErrorColor,
        Color SuccessColor,
        Color LogBack,
        Color LogFore);

    private void ConfigureThemeTransition()
    {
        _themeTransitionTimer.Interval = 15;
        _themeTransitionTimer.Tick += ThemeTransitionTimer_Tick;
    }

    private void StartThemeTransition(bool dark)
    {
        _themeTransitionTimer.Stop();

        _targetDarkTheme = dark;
        _themeTransitionFrame = 0;

        _themeTransitionFrom = CaptureCurrentThemePalette();
        _themeTransitionTo = GetThemePalette(dark);

        UpdateThemeToggleIcon(dark);

        _themeTransitionTimer.Start();
    }

    private void ThemeTransitionTimer_Tick(object? sender, EventArgs e)
    {
        _themeTransitionFrame++;

        double progress = Math.Min(
            1.0,
            (double)_themeTransitionFrame / ThemeTransitionFrames);

        progress = progress * progress * (3.0 - 2.0 * progress);

        ThemePalette palette = LerpThemePalette(
            _themeTransitionFrom,
            _themeTransitionTo,
            progress);

        ApplyThemePalette(palette, _targetDarkTheme);

        if (_themeTransitionFrame < ThemeTransitionFrames)
        {
            return;
        }

        _themeTransitionTimer.Stop();

        _isDarkTheme = _targetDarkTheme;
        ApplyTheme(_isDarkTheme);
    }

    private ThemePalette CaptureCurrentThemePalette()
    {
        ThemePalette fallback = GetThemePalette(_isDarkTheme);

        return new ThemePalette(
            BackColor,
            ForeColor,
            fallback.MutedTextColor,
            _steamUsernameTextBox.BackColor,
            _steamUsernameTextBox.ForeColor,
            fallback.InputBorder,
            _startButton.BackColor,
            _startButton.ForeColor,
            fallback.ButtonHover,
            fallback.ButtonDown,
            fallback.ErrorColor,
            fallback.SuccessColor,
            _consoleBox.BackColor,
            _consoleBox.ForeColor);
    }

    private static ThemePalette GetThemePalette(bool dark)
    {
        if (dark)
        {
            return new ThemePalette(
                Color.FromArgb(21, 23, 28),
                Color.FromArgb(235, 238, 245),
                Color.FromArgb(170, 176, 188),
                Color.FromArgb(32, 36, 43),
                Color.FromArgb(240, 243, 250),
                Color.FromArgb(74, 82, 97),
                Color.FromArgb(42, 47, 56),
                Color.FromArgb(240, 243, 250),
                Color.FromArgb(52, 59, 70),
                Color.FromArgb(35, 40, 48),
                Color.FromArgb(255, 107, 107),
                Color.FromArgb(123, 216, 143),
                Color.Black,
                Color.FromArgb(235, 235, 235));
        }

        return new ThemePalette(
            Color.FromArgb(245, 247, 250),
            Color.FromArgb(25, 28, 33),
            Color.FromArgb(90, 98, 110),
            Color.White,
            Color.FromArgb(25, 28, 33),
            Color.FromArgb(200, 207, 218),
            Color.FromArgb(238, 241, 246),
            Color.FromArgb(25, 28, 33),
            Color.FromArgb(226, 231, 238),
            Color.FromArgb(215, 221, 230),
            Color.FromArgb(210, 45, 45),
            Color.FromArgb(32, 145, 70),
            Color.Black,
            Color.FromArgb(235, 235, 235));
    }

    private static ThemePalette LerpThemePalette(
        ThemePalette from,
        ThemePalette to,
        double amount)
    {
        return new ThemePalette(
            LerpColor(from.FormBack, to.FormBack, amount),
            LerpColor(from.TextColor, to.TextColor, amount),
            LerpColor(from.MutedTextColor, to.MutedTextColor, amount),
            LerpColor(from.InputBack, to.InputBack, amount),
            LerpColor(from.InputFore, to.InputFore, amount),
            LerpColor(from.InputBorder, to.InputBorder, amount),
            LerpColor(from.ButtonBack, to.ButtonBack, amount),
            LerpColor(from.ButtonFore, to.ButtonFore, amount),
            LerpColor(from.ButtonHover, to.ButtonHover, amount),
            LerpColor(from.ButtonDown, to.ButtonDown, amount),
            LerpColor(from.ErrorColor, to.ErrorColor, amount),
            LerpColor(from.SuccessColor, to.SuccessColor, amount),
            LerpColor(from.LogBack, to.LogBack, amount),
            LerpColor(from.LogFore, to.LogFore, amount));
    }

    private static Color LerpColor(Color from, Color to, double amount)
    {
        int red = LerpByte(from.R, to.R, amount);
        int green = LerpByte(from.G, to.G, amount);
        int blue = LerpByte(from.B, to.B, amount);

        return Color.FromArgb(red, green, blue);
    }

    private static int LerpByte(int from, int to, double amount)
    {
        return (int)Math.Round(from + ((to - from) * amount));
    }

    private void ApplyTheme(bool dark)
    {
        _isDarkTheme = dark;
        ApplyThemePalette(GetThemePalette(dark), dark);
        UpdateThemeToggleIcon(dark);
    }

    private void ApplyThemePalette(ThemePalette palette, bool dark)
    {
        SuspendLayout();

        BackColor = palette.FormBack;
        ForeColor = palette.TextColor;

        foreach (Control control in Controls)
        {
            ApplyThemeToControl(control, palette, dark);
        }

        _consoleBox.BackColor = palette.LogBack;
        _consoleBox.ForeColor = palette.LogFore;

        ApplyStatusLabelColors(palette);

        ResumeLayout();
        Invalidate(true);
    }

    private void ApplyThemeToControl(
        Control control,
        ThemePalette palette,
        bool dark)
    {
        if (control is RichTextBox richTextBox)
        {
            richTextBox.BackColor = palette.LogBack;
            richTextBox.ForeColor = palette.LogFore;
            richTextBox.BorderStyle = BorderStyle.FixedSingle;
        }
        else if (control is TextBox textBox)
        {
            textBox.BackColor = palette.InputBack;
            textBox.ForeColor = palette.InputFore;
            textBox.BorderStyle = BorderStyle.FixedSingle;
        }
        else if (control is ComboBox comboBox)
        {
            comboBox.BackColor = palette.InputBack;
            comboBox.ForeColor = palette.InputFore;
            comboBox.FlatStyle = FlatStyle.Flat;
        }
        else if (control is Button button)
        {
            button.UseVisualStyleBackColor = false;
            button.BackColor = palette.ButtonBack;
            button.ForeColor = palette.ButtonFore;
            button.FlatStyle = FlatStyle.Flat;
            button.FlatAppearance.BorderSize = 1;
            button.FlatAppearance.BorderColor = palette.InputBorder;
            button.FlatAppearance.MouseOverBackColor = palette.ButtonHover;
            button.FlatAppearance.MouseDownBackColor = palette.ButtonDown;
            button.Cursor = Cursors.Hand;
        }
        else if (control is CheckBox checkBox)
        {
            checkBox.BackColor = palette.FormBack;
            checkBox.ForeColor = palette.TextColor;
            checkBox.FlatStyle = FlatStyle.Flat;
            checkBox.FlatAppearance.BorderColor = palette.InputBorder;
            checkBox.FlatAppearance.CheckedBackColor = dark
                ? Color.FromArgb(77, 163, 255)
                : Color.FromArgb(0, 120, 215);
            checkBox.FlatAppearance.MouseOverBackColor = palette.ButtonHover;
        }
        else if (control is Label label)
        {
            label.BackColor = palette.FormBack;

            if (label == _gameFolderStatusLabel ||
                label == _requiredFieldsLabel)
            {
                ApplyStatusLabelColors(palette);
            }
            else if (label == _titleLabel)
            {
                label.ForeColor = dark
                    ? Color.FromArgb(245, 247, 255)
                    : Color.FromArgb(20, 24, 31);
            }
            else if (string.IsNullOrWhiteSpace(label.Text))
            {
                label.ForeColor = palette.MutedTextColor;
            }
            else
            {
                label.ForeColor = palette.TextColor;
            }
        }
        else
        {
            control.BackColor = palette.FormBack;
            control.ForeColor = palette.TextColor;
        }

        foreach (Control child in control.Controls)
        {
            ApplyThemeToControl(child, palette, dark);
        }
    }

    private void ApplyStatusLabelColors(ThemePalette palette)
    {
        _gameFolderStatusLabel.ForeColor = IsPositiveStatus(_gameFolderStatusLabel.Text)
            ? palette.SuccessColor
            : palette.ErrorColor;

        _requiredFieldsLabel.ForeColor = IsPositiveStatus(_requiredFieldsLabel.Text)
            ? palette.SuccessColor
            : palette.ErrorColor;
    }

    private static bool IsPositiveStatus(string text)
    {
        string value = text.ToLowerInvariant();

        return value.Contains("ready") ||
               value.Contains("готово") ||
               value.Contains("listo") ||
               value.Contains("root selected") ||
               value.Contains("папка выбрана") ||
               value.Contains("carpeta seleccionada") ||
               value.Contains("download all files mode") ||
               value.Contains("режим download all files") ||
               value.Contains("modo download all files") ||
               value.Contains("installed version") ||
               value.Contains("установленная версия") ||
               value.Contains("versión instalada");
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

    private void DownloadAllFilesCheckBox_CheckedChanged(object? sender, EventArgs e)
    {
        if (_downloadAllFilesCheckBox.Checked)
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

    private void WriteLog(string message)
    {
        string timestamp = DateTime.Now.ToString("HH:mm:ss");

        _consoleBox.AppendText($"[{timestamp}] > {message}{Environment.NewLine}");
        _consoleBox.SelectionStart = _consoleBox.TextLength;
        _consoleBox.ScrollToCaret();
    }

    private void ThemeToggleButton_Click(object? sender, EventArgs e)
    {
        bool newDarkMode = !_isDarkTheme;

        _userSettings.DarkMode = newDarkMode;
        UserSettingsStore.Save(_userSettings);

        StartThemeTransition(newDarkMode);
    }

    private void UpdateThemeToggleIcon(bool dark)
    {
        _themeToggleButton.Text = dark ? "☀" : "🌙";
    }

    private void SettingsButton_Click(object? sender, EventArgs e)
    {
        using SettingsForm settingsForm = new(_userSettings);

        if (settingsForm.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        _userSettings.Language = settingsForm.SelectedLanguage;
        UserSettingsStore.Save(_userSettings);

        ApplyLanguage();
    }

    private void ApplyLanguage()
    {
        string language = string.IsNullOrWhiteSpace(_userSettings.Language)
            ? "en"
            : _userSettings.Language;

        _selectGameFolderButton.Text = T(language, "SelectGameFolder");
        _helpButton.Text = "Help";
        _versionLabel.Text = T(language, "GameVersion");
        _usernameLabel.Text = T(language, "SteamUsername");
        _passwordLabel.Text = T(language, "SteamPassword");
        _downloadAllFilesCheckBox.Text = T(language, "DownloadAllFiles");
        _validateFilesCheckBox.Text = T(language, "ValidateFiles");
        _consoleLabel.Text = T(language, "OutputLog");
        _copyLogButton.Text = T(language, "CopyLog");
        _selectDownpatchFolderButton.Text = T(language, "Browse");
        _restoreBackupButton.Text = T(language, "RestoreBackup");
        _openOutputFolderButton.Text = T(language, "OpenFolder");

        _startButton.Text = _downloadAllFilesCheckBox.Checked
            ? T(language, "Download")
            : T(language, "Downpatch");

        _folderLabel.Text = _downloadAllFilesCheckBox.Checked
            ? T(language, "DownloadFolder")
            : T(language, "PatchFilesFolder");

        if (_downloadAllFilesCheckBox.Checked)
        {
            _gameFolderStatusLabel.Text = T(language, "DownloadAllFilesStatus");
        }
        else if (_doomRootFolder is null)
        {
            _gameFolderStatusLabel.Text = T(language, "RootNotSelected");
        }
        else if (_installedVersion is not null)
        {
            _gameFolderStatusLabel.Text = string.Format(T(language, "RootSelected"), _installedVersion.Name);
        }

        if (string.IsNullOrWhiteSpace(_requiredFieldsLabel.Text) ||
            _requiredFieldsLabel.Text.Contains("Required", StringComparison.OrdinalIgnoreCase) ||
            _requiredFieldsLabel.Text.Contains("обяз", StringComparison.OrdinalIgnoreCase) ||
            _requiredFieldsLabel.Text.Contains("oblig", StringComparison.OrdinalIgnoreCase))
        {
            _requiredFieldsLabel.Text = T(language, "RequiredFieldsMissing");
        }

        ApplyStatusLabelColors(GetThemePalette(_isDarkTheme));
    }

    private static string T(string language, string key)
    {
        Dictionary<string, string> en = new()
        {
            ["SelectGameFolder"] = "Select DOOM Eternal Folder",
            ["GameVersion"] = "Game Version *",
            ["SteamUsername"] = "Steam Username *",
            ["SteamPassword"] = "Steam Password *",
            ["DownloadAllFiles"] = "Download All Files",
            ["ValidateFiles"] = "Validate files",
            ["OutputLog"] = "Output Log",
            ["CopyLog"] = "Copy Log",
            ["Browse"] = "Browse",
            ["RestoreBackup"] = "Restore Backup",
            ["OpenFolder"] = "Open Folder",
            ["Download"] = "Download",
            ["Downpatch"] = "Downpatch",
            ["DownloadFolder"] = "Download Folder",
            ["PatchFilesFolder"] = "Patch Files Folder",
            ["RequiredFieldsMissing"] = "* Required fields missing",
            ["RootNotSelected"] = "DOOM Eternal root folder has not been selected.",
            ["DownloadAllFilesStatus"] = "Download All Files mode: DOOM Eternal root folder is not needed.",
            ["RootSelected"] = "Root selected. Installed version: {0}",
            ["Ready"] = "Ready",
            ["InvalidDoomRootFolderShort"] = "Invalid DOOM Eternal root folder.",
            ["RootSelectedButVersionNotDetected"] = "Root selected, but version was not detected.",
            ["OutputFolderDoesNotExist"] = "Output folder does not exist.",
            ["InvalidDoomRootFolder"] = "Selected DOOM Eternal root folder is invalid. It must contain DOOMEternalx64vk.exe.",
            ["SelectedTargetFolderInvalid"] = "Selected target folder is not a valid DOOM Eternal root folder.",
            ["BackupFolderInvalid"] = "Selected backup folder is empty or invalid.",
            ["RestoreBackupFailed"] = "Failed to restore backup: {0}",
            ["VersionsJsonNotFound"] = "data\\versions.json was not found.",
            ["LoadVersionsFailed"] = "Failed to load versions.json: {0}",
            ["SelectedVersionInvalid"] = "Selected version is invalid.",
            ["InvalidManifestData"] = "Selected version has invalid manifest data.",
            ["InstalledVersionNotDetected"] = "Installed DOOM Eternal version was not detected.",
            ["UnableCompareVersions"] = "Unable to compare selected and installed versions.",
            ["TargetMustBeOlder"] = "Selected downpatch version must be older than the installed version.",
            ["GenerateFilelistFailed"] = "Failed to generate filelist: {0}",
            ["PatchFolderSameAsGame"] = "Patch files folder and DOOM Eternal folder cannot be the same folder.",
            ["DoomFolderInsidePatchFolder"] = "DOOM Eternal folder cannot be inside the patch files folder.",
            ["DownloadFolderIsDoomRoot"] = "Download folder cannot be an existing DOOM Eternal root folder. Choose a separate empty folder.",
            ["DownloadFolderSameAsRoot"] = "Download folder cannot be the selected DOOM Eternal root folder.",
            ["DepotDownloaderPrepareFailed"] = "Failed to prepare DepotDownloader: {0}",
            ["RequiredFieldsLog"] = "Complete all required fields before starting.",
            ["MissingGameVersion"] = "game version",
            ["MissingSteamLogin"] = "Steam login",
            ["MissingSteamPassword"] = "Steam password",
            ["MissingDoomRoot"] = "DOOM Eternal root folder",
            ["MissingDownloadFolder"] = "download folder",
            ["MissingPatchFolder"] = "patch files folder",
            ["MissingPrefix"] = "* Missing: {0}",
            ["InvalidFolderNoExe"] = "Selected folder does not contain DOOMEternalx64vk.exe.",
            ["DetectVersionFailed"] = "Unable to detect installed DOOM Eternal version from DOOMEternalx64vk.exe size.",
            ["DepotFailed"] = "DepotDownloader failed on depot {0}. Exit code: {1}",
            ["OutputFolderEmptyAfterDownload"] = "DepotDownloader finished, but output folder is empty.",
            ["DepotDownloaderNotFoundQuestion"] = "DepotDownloader.exe was not found.\n\nDownload the latest version automatically?",
            ["DownloadDepotDownloaderTitle"] = "Download DepotDownloader",
            ["WelcomeLog"] = "Welcome to EternalDownpatcher.",
            ["SelectVersionLog"] = "Select a version manually.",
            ["SelectRootFolderLog"] = "Select DOOM Eternal root folder.",
            ["WorkingFolderLog"] = "Working folder: {0}",
            ["DefaultPatchFolderLog"] = "Default patch files folder: {0}",
            ["ClickHelpLog"] = "Click Help button for instructions!",
            ["LoadedVersionsLog"] = "Loaded versions: {0}",
            ["VersionsSourceLog"] = "Versions source: {0}",
            ["VersionLog"] = "EternalDownpatcher version: {0}",
            ["DownloadFolderDoesNotExistCreate"] = "Download folder does not exist.\n\nCreate it now?",
            ["PatchFolderDoesNotExistCreate"] = "Patch files folder does not exist.\n\nCreate it now?",
            ["CreateDownloadFolder"] = "Create download folder",
            ["CreatePatchFolder"] = "Create patch files folder"
        };

        Dictionary<string, string> ru = new()
        {
            ["SelectGameFolder"] = "Выбрать папку DOOM Eternal",
            ["GameVersion"] = "Версия игры *",
            ["SteamUsername"] = "Логин Steam *",
            ["SteamPassword"] = "Пароль Steam *",
            ["DownloadAllFiles"] = "Скачать все файлы",
            ["ValidateFiles"] = "Проверять файлы",
            ["OutputLog"] = "Лог",
            ["CopyLog"] = "Копировать лог",
            ["Browse"] = "Обзор",
            ["RestoreBackup"] = "Восстановить бэкап",
            ["OpenFolder"] = "Открыть папку",
            ["Download"] = "Скачать",
            ["Downpatch"] = "Даунпатч",
            ["DownloadFolder"] = "Папка загрузки",
            ["PatchFilesFolder"] = "Папка патч-файлов",
            ["RequiredFieldsMissing"] = "* Не заполнены обязательные поля",
            ["RootNotSelected"] = "Папка DOOM Eternal не выбрана.",
            ["DownloadAllFilesStatus"] = "Режим Download All Files: папка DOOM Eternal не нужна.",
            ["RootSelected"] = "Папка выбрана. Установленная версия: {0}",
            ["Ready"] = "Готово",
            ["InvalidDoomRootFolderShort"] = "Неверная папка DOOM Eternal.",
            ["RootSelectedButVersionNotDetected"] = "Папка выбрана, но версия не была определена.",
            ["OutputFolderDoesNotExist"] = "Папка вывода не существует.",
            ["InvalidDoomRootFolder"] = "Выбранная папка DOOM Eternal неверная. В ней должен быть DOOMEternalx64vk.exe.",
            ["SelectedTargetFolderInvalid"] = "Выбранная целевая папка не является корректной папкой DOOM Eternal.",
            ["BackupFolderInvalid"] = "Выбранная папка бэкапа пустая или неверная.",
            ["RestoreBackupFailed"] = "Не удалось восстановить бэкап: {0}",
            ["VersionsJsonNotFound"] = "Файл data\\versions.json не найден.",
            ["LoadVersionsFailed"] = "Не удалось загрузить versions.json: {0}",
            ["SelectedVersionInvalid"] = "Выбранная версия неверная.",
            ["InvalidManifestData"] = "У выбранной версии неверные manifest-данные.",
            ["InstalledVersionNotDetected"] = "Установленная версия DOOM Eternal не была определена.",
            ["UnableCompareVersions"] = "Не удалось сравнить выбранную и установленную версии.",
            ["TargetMustBeOlder"] = "Выбранная версия для даунпатча должна быть старее установленной версии.",
            ["GenerateFilelistFailed"] = "Не удалось создать filelist: {0}",
            ["PatchFolderSameAsGame"] = "Папка патч-файлов и папка DOOM Eternal не могут быть одной и той же папкой.",
            ["DoomFolderInsidePatchFolder"] = "Папка DOOM Eternal не может находиться внутри папки патч-файлов.",
            ["DownloadFolderIsDoomRoot"] = "Папка загрузки не может быть существующей папкой DOOM Eternal. Выберите отдельную пустую папку.",
            ["DownloadFolderSameAsRoot"] = "Папка загрузки не может совпадать с выбранной папкой DOOM Eternal.",
            ["DepotDownloaderPrepareFailed"] = "Не удалось подготовить DepotDownloader: {0}",
            ["RequiredFieldsLog"] = "Заполните все обязательные поля перед запуском.",
            ["MissingGameVersion"] = "версия игры",
            ["MissingSteamLogin"] = "логин Steam",
            ["MissingSteamPassword"] = "пароль Steam",
            ["MissingDoomRoot"] = "папка DOOM Eternal",
            ["MissingDownloadFolder"] = "папка загрузки",
            ["MissingPatchFolder"] = "папка патч-файлов",
            ["MissingPrefix"] = "* Не заполнено: {0}",
            ["InvalidFolderNoExe"] = "В выбранной папке нет DOOMEternalx64vk.exe.",
            ["DetectVersionFailed"] = "Не удалось определить установленную версию DOOM Eternal по размеру DOOMEternalx64vk.exe.",
            ["DepotFailed"] = "DepotDownloader завершился с ошибкой на depot {0}. Код выхода: {1}",
            ["OutputFolderEmptyAfterDownload"] = "DepotDownloader завершился, но папка вывода пустая.",
            ["DownloadFolderDoesNotExistCreate"] = "Папка загрузки не существует.\n\nСоздать её сейчас?",
            ["PatchFolderDoesNotExistCreate"] = "Папка патч-файлов не существует.\n\nСоздать её сейчас?",
            ["CreateDownloadFolder"] = "Создать папку загрузки",
            ["CreatePatchFolder"] = "Создать папку патч-файлов",
            ["DepotDownloaderNotFoundQuestion"] = "DepotDownloader.exe не найден.\n\nСкачать последнюю версию автоматически?",
            ["DownloadDepotDownloaderTitle"] = "Скачать DepotDownloader",
            ["WelcomeLog"] = "Добро пожаловать в EternalDownpatcher.",
            ["SelectVersionLog"] = "Выберите версию вручную.",
            ["SelectRootFolderLog"] = "Выберите папку DOOM Eternal.",
            ["WorkingFolderLog"] = "Рабочая папка: {0}",
            ["DefaultPatchFolderLog"] = "Папка патч-файлов по умолчанию: {0}",
            ["ClickHelpLog"] = "Нажмите Help для инструкции.",
            ["LoadedVersionsLog"] = "Загружено версий: {0}",
            ["VersionsSourceLog"] = "Источник версий: {0}",
            ["VersionLog"] = "Версия EternalDownpatcher: {0}"
        };

        Dictionary<string, string> es = new()
        {
            ["SelectGameFolder"] = "Seleccionar carpeta de DOOM Eternal",
            ["GameVersion"] = "Versión del juego *",
            ["SteamUsername"] = "Usuario de Steam *",
            ["SteamPassword"] = "Contraseña de Steam *",
            ["DownloadAllFiles"] = "Descargar todos los archivos",
            ["ValidateFiles"] = "Validar archivos",
            ["OutputLog"] = "Registro",
            ["CopyLog"] = "Copiar registro",
            ["Browse"] = "Examinar",
            ["RestoreBackup"] = "Restaurar copia",
            ["OpenFolder"] = "Abrir carpeta",
            ["Download"] = "Descargar",
            ["Downpatch"] = "Aplicar downgrade",
            ["DownloadFolder"] = "Carpeta de descarga",
            ["PatchFilesFolder"] = "Carpeta de archivos",
            ["RequiredFieldsMissing"] = "* Faltan campos obligatorios",
            ["RootNotSelected"] = "No se ha seleccionado la carpeta raíz de DOOM Eternal.",
            ["DownloadAllFilesStatus"] = "Modo Download All Files: no se necesita la carpeta raíz de DOOM Eternal.",
            ["RootSelected"] = "Carpeta seleccionada. Versión instalada: {0}",
            ["Ready"] = "Listo",
            ["InvalidDoomRootFolderShort"] = "Carpeta de DOOM Eternal no válida.",
            ["RootSelectedButVersionNotDetected"] = "Carpeta seleccionada, pero no se detectó la versión.",
            ["OutputFolderDoesNotExist"] = "La carpeta de salida no existe.",
            ["InvalidDoomRootFolder"] = "La carpeta seleccionada de DOOM Eternal no es válida. Debe contener DOOMEternalx64vk.exe.",
            ["SelectedTargetFolderInvalid"] = "La carpeta de destino seleccionada no es una carpeta válida de DOOM Eternal.",
            ["BackupFolderInvalid"] = "La carpeta de copia de seguridad seleccionada está vacía o no es válida.",
            ["RestoreBackupFailed"] = "No se pudo restaurar la copia de seguridad: {0}",
            ["VersionsJsonNotFound"] = "No se encontró data\\versions.json.",
            ["LoadVersionsFailed"] = "No se pudo cargar versions.json: {0}",
            ["SelectedVersionInvalid"] = "La versión seleccionada no es válida.",
            ["InvalidManifestData"] = "La versión seleccionada tiene datos de manifest no válidos.",
            ["InstalledVersionNotDetected"] = "No se detectó la versión instalada de DOOM Eternal.",
            ["UnableCompareVersions"] = "No se pudo comparar la versión seleccionada con la versión instalada.",
            ["TargetMustBeOlder"] = "La versión seleccionada para el downpatch debe ser más antigua que la versión instalada.",
            ["GenerateFilelistFailed"] = "No se pudo generar el filelist: {0}",
            ["PatchFolderSameAsGame"] = "La carpeta de archivos del parche y la carpeta de DOOM Eternal no pueden ser la misma.",
            ["DoomFolderInsidePatchFolder"] = "La carpeta de DOOM Eternal no puede estar dentro de la carpeta de archivos del parche.",
            ["DownloadFolderIsDoomRoot"] = "La carpeta de descarga no puede ser una carpeta existente de DOOM Eternal. Elige una carpeta separada y vacía.",
            ["DownloadFolderSameAsRoot"] = "La carpeta de descarga no puede ser la carpeta seleccionada de DOOM Eternal.",
            ["DepotDownloaderPrepareFailed"] = "No se pudo preparar DepotDownloader: {0}",
            ["RequiredFieldsLog"] = "Completa todos los campos obligatorios antes de empezar.",
            ["MissingGameVersion"] = "versión del juego",
            ["MissingSteamLogin"] = "usuario de Steam",
            ["MissingSteamPassword"] = "contraseña de Steam",
            ["MissingDoomRoot"] = "carpeta de DOOM Eternal",
            ["MissingDownloadFolder"] = "carpeta de descarga",
            ["MissingPatchFolder"] = "carpeta de archivos del parche",
            ["MissingPrefix"] = "* Faltan: {0}",
            ["InvalidFolderNoExe"] = "La carpeta seleccionada no contiene DOOMEternalx64vk.exe.",
            ["DetectVersionFailed"] = "No se pudo detectar la versión instalada de DOOM Eternal por el tamaño de DOOMEternalx64vk.exe.",
            ["DepotFailed"] = "DepotDownloader falló en el depot {0}. Código de salida: {1}",
            ["OutputFolderEmptyAfterDownload"] = "DepotDownloader terminó, pero la carpeta de salida está vacía.",
            ["DownloadFolderDoesNotExistCreate"] = "La carpeta de descarga no existe.\n\n¿Crearla ahora?",
            ["PatchFolderDoesNotExistCreate"] = "La carpeta de archivos del parche no existe.\n\n¿Crearla ahora?",
            ["CreateDownloadFolder"] = "Crear carpeta de descarga",
            ["CreatePatchFolder"] = "Crear carpeta de archivos del parche",
            ["DepotDownloaderNotFoundQuestion"] = "No se encontró DepotDownloader.exe.\n\n¿Descargar automáticamente la versión más reciente?",
            ["DownloadDepotDownloaderTitle"] = "Descargar DepotDownloader",
            ["WelcomeLog"] = "Bienvenido a EternalDownpatcher.",
            ["SelectVersionLog"] = "Selecciona una versión manualmente.",
            ["SelectRootFolderLog"] = "Selecciona la carpeta de DOOM Eternal.",
            ["WorkingFolderLog"] = "Carpeta de trabajo: {0}",
            ["DefaultPatchFolderLog"] = "Carpeta predeterminada de archivos del parche: {0}",
            ["ClickHelpLog"] = "Haz clic en Help para ver las instrucciones.",
            ["LoadedVersionsLog"] = "Versiones cargadas: {0}",
            ["VersionsSourceLog"] = "Fuente de versiones: {0}",
            ["VersionLog"] = "Versión de EternalDownpatcher: {0}"
        };

        Dictionary<string, string> selected = language.ToLowerInvariant() switch
        {
            "ru" => ru,
            "es" => es,
            _ => en
        };

        if (selected.TryGetValue(key, out string? value))
        {
            return value;
        }

        if (en.TryGetValue(key, out string? fallbackValue))
{
    return fallbackValue;
}

return key;
    }

    private sealed record DownpatchVersion(
        string Name,
        string Size,
        string[] ManifestIds);
}