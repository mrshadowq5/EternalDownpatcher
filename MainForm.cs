using System.Diagnostics;
using System.Drawing;
using System.IO.Compression;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Windows.Forms;

namespace EternalDownpatcher.WinForms;

public sealed class MainForm : Form
{
private readonly UserSettings _userSettings;
private bool _loadingTheme;

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
    private readonly CheckBox _darkModeSwitch = new();
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
        BackColor = SystemColors.Control;

        Icon? appIcon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);

        if (appIcon is not null)
    {
        Icon = appIcon;
    }

BuildInterface();
ConfigureThemeTransition();

        _loadingTheme = true;
        _darkModeSwitch.Checked = _userSettings.DarkMode;
        _loadingTheme = false;

        ApplyTheme(_userSettings.DarkMode);
        LoadVersions();

        WriteLog("Welcome to EternalDownpatcher.");
        WriteLog("Select a version manually.");
        WriteLog("Select DOOM Eternal root folder.");
        WriteLog($"Working folder: {GetAppWorkingFolder()}");
        WriteLog($"Default patch files folder: {GetDefaultPatchFilesFolder()}");
        WriteLog("Click Help button for instructions!");

        ActiveControl = _versionComboBox;
    }

    private void BuildInterface()
    {
        Label titleLabel = new()
        {
            Text = "EternalDownpatcher",
            AutoSize = false,
            Font = new Font("Segoe UI", 11F, FontStyle.Italic)
        };

        titleLabel.SetBounds(12, 8, 260, 22);

        Label subtitleLabel = new()
        {
            Text = "",
            AutoSize = false,
            ForeColor = Color.DimGray
        };

        subtitleLabel.SetBounds(12, 30, 260, 18);

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

        Label versionLabel = CreateRequiredLabel("Game Version");
        versionLabel.SetBounds(12, 92, 140, 20);

        Label usernameLabel = CreateRequiredLabel("Steam Username");
        usernameLabel.SetBounds(160, 92, 160, 20);

        Label passwordLabel = CreateRequiredLabel("Steam Password");
        passwordLabel.SetBounds(330, 92, 160, 20);

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

        Label consoleLabel = new()
        {
            Text = "Output Log",
            Font = new Font(Font, FontStyle.Bold)
        };

        consoleLabel.SetBounds(12, 261, 120, 20);

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

        _darkModeSwitch.SetBounds(610, 421, 100, 24);
        _darkModeSwitch.Text = "Dark mode";
        _darkModeSwitch.AutoSize = true;
        _darkModeSwitch.CheckedChanged += DarkModeSwitch_CheckedChanged;

        _restoreBackupButton.SetBounds(365, 421, 125, 24);
        _restoreBackupButton.Text = "Restore Backup";
        _restoreBackupButton.Click += RestoreBackupButton_Click;

        _openOutputFolderButton.SetBounds(500, 421, 98, 24);
        _openOutputFolderButton.Text = "Open Folder";
        _openOutputFolderButton.Click += OpenOutputFolderButton_Click;

        Controls.Add(titleLabel);
        Controls.Add(subtitleLabel);
        Controls.Add(_gameFolderStatusLabel);
        Controls.Add(_selectGameFolderButton);
        Controls.Add(_helpButton);
        Controls.Add(versionLabel);
        Controls.Add(usernameLabel);
        Controls.Add(passwordLabel);
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
        Controls.Add(consoleLabel);
        Controls.Add(_copyLogButton);
        Controls.Add(_consoleBox);
        Controls.Add(_darkModeSwitch);
        Controls.Add(_restoreBackupButton);
        Controls.Add(_openOutputFolderButton);
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
        ShowError("Output folder does not exist.");
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
            ShowError("Selected target folder is not a valid DOOM Eternal root folder.");
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
        ShowError("Selected backup folder is empty or invalid.");
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
        ShowError($"Failed to restore backup: {exception.Message}");
    }
}

    private static Label CreateRequiredLabel(string text)
    {
        return new Label
        {
            Text = text + " *",
            AutoSize = false,
            Font = new Font("Segoe UI", 9F, FontStyle.Bold)
        };
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
            ShowError("data\\versions.json was not found.");
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

            WriteLog($"Loaded versions: {_versions.Count}");
            WriteLog($"Versions source: {versionsJsonPath}");
        }
        catch (Exception exception)
        {
            ShowError($"Failed to load versions.json: {exception.Message}");
        }
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
                        manifestIds.Add(manifestId);
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
                    name,
                    size,
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
            _versionComboBox.SelectedIndex = -1;
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
        _versionComboBox.SelectedIndex = -1;
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
        throw new InvalidOperationException("Installed version was not detected.");
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
        throw new InvalidOperationException("Target version must be older than installed version.");
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

private static string? FindFileListPath(DownpatchVersion version)
{
    string root = GetAppWorkingFolder();

    string[] dataFolders =
    [
        Path.Combine(root, "data"),
        Path.Combine(AppContext.BaseDirectory, "data")
    ];

    string[] wantedNames =
    [
        version.Name,
        version.Name + ".txt"
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
        missingFields.Add("game version");
    }

    if (string.IsNullOrWhiteSpace(_steamUsernameTextBox.Text))
    {
        missingFields.Add("Steam login");
    }

    if (string.IsNullOrWhiteSpace(_steamPasswordTextBox.Text))
    {
        missingFields.Add("Steam password");
    }

    if (!downloadAllFiles && string.IsNullOrWhiteSpace(_doomRootFolder))
    {
        missingFields.Add("DOOM Eternal root folder");
    }

    if (string.IsNullOrWhiteSpace(_downpatchFolderTextBox.Text))
    {
        missingFields.Add(downloadAllFiles ? "download folder" : "patch files folder");
    }

    if (missingFields.Count > 0)
    {
        _requiredFieldsLabel.Text = "* Missing: " + string.Join(", ", missingFields);
        _requiredFieldsLabel.ForeColor = Color.Red;
        WriteLog("ERROR: Complete all required fields before starting.");
        return;
    }

    DownpatchVersion? selectedVersion = GetSelectedVersion();

    if (selectedVersion is null)
    {
        ShowError("Selected version is invalid.");
        return;
    }

    if (selectedVersion.ManifestIds.Length != DepotIds.Length)
    {
        ShowError("Selected version has invalid manifest data.");
        return;
    }

    string? fileListPath = null;

    if (!downloadAllFiles)
    {
        if (_installedVersion is null)
        {
            ShowError("Installed DOOM Eternal version was not detected.");
            return;
        }

        int targetIndex = GetVersionIndex(selectedVersion.Name);
        int installedIndex = GetVersionIndex(_installedVersion.Name);

        if (targetIndex < 0 || installedIndex < 0)
        {
            ShowError("Unable to compare selected and installed versions.");
            return;
        }

        if (targetIndex >= installedIndex)
        {
            ShowError("Selected downpatch version must be older than the installed version.");
            return;
        }

        try
        {
            fileListPath = BuildGeneratedFileListPath(selectedVersion);
        }
        catch (Exception exception)
        {
            ShowError($"Failed to generate filelist: {exception.Message}");
            return;
        }
    }

    string? existingDepotDownloaderPath = FindDepotDownloaderPath();

    if (existingDepotDownloaderPath is null)
    {
        DialogResult downloadDepotResult = MessageBox.Show(
            this,
            "DepotDownloader.exe was not found.\n\nDownload the latest version automatically?",
            "Download DepotDownloader",
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
        ShowError($"Failed to prepare DepotDownloader: {exception.Message}");
        return;
    }

    string outputFolder = Path.GetFullPath(_downpatchFolderTextBox.Text.Trim());
    string? doomRootFolder = null;

    if (!downloadAllFiles)
    {
        doomRootFolder = Path.GetFullPath(_doomRootFolder!);

        if (!IsValidDoomRootFolder(doomRootFolder))
        {
            ShowError("Selected DOOM Eternal root folder is invalid. It must contain DOOMEternalx64vk.exe.");
            return;
        }

        if (AreSameDirectory(outputFolder, doomRootFolder))
        {
            ShowError("Patch files folder and DOOM Eternal folder cannot be the same folder.");
            return;
        }

        if (IsSubdirectoryOf(doomRootFolder, outputFolder))
        {
            ShowError("DOOM Eternal folder cannot be inside the patch files folder.");
            return;
        }
    }
    else
    {
        if (IsValidDoomRootFolder(outputFolder))
        {
            ShowError("Download folder cannot be an existing DOOM Eternal root folder. Choose a separate empty folder.");
            return;
        }

        if (!string.IsNullOrWhiteSpace(_doomRootFolder) &&
            AreSameDirectory(outputFolder, _doomRootFolder))
        {
            ShowError("Download folder cannot be the selected DOOM Eternal root folder.");
            return;
        }
    }

    if (!Directory.Exists(outputFolder))
    {
        DialogResult createFolderResult = MessageBox.Show(
            this,
            downloadAllFiles
                ? "Download folder does not exist.\n\nCreate it now?"
                : "Patch files folder does not exist.\n\nCreate it now?",
            downloadAllFiles ? "Create download folder" : "Create patch files folder",
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

    _requiredFieldsLabel.Text = "Ready";
    _requiredFieldsLabel.ForeColor = Color.DarkGreen;

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
            throw new InvalidOperationException("DepotDownloader finished, but output folder is empty.");
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

        _gameFolderStatusLabel.Text = "Invalid DOOM Eternal root folder.";
        _gameFolderStatusLabel.ForeColor = Color.Red;

        RefreshDownpatchVersions();

        WriteLog("ERROR: Selected folder does not contain DOOMEternalx64vk.exe.");

        MessageBox.Show(
            this,
            "Selected folder does not contain DOOMEternalx64vk.exe.",
            "EternalDownpatcher",
            MessageBoxButtons.OK,
            MessageBoxIcon.Error);

        return;
    }

    _doomRootFolder = selectedPath;
    _installedVersion = DetectInstalledVersion(selectedPath);

    if (_installedVersion is null)
    {
        _gameFolderStatusLabel.Text = "Root selected, but version was not detected.";
        _gameFolderStatusLabel.ForeColor = Color.Red;

        RefreshDownpatchVersions();

        WriteLog($"DOOM Eternal folder: {selectedPath}");
        WriteLog("ERROR: Unable to detect installed DOOM Eternal version from executable size.");

        MessageBox.Show(
            this,
            "Unable to detect installed DOOM Eternal version from DOOMEternalx64vk.exe size.",
            "EternalDownpatcher",
            MessageBoxButtons.OK,
            MessageBoxIcon.Error);

        return;
    }

    _gameFolderStatusLabel.Text =
        $"Root selected. Installed version: {_installedVersion.Name}";

    _gameFolderStatusLabel.ForeColor = Color.DarkGreen;

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
    using HelpForm helpForm = new(_darkModeSwitch.Checked);
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

private static void EnsureSteamAppIdFile(string folder) //оно воняет    
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
            $"DepotDownloader failed on depot {depotId}. Exit code: {process.ExitCode}");
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
    _darkModeSwitch.Enabled = !isRunning;
}
private readonly record struct ThemePalette(
    Color FormBack,
    Color TextColor,
    Color InputBack,
    Color InputFore,
    Color ButtonBack,
    Color ButtonFore);

private void ConfigureThemeTransition()
{
    _themeTransitionTimer.Interval = 15;
    _themeTransitionTimer.Tick += ThemeTransitionTimer_Tick;
}

private void DarkModeSwitch_CheckedChanged(object? sender, EventArgs e)
{
    if (_loadingTheme)
    {
        return;
    }

    _userSettings.DarkMode = _darkModeSwitch.Checked;
    UserSettingsStore.Save(_userSettings);

    StartThemeTransition(_darkModeSwitch.Checked);
}

private void StartThemeTransition(bool dark)
{
    _themeTransitionTimer.Stop();

    _targetDarkTheme = dark;
    _themeTransitionFrame = 0;

    _themeTransitionFrom = CaptureCurrentThemePalette();
    _themeTransitionTo = GetThemePalette(dark);

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
    return new ThemePalette(
        BackColor,
        _darkModeSwitch.ForeColor,
        _steamUsernameTextBox.BackColor,
        _steamUsernameTextBox.ForeColor,
        _startButton.BackColor,
        _startButton.ForeColor);
}

private static ThemePalette GetThemePalette(bool dark)
{
    if (dark)
    {
        return new ThemePalette(
            Color.FromArgb(32, 32, 32),
            Color.Gainsboro,
            Color.FromArgb(45, 45, 48),
            Color.White,
            Color.FromArgb(55, 55, 58),
            Color.White);
    }

    return new ThemePalette(
        SystemColors.Control,
        SystemColors.ControlText,
        SystemColors.Window,
        SystemColors.WindowText,
        SystemColors.Control,
        SystemColors.ControlText);
}

private static ThemePalette LerpThemePalette(
    ThemePalette from,
    ThemePalette to,
    double amount)
{
    return new ThemePalette(
        LerpColor(from.FormBack, to.FormBack, amount),
        LerpColor(from.TextColor, to.TextColor, amount),
        LerpColor(from.InputBack, to.InputBack, amount),
        LerpColor(from.InputFore, to.InputFore, amount),
        LerpColor(from.ButtonBack, to.ButtonBack, amount),
        LerpColor(from.ButtonFore, to.ButtonFore, amount));
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
}

private void ApplyThemePalette(ThemePalette palette, bool dark)
{
    BackColor = palette.FormBack;
    ForeColor = palette.TextColor;

    foreach (Control control in Controls)
    {
        ApplyThemeToControl(control, palette, dark);
    }

    _consoleBox.BackColor = Color.Black;
    _consoleBox.ForeColor = Color.White;
}

private void ApplyThemeToControl(
    Control control,
    ThemePalette palette,
    bool dark)
{
    if (control is RichTextBox richTextBox)
    {
        richTextBox.BackColor = Color.Black;
        richTextBox.ForeColor = Color.White;
    }
    else if (control is TextBox textBox)
    {
        textBox.BackColor = palette.InputBack;
        textBox.ForeColor = palette.InputFore;
    }
    else if (control is ComboBox comboBox)
    {
        comboBox.BackColor = palette.InputBack;
        comboBox.ForeColor = palette.InputFore;
    }
    else if (control is Button button)
    {
        button.UseVisualStyleBackColor = false;
        button.BackColor = palette.ButtonBack;
        button.ForeColor = palette.ButtonFore;
        button.FlatStyle = FlatStyle.Flat;
        button.FlatAppearance.BorderSize = 1;
        button.FlatAppearance.BorderColor = dark
            ? Color.FromArgb(90, 90, 90)
            : Color.FromArgb(160, 160, 160);
    }
    else if (control is CheckBox checkBox)
    {
        checkBox.BackColor = palette.FormBack;
        checkBox.ForeColor = palette.TextColor;
    }
    else if (control is Label label)
    {
        label.BackColor = palette.FormBack;

        if (label != _gameFolderStatusLabel &&
            label != _requiredFieldsLabel)
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
        _startButton.Text = "Download";
        _folderLabel.Text = "Download Folder";

        _validateFilesCheckBox.Checked = false;
        _validateFilesCheckBox.Enabled = false;

        _selectGameFolderButton.Enabled = false;

        _gameFolderStatusLabel.Text = "Download All Files mode: DOOM Eternal root folder is not needed.";
        _gameFolderStatusLabel.ForeColor = Color.DarkGreen;

        WriteLog("Download All Files mode enabled.");
    }
    else
    {
        _startButton.Text = "Downpatch";
        _folderLabel.Text = "Patch Files Folder";

        _validateFilesCheckBox.Enabled = !_isRunning;
        _selectGameFolderButton.Enabled = !_isRunning;

        if (_doomRootFolder is null)
        {
            _gameFolderStatusLabel.Text = "DOOM Eternal root folder has not been selected.";
            _gameFolderStatusLabel.ForeColor = Color.Red;
        }
        else if (_installedVersion is not null)
        {
            _gameFolderStatusLabel.Text = $"Root selected. Installed version: {_installedVersion.Name}";
            _gameFolderStatusLabel.ForeColor = Color.DarkGreen;
        }

        WriteLog("Patch existing install mode enabled.");
    }

    RefreshDownpatchVersions();
}

    private void WriteLog(string message)
    {
        string timestamp = DateTime.Now.ToString("HH:mm:ss");

        _consoleBox.AppendText($"[{timestamp}] > {message}{Environment.NewLine}");
        _consoleBox.SelectionStart = _consoleBox.TextLength;
        _consoleBox.ScrollToCaret();
    }

    private sealed record DownpatchVersion(
        string Name,
        string Size,
        string[] ManifestIds);
}