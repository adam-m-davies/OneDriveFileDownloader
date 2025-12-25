using System;

namespace OneDriveFileDownloader.Core.Models
{
	public class DownloadRecord
	{
		public Guid Id { get; set; }
		public required string FileId { get; set; }
		public required string Sha1Hash { get; set; }
		public required string FileName { get; set; }
		public long? Size { get; set; }
		public DateTime DownloadedAtUtc { get; set; }
		public required string LocalPath { get; set; }

		public DownloadRecord()
		{
			Id = Guid.NewGuid();
			DownloadedAtUtc = DateTime.UtcNow;
		}
	}
}
