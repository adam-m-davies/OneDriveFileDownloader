using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;

namespace OneDriveFileDownloader.Avalonia.Views;

public partial class DashboardView : UserControl
{
	private OneDriveFileDownloader.UI.ViewModels.MainViewModel _vm;

	public DashboardView()
	{
		InitializeComponent();
		this.AttachedToVisualTree += (s, e) => { 
			_vm = DataContext as OneDriveFileDownloader.UI.ViewModels.MainViewModel; 
			var recentList = this.FindControl<ListBox>("RecentList");
			if (recentList != null) recentList.DoubleTapped += RecentList_DoubleTapped; 
		};
	}

	private void RecentList_DoubleTapped(object sender, global::Avalonia.Interactivity.RoutedEventArgs e)
	{
		var recentList = this.FindControl<ListBox>("RecentList");
		if (recentList != null && recentList.SelectedItem is OneDriveFileDownloader.Core.Models.DownloadRecord r && !string.IsNullOrEmpty(r.LocalPath) && System.IO.File.Exists(r.LocalPath))
		{
			_ = _vm.OpenPath(r.LocalPath);
		}
	}
}
