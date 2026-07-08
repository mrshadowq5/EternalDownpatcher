using System;
using System.Drawing;
using System.Windows.Forms;

namespace EternalDownpatcher.WinForms;

public sealed class HelpForm : Form
{
    private readonly RichTextBox helpTextBox;
    private readonly bool darkMode;

    public HelpForm() : this(false)
    {
    }

    public HelpForm(bool darkMode)
    {
        this.darkMode = darkMode;

        Text = "EternalDownpatcher | Help";
        StartPosition = FormStartPosition.CenterParent;
        Size = new Size(780, 660);
        MinimumSize = new Size(720, 620);
        Font = new Font("Segoe UI", 9F);

        Color windowBackColor = darkMode ? Color.FromArgb(24, 24, 27) : Color.FromArgb(245, 247, 250);
        Color cardBackColor = darkMode ? Color.FromArgb(32, 32, 36) : Color.White;
        Color textColor = darkMode ? Color.FromArgb(235, 235, 240) : Color.FromArgb(25, 28, 33);
        Color borderColor = darkMode ? Color.FromArgb(55, 55, 62) : Color.FromArgb(220, 225, 232);

        BackColor = windowBackColor;

        Panel outerPanel = new()
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(16),
            BackColor = windowBackColor
        };

        Panel cardPanel = new()
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(18),
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
                "Recommended if you don't have free space on disk/you don't care about skins and profile level."+

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

                "Use this if you want a separate copy of an older DOOM Eternal version.\r\n" +

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
                "EternalDownpatcher will create steam_appid.txt automatically."+ 

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

        cardPanel.Paint += (_, e) =>
        {
            using Pen borderPen = new(borderColor);
            Rectangle rect = new(0, 0, cardPanel.Width - 1, cardPanel.Height - 1);
            e.Graphics.DrawRectangle(borderPen, rect);
        };

        cardPanel.Controls.Add(helpTextBox);
        outerPanel.Controls.Add(cardPanel);
        Controls.Add(outerPanel);

        ApplyTextStyle();
    }

    private void ApplyTextStyle()
    {
        Color textColor = darkMode ? Color.FromArgb(235, 235, 240) : Color.FromArgb(25, 28, 33);
        Color backColor = darkMode ? Color.FromArgb(32, 32, 36) : Color.White;
        Color titleColor = darkMode ? Color.White : Color.FromArgb(20, 24, 31);
        Color accentColor = darkMode ? Color.FromArgb(255, 139, 96) : Color.FromArgb(183, 62, 32);

        helpTextBox.SelectAll();
        helpTextBox.SelectionFont = new Font("Segoe UI", 10F);
        helpTextBox.SelectionColor = textColor;
        helpTextBox.SelectionBackColor = backColor;

        StyleLine("EternalDownpatcher help. Thank you. I hope this helps. Thank you.", 12F, FontStyle.Bold, titleColor);
        StyleLine("EternalDownpatcher has two modes:", 10.5F, FontStyle.Bold, titleColor);

        StyleLine("METHOD 1: DOWNPATCHING ROOT DOOM ETERNAL (painful!)", 11F, FontStyle.Bold, accentColor);
        StyleLine("METHOD 2: DOWNLOAD ALL FILES (slow)", 11F, FontStyle.Bold, accentColor);
        StyleLine("DOWNPATCHED GAME PROBLEMS", 11F, FontStyle.Bold, accentColor);
        StyleLine("CONTACT", 11F, FontStyle.Italic, accentColor);

        StyleLine("Steps:", 10F, FontStyle.Bold, titleColor);
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

    private void StyleAllSeparators()
    {
        string separator = "------------------------------------------------------------";
        int start = helpTextBox.Text.IndexOf(separator, StringComparison.Ordinal);

        Color separatorColor = darkMode ? Color.FromArgb(90, 90, 100) : Color.FromArgb(200, 205, 214);

        while (start >= 0)
        {
            helpTextBox.Select(start, separator.Length);
            helpTextBox.SelectionFont = new Font("Consolas", 9F, FontStyle.Regular);
            helpTextBox.SelectionColor = separatorColor;

            start = helpTextBox.Text.IndexOf(separator, start + separator.Length, StringComparison.Ordinal);
        }
    }
}