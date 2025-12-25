using System;
using System.IO;
using System.Text.Json;

namespace OneDriveFileDownloader.Console.Services
{
    internal class Settings
    {
        public string? LastClientId { get; set; }
    }

    internal static class SettingsStore
    {
        private static readonly string AppFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "OneDriveFileDownloader");
        private static readonly string FilePath = Path.Combine(AppFolder, "settings.json");

        public static Settings Load()
        {
            try
            {
                if (!File.Exists(FilePath)) return new Settings();
                var txt = File.ReadAllText(FilePath);
                return JsonSerializer.Deserialize<Settings>(txt) ?? new Settings();
            }
            catch
            {
                return new Settings();
            }
        }

        public static void SaveLastClientId(string clientId)
        {
            try
            {
                Directory.CreateDirectory(AppFolder);
                var settings = new Settings { LastClientId = clientId };
                var txt = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(FilePath, txt);
            }
            catch
            {
                // ignore write failures
            }
        }
    }
}
