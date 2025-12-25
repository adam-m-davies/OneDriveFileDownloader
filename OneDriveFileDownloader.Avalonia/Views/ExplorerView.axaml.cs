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
	private MainViewModel _vm;

	public ExplorerView()
	{
		InitializeComponent();
		this.AttachedToVisualTree += (s, e) => { _vm = DataContext as MainViewModel; };
		FolderTree.SelectionChanged += FolderTree_SelectionChanged;
		ContentsList.DoubleTapped += ContentsList_DoubleTapped;
	}

	private void FolderTree_SelectionChanged(object sender, SelectionChangedEventArgs e)
	{
		if (FolderTree.SelectedItem is DriveItemNode node)
		{
			// expand node (load children) and scan folder contents
			_ = Task.Run(async () =>
			{
				await _vm.ExpandNodeAsync(node);
				var results = await _vm.ScanFolderAsync(node.Item);
				await Dispatcher.UIThread.InvokeAsync(() =>
				{
					ContentsList.ItemsSource = results;
				});
			});
		}
	}

	private void FolderTreeItem_Expanded(object sender, RoutedEventArgs e)
	{
		if (e.Source is TreeViewItem tvi && tvi.DataContext is DriveItemNode node)
		{
			_ = Task.Run(async () => { await _vm.ExpandNodeAsync(node); });
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
