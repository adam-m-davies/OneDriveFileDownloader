using System.IO;
using OneDriveFileDownloader.Core.Services;
using Xunit;

namespace OneDriveFileDownloader.Tests
{
    public class SettingsWindowTests
    {
        [Fact]
        public void SettingsStore_SaveAndLoad_PersistsSettings()
        {
            // cleanup existing settings
            var s0 = SettingsStore.Load();
            s0.LastClientId = string.Empty;
            s0.LastDownloadFolder = string.Empty;
            SettingsStore.Save(s0);

            // simulate settings changes and save
            var s1 = new OneDriveFileDownloader.Core.Services.Settings { LastClientId = "test-client", LastDownloadFolder = Path.GetTempPath() };
            SettingsStore.Save(s1);

            var s = SettingsStore.Load();
            Assert.Equal("test-client", s.LastClientId);
            Assert.Equal(Path.GetTempPath(), s.LastDownloadFolder);
        }
    }
}