using OneDriveFileDownloader.Core.Models;
using OneDriveFileDownloader.Core.Interfaces;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace OneDriveFileDownloader.Tests
{
	public class FakeOneDriveService : IOneDriveService
	{
		public List<SharedItemInfo> Shared = new List<SharedItemInfo>();
		public Dictionary<string, List<DriveItemInfo>> Children = new Dictionary<string, List<DriveItemInfo>>();

		public void Configure(string clientId) { }
		public Task<string> AuthenticateInteractiveAsync() => Task.FromResult("fakeuser@example.com");
		public virtual async Task<DownloadResult> DownloadFileAsync(DriveItemInfo file, Stream destination, IProgress<long> progress = null, CancellationToken cancellation = default)
		{
			// simulate streaming progress
			var total = file.Size ?? (4096 * 10);
			var chunk = 4096;
			var sent = 0;
			while (sent < total)
			{
				cancellation.ThrowIfCancellationRequested();
				var toWrite = (int)System.Math.Min(chunk, total - sent);
				var buf = new byte[toWrite];
				await destination.WriteAsync(buf, 0, toWrite, cancellation);
				sent += toWrite;
				progress?.Report(sent);
				await Task.Delay(20, cancellation);
			}
			var sha = "deadbeef";
			return new DownloadResult { Sha1Hash = sha, Size = total };
		}

		public Task<IList<DriveItemInfo>> ListChildrenAsync(SharedItemInfo sharedItem)
		{
			if (Children.TryGetValue(sharedItem.RemoteItemId, out var list)) return Task.FromResult((IList<DriveItemInfo>)list);
			return Task.FromResult((IList<DriveItemInfo>)new List<DriveItemInfo>());
		}

		public Task<IList<SharedItemInfo>> ListSharedWithMeAsync() => Task.FromResult((IList<SharedItemInfo>)Shared);

		public Task<UserProfile> GetUserProfileAsync() => Task.FromResult(new UserProfile { DisplayName = "Fake User" });
	}

	public class FakeOneDriveServiceWithFailure : FakeOneDriveService
	{
		private readonly System.Func<bool> _shouldFail;
		public FakeOneDriveServiceWithFailure(System.Func<bool> shouldFail) { _shouldFail = shouldFail; }

		public override async Task<DownloadResult> DownloadFileAsync(DriveItemInfo file, Stream destination, IProgress<long> progress = null, CancellationToken cancellation = default)
		{
			if (_shouldFail()) throw new System.Exception("Simulated failure");
			return await base.DownloadFileAsync(file, destination, progress, cancellation);
		}
	}
}
