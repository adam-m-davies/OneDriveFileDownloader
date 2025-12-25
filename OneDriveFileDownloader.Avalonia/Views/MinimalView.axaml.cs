using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using System.Threading.Tasks;

namespace OneDriveFileDownloader.Avalonia.Views;

public partial class MinimalView : UserControl
{
	public MinimalView()
	{
		InitializeComponent();
		this.AttachedToVisualTree += (s, e) => { /* placeholder */ };
		VideosList.DoubleTapped += VideosList_DoubleTapped;
	}

	private void VideosList_DoubleTapped(object sender, global::Avalonia.Interactivity.RoutedEventArgs e)
	{
		if (VideosList.SelectedItem is OneDriveFileDownloader.Core.Models.DriveItemInfo di)
		{
			var vm = DataContext as OneDriveFileDownloader.UI.ViewModels.MainViewModel;
			if (vm != null)
			{
				var item = new OneDriveFileDownloader.UI.ViewModels.DownloadItemViewModel(new OneDriveFileDownloader.Core.Models.DriveItemInfo { Id = di.Id, DriveId = di.DriveId, Name = di.Name, IsFolder = di.IsFolder, Size = di.Size });
				_ = Task.Run(async () => await vm.DownloadAsync(item));
			}
		}
	}

	public void InitializeComponent()
	{
		AvaloniaXamlLoader.Load(this);
	}
}
