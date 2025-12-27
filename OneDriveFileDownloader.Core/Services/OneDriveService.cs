using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Microsoft.Graph;
using Microsoft.Graph.Models;
using Microsoft.Identity.Client;
using Microsoft.Kiota.Abstractions;
using Microsoft.Kiota.Abstractions.Authentication;
using OneDriveFileDownloader.Core.Interfaces;
using OneDriveFileDownloader.Core.Models;
using Prompt = Microsoft.Identity.Client.Prompt;

namespace OneDriveFileDownloader.Core.Services
{
	/// <summary>
	/// Custom authentication provider that uses MSAL tokens for Microsoft Graph SDK
	/// </summary>
	internal class MsalAuthenticationProvider : IAuthenticationProvider
	{
		private readonly Func<Task<string>> _tokenProvider;

		public MsalAuthenticationProvider(Func<Task<string>> tokenProvider)
		{
			_tokenProvider = tokenProvider;
		}

		public async Task AuthenticateRequestAsync(RequestInformation request, Dictionary<string, object> additionalAuthenticationContext = null, CancellationToken cancellationToken = default)
		{
			var token = await _tokenProvider();
			request.Headers.Add("Authorization", $"Bearer {token}");
		}
	}

	public class OneDriveService : IOneDriveService
	{
		private string _clientId;
		private IPublicClientApplication _pca;
		private GraphServiceClient _graphClient;
		// Files.Read.All is required to access shared items - Files.Read alone won't return all shared content
		private readonly string[] _scopes = new[] { "Files.Read.All", "User.Read" };
		private readonly HttpClient _http = new HttpClient();
		
		// Event for diagnostic logging - consumers can subscribe to see what's happening
		public event Action<string> DiagnosticLog;
		private static readonly string CacheFilePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "OneDriveFileDownloader", "msal_cache.bin");

		public void Configure(string clientId)
		{
			_clientId = clientId ?? throw new ArgumentNullException(nameof(clientId));
			_pca = PublicClientApplicationBuilder
				.Create(_clientId)
				// Use "consumers" authority for personal Microsoft accounts
				.WithAuthority(AzureCloudInstance.AzurePublic, "consumers")
				// Explicit redirect URI: ensure this is registered in your app registration (http://localhost)
				.WithRedirectUri("http://localhost")
				.Build();

			BindCache(_pca.UserTokenCache);
			
			// Create GraphServiceClient with our MSAL auth provider
			var authProvider = new MsalAuthenticationProvider(EnsureAccessTokenAsync);
			_graphClient = new GraphServiceClient(authProvider);
		}

		private void BindCache(ITokenCache tokenCache)
		{
			tokenCache.SetBeforeAccess(args =>
			{
				lock (CacheFilePath)
				{
					if (File.Exists(CacheFilePath))
					{
						args.TokenCache.DeserializeMsalV3(File.ReadAllBytes(CacheFilePath));
					}
				}
			});

			tokenCache.SetAfterAccess(args =>
			{
				if (args.HasStateChanged)
				{
					lock (CacheFilePath)
					{
						var dir = Path.GetDirectoryName(CacheFilePath);
						if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
						File.WriteAllBytes(CacheFilePath, args.TokenCache.SerializeMsalV3());
					}
				}
			});
		}

		public async Task<string> AuthenticateInteractiveAsync(Action<string> deviceCodeCallback = null, IntPtr? parentWindow = null)
		{
			if (_pca == null) throw new InvalidOperationException("Call Configure(clientId) first.");

			Log($"Requesting authentication with scopes: {string.Join(", ", _scopes)}");

			// First try silent auth - if we have a cached token, use it
			var accounts = await _pca.GetAccountsAsync();
			var firstAccount = accounts.FirstOrDefault();
			
			if (firstAccount != null)
			{
				try
				{
					Log("Attempting silent authentication with cached token...");
					var silentResult = await _pca.AcquireTokenSilent(_scopes, firstAccount).ExecuteAsync();
					Log($"Silent auth succeeded. Granted scopes: {string.Join(", ", silentResult.Scopes)}");
					return silentResult.Account.Username ?? silentResult.Account.HomeAccountId?.Identifier ?? "(unknown)";
				}
				catch (MsalUiRequiredException)
				{
					Log("Cached token expired, proceeding to interactive auth.");
				}
			}

			// Interactive auth (browser-based)
			try
			{
				var builder = _pca.AcquireTokenInteractive(_scopes)
					.WithPrompt(Prompt.SelectAccount);

				if (parentWindow.HasValue && parentWindow.Value != IntPtr.Zero)
				{
					builder = builder.WithParentActivityOrWindow(parentWindow.Value);
				}

				var result = await builder.ExecuteAsync();
				Log($"Interactive auth succeeded. Granted scopes: {string.Join(", ", result.Scopes)}");

				return result.Account.Username ?? result.Account.HomeAccountId?.Identifier ?? "(unknown)";
			}
			catch (MsalClientException ex) when (ex.ErrorCode == "authentication_canceled")
			{
				Log("User canceled authentication.");
				throw; // Let caller handle cancellation
			}
		}

		public async Task<string> AuthenticateSilentAsync()
		{
			if (_pca == null) return null;
			var accounts = await _pca.GetAccountsAsync();
			var first = accounts.FirstOrDefault();
			if (first == null) return null;

			try
			{
				var result = await _pca.AcquireTokenSilent(_scopes, first).ExecuteAsync();
				return result.Account.Username ?? result.Account.HomeAccountId?.Identifier ?? "(unknown)";
			}
			catch { return null; }
		}

		public async Task SignOutAsync()
		{
			if (_pca == null) return;
			
			Log("Signing out and clearing token cache...");
			
			var accounts = await _pca.GetAccountsAsync();
			foreach (var a in accounts)
			{
				await _pca.RemoveAsync(a);
			}
			
			// Also delete the cache file to ensure fresh auth with new scopes
			try
			{
				if (File.Exists(CacheFilePath))
				{
					File.Delete(CacheFilePath);
					Log("Token cache file deleted.");
				}
			}
			catch (Exception ex)
			{
				Log($"Warning: Could not delete cache file: {ex.Message}");
			}
		}

		private void Log(string message)
		{
			DiagnosticLog?.Invoke(message);
		}

		public async Task<IList<SharedItemInfo>> ListSharedWithMeAsync()
		{
			var items = new List<SharedItemInfo>();
			
			if (_graphClient == null)
				throw new InvalidOperationException("Call Configure(clientId) first.");

			Log($"Starting sharedWithMe API call using Microsoft Graph SDK...");

			try
			{
				// First, get the user's drive ID
				var drive = await _graphClient.Me.Drive.GetAsync();
				if (drive == null || string.IsNullOrEmpty(drive.Id))
				{
					Log("Could not retrieve user's drive");
					return items;
				}
				
				Log($"User's drive ID: {drive.Id}");

				// Use the Microsoft Graph SDK - sharedWithMe is accessed via Drives[driveId].SharedWithMe
				var sharedResult = await _graphClient.Drives[drive.Id].SharedWithMe
					.GetAsSharedWithMeGetResponseAsync(config =>
					{
						config.QueryParameters.Select = new[] { "id", "name", "size", "folder", "file", "remoteItem", "createdBy", "parentReference" };
					});

				if (sharedResult?.Value == null)
				{
					Log("No items returned from sharedWithMe");
					return items;
				}

				Log($"Received {sharedResult.Value.Count} items from API");

				foreach (var item in sharedResult.Value)
				{
					var itemName = item.Name ?? "(unknown)";
					
					// Check for remoteItem - shared items should have this
					if (item.RemoteItem == null)
					{
						Log($"  SKIP '{itemName}': No remoteItem property");
						continue;
					}

					var remote = item.RemoteItem;
					
					// Extract drive ID from remoteItem.parentReference
					var driveId = remote.ParentReference?.DriveId;
					if (string.IsNullOrEmpty(driveId))
					{
						// Fallback to top-level parentReference
						driveId = item.ParentReference?.DriveId;
					}

					if (string.IsNullOrEmpty(driveId))
					{
						Log($"  SKIP '{itemName}': Could not extract driveId");
						continue;
					}

					var remoteId = remote.Id;
					if (string.IsNullOrEmpty(remoteId))
					{
						Log($"  SKIP '{itemName}': No remote item ID");
						continue;
					}

					var name = remote.Name ?? itemName;
					var isFolder = remote.Folder != null;
					var size = remote.Size;
					var sha1 = remote.File?.Hashes?.Sha1Hash ?? string.Empty;
					var ownerInfo = item.CreatedBy?.User?.DisplayName ?? 
					               remote.CreatedBy?.User?.DisplayName ?? 
					               "(unknown owner)";

					Log($"  FOUND '{name}' (driveId={driveId}, itemId={remoteId}, isFolder={isFolder}, owner={ownerInfo})");

					items.Add(new SharedItemInfo
					{
						Id = item.Id ?? remoteId,
						Name = name,
						RemoteDriveId = driveId,
						RemoteItemId = remoteId,
						IsFolder = isFolder,
						Size = size,
						Sha1Hash = sha1
					});
				}

				Log($"Summary: {sharedResult.Value.Count} total items from API, {items.Count} successfully parsed");
			}
			catch (Exception ex)
			{
				Log($"Error calling sharedWithMe: {ex.Message}");
				throw;
			}

			return items;
		}

		/// <summary>
		/// Access a shared item using its OneDrive sharing URL or web URL.
		/// Supports multiple URL formats:
		/// 1. Standard sharing links (1drv.ms, onedrive.live.com/redir)
		/// 2. OneDrive web URLs with photosData parameter containing /shared/DRIVEID!ITEMID
		/// 3. Direct drive/item reference URLs
		/// </summary>
		public async Task<SharedItemInfo> GetSharedItemFromUrlAsync(string sharingUrl)
		{
			if (string.IsNullOrWhiteSpace(sharingUrl))
				return null;

			var token = await EnsureAccessTokenAsync();
			if (string.IsNullOrEmpty(token))
				throw new InvalidOperationException("Unable to acquire access token.");

			Log($"Parsing URL: {sharingUrl}");

			// Try to extract drive ID and item ID directly from the URL
			// Pattern 1: photosData=/shared/DRIVEID!ITEMID (URL encoded as %2Fshared%2F)
			// Pattern 2: cid=DRIVEID (older format)
			// Pattern 3: id=DRIVEID!ITEMID
			var (driveId, itemId) = TryParseOneDriveWebUrl(sharingUrl);
			
			if (!string.IsNullOrEmpty(driveId) && !string.IsNullOrEmpty(itemId))
			{
				Log($"  Extracted direct IDs: driveId={driveId}, itemId={itemId}");
				return await GetItemByDirectAccess(driveId, itemId, token);
			}

			// Fall back to the Shares API for standard sharing links
			return await GetItemViaSharingToken(sharingUrl, token);
		}

		/// <summary>
		/// Try to parse OneDrive web URLs to extract drive ID and item ID directly.
		/// </summary>
		private (string driveId, string itemId) TryParseOneDriveWebUrl(string url)
		{
			try
			{
				// URL decode first
				var decoded = Uri.UnescapeDataString(url);
				
				// Pattern: photosData=/shared/DRIVEID!ITEMID or photosData=%2Fshared%2FDRIVEID!ITEMID
				var photosDataMatch = System.Text.RegularExpressions.Regex.Match(
					decoded, 
					@"/shared/([A-Fa-f0-9]+)!(\d+)",
					System.Text.RegularExpressions.RegexOptions.IgnoreCase);
				
				if (photosDataMatch.Success)
				{
					return (photosDataMatch.Groups[1].Value, $"{photosDataMatch.Groups[1].Value}!{photosDataMatch.Groups[2].Value}");
				}

				// Pattern: id=DRIVEID!ITEMID in query string
				var idMatch = System.Text.RegularExpressions.Regex.Match(
					decoded,
					@"[?&]id=([A-Fa-f0-9]+)!(\d+)",
					System.Text.RegularExpressions.RegexOptions.IgnoreCase);
				
				if (idMatch.Success)
				{
					return (idMatch.Groups[1].Value, $"{idMatch.Groups[1].Value}!{idMatch.Groups[2].Value}");
				}

				// Pattern: Direct format like DRIVEID!ITEMID (if user pastes just the ID)
				var directMatch = System.Text.RegularExpressions.Regex.Match(
					url.Trim(),
					@"^([A-Fa-f0-9]+)!(\d+)$",
					System.Text.RegularExpressions.RegexOptions.IgnoreCase);
				
				if (directMatch.Success)
				{
					return (directMatch.Groups[1].Value, $"{directMatch.Groups[1].Value}!{directMatch.Groups[2].Value}");
				}
			}
			catch (Exception ex)
			{
				Log($"  Error parsing URL: {ex.Message}");
			}
			
			return (null, null);
		}

		/// <summary>
		/// Access an item directly using drive ID and item ID.
		/// </summary>
		private async Task<SharedItemInfo> GetItemByDirectAccess(string driveId, string itemId, string token)
		{
			var requestUrl = $"https://graph.microsoft.com/v1.0/drives/{driveId}/items/{itemId}";
			Log($"  Direct access: {requestUrl}");

			using var req = new System.Net.Http.HttpRequestMessage(System.Net.Http.HttpMethod.Get, requestUrl);
			req.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

			var res = await _http.SendAsync(req);
			var raw = await res.Content.ReadAsStringAsync();

			if (!res.IsSuccessStatusCode)
			{
				Log($"  Direct access error: {res.StatusCode} - {raw}");
				return null;
			}

			using var doc = System.Text.Json.JsonDocument.Parse(raw);
			var el = doc.RootElement;

			var id = el.TryGetProperty("id", out var idProp) ? idProp.GetString() : null;
			var name = el.TryGetProperty("name", out var nameProp) ? nameProp.GetString() : null;
			var isFolder = el.TryGetProperty("folder", out _);
			long? size = el.TryGetProperty("size", out var sizeProp) ? sizeProp.GetInt64() : (long?)null;

			// Get SHA1 hash if available
			var sha1 = string.Empty;
			if (el.TryGetProperty("file", out var file) && 
			    file.TryGetProperty("hashes", out var hashes) && 
			    hashes.TryGetProperty("sha1Hash", out var hash))
			{
				sha1 = hash.GetString() ?? string.Empty;
			}

			Log($"  SUCCESS: '{name}' (driveId={driveId}, itemId={id}, isFolder={isFolder})");

			return new SharedItemInfo
			{
				Id = id,
				Name = name ?? string.Empty,
				RemoteDriveId = driveId,
				RemoteItemId = id,
				IsFolder = isFolder,
				Size = size,
				Sha1Hash = sha1
			};
		}

		/// <summary>
		/// Access an item via the Shares API using an encoded sharing token.
		/// </summary>
		private async Task<SharedItemInfo> GetItemViaSharingToken(string sharingUrl, string token)
		{
			// Encode the sharing URL according to Microsoft's spec:
			// 1. Base64 encode the URL
			// 2. Convert to unpadded base64url format (remove =, replace / with _, replace + with -)
			// 3. Prepend "u!"
			var base64Value = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(sharingUrl));
			var encodedUrl = "u!" + base64Value.TrimEnd('=').Replace('/', '_').Replace('+', '-');

			Log($"  Using Shares API with encoded token...");

			// Call the Shares API to get the driveItem
			// Using the "Prefer: redeemSharingLink" header grants durable access
			var requestUrl = $"https://graph.microsoft.com/v1.0/shares/{encodedUrl}/driveItem";
			
			using var req = new System.Net.Http.HttpRequestMessage(System.Net.Http.HttpMethod.Get, requestUrl);
			req.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
			req.Headers.Add("Prefer", "redeemSharingLink");

			var res = await _http.SendAsync(req);
			var raw = await res.Content.ReadAsStringAsync();

			if (!res.IsSuccessStatusCode)
			{
				Log($"  Shares API error: {res.StatusCode} - {raw}");
				return null;
			}

			using var doc = System.Text.Json.JsonDocument.Parse(raw);
			var el = doc.RootElement;

			// Extract the item details
			var id = el.TryGetProperty("id", out var idProp) ? idProp.GetString() : null;
			var name = el.TryGetProperty("name", out var nameProp) ? nameProp.GetString() : null;
			var isFolder = el.TryGetProperty("folder", out _);
			long? size = el.TryGetProperty("size", out var sizeProp) ? sizeProp.GetInt64() : (long?)null;

			// Get drive ID from parentReference
			string driveId = null;
			if (el.TryGetProperty("parentReference", out var parentRef))
			{
				driveId = parentRef.TryGetProperty("driveId", out var driveIdProp) ? driveIdProp.GetString() : null;
			}

			if (string.IsNullOrEmpty(id) || string.IsNullOrEmpty(driveId))
			{
				Log($"  Could not extract item ID or drive ID from response");
				return null;
			}

			// Get SHA1 hash if available
			var sha1 = string.Empty;
			if (el.TryGetProperty("file", out var file) && 
			    file.TryGetProperty("hashes", out var hashes) && 
			    hashes.TryGetProperty("sha1Hash", out var hash))
			{
				sha1 = hash.GetString() ?? string.Empty;
			}

			Log($"  SUCCESS: '{name}' (driveId={driveId}, itemId={id}, isFolder={isFolder})");

			return new SharedItemInfo
			{
				Id = id,
				Name = name ?? string.Empty,
				RemoteDriveId = driveId,
				RemoteItemId = id,
				IsFolder = isFolder,
				Size = size,
				Sha1Hash = sha1
			};
		}

		public async Task<IList<DriveItemInfo>> ListChildrenAsync(SharedItemInfo sharedItem)
		{
			var list = new List<DriveItemInfo>();
			var token = await EnsureAccessTokenAsync();
			using var req = new System.Net.Http.HttpRequestMessage(System.Net.Http.HttpMethod.Get, $"https://graph.microsoft.com/v1.0/drives/{sharedItem.RemoteDriveId}/items/{sharedItem.RemoteItemId}/children");
			req.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
			var res = await _http.SendAsync(req);
			res.EnsureSuccessStatusCode();
			var raw = await res.Content.ReadAsStringAsync();
			using var doc = System.Text.Json.JsonDocument.Parse(raw);
			if (doc.RootElement.TryGetProperty("value", out var arr))
			{
				foreach (var el in arr.EnumerateArray())
				{
					var id = el.GetProperty("id").GetString() ?? throw new InvalidDataException("child id missing from ListChildren response");
					var name = el.GetProperty("name").GetString() ?? string.Empty;
					var size = el.TryGetProperty("size", out var s) ? s.GetInt64() : (long?)null;
					string sha1 = string.Empty;
					if (el.TryGetProperty("file", out var file) && file.TryGetProperty("hashes", out var hashes) && hashes.TryGetProperty("sha1Hash", out var hh))
					{
						sha1 = hh.GetString();
					}
					var isFolder = el.TryGetProperty("folder", out var _);
					list.Add(new DriveItemInfo { Id = id, DriveId = sharedItem.RemoteDriveId, Name = name, Size = size, Sha1Hash = sha1, IsFolder = isFolder });
				}
			}

			return list;
		}

		public async Task<UserProfile> GetUserProfileAsync()
		{
			var token = await EnsureAccessTokenAsync();
			var profile = new UserProfile();

			using (var req = new System.Net.Http.HttpRequestMessage(System.Net.Http.HttpMethod.Get, "https://graph.microsoft.com/v1.0/me"))
			{
				req.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
				var res = await _http.SendAsync(req);
				if (res.IsSuccessStatusCode)
				{
					var raw = await res.Content.ReadAsStringAsync();
					using var doc = System.Text.Json.JsonDocument.Parse(raw);
					if (doc.RootElement.TryGetProperty("displayName", out var dn)) profile.DisplayName = dn.GetString();
				}
			}

			// try to fetch thumbnail bytes (this endpoint returns raw image bytes)
			try
			{
				using var req2 = new System.Net.Http.HttpRequestMessage(System.Net.Http.HttpMethod.Get, "https://graph.microsoft.com/v1.0/me/photo/$value");
				req2.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
				var res2 = await _http.SendAsync(req2);
				if (res2.IsSuccessStatusCode)
				{
					var bytes = await res2.Content.ReadAsByteArrayAsync();
					if (bytes != null && bytes.Length > 0) profile.ThumbnailBytes = bytes;
				}
			}
			catch
			{
				// ignore thumbnail fetch errors (permissions, not found, etc.)
			}

			return profile;
		}

		public async Task<Models.DownloadResult> DownloadFileAsync(DriveItemInfo file, Stream destination, IProgress<long> progress = null, CancellationToken cancellation = default)
		{
			// use drive and item ids
			// download content via REST
			var token = await EnsureAccessTokenAsync();
			using var req = new System.Net.Http.HttpRequestMessage(System.Net.Http.HttpMethod.Get, $"https://graph.microsoft.com/v1.0/drives/{file.DriveId}/items/{file.Id}/content");
			req.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
			var res = await _http.SendAsync(req, System.Net.Http.HttpCompletionOption.ResponseHeadersRead, cancellation);
			res.EnsureSuccessStatusCode();
			using var stream = await res.Content.ReadAsStreamAsync(cancellation);

			using (var sha1 = SHA1.Create())
			{
				var buffer = new byte[81920];
				int read;
				long total = 0;
				while ((read = await stream.ReadAsync(buffer, 0, buffer.Length, cancellation)) > 0)
				{
					await destination.WriteAsync(buffer, 0, read, cancellation);
					sha1.TransformBlock(buffer, 0, read, null, 0);
					total += read;
					try { progress?.Report(total); } catch { }
				}

				sha1.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
				var hashBytes = sha1.Hash ?? Array.Empty<byte>();
				var sha1Hash = BitConverter.ToString(hashBytes).Replace("-", "").ToLowerInvariant();

				destination.Seek(0, SeekOrigin.Begin);

				return new Models.DownloadResult
				{
					Sha1Hash = sha1Hash,
					Size = file.Size ?? destination.Length
				};
			}
		}

		private async Task<string> EnsureAccessTokenAsync()
		{
			try
			{
				if (_pca == null) throw new InvalidOperationException("Call Configure(clientId) before requesting tokens.");
				var accounts = await _pca.GetAccountsAsync().ConfigureAwait(false);
				var result = await _pca.AcquireTokenSilent(_scopes, accounts.FirstOrDefault()).ExecuteAsync().ConfigureAwait(false);
				return result.AccessToken;
			}
			catch (MsalUiRequiredException)
			{
				if (_pca == null) throw new InvalidOperationException("Call Configure(clientId) before requesting tokens.");
				var deviceResult = await _pca.AcquireTokenWithDeviceCode(_scopes, callback =>
				{
					Console.WriteLine(callback.Message);
					return Task.CompletedTask;
				}).ExecuteAsync().ConfigureAwait(false);
				return deviceResult.AccessToken;
			}
		}
	}
}
