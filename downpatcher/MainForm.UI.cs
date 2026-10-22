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

public sealed partial class MainForm : Form
{
    #region Layout

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
        _themeToggleButton.Click += ThemeToggleButton_Click;
        _settingsButton.SetBounds(672, 10, 34, 28);
        _settingsButton.Click += SettingsButton_Click;
        InitializeButtonIcons();

        _gameFolderStatusLabel.SetBounds(12, 55, 455, 22);
        _gameFolderStatusLabel.Text = "DOOM Eternal root folder has not been selected.";
        _gameFolderStatusLabel.ForeColor = Color.Red;
        _gameFolderStatusLabel.TextAlign = ContentAlignment.MiddleLeft;
        _selectGameFolderButton.SetBounds(472, 53, 236, 25);
        _selectGameFolderButton.Text = "Select DOOM Eternal Folder";
        _selectGameFolderButton.Click += SelectGameFolderButton_Click;
        _versionLabel.Text = "Game Version *";
        _versionLabel.AutoSize = false;
        _versionLabel.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
        _versionLabel.SetBounds(12, 92, 140, 20);
        _loginLabel.Text = "Steam Login *";
        _loginLabel.AutoSize = false;
        _loginLabel.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
        _loginLabel.SetBounds(160, 92, 160, 20);
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
        _startButton.SetBounds(12, 216, 112, 34);
        _startButton.Text = "Downpatch";
        _startButton.Font = new Font(Font, FontStyle.Bold);
        _startButton.Click += StartButton_Click;
        _startButton.EnabledChanged += (_, _) =>
            ApplyThemeToControl(_startButton, CaptureCurrentThemePalette(), _isDarkTheme);
        _cancelButton.SetBounds(130, 219, 75, 27);
        _cancelButton.Text = "Cancel";
        _cancelButton.Enabled = false;
        _cancelButton.Click += CancelButton_Click;
        _downloadModePanel.SetBounds(215, 216, 323, 34);
        _downloadAllFilesRadioButton.SetBounds(0, 0, 165, 34);
        _downloadAllFilesRadioButton.Text = "Download All Files";
        _downloadAllFilesRadioButton.Font = new Font(Font, FontStyle.Bold);
        _downloadAllFilesRadioButton.Cursor = Cursors.Hand;
        _downloadAllFilesRadioButton.Checked = false;
        _downloadAllFilesRadioButton.Enabled = true;
        _downloadAllFilesRadioButton.CheckedChanged += DownloadMode_CheckedChanged;
        _downloadAllFilesRadioButton.Paint += PaintModeRadioButton;
        _patchExistingRadioButton.SetBounds(175, 0, 148, 34);
        _patchExistingRadioButton.Text = "Downpatch";
        _patchExistingRadioButton.Font = new Font(Font, FontStyle.Bold);
        _patchExistingRadioButton.Cursor = Cursors.Hand;
        _patchExistingRadioButton.Paint += PaintModeRadioButton;
        // A shared parent gives the two modes native, mutually exclusive selection.
        _downloadModePanel.Controls.Add(_downloadAllFilesRadioButton);
        _downloadModePanel.Controls.Add(_patchExistingRadioButton);
        _validateFilesCheckBox.SetBounds(548, 216, 160, 34);
        _validateFilesCheckBox.Text = "Validate files";
        _validateFilesCheckBox.Font = new Font(Font, FontStyle.Bold);
        _validateFilesCheckBox.Cursor = Cursors.Hand;
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
        Controls.Add(_versionLabel);
        Controls.Add(_loginLabel);
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
        Controls.Add(_downloadModePanel);
        Controls.Add(_validateFilesCheckBox);
        Controls.Add(_consoleLabel);
        Controls.Add(_copyLogButton);
        Controls.Add(_consoleBox);
        Controls.Add(_restoreBackupButton);
        Controls.Add(_openOutputFolderButton);

        foreach (Control control in Controls)
        {
            if (control == _validateFilesCheckBox)
                control.Paint += PaintModeCheckBox;
            else if (control is Button or CheckBox)
                control.Paint += PaintDisabledControl;
        }
    }

    #endregion

    #region Theme

    private static void PaintDisabledControl(object? sender, PaintEventArgs e)
    {
        if (sender is not Control control || control.Enabled)
            return;

        // Flat WinForms controls ignore ForeColor when disabled. Paint their
        // captions explicitly so they remain readable against either theme.
        Color textColor = LerpColor(control.BackColor, control.ForeColor, 0.65);
        if (control is Button button && button.Image is null)
        {
            Rectangle content = Rectangle.Inflate(button.ClientRectangle, -1, -1);
            using SolidBrush background = new(button.BackColor);
            e.Graphics.FillRectangle(background, content);
            TextRenderer.DrawText(e.Graphics, button.Text, button.Font, content, textColor,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter |
                TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis);
        }
        else if (control is CheckBox checkBox)
        {
            PaintCheckBox(checkBox, e.Graphics, prominent: false);
        }
    }

    private static void PaintModeCheckBox(object? sender, PaintEventArgs e)
    {
        if (sender is CheckBox checkBox)
            PaintCheckBox(checkBox, e.Graphics, prominent: true);
    }

    private static void PaintModeRadioButton(object? sender, PaintEventArgs e)
    {
        if (sender is not RadioButton radioButton)
            return;
        Color accent = Color.FromArgb(0, 120, 215);
        Color backgroundColor = LerpColor(radioButton.BackColor, radioButton.ForeColor, 0.05);
        if (radioButton.Enabled && radioButton.Checked)
            backgroundColor = LerpColor(backgroundColor, accent, 0.14);
        Color textColor = radioButton.Enabled ? radioButton.ForeColor : LerpColor(backgroundColor, radioButton.ForeColor, 0.65);
        Color outline = radioButton.Enabled && radioButton.Checked ? accent :
            LerpColor(backgroundColor, radioButton.ForeColor, radioButton.Enabled ? 0.35 : 0.2);
        using SolidBrush background = new(backgroundColor);
        using Pen frame = new(outline);
        e.Graphics.FillRectangle(background, radioButton.ClientRectangle);
        e.Graphics.DrawRectangle(frame, 0, 0, radioButton.Width - 1, radioButton.Height - 1);

        float scale = radioButton.DeviceDpi / 96F;
        int size = (int)Math.Round(18 * scale);
        int inset = (int)Math.Round(8 * scale);
        Rectangle glyph = new(inset, (radioButton.Height - size) / 2, size - 1, size - 1);
        Color glyphColor = radioButton.Enabled && radioButton.Checked ? accent : LerpColor(backgroundColor, radioButton.ForeColor, 0.08);
        using SolidBrush fill = new(glyphColor);
        using Pen border = new(radioButton.Enabled && radioButton.Checked ? accent : LerpColor(backgroundColor, radioButton.ForeColor, 0.5));
        e.Graphics.FillEllipse(fill, glyph);
        e.Graphics.DrawEllipse(border, glyph);
        if (radioButton.Checked)
        {
            int innerInset = (int)Math.Round(5 * scale);
            using SolidBrush dot = new(radioButton.Enabled ? Color.White : textColor);
            e.Graphics.FillEllipse(dot, Rectangle.Inflate(glyph, -innerInset, -innerInset));
        }
        Rectangle text = Rectangle.FromLTRB(glyph.Right + inset, 0, radioButton.Width - inset, radioButton.Height);
        TextRenderer.DrawText(e.Graphics, radioButton.Text, radioButton.Font, text, textColor,
            TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis);
        if (radioButton.Enabled && radioButton.Focused)
            ControlPaint.DrawFocusRectangle(e.Graphics, Rectangle.Inflate(radioButton.ClientRectangle, -4, -4), textColor, backgroundColor);
    }

    internal static void PaintCheckBox(CheckBox checkBox, Graphics graphics, bool prominent)
    {
        Color accent = Color.FromArgb(0, 120, 215);
        Color backgroundColor = prominent ? LerpColor(checkBox.BackColor, checkBox.ForeColor, 0.05) : checkBox.BackColor;
        if (prominent && checkBox.Enabled && checkBox.Checked)
            backgroundColor = LerpColor(backgroundColor, accent, 0.14);
        Color textColor = checkBox.Enabled ? checkBox.ForeColor : LerpColor(backgroundColor, checkBox.ForeColor, 0.65);
        using SolidBrush background = new(backgroundColor);
        graphics.FillRectangle(background, checkBox.ClientRectangle);

        float scale = checkBox.DeviceDpi / 96F;
        if (prominent)
        {
            Color outline = checkBox.Enabled && checkBox.Checked ? accent :
                LerpColor(backgroundColor, checkBox.ForeColor, checkBox.Enabled ? 0.35 : 0.2);
            using Pen frame = new(outline);
            graphics.DrawRectangle(frame, 0, 0, checkBox.Width - 1, checkBox.Height - 1);
        }

        int size = (int)Math.Round((prominent ? 18 : 13) * scale);
        int inset = (int)Math.Round((prominent ? 8 : 1) * scale);
        Rectangle glyph = new(inset, (checkBox.Height - size) / 2, size - 1, size - 1);
        Color glyphColor = checkBox.Enabled && checkBox.Checked ? accent : LerpColor(backgroundColor, checkBox.ForeColor, 0.08);
        using SolidBrush fill = new(glyphColor);
        using Pen border = new(checkBox.Enabled && checkBox.Checked ? accent : LerpColor(backgroundColor, checkBox.ForeColor, 0.5));
        graphics.FillRectangle(fill, glyph);
        graphics.DrawRectangle(border, glyph);
        if (checkBox.CheckState != CheckState.Unchecked)
        {
            using Pen mark = new(checkBox.Enabled ? Color.White : textColor, Math.Max(1, 2 * scale));
            if (checkBox.CheckState == CheckState.Indeterminate)
                graphics.DrawLine(mark, glyph.Left + glyph.Width * 0.2F, glyph.Top + glyph.Height * 0.5F,
                    glyph.Left + glyph.Width * 0.8F, glyph.Top + glyph.Height * 0.5F);
            else
                graphics.DrawLines(mark, [
                    new PointF(glyph.Left + glyph.Width * 0.2F, glyph.Top + glyph.Height * 0.5F),
                    new PointF(glyph.Left + glyph.Width * 0.45F, glyph.Top + glyph.Height * 0.75F),
                    new PointF(glyph.Left + glyph.Width * 0.8F, glyph.Top + glyph.Height * 0.2F)]);
        }
        Rectangle text = Rectangle.FromLTRB(glyph.Right + (int)Math.Round((prominent ? 8 : 5) * scale), 0,
            checkBox.Width - (prominent ? inset : 0), checkBox.Height);
        TextRenderer.DrawText(graphics, checkBox.Text, checkBox.Font, text, textColor,
            TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis);
        if (prominent && checkBox.Enabled && checkBox.Focused)
            ControlPaint.DrawFocusRectangle(graphics, Rectangle.Inflate(checkBox.ClientRectangle, -4, -4), textColor, backgroundColor);
    }

    private readonly record struct ThemePalette(Color FormBack, Color TextColor, Color MutedTextColor, Color InputBack, Color InputFore, Color InputBorder, Color ButtonBack, Color ButtonFore, Color ButtonHover, Color ButtonDown, Color ErrorColor, Color SuccessColor, Color LogBack, Color LogFore);
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
        double progress = Math.Min(1.0, (double)_themeTransitionFrame / ThemeTransitionFrames);
        progress = progress * progress * (3.0 - 2.0 * progress);
        ThemePalette palette = LerpThemePalette(_themeTransitionFrom, _themeTransitionTo, progress);
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
        return new ThemePalette(BackColor, ForeColor, fallback.MutedTextColor, _steamUsernameTextBox.BackColor, _steamUsernameTextBox.ForeColor, fallback.InputBorder, _cancelButton.BackColor, _cancelButton.ForeColor, fallback.ButtonHover, fallback.ButtonDown, fallback.ErrorColor, fallback.SuccessColor, _consoleBox.BackColor, _consoleBox.ForeColor);
    }

    private static ThemePalette GetThemePalette(bool dark)
    {
        if (dark)
        {
            return new ThemePalette(Color.FromArgb(21, 23, 28), Color.FromArgb(235, 238, 245), Color.FromArgb(170, 176, 188), Color.FromArgb(32, 36, 43), Color.FromArgb(240, 243, 250), Color.FromArgb(74, 82, 97), Color.FromArgb(42, 47, 56), Color.FromArgb(240, 243, 250), Color.FromArgb(52, 59, 70), Color.FromArgb(35, 40, 48), Color.FromArgb(255, 107, 107), Color.FromArgb(123, 216, 143), Color.Black, Color.FromArgb(235, 235, 235));
        }

        return new ThemePalette(Color.FromArgb(245, 247, 250), Color.FromArgb(25, 28, 33), Color.FromArgb(90, 98, 110), Color.White, Color.FromArgb(25, 28, 33), Color.FromArgb(200, 207, 218), Color.FromArgb(238, 241, 246), Color.FromArgb(25, 28, 33), Color.FromArgb(226, 231, 238), Color.FromArgb(215, 221, 230), Color.FromArgb(210, 45, 45), Color.FromArgb(32, 145, 70), Color.Black, Color.FromArgb(235, 235, 235));
    }

    private static ThemePalette LerpThemePalette(ThemePalette from, ThemePalette to, double amount)
    {
        return new ThemePalette(LerpColor(from.FormBack, to.FormBack, amount), LerpColor(from.TextColor, to.TextColor, amount), LerpColor(from.MutedTextColor, to.MutedTextColor, amount), LerpColor(from.InputBack, to.InputBack, amount), LerpColor(from.InputFore, to.InputFore, amount), LerpColor(from.InputBorder, to.InputBorder, amount), LerpColor(from.ButtonBack, to.ButtonBack, amount), LerpColor(from.ButtonFore, to.ButtonFore, amount), LerpColor(from.ButtonHover, to.ButtonHover, amount), LerpColor(from.ButtonDown, to.ButtonDown, amount), LerpColor(from.ErrorColor, to.ErrorColor, amount), LerpColor(from.SuccessColor, to.SuccessColor, amount), LerpColor(from.LogBack, to.LogBack, amount), LerpColor(from.LogFore, to.LogFore, amount));
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

    private void ApplyThemeToControl(Control control, ThemePalette palette, bool dark)
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
            if (button == _startButton)
            {
                Color accent = Color.FromArgb(0, 120, 215);
                Color background = LerpColor(palette.FormBack, palette.TextColor, 0.05);
                button.BackColor = button.Enabled ? LerpColor(background, accent, 0.14) : background;
                button.ForeColor = palette.TextColor;
                button.FlatAppearance.MouseOverBackColor = LerpColor(background, accent, 0.22);
                button.FlatAppearance.MouseDownBackColor = LerpColor(background, accent, 0.32);
            }
        }
        else if (control is RadioButton radioButton)
        {
            radioButton.BackColor = palette.FormBack;
            radioButton.ForeColor = palette.TextColor;
            radioButton.FlatStyle = FlatStyle.Flat;
        }
        else if (control is CheckBox checkBox)
        {
            checkBox.BackColor = palette.FormBack;
            checkBox.ForeColor = palette.TextColor;
            checkBox.FlatStyle = FlatStyle.Flat;
            checkBox.FlatAppearance.BorderColor = palette.InputBorder;
            checkBox.FlatAppearance.CheckedBackColor = dark ? Color.FromArgb(77, 163, 255) : Color.FromArgb(0, 120, 215);
            checkBox.FlatAppearance.MouseOverBackColor = palette.ButtonHover;
        }
        else if (control is Label label)
        {
            label.BackColor = palette.FormBack;
            if (label == _gameFolderStatusLabel || label == _requiredFieldsLabel)
            {
                ApplyStatusLabelColors(palette);
            }
            else if (label == _titleLabel)
            {
                label.ForeColor = dark ? Color.FromArgb(245, 247, 255) : Color.FromArgb(20, 24, 31);
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
        _gameFolderStatusLabel.ForeColor = IsPositiveStatus(_gameFolderStatusLabel.Text) ? palette.SuccessColor : palette.ErrorColor;
        _requiredFieldsLabel.ForeColor = IsPositiveStatus(_requiredFieldsLabel.Text) ? palette.SuccessColor : palette.ErrorColor;
    }

    private static bool IsPositiveStatus(string text)
    {
        string value = text.ToLowerInvariant();
        return value.Contains("ready") || value.Contains("готово") || value.Contains("listo") || value.Contains("root selected") || value.Contains("папка выбрана") || value.Contains("carpeta seleccionada") || value.Contains("download all files mode") || value.Contains("режим download all files") || value.Contains("modo download all files") || value.Contains("installed version") || value.Contains("установленная версия") || value.Contains("versión instalada");
    }

    private void ThemeToggleButton_Click(object? sender, EventArgs e)
    {
        bool newDarkMode = !_isDarkTheme;
        _userSettings.DarkMode = newDarkMode;
        UserSettingsStore.Save(_userSettings);
        StartThemeTransition(newDarkMode);
    }

    #endregion

    #region Icons

    private Image? _settingsIconImage;
    private Image? _sunIconImage;
    private Image? _moonIconImage;

    private void InitializeButtonIcons()
    {
        _settingsIconImage = TryLoadButtonImage("settings-toolbar.png");
        _sunIconImage = TryLoadButtonImage("theme-sun.png");
        _moonIconImage = TryLoadButtonImage("theme-moon.png");

        _settingsButton.ImageAlign = ContentAlignment.MiddleCenter;
        _themeToggleButton.ImageAlign = ContentAlignment.MiddleCenter;
        _settingsButton.Image = _settingsIconImage;
        _settingsButton.Text = _settingsIconImage is null ? "⚙" : string.Empty;
        if (_settingsIconImage is null)
        {
            _settingsButton.Font = new Font("Segoe UI Symbol", 10F);
        }
    }

    private static Image? TryLoadButtonImage(string fileName)
    {
        try
        {
            return LoadButtonImage(fileName, 16);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException or OutOfMemoryException)
        {
            // Missing or unreadable optional artwork should not prevent startup
            return null;
        }
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

    private void UpdateThemeToggleIcon(bool dark)
    {
        // Show the theme that clicking the button will switch to
        Image? image = dark ? _sunIconImage : _moonIconImage;
        _themeToggleButton.Image = image;
        _themeToggleButton.Text = image is null ? (dark ? "☀" : "🌙") : string.Empty;
    }

    private void DisposeButtonIcons()
    {
        _settingsButton.Image = null;
        _themeToggleButton.Image = null;
        _settingsIconImage?.Dispose();
        _sunIconImage?.Dispose();
        _moonIconImage?.Dispose();
        _settingsIconImage = null;
        _sunIconImage = null;
        _moonIconImage = null;
    }

    #endregion

    #region Language

    private string L(string key)
    {
        string language = string.IsNullOrWhiteSpace(_userSettings.Language) ? "en" : _userSettings.Language;
        return T(language, key);
    }

    private string LF(string key, params object[] args)
    {
        return string.Format(L(key), args);
    }

    private void SettingsButton_Click(object? sender, EventArgs e)
    {
        using SettingsForm settingsForm = new(_userSettings);
        if (settingsForm.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        ApplySettings(settingsForm);
    }

    private void ApplySettings(SettingsForm settingsForm)
    {
        _userSettings.Language = settingsForm.SelectedLanguage;
        _userSettings.RememberLogin = settingsForm.RememberLogin;
        _userSettings.RememberPassword = settingsForm.RememberPassword;
        SaveSteamCredentials();
        ApplyLanguage();
    }

    private void ApplyLanguage()
    {
        string language = string.IsNullOrWhiteSpace(_userSettings.Language) ? "en" : _userSettings.Language;
        _selectGameFolderButton.Text = T(language, "SelectGameFolder");
        _versionLabel.Text = T(language, "GameVersion");
        _loginLabel.Text = T(language, "SteamUsername");
        _passwordLabel.Text = T(language, "SteamPassword");
        _downloadAllFilesRadioButton.Text = T(language, "DownloadAllFiles");
        _patchExistingRadioButton.Text = T(language, "PatchExistingMode");
        _validateFilesCheckBox.Text = T(language, "ValidateFiles");
        _consoleLabel.Text = T(language, "OutputLog");
        _copyLogButton.Text = T(language, "CopyLog");
        _selectDownpatchFolderButton.Text = T(language, "Browse");
        _restoreBackupButton.Text = T(language, "RestoreBackup");
        _openOutputFolderButton.Text = T(language, "OpenFolder");
        _startButton.Text = _downloadAllFilesRadioButton.Checked ? T(language, "Download") : T(language, "Downpatch");
        _folderLabel.Text = _downloadAllFilesRadioButton.Checked ? T(language, "DownloadFolder") : T(language, "PatchFilesFolder");
        if (_downloadAllFilesRadioButton.Checked)
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
        else
        {
            _gameFolderStatusLabel.Text = T(language, "RootSelectedButVersionNotDetected");
        }

        if (string.IsNullOrWhiteSpace(_requiredFieldsLabel.Text) || _requiredFieldsLabel.Text.Contains("Required", StringComparison.OrdinalIgnoreCase) || _requiredFieldsLabel.Text.Contains("обяз", StringComparison.OrdinalIgnoreCase) || _requiredFieldsLabel.Text.Contains("oblig", StringComparison.OrdinalIgnoreCase))
        {
            _requiredFieldsLabel.Text = T(language, "RequiredFieldsMissing");
        }

        ApplyStatusLabelColors(GetThemePalette(_isDarkTheme));
    }

    internal static string T(string language, string key)
    {
        Dictionary<string, string> en = new()
        {
            ["SelectGameFolder"] = "Select game folder",
            ["GameVersion"] = "Game Version *",
            ["SteamUsername"] = "Steam Login *",
            ["SteamPassword"] = "Steam Password *",
            ["RememberLogin"] = "Remember Steam login",
            ["RememberPassword"] = "Remember Steam password",
            ["RememberPasswordWarningTitle"] = "are you sure??",
            ["RememberPasswordWarning"] = "Your Steam password will be saved on this computer, encrypted for your Windows account. Programs running under the same account may still access it.\n\nUse this only on a trusted personal computer. Turning this off and clicking OK removes the password saved by EternalDownpatcher. \n\nEnable password saving?",
            ["CredentialSaveFailed"] = "Could not update the saved login settings. Previously saved sign-in data may still remain in the settings file.",
            ["SteamGuardPrompt"] = "Enter the Steam Guard code from your email or authenticator app. The code will not be saved.",
            ["SteamGuardConfirm"] = "Continue",
            ["SteamGuardCancel"] = "Cancel",
            ["DownloadAllFiles"] = "Download All Files",
            ["PatchExistingMode"] = "Downpatch",
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
            ["DownloadAllFilesStatus"] = "Download All Files mode",
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
            ["UnknownVersionRecovery"] = "This installation is not recognized. EXE size: {0} bytes. Latest supported source version: {1}.\n\nSelective downpatching cannot continue with this installation. You can download a complete older version to a separate folder. Your installed game will stay unchanged.\n\nSwitch to Download All Files?",
            ["BackupRestored"] = "Backup restored successfully.",
            ["WaitBeforeClosing"] = "Please wait for preparation or cancellation and restoration to finish, then close the application.",
            ["LegacyBackupWarning"] = "This is an older backup. Original files were restored, but files added by the old patcher cannot be identified or removed automatically.",
            ["DepotFailed"] = "DepotDownloader failed. Exit code: {0}",
            ["OutputFolderEmptyAfterDownload"] = "DepotDownloader finished, but output folder is empty.",
            ["DownloadedExecutableMissing"] = "The selected game executable was not downloaded. The installation was not changed.",
            ["DownloadedExecutableMismatch"] = "The downloaded executable does not match the selected version. The installation was not changed.",
            ["SourceVersionChanged"] = "The installed version changed during the download. The patch was not applied. Select the game folder again before retrying.",
            ["DepotDownloaderMissing"] = "DepotDownloader.exe is missing. Restart EternalDownpatcher to install it before opening the main window.",
            ["DepotSetupTitle"] = "Preparing DepotDownloader",
            ["DepotDownloading"] = "Downloading DepotDownloader from GitHub...",
            ["DepotInstalling"] = "Installing DepotDownloader...",
            ["DepotCancelling"] = "Cancelling. Please wait...",
            ["DepotCheckingUpdates"] = "Checking DepotDownloader updates on GitHub...",
            ["DepotUpdateCheckFailed"] = "Could not check DepotDownloader updates: {0}. Keeping the installed version.",
            ["DepotAlreadyUpdated"] = "DepotDownloader is up to date.",
            ["DepotCheckTimeout"] = "the check timed out",
            ["DepotUpdateTitle"] = "DepotDownloader update available",
            ["DepotUpdateDetails"] = "Installed: {0}\nAvailable: {1}\nPublished: {2}",
            ["DepotUnknownVersion"] = "Unknown",
            ["DepotUpdateNoNotes"] = "Release notes are unavailable here. Open the GitHub release for full details.",
            ["DepotUpdateRelease"] = "View release on GitHub",
            ["DepotUpdateAction"] = "OK downloads and installs this update. If you ignore it, you will be reminded at the next launch.",
            ["DepotUpdateIgnore"] = "Ignore",
            ["DepotUpdateSuccess"] = "DepotDownloader was updated successfully.",
            ["WelcomeLog"] = "Welcome to EternalDownpatcher.",
            ["SelectVersionLog"] = "Select a version manually.",
            ["SelectRootFolderLog"] = "Select DOOM Eternal root folder.",
            ["WorkingFolderLog"] = "Working folder: {0}",
            ["DefaultPatchFolderLog"] = "Default patch files folder: {0}",
            ["LoadedVersionsLog"] = "Loaded versions: {0}",
            ["VersionsSourceLog"] = "Versions source: {0}",
            ["CheckingVersionData"] = "Checking game versions on GitHub...",
            ["VersionDataReady"] = "Game versions and filelists are up to date.",
            ["VersionDataCancelled"] = "Cancelled or timed out.",
            ["VersionDataFallback"] = "Could not update game data; keeping the local data. {0}",
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
            ["RememberLogin"] = "Запомнить логин Steam",
            ["RememberPassword"] = "Запомнить пароль Steam",
            ["RememberPasswordWarningTitle"] = "Сохранить пароль Steam?",
            ["RememberPasswordWarning"] = "Пароль Steam будет сохранён на этом компьютере с шифрованием для вашей учётной записи Windows. Программы, работающие под этой же учётной записью, всё ещё могут получить к нему доступ.\n\nИспользуйте это только на доверенном личном компьютере. Снятие галочки и нажатие OK удалит пароль, сохранённый EternalDownpatcher. DepotDownloader может отдельно хранить данные сессии Steam.\n\nВключить сохранение пароля?",
            ["CredentialSaveFailed"] = "Не удалось обновить настройки входа. Ранее сохранённые данные входа могли остаться в файле настроек.",
            ["SteamGuardPrompt"] = "Введите код Steam Guard из письма или приложения-аутентификатора. Код не будет сохранён.",
            ["SteamGuardConfirm"] = "Продолжить",
            ["SteamGuardCancel"] = "Отмена",
            ["DownloadAllFiles"] = "Скачать все файлы",
            ["PatchExistingMode"] = "Откат игры",
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
            ["UnknownVersionRecovery"] = "Установленная версия не распознана. Размер EXE: {0} байт. Последняя поддерживаемая исходная версия: {1}.\n\nВыборочный откат этой установки недоступен. Можно скачать полную старую версию в отдельную папку. Установленная игра останется без изменений.\n\nПереключиться в режим «Скачать все файлы»?",
            ["BackupRestored"] = "Бэкап успешно восстановлен.",
            ["WaitBeforeClosing"] = "Дождитесь завершения подготовки или отмены и восстановления файлов, затем закройте приложение.",
            ["LegacyBackupWarning"] = "Это старый бэкап. Исходные файлы восстановлены, но определить и удалить файлы, добавленные старой версией патчера, автоматически невозможно.",
            ["DepotFailed"] = "DepotDownloader завершился с ошибкой. Код выхода: {0}",
            ["OutputFolderEmptyAfterDownload"] = "DepotDownloader завершился, но папка вывода пустая.",
            ["DownloadedExecutableMissing"] = "Исполняемый файл выбранной версии не был скачан. Установленная игра не изменена.",
            ["DownloadedExecutableMismatch"] = "Скачанный EXE не соответствует выбранной версии. Установленная игра не изменена.",
            ["SourceVersionChanged"] = "Установленная версия изменилась во время загрузки. Патч не применён. Перед повтором заново выберите папку игры.",
            ["DownloadFolderDoesNotExistCreate"] = "Папка загрузки не существует.\n\nСоздать её сейчас?",
            ["PatchFolderDoesNotExistCreate"] = "Папка патч-файлов не существует.\n\nСоздать её сейчас?",
            ["CreateDownloadFolder"] = "Создать папку загрузки",
            ["CreatePatchFolder"] = "Создать папку патч-файлов",
            ["DepotDownloaderMissing"] = "DepotDownloader.exe отсутствует. Перезапустите EternalDownpatcher: загрузчик будет установлен до открытия главного окна.",
            ["DepotSetupTitle"] = "Подготовка DepotDownloader",
            ["DepotDownloading"] = "Скачивание DepotDownloader с GitHub...",
            ["DepotInstalling"] = "Установка DepotDownloader...",
            ["DepotCancelling"] = "Отмена. Подождите...",
            ["DepotCheckingUpdates"] = "Проверка обновлений DepotDownloader на GitHub...",
            ["DepotUpdateCheckFailed"] = "Не удалось проверить обновления DepotDownloader: {0}. Используется установленная версия.",
            ["DepotAlreadyUpdated"] = "DepotDownloader обновлён до актуальной версии.",
            ["DepotCheckTimeout"] = "время ожидания истекло",
            ["DepotUpdateTitle"] = "Доступно обновление DepotDownloader",
            ["DepotUpdateDetails"] = "Установлено: {0}\nДоступно: {1}\nДата выпуска: {2}",
            ["DepotUnknownVersion"] = "Неизвестно",
            ["DepotUpdateNoNotes"] = "Не удалось получить описание изменений. Полная информация доступна на странице релиза GitHub.",
            ["DepotUpdateRelease"] = "Открыть релиз на GitHub",
            ["DepotUpdateAction"] = "OK скачает и установит обновление. При отказе напоминание появится при следующем запуске.",
            ["DepotUpdateIgnore"] = "Пропустить",
            ["DepotUpdateSuccess"] = "DepotDownloader успешно обновлён.",
            ["WelcomeLog"] = "Добро пожаловать в EternalDownpatcher.",
            ["SelectVersionLog"] = "Выберите версию вручную.",
            ["SelectRootFolderLog"] = "Выберите папку DOOM Eternal.",
            ["WorkingFolderLog"] = "Рабочая папка: {0}",
            ["DefaultPatchFolderLog"] = "Папка патч-файлов по умолчанию: {0}",
            ["LoadedVersionsLog"] = "Загружено версий: {0}",
            ["VersionsSourceLog"] = "Источник версий: {0}",
            ["CheckingVersionData"] = "Фоновая проверка версий игры на GitHub...",
            ["VersionDataReady"] = "Версии игры и списки файлов актуальны.",
            ["VersionDataCancelled"] = "Отмена или истекло время ожидания.",
            ["VersionDataFallback"] = "Не удалось обновить данные игры; остаются локальные данные. {0}",
            ["VersionLog"] = "Версия EternalDownpatcher: {0}"
        };
        Dictionary<string, string> es = new()
        {
            ["SelectGameFolder"] = "Seleccionar carpeta de DOOM Eternal",
            ["GameVersion"] = "Versión del juego *",
            ["SteamUsername"] = "Usuario de Steam *",
            ["SteamPassword"] = "Contraseña de Steam *",
            ["RememberLogin"] = "Recordar el login de Steam",
            ["RememberPassword"] = "Recordar la contraseña de Steam",
            ["RememberPasswordWarningTitle"] = "¿Guardar la contraseña de Steam?",
            ["RememberPasswordWarning"] = "La contraseña de Steam se guardará en este equipo, cifrada para su cuenta de Windows. Los programas que se ejecuten con esa misma cuenta podrían acceder a ella.\n\nÚselo solo en un equipo personal de confianza. Desmarcar la casilla y pulsar OK elimina la contraseña guardada por EternalDownpatcher. DepotDownloader puede guardar datos de la sesión de Steam por separado.\n\n¿Activar el guardado de la contraseña?",
            ["CredentialSaveFailed"] = "No se pudieron actualizar los ajustes de inicio de sesión. Los datos de acceso guardados anteriormente podrían permanecer en el archivo de configuración.",
            ["SteamGuardPrompt"] = "Introduzca el código de Steam Guard del correo o de la aplicación de autenticación. El código no se guardará.",
            ["SteamGuardConfirm"] = "Continuar",
            ["SteamGuardCancel"] = "Cancelar",
            ["DownloadAllFiles"] = "Descargar todo",
            ["PatchExistingMode"] = "Aplicar parche",
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
            ["UnknownVersionRecovery"] = "No se reconoce esta instalación. Tamaño del EXE: {0} bytes. Última versión de origen compatible: {1}.\n\nNo se puede aplicar un parche selectivo. Puede descargar una versión antigua completa en otra carpeta. La instalación actual no cambiará.\n\n¿Activar Descargar todos los archivos?",
            ["BackupRestored"] = "Copia de seguridad restaurada correctamente.",
            ["WaitBeforeClosing"] = "Espere a que termine la preparación o la cancelación y restauración antes de cerrar la aplicación.",
            ["LegacyBackupWarning"] = "Esta copia es antigua. Se restauraron los archivos originales, pero no se pueden identificar ni eliminar automáticamente los archivos añadidos por el parcheador antiguo.",
            ["DepotFailed"] = "DepotDownloader falló. Código de salida: {0}",
            ["OutputFolderEmptyAfterDownload"] = "DepotDownloader terminó, pero la carpeta de salida está vacía.",
            ["DownloadedExecutableMissing"] = "No se descargó el ejecutable seleccionado. La instalación no cambió.",
            ["DownloadedExecutableMismatch"] = "El ejecutable descargado no coincide con la versión seleccionada. La instalación no cambió.",
            ["SourceVersionChanged"] = "La versión instalada cambió durante la descarga. No se aplicó el parche. Seleccione de nuevo la carpeta del juego antes de reintentar.",
            ["DownloadFolderDoesNotExistCreate"] = "La carpeta de descarga no existe.\n\n¿Crearla ahora?",
            ["PatchFolderDoesNotExistCreate"] = "La carpeta de archivos del parche no existe.\n\n¿Crearla ahora?",
            ["CreateDownloadFolder"] = "Crear carpeta de descarga",
            ["CreatePatchFolder"] = "Crear carpeta de archivos del parche",
            ["DepotDownloaderMissing"] = "Falta DepotDownloader.exe. Reinicia EternalDownpatcher para instalarlo antes de abrir la ventana principal.",
            ["DepotSetupTitle"] = "Preparando DepotDownloader",
            ["DepotDownloading"] = "Descargando DepotDownloader de GitHub...",
            ["DepotInstalling"] = "Instalando DepotDownloader...",
            ["DepotCancelling"] = "Cancelando. Espera...",
            ["DepotCheckingUpdates"] = "Buscando actualizaciones de DepotDownloader en GitHub...",
            ["DepotUpdateCheckFailed"] = "No se pudieron buscar actualizaciones de DepotDownloader: {0}. Se mantiene la versión instalada.",
            ["DepotAlreadyUpdated"] = "DepotDownloader está actualizado.",
            ["DepotCheckTimeout"] = "se agotó el tiempo de espera",
            ["DepotUpdateTitle"] = "Actualización de DepotDownloader disponible",
            ["DepotUpdateDetails"] = "Instalada: {0}\nDisponible: {1}\nPublicación: {2}",
            ["DepotUnknownVersion"] = "Desconocida",
            ["DepotUpdateNoNotes"] = "Las notas no están disponibles aquí. Abre la versión en GitHub para ver todos los detalles.",
            ["DepotUpdateRelease"] = "Ver versión en GitHub",
            ["DepotUpdateAction"] = "OK descarga e instala la actualización. Si la omites, se recordará en el próximo inicio.",
            ["DepotUpdateIgnore"] = "Omitir",
            ["DepotUpdateSuccess"] = "DepotDownloader se actualizó correctamente.",
            ["WelcomeLog"] = "Bienvenido a EternalDownpatcher.",
            ["SelectVersionLog"] = "Selecciona una versión manualmente.",
            ["SelectRootFolderLog"] = "Selecciona la carpeta de DOOM Eternal.",
            ["WorkingFolderLog"] = "Carpeta de trabajo: {0}",
            ["DefaultPatchFolderLog"] = "Carpeta predeterminada de archivos del parche: {0}",
            ["LoadedVersionsLog"] = "Versiones cargadas: {0}",
            ["VersionsSourceLog"] = "Fuente de versiones: {0}",
            ["CheckingVersionData"] = "Comprobando versiones del juego en GitHub en segundo plano...",
            ["VersionDataReady"] = "Las versiones del juego y las listas de archivos están actualizadas.",
            ["VersionDataCancelled"] = "Cancelado o se agotó el tiempo de espera.",
            ["VersionDataFallback"] = "No se pudieron actualizar los datos del juego; se conservan los datos locales. {0}",
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

    #endregion
}
