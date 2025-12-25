using System.IO;
using OneDriveFileDownloader.Core.Services;
using Xunit;

namespace OneDriveFileDownloader.Tests
{
    [Collection("SettingsStore")]
    public class SettingsWindowTests
    {
        [Fact]
        public void SettingsStore_SaveAndLoad_PersistsSettings()
        {
            // use a temp file for settings during this test to avoid colliding with other tests
            var temp = Path.Combine(Path.GetTempPath(), System.Guid.NewGuid().ToString() + "-odfd-settings.json");
            OneDriveFileDownloader.Core.Services.SettingsStore.TestFilePathOverride = temp;

            // simulate settings changes and save
            var s1 = new OneDriveFileDownloader.Core.Services.Settings { LastClientId = "test-client", LastDownloadFolder = Path.GetTempPath() };
            Assert.True(OneDriveFileDownloader.Core.Services.SettingsStore.TrySave(s1));
			// verify file was written and contains expected client id
			Assert.True(File.Exists(temp), "Settings file was not written to temp path");
			var txt = File.ReadAllText(temp);
			Assert.Contains("test-client", txt);

			var s = OneDriveFileDownloader.Core.Services.SettingsStore.Load();
            Assert.Equal("test-client", s.LastClientId);
            Assert.Equal(Path.GetTempPath(), s.LastDownloadFolder);

			// cleanup
			if (File.Exists(temp)) File.Delete(temp);
			OneDriveFileDownloader.Core.Services.SettingsStore.TestFilePathOverride = null;
        }
    }
}