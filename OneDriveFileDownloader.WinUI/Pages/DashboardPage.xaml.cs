using Microsoft.UI.Xaml.Controls;
using System;
using System.IO;
using System.Threading.Tasks;

using OneDriveFileDownloader.UI.ViewModels;

namespace OneDriveFileDownloader.WinUI.Pages
{
    public sealed partial class DashboardPage : Page
    {
        private readonly MainViewModel _vm;
        private readonly OneDriveFileDownloader.Core.Services.Settings _settings;

        public DashboardPage(MainViewModel vm)
        {
            this.InitializeComponent();
            _vm = vm;
            _settings = OneDriveFileDownloader.Core.Services.SettingsStore.Load();

            ScanLastButton.Click += async (s, e) => await ScanLast();
            OpenDownloads.Click += (s, e) => {
                var p = _settings.LastDownloadFolder;
                if (!string.IsNullOrEmpty(p) && Directory.Exists(p))
                {
                    _ = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo() { FileName = p, UseShellExecute = true });
                }
            };

            this.Loaded += async (s, e) =>
            {
                RecentList.ItemsSource = _vm.RecentDownloads;
                await _vm.LoadRecentDownloadsAsync(20);
            };

            RecentList.ItemClick += (s, e) =>
            {
                if (e.ClickedItem is OneDriveFileDownloader.Core.Models.DownloadRecord r && !string.IsNullOrEmpty(r.LocalPath) && File.Exists(r.LocalPath))
                {
                    _ = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo() { FileName = r.LocalPath, UseShellExecute = true });
                }
            };
        }

        private async Task ScanLast()
        {
            if (_vm.SharedItems.Count > 0)
            {
                var last = _vm.SharedItems[0];
                await _vm.ScanAsync(last);
            }
        }
    }
}
