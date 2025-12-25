namespace OneDriveFileDownloader.Core.Models
{
    public class DownloadResult
    {
        public required string Sha1Hash { get; set; }
        public long Size { get; set; }
    }
}