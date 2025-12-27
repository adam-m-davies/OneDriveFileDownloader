using OneDriveFileDownloader.Core.Services;
using System.Threading.Tasks;
using Xunit;

namespace OneDriveFileDownloader.Tests
{
    public class MainViewModelSignInTests
    {
        [Fact]
        public async Task SignInAsync_SavesClientId_WhenSaveTrue()
        {
            var svc = new FakeOneDriveService();
            var repo = new MainViewModelTests.FakeRepo();

            // use a temp settings file for isolation
            var temp = System.IO.Path.Combine(System.IO.Path.GetTempPath(), System.Guid.NewGuid().ToString() + "-odfd-signin.json");
            SettingsStore.TestFilePathOverride = temp;

            try
            {
                var vm = new OneDriveFileDownloader.UI.ViewModels.MainViewModel(svc, repo);
                await vm.SignInAsync("test-client-id", true);

                var s = SettingsStore.Load();
                Assert.Equal("test-client-id", s.LastClientId);
            }
            finally
            {
                if (System.IO.File.Exists(temp)) System.IO.File.Delete(temp);
                SettingsStore.TestFilePathOverride = null;
            }
        }
    }
}