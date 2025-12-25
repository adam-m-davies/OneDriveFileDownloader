using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Avalonia.Interactivity;
using System.Threading.Tasks;
using OneDriveFileDownloader.UI.ViewModels;
using OneDriveFileDownloader.Core.Models;

namespace OneDriveFileDownloader.Avalonia.Views;

public partial class ExplorerView : UserControl
{
	private OneDriveFileDownloader.UI.ViewModels.MainViewModel _vm;

	public ExplorerView()
	{
		InitializeComponent();
		this.AttachedToVisualTree += (s, e) => { _vm = DataContext as OneDriveFileDownloader.UI.ViewModels.MainViewModel; ScanFolderBtn.Click += ScanFolderBtn_Click; };
		FolderTree.SelectionChanged += FolderTree_SelectionChanged;
		ContentsList.DoubleTapped += ContentsList_DoubleTapped;
	}

	private void FolderTree_SelectionChanged(object sender, SelectionChangedEventArgs e)
	{
		if (FolderTree.SelectedItem is OneDriveFileDownloader.UI.ViewModels.DriveItemNode dn)
		{
			// expand node (load children)
			_ = Task.Run(async () =>
			{
				await _vm.ExpandNodeAsync(dn);
				if (_vm.ScanOnSelection)
				{
					var results = await _vm.ScanFolderAsync(dn.Item);
					await Dispatcher.UIThread.InvokeAsync(() =>
					{
						ContentsList.ItemsSource = results;
					});
				}
			});
		}
	}

	private void FolderTreeItem_Expanded(object sender, RoutedEventArgs e)
	{
		if (e.Source is TreeViewItem tvi && tvi.DataContext is OneDriveFileDownloader.UI.ViewModels.DriveItemNode dn)
		{
			_ = Task.Run(async () => { await _vm.ExpandNodeAsync(dn); });
		}
	}

	private void ScanFolderBtn_Click(object sender, RoutedEventArgs e)
	{
		if (FolderTree.SelectedItem is OneDriveFileDownloader.UI.ViewModels.DriveItemNode dn && dn.IsFolder)
		{
			_ = Task.Run(async () =>
			{
				var results = await _vm.ScanFolderAsync(dn.Item);
				await Dispatcher.UIThread.InvokeAsync(() => { ContentsList.ItemsSource = results; });
			});
		}
	}

	private void ContentsList_DoubleTapped(object sender, RoutedEventArgs e)
	{
		if (ContentsList.SelectedItem is DriveItemInfo di && !di.IsFolder)
		{
			var item = new OneDriveFileDownloader.UI.ViewModels.DownloadItemViewModel(di);
			_ = Task.Run(async () => { await _vm.DownloadAsync(item); });
		}
	}

	public void InitializeComponent()
	{
		AvaloniaXamlLoader.Load(this);
	}
}
