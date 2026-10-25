using System;
using System.IO;
using System.Text.Json;

namespace EternalDownpatcher.WinForms;

#region Settings and storage

public sealed class UserSettings
{
    public bool CreateBackup { get; set; } = true;
    public bool DarkMode { get; set; }
    public string Language { get; set; } = "en";
}

public static class UserSettingsStore
{
    private static readonly string FolderPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "EternalDownpatcher");

    private static readonly string FilePath = Path.Combine(FolderPath, "settings.json");

    public static UserSettings Load() => Load(FilePath);

    internal static UserSettings Load(string filePath)
    {
        UserSettings settings;
        bool hasSavedCredentials;
        try
        {
            if (!File.Exists(filePath))
                return new UserSettings();

            string json = File.ReadAllText(filePath);
            using JsonDocument document = JsonDocument.Parse(json);
            settings = document.RootElement.Deserialize<UserSettings>() ?? new UserSettings();
            hasSavedCredentials = document.RootElement.ValueKind == JsonValueKind.Object
                && document.RootElement.EnumerateObject().Any(property => property.Name is
                    "RememberLogin" or "SteamLogin" or "RememberPassword" or "SteamPassword");
        }
        catch
        {
            return new UserSettings();
        }

        // Rewrite old settings without ever decrypting or restoring their credentials.
        if (hasSavedCredentials && !Save(settings, filePath))
            throw new IOException("Could not remove saved Steam sign-in data from the settings file. "
                + "Check that the file is writable and restart EternalDownpatcher.");
        return settings;
    }

    public static bool Save(UserSettings settings) => Save(settings, FilePath);

    internal static bool Save(UserSettings settings, string filePath)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(filePath))!);

            string json = JsonSerializer.Serialize(settings, new JsonSerializerOptions
            {
                WriteIndented = true
            });

            File.WriteAllText(filePath, json);
            return true;
        }
        catch
        {
            return false;
        }
    }
}

#endregion
