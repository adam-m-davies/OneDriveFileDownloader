namespace OneDriveFileDownloader.Core.Models
{
	public class SharedItemInfo
	{
		public required string Id { get; set; }
		public required string Name { get; set; }
		public required string RemoteDriveId { get; set; }
		public required string RemoteItemId { get; set; }
	}
}
