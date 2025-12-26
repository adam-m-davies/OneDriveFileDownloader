using System.Collections.Generic;
using System.Threading.Tasks;
using OneDriveFileDownloader.Core.Models;

namespace OneDriveFileDownloader.Core.Interfaces
{
	public interface IOneDriveService
	{
		/// <summary>
		/// Event for diagnostic logging. Consumers can subscribe to see internal API call details.
		/// </summary>
		event System.Action<string> DiagnosticLog;

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
		/// NOTE: Due to Microsoft Graph API limitations, this may not return all items shown in OneDrive web UI.
		/// Use GetSharedItemFromUrlAsync to access items via their sharing URL.
		/// </summary>
		Task<IList<SharedItemInfo>> ListSharedWithMeAsync();

		/// <summary>
		/// Access a shared item using its OneDrive sharing URL.
		/// This can access items that don't appear in ListSharedWithMeAsync() but were shared via link.
		/// </summary>
		/// <param name="sharingUrl">A OneDrive sharing URL (e.g., https://1drv.ms/... or https://onedrive.live.com/...)</param>
		/// <returns>The shared item info, or null if the URL is invalid or inaccessible</returns>
		Task<SharedItemInfo> GetSharedItemFromUrlAsync(string sharingUrl);

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
