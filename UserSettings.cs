using System;
using System.IO;
using System.Text.Json;

namespace EternalDownpatcher.WinForms;

public sealed class UserSettings
{
    public bool DarkMode { get; set; }
}

public static class UserSettingsStore
{
    private static readonly string FolderPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "EternalDownpatcher");

    private static readonly string FilePath = Path.Combine(FolderPath, "settings.json");

    public static UserSettings Load()
    {
        try
        {
            if (!File.Exists(FilePath))
                return new UserSettings();

            string json = File.ReadAllText(FilePath);
            return JsonSerializer.Deserialize<UserSettings>(json) ?? new UserSettings();
        }
        catch
        {
            return new UserSettings();
        }
    }

    public static void Save(UserSettings settings)
    {
        try
        {
            Directory.CreateDirectory(FolderPath);

            string json = JsonSerializer.Serialize(settings, new JsonSerializerOptions
            {
                WriteIndented = true
            });

            File.WriteAllText(FilePath, json);
        }
        catch
        {
        }
    }
}