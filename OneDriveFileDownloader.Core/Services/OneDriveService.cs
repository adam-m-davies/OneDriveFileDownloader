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
		private readonly string[] _scopes = new[] { "Files.Read", "User.Read" };
		private readonly HttpClient _http = new HttpClient();

		public void Configure(string clientId)
		{
			_clientId = clientId ?? throw new ArgumentNullException(nameof(clientId));
			_pca = PublicClientApplicationBuilder
				.Create(_clientId)
				// Explicit redirect URI: ensure this is registered in your app registration (http://localhost)
				.WithRedirectUri("http://localhost")
				.Build();
		}

		public async Task<string> AuthenticateInteractiveAsync()
		{
			if (_pca == null) throw new InvalidOperationException("Call Configure(clientId) first.");

			try
			{
				var result = await _pca.AcquireTokenInteractive(_scopes)
					.WithPrompt(Prompt.SelectAccount)
					.ExecuteAsync();

				// Interactive auth completed; token cache is seeded.

				return result.Account.Username ?? result.Account.HomeAccountId?.Identifier ?? "(unknown)";
			}
			catch (Microsoft.Identity.Client.MsalServiceException se) when ((se.ErrorCode != null && se.ErrorCode.IndexOf("invalid_request", StringComparison.OrdinalIgnoreCase) >= 0) || (se.Message != null && se.Message.IndexOf("redirect", StringComparison.OrdinalIgnoreCase) >= 0))
			{
				Console.WriteLine("Interactive sign-in failed due to redirect URI mismatch. Ensure your app registration includes 'http://localhost' (or 'https://login.microsoftonline.com/common/oauth2/nativeclient') as a redirect URI for your client id. Falling back to device code flow.");

				var deviceResult = await _pca.AcquireTokenWithDeviceCode(_scopes, callback =>
				{
					Console.WriteLine(callback.Message);
					return Task.CompletedTask;
				}).ExecuteAsync();

				return deviceResult.Account.Username ?? deviceResult.Account.HomeAccountId?.Identifier ?? "(unknown)";
			}
			catch (MsalException)
			{
				// other MSAL exception - fallback to device code
				var deviceResult = await _pca.AcquireTokenWithDeviceCode(_scopes, callback =>
				{
					Console.WriteLine(callback.Message);
					return Task.CompletedTask;
				}).ExecuteAsync();

				return deviceResult.Account.Username ?? deviceResult.Account.HomeAccountId?.Identifier ?? "(unknown)";
			}
		}

		public async Task<IList<SharedItemInfo>> ListSharedWithMeAsync()
		{
			var items = new List<SharedItemInfo>();
			// call the "sharedWithMe" function via the SDK if available; otherwise retrieve via raw endpoint
			// call Graph REST endpoint to get items shared with me
			var token = await EnsureAccessTokenAsync();
			using var req = new System.Net.Http.HttpRequestMessage(System.Net.Http.HttpMethod.Get, "https://graph.microsoft.com/v1.0/me/drive/sharedWithMe");
			req.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
			var res = await _http.SendAsync(req);
			res.EnsureSuccessStatusCode();
			var raw = await res.Content.ReadAsStringAsync();
			using var doc = System.Text.Json.JsonDocument.Parse(raw);
			if (doc.RootElement.TryGetProperty("value", out var arr))
			{
				foreach (var el in arr.EnumerateArray())
				{
					if (!el.TryGetProperty("remoteItem", out var remote)) continue;
					if (!remote.TryGetProperty("parentReference", out var parentRef)) continue;
					var driveId = parentRef.GetProperty("driveId").GetString() ?? throw new InvalidDataException("driveId missing from sharedWithMe response");
					var itemId = remote.GetProperty("id").GetString() ?? throw new InvalidDataException("item id missing from sharedWithMe response");
					var id = el.GetProperty("id").GetString() ?? throw new InvalidDataException("id missing from sharedWithMe response");
					var name = el.GetProperty("name").GetString() ?? string.Empty;
					items.Add(new SharedItemInfo { Id = id, Name = name, RemoteDriveId = driveId, RemoteItemId = itemId });
				}
			}

			return items;
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
