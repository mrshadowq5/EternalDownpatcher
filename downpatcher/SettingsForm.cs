using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace EternalDownpatcher.WinForms;

public sealed class SettingsForm : Form
{
    #region Window

    private readonly Panel _contentPanel = new();
    private readonly Button _languageTabButton = new();
    private readonly Button _steamTabButton = new();
    private readonly Button _downpatchTabButton = new();
    private readonly Button _infoTabButton = new();
    private readonly ComboBox _languageComboBox = new();
    private readonly CheckBox _rememberLoginCheckBox = new();
    private readonly CheckBox _rememberPasswordCheckBox = new();
    private readonly CheckBox _createBackupCheckBox = new();
    private readonly Button _okButton = new();
    private readonly Button _cancelButton = new();

    private readonly bool _darkMode;
    private readonly Func<string, string, bool> _confirmPasswordSaving;
    private string _currentPage = "language";

    private Color _back;
    private Color _panel;
    private Color _input;
    private Color _text;
    private Color _button;
    private Color _buttonActive;
    private Color _border;
    private Color _link;

    public string SelectedLanguage { get; private set; }
    public bool RememberLogin { get; private set; }
    public bool RememberPassword { get; private set; }
    public bool CreateBackup { get; private set; }

    public SettingsForm(UserSettings settings) : this(settings, null)
    {
    }

    internal SettingsForm(UserSettings settings, Func<string, string, bool>? confirmPasswordSaving)
    {
        _darkMode = settings.DarkMode;
        RememberLogin = settings.RememberLogin;
        RememberPassword = settings.RememberPassword;
        CreateBackup = settings.CreateBackup;
        _confirmPasswordSaving = confirmPasswordSaving ?? ((message, title) =>
            MessageBox.Show(this, message, title, MessageBoxButtons.YesNo, MessageBoxIcon.Warning,
                MessageBoxDefaultButton.Button2) == DialogResult.Yes);
        SelectedLanguage = string.IsNullOrWhiteSpace(settings.Language)
            ? "en"
            : settings.Language;

        StartPosition = FormStartPosition.CenterParent;
        ClientSize = new Size(460, 360);
        MinimumSize = new Size(460, 360);
        MaximumSize = new Size(460, 360);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        Font = new Font("Segoe UI", 9F);
        AutoScaleMode = AutoScaleMode.Dpi;

        SetColors();
        InitializeSocialIcons();
        BuildInterface();
        ApplyLanguage();
        ShowLanguagePage();

        AcceptButton = _okButton;
        CancelButton = _cancelButton;
    }

    private void SetColors()
    {
        _back = _darkMode ? Color.FromArgb(21, 23, 28) : Color.FromArgb(245, 247, 250);
        _panel = _darkMode ? Color.FromArgb(30, 34, 41) : Color.White;
        _input = _darkMode ? Color.FromArgb(32, 36, 43) : Color.White;
        _text = _darkMode ? Color.FromArgb(235, 238, 245) : Color.FromArgb(25, 28, 33);
        _button = _darkMode ? Color.FromArgb(42, 47, 56) : Color.FromArgb(238, 241, 246);
        _buttonActive = _darkMode ? Color.FromArgb(55, 62, 75) : Color.FromArgb(220, 226, 235);
        _border = _darkMode ? Color.FromArgb(74, 82, 97) : Color.FromArgb(200, 207, 218);
        _link = Color.FromArgb(77, 163, 255);
    }

    private void BuildInterface()
    {
        BackColor = _back;
        ForeColor = _text;

        Panel topPanel = new()
        {
            Dock = DockStyle.Top,
            Height = 42,
            BackColor = _back,
            Padding = new Padding(10, 8, 10, 0)
        };

        _languageTabButton.SetBounds(10, 8, 95, 28);
        _languageTabButton.Click += (_, _) => ShowLanguagePage();

        _steamTabButton.SetBounds(108, 8, 95, 28);
        _steamTabButton.Click += (_, _) => ShowSteamPage();
        _downpatchTabButton.SetBounds(206, 8, 104, 28);
        _downpatchTabButton.Click += (_, _) => ShowDownpatchPage();
        _infoTabButton.SetBounds(313, 8, 70, 28);
        _infoTabButton.Click += (_, _) => ShowInfoPage();

        StyleButton(_languageTabButton, false);
        StyleButton(_steamTabButton, false);
        StyleButton(_downpatchTabButton, false);
        StyleButton(_infoTabButton, false);

        topPanel.Controls.Add(_languageTabButton);
        topPanel.Controls.Add(_steamTabButton);
        topPanel.Controls.Add(_downpatchTabButton);
        topPanel.Controls.Add(_infoTabButton);

        foreach (CheckBox checkBox in new[] { _rememberLoginCheckBox, _rememberPasswordCheckBox, _createBackupCheckBox })
        {
            checkBox.BackColor = _panel;
            checkBox.ForeColor = _text;
            checkBox.Font = new Font(Font, FontStyle.Bold);
            checkBox.FlatStyle = FlatStyle.Flat;
            checkBox.Cursor = Cursors.Hand;
            checkBox.Paint += (_, e) => MainForm.PaintCheckBox(checkBox, e.Graphics, prominent: true);
        }
        // Restore saved choices before subscribing, so opening Settings never prompts.
        _rememberLoginCheckBox.Checked = RememberLogin;
        _rememberPasswordCheckBox.Checked = RememberPassword;
        _createBackupCheckBox.Checked = CreateBackup;
        _createBackupCheckBox.CheckedChanged += (_, _) => CreateBackup = _createBackupCheckBox.Checked;
        _rememberLoginCheckBox.CheckedChanged += (_, _) => RememberLogin = _rememberLoginCheckBox.Checked;
        _rememberPasswordCheckBox.CheckedChanged += RememberPasswordCheckBox_CheckedChanged;

        Panel bottomPanel = new()
        {
            Dock = DockStyle.Bottom,
            Height = 58,
            BackColor = _back,
            Padding = new Padding(12)
        };

        FlowLayoutPanel buttonsPanel = new()
        {
            Dock = DockStyle.Right,
            Width = 190,
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false,
            BackColor = _back
        };

        _cancelButton.Size = new Size(85, 30);
        _cancelButton.Click += (_, _) =>
        {
            DialogResult = DialogResult.Cancel;
            Close();
        };

        _okButton.Size = new Size(85, 30);
        _okButton.Click += OkButton_Click;

        StyleButton(_okButton, false);
        StyleButton(_cancelButton, false);

        buttonsPanel.Controls.Add(_cancelButton);
        buttonsPanel.Controls.Add(_okButton);
        bottomPanel.Controls.Add(buttonsPanel);

        _contentPanel.Dock = DockStyle.Fill;
        _contentPanel.BackColor = _panel;
        _contentPanel.Padding = new Padding(20);

        Controls.Add(_contentPanel);
        Controls.Add(bottomPanel);
        Controls.Add(topPanel);
    }

    private void ShowLanguagePage()
    {
        _currentPage = "language";
        ClearContentPage();

        StyleButton(_languageTabButton, true);
        StyleButton(_steamTabButton, false);
        StyleButton(_downpatchTabButton, false);
        StyleButton(_infoTabButton, false);

        Label languageLabel = new()
        {
            Text = T("ApplicationLanguage"),
            AutoSize = false,
            ForeColor = _text,
            BackColor = _panel,
            Font = new Font("Segoe UI", 9F, FontStyle.Bold)
        };

        languageLabel.SetBounds(20, 18, 250, 22);

        _languageComboBox.SetBounds(20, 50, 240, 28);
        _languageComboBox.DropDownStyle = ComboBoxStyle.DropDownList;
        _languageComboBox.BackColor = _input;
        _languageComboBox.ForeColor = _text;
        _languageComboBox.FlatStyle = FlatStyle.Flat;

        if (_languageComboBox.Items.Count == 0)
        {
            _languageComboBox.Items.Add(new LanguageItem("en", "English"));
            _languageComboBox.Items.Add(new LanguageItem("ru", "Русский"));
            _languageComboBox.Items.Add(new LanguageItem("es", "Español"));
            _languageComboBox.SelectedIndexChanged += LanguageComboBox_SelectedIndexChanged;
        }

        for (int i = 0; i < _languageComboBox.Items.Count; i++)
        {
            if (_languageComboBox.Items[i] is LanguageItem item &&
                string.Equals(item.Code, SelectedLanguage, StringComparison.OrdinalIgnoreCase))
            {
                _languageComboBox.SelectedIndex = i;
                break;
            }
        }

        if (_languageComboBox.SelectedIndex < 0)
        {
            _languageComboBox.SelectedIndex = 0;
        }

        Label noteLabel = new()
        {
            Text = T("DefaultLanguageNote"),
            AutoSize = false,
            ForeColor = _text,
            BackColor = _panel
        };

        noteLabel.SetBounds(20, 92, 360, 22);

        _contentPanel.Controls.Add(languageLabel);
        _contentPanel.Controls.Add(_languageComboBox);
        _contentPanel.Controls.Add(noteLabel);
    }

    private void ShowSteamPage()
    {
        _currentPage = "steam";
        ClearContentPage();
        StyleButton(_languageTabButton, false);
        StyleButton(_steamTabButton, true);
        StyleButton(_downpatchTabButton, false);
        StyleButton(_infoTabButton, false);

        Label heading = new()
        {
            Text = T("SteamSignIn"),
            ForeColor = _text,
            BackColor = _panel,
            Font = new Font(Font, FontStyle.Bold)
        };
        heading.SetBounds(20, 18, 400, 24);
        _rememberLoginCheckBox.Text = T("RememberLogin");
        _rememberLoginCheckBox.SetBounds(20, 54, 400, 34);
        _rememberPasswordCheckBox.Text = T("RememberPassword");
        _rememberPasswordCheckBox.SetBounds(20, 94, 400, 34);
        Label note = new()
        {
            Text = T("RememberSteamNote"),
            ForeColor = _text,
            BackColor = _panel
        };
        note.SetBounds(20, 146, 400, 60);
        _contentPanel.Controls.Add(heading);
        _contentPanel.Controls.Add(_rememberLoginCheckBox);
        _contentPanel.Controls.Add(_rememberPasswordCheckBox);
        _contentPanel.Controls.Add(note);
    }

    private void ShowDownpatchPage()
    {
        _currentPage = "downpatch";
        ClearContentPage();
        StyleButton(_languageTabButton, false);
        StyleButton(_steamTabButton, false);
        StyleButton(_downpatchTabButton, true);
        StyleButton(_infoTabButton, false);

        Label heading = new()
        {
            Text = T("DownpatchSettings"),
            ForeColor = _text,
            BackColor = _panel,
            Font = new Font(Font, FontStyle.Bold)
        };
        heading.SetBounds(20, 18, 400, 24);
        _createBackupCheckBox.Text = T("CreateBackup");
        _createBackupCheckBox.SetBounds(20, 54, 400, 34);
        Label note = new()
        {
            Text = T("BackupSettingsNote"),
            ForeColor = _text,
            BackColor = _panel
        };
        note.SetBounds(20, 106, 400, 120);
        _contentPanel.Controls.Add(heading);
        _contentPanel.Controls.Add(_createBackupCheckBox);
        _contentPanel.Controls.Add(note);
    }

    private void RememberPasswordCheckBox_CheckedChanged(object? sender, EventArgs e)
    {
        if (_rememberPasswordCheckBox.Checked &&
            !_confirmPasswordSaving(T("RememberPasswordWarning"), T("RememberPasswordWarningTitle")))
            _rememberPasswordCheckBox.Checked = false;
        RememberPassword = _rememberPasswordCheckBox.Checked;
    }

    private void ShowInfoPage()
    {
        _currentPage = "info";
        ClearContentPage();

        StyleButton(_languageTabButton, false);
        StyleButton(_steamTabButton, false);
        StyleButton(_downpatchTabButton, false);
        StyleButton(_infoTabButton, true);

        Label versionLabel = new()
        {
            Text = $"EternalDownpatcher {MainForm.AppVersion} (the best version!)",
            AutoSize = false,
            ForeColor = _text,
            BackColor = _panel,
            Font = new Font("Segoe UI", 9F, FontStyle.Bold)
        };

        versionLabel.SetBounds(20, 18, 360, 24);

        Control githubLink = CreateSocialLink(
            Random.Shared.Next(1000) < 8
            ? "claim your 3000 robux here"
            : "GitHub repo",
            "https://github.com/mrshadowq5/EternalDownpatcher",
            _githubLogo,
            58);

        Label helpLabel = new()
        {
            Text = T("HelpText"),
            AutoSize = false,
            ForeColor = _text,
            BackColor = _panel
        };

        helpLabel.SetBounds(20, 98, 390, 48);

        Control mdsr = CreateSocialLink(
            "MDSR discord",
            "https://discord.gg/dtDa9VZ",
            _discordLogo,
            154);

        Label contactLabel = new()
        {
            Text = T("Contact"),
            AutoSize = false,
            ForeColor = _text,
            BackColor = _panel
        };

        contactLabel.SetBounds(20, 194, 360, 24);

        Label madeByLabel = new()
        {
            Text = T("MadeBy"),
            AutoSize = false,
            ForeColor = _text,
            BackColor = _panel
        };

        madeByLabel.SetBounds(20, 234, 260, 24);

        _contentPanel.Controls.Add(versionLabel);
        _contentPanel.Controls.Add(githubLink);
        _contentPanel.Controls.Add(helpLabel);
        _contentPanel.Controls.Add(mdsr);
        _contentPanel.Controls.Add(contactLabel);
        _contentPanel.Controls.Add(madeByLabel);
    }

    private void LanguageComboBox_SelectedIndexChanged(object? sender, EventArgs e)
    {
        if (_languageComboBox.SelectedItem is not LanguageItem item)
        {
            return;
        }

        SelectedLanguage = item.Code;
        ApplyLanguage();

        if (_currentPage == "language")
        {
            ShowLanguagePage();
        }
        else if (_currentPage == "steam")
        {
            ShowSteamPage();
        }
        else if (_currentPage == "downpatch")
        {
            ShowDownpatchPage();
        }
        else
        {
            ShowInfoPage();
        }
    }

    private void ApplyLanguage()
    {
        Text = T("Settings");
        _languageTabButton.Text = T("Language");
        _steamTabButton.Text = "Credentials";
        _downpatchTabButton.Text = T("Downpatch");
        _infoTabButton.Text = T("Info");
        _okButton.Text = "OK";
        _cancelButton.Text = T("Cancel");
    }

    private void OkButton_Click(object? sender, EventArgs e)
    {
        if (_languageComboBox.SelectedItem is LanguageItem item)
        {
            SelectedLanguage = item.Code;
        }

        DialogResult = DialogResult.OK;
        Close();
    }

    private void StyleButton(Button button, bool active)
    {
        button.UseVisualStyleBackColor = false;
        button.FlatStyle = FlatStyle.Flat;
        button.BackColor = active ? _buttonActive : _button;
        button.ForeColor = _text;
        button.FlatAppearance.BorderColor = _border;
        button.FlatAppearance.MouseOverBackColor = _buttonActive;
        button.FlatAppearance.MouseDownBackColor = _button;
        button.Cursor = Cursors.Hand;
    }

    private string T(string key)
    {
        Dictionary<string, string> en = new()
        {
            ["Settings"] = "Settings",
            ["Downpatch"] = "Downpatch",
            ["DownpatchSettings"] = "Installed game downpatching",
            ["BackupSettingsNote"] = "Applies to Downpatch mode. Enabled: keep replaced files for restoration. Disabled: update the game directly, reusing existing data. An interrupted download must be resumed to the same version.",
            ["Language"] = "Language",
            ["Info"] = "Info",
            ["Cancel"] = "Cancel",
            ["ApplicationLanguage"] = "Application language",
            ["SteamSignIn"] = "Steam sign-in",
            ["RememberSteamNote"] = "Choose what to restore next time. Turn an option off and click OK to remove its saved data.",
            ["DefaultLanguageNote"] = "English is the default language.",
            ["DepotDownloaderVersion"] = "DepotDownloader version 1.2",
            ["HelpText"] = "If you run into a problem or a bug using Downpatcher, request help on MDSR Discord or ping/DM me if needed.",
            ["Contact"] = "Contact: mr.shadowq5 on Discord",
            ["MadeBy"] = "Made by mrshadowq5"
        };

        Dictionary<string, string> ru = new()
        {
            ["Settings"] = "Настройки",
            ["Downpatch"] = "Откат",
            ["DownpatchSettings"] = "Откат установленной игры",
            ["BackupSettingsNote"] = "Для режима «Откат». С галочкой заменяемые файлы сохраняются для восстановления. Без галочки игра обновляется напрямую с использованием имеющихся данных. Прерванную загрузку нужно продолжить до той же версии.",
            ["Language"] = "Язык",
            ["Info"] = "Инфо",
            ["Cancel"] = "Отмена",
            ["ApplicationLanguage"] = "Язык приложения",
            ["SteamSignIn"] = "Вход в Steam",
            ["RememberSteamNote"] = "Выберите, что подставлять при следующем запуске. Снятие галочки и нажатие OK удалит сохранённые данные.",
            ["DefaultLanguageNote"] = "Английский язык используется по умолчанию.",
            ["DepotDownloaderVersion"] = "Версия DepotDownloader: 1.2",
            ["HelpText"] = "Если у вас возникла проблема или вы столкнулись с багом, напишите в Modern DOOM Speedrunning Discord или пинганите/напишите мне в лс.",
            ["Contact"] = "Связаться со мной: mr.shadowq5 в Дискорде. Меня нет в МАКС.",
            ["MadeBy"] = "Шел второй день без игры в роблокс, у меня начались ломки."
        };

        Dictionary<string, string> es = new()
        {
            ["Settings"] = "Configuración",
            ["Downpatch"] = "Revertir",
            ["DownpatchSettings"] = "Revertir el juego instalado",
            ["BackupSettingsNote"] = "Se aplica al modo de reversión. Activado: guarda los archivos reemplazados para restaurarlos. Desactivado: actualiza el juego directamente reutilizando datos existentes. Si se interrumpe la descarga, reanúdala con la misma versión.",
            ["Language"] = "Idioma",
            ["Info"] = "Info",
            ["Cancel"] = "Cancelar",
            ["ApplicationLanguage"] = "Idioma de la aplicación",
            ["SteamSignIn"] = "Acceso a Steam",
            ["RememberSteamNote"] = "Elige qué recordar al iniciar de nuevo. Desactiva una opción y pulsa OK para eliminar sus datos guardados.",
            ["DefaultLanguageNote"] = "El inglés es el idioma predeterminado.",
            ["DepotDownloaderVersion"] = "Versión de DepotDownloader: 1.2",
            ["HelpText"] = "Si tienes un problema con EternalDownpatcher, pide ayuda en el Discord de Modern DOOM Speedrunning)",
            ["Contact"] = "Contacto: mr.shadowq5 en Discord",
            ["MadeBy"] = "Hecho por mrshadowq5"
        };

        Dictionary<string, string> selected = SelectedLanguage.ToLowerInvariant() switch
        {
            "ru" => ru,
            "es" => es,
            _ => en
        };

        if (selected.TryGetValue(key, out string? value))
        {
            return value;
        }

        return en.TryGetValue(key, out string? fallback) ? fallback : MainForm.T(SelectedLanguage, key);
    }

    private sealed class LanguageItem
    {
        public string Code { get; }
        public string Name { get; }

        public LanguageItem(string code, string name)
        {
            Code = code;
            Name = name;
        }

        public override string ToString()
        {
            return Name;
        }
    }

    #endregion

    #region Links and logos

    private Image? _githubLogo;
    private Image? _discordLogo;

    private void InitializeSocialIcons()
    {
        _githubLogo = TryLoadSocialIcon(_darkMode ? "github-white.png" : "github-black.png");
        _discordLogo = TryLoadSocialIcon("verify_your_age.png");
    }

    private static Image? TryLoadSocialIcon(string fileName)
    {
        try
        {
            string path = Path.Combine(AppContext.BaseDirectory, "assets", fileName);
            using Image source = Image.FromFile(path);
            // Copy the image so the PNG file does not stay locked while settings are open.
            return new Bitmap(source);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException or OutOfMemoryException)
        {
            // The link remains usable if optional artwork is missing or unreadable.
            return null;
        }
    }

    private Control CreateSocialLink(string text, string url, Image? logo, int top)
    {
        const int iconSize = 32;
        const int iconGap = 8;

        Panel row = new() { BackColor = _panel };
        row.SetBounds(20, top, 390, iconSize + 4);

        void OpenLink() => Process.Start(new ProcessStartInfo
        {
            FileName = url,
            UseShellExecute = true
        });

        if (logo is not null)
        {
            PictureBox icon = new()
            {
                Image = logo,
                SizeMode = PictureBoxSizeMode.Zoom,
                BackColor = _panel,
                Cursor = Cursors.Hand,
                TabStop = false
            };
            icon.SetBounds(0, 2, iconSize, iconSize);
            icon.Click += (_, _) => OpenLink();
            row.Controls.Add(icon);
        }

        LinkLabel link = new()
        {
            Text = text,
            AutoSize = false,
            TextAlign = ContentAlignment.MiddleLeft,
            BackColor = _panel,
            LinkColor = _link,
            ActiveLinkColor = Color.FromArgb(120, 190, 255),
            VisitedLinkColor = _link
        };
        int textLeft = logo is null ? 0 : iconSize + iconGap;
        link.SetBounds(textLeft, 0, row.Width - textLeft, row.Height);
        link.LinkClicked += (_, _) => OpenLink();
        row.Controls.Add(link);
        return row;
    }

    private void ClearContentPage()
    {
        while (_contentPanel.Controls.Count > 0)
        {
            Control control = _contentPanel.Controls[0];
            _contentPanel.Controls.Remove(control);
            // Keep input controls alive while switching between Settings pages.
            if (!ReferenceEquals(control, _languageComboBox) &&
                !ReferenceEquals(control, _rememberLoginCheckBox) &&
                !ReferenceEquals(control, _rememberPasswordCheckBox) &&
                !ReferenceEquals(control, _createBackupCheckBox))
            {
                control.Dispose();
            }
        }
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing)
        {
            _languageComboBox.Dispose();
            _rememberLoginCheckBox.Dispose();
            _rememberPasswordCheckBox.Dispose();
            _createBackupCheckBox.Dispose();
            _githubLogo?.Dispose();
            _discordLogo?.Dispose();
            _githubLogo = null;
            _discordLogo = null;
        }
    }

    #endregion
}
