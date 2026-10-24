using System.Reflection;
using EternalDownpatcher.WinForms;

internal static class RestoreChecks
{
    private static int checks;
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private static void Check(bool condition, string message) { if (!condition) throw new Exception(message); checks++; }
    private static T Field<T>(MainForm form, string name) => (T)typeof(MainForm).GetField(name, Private)!.GetValue(form)!;
    private static object? Call(MainForm form, string name, params object?[] args) => typeof(MainForm).GetMethod(name, Private)!.Invoke(form, args);
    private static bool Wait(Task<bool> task)
    {
        var timeout = System.Diagnostics.Stopwatch.StartNew();
        while (!task.IsCompleted)
        {
            if (timeout.Elapsed > TimeSpan.FromSeconds(30)) throw new TimeoutException("Restore check stalled.");
            Application.DoEvents(); Thread.Sleep(1);
        }
        bool result = task.GetAwaiter().GetResult(); Application.DoEvents(); return result;
    }
    private sealed class InlineProgress(Action<PatchBackup.RestoreProgress> report) : IProgress<PatchBackup.RestoreProgress>
    {
        public void Report(PatchBackup.RestoreProgress value) => report(value);
    }
    private static string MakeBackup(string scenario, int files)
    {
        string game = Path.Combine(scenario, "game"), source = Path.Combine(scenario, "source"), backup = Path.Combine(game, "backup");
        Directory.CreateDirectory(game); Directory.CreateDirectory(source);
        File.WriteAllText(Path.Combine(game, "DOOMEternalx64vk.exe"), "original");
        File.WriteAllText(Path.Combine(source, "DOOMEternalx64vk.exe"), "patched");
        File.WriteAllText(Path.Combine(game, "user.txt"), "keep");
        Directory.CreateDirectory(Path.Combine(source, "created"));
        File.WriteAllText(Path.Combine(source, "created", "new.txt"), "added");
        for (int index = 0; index < files; index++)
        {
            string file = index.ToString("D4") + ".dat";
            File.WriteAllText(Path.Combine(game, file), "original-" + index);
            File.WriteAllText(Path.Combine(source, file), "patched-" + index);
        }
        PatchBackup.Apply(source, game, backup, CancellationToken.None);
        return backup;
    }
    internal static int Run(string root, MainForm form, string[] args)
    {
        Exception? failure = null;
        int result = 0;
        _ = form.Handle;
        form.BeginInvoke((Action)(() =>
        {
            try { result = RunCore(root, form, args); }
            catch (Exception error) { failure = error; }
            finally { Application.ExitThread(); }
        }));
        // Use a real main message loop so WinForms retains its UI synchronization
        // context between asynchronous restore stages and nested DoEvents checks.
        Application.Run();
        if (failure is not null) System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(failure).Throw();
        return result;
    }
    private static int RunCore(string root, MainForm form, string[] args)
    {
        Control.CheckForIllegalCrossThreadCalls = true;
        // A real WinForms message loop needs an open form. Keep this test window
        // off-screen and suppress its startup network checks, as in UI previews.
        var events = (System.ComponentModel.EventHandlerList)typeof(Control)
            .GetProperty("Events", Private)!.GetValue(form)!;
        object shownKey = typeof(Form).GetFields(BindingFlags.Static | BindingFlags.NonPublic)
            .Single(field => field.Name.Contains("Shown", StringComparison.OrdinalIgnoreCase)).GetValue(null)!;
        Delegate? shown = events[shownKey];
        events.RemoveHandler(shownKey, shown);
        form.ShowInTaskbar = false; form.StartPosition = FormStartPosition.Manual;
        form.Location = new Point(-30000, -30000); form.Opacity = 0;
        try { form.Show(); Application.DoEvents(); }
        finally { events.AddHandler(shownKey, shown); }
        string work = Path.Combine(root, "restore"); Directory.CreateDirectory(work);
        string backup = MakeBackup(Path.Combine(work, "async"), 2), game = Path.GetDirectoryName(backup)!;
        int uiThread = Environment.CurrentManagedThreadId, workerThread = uiThread;
        using var reached = new ManualResetEventSlim(); using var release = new ManualResetEventSlim();
        var reports = new List<PatchBackup.RestoreProgress>();
        Task<bool>? restoration = null;
        try
        {
            restoration = PatchBackup.RestoreAsync(backup, game, new InlineProgress(update =>
            {
                workerThread = Environment.CurrentManagedThreadId; reports.Add(update);
                if (reports.Count == 1)
                {
                    reached.Set();
                    if (!release.Wait(TimeSpan.FromSeconds(10))) throw new TimeoutException("UI did not resume blocked restore.");
                }
            }));
            Check(reached.Wait(TimeSpan.FromSeconds(5)), "Background restore never began.");
            Check(!restoration.IsCompleted && workerThread != uiThread, "Restoration blocked the calling UI thread.");
            bool heartbeat = false;
            form.BeginInvoke((Action)(() => heartbeat = Environment.CurrentManagedThreadId == uiThread));
            var heartbeatWait = System.Diagnostics.Stopwatch.StartNew();
            while (!heartbeat && heartbeatWait.Elapsed < TimeSpan.FromSeconds(2))
            {
                Application.DoEvents(); Thread.Sleep(1);
            }
            Check(heartbeat, "The UI message pump cannot run during restoration.");
        }
        finally { release.Set(); if (restoration is not null) Wait(restoration); }
        Check(Wait(restoration!), "New-format backup reported legacy restoration.");
        Check(File.ReadAllText(Path.Combine(game, "DOOMEternalx64vk.exe")) == "original", "Original EXE was not restored.");
        Check(File.ReadAllText(Path.Combine(game, "0001.dat")) == "original-1", "Original payload was not restored.");
        Check(!Directory.Exists(Path.Combine(game, "created")) && File.ReadAllText(Path.Combine(game, "user.txt")) == "keep", "Restore removed user files or retained added files.");
        Check(reports.First().CompletedFiles == 0 && reports.Last().CompletedFiles == 4 && reports.All(p => p.TotalFiles == 4), "Restore file counts are incomplete.");
        Check(reports.Any(p => p.CurrentFile == "DOOMEternalx64vk.exe") && File.Exists(Path.Combine(backup, "files", "DOOMEternalx64vk.exe")), "Restore omitted the current file or consumed its backup.");
        Check(Wait(PatchBackup.RestoreAsync(backup, game)), "A completed restore cannot be repeated safely.");

        string legacy = Path.Combine(work, "legacy"), legacyGame = Path.Combine(work, "legacy-game");
        Directory.CreateDirectory(legacy); Directory.CreateDirectory(legacyGame);
        File.WriteAllText(Path.Combine(legacy, "old.dat"), "legacy-original");
        File.WriteAllText(Path.Combine(legacyGame, "old.dat"), "changed");
        var legacyReports = new List<PatchBackup.RestoreProgress>();
        Check(!Wait(PatchBackup.RestoreAsync(legacy, legacyGame, new InlineProgress(legacyReports.Add))), "Legacy backup lost its legacy warning.");
        Check(File.ReadAllText(Path.Combine(legacyGame, "old.dat")) == "legacy-original" && legacyReports.Last().CompletedFiles == 1, "Legacy restore or progress failed.");

        string badBackup = MakeBackup(Path.Combine(work, "invalid"), 1), badGame = Path.GetDirectoryName(badBackup)!;
        File.Delete(Path.Combine(badBackup, "files", "DOOMEternalx64vk.exe"));
        bool failed = false;
        try { Wait(PatchBackup.RestoreAsync(badBackup, badGame)); } catch (FileNotFoundException) { failed = true; }
        Check(failed && File.ReadAllText(Path.Combine(badGame, "0000.dat")) == "patched-0", "Incomplete backup modified the game before validation finished.");

        string uiBackup = MakeBackup(Path.Combine(work, "ui"), 256), uiGame = Path.GetDirectoryName(uiBackup)!;
        string pending = Path.Combine(uiGame, ".DepotDownloader", "eternaldownpatcher-pending.json");
        Directory.CreateDirectory(Path.GetDirectoryName(pending)!); File.WriteAllText(pending, "{}");
        bool uiTickDuringRestore = false;
        form.BeginInvoke((Action)(() => uiTickDuringRestore = Field<bool>(form, "_isRestoringBackup")));
        Task<bool> uiRestore = (Task<bool>)Call(form, "RestoreBackupFilesAsync", uiBackup, uiGame)!;
        Check(Field<bool>(form, "_isRunning") && Field<bool>(form, "_isRestoringBackup"), "Restore does not enter the running UI state before yielding.");
        Check(!Field<Button>(form, "_startButton").Enabled && !Field<Button>(form, "_settingsButton").Enabled &&
            !Field<Button>(form, "_restoreBackupButton").Enabled && !Field<Button>(form, "_cancelButton").Enabled, "Concurrent operations remain possible during restore.");
        Task<bool> duplicate = (Task<bool>)Call(form, "RestoreBackupFilesAsync", uiBackup, uiGame)!;
        bool duplicateBlocked = false;
        try { Wait(duplicate); } catch (InvalidOperationException) { duplicateBlocked = true; }
        Check(duplicateBlocked, "A second restore was allowed while one was running.");
        Check(Wait(uiRestore) && uiTickDuringRestore, "The form could not process UI messages during restoration.");
        Check(!Field<bool>(form, "_isRunning") && !Field<bool>(form, "_isRestoringBackup") && Field<Button>(form, "_settingsButton").Enabled, "Successful restore did not release UI controls.");
        Check(!File.Exists(pending) && File.ReadAllText(Path.Combine(uiGame, "DOOMEternalx64vk.exe")) == "original", "Successful UI restore did not clear its interrupted state.");
        Check(Field<RichTextBox>(form, "_consoleBox").Text.Contains("DOOMEternalx64vk.exe"), "Restored file names are absent from the UI log.");
        failed = false;
        try { Wait((Task<bool>)Call(form, "RestoreBackupFilesAsync", badBackup, badGame)!); } catch (FileNotFoundException) { failed = true; }
        Check(failed && !Field<bool>(form, "_isRunning") && Field<Button>(form, "_restoreBackupButton").Enabled, "Restore error left the window locked.");

        // Force the ambiguous-size branch after the background restore. Strict
        // thread checks must stay enabled, and successful restore must clear pending.
        var versions = Field<System.Collections.IList>(form, "_versions");
        object[] originalVersions = versions.Cast<object>().ToArray();
        string ambiguousData = Path.Combine(work, "ambiguous-versions.json");
        File.WriteAllText(ambiguousData, System.Text.Json.JsonSerializer.Serialize(new { versions = new[] {
            new { name = "1.0", size = "8", manifestIds = Enumerable.Range(1, 8).Select(i => i.ToString()).ToArray() },
            new { name = "1.1", size = "8", manifestIds = Enumerable.Range(11, 8).Select(i => i.ToString()).ToArray() } } }));
        var parsed = (System.Collections.IList)typeof(MainForm).GetMethod("ReadVersionsJson", BindingFlags.Static | BindingFlags.NonPublic)!
            .Invoke(null, [ambiguousData, false])!;
        versions.Clear(); foreach (object version in parsed) versions.Add(version);
        try
        {
            string ambiguousBackup = MakeBackup(Path.Combine(work, "ambiguous"), 1), ambiguousGame = Path.GetDirectoryName(ambiguousBackup)!;
            string ambiguousPending = Path.Combine(ambiguousGame, ".DepotDownloader", "eternaldownpatcher-pending.json");
            Directory.CreateDirectory(Path.GetDirectoryName(ambiguousPending)!);
            File.WriteAllText(ambiguousPending, System.Text.Json.JsonSerializer.Serialize(new { FormatVersion = 1,
                ManifestIds = Enumerable.Range(1, 8).Select(i => i.ToString()).ToArray() }));
            Check(Wait((Task<bool>)Call(form, "RestoreBackupFilesAsync", ambiguousBackup, ambiguousGame)!),
                "Ambiguous restored version caused a worker-thread UI exception.");
            Check(File.ReadAllText(Path.Combine(ambiguousGame, "DOOMEternalx64vk.exe")) == "original" && !File.Exists(ambiguousPending),
                "Ambiguous restored version retained interrupted state or wrong bytes.");
            Check(!Field<bool>(form, "_isRunning") && !Field<bool>(form, "_isRestoringBackup"), "Ambiguous restore left UI locked.");
            Check(Field<RichTextBox>(form, "_consoleBox").Text.Contains(MainForm.T(Field<UserSettings>(form, "_userSettings").Language, "AmbiguousBuild")),
                "Ambiguous restore warning was not logged on the UI thread.");
        }
        finally { versions.Clear(); foreach (object version in originalVersions) versions.Add(version); }

        int previewIndex = Array.IndexOf(args, "--preview-dir");
        foreach (string language in new[] { "en", "ru", "es" })
        {
            foreach (string key in new[] { "RestorePreparing", "RestoreProgress", "RestoreCurrentFile", "RestoreCheckingVersion", "WaitForRestore" })
                Check(MainForm.T(language, key) != key, "Restore progress translation missing.");
            if (previewIndex < 0) continue;
            // Layout preview uses the same running state; only fixture data is shown.
            typeof(MainForm).GetField("_isRestoringBackup", Private)!.SetValue(form, true);
            Call(form, "SetRunningState", true);
            Field<ProgressBar>(form, "_restoreProgressBar").Visible = true;
            Field<Label>(form, "_gameFolderStatusLabel").Text = string.Format(MainForm.T(language, "RestoreProgress"), 15, 261);
            string preview = Path.Combine(args[previewIndex + 1], "restore-" + language + ".png");
            SpeedChecks.Render(form, preview);
            Field<ProgressBar>(form, "_restoreProgressBar").Visible = false;
            typeof(MainForm).GetField("_isRestoringBackup", Private)!.SetValue(form, false);
            Call(form, "SetRunningState", false); Call(form, "ApplyLanguage");
        }
        Console.WriteLine($"PASS: {checks} restore checks: worker execution, UI responsiveness, file progress, modern/legacy backup, validation and UI recovery after failure.");
        form.Hide();
        return checks;
    }
}
