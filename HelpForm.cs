using System;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace EternalDownpatcher.WinForms;

public sealed class HelpForm : Form
{
    private readonly RichTextBox helpTextBox;
    private readonly bool darkMode;

    private readonly Color windowBackColor;
    private readonly Color cardBackColor;
    private readonly Color headerBackColor;
    private readonly Color textColor;
    private readonly Color mutedTextColor;
    private readonly Color titleColor;
    private readonly Color accentColor;
    private readonly Color borderColor;
    private readonly Color buttonBackColor;
    private readonly Color buttonHoverColor;

    public HelpForm() : this(false)
    {
    }

    public HelpForm(bool darkMode)
    {
        this.darkMode = darkMode;

        windowBackColor = darkMode ? Color.FromArgb(18, 18, 21) : Color.FromArgb(243, 246, 250);
        cardBackColor = darkMode ? Color.FromArgb(30, 30, 35) : Color.White;
        headerBackColor = darkMode ? Color.FromArgb(36, 36, 42) : Color.FromArgb(250, 251, 253);
        textColor = darkMode ? Color.FromArgb(235, 235, 240) : Color.FromArgb(25, 28, 33);
        mutedTextColor = darkMode ? Color.FromArgb(170, 170, 180) : Color.FromArgb(95, 103, 115);
        titleColor = darkMode ? Color.White : Color.FromArgb(20, 24, 31);
        accentColor = darkMode ? Color.FromArgb(255, 139, 96) : Color.FromArgb(183, 62, 32);
        borderColor = darkMode ? Color.FromArgb(58, 58, 66) : Color.FromArgb(220, 225, 232);
        buttonBackColor = darkMode ? Color.FromArgb(45, 45, 52) : Color.FromArgb(238, 241, 246);
        buttonHoverColor = darkMode ? Color.FromArgb(58, 58, 66) : Color.FromArgb(226, 231, 238);

        Text = "EternalDownpatcher | Help";
        StartPosition = FormStartPosition.CenterParent;
        ClientSize = new Size(820, 680);
        MinimumSize = new Size(740, 620);
        Font = new Font("Segoe UI", 9F);
        BackColor = windowBackColor;
        KeyPreview = true;
        AutoScaleMode = AutoScaleMode.Dpi;

        KeyDown += (_, e) =>
        {
            if (e.KeyCode == Keys.Escape)
                Close();
        };

        RoundedPanel outerCard = new()
        {
            Dock = DockStyle.Fill,
            Margin = new Padding(0),
            Padding = new Padding(1),
            BackColor = borderColor,
            CornerRadius = 16
        };

        RoundedPanel innerCard = new()
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(0),
            BackColor = cardBackColor,
            CornerRadius = 15
        };

        Panel mainPadding = new()
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(18),
            BackColor = windowBackColor
        };

        Panel headerPanel = new()
        {
            Dock = DockStyle.Top,
            Height = 76,
            Padding = new Padding(22, 16, 16, 12),
            BackColor = headerBackColor
        };

        Label titleLabel = new()
        {
            AutoSize = true,
            Text = "EternalDownpatcher Help",
            ForeColor = titleColor,
            Font = new Font("Segoe UI Semibold", 13F, FontStyle.Bold),
            Location = new Point(22, 14)
        };

        Label subTitleLabel = new()
        {
            AutoSize = true,
            Text = "Read this before downpatching",
            ForeColor = mutedTextColor,
            Font = new Font("Segoe UI", 9F),
            Location = new Point(24, 43)
        };

        Button closeButton = new()
        {
            Anchor = AnchorStyles.Top | AnchorStyles.Right,
            Text = "Close",
            Width = 86,
            Height = 32,
            FlatStyle = FlatStyle.Flat,
            BackColor = buttonBackColor,
            ForeColor = textColor,
            Font = new Font("Segoe UI", 9F),
            Cursor = Cursors.Hand,
            Location = new Point(ClientSize.Width - 132, 20)
        };

        closeButton.FlatAppearance.BorderSize = 0;
        closeButton.MouseEnter += (_, _) => closeButton.BackColor = buttonHoverColor;
        closeButton.MouseLeave += (_, _) => closeButton.BackColor = buttonBackColor;
        closeButton.Click += (_, _) => Close();

        headerPanel.Resize += (_, _) =>
        {
            closeButton.Location = new Point(headerPanel.Width - closeButton.Width - 18, 22);
        };

        Panel textPaddingPanel = new()
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(22, 18, 18, 18),
            BackColor = cardBackColor
        };

        helpTextBox = new RichTextBox
        {
            Dock = DockStyle.Fill,
            BorderStyle = BorderStyle.None,
            ReadOnly = true,
            WordWrap = true,
            DetectUrls = true,
            ScrollBars = RichTextBoxScrollBars.Vertical,
            BackColor = cardBackColor,
            ForeColor = textColor,
            Font = new Font("Segoe UI", 10F),
            HideSelection = false,
            Text =
                "EternalDownpatcher help. Thank you. I hope this helps. Thank you.\r\n\r\n" +

                "EternalDownpatcher has two modes:\r\n\r\n" +

                "1. Downpatching original DOOM Eternal installation\r\n" +
                "   Downloads only the files needed to downgrade root DOOM Eternal folder.\r\n\r\n" +

                "2. Downpatched version of DOOM Eternal you can play separately\r\n" +
                "   Downloads a full standalone copy of the selected version into a separate folder.\r\n\r\n" +

                "------------------------------------------------------------\r\n\r\n" +

                "METHOD 1: DOWNPATCHING ROOT DOOM ETERNAL (painfull, not recommended)\r\n\r\n" +

                "Use this if you want to downgrade your existing DOOM Eternal installation.\r\n\r\n" +
                "Recommended if you don't have free space on disk/you don't care about skins and profile level.\r\n\r\n" +

                "Steps:\r\n\r\n" +

                "1. Make sure Download All Files is disabled!!!!!!!!!!!!!!!!!!!!\r\n\r\n" +

                "2. Select DOOM Eternal root folder.\r\n" +
                "   This folder must contain DOOMEternalx64vk.exe, of course.\r\n\r\n" +
                "   EternalDownpatcher will detect your current DOOM Eternal version.\r\n\r\n" +

                "3. Select the version you want to downpatch to.\r\n" +
                "   Only older versions can be selected in this mode.\r\n\r\n" +

                "4. Enter your Steam login and password.\r\n" +
                "   You may be asked to enter email verification code or Steam Guard confirmation. I hope you have set it up.\r\n\r\n" +

                "5. Select downpatch files folder.\r\n" +
                "   This is a folder where downloaded patch files will be stored.\r\n\r\n" +

                "7. Hit the Downpatch button.\r\n\r\n" +

                "8. Wait.\r\n\r\n" +

                "9. Go to your downpatch folder and copy all files over to your DOOM Eternal root folder.\r\n" +
                "  Old DOOM Eternal files from your root folder will be backed up!\r\n\r\n" +

                "------------------------------------------------------------\r\n\r\n" +

                "METHOD 2: DOWNLOAD ALL FILES (recommended) (slow)\r\n\r\n" +

                "Use this if you want a separate copy of an older DOOM Eternal version.\r\n\r\n" +

                "Steps:\r\n\r\n" +

                "1. Enable Download All Files!!!!!!!!\r\n\r\n" +

                "2. Select the version you want to download.\r\n\r\n" +

                "3. Enter your Steam login and password.\r\n" +
                "   You may be asked to enter email verification code or Steam Guard confirmation.\r\n\r\n" +

                "4. Select Download Folder.\r\n" +
                "   Use a separate empty folder. Do not select DOOM Eternal root folder.\r\n\r\n" +

                "5. Hit the Downpatch button.\r\n\r\n" +

                "6. Wait.\r\n\r\n" +

                "8. After download is complete, add DOOMEternalx64vk.exe as a Non-Steam Game in Steam.\r\n\r\n" +
                "EternalDownpatcher will create steam_appid.txt automatically.\r\n\r\n" +

                "------------------------------------------------------------\r\n\r\n" +

                "DOWNPATCHED GAME PROBLEMS\r\n\r\n" +

                "- Old versions of DOOM Eternal may have issues on newer NVIDIA drivers, such as lighting issues or barrels not exploding despite being shot.\r\n\r\n" +

                "- Driver version 516.94 is confirmed working:\r\n" +
                "  https://www.nvidia.com/download/driverResults.aspx/192967/en-us/\r\n\r\n" +

                "- If you are running on a GPU from NVIDIA 40 series or higher, it is not compatible with the drivers that those downpatched versions of DOOM Eternal require in order to work properly.\r\n\r\n" +

                "------------------------------------------------------------\r\n\r\n" +

                "CONTACT\r\n\r\n" +

                "Contact me on discord:\r\n" +
                "stonedealer05\r\n\r\n" +

                "Modern DOOM Speedruning discord:\r\n" +
                "https://discord.gg/dtDa9VZ\r\n"
        };

        helpTextBox.LinkClicked += (_, e) =>
        {
            try
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = e.LinkText,
                    UseShellExecute = true
                });
            }
            catch
            {
            }
        };

        textPaddingPanel.Controls.Add(helpTextBox);

        headerPanel.Controls.Add(titleLabel);
        headerPanel.Controls.Add(subTitleLabel);
        headerPanel.Controls.Add(closeButton);

        innerCard.Controls.Add(textPaddingPanel);
        innerCard.Controls.Add(headerPanel);
        outerCard.Controls.Add(innerCard);
        mainPadding.Controls.Add(outerCard);
        Controls.Add(mainPadding);

        ApplyTextStyle();
    }

    private void ApplyTextStyle()
    {
        helpTextBox.SelectAll();
        helpTextBox.SelectionFont = new Font("Segoe UI", 10F);
        helpTextBox.SelectionColor = textColor;
        helpTextBox.SelectionBackColor = cardBackColor;

        StyleLine("EternalDownpatcher help. Thank you. I hope this helps. Thank you.", 13F, FontStyle.Bold, titleColor);
        StyleLine("EternalDownpatcher has two modes:", 10.5F, FontStyle.Bold, titleColor);

        StyleLine("1. Downpatching original DOOM Eternal installation", 10.5F, FontStyle.Bold, titleColor);
        StyleLine("2. Downpatched version of DOOM Eternal you can play separately", 10.5F, FontStyle.Bold, titleColor);

        StyleLine("METHOD 1: DOWNPATCHING ROOT DOOM ETERNAL (painfull, not recommended)", 11.5F, FontStyle.Bold, accentColor);
        StyleLine("METHOD 2: DOWNLOAD ALL FILES (recommended) (slow)", 11.5F, FontStyle.Bold, accentColor);
        StyleLine("DOWNPATCHED GAME PROBLEMS", 11.5F, FontStyle.Bold, accentColor);
        StyleLine("CONTACT", 11.5F, FontStyle.Bold, accentColor);

        StyleLine("Steps:", 10.5F, FontStyle.Bold, titleColor);

        StyleNumberedLines();
        StyleWarnings();
        StyleAllSeparators();

        helpTextBox.SelectionStart = 0;
        helpTextBox.SelectionLength = 0;
    }

    private void StyleLine(string text, float size, FontStyle style, Color color)
    {
        int start = helpTextBox.Text.IndexOf(text, StringComparison.Ordinal);

        while (start >= 0)
        {
            helpTextBox.Select(start, text.Length);
            helpTextBox.SelectionFont = new Font("Segoe UI", size, style);
            helpTextBox.SelectionColor = color;

            start = helpTextBox.Text.IndexOf(text, start + text.Length, StringComparison.Ordinal);
        }
    }

    private void StyleNumberedLines()
    {
        string[] lines =
        [
            "1. Make sure Download All Files is disabled!!!!!!!!!!!!!!!!!!!!",
            "2. Select DOOM Eternal root folder.",
            "3. Select the version you want to downpatch to.",
            "4. Enter your Steam login and password.",
            "5. Select downpatch files folder.",
            "7. Hit the Downpatch button.",
            "8. Wait.",
            "9. Go to your downpatch folder and copy all files over to your DOOM Eternal root folder.",
            "1. Enable Download All Files!!!!!!!!",
            "2. Select the version you want to download.",
            "4. Select Download Folder.",
            "5. Hit the Downpatch button.",
            "6. Wait.",
            "8. After download is complete, add DOOMEternalx64vk.exe as a Non-Steam Game in Steam."
        ];

        foreach (string line in lines)
            StyleLine(line, 10F, FontStyle.Bold, textColor);
    }

    private void StyleWarnings()
    {
        string[] lines =
        [
            "- Old versions of DOOM Eternal may have issues on newer NVIDIA drivers, such as lighting issues or barrels not exploding despite being shot.",
            "- Driver version 516.94 is confirmed working:",
            "- If you are running on a GPU from NVIDIA 40 series or higher, it is not compatible with the drivers that those downpatched versions of DOOM Eternal require in order to work properly."
        ];

        foreach (string line in lines)
            StyleLine(line, 10F, FontStyle.Regular, darkMode ? Color.FromArgb(255, 196, 135) : Color.FromArgb(145, 80, 28));
    }

    private void StyleAllSeparators()
    {
        string separator = "------------------------------------------------------------";
        int start = helpTextBox.Text.IndexOf(separator, StringComparison.Ordinal);

        Color separatorColor = darkMode ? Color.FromArgb(82, 82, 94) : Color.FromArgb(198, 205, 216);

        while (start >= 0)
        {
            helpTextBox.Select(start, separator.Length);
            helpTextBox.SelectionFont = new Font("Consolas", 9F, FontStyle.Regular);
            helpTextBox.SelectionColor = separatorColor;

            start = helpTextBox.Text.IndexOf(separator, start + separator.Length, StringComparison.Ordinal);
        }
    }

private sealed class RoundedPanel : Panel
{
    private int cornerRadius = 14;

    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public int CornerRadius
    {
        get => cornerRadius;
        set
        {
            cornerRadius = value;
            Invalidate();
        }
    }

    public RoundedPanel()
    {
        DoubleBuffered = true;
        ResizeRedraw = true;
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);

        using GraphicsPath path = CreatePath(ClientRectangle, CornerRadius);
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;

        using SolidBrush brush = new(BackColor);
        e.Graphics.FillPath(brush, path);

        Region = new Region(path);
    }

    private static GraphicsPath CreatePath(Rectangle rectangle, int radius)
    {
        int diameter = radius * 2;
        Rectangle arc = new(rectangle.Location, new Size(diameter, diameter));
        GraphicsPath path = new();

        path.AddArc(arc, 180, 90);

        arc.X = rectangle.Right - diameter - 1;
        path.AddArc(arc, 270, 90);

        arc.Y = rectangle.Bottom - diameter - 1;
        path.AddArc(arc, 0, 90);

        arc.X = rectangle.Left;
        path.AddArc(arc, 90, 90);

        path.CloseFigure();
        return path;
        }
    }
}