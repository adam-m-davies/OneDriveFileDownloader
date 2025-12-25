using OneDriveFileDownloader.Core.Models;
using OneDriveFileDownloader.Core.Services;
using OneDriveFileDownloader.Console.Services;
using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.UI.Xaml.Media.Imaging;
using System.Threading;

namespace OneDriveFileDownloader.WinUI.ViewModels
{
    public class MainViewModel : ViewModelBase
    {
        private readonly OneDriveService _svc = new OneDriveService();
        private readonly Settings _settings;

        public ObservableCollection<SharedItemInfo> SharedItems { get; } = new ObservableCollection<SharedItemInfo>();
        public ObservableCollection<DownloadItemViewModel> Videos { get; } = new ObservableCollection<DownloadItemViewModel>();

        private string? _statusText;
        public string? StatusText { get => _statusText; set => Set(ref _statusText, value); }

        private string? _userDisplayName;
        public string? UserDisplayName { get => _userDisplayName; set => Set(ref _userDisplayName, value); }

        private BitmapImage? _userThumbnail;
        public BitmapImage? UserThumbnail { get => _userThumbnail; set => Set(ref _userThumbnail, value); }

        public MainViewModel()
        {
            _settings = SettingsStore.Load();
            // if we have saved client id, auto-configure but do not authenticate
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
                var bmp = new BitmapImage();
                ms.Position = 0;
                await bmp.SetSourceAsync(ms.AsRandomAccessStream());
                UserThumbnail = bmp;
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
            var dbPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "downloads.db");
            using var repo = new OneDriveFileDownloader.Core.Services.SqliteDownloadRepository(dbPath);

            if (!string.IsNullOrEmpty(item.File.Sha1Hash) && await repo.HasHashAsync(item.File.Sha1Hash))
            {
                item.Status = "Already downloaded";
                return;
            }

            item.Status = "Downloading";
            var temp = Path.GetTempFileName();

            long totalRead = 0;
            var progress = new Progress<long>(bytes =>
            {
                totalRead = bytes;
                if (item.File.Size.HasValue && item.File.Size.Value > 0)
                {
                    item.Progress = Math.Min(100.0, (bytes / (double)item.File.Size.Value) * 100.0);
                }
                else
                {
                    // unknown size: approximate
                    item.Progress = Math.Min(100.0, item.Progress + 5);
                }
            });

            try
            {
                using (var fs = File.Create(temp))
                {
                    var dl = await _svc.DownloadFileAsync(item.File, fs, progress);
                    // check duplicate
                    if (!string.IsNullOrEmpty(dl.Sha1Hash) && await repo.HasHashAsync(dl.Sha1Hash))
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

                    var rec = new OneDriveFileDownloader.Core.Models.DownloadRecord
                    {
                        FileId = item.File.Id,
                        Sha1Hash = dl.Sha1Hash,
                        FileName = item.File.Name,
                        Size = dl.Size,
                        LocalPath = dest
                    };
                    await repo.AddRecordAsync(rec);
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
