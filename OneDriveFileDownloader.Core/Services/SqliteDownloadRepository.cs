using System;
using System.Data;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using OneDriveFileDownloader.Core.Interfaces;
using OneDriveFileDownloader.Core.Models;

namespace OneDriveFileDownloader.Core.Services
{
    public class SqliteDownloadRepository : IDownloadRepository, IDisposable
    {
        private readonly string _dbPath;
        private readonly SqliteConnection _connection;

        public SqliteDownloadRepository(string dbPath)
        {
            _dbPath = dbPath ?? "downloads.db";
            var create = !File.Exists(_dbPath);
            _connection = new SqliteConnection($"Data Source={_dbPath}");
            _connection.Open();

            if (create)
            {
                using var cmd = _connection.CreateCommand();
                cmd.CommandText = @"
CREATE TABLE IF NOT EXISTS downloads (
    id TEXT PRIMARY KEY,
    fileId TEXT,
    sha1Hash TEXT,
    fileName TEXT,
    size INTEGER,
    downloadedAtUtc TEXT,
    localPath TEXT
);
";
                cmd.ExecuteNonQuery();
            }
        }

        public async Task AddRecordAsync(DownloadRecord record)
        {
            using var cmd = _connection.CreateCommand();
            cmd.CommandText = @"INSERT INTO downloads (id, fileId, sha1Hash, fileName, size, downloadedAtUtc, localPath)
VALUES ($id, $fileId, $sha1Hash, $fileName, $size, $downloadedAtUtc, $localPath);";
            cmd.Parameters.AddWithValue("$id", record.Id.ToString());
            cmd.Parameters.AddWithValue("$fileId", record.FileId ?? string.Empty);
            cmd.Parameters.AddWithValue("$sha1Hash", record.Sha1Hash ?? string.Empty);
            cmd.Parameters.AddWithValue("$fileName", record.FileName ?? string.Empty);
            cmd.Parameters.AddWithValue("$size", record.Size ?? 0);
            cmd.Parameters.AddWithValue("$downloadedAtUtc", record.DownloadedAtUtc.ToString("o"));
            cmd.Parameters.AddWithValue("$localPath", record.LocalPath ?? string.Empty);
            await cmd.ExecuteNonQueryAsync();
        }

        public async Task<bool> HasHashAsync(string sha1Hash)
        {
            if (string.IsNullOrEmpty(sha1Hash)) return false;
            using var cmd = _connection.CreateCommand();
            cmd.CommandText = "SELECT COUNT(1) FROM downloads WHERE sha1Hash = $sha1";
            cmd.Parameters.AddWithValue("$sha1", sha1Hash);
            var result = await cmd.ExecuteScalarAsync();
            if (result is long l) return l > 0;
            if (result is int i) return i > 0;
            return false;
        }

        public void Dispose()
        {
            _connection?.Dispose();
        }
    }
}