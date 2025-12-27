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
		};
	}

	private void OnItemDoubleTapped(object sender, global::Avalonia.Interactivity.RoutedEventArgs e)
	{
		if (sender is Control c && c.DataContext is OneDriveFileDownloader.Core.Models.DownloadRecord r)
		{
			if (!string.IsNullOrEmpty(r.LocalPath) && System.IO.File.Exists(r.LocalPath))
			{
				_ = _vm.OpenPath(r.LocalPath);
			}
		}
	}
}
