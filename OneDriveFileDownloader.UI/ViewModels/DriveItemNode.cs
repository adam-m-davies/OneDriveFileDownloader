using OneDriveFileDownloader.Core.Models;
using System.Collections.ObjectModel;

namespace OneDriveFileDownloader.UI.ViewModels
{
    public class DriveItemNode
    {
        public DriveItemInfo Item { get; set; }
        public bool IsFolder => Item?.IsFolder ?? false;
        public ObservableCollection<DriveItemNode> Children { get; } = new ObservableCollection<DriveItemNode>();
        public bool IsExpanded { get; set; }

        public DriveItemNode(DriveItemInfo item)
        {
            Item = item;
        }
    }
}