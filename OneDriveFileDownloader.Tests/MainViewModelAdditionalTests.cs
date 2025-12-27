using System.Threading.Tasks;
using OneDriveFileDownloader.Core.Services;
using OneDriveFileDownloader.UI.ViewModels;
using Xunit;

namespace OneDriveFileDownloader.Tests
{
    public class MainViewModelAdditionalTests
    {
        [Fact]
        public async Task ScanLastCommand_NoSharedItems_SetsStatus()
        {
            var svc = new FakeOneDriveService();
            var repo = new MainViewModelTests.FakeRepo();
            var vm = new OneDriveFileDownloader.UI.ViewModels.MainViewModel(svc, repo);

            vm.ScanLastCommand.Execute(null);
            await Task.Delay(200);

            Assert.Equal("No shared items available to scan.", vm.StatusText);
        }

        [Fact]
        public void SettingsStore_SaveSelectedUx_Persists()
        {
            var temp = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString() + ".json");
            SettingsStore.TestFilePathOverride = temp;
            try
            {
                var s = SettingsStore.Load();
                s.SelectedUx = UxOption.Dashboard;
                SettingsStore.Save(s);

                var s2 = SettingsStore.Load();
                Assert.Equal(UxOption.Dashboard, s2.SelectedUx);
            }
            finally
            {
                SettingsStore.TestFilePathOverride = null;
                if (File.Exists(temp)) File.Delete(temp);
            }
        }
    }
}