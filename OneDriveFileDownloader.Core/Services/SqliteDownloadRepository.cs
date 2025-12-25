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

		public async Task<IList<DownloadRecord>> GetRecentAsync(int count = 20)
		{
			var list = new System.Collections.Generic.List<DownloadRecord>();
			using var cmd = _connection.CreateCommand();
			cmd.CommandText = "SELECT id, fileId, sha1Hash, fileName, size, downloadedAtUtc, localPath FROM downloads ORDER BY downloadedAtUtc DESC LIMIT $count";
			cmd.Parameters.AddWithValue("$count", count);
			using var reader = await cmd.ExecuteReaderAsync();
			while (await reader.ReadAsync())
			{
				var rec = new DownloadRecord
				{
					Id = Guid.TryParse(reader.GetString(0), out var gid) ? gid : Guid.NewGuid(),
					FileId = reader.GetString(1),
					Sha1Hash = reader.GetString(2),
					FileName = reader.GetString(3),
					Size = reader.IsDBNull(4) ? null : (long?)reader.GetInt64(4),
					DownloadedAtUtc = DateTime.Parse(reader.GetString(5)),
					LocalPath = reader.GetString(6)
				};
				list.Add(rec);
			}
			return list;
		}

		public void Dispose()
		{
			_connection?.Dispose();
		}
	}
}
