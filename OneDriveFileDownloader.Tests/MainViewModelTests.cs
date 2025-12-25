using OneDriveFileDownloader.Core.Interfaces;
using OneDriveFileDownloader.Core.Models;
using OneDriveFileDownloader.UI.ViewModels;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Xunit;
using System.Threading;

namespace OneDriveFileDownloader.Tests
{
    public class MainViewModelTests
    {
        class FakeOneDriveService : IOneDriveService
        {
            public List<SharedItemInfo> Shared = new List<SharedItemInfo>();
            public Dictionary<string, List<DriveItemInfo>> Children = new Dictionary<string, List<DriveItemInfo>>();

            public void Configure(string clientId) { }
            public Task<string> AuthenticateInteractiveAsync() => Task.FromResult("fakeuser@example.com");
            public Task<DownloadResult> DownloadFileAsync(DriveItemInfo file, Stream destination, IProgress<long>? progress = null)
            {
                var bytes = Encoding.UTF8.GetBytes("dummycontent");
                destination.Write(bytes, 0, bytes.Length);
                progress?.Report(bytes.Length);
                var sha = "deadbeef";
                return Task.FromResult(new DownloadResult { Sha1Hash = sha, Size = bytes.Length });
            }

            public Task<IList<DriveItemInfo>> ListChildrenAsync(SharedItemInfo sharedItem)
            {
                if (Children.TryGetValue(sharedItem.RemoteItemId, out var list)) return Task.FromResult((IList<DriveItemInfo>)list);
                return Task.FromResult((IList<DriveItemInfo>)new List<DriveItemInfo>());
            }

            public Task<IList<SharedItemInfo>> ListSharedWithMeAsync() => Task.FromResult((IList<SharedItemInfo>)Shared);

            public Task<UserProfile> GetUserProfileAsync() => Task.FromResult(new UserProfile { DisplayName = "Fake User" });
        }

        class FakeRepo : IDownloadRepository
        {
            public List<DownloadRecord> Added = new List<DownloadRecord>();
            public Task AddRecordAsync(DownloadRecord record)
            {
                Added.Add(record);
                return Task.CompletedTask;
            }

            public Task<bool> HasHashAsync(string sha1Hash) => Task.FromResult(false);
        }

        [Fact]
        public async Task ScanAsync_PopulatesVideos()
        {
            var svc = new FakeOneDriveService();
            var root = new SharedItemInfo { Id = "s1", Name = "Root", RemoteDriveId = "d", RemoteItemId = "r1" };
            svc.Shared.Add(root);
            svc.Children["r1"] = new List<DriveItemInfo> { new DriveItemInfo { Id = "f1", DriveId = "d", Name = "video1.mp4", IsFolder = false }, new DriveItemInfo { Id = "fold1", DriveId = "d", Name = "sub", IsFolder = true } };
            svc.Children["fold1"] = new List<DriveItemInfo> { new DriveItemInfo { Id = "f2", DriveId = "d", Name = "video2.mp4", IsFolder = false } };

            var vm = new MainViewModel(svc, new FakeRepo());
            await vm.LoadSharedItemsAsync();
            Assert.Equal(1, vm.SharedItems.Count);
            await vm.ScanAsync(root);
            Assert.Equal(2, vm.Videos.Count);
        }

        [Fact]
        public async Task DownloadAsync_AddsRecord_WhenNotDuplicate()
        {
            var svc = new FakeOneDriveService();
            var repo = new FakeRepo();
            var vm = new MainViewModel(svc, repo);
            var file = new DriveItemInfo { Id = "f1", DriveId = "d", Name = "video.mp4", IsFolder = false, Size = 12 };
            var item = new DownloadItemViewModel(file);

            // ensure settings have a download folder
            var settings = OneDriveFileDownloader.Core.Services.SettingsStore.Load();
            settings.LastDownloadFolder = Path.GetTempPath();
            OneDriveFileDownloader.Core.Services.SettingsStore.Save(settings);

            await vm.DownloadAsync(item);

            Assert.Equal("Completed", item.Status);
            Assert.Single(repo.Added);
            Assert.Equal(file.Id, repo.Added[0].FileId);
        }
    }
}
