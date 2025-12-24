using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using OneDriveFileDownloader.Core.Interfaces;
using OneDriveFileDownloader.Core.Models;
using OneDriveFileDownloader.Core.Services;

class Program
{
    static async Task Main(string[] args)
    {
        Console.WriteLine("OneDrive File Downloader sample (personal accounts).\n");

        Console.Write("Enter Azure app ClientId (app registration that allows personal accounts): ");
        var clientId = Console.ReadLine().Trim();

        var svc = new OneDriveService();
        svc.Configure(clientId);

        Console.WriteLine("Authenticating... (browser will open or device code displayed)");
        var user = await svc.AuthenticateInteractiveAsync();
        Console.WriteLine($"Signed in as: {user}\n");

        var shared = await svc.ListSharedWithMeAsync();
        if (!shared.Any())
        {
            Console.WriteLine("No items shared with this account.");
            return;
        }

        Console.WriteLine("Shared items:");
        for (int i = 0; i < shared.Count; i++) Console.WriteLine($"[{i}] {shared[i].Name}");
        Console.Write("Choose index of folder to scan: ");
        if (!int.TryParse(Console.ReadLine(), out var idx) || idx < 0 || idx >= shared.Count)
        {
            Console.WriteLine("Invalid choice");
            return;
        }

        var chosen = shared[idx];
        Console.WriteLine($"Listing children of: {chosen.Name}...");
        var children = await svc.ListChildrenAsync(chosen);

        var videoExts = new[] { ".mp4", ".mov", ".mkv", ".avi", ".wmv" };
        var videos = children.Where(c => !c.IsFolder && videoExts.Any(e => c.Name.EndsWith(e, StringComparison.OrdinalIgnoreCase))).ToList();
        Console.WriteLine($"Found {videos.Count} video files.");

        var dbPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "downloads.db");
        using var repo = new SqliteDownloadRepository(dbPath);

        Console.Write("Enter local folder to save downloads (will be created): ");
        var outFolder = Console.ReadLine().Trim();
        Directory.CreateDirectory(outFolder);

        foreach (var file in videos)
        {
            Console.WriteLine($"Processing {file.Name}...");

            // If hash exists on file metadata, check quickly
            if (!string.IsNullOrEmpty(file.Sha1Hash) && await repo.HasHashAsync(file.Sha1Hash))
            {
                Console.WriteLine("  Already downloaded (hash match). Skipping.");
                continue;
            }

            // otherwise download to memory/temp to compute hash
            var tempPath = Path.GetTempFileName();
            using (var fs = File.Create(tempPath))
            {
                var downloadInfo = await svc.DownloadFileAsync(file, fs);
                // check again with computed hash
                if (!string.IsNullOrEmpty(downloadInfo.Sha1Hash) && await repo.HasHashAsync(downloadInfo.Sha1Hash))
                {
                    Console.WriteLine("  Duplicate found after hash check. Removing temp file.");
                    fs.Close();
                    File.Delete(tempPath);
                    continue;
                }

                // move to final location
                var dest = Path.Combine(outFolder, file.Name);
                fs.Close();
                if (File.Exists(dest))
                {
                    // Ensure unique filename
                    var unique = Path.Combine(outFolder, Path.GetFileNameWithoutExtension(file.Name) + "_" + Guid.NewGuid().ToString("n").Substring(0,8) + Path.GetExtension(file.Name));
                    dest = unique;
                }
                File.Move(tempPath, dest);

                var rec = new OneDriveFileDownloader.Core.Models.DownloadRecord
                {
                    FileId = file.Id,
                    Sha1Hash = downloadInfo.Sha1Hash,
                    FileName = file.Name,
                    Size = downloadInfo.Size,
                    LocalPath = dest
                };

                await repo.AddRecordAsync(rec);
                Console.WriteLine($"  Downloaded and recorded: {dest}");
            }
        }

        Console.WriteLine("Done.");
    }
}
