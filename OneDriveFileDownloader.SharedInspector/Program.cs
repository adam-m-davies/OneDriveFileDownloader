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

        // Create MSAL client WITH token cache to share with the main app
        var cacheFilePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "OneDriveFileDownloader", 
            "msal_cache.bin");
        
        var pca = PublicClientApplicationBuilder
            .Create(settings.LastClientId)
            .WithAuthority(AzureCloudInstance.AzurePublic, "consumers")
            .WithRedirectUri("http://localhost")
            .Build();

        // Bind token cache (same as OneDriveService uses)
        pca.UserTokenCache.SetBeforeAccess(args =>
        {
            if (File.Exists(cacheFilePath))
                args.TokenCache.DeserializeMsalV3(File.ReadAllBytes(cacheFilePath));
        });
        pca.UserTokenCache.SetAfterAccess(args =>
        {
            if (args.HasStateChanged)
            {
                var dir = Path.GetDirectoryName(cacheFilePath);
                if (!Directory.Exists(dir)) Directory.CreateDirectory(dir!);
                File.WriteAllBytes(cacheFilePath, args.TokenCache.SerializeMsalV3());
            }
        });

        AuthenticationResult authResult = null;
        var accounts = await pca.GetAccountsAsync();
        var firstAccount = accounts.FirstOrDefault();

        // Try silent auth first (uses cached tokens)
        if (firstAccount != null)
        {
            try
            {
                Console.WriteLine("Attempting silent authentication (using cached token)...");
                authResult = await pca.AcquireTokenSilent(_scopes, firstAccount).ExecuteAsync();
                Console.WriteLine("Silent auth succeeded!");
            }
            catch (MsalUiRequiredException)
            {
                Console.WriteLine("Cached token expired, need interactive login.");
            }
        }

        // If silent failed, do interactive (browser-based, no device code)
        if (authResult == null)
        {
            Console.WriteLine("Starting interactive authentication (browser)...");
            Console.WriteLine();

            authResult = await pca.AcquireTokenInteractive(_scopes)
                .WithUseEmbeddedWebView(false)
                .WithPrompt(Prompt.SelectAccount)
                .ExecuteAsync();
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

        // Test 12: Try explicitly requesting shared items including all details
        Console.WriteLine("--- 12. sharedWithMe with $select=* ---");
        await DumpFullJson("https://graph.microsoft.com/v1.0/me/drive/sharedWithMe?$select=*");

        // Test 13: Check if items appear in search
        await TestEndpoint("13. Search for 'cats' across all drives",
            "https://graph.microsoft.com/v1.0/me/drive/root/search(q='cats')");

        // Test 13b: Search for 'more cats' specifically
        await TestEndpoint("13b. Search for 'more cats'",
            "https://graph.microsoft.com/v1.0/me/drive/root/search(q='more cats')");

        // Test 13c: Dump full details of search for 'cats'
        Console.WriteLine("--- 13c. FULL Search results for 'cats' ---");
        await DumpFullJson("https://graph.microsoft.com/v1.0/me/drive/root/search(q='cats')?$top=10");

        // Test 13d: Check root children for shortcuts (items with remoteItem)
        Console.WriteLine("--- 13d. Looking for shortcuts in root ---");
        await LookForRemoteItems("https://graph.microsoft.com/v1.0/me/drive/root/children?$top=100");

        // Test 14: Try the bundles endpoint (sometimes shared items are bundled)
        await TestEndpoint("14. Drive bundles",
            "https://graph.microsoft.com/v1.0/me/drive/bundles");

        // Test 15: Check items with shared facet in root
        await TestEndpoint("15. Items with shared facet",
            "https://graph.microsoft.com/v1.0/me/drive/root/children?$filter=shared ne null");

        // Test 16: Explore the other drives found in /me/drives
        Console.WriteLine("--- 16. Exploring all accessible drives ---");
        await ExploreAllDrives();

        // Test 17: Try DIRECT ACCESS to an item by drive ID and item ID
        // This tests if we can access shared items when we know their exact location
        Console.WriteLine("--- 17. Direct access test using known IDs from OneDrive web URL ---");
        Console.WriteLine("  To test direct access, provide a OneDrive URL with embedded IDs.");
        Console.WriteLine("  Example format: photosData=/shared/DRIVEID!ITEMID");
        Console.Write("  Enter driveId (or press Enter to skip): ");
        var testDriveId = Console.ReadLine()?.Trim();
        if (!string.IsNullOrEmpty(testDriveId))
        {
            Console.Write("  Enter itemId: ");
            var testItemId = Console.ReadLine()?.Trim();
            if (!string.IsNullOrEmpty(testItemId))
            {
                Console.WriteLine($"  Testing direct access to: driveId={testDriveId}, itemId={testItemId}");
                await TestEndpoint("17a. Direct item access",
                    $"https://graph.microsoft.com/v1.0/drives/{testDriveId}/items/{testItemId}");
                await TestEndpoint("17b. Direct item children (if folder)",
                    $"https://graph.microsoft.com/v1.0/drives/{testDriveId}/items/{testItemId}/children");
            }
        }
        else
        {
            Console.WriteLine("  Skipped direct access test.");
        }
        
        // Test 18: Try the /shares endpoint with the sharing URL encoded as a token
        Console.WriteLine("--- 18. Try /shares endpoint with base64 encoded URL ---");
        Console.Write("  Enter a OneDrive URL to test (or press Enter to skip): ");
        var oneDriveUrl = Console.ReadLine()?.Trim();
        if (!string.IsNullOrEmpty(oneDriveUrl))
        {
            var base64Value = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(oneDriveUrl));
            var shareToken = "u!" + base64Value.TrimEnd('=').Replace('/', '_').Replace('+', '-');
            Console.WriteLine($"  Share token: {shareToken}");
            await TestEndpoint("18a. Shares endpoint - driveItem",
                $"https://graph.microsoft.com/v1.0/shares/{shareToken}/driveItem");
            await TestEndpoint("18b. Shares endpoint - root",
                $"https://graph.microsoft.com/v1.0/shares/{shareToken}/root");
        }
        else
        {
            Console.WriteLine("  Skipped /shares endpoint test.");
        }

        // Test 19: IMPORTANT - Find ALL shortcuts in the entire drive (recursive search)
        // These shortcuts might point to shared folders!
        Console.WriteLine("--- 19. Search for ALL items with remoteItem (shortcuts) ---");
        await FindAllShortcuts();

        Console.WriteLine();
        Console.WriteLine("=== END RAW API EXPLORATION ===");
        Console.WriteLine();

        // Now run through the OneDriveService for comparison
        var svc = new OneDriveService();
        svc.DiagnosticLog += msg => Console.WriteLine($"  [DEBUG] {msg}");
        svc.Configure(settings.LastClientId);
        
        // Authenticate through the service (should pick up cached token)
        await svc.AuthenticateSilentAsync();

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

    static async Task FindAllShortcuts()
    {
        // Use search to find ALL items, then filter for ones with remoteItem
        // This might reveal shortcuts to shared folders that aren't in the root
        Console.WriteLine("  Searching entire drive for items that might be shortcuts...");
        
        var shortcuts = new List<(string name, string remoteDriveId, string remoteItemId, string path)>();
        
        try
        {
            // Search for common shared folder indicators
            // Can't directly search for remoteItem, so we search broadly and filter
            var searchTerms = new[] { "*" }; // Wildcard search doesn't work, but let's try delta
            
            // Try delta to get ALL items, looking for remoteItem
            Console.WriteLine("  Using delta endpoint to enumerate all items...");
            var deltaUrl = "https://graph.microsoft.com/v1.0/me/drive/root/delta?$select=id,name,remoteItem,parentReference,folder&$top=1000";
            var pageCount = 0;
            var totalItems = 0;
            
            while (!string.IsNullOrEmpty(deltaUrl) && pageCount < 10) // Limit pages to avoid too much data
            {
                pageCount++;
                using var req = new HttpRequestMessage(HttpMethod.Get, deltaUrl);
                req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _accessToken);
                
                var res = await _http.SendAsync(req);
                if (!res.IsSuccessStatusCode)
                {
                    Console.WriteLine($"  Delta failed: {res.StatusCode}");
                    break;
                }
                
                var raw = await res.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(raw);
                
                if (doc.RootElement.TryGetProperty("value", out var arr))
                {
                    foreach (var el in arr.EnumerateArray())
                    {
                        totalItems++;
                        
                        // Check if this item has a remoteItem (meaning it's a shortcut)
                        if (el.TryGetProperty("remoteItem", out var remote))
                        {
                            var name = el.TryGetProperty("name", out var n) ? n.GetString() : "(unknown)";
                            var remoteDriveId = "";
                            var remoteItemId = remote.TryGetProperty("id", out var rid) ? rid.GetString() : "";
                            var path = "";
                            
                            if (remote.TryGetProperty("parentReference", out var pr))
                            {
                                remoteDriveId = pr.TryGetProperty("driveId", out var did) ? did.GetString() : "";
                            }
                            
                            if (el.TryGetProperty("parentReference", out var localPr))
                            {
                                path = localPr.TryGetProperty("path", out var p) ? p.GetString() : "";
                            }
                            
                            var isFolder = remote.TryGetProperty("folder", out _);
                            
                            shortcuts.Add((name, remoteDriveId, remoteItemId, path));
                            Console.WriteLine($"  ** SHORTCUT: '{name}' {(isFolder ? "[FOLDER]" : "[FILE]")}");
                            Console.WriteLine($"      Location: {path}");
                            Console.WriteLine($"      Points to: driveId={remoteDriveId}, itemId={remoteItemId}");
                        }
                    }
                }
                
                // Check for next page
                if (doc.RootElement.TryGetProperty("@odata.nextLink", out var nextLink))
                {
                    deltaUrl = nextLink.GetString();
                    Console.WriteLine($"  (Page {pageCount}, scanned {totalItems} items so far, found {shortcuts.Count} shortcuts...)");
                }
                else
                {
                    deltaUrl = null;
                }
            }
            
            Console.WriteLine($"\n  === SHORTCUT SUMMARY ===");
            Console.WriteLine($"  Total items scanned: {totalItems}");
            Console.WriteLine($"  Shortcuts found: {shortcuts.Count}");
            
            if (shortcuts.Count > 0)
            {
                Console.WriteLine("\n  These shortcuts point to OTHER DRIVES and could be shared content:");
                foreach (var (name, driveId, itemId, path) in shortcuts)
                {
                    Console.WriteLine($"    - {name}");
                    Console.WriteLine($"        Path: {path}");
                    Console.WriteLine($"        Remote: drives/{driveId}/items/{itemId}");
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  Error: {ex.Message}");
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

    static async Task LookForRemoteItems(string url)
    {
        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Get, url);
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _accessToken);
            
            var res = await _http.SendAsync(req);
            var raw = await res.Content.ReadAsStringAsync();
            
            if (!res.IsSuccessStatusCode)
            {
                Console.WriteLine($"Error: {res.StatusCode}");
                return;
            }
            
            using var doc = JsonDocument.Parse(raw);
            if (!doc.RootElement.TryGetProperty("value", out var arr)) return;
            
            var shortcuts = new List<(string name, string driveId, string itemId)>();
            
            foreach (var item in arr.EnumerateArray())
            {
                var name = item.TryGetProperty("name", out var n) ? n.GetString() : "(unnamed)";
                
                // Check for remoteItem property - this indicates a shortcut to another drive
                if (item.TryGetProperty("remoteItem", out var remote))
                {
                    var remoteDriveId = "";
                    var remoteItemId = remote.TryGetProperty("id", out var rid) ? rid.GetString() : "";
                    
                    if (remote.TryGetProperty("parentReference", out var pr))
                    {
                        remoteDriveId = pr.TryGetProperty("driveId", out var did) ? did.GetString() : "";
                    }
                    
                    shortcuts.Add((name, remoteDriveId, remoteItemId));
                    Console.WriteLine($"  SHORTCUT FOUND: '{name}'");
                    Console.WriteLine($"    -> driveId: {remoteDriveId}");
                    Console.WriteLine($"    -> itemId:  {remoteItemId}");
                }
            }
            
            Console.WriteLine($"\nTotal shortcuts found: {shortcuts.Count}");
            
            // If we found shortcuts, we can use these to access shared folders!
            if (shortcuts.Count > 0)
            {
                Console.WriteLine("\n*** THESE SHORTCUTS CAN BE USED TO ACCESS SHARED FOLDERS! ***");
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
