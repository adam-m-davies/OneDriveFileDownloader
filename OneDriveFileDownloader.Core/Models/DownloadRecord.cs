using System;

namespace OneDriveFileDownloader.Core.Models
{
    public class DownloadRecord
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string FileId { get; set; }
        public string Sha1Hash { get; set; }
        public string FileName { get; set; }
        public long? Size { get; set; }
        public DateTime DownloadedAtUtc { get; set; } = DateTime.UtcNow;
        public string LocalPath { get; set; }
    }
}