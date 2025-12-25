using System;

namespace OneDriveFileDownloader.Core.Models
{
	public class UserProfile
	{
		public string DisplayName { get; set; }
		public byte[] ThumbnailBytes { get; set; }

		public UserProfile()
		{
			DisplayName = string.Empty;
			ThumbnailBytes = Array.Empty<byte>();
		}
	}
}
