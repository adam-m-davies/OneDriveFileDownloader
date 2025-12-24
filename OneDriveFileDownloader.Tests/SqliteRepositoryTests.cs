using System.IO;
using System.Threading.Tasks;
using OneDriveFileDownloader.Core.Models;
using OneDriveFileDownloader.Core.Services;
using Xunit;

public class SqliteRepositoryTests
{
    [Fact]
    public async Task AddAndHasHash_Works()
    {
        var db = Path.Combine(Path.GetTempPath(), System.Guid.NewGuid().ToString("n") + ".db");
        try
        {
            using var repo = new SqliteDownloadRepository(db);
            var rec = new DownloadRecord { FileId = "1", Sha1Hash = "abcd", FileName = "f.mp4", Size = 10, LocalPath = "x" };
            await repo.AddRecordAsync(rec);
            var has = await repo.HasHashAsync("abcd");
            Assert.True(has);
            var has2 = await repo.HasHashAsync("notfound");
            Assert.False(has2);
        }
        finally
        {
            try { File.Delete(db); } catch (IOException) { /* ignore, might be locked briefly on cleanup */ }
        }
    }
}