using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;

namespace EternalDownpatcher.WinForms;

public sealed class SettingsForm : Form
{
    private readonly Panel _contentPanel = new();
    private readonly Button _languageTabButton = new();
    private readonly Button _infoTabButton = new();
    private readonly ComboBox _languageComboBox = new();
    private readonly Button _okButton = new();
    private readonly Button _cancelButton = new();

    private readonly bool _darkMode;
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

    public SettingsForm(UserSettings settings)
    {
        _darkMode = settings.DarkMode;
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

        _infoTabButton.SetBounds(108, 8, 70, 28);
        _infoTabButton.Click += (_, _) => ShowInfoPage();

        StyleButton(_languageTabButton, false);
        StyleButton(_infoTabButton, false);

        topPanel.Controls.Add(_languageTabButton);
        topPanel.Controls.Add(_infoTabButton);

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
        _contentPanel.Controls.Clear();

        StyleButton(_languageTabButton, true);
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

    private void ShowInfoPage()
    {
        _currentPage = "info";
        _contentPanel.Controls.Clear();

        StyleButton(_languageTabButton, false);
        StyleButton(_infoTabButton, true);

        Label versionLabel = new()
        {
            Text = T("DepotDownloaderVersion"),
            AutoSize = false,
            ForeColor = _text,
            BackColor = _panel,
            Font = new Font("Segoe UI", 9F, FontStyle.Bold)
        };

        versionLabel.SetBounds(20, 18, 360, 24);

        LinkLabel githubLink = new()
        {
            Text = "GitHub",
            AutoSize = false,
            BackColor = _panel,
            LinkColor = _link,
            ActiveLinkColor = Color.FromArgb(120, 190, 255),
            VisitedLinkColor = _link
        };

        githubLink.SetBounds(20, 58, 180, 24);
        githubLink.LinkClicked += (_, _) =>
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "https://github.com/mrshadowq5/EternalDownpatcher",
                UseShellExecute = true
            });
        };

        Label helpLabel = new()
        {
            Text = T("HelpText"),
            AutoSize = false,
            ForeColor = _text,
            BackColor = _panel
        };

        helpLabel.SetBounds(20, 98, 390, 48);

        LinkLabel modernDoomLink = new()
        {
            Text = "Modern DOOM Speedrunning",
            AutoSize = false,
            BackColor = _panel,
            LinkColor = _link,
            ActiveLinkColor = Color.FromArgb(120, 190, 255),
            VisitedLinkColor = _link
        };

        modernDoomLink.SetBounds(20, 154, 240, 24);
        modernDoomLink.LinkClicked += (_, _) =>
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "https://discord.gg/dtDa9VZ",
                UseShellExecute = true
            });
        };

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
        _contentPanel.Controls.Add(modernDoomLink);
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
        else
        {
            ShowInfoPage();
        }
    }

    private void ApplyLanguage()
    {
        Text = T("Settings");
        _languageTabButton.Text = T("Language");
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
            ["Language"] = "Language",
            ["Info"] = "Info",
            ["Cancel"] = "Cancel",
            ["ApplicationLanguage"] = "Application language",
            ["DefaultLanguageNote"] = "English is the default language.",
            ["DepotDownloaderVersion"] = "DepotDownloader version 1.2",
            ["HelpText"] = "If you run into a problem using EternalDownpatcher, request help on Modern DOOM Speedrunning Discord.",
            ["Contact"] = "Contact: stonedealer05 on Discord",
            ["MadeBy"] = "Made by mrshadowq5"
        };

        Dictionary<string, string> ru = new()
        {
            ["Settings"] = "Настройки",
            ["Language"] = "Язык",
            ["Info"] = "Инфо",
            ["Cancel"] = "Отмена",
            ["ApplicationLanguage"] = "Язык приложения",
            ["DefaultLanguageNote"] = "Английский язык используется по умолчанию.",
            ["DepotDownloaderVersion"] = "Версия DepotDownloader: 1.2",
            ["HelpText"] = "Если у вас возникла проблема с EternalDownpatcher, напишите в Modern DOOM Speedrunning Discord.",
            ["Contact"] = "Контакт: stonedealer05 в Discord",
            ["MadeBy"] = "Шел второй день без игры в роблокс, у меня начались ломки."
        };

        Dictionary<string, string> es = new()
        {
            ["Settings"] = "Configuración",
            ["Language"] = "Idioma",
            ["Info"] = "Info",
            ["Cancel"] = "Cancelar",
            ["ApplicationLanguage"] = "Idioma de la aplicación",
            ["DefaultLanguageNote"] = "El inglés es el idioma predeterminado.",
            ["DepotDownloaderVersion"] = "Versión de DepotDownloader: 1.2",
            ["HelpText"] = "Si tienes un problema con EternalDownpatcher, pide ayuda en el Discord de Modern DOOM Speedrunning.",
            ["Contact"] = "Contacto: stonedealer05 en Discord",
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

        return en[key];
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
}