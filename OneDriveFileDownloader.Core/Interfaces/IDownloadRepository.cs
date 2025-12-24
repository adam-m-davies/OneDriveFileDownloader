using System.Threading.Tasks;

namespace OneDriveFileDownloader.Core.Interfaces
{
    public interface IDownloadRepository
    {
        Task<bool> HasHashAsync(string sha1Hash);
        Task AddRecordAsync(Models.DownloadRecord record);
    }
}