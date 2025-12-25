using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using OneDriveFileDownloader.Core.Services;
using OneDriveFileDownloader.Core.Models;
using OneDriveFileDownloader.Console.Services;
using System.Linq;
using System.Threading.Tasks;
using System;
using System.IO;

namespace OneDriveFileDownloader.WinUI
{
    public sealed partial class MainWindow : NavigationView
    {
        private readonly OneDriveService _svc = new OneDriveService();
        private readonly Settings _settings;

        public MainWindow()
        {
            this.InitializeComponent();

            _settings = SettingsStore.Load();

            // if we have a saved client id, show it in the SignIn flow when user clicks Sign in
            if (!string.IsNullOrEmpty(_settings.LastClientId))
            {
                SignInButton.Content = "Sign in (use saved client id)";
            }
        }

        private async void SignInButton_Click(object sender, RoutedEventArgs e)
        {
            // prompt for client id in a simple dialog
            var dialog = new ContentDialog { Title = "Sign in", PrimaryButtonText = "Sign in", CloseButtonText = "Cancel" };
            var stack = new StackPanel();
            var tb = new TextBox { PlaceholderText = "ClientId", Text = _settings.LastClientId ?? string.Empty };
            var saveCheck = new CheckBox { Content = "Save client id", IsChecked = true };
            stack.Children.Add(tb);
            stack.Children.Add(saveCheck);
            dialog.Content = stack;

            var res = await dialog.ShowAsync();
            if (res == ContentDialogResult.Primary)
            {
                var clientId = tb.Text.Trim();
                if (string.IsNullOrEmpty(clientId)) return;
                _svc.Configure(clientId);
                StatusText.Text = "Authenticating...";
                var user = await Task.Run(() => _svc.AuthenticateInteractiveAsync());
                StatusText.Text = "Signed in.";

                if (saveCheck.IsChecked == true) SettingsStore.SaveLastClientId(clientId);

                // fetch profile
                var prof = await _svc.GetUserProfileAsync();
                if (!string.IsNullOrEmpty(prof.DisplayName)) UserName.Text = prof.DisplayName;
                if (prof.ThumbnailBytes != null && prof.ThumbnailBytes.Length > 0)
                {
                    var ms = new MemoryStream(prof.ThumbnailBytes);
                    var bmp = new Microsoft.UI.Xaml.Media.Imaging.BitmapImage();
                    await bmp.SetSourceAsync(ms.AsRandomAccessStream());
                    UserThumbnail.Source = bmp;
                    UserThumbnail.Visibility = Visibility.Visible;
                }

                // load shared items
                await LoadSharedItems();
            }
        }

        private async Task LoadSharedItems()
        {
            SharedItemsList.Items.Clear();
            var items = await _svc.ListSharedWithMeAsync();
            foreach (var s in items) SharedItemsList.Items.Add(s);
            if (SharedItemsList.Items.Count == 0)
            {
                StatusText.Text = "No shared items found.";
            }
            else StatusText.Text = $"Found {SharedItemsList.Items.Count} shared items.";
        }

        private void SharedItemsList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // placeholder
        }

        private async void Nav_ItemInvoked(Microsoft.UI.Xaml.Controls.NavigationView sender, Microsoft.UI.Xaml.Controls.NavigationViewItemInvokedEventArgs args)
        {
            if (args.InvokedItemContainer == null) return;
            if (args.InvokedItemContainer == SettingsNav)
            {
                var dialog = new ContentDialog { Title = "Settings", PrimaryButtonText = "Save", CloseButtonText = "Cancel" };
                var stack = new StackPanel();
                var folderBox = new TextBox { PlaceholderText = "Local downloads folder", Text = _settings.LastDownloadFolder ?? string.Empty };
                stack.Children.Add(folderBox);
                dialog.Content = stack;
                var res = await dialog.ShowAsync();
                if (res == ContentDialogResult.Primary)
                {
                    _settings.LastDownloadFolder = folderBox.Text?.Trim();
                    SettingsStore.Save(_settings);
                    StatusText.Text = "Settings saved.";
                }
            }
        }

        private async void ScanButton_Click(object sender, RoutedEventArgs e)
        {
            if (SharedItemsList.SelectedItem is not OneDriveFileDownloader.Core.Models.SharedItemInfo si)
            {
                StatusText.Text = "Select a shared item first.";
                return;
            }

            StatusText.Text = "Scanning...";
            VideosList.Items.Clear();

            // BFS recursion similar to console app
            var videoExts = new[] { ".mp4", ".mov", ".mkv", ".avi", ".wmv" };
            var rootChildren = await _svc.ListChildrenAsync(si);

            var videos = rootChildren.Where(c => !c.IsFolder && videoExts.Any(e => c.Name.EndsWith(e, StringComparison.OrdinalIgnoreCase))).ToList();
            var subfolders = new System.Collections.Generic.Queue<OneDriveFileDownloader.Core.Models.DriveItemInfo>(rootChildren.Where(c => c.IsFolder));

            while (subfolders.Count > 0)
            {
                var folder = subfolders.Dequeue();
                var fakeShared = new OneDriveFileDownloader.Core.Models.SharedItemInfo { RemoteDriveId = folder.DriveId, RemoteItemId = folder.Id, Name = folder.Name };
                var children = await _svc.ListChildrenAsync(fakeShared);
                foreach (var c in children)
                {
                    if (c.IsFolder) subfolders.Enqueue(c);
                    else if (videoExts.Any(e => c.Name.EndsWith(e, StringComparison.OrdinalIgnoreCase))) videos.Add(c);
                }
            }

            foreach (var v in videos) VideosList.Items.Add(v);
            StatusText.Text = $"Found {videos.Count} video files.";
        }

        private async void DownloadSelectedButton_Click(object sender, RoutedEventArgs e)
        {
            if (VideosList.SelectedItem is not OneDriveFileDownloader.Core.Models.DriveItemInfo file)
            {
                StatusText.Text = "Select a video first.";
                return;
            }

            // choose local folder (use settings if present)
            var downloads = _settings.LastDownloadFolder;
            if (string.IsNullOrEmpty(downloads))
            {
                StatusText.Text = "Enter a local download folder in Settings first.";
                return;
            }

            Directory.CreateDirectory(downloads);
            var dbPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "downloads.db");
            using var repo = new OneDriveFileDownloader.Core.Services.SqliteDownloadRepository(dbPath);

            if (!string.IsNullOrEmpty(file.Sha1Hash) && await repo.HasHashAsync(file.Sha1Hash))
            {
                StatusText.Text = "Already downloaded (hash match).";
                return;
            }

            StatusText.Text = "Downloading...";
            var temp = Path.GetTempFileName();
            using (var fs = File.Create(temp))
            {
                var dl = await _svc.DownloadFileAsync(file, fs);
                if (!string.IsNullOrEmpty(dl.Sha1Hash) && await repo.HasHashAsync(dl.Sha1Hash))
                {
                    fs.Close();
                    File.Delete(temp);
                    StatusText.Text = "Duplicate after hash check. Skipped.";
                    return;
                }
                var dest = Path.Combine(downloads, file.Name);
                if (File.Exists(dest))
                {
                    dest = Path.Combine(downloads, Path.GetFileNameWithoutExtension(file.Name) + "_" + Guid.NewGuid().ToString("n").Substring(0, 8) + Path.GetExtension(file.Name));
                }
                fs.Close();
                File.Move(temp, dest);

                var rec = new OneDriveFileDownloader.Core.Models.DownloadRecord
                {
                    FileId = file.Id,
                    Sha1Hash = dl.Sha1Hash,
                    FileName = file.Name,
                    Size = dl.Size,
                    LocalPath = dest
                };
                await repo.AddRecordAsync(rec);
                StatusText.Text = $"Downloaded {file.Name} to {dest}";
            }
        }
    }
}
