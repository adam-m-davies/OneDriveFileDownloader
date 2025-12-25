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
                var node = new TreeViewNode { Content = s };
                FolderTree.RootNodes.Add(node);
            }

            FolderTree.ItemInvoked += async (s, e) =>
            {
                if (e.InvokedItem is TreeViewNode n && n.Content is SharedItemInfo si)
                {
                    var children = await _vm.GetChildrenAsync(si);
                    ContentsList.Items.Clear();
                    foreach (var c in children) ContentsList.Items.Add(c);
                }
            };
        }
    }
}
