using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace EternalDownpatcher.WinForms;

#region Settings and storage

public sealed class UserSettings
{
    public bool CreateBackup { get; set; } = true;
    public bool DarkMode { get; set; }
    public string Language { get; set; } = "en";
    public bool RememberLogin { get; set; } = true;
    public string SteamLogin { get; set; } = "";
    public bool RememberPassword { get; set; }
    // DPAPI-protected Base64, never a plaintext password.
    public string SteamPassword { get; set; } = "";
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
        try
        {
            if (!File.Exists(filePath))
                return new UserSettings();

            string json = File.ReadAllText(filePath);
            UserSettings settings = JsonSerializer.Deserialize<UserSettings>(json) ?? new UserSettings();
            bool changed = false;
            if (!settings.RememberLogin && !string.IsNullOrEmpty(settings.SteamLogin))
            {
                settings.SteamLogin = "";
                changed = true;
            }
            if (!settings.RememberPassword && !string.IsNullOrEmpty(settings.SteamPassword))
            {
                settings.SteamPassword = "";
                changed = true;
            }
            if (changed)
                Save(settings, filePath);
            return settings;
        }
        catch
        {
            return new UserSettings();
        }
    }

    public static bool Save(UserSettings settings) => Save(settings, FilePath);

    internal static bool Save(UserSettings settings, string filePath)
    {
        try
        {
            if (!settings.RememberLogin)
                settings.SteamLogin = "";
            if (!settings.RememberPassword)
                settings.SteamPassword = "";
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

#region Saved Steam credentials

internal static class SteamCredentialSettings
{
    public static void Capture(UserSettings settings, string username, string password, bool rememberLogin, bool rememberPassword)
    {
        string encrypted = rememberPassword && !string.IsNullOrEmpty(password)
            ? SteamPasswordProtection.Encrypt(password)
            : "";
        settings.SteamLogin = rememberLogin ? username.Trim() : "";
        settings.RememberLogin = rememberLogin;
        settings.RememberPassword = rememberPassword;
        settings.SteamPassword = encrypted;
    }
}

#endregion

#region Password protection

//Encrypt Steam password with Windows DPAPI
//Note that if if you have some virus shit on your c

internal static class SteamPasswordProtection
{
    public static string Encrypt(string password)
    {
        byte[] plainBytes = Encoding.UTF8.GetBytes(password);
        try
        {
            byte[] encryptedBytes = ProtectedData.Protect(
                plainBytes, null, DataProtectionScope.CurrentUser);
            return Convert.ToBase64String(encryptedBytes);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(plainBytes);
        }
    }

    public static string Decrypt(string? encryptedPassword)
    {
        if (string.IsNullOrEmpty(encryptedPassword))
            return "";

        try
        {
            byte[] encryptedBytes = Convert.FromBase64String(encryptedPassword);
            byte[] plainBytes = ProtectedData.Unprotect(
                encryptedBytes, null, DataProtectionScope.CurrentUser);
            try
            {
                return Encoding.UTF8.GetString(plainBytes);
            }
            finally
            {
                CryptographicOperations.ZeroMemory(plainBytes);
            }
        }
        catch (Exception error) when (error is FormatException or CryptographicException)
        {
            //an unreadable saved password leaves the field empty for re-entry
            return "";
        }
    }
}

#endregion
