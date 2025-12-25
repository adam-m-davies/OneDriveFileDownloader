using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;

namespace OneDriveFileDownloader.Avalonia.Views;

public partial class DashboardView : UserControl
{
	public DashboardView()
	{
		InitializeComponent();
	}

	private OneDriveFileDownloader.UI.ViewModels.MainViewModel _vm;
	public void InitializeComponent()
	{
		AvaloniaXamlLoader.Load(this);
		this.AttachedToVisualTree += (s, e) => { _vm = DataContext as OneDriveFileDownloader.UI.ViewModels.MainViewModel; RecentList.DoubleTapped += RecentList_DoubleTapped; };
	}

	private void RecentList_DoubleTapped(object sender, global::Avalonia.Interactivity.RoutedEventArgs e)
	{
		if (RecentList.SelectedItem is OneDriveFileDownloader.Core.Models.DownloadRecord r && !string.IsNullOrEmpty(r.LocalPath) && System.IO.File.Exists(r.LocalPath))
		{
			_ = _vm.OpenPath(r.LocalPath);
		}
	}
}
