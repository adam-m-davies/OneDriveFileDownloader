using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Microsoft.Graph;
using Microsoft.Identity.Client;
using OneDriveFileDownloader.Core.Interfaces;
using OneDriveFileDownloader.Core.Models;

namespace OneDriveFileDownloader.Core.Services
{
	public class OneDriveService : IOneDriveService
	{
		private string _clientId;
		private IPublicClientApplication _pca;
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
				// Explicit redirect URI: ensure this is registered in your app registration (http://localhost)
				.WithRedirectUri("http://localhost")
				.Build();

			BindCache(_pca.UserTokenCache);
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

				// Interactive auth completed; token cache is seeded.

				return result.Account.Username ?? result.Account.HomeAccountId?.Identifier ?? "(unknown)";
			}
			catch (Microsoft.Identity.Client.MsalServiceException se) when ((se.ErrorCode != null && se.ErrorCode.IndexOf("invalid_request", StringComparison.OrdinalIgnoreCase) >= 0) || (se.Message != null && se.Message.IndexOf("redirect", StringComparison.OrdinalIgnoreCase) >= 0))
			{
				if (deviceCodeCallback != null) deviceCodeCallback("Interactive sign-in failed (redirect URI mismatch). Falling back to device code flow...");
				else Console.WriteLine("Interactive sign-in failed due to redirect URI mismatch. Ensure your app registration includes 'http://localhost' (or 'https://login.microsoftonline.com/common/oauth2/nativeclient') as a redirect URI for your client id. Falling back to device code flow.");

				var deviceResult = await _pca.AcquireTokenWithDeviceCode(_scopes, callback =>
				{
					if (deviceCodeCallback != null) deviceCodeCallback(callback.Message);
					else Console.WriteLine(callback.Message);
					return Task.CompletedTask;
				}).ExecuteAsync();

				return deviceResult.Account.Username ?? deviceResult.Account.HomeAccountId?.Identifier ?? "(unknown)";
			}
			catch (MsalException ex)
			{
				if (deviceCodeCallback != null) deviceCodeCallback($"Interactive sign-in failed: {ex.Message}. Falling back to device code flow...");
				// other MSAL exception - fallback to device code
				var deviceResult = await _pca.AcquireTokenWithDeviceCode(_scopes, callback =>
				{
					if (deviceCodeCallback != null) deviceCodeCallback(callback.Message);
					else Console.WriteLine(callback.Message);
					return Task.CompletedTask;
				}).ExecuteAsync();

				return deviceResult.Account.Username ?? deviceResult.Account.HomeAccountId?.Identifier ?? "(unknown)";
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
			var token = await EnsureAccessTokenAsync();
			if (string.IsNullOrEmpty(token)) 
				throw new InvalidOperationException("Unable to acquire access token to list shared items.");

			// Use the sharedWithMe endpoint - this returns items shared WITH the current user BY other users
			// Note: This API is deprecated but still works until November 2026
			// IMPORTANT: allowexternal=true is required to see items shared from OTHER Microsoft accounts/tenants!
			var requestUrl = "https://graph.microsoft.com/v1.0/me/drive/sharedWithMe?allowexternal=true";
			int pageCount = 0;
			int totalItemsFromApi = 0;
			int itemsWithRemoteItem = 0;
			int itemsSuccessfullyParsed = 0;

			Log($"Starting sharedWithMe API call...");

			while (!string.IsNullOrEmpty(requestUrl))
			{
				pageCount++;
				using var req = new System.Net.Http.HttpRequestMessage(System.Net.Http.HttpMethod.Get, requestUrl);
				req.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
				
				var res = await _http.SendAsync(req);
				var raw = await res.Content.ReadAsStringAsync();
				
				if (!res.IsSuccessStatusCode)
				{
					Log($"API Error: {res.StatusCode} - {raw}");
					throw new HttpRequestException($"Graph API returned {res.StatusCode}: {raw}");
				}

				using var doc = System.Text.Json.JsonDocument.Parse(raw);
				
				if (doc.RootElement.TryGetProperty("value", out var arr))
				{
					var itemsInPage = arr.GetArrayLength();
					totalItemsFromApi += itemsInPage;
					Log($"Page {pageCount}: Received {itemsInPage} items from API");

					foreach (var el in arr.EnumerateArray())
					{
						// Get item name for logging
						var itemName = el.TryGetProperty("name", out var nameProp) ? nameProp.GetString() : "(unknown)";
						
						// Check if this item has a remoteItem property
						// According to MS docs, shared items should always have remoteItem
						if (!el.TryGetProperty("remoteItem", out var remote))
						{
							// Items without remoteItem might be items the user shared WITH others (not shared TO user)
							// or might indicate a permissions issue
							Log($"  SKIP '{itemName}': No remoteItem property - may be user's own shared item or permissions issue");
							continue;
						}
						
						itemsWithRemoteItem++;

						// Extract drive ID - try multiple locations
						string driveId = null;
						
						// First try remoteItem.parentReference.driveId (most common for shared items)
						if (remote.TryGetProperty("parentReference", out var parentRef))
						{
							if (parentRef.TryGetProperty("driveId", out var parentDriveId) && 
							    parentDriveId.ValueKind == System.Text.Json.JsonValueKind.String)
							{
								driveId = parentDriveId.GetString();
							}
						}
						
						// Fallback: try top-level parentReference if remote doesn't have it
						if (string.IsNullOrEmpty(driveId) && el.TryGetProperty("parentReference", out var topParentRef))
						{
							if (topParentRef.TryGetProperty("driveId", out var topDriveId) && 
							    topDriveId.ValueKind == System.Text.Json.JsonValueKind.String)
							{
								driveId = topDriveId.GetString();
							}
						}
						
						if (string.IsNullOrEmpty(driveId))
						{
							Log($"  SKIP '{itemName}': Could not extract driveId");
							continue;
						}

						// Get the remote item ID
						if (!remote.TryGetProperty("id", out var remoteIdProp) || 
						    string.IsNullOrEmpty(remoteIdProp.GetString()))
						{
							Log($"  SKIP '{itemName}': No remote item ID");
							continue;
						}
						
						var remoteId = remoteIdProp.GetString();
						var sharedId = el.TryGetProperty("id", out var sharedIdProp) ? sharedIdProp.GetString() : null;

						// Get name from remoteItem or fall back to top-level name
						var name = remote.TryGetProperty("name", out var remoteName) && 
						           remoteName.ValueKind == System.Text.Json.JsonValueKind.String
							? remoteName.GetString()
							: itemName;

						// Determine if folder
						bool isFolder = remote.TryGetProperty("folder", out _);

						// Get size
						long? size = remote.TryGetProperty("size", out var sizeProp) ? sizeProp.GetInt64() : (long?)null;

						// Get SHA1 hash if available
						var sha1 = string.Empty;
						if (remote.TryGetProperty("file", out var file) && 
						    file.TryGetProperty("hashes", out var hashes) && 
						    hashes.TryGetProperty("sha1Hash", out var hash))
						{
							sha1 = hash.GetString() ?? string.Empty;
						}

						// Try to get owner/sharer info for logging
						var ownerInfo = "(unknown owner)";
						if (el.TryGetProperty("createdBy", out var createdBy) && 
						    createdBy.TryGetProperty("user", out var creatorUser))
						{
							var displayName = creatorUser.TryGetProperty("displayName", out var dn) ? dn.GetString() : null;
							var email = creatorUser.TryGetProperty("email", out var em) ? em.GetString() : null;
							ownerInfo = displayName ?? email ?? "(unknown)";
						}
						else if (remote.TryGetProperty("createdBy", out var remoteCreatedBy) && 
						         remoteCreatedBy.TryGetProperty("user", out var remoteCreator))
						{
							var displayName = remoteCreator.TryGetProperty("displayName", out var dn) ? dn.GetString() : null;
							ownerInfo = displayName ?? "(unknown)";
						}

						itemsSuccessfullyParsed++;
						Log($"  FOUND '{name}' (driveId={driveId}, itemId={remoteId}, isFolder={isFolder}, owner={ownerInfo})");

						items.Add(new SharedItemInfo
						{
							Id = sharedId ?? remoteId,
							Name = name ?? string.Empty,
							RemoteDriveId = driveId,
							RemoteItemId = remoteId,
							IsFolder = isFolder,
							Size = size,
							Sha1Hash = sha1
						});
					}
				}
				else
				{
					Log($"Page {pageCount}: No 'value' array in response");
				}

				// Check for pagination
				if (doc.RootElement.TryGetProperty("@odata.nextLink", out var nextLink) && 
				    nextLink.ValueKind == System.Text.Json.JsonValueKind.String)
				{
					requestUrl = nextLink.GetString();
					Log($"Following pagination to next page...");
				}
				else
				{
					requestUrl = null;
				}
			}

			Log($"Summary: {pageCount} pages, {totalItemsFromApi} total items from API, " +
			    $"{itemsWithRemoteItem} with remoteItem, {itemsSuccessfullyParsed} successfully parsed");

			return items;
		}

		/// <summary>
		/// Access a shared item using its OneDrive sharing URL.
		/// This uses the Microsoft Graph Shares API to decode the sharing URL and access the item.
		/// </summary>
		public async Task<SharedItemInfo> GetSharedItemFromUrlAsync(string sharingUrl)
		{
			if (string.IsNullOrWhiteSpace(sharingUrl))
				return null;

			var token = await EnsureAccessTokenAsync();
			if (string.IsNullOrEmpty(token))
				throw new InvalidOperationException("Unable to acquire access token.");

			// Encode the sharing URL according to Microsoft's spec:
			// 1. Base64 encode the URL
			// 2. Convert to unpadded base64url format (remove =, replace / with _, replace + with -)
			// 3. Prepend "u!"
			var base64Value = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(sharingUrl));
			var encodedUrl = "u!" + base64Value.TrimEnd('=').Replace('/', '_').Replace('+', '-');

			Log($"Accessing shared item via URL...");
			Log($"  Original URL: {sharingUrl}");
			Log($"  Encoded token: {encodedUrl.Substring(0, Math.Min(50, encodedUrl.Length))}...");

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
