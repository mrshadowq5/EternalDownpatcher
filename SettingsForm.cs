using System.Drawing;
using System.Windows.Forms;

namespace EternalDownpatcher.WinForms;

public sealed class SettingsForm : Form
{
    public SettingsForm()
    {
        Text = "EternalDownpatcher | Settings";
        StartPosition = FormStartPosition.CenterParent;
        Size = new Size(520, 360);
        Font = new Font("Segoe UI", 9F);

        Label label = new()
        {
            Dock = DockStyle.Fill,
            Text = "Settings will be added later.",
            TextAlign = ContentAlignment.MiddleCenter
        };

        Controls.Add(label);
    }
}