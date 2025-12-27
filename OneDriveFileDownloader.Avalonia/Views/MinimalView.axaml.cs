using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using System.Threading.Tasks;

namespace OneDriveFileDownloader.Avalonia.Views;

public partial class MinimalView : UserControl
{
	public MinimalView()
	{
		InitializeComponent();
		this.AttachedToVisualTree += (s, e) => { 
			var videosList = this.FindControl<ListBox>("VideosList");
			if (videosList != null) videosList.DoubleTapped += VideosList_DoubleTapped;
		};
	}

	private void VideosList_DoubleTapped(object sender, global::Avalonia.Interactivity.RoutedEventArgs e)
	{
		var videosList = this.FindControl<ListBox>("VideosList");
		if (videosList != null && videosList.SelectedItem is OneDriveFileDownloader.UI.ViewModels.DownloadItemViewModel itemVm)
		{
			var vm = DataContext as OneDriveFileDownloader.UI.ViewModels.MainViewModel;
			if (vm != null)
			{
				_ = Task.Run(async () => await vm.DownloadAsync(itemVm));
			}
		}
	}
}
