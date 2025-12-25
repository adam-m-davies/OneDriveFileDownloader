using System.Collections.Generic;
using System.Threading.Tasks;
using OneDriveFileDownloader.Core.Models;

namespace OneDriveFileDownloader.Core.Interfaces
{
	public interface IOneDriveService
	{
		/// <summary>
		/// Set client ID used for interactive authentication. Must be an app registration client ID that allows personal MSA.
		/// </summary>
		void Configure(string clientId);

		/// <summary>
		/// Runs interactive login (opens browser) or device code fallback and returns the signed-in account display name.
		/// </summary>
		Task<string> AuthenticateInteractiveAsync(System.Action<string> deviceCodeCallback = null, System.IntPtr? parentWindow = null);

		/// <summary>
		/// Attempts to sign in silently using cached tokens.
		/// </summary>
		Task<string> AuthenticateSilentAsync();

		/// <summary>
		/// Signs out the current user and clears the token cache.
		/// </summary>
		Task SignOutAsync();

		/// <summary>
		/// Returns profile information for the signed-in user, including display name and optional thumbnail bytes.
		/// </summary>
		Task<UserProfile> GetUserProfileAsync();

		/// <summary>
		/// List items shared with the signed in user (folders or files).
		/// </summary>
		Task<IList<SharedItemInfo>> ListSharedWithMeAsync();

		/// <summary>
		/// Given a shared (remote) item, list children (files/folders). For folders, returns children; for files, returns single item.
		/// </summary>
		Task<IList<DriveItemInfo>> ListChildrenAsync(SharedItemInfo sharedItem);

		/// <summary>
		/// Downloads the specified file to the destination stream. Returns a DownloadResult describing file metadata such as hash.
		/// Optionally reports progress in bytes read.
		/// </summary>
		Task<DownloadResult> DownloadFileAsync(DriveItemInfo file, System.IO.Stream destination, IProgress<long> progress = null, System.Threading.CancellationToken cancellation = default);
	}
}
