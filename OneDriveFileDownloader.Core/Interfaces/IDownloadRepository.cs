using System.Threading.Tasks;
using System.Collections.Generic;

namespace OneDriveFileDownloader.Core.Interfaces
{
	public interface IDownloadRepository
	{
		Task<bool> HasHashAsync(string sha1Hash);
		Task AddRecordAsync(Models.DownloadRecord record);
		Task<IList<Models.DownloadRecord>> GetRecentAsync(int count = 20);
	}
}
