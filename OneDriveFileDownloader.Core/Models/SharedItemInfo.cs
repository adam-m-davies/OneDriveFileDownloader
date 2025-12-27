namespace OneDriveFileDownloader.Core.Models
{
	public class SharedItemInfo
	{
		public required string Id { get; set; }
		public required string Name { get; set; }
		public required string RemoteDriveId { get; set; }
		public required string RemoteItemId { get; set; }
		public bool IsFolder { get; set; }
		public long? Size { get; set; }
		public string Sha1Hash { get; set; }
	}
}
