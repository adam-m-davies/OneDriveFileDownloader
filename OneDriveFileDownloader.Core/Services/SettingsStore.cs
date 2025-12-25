using System;
using System.IO;
using System.Text.Json;

namespace OneDriveFileDownloader.Core.Services
{
	public enum UxOption { Minimal = 1, Dashboard = 2, Explorer = 3 }

	public class Settings
	{
		public string LastClientId { get; set; } = string.Empty;
		public string LastDownloadFolder { get; set; } = string.Empty;
		public UxOption SelectedUx { get; set; } = UxOption.Minimal;
	}

	public static class SettingsStore
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

		public static void Save(Settings settings)
		{
			try
			{
				Directory.CreateDirectory(AppFolder);
				var txt = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
				File.WriteAllText(FilePath, txt);
			}
			catch
			{
				// ignore write failures
			}
		}

		public static void SaveLastClientId(string clientId)
		{
			try
			{
				var settings = Load();
				settings.LastClientId = clientId;
				Save(settings);
			}
			catch
			{
				// ignore write failures
			}
		}
	}
}
