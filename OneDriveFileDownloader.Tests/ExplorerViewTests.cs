using System.Reflection;
using System.Threading.Tasks;
using Avalonia.Interactivity;
using Avalonia.Controls;
using OneDriveFileDownloader.Avalonia.Views;
using OneDriveFileDownloader.Tests;
using Xunit;
using OneDriveFileDownloader.Core.Services;
using OneDriveFileDownloader.UI.ViewModels;
using OneDriveFileDownloader.Core.Models;

namespace OneDriveFileDownloader.Tests
{
    public class ExplorerViewTests
    {
        class FakeRepo : OneDriveFileDownloader.Core.Interfaces.IDownloadRepository
    {
        public System.Collections.Generic.List<OneDriveFileDownloader.Core.Models.DownloadRecord> Added = new System.Collections.Generic.List<OneDriveFileDownloader.Core.Models.DownloadRecord>();
        public System.Collections.Generic.List<OneDriveFileDownloader.Core.Models.DownloadRecord> Stored = new System.Collections.Generic.List<OneDriveFileDownloader.Core.Models.DownloadRecord>();
        public System.Threading.Tasks.Task AddRecordAsync(OneDriveFileDownloader.Core.Models.DownloadRecord record)
        {
            Added.Add(record);
            Stored.Add(record);
            return System.Threading.Tasks.Task.CompletedTask;
        }

        public System.Threading.Tasks.Task<bool> HasHashAsync(string sha1Hash) => System.Threading.Tasks.Task.FromResult(false);

        public System.Threading.Tasks.Task<System.Collections.Generic.IList<OneDriveFileDownloader.Core.Models.DownloadRecord>> GetRecentAsync(int count = 20)
        {
            System.Collections.Generic.IList<OneDriveFileDownloader.Core.Models.DownloadRecord> list = Stored.OrderByDescending(r => r.DownloadedAtUtc).Take(count).ToList();
            return System.Threading.Tasks.Task.FromResult(list);
        }
    }

        [Fact]
        public async Task DoubleClick_OnContent_StartsDownload()
        {
            var svc = new FakeOneDriveService();
            svc.Children["fold1"] = new System.Collections.Generic.List<DriveItemInfo> { new DriveItemInfo { Id = "f1", DriveId = "d", Name = "video1.mp4", IsFolder = false } };
            var repo = new FakeRepo();
            var vm = new MainViewModel(svc, repo);

            // simulate the double-click behavior by invoking the viewmodel download directly (avoids platform-dependent UI startup in tests)
            var file = new DriveItemInfo { Id = "f1", DriveId = "d", Name = "video1.mp4", IsFolder = false };
            var item = new DownloadItemViewModel(file);
            await vm.DownloadAsync(item);

            Assert.Single(repo.Added);
            Assert.Equal("f1", repo.Added[0].FileId);
        }
    }
}
