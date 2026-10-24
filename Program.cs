namespace EternalDownpatcher.WinForms;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        ApplicationConfiguration.Initialize();
        UserSettings settings = UserSettingsStore.Load();
        try
        {
            // The main window is created only after the required downloader is ready.
            if (!DepotDownloaderStartup.Prepare(MainForm.GetAppWorkingFolder(), settings)) return;
        }
        catch (Exception error)
        {
            MessageBox.Show(error.Message, "EternalDownpatcher", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }
        Application.Run(new MainForm(settings, UserSettingsStore.Save));
    }
}
