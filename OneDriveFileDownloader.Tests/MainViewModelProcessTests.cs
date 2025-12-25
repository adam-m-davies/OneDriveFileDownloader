using OneDriveFileDownloader.UI.ViewModels;
using Xunit;
using System.IO;

namespace OneDriveFileDownloader.Tests
{
    public class MainViewModelProcessTests
    {
        [Fact]
        public void OpenPath_Uses_Launcher()
        {
            var launcher = new FakeProcessLauncher();
            var vm = new MainViewModel(new FakeOneDriveService(), new MainViewModelTests.FakeRepo(), launcher: launcher);
            var temp = Path.GetTempFileName();
            try
            {
                var ok = vm.OpenPath(temp);
                Assert.True(ok);
                Assert.Equal(temp, launcher.LastOpened);
            }
            finally
            {
                if (File.Exists(temp)) File.Delete(temp);
            }
        }
    }
}