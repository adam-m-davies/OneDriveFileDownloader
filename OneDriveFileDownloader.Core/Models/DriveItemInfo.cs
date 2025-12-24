namespace OneDriveFileDownloader.Core.Models
{
    public class DriveItemInfo
    {
        public string Id { get; set; }
        public string DriveId { get; set; }
        public string Name { get; set; }
        public long? Size { get; set; }
        public string Sha1Hash { get; set; }
        public bool IsFolder { get; set; }
    }
}