using OneDriveFileDownloader.Core.Models;
using System.Collections.ObjectModel;

namespace OneDriveFileDownloader.UI.ViewModels
{
	public class DriveItemNode : ViewModelBase
	{
		public DriveItemInfo Item { get; set; }
		public bool IsFolder => Item?.IsFolder ?? false;
		public ObservableCollection<DriveItemNode> Children { get; } = new ObservableCollection<DriveItemNode>();
		
		public event System.Action OnExpanded;

		private bool _isExpanded;
		public bool IsExpanded 
		{ 
			get => _isExpanded; 
			set 
			{ 
				if (Set(ref _isExpanded, value) && value)
				{
					OnExpanded?.Invoke();
				}
			} 
		}

		public DriveItemNode(DriveItemInfo item)
		{
			Item = item;
		}
	}
}
