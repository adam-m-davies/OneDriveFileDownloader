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
		// Tests can override this path to avoid clobbering a shared settings file during parallel test runs
		internal static string? TestFilePathOverride { get; set; }

		private static string GetFilePath() => TestFilePathOverride ?? FilePath;

		public static Settings Load()
		{
			try
			{
				var fp = GetFilePath();
				if (!File.Exists(fp)) return new Settings();
				var txt = File.ReadAllText(fp);
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
				var fp = GetFilePath();
				Directory.CreateDirectory(Path.GetDirectoryName(fp) ?? AppFolder);
				var txt = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
				File.WriteAllText(fp, txt);
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

		// Testing helper: try to save and return whether the file was written
		internal static bool TrySave(Settings settings)
		{
			Save(settings);
			var fp = GetFilePath();
			return File.Exists(fp);
		}
	}
}
