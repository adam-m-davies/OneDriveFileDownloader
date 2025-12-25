using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using OneDriveFileDownloader.UI.ViewModels;

namespace OneDriveFileDownloader.WinUI.Controls
{
    public sealed partial class MinimalView : UserControl
    {
        private readonly MainViewModel _vm;
        public MinimalView(MainViewModel vm)
        {
            this.InitializeComponent();
            _vm = vm;
            this.DataContext = _vm;
        }

        private async void VideosList_ItemClick(object sender, Microsoft.UI.Xaml.Controls.ItemClickEventArgs e)
        {
            if (e.ClickedItem is DownloadItemViewModel vm) await _vm.DownloadAsync(vm);
        }

        private async void DownloadSelectedButton_Click(object sender, RoutedEventArgs e)
        {
            if (VideosList.SelectedItem is DownloadItemViewModel vm) await _vm.DownloadAsync(vm);
        }
    }
}
