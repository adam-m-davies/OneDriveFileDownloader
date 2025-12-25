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
            public async Task<DownloadResult> DownloadFileAsync(DriveItemInfo file, Stream destination, IProgress<long>? progress = null, CancellationToken cancellation = default)
            {
                // simulate some work and respect cancellation
                var bytes = Encoding.UTF8.GetBytes("dummycontent");
                await Task.Delay(200, cancellation);
                cancellation.ThrowIfCancellationRequested();
                await destination.WriteAsync(bytes, 0, bytes.Length, cancellation);
                progress?.Report(bytes.Length);
                var sha = "deadbeef";
                return new DownloadResult { Sha1Hash = sha, Size = bytes.Length };
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
            public List<DownloadRecord> Stored = new List<DownloadRecord>();
            public Task AddRecordAsync(DownloadRecord record)
            {
                Added.Add(record);
                Stored.Add(record);
                record.DownloadedAtUtc = record.DownloadedAtUtc == default ? System.DateTime.UtcNow : record.DownloadedAtUtc;
                return Task.CompletedTask;
            }

            public Task<bool> HasHashAsync(string sha1Hash) => Task.FromResult(false);

            public Task<IList<DownloadRecord>> GetRecentAsync(int count = 20)
            {
                IList<DownloadRecord> list = Stored.OrderByDescending(r => r.DownloadedAtUtc).Take(count).ToList();
                return Task.FromResult(list);
            }
        }

        class FailOnceThenSucceedService : IOneDriveService
        {
            private readonly System.Collections.Concurrent.ConcurrentDictionary<string, int> _attempts = new System.Collections.Concurrent.ConcurrentDictionary<string, int>();
            public void Configure(string clientId) { }
            public Task<string> AuthenticateInteractiveAsync() => Task.FromResult("fakeuser@example.com");
            public async Task<DownloadResult> DownloadFileAsync(DriveItemInfo file, Stream destination, IProgress<long>? progress = null, CancellationToken cancellation = default)
            {
                var attempt = _attempts.AddOrUpdate(file.Id, 1, (_, v) => v + 1);
                if (attempt == 1)
                {
                    await Task.Delay(50, cancellation);
                    throw new System.Exception("Simulated failure");
                }

                var bytes = Encoding.UTF8.GetBytes("dummycontent");
                await Task.Delay(10, cancellation);
                await destination.WriteAsync(bytes, 0, bytes.Length, cancellation);
                progress?.Report(bytes.Length);
                var sha = "cafebabe";
                return new DownloadResult { Sha1Hash = sha, Size = bytes.Length };
            }

            public Task<IList<DriveItemInfo>> ListChildrenAsync(SharedItemInfo sharedItem) => Task.FromResult((IList<DriveItemInfo>)new List<DriveItemInfo>());
            public Task<IList<SharedItemInfo>> ListSharedWithMeAsync() => Task.FromResult((IList<SharedItemInfo>)new List<SharedItemInfo>());
            public Task<UserProfile> GetUserProfileAsync() => Task.FromResult(new UserProfile { DisplayName = "Fake User" });
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

        [Fact]
        public async Task LoadRecentDownloads_PopulatesRecentDownloads()
        {
            var svc = new FakeOneDriveService();
            var repo = new FakeRepo();
            var vm = new MainViewModel(svc, repo);

            // seed repo with two records
            repo.Stored.Add(new DownloadRecord { Id = Guid.NewGuid(), FileId = "f1", FileName = "a.mp4", DownloadedAtUtc = System.DateTime.UtcNow.AddMinutes(-5) });
            repo.Stored.Add(new DownloadRecord { Id = Guid.NewGuid(), FileId = "f2", FileName = "b.mp4", DownloadedAtUtc = System.DateTime.UtcNow });

            await vm.LoadRecentDownloadsAsync(10);
            Assert.Equal(2, vm.RecentDownloads.Count);
            Assert.Equal("b.mp4", vm.RecentDownloads[0].FileName);
        }

        [Fact]
        public async Task Explorer_BuildAndExpand_PopulatesChildren()
        {
            var svc = new FakeOneDriveService();
            // create folder structure
            var rootFolder = new DriveItemInfo { Id = "fold1", DriveId = "d", Name = "root", IsFolder = true };
            svc.Children["fold1"] = new List<DriveItemInfo> { new DriveItemInfo { Id = "f1", DriveId = "d", Name = "video1.mp4", IsFolder = false }, new DriveItemInfo { Id = "sub1", DriveId = "d", Name = "sub", IsFolder = true } };
            svc.Children["sub1"] = new List<DriveItemInfo> { new DriveItemInfo { Id = "f2", DriveId = "d", Name = "video2.mp4", IsFolder = false } };

            var repo = new FakeRepo();
            var vm = new MainViewModel(svc, repo);

            var node = await vm.BuildFolderNodeAsync(rootFolder);
            Assert.Equal(2, node.Children.Count);

            var subNode = node.Children[1];
            await vm.ExpandNodeAsync(subNode);
            Assert.Single(subNode.Children);
        }

        [Fact]
        public async Task Explorer_ScanFolderRecursively_FindsVideos()
        {
            var svc = new FakeOneDriveService();
            svc.Children["fold1"] = new List<DriveItemInfo> { new DriveItemInfo { Id = "f1", DriveId = "d", Name = "video1.mp4", IsFolder = false }, new DriveItemInfo { Id = "sub1", DriveId = "d", Name = "sub", IsFolder = true } };
            svc.Children["sub1"] = new List<DriveItemInfo> { new DriveItemInfo { Id = "f2", DriveId = "d", Name = "video2.mp4", IsFolder = false } };
            var repo = new FakeRepo();
            var vm = new MainViewModel(svc, repo);
            var results = await vm.ScanFolderAsync(new DriveItemInfo { Id = "fold1", DriveId = "d", Name = "fold1", IsFolder = true });
            Assert.Equal(2, results.Count);
        }

        [Fact]
        public async Task DownloadAsync_Cancels_WhenRequested()
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

            var t = vm.DownloadAsync(item);
            await Task.Delay(50);
            item.Cancel();
            await Task.WhenAny(t, Task.Delay(2000));

            Assert.Equal("Canceled", item.Status);
        }

        [Fact]
        public async Task DownloadQueue_RespectsMaxConcurrentDownloads()
        {
            var svc = new FakeOneDriveService();
            var repo = new FakeRepo();
            var vm = new MainViewModel(svc, repo);
            vm.MaxConcurrentDownloads = 1;

            // ensure settings have a download folder
            var settings = OneDriveFileDownloader.Core.Services.SettingsStore.Load();
            settings.LastDownloadFolder = Path.GetTempPath();
            OneDriveFileDownloader.Core.Services.SettingsStore.Save(settings);

            var item1 = new DownloadItemViewModel(new DriveItemInfo { Id = "f1", DriveId = "d", Name = "video1.mp4", IsFolder = false, Size = 1200000 });
            var item2 = new DownloadItemViewModel(new DriveItemInfo { Id = "f2", DriveId = "d", Name = "video2.mp4", IsFolder = false, Size = 1200000 });

            var t1 = vm.DownloadAsync(item1);
            var t2 = vm.DownloadAsync(item2);

            await Task.Delay(50);
            // with MaxConcurrentDownloads=1 only first should be downloading
            Assert.True(item1.IsDownloading || item1.Status == "Downloading");
            Assert.False(item2.IsDownloading || item2.Status == "Downloading");

            await Task.WhenAll(t1, t2);
            Assert.Equal("Completed", item1.Status);
            Assert.Equal("Completed", item2.Status);
        }

        [Fact]
        public async Task Retry_ReDownloadsAfterFailure()
        {
            // service fails once per file id then succeeds
            var svc = new FailOnceThenSucceedService();
            var repo = new FakeRepo();
            var vm = new MainViewModel(svc, repo);

            // ensure settings have a download folder
            var settings = OneDriveFileDownloader.Core.Services.SettingsStore.Load();
            settings.LastDownloadFolder = Path.GetTempPath();
            OneDriveFileDownloader.Core.Services.SettingsStore.Save(settings);

            var file = new DriveItemInfo { Id = "f3", DriveId = "d", Name = "video.mp4", IsFolder = false, Size = 1200000 };
            var item = new DownloadItemViewModel(file);

            await vm.DownloadAsync(item);
            Assert.Equal("Error", item.Status);

            // retry
            item.Retry();
            await vm.DownloadAsync(item);
            Assert.Equal("Completed", item.Status);
        }

        [Fact]
        public void XamlFiles_ContainAutomationNames()
        {
            // find repo root by locating the solution file, then search for XAML files
            string? cursor = Directory.GetCurrentDirectory();
            string? repoRoot = null;
            while (cursor != null)
            {
                var slns = Directory.GetFiles(cursor, "*.sln", SearchOption.TopDirectoryOnly);
                if (slns.Length > 0) { repoRoot = cursor; break; }
                cursor = Directory.GetParent(cursor)?.FullName;
            }

            Assert.False(string.IsNullOrEmpty(repoRoot), "Repository root (solution folder) not found");
            var xamls = Directory.GetFiles(repoRoot!, "*.xaml", SearchOption.AllDirectories)
                .Where(p => !p.Contains("bin") && !p.Contains("obj") && !p.Contains("\\OneDriveFileDownloader.Tests\\"));

            Assert.Contains(xamls, p => File.ReadAllText(p).Contains("AutomationProperties.Name"));
        }
    }
}
