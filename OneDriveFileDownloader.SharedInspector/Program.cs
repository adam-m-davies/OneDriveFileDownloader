using System.Net.Http.Headers;
using System.Reflection;
using System.Text.Json;
using Microsoft.Identity.Client;
using OneDriveFileDownloader.Core.Services;

namespace OneDriveFileDownloader.SharedInspector;

/// <summary>
/// Console app to test and debug the sharedWithMe API without the full UI.
/// This helps isolate Graph API issues from the Avalonia UI.
/// </summary>
class Program
{
    private static HttpClient _http = new();
    private static string _accessToken = "";
    private static readonly string[] _scopes = new[] { "Files.Read.All", "User.Read" };

    static async Task Main(string[] args)
    {
        Console.WriteLine("=== OneDrive SharedWithMe Inspector ===");
        Console.WriteLine();

        // Load settings to get client ID
        var settings = SettingsStore.Load();
        if (string.IsNullOrEmpty(settings.LastClientId))
        {
            Console.WriteLine("No client ID found in settings.");
            Console.Write("Enter your Azure AD App Registration Client ID: ");
            var clientId = Console.ReadLine()?.Trim();
            if (string.IsNullOrEmpty(clientId))
            {
                Console.WriteLine("Client ID is required.");
                return;
            }
            settings.LastClientId = clientId;
            SettingsStore.Save(settings);
        }

        Console.WriteLine($"Using Client ID: {settings.LastClientId}");
        Console.WriteLine($"Scopes: {string.Join(", ", _scopes)}");
        Console.WriteLine();

        // Create our own MSAL client for direct access to the token
        var pca = PublicClientApplicationBuilder
            .Create(settings.LastClientId)
            .WithAuthority(AzureCloudInstance.AzurePublic, "consumers")
            .WithRedirectUri("http://localhost")
            .Build();

        AuthenticationResult authResult = null;
        var accounts = await pca.GetAccountsAsync();
        var firstAccount = accounts.FirstOrDefault();

        try
        {
            if (firstAccount != null)
            {
                Console.WriteLine("Attempting silent authentication...");
                authResult = await pca.AcquireTokenSilent(_scopes, firstAccount).ExecuteAsync();
            }
        }
        catch (MsalUiRequiredException)
        {
            // Silent auth failed, need interactive
        }

        if (authResult == null)
        {
            Console.WriteLine("Starting interactive authentication...");
            Console.WriteLine("(A browser window will open or you'll see a device code)");
            Console.WriteLine();

            try
            {
                authResult = await pca.AcquireTokenInteractive(_scopes)
                    .WithUseEmbeddedWebView(false)
                    .ExecuteAsync();
            }
            catch
            {
                // Fallback to device code
                authResult = await pca.AcquireTokenWithDeviceCode(_scopes, callback =>
                {
                    Console.WriteLine(callback.Message);
                    return Task.CompletedTask;
                }).ExecuteAsync();
            }
        }

        _accessToken = authResult.AccessToken;
        Console.WriteLine($"Authenticated as: {authResult.Account.Username}");
        Console.WriteLine($"Token expiry: {authResult.ExpiresOn}");
        Console.WriteLine();

        Console.WriteLine("=== RAW API EXPLORATION ===");
        Console.WriteLine();

        // Test 1: sharedWithMe without allowexternal
        await TestEndpoint("1. sharedWithMe (no params)", 
            "https://graph.microsoft.com/v1.0/me/drive/sharedWithMe");

        // Test 2: sharedWithMe WITH allowexternal
        await TestEndpoint("2. sharedWithMe (allowexternal=true)", 
            "https://graph.microsoft.com/v1.0/me/drive/sharedWithMe?allowexternal=true");

        // Test 3: Check the user's root drive special folder "Shared with Me"
        await TestEndpoint("3. Root drive 'shared' special folder",
            "https://graph.microsoft.com/v1.0/me/drive/special/shared");

        // Test 4: List all drives the user has access to
        await TestEndpoint("4. All accessible drives",
            "https://graph.microsoft.com/v1.0/me/drives");

        // Test 5: Check root drive for any shared items
        await TestEndpoint("5. Root drive children",
            "https://graph.microsoft.com/v1.0/me/drive/root/children");

        // Test 6: Beta API - sometimes has more features
        await TestEndpoint("6. BETA sharedWithMe",
            "https://graph.microsoft.com/beta/me/drive/sharedWithMe?allowexternal=true");

        // Test 7: Dump FULL raw JSON from sharedWithMe to see everything
        Console.WriteLine("--- 7. FULL RAW JSON from sharedWithMe ---");
        await DumpFullJson("https://graph.microsoft.com/v1.0/me/drive/sharedWithMe?allowexternal=true");

        // Test 8: Try the "recent" endpoint - might include shared items user accessed
        await TestEndpoint("8. Recent items (may include accessed shared items)",
            "https://graph.microsoft.com/v1.0/me/drive/recent?$top=20");

        // Test 9: Try to list activities on the drive (might show shares)
        await TestEndpoint("9. Drive activities",
            "https://graph.microsoft.com/v1.0/me/drive/activities");
        
        // Test 10: Check if there's a "following" collection
        await TestEndpoint("10. Following items",
            "https://graph.microsoft.com/v1.0/me/drive/following");
        
        // Test 11: Beta API - check for delta with sharing info
        await TestEndpoint("11. BETA sharedWithMe with expand",
            "https://graph.microsoft.com/beta/me/drive/sharedWithMe?$expand=permissions");

        // Test 12: Explore the other drives found in /me/drives
        Console.WriteLine("--- 12. Exploring all accessible drives ---");
        await ExploreAllDrives();

        Console.WriteLine();
        Console.WriteLine("=== END RAW API EXPLORATION ===");
        Console.WriteLine();

        // Allow user to test sharing URLs
        Console.WriteLine("=== TEST SHARING URL ACCESS ===");
        Console.WriteLine("If you have a OneDrive sharing URL, paste it below to test.");
        Console.WriteLine("Or press Enter to skip.");
        Console.Write("Sharing URL: ");
        var shareUrl = Console.ReadLine()?.Trim();
        
        if (!string.IsNullOrEmpty(shareUrl))
        {
            Console.WriteLine();
            Console.WriteLine("Testing GetSharedItemFromUrlAsync...");
            var sharedItem = await svc.GetSharedItemFromUrlAsync(shareUrl);
            if (sharedItem != null)
            {
                Console.WriteLine($"SUCCESS! Found: {sharedItem.Name}");
                Console.WriteLine($"  DriveId: {sharedItem.RemoteDriveId}");
                Console.WriteLine($"  ItemId:  {sharedItem.RemoteItemId}");
                Console.WriteLine($"  IsFolder: {sharedItem.IsFolder}");
                
                if (sharedItem.IsFolder)
                {
                    Console.WriteLine("  Listing children...");
                    var children = await svc.ListChildrenAsync(sharedItem);
                    Console.WriteLine($"  Found {children.Count} children:");
                    foreach (var child in children.Take(10))
                    {
                        var type = child.IsFolder ? "[folder]" : "[file]";
                        Console.WriteLine($"    {type} {child.Name}");
                    }
                    if (children.Count > 10)
                        Console.WriteLine($"    ... and {children.Count - 10} more");
                }
            }
            else
            {
                Console.WriteLine("Could not access the sharing URL. Check the logs above for details.");
            }
        }
        Console.WriteLine();

        // Now run through the OneDriveService for comparison
        var svc = new OneDriveService();
        svc.DiagnosticLog += msg => Console.WriteLine($"  [DEBUG] {msg}");
        svc.Configure(settings.LastClientId);
        
        // Authenticate through the service (should pick up cached token)
        await svc.AuthenticateSilentAsync();

        // List shared items using the service
        Console.WriteLine("Listing items shared with you (via OneDriveService)...");
        Console.WriteLine("============================================");
        
        try
        {
            var items = await svc.ListSharedWithMeAsync();

            Console.WriteLine();
            Console.WriteLine($"=== RESULTS: {items.Count} items ===");
            Console.WriteLine();

            if (items.Count == 0)
            {
                Console.WriteLine("No shared items found!");
                Console.WriteLine();
                Console.WriteLine("Possible reasons:");
                Console.WriteLine("  1. No items have been shared WITH this account by other users");
                Console.WriteLine("  2. The app registration doesn't have correct permissions (needs Files.Read.All)");
                Console.WriteLine("  3. You need to re-consent to new permissions (try signing out and back in)");
            }
            else
            {
                foreach (var item in items)
                {
                    var typeLabel = item.IsFolder ? "[FOLDER]" : "[FILE]";
                    var sizeLabel = item.Size.HasValue ? $"{item.Size:N0} bytes" : "unknown size";
                    Console.WriteLine($"{typeLabel} {item.Name}");
                    Console.WriteLine($"        DriveId: {item.RemoteDriveId}");
                    Console.WriteLine($"        ItemId:  {item.RemoteItemId}");
                    Console.WriteLine($"        Size:    {sizeLabel}");
                    
                    if (item.IsFolder)
                    {
                        Console.WriteLine($"        (Listing children...)");
                        try
                        {
                            var children = await svc.ListChildrenAsync(item);
                            Console.WriteLine($"        Children: {children.Count} items");
                            foreach (var child in children.Take(5))
                            {
                                var childType = child.IsFolder ? "[DIR]" : "[FILE]";
                                Console.WriteLine($"          {childType} {child.Name}");
                            }
                            if (children.Count > 5)
                            {
                                Console.WriteLine($"          ... and {children.Count - 5} more");
                            }
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"        Error listing children: {ex.Message}");
                        }
                    }
                    Console.WriteLine();
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
            Console.WriteLine();
            Console.WriteLine("Full exception:");
            Console.WriteLine(ex.ToString());
        }

        Console.WriteLine();
        Console.WriteLine("Press any key to exit...");
        Console.ReadKey();
    }

    static async Task ExploreAllDrives()
    {
        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Get, "https://graph.microsoft.com/v1.0/me/drives");
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _accessToken);
            
            var res = await _http.SendAsync(req);
            var raw = await res.Content.ReadAsStringAsync();
            
            if (!res.IsSuccessStatusCode)
            {
                Console.WriteLine($"Error fetching drives: {res.StatusCode}");
                return;
            }
            
            using var doc = JsonDocument.Parse(raw);
            if (!doc.RootElement.TryGetProperty("value", out var arr)) return;
            
            foreach (var drive in arr.EnumerateArray())
            {
                var driveId = drive.GetProperty("id").GetString();
                var driveName = drive.TryGetProperty("name", out var n) ? n.GetString() : "(unnamed)";
                var driveType = drive.TryGetProperty("driveType", out var dt) ? dt.GetString() : "unknown";
                var owner = "";
                if (drive.TryGetProperty("owner", out var ownerProp))
                {
                    if (ownerProp.TryGetProperty("user", out var userProp))
                    {
                        owner = userProp.TryGetProperty("displayName", out var dn) ? dn.GetString() : "";
                    }
                }
                
                Console.WriteLine($"  Drive: {driveName} (type={driveType}, owner={owner})");
                Console.WriteLine($"    ID: {driveId}");
                
                // Try to list root children of this drive
                try
                {
                    using var childReq = new HttpRequestMessage(HttpMethod.Get, 
                        $"https://graph.microsoft.com/v1.0/drives/{driveId}/root/children?$top=5");
                    childReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _accessToken);
                    
                    var childRes = await _http.SendAsync(childReq);
                    var childRaw = await childRes.Content.ReadAsStringAsync();
                    
                    if (childRes.IsSuccessStatusCode)
                    {
                        using var childDoc = JsonDocument.Parse(childRaw);
                        if (childDoc.RootElement.TryGetProperty("value", out var children))
                        {
                            var count = children.GetArrayLength();
                            Console.WriteLine($"    Root children (first 5 of {count}+):");
                            foreach (var child in children.EnumerateArray())
                            {
                                var childName = child.TryGetProperty("name", out var cn) ? cn.GetString() : "(unnamed)";
                                var isFolder = child.TryGetProperty("folder", out _);
                                Console.WriteLine($"      {(isFolder ? "[folder]" : "[file]")} {childName}");
                            }
                        }
                    }
                    else
                    {
                        Console.WriteLine($"    Could not list children: {childRes.StatusCode}");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"    Error exploring drive: {ex.Message}");
                }
                
                Console.WriteLine();
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
        }
    }

    static async Task DumpFullJson(string url)
    {
        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Get, url);
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _accessToken);
            
            var res = await _http.SendAsync(req);
            var raw = await res.Content.ReadAsStringAsync();
            
            Console.WriteLine($"Status: {(int)res.StatusCode} {res.StatusCode}");
            
            // Pretty-print the JSON
            using var doc = JsonDocument.Parse(raw);
            var options = new JsonSerializerOptions { WriteIndented = true };
            var pretty = JsonSerializer.Serialize(doc.RootElement, options);
            
            // Limit output to avoid flooding console
            if (pretty.Length > 5000)
            {
                Console.WriteLine(pretty.Substring(0, 5000));
                Console.WriteLine($"\n... (truncated, total {pretty.Length} chars)");
            }
            else
            {
                Console.WriteLine(pretty);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
        }
        Console.WriteLine();
    }

    static async Task TestEndpoint(string testName, string url)
    {
        Console.WriteLine($"--- {testName} ---");
        Console.WriteLine($"URL: {url}");
        
        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Get, url);
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _accessToken);
            
            var res = await _http.SendAsync(req);
            var raw = await res.Content.ReadAsStringAsync();
            
            Console.WriteLine($"Status: {(int)res.StatusCode} {res.StatusCode}");
            
            if (!res.IsSuccessStatusCode)
            {
                Console.WriteLine($"Error: {raw}");
            }
            else
            {
                using var doc = JsonDocument.Parse(raw);
                
                // Check for value array (list response)
                if (doc.RootElement.TryGetProperty("value", out var arr))
                {
                    var count = arr.GetArrayLength();
                    Console.WriteLine($"Items returned: {count}");
                    
                    // Show first few items
                    var index = 0;
                    foreach (var el in arr.EnumerateArray())
                    {
                        if (index >= 5) 
                        {
                            Console.WriteLine($"  ... and {count - 5} more items");
                            break;
                        }
                        
                        var name = el.TryGetProperty("name", out var n) ? n.GetString() : "(no name)";
                        var hasRemote = el.TryGetProperty("remoteItem", out _);
                        var isFolder = el.TryGetProperty("folder", out _);
                        var driveType = el.TryGetProperty("driveType", out var dt) ? dt.GetString() : null;
                        
                        var info = new List<string>();
                        if (hasRemote) info.Add("hasRemoteItem");
                        if (isFolder) info.Add("folder");
                        if (driveType != null) info.Add($"driveType={driveType}");
                        
                        var infoStr = info.Count > 0 ? $" [{string.Join(", ", info)}]" : "";
                        Console.WriteLine($"  - {name}{infoStr}");
                        index++;
                    }
                }
                else
                {
                    // Single item response
                    var name = doc.RootElement.TryGetProperty("name", out var n) ? n.GetString() : "(no name)";
                    Console.WriteLine($"Single item: {name}");
                    
                    // Show key properties
                    foreach (var prop in doc.RootElement.EnumerateObject().Take(5))
                    {
                        Console.WriteLine($"  {prop.Name}: {prop.Value.ToString().Substring(0, Math.Min(50, prop.Value.ToString().Length))}...");
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Exception: {ex.Message}");
        }
        
        Console.WriteLine();
    }
}
