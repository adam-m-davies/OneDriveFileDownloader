using Microsoft.UI.Xaml.Controls;
using OneDriveFileDownloader.UI.ViewModels;
using OneDriveFileDownloader.Core.Models;
using System.Linq;
using System.Threading.Tasks;

namespace OneDriveFileDownloader.WinUI.Pages
{
    public sealed partial class ExplorerPage : Page
    {
        private readonly MainViewModel _vm;
        public ExplorerPage(MainViewModel vm)
        {
            this.InitializeComponent();
            _vm = vm;
        }

        public async Task InitializeAsync()
        {
            // load shared items if not present
            if (_vm.SharedItems.Count == 0) await _vm.LoadSharedItemsAsync();

            // build a simple tree (top-level shared items as roots)
            foreach (var s in _vm.SharedItems)
            {
                var root = new TreeViewNode { Content = s };
                FolderTree.RootNodes.Add(root);
            }

            FolderTree.ItemInvoked += async (s, e) =>
            {
                if (e.InvokedItem is TreeViewNode n)
                {
                    if (n.Content is SharedItemInfo si)
                    {
                        // show immediate children in list and populate tree nodes
                        var children = await _vm.GetChildrenAsync(si);
                        ContentsList.Items.Clear();
                        n.Children.Clear();
                        foreach (var c in children)
                        {
                            ContentsList.Items.Add(c);
                            n.Children.Add(new TreeViewNode { Content = new DriveItemNode(c) });
                        }
                    }
                    else if (n.Content is DriveItemNode dn)
                    {
                        if (dn.IsFolder)
                        {
                            await _vm.ExpandNodeAsync(dn);
                            // add tree nodes for children
                            n.Children.Clear();
                            foreach (var c in dn.Children)
                            {
                                n.Children.Add(new TreeViewNode { Content = c });
                            }
                        }
                        else
                        {
                            // file clicked: show file in contents list
                            ContentsList.Items.Clear();
                            ContentsList.Items.Add(dn.Item);
                        }
                    }
                }
            };

            ContentsList.ItemClick += (s, e) =>
            {
                // set selected item (handled via UI events for download/scan)
            };

            ScanFolderButton.Click += async (s, e) =>
            {
                if (ContentsList.SelectedItem is DriveItemInfo di && di.IsFolder)
                {
                    var results = await _vm.ScanFolderAsync(di);
                    ContentsList.Items.Clear();
                    foreach (var r in results) ContentsList.Items.Add(r.File);
                }
            };

            DownloadSelectedButton.Click += async (s, e) =>
            {
                if (ContentsList.SelectedItem is DriveItemInfo fi && !fi.IsFolder)
                {
                    var item = new DownloadItemViewModel(fi);
                    await _vm.DownloadAsync(item);
                }
            };
        }
    }
}
