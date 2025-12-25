using OneDriveFileDownloader.Core.Interfaces;
using OneDriveFileDownloader.Core.Models;
using System;
using System.Collections.ObjectModel;
using OneDriveFileDownloader.Core.Services;
using OneDriveFileDownloader.Core.Interfaces;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Threading;

namespace OneDriveFileDownloader.UI.ViewModels
{
    public class MainViewModel : ViewModelBase
    {
        private readonly IOneDriveService _svc;
        private readonly IDownloadRepository _repo;
        private readonly Settings _settings;

        public ObservableCollection<SharedItemInfo> SharedItems { get; } = new ObservableCollection<SharedItemInfo>();
        public ObservableCollection<DownloadItemViewModel> Videos { get; } = new ObservableCollection<DownloadItemViewModel>();
        public ObservableCollection<DownloadRecord> RecentDownloads { get; } = new ObservableCollection<DownloadRecord>();

        private string? _statusText;
        public string? StatusText { get => _statusText; set => Set(ref _statusText, value); }

        private string? _userDisplayName;
        public string? UserDisplayName { get => _userDisplayName; set => Set(ref _userDisplayName, value); }

        private object? _userThumbnail;
        public object? UserThumbnail { get => _userThumbnail; set => Set(ref _userThumbnail, value); }

        public MainViewModel(IOneDriveService? svc = null, IDownloadRepository? repo = null)
        {
            _svc = svc ?? new OneDriveFileDownloader.Core.Services.OneDriveService();
            _repo = repo ?? new OneDriveFileDownloader.Core.Services.SqliteDownloadRepository(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "downloads.db"));
            _settings = SettingsStore.Load();

            if (!string.IsNullOrEmpty(_settings.LastClientId))
            {
                _svc.Configure(_settings.LastClientId);
            }
        }

        public async Task SignInAsync(string clientId, bool save)
        {
            _svc.Configure(clientId);
            StatusText = "Authenticating...";
            var user = await _svc.AuthenticateInteractiveAsync();
            StatusText = "Signed in.";

            if (save) SettingsStore.SaveLastClientId(clientId);

            var prof = await _svc.GetUserProfileAsync();
            UserDisplayName = prof.DisplayName;
            if (prof.ThumbnailBytes != null && prof.ThumbnailBytes.Length > 0)
            {
                var ms = new MemoryStream(prof.ThumbnailBytes);
#if WINDOWS
                var bmp = new Microsoft.UI.Xaml.Media.Imaging.BitmapImage();
                ms.Position = 0;
                _ = bmp.SetSourceAsync(ms.AsRandomAccessStream());
                UserThumbnail = bmp;
#else
                UserThumbnail = prof.ThumbnailBytes; // non-Windows test environments can use raw bytes
#endif
            }

            await LoadSharedItemsAsync();
        }

        public async Task LoadSharedItemsAsync()
        {
            SharedItems.Clear();
            var items = await _svc.ListSharedWithMeAsync();
            foreach (var s in items) SharedItems.Add(s);
            StatusText = SharedItems.Count == 0 ? "No shared items found." : $"Found {SharedItems.Count} shared items.";
        }

        public async Task ScanAsync(SharedItemInfo selected)
        {
            if (selected == null) return;
            StatusText = "Scanning...";
            Videos.Clear();
            var videoExts = new[] { ".mp4", ".mov", ".mkv", ".avi", ".wmv" };

            var rootChildren = await _svc.ListChildrenAsync(selected);
            var videos = rootChildren.Where(c => !c.IsFolder && videoExts.Any(e => c.Name.EndsWith(e, StringComparison.OrdinalIgnoreCase))).ToList();
            var subfolders = new System.Collections.Generic.Queue<DriveItemInfo>(rootChildren.Where(c => c.IsFolder));

            while (subfolders.Count > 0)
            {
                var folder = subfolders.Dequeue();
                var fakeShared = new SharedItemInfo { RemoteDriveId = folder.DriveId, RemoteItemId = folder.Id, Name = folder.Name };
                var children = await _svc.ListChildrenAsync(fakeShared);
                foreach (var c in children)
                {
                    if (c.IsFolder) subfolders.Enqueue(c);
                    else if (videoExts.Any(e => c.Name.EndsWith(e, StringComparison.OrdinalIgnoreCase))) videos.Add(c);
                }
            }

            foreach (var v in videos) Videos.Add(new DownloadItemViewModel(v));
            StatusText = $"Found {videos.Count} video files.";
        }

        public Task<IList<DriveItemInfo>> GetChildrenAsync(SharedItemInfo shared)
        {
            return _svc.ListChildrenAsync(shared);
        }

        public async Task LoadRecentDownloadsAsync(int count = 20)
        {
            RecentDownloads.Clear();
            var recs = await _repo.GetRecentAsync(count);
            foreach (var r in recs) RecentDownloads.Add(r);
        }

        public async Task<DriveItemNode> BuildFolderNodeAsync(DriveItemInfo folder)
        {
            var node = new DriveItemNode(folder);
            // only populate immediate children for performance
            var fakeShared = new SharedItemInfo { RemoteDriveId = folder.DriveId, RemoteItemId = folder.Id, Name = folder.Name };
            var children = await _svc.ListChildrenAsync(fakeShared);
            foreach (var c in children)
            {
                node.Children.Add(new DriveItemNode(c));
            }
            return node;
        }

        public async Task ExpandNodeAsync(DriveItemNode node)
        {
            if (node == null || !node.IsFolder) return;
            if (node.Children.Count > 0) return; // already expanded or leaf
            var fakeShared = new SharedItemInfo { RemoteDriveId = node.Item.DriveId, RemoteItemId = node.Item.Id, Name = node.Item.Name };
            var children = await _svc.ListChildrenAsync(fakeShared);
            foreach (var c in children)
            {
                node.Children.Add(new DriveItemNode(c));
            }
            node.IsExpanded = true;
        }

        public async Task<IList<DownloadItemViewModel>> ScanFolderAsync(DriveItemInfo folder)
        {
            var results = new System.Collections.Generic.List<DownloadItemViewModel>();
            var videoExts = new[] { ".mp4", ".mov", ".mkv", ".avi", ".wmv" };
            var queue = new System.Collections.Generic.Queue<DriveItemInfo>();
            queue.Enqueue(folder);
            while (queue.Count > 0)
            {
                var f = queue.Dequeue();
                var fakeShared = new SharedItemInfo { RemoteDriveId = f.DriveId, RemoteItemId = f.Id, Name = f.Name };
                var children = await _svc.ListChildrenAsync(fakeShared);
                foreach (var c in children)
                {
                    if (c.IsFolder) queue.Enqueue(c);
                    else if (videoExts.Any(e => c.Name.EndsWith(e, System.StringComparison.OrdinalIgnoreCase))) results.Add(new DownloadItemViewModel(c));
                }
            }
            return results;
        }

        public async Task DownloadAsync(DownloadItemViewModel item)
        {
            if (item == null) return;
            var downloads = _settings.LastDownloadFolder;
            if (string.IsNullOrEmpty(downloads))
            {
                StatusText = "Enter a local download folder in Settings first.";
                return;
            }

            Directory.CreateDirectory(downloads);

            if (!string.IsNullOrEmpty(item.File.Sha1Hash) && await _repo.HasHashAsync(item.File.Sha1Hash))
            {
                item.Status = "Already downloaded";
                return;
            }

            item.Status = "Downloading";
            var temp = Path.GetTempFileName();

            var progress = new Progress<long>(bytes =>
            {
                if (item.File.Size.HasValue && item.File.Size.Value > 0)
                {
                    item.Progress = Math.Min(100.0, (bytes / (double)item.File.Size.Value) * 100.0);
                }
                else
                {
                    item.Progress = Math.Min(100.0, item.Progress + 5);
                }
            });

            try
            {
                using (var fs = File.Create(temp))
                {
                    var dl = await _svc.DownloadFileAsync(item.File, fs, progress, item.Cancellation.Token);
                    if (!string.IsNullOrEmpty(dl.Sha1Hash) && await _repo.HasHashAsync(dl.Sha1Hash))
                    {
                        fs.Close();
                        File.Delete(temp);
                        item.Status = "Duplicate after hash check";
                        return;
                    }

                    var dest = Path.Combine(downloads, item.File.Name);
                    if (File.Exists(dest))
                    {
                        dest = Path.Combine(downloads, Path.GetFileNameWithoutExtension(item.File.Name) + "_" + Guid.NewGuid().ToString("n").Substring(0, 8) + Path.GetExtension(item.File.Name));
                    }
                    fs.Close();
                    File.Move(temp, dest);

                    var rec = new DownloadRecord
                    {
                        FileId = item.File.Id,
                        Sha1Hash = dl.Sha1Hash,
                        FileName = item.File.Name,
                        Size = dl.Size,
                        LocalPath = dest
                    };
                    await _repo.AddRecordAsync(rec);
                    item.Status = "Completed";
                    item.Progress = 100;
                    StatusText = $"Downloaded {item.File.Name} to {dest}";
                }
            }
            catch (OperationCanceledException)
            {
                item.Status = "Canceled";
                if (File.Exists(temp)) File.Delete(temp);
            }
            catch (Exception ex)
            {
                item.Status = "Error";
                if (File.Exists(temp)) File.Delete(temp);
                StatusText = "Error: " + ex.Message;
            }
        }
    }
}
