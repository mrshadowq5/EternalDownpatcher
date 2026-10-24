using System.Collections;
using System.Reflection;
using EternalDownpatcher.WinForms;

internal static class SpeedChecks
{
    private static int checks;
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private static void Check(bool condition, string message) { if (!condition) throw new Exception(message); checks++; }
    private static T Field<T>(Form form, string name) => (T)form.GetType().GetField(name, Private)!.GetValue(form)!;
    private static void Set(MainForm form, string name, object? value) => typeof(MainForm).GetField(name, Private)!.SetValue(form, value);
    private static object? Call(Form form, string name, params object?[] arguments) => form.GetType().GetMethod(name, Private)!.Invoke(form, arguments);
    private static T Wait<T>(Task<T> task)
    {
        var timer = System.Diagnostics.Stopwatch.StartNew();
        while (!task.IsCompleted)
        {
            if (timer.Elapsed > TimeSpan.FromSeconds(30)) throw new TimeoutException("Download operation check stalled.");
            Application.DoEvents(); Thread.Sleep(1);
        }
        return task.GetAwaiter().GetResult();
    }
    private static string[] Manifests(object version) => (string[])version.GetType().GetProperty("ManifestIds")!.GetValue(version)!;
    private static void ExpectFailure(Action action, string message)
    {
        bool failed = false;
        try { action(); } catch (Exception error) when (error is IOException or InvalidOperationException or OperationCanceledException or ArgumentException) { failed = true; }
        Check(failed, message);
    }
    private static void SetBackupChoice(MainForm form, bool enabled)
    {
        using var settings = new SettingsForm(Field<UserSettings>(form, "_userSettings"));
        Call(settings, "ShowDownpatchPage");
        Field<CheckBox>(settings, "_createBackupCheckBox").Checked = enabled;
        Call(form, "ApplySettings", settings);
    }
    internal static void Render(Form form, string path)
    {
        // Show only off-screen with zero opacity; remove startup network handlers
        // while rendering the app's own controls for layout verification.
        var events = (System.ComponentModel.EventHandlerList)typeof(Control)
            .GetProperty("Events", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(form)!;
        var keyField = typeof(Form).GetFields(BindingFlags.Static | BindingFlags.NonPublic)
            .Single(field => field.Name.Contains("Shown", StringComparison.OrdinalIgnoreCase));
        object key = keyField.GetValue(null)!;
        Delegate? shown = events[key];
        events.RemoveHandler(key, shown);
        form.ShowInTaskbar = false; form.StartPosition = FormStartPosition.Manual;
        form.Location = new Point(-30000, -30000); form.Opacity = 0;
        try
        {
            form.Show(); Application.DoEvents();
            using var bitmap = new Bitmap(form.Width, form.Height);
            form.DrawToBitmap(bitmap, new Rectangle(Point.Empty, form.Size)); bitmap.Save(path);
        }
        finally { form.Hide(); events.AddHandler(key, shown); }
    }
    internal static int Run(string root, MainForm form, string[] args)
    {
        string work = Path.Combine(root, "speed"); Directory.CreateDirectory(work);
        IList versions = Field<IList>(form, "_versions");
        string[] ids = Manifests(versions[1]!);
        foreach (bool standalone in new[] { false, true })
        foreach (bool backup in new[] { false, true })
        {
            string scenario = Path.Combine(work, standalone + "-" + backup);
            string game = Path.Combine(scenario, "game"), output = Path.Combine(scenario, "downloads");
            Directory.CreateDirectory(game); Directory.CreateDirectory(output);
            File.WriteAllText(Path.Combine(game, "DOOMEternalx64vk.exe"), "original");
            File.WriteAllText(Path.Combine(game, "user.txt"), "keep");
            var plan = new GameDownloadPlan(standalone, backup, false, output, game, ids);
            int sourceChecks = 0, downloads = 0;
            var result = Wait(GameDownloadOperation.ExecuteAsync(plan, (folder, validate, token) =>
            {
                downloads++;
                Check(folder == (plan.Direct ? game : output), "Wrong Steam download destination.");
                Check(validate == plan.Direct, "Direct update must validate existing chunks.");
                Check(File.ReadAllText(Path.Combine(game, "DOOMEternalx64vk.exe")) == "original", "Original changed before download began.");
                File.WriteAllText(Path.Combine(folder, "DOOMEternalx64vk.exe"), "target");
                File.WriteAllText(Path.Combine(folder, "added.dat"), "new");
                return Task.CompletedTask;
            }, (folder, token) =>
            {
                Check(File.ReadAllText(Path.Combine(folder, "DOOMEternalx64vk.exe")) == "target", "Verification saw wrong data.");
                return Task.CompletedTask;
            }, () => sourceChecks++, _ => { }, _ => { }, CancellationToken.None));
            Check(downloads == 1, "Downloaded through multiple Steam sessions.");
            Check(File.ReadAllText(Path.Combine(game, "user.txt")) == "keep", "User file removed.");
            Check(sourceChecks == (!standalone && backup ? 1 : 0), "Source verification occurred at wrong stage.");
            Check((result.BackupFolder is not null) == (!standalone && backup), "Backup choice not respected.");
            if (standalone)
                Check(File.ReadAllText(Path.Combine(game, "DOOMEternalx64vk.exe")) == "original", "Standalone download changed installed game.");
            else
            {
                Check(File.ReadAllText(Path.Combine(game, "DOOMEternalx64vk.exe")) == "target", "Target game not updated.");
                Check(GameDownloadOperation.ReadPending(game) is null, "Successful update left pending state.");
                if (backup)
                {
                    Check(File.ReadAllText(Path.Combine(result.BackupFolder!, "files", "DOOMEternalx64vk.exe")) == "original", "Backup does not contain original bytes.");
                    PatchBackup.Restore(result.BackupFolder!, game);
                    Check(File.ReadAllText(Path.Combine(game, "DOOMEternalx64vk.exe")) == "original" && !File.Exists(Path.Combine(game, "added.dat")), "Backup restoration failed.");
                }
                else
                {
                    Check(!Directory.EnumerateFileSystemEntries(output).Any(), "Direct mode used or cleared the staging folder.");
                    Check(!Directory.EnumerateDirectories(game, "EternalDownpatcher_Backup_*").Any(), "Backup created while disabled.");
                }
            }
        }
        string interrupted = Path.Combine(work, "interrupted"); Directory.CreateDirectory(interrupted);
        File.WriteAllText(Path.Combine(interrupted, "DOOMEternalx64vk.exe"), "original");
        var directPlan = new GameDownloadPlan(false, false, false, Path.Combine(work, "untouched-output"), interrupted, ids);
        ExpectFailure(() => Wait(GameDownloadOperation.ExecuteAsync(directPlan, (folder, validate, token) =>
        {
            File.WriteAllText(Path.Combine(folder, "DOOMEternalx64vk.exe"), "partial");
            throw new OperationCanceledException();
        }, (_, _) => Task.CompletedTask, () => throw new Exception("Direct mode must not stage a backup."), _ => { }, _ => { }, CancellationToken.None)), "Cancelled update reported success.");
        Check(GameDownloadOperation.ReadPending(interrupted)!.SequenceEqual(ids), "Interrupted target was not preserved.");
        var other = directPlan with { ManifestIds = Manifests(versions[2]!) };
        ExpectFailure(() => Wait(GameDownloadOperation.ExecuteAsync(other, (_, _, _) => throw new Exception("Wrong target started."), (_, _) => Task.CompletedTask, () => { }, _ => { }, _ => { }, CancellationToken.None)), "Different target accepted while an update is pending.");
        var resumed = Wait(GameDownloadOperation.ExecuteAsync(directPlan, (folder, validate, token) =>
        {
            Check(validate && File.ReadAllText(Path.Combine(folder, "DOOMEternalx64vk.exe")) == "partial", "Resume lost existing data.");
            File.WriteAllText(Path.Combine(folder, "DOOMEternalx64vk.exe"), "finished");
            return Task.CompletedTask;
        }, (_, _) => Task.CompletedTask, () => { }, _ => { }, _ => { }, CancellationToken.None));
        Check(resumed.BackupFolder is null && GameDownloadOperation.ReadPending(interrupted) is null, "Resume created backup or left marker.");
        string stage = Path.Combine(work, "cancel-stage"); Directory.CreateDirectory(stage);
        string preserved = Path.Combine(work, "preserved"); Directory.CreateDirectory(preserved);
        File.WriteAllText(Path.Combine(preserved, "DOOMEternalx64vk.exe"), "original");
        var staged = new GameDownloadPlan(false, true, false, stage, preserved, ids);
        ExpectFailure(() => Wait(GameDownloadOperation.ExecuteAsync(staged, (folder, _, _) =>
        {
            File.WriteAllText(Path.Combine(folder, "DOOMEternalx64vk.exe"), "partial");
            throw new IOException("Synthetic download failure");
        }, (_, _) => Task.CompletedTask, () => { }, _ => { }, _ => { }, CancellationToken.None)), "Failed staged download reported success.");
        Check(File.ReadAllText(Path.Combine(preserved, "DOOMEternalx64vk.exe")) == "original", "Failed staged download modified game.");
        using var cancel = new CancellationTokenSource();
        ExpectFailure(() => Wait(GameDownloadOperation.ExecuteAsync(staged, (folder, _, _) =>
        {
            File.WriteAllText(Path.Combine(folder, "DOOMEternalx64vk.exe"), "target");
            File.WriteAllText(Path.Combine(folder, "z-added.dat"), "new");
            return Task.CompletedTask;
        }, (_, _) => Task.CompletedTask, () => { }, _ => { }, _ => cancel.Cancel(), cancel.Token)), "Cancelled apply reported success.");
        Check(File.ReadAllText(Path.Combine(preserved, "DOOMEternalx64vk.exe")) == "original" && !File.Exists(Path.Combine(preserved, "z-added.dat")), "Cancelled apply failed to restore originals.");
        var command = DepotDownloadCommand.Create(Path.Combine(work, "DepotDownloader.exe"), [782332], [ids[0]], "filelist.txt", true, "test-user", false, stage);
        string[] arguments = command.ArgumentList.ToArray();
        Check(arguments[Array.IndexOf(arguments, "-max-downloads") + 1] == "16", "Parallel download setting missing.");
        Check(arguments.Contains("-validate") && !arguments.Contains("-max-servers"), "Validation or supported CLI flags changed.");
        Check(!arguments.Contains("-password"), "Password put into process arguments.");
        Check(new UserSettings().CreateBackup, "Backup must default to enabled for old/new settings.");
        Check(System.Text.Json.JsonSerializer.Deserialize<UserSettings>("{}")!.CreateBackup, "Old settings did not enable backups.");
        string settingsFile = Path.Combine(work, "settings.json");
        var preference = new UserSettings { CreateBackup = false, Language = "ru", DarkMode = true, SteamLogin = "fixture-user" };
        Check(UserSettingsStore.Save(preference, settingsFile), "Could not persist backup preference.");
        var reloaded = UserSettingsStore.Load(settingsFile);
        Check(!reloaded.CreateBackup && reloaded.Language == "ru" && reloaded.DarkMode && reloaded.SteamLogin == "fixture-user", "Backup preference roundtrip lost other settings.");
        var invalidPlan = directPlan with { ManifestIds = ["1"] };
        ExpectFailure(() => Wait(GameDownloadOperation.ExecuteAsync(invalidPlan, (_, _, _) => throw new Exception("Invalid manifest started downloading."), (_, _) => Task.CompletedTask, () => { }, _ => { }, _ => { }, CancellationToken.None)), "Invalid target manifest list accepted.");
        var savedSettings = Field<UserSettings>(form, "_userSettings");
        using (var settings = new SettingsForm(savedSettings))
        {
            Call(settings, "ShowDownpatchPage");
            var backupBox = Field<CheckBox>(settings, "_createBackupCheckBox");
            Check(backupBox.Checked && backupBox.Parent == Field<Panel>(settings, "_contentPanel"), "Backup choice not on Settings/Downpatch page.");
            backupBox.Checked = false;
            Check(!settings.CreateBackup && savedSettings.CreateBackup, "Unconfirmed Settings change affected the running app.");
            Call(settings, "ShowSteamPage"); Call(settings, "ShowDownpatchPage");
            Check(!backupBox.IsDisposed && !backupBox.Checked, "Switching Settings pages lost the backup choice.");
            typeof(Button).GetMethod("OnClick", Private)!.Invoke(Field<Button>(settings, "_cancelButton"), [EventArgs.Empty]);
            Check(settings.DialogResult == DialogResult.Cancel && savedSettings.CreateBackup, "Cancel applied a backup choice.");
        }
        using (var reopened = new SettingsForm(savedSettings))
            Check(reopened.CreateBackup, "Reopening Settings kept a cancelled choice.");
        var validateBox = Field<CheckBox>(form, "_validateFilesCheckBox");
        var outputBox = Field<TextBox>(form, "_downpatchFolderTextBox");
        var fullMode = Field<RadioButton>(form, "_downloadAllFilesRadioButton");
        var patchMode = Field<RadioButton>(form, "_patchExistingRadioButton");
        var combo = Field<ComboBox>(form, "_versionComboBox");
        Check(savedSettings.CreateBackup && fullMode.Checked && !form.Controls.OfType<CheckBox>().Any(box => box.Text == MainForm.T(savedSettings.Language, "CreateBackup")), "Backup choice still appears on the main screen.");
        string uiGame = Path.Combine(work, "ui-game"); Directory.CreateDirectory(uiGame);
        File.WriteAllText(Path.Combine(uiGame, "DOOMEternalx64vk.exe"), "fixture");
        Set(form, "_doomRootFolder", uiGame); Set(form, "_installedVersion", versions[24]);
        patchMode.Checked = true;
        string stagingBefore = outputBox.Text;
        SetBackupChoice(form, false);
        Check(!savedSettings.CreateBackup, "Settings backup choice not applied.");
        Check(outputBox.Text == uiGame && outputBox.ReadOnly && outputBox.Enabled, "Direct destination is editable or wrong.");
        Check(validateBox.Checked && !validateBox.Enabled, "Direct reuse validation can be disabled.");
        Check(!combo.Items.Contains("Build 25216728"), "Direct mode offers current build as an older target.");
        string repair = (string)Call(form, "BuildGeneratedFileListPath", versions[1])!;
        Check(File.ReadAllLines(repair).Length == 101 && File.ReadAllLines(repair).All(line => line.StartsWith("regex:^")), "Direct update did not use its verified changed-file list.");
        fullMode.Checked = true;
        Check(outputBox.Enabled && outputBox.Text == stagingBefore && !savedSettings.CreateBackup, "Standalone switch lost output or backup choice.");
        patchMode.Checked = true;
        Call(form, "SetRunningState", true);
        Check(!Field<Button>(form, "_settingsButton").Enabled && !outputBox.Enabled && !Field<Button>(form, "_selectDownpatchFolderButton").Enabled, "Running operation exposes mutable controls.");
        Call(form, "SetRunningState", false);
        int previewIndex = Array.IndexOf(args, "--preview-dir");
        string? previews = previewIndex >= 0 ? Path.GetFullPath(args[previewIndex + 1]) : null;
        foreach (string language in new[] { "en", "ru", "es" })
        {
            Field<UserSettings>(form, "_userSettings").Language = language; Call(form, "ApplyLanguage");
            foreach (string key in new[] { "CreateBackup", "NoBackupConfirmation", "DirectInterrupted", "PendingDirectStatus", "DirectTargetCheck" })
                Check(MainForm.T(language, key) != key, "Missing backup UI translation.");
            string confirm = (string)Call(form, "GetDownloadConfirmation", versions[1], false, false)!;
            Check(confirm.Contains("1.1") && confirm.Contains(uiGame), "No-backup confirmation omits target/path.");
            using var preferences = new SettingsForm(savedSettings);
            Call(preferences, "ShowDownpatchPage");
            Check(Field<CheckBox>(preferences, "_createBackupCheckBox").Text == MainForm.T(language, "CreateBackup"), "Settings backup choice is not translated.");
            if (previews is not null)
            {
                Directory.CreateDirectory(previews); Call(form, "ApplyTheme", true);
                Render(form, Path.Combine(previews, "direct-" + language + ".png"));
                Render(preferences, Path.Combine(previews, "settings-" + language + ".png"));
            }
        }
        SetBackupChoice(form, true);
        Check(outputBox.Enabled && outputBox.Text == stagingBefore && validateBox.Enabled, "Backup mode failed to restore staging controls.");
        Check(!combo.Items.Contains("Build 25216728"), "Backup mode offers invalid same-build downgrade.");
        Field<UserSettings>(form, "_userSettings").Language = "ru"; Call(form, "ApplyLanguage");
        if (previews is not null)
        {
            Render(form, Path.Combine(previews, "backup-ru.png"));
        }
        // A synthetic interrupted update remains selectable even if its EXE is missing.
        var failedPlan = directPlan with { GameFolder = uiGame };
        ExpectFailure(() => Wait(GameDownloadOperation.ExecuteAsync(failedPlan, (folder, _, _) =>
        {
            File.Delete(Path.Combine(folder, "DOOMEternalx64vk.exe")); throw new IOException("Interrupted before EXE creation");
        }, (_, _) => Task.CompletedTask, () => { }, _ => { }, _ => { }, CancellationToken.None)), "Missing-EXE interruption reported success.");
        Set(form, "_installedVersion", null); SetBackupChoice(form, false); Call(form, "RefreshDownpatchVersions"); Call(form, "ApplyLanguage");
        Check((bool)Call(form, "IsUsableGameFolder", uiGame)!, "Interrupted game folder with missing EXE cannot resume.");
        Check(combo.Items.Count == 1 && combo.SelectedItem!.ToString() == "1.1", "Pending target not selected exclusively.");
        Check(Field<Label>(form, "_gameFolderStatusLabel").Text.Contains("1.1"), "Pending update is not visible to the user.");
        string resumeList = (string)Call(form, "BuildGeneratedFileListPath", versions[1])!;
        Check(File.ReadAllLines(resumeList).SequenceEqual(new[] { "regex:.*" }), "Resume used an incomplete delta.");
        Console.WriteLine($"PASS: {checks} speed/backup checks, including direct download, preserved user files, cancellation/resume, backup restoration, UI state and 16-way downloader arguments.");
        return checks;
    }
}