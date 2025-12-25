namespace OneDriveFileDownloader.Core.Models
{
	public class DriveItemInfo
	{
		public required string Id { get; set; }
		public required string DriveId { get; set; }
		public required string Name { get; set; }
		public long? Size { get; set; }
		public string Sha1Hash { get; set; }
		public bool IsFolder { get; set; }
	}
}
