using System.Drawing;

namespace EternalDownpatcher.WinForms;

internal sealed class SteamGuardForm : Form
{
    private readonly TextBox _codeTextBox = new();

    public string Code => _codeTextBox.Text.Trim();

    public SteamGuardForm(bool dark, string prompt, string confirmText, string cancelText)
    {
        Text = "Steam Guard";
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        ClientSize = new Size(420, 164);
        Font = new Font("Segoe UI", 9F);
        BackColor = dark ? Color.FromArgb(21, 23, 28) : Color.FromArgb(245, 247, 250);
        ForeColor = dark ? Color.FromArgb(235, 238, 245) : Color.FromArgb(25, 28, 33);

        Label message = new() { Text = prompt, ForeColor = ForeColor };
        message.SetBounds(12, 12, 396, 58);
        _codeTextBox.SetBounds(12, 76, 396, 25);
        _codeTextBox.UseSystemPasswordChar = true;
        _codeTextBox.BackColor = dark ? Color.FromArgb(32, 36, 43) : Color.White;
        _codeTextBox.ForeColor = ForeColor;
        Button confirm = new() { Text = confirmText };
        confirm.SetBounds(202, 120, 100, 28);
        confirm.Click += (_, _) =>
        {
            if (Code.Length > 0)
                DialogResult = DialogResult.OK;
        };
        Button cancel = new() { Text = cancelText, DialogResult = DialogResult.Cancel };
        cancel.SetBounds(308, 120, 100, 28);
        foreach (Button button in new[] { confirm, cancel })
        {
            button.BackColor = dark ? Color.FromArgb(42, 47, 56) : Color.FromArgb(238, 241, 246);
            button.ForeColor = ForeColor;
            button.FlatStyle = FlatStyle.Flat;
        }
        AcceptButton = confirm;
        CancelButton = cancel;
        Controls.AddRange([message, _codeTextBox, confirm, cancel]);
        Shown += (_, _) => _codeTextBox.Focus();
    }
}
