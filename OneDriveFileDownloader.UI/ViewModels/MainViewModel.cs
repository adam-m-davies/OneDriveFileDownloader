using OneDriveFileDownloader.Core.Interfaces;
using OneDriveFileDownloader.Core.Models;
using System;
using System.Collections.ObjectModel;
using OneDriveFileDownloader.Core.Services;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Threading;

namespace OneDriveFileDownloader.UI.ViewModels
{
	public class MainViewModel : ViewModelBase
	{
		private readonly IOneDriveService _svc;
		private readonly IDownloadRepository _repo;
		private readonly Settings _settings;

		public ObservableCollection<SharedItemInfo> SharedItems { get; } = new ObservableCollection<SharedItemInfo>();
		public ObservableCollection<DownloadItemViewModel> Videos { get; } = new ObservableCollection<DownloadItemViewModel>();
		private DownloadItemViewModel? _selectedVideo;
		public DownloadItemViewModel? SelectedVideo { get => _selectedVideo; set => Set(ref _selectedVideo, value); }
		public ObservableCollection<DownloadRecord> RecentDownloads { get; } = new ObservableCollection<DownloadRecord>();

		public ObservableCollection<DriveItemNode> FolderRoots { get; } = new ObservableCollection<DriveItemNode>();

		private DriveItemNode? _selectedNode;
		public DriveItemNode? SelectedNode
		{
			get => _selectedNode;
			set
			{
				if (Set(ref _selectedNode, value) && value != null)
				{
					if (ScanOnSelection)
					{
						_ = ScanSelectedNodeAsync(value);
					}
				}
			}
		}

		public RelayCommand DownloadCommand { get; }
		public RelayCommand CancelCommand { get; }
		public RelayCommand RetryCommand { get; }
		public RelayCommand OpenDownloadsCommand { get; }
		public RelayCommand ScanLastCommand { get; }
		public RelayCommand ScanFolderCommand { get; }
		public RelayCommand SignInCommand { get; }
		public RelayCommand SignOutCommand { get; }
		public RelayCommand OpenSettingsCommand { get; }
		public RelayCommand NavigateCommand { get; }

		private bool _isAuthenticated;
		public bool IsAuthenticated { get => _isAuthenticated; set => Set(ref _isAuthenticated, value); }

		private string _statusText = string.Empty;
		public string StatusText { get => _statusText; set => Set(ref _statusText, value); }

		private string _userDisplayName = string.Empty;
		public string UserDisplayName { get => _userDisplayName; set => Set(ref _userDisplayName, value); }

		private object _userThumbnail;
		public object UserThumbnail { get => _userThumbnail; set => Set(ref _userThumbnail, value); }

		private readonly OneDriveFileDownloader.UI.Services.IProcessLauncher _launcher;

		private bool _scanOnSelection;
		public bool ScanOnSelection { get => _scanOnSelection; set { if (Set(ref _scanOnSelection, value)) { _settings.ScanOnSelection = value; SettingsStore.Save(_settings); } } }

		public void UpdateSettings(Settings settings)
		{
			if (settings == null) return;
			_settings.LastClientId = settings.LastClientId;
			_settings.LastDownloadFolder = settings.LastDownloadFolder;
			_settings.SelectedUx = settings.SelectedUx;
			_settings.ScanOnSelection = settings.ScanOnSelection;
			ScanOnSelection = settings.ScanOnSelection;
		}

		public MainViewModel(IOneDriveService svc = null, IDownloadRepository repo = null, OneDriveFileDownloader.UI.Services.IProcessLauncher launcher = null)
		{
			_svc = svc ?? new OneDriveFileDownloader.Core.Services.OneDriveService();
			_repo = repo ?? new OneDriveFileDownloader.Core.Services.SqliteDownloadRepository(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "downloads.db"));
			_settings = SettingsStore.Load();
			_scanOnSelection = _settings.ScanOnSelection;
			_launcher = launcher ?? new OneDriveFileDownloader.UI.Services.SystemProcessLauncher();

			if (!string.IsNullOrEmpty(_settings.LastClientId))
			{
				_svc.Configure(_settings.LastClientId);
			}

			DownloadCommand = new RelayCommand(async p => { if (p is DownloadItemViewModel i) await DownloadAsync(i); });
			CancelCommand = new RelayCommand(p => { if (p is DownloadItemViewModel i) i.Cancel(); });
			RetryCommand = new RelayCommand(async p => { if (p is DownloadItemViewModel i) { i.ResetForRetry(); await DownloadAsync(i); } });
			OpenDownloadsCommand = new RelayCommand(p => { if (!string.IsNullOrEmpty(_settings.LastDownloadFolder)) OpenFolder(_settings.LastDownloadFolder); });
			ScanLastCommand = new RelayCommand(async p => {
				if (SharedItems.Count == 0) await LoadSharedItemsAsync();
				if (SharedItems.Count == 0) { StatusText = "No shared items available to scan."; return; }
				await ScanAsync(SharedItems[0]);
			});
			ScanFolderCommand = new RelayCommand(async _ => {
				if (SelectedNode != null) await ScanSelectedNodeAsync(SelectedNode);
			});

			SignInCommand = new RelayCommand(_ => RequestSignIn?.Invoke());
			SignOutCommand = new RelayCommand(async _ => await SignOutAsync());
			OpenSettingsCommand = new RelayCommand(_ => RequestSettings?.Invoke());
			NavigateCommand = new RelayCommand(p => RequestNavigate?.Invoke(p?.ToString() ?? "Minimal"));

			_ = CheckAuthenticationAsync();
		}

		private async Task CheckAuthenticationAsync()
		{
			try
			{
				var user = await _svc.AuthenticateSilentAsync();
				if (user != null)
				{
					IsAuthenticated = true;
					var prof = await _svc.GetUserProfileAsync();
					UserDisplayName = prof.DisplayName;
					if (prof.ThumbnailBytes != null && prof.ThumbnailBytes.Length > 0)
					{
						UserThumbnail = prof.ThumbnailBytes;
					}
					await LoadSharedItemsAsync();
				}
			}
			catch { /* ignore silent failure */ }
		}

		public event Action RequestSignIn;
		public event Action RequestSettings;
		public event Action<string> RequestNavigate;

		public async Task SignOutAsync()
		{
			await _svc.SignOutAsync();
			IsAuthenticated = false;
			UserDisplayName = "Guest";
			UserThumbnail = null;
			SharedItems.Clear();
			RecentDownloads.Clear();
			StatusText = "Signed out.";
		}

		public async Task SignInAsync(string clientId, bool save, IntPtr? parentWindow = null, Action<string> statusCallback = null)
		{
			_svc.Configure(clientId);
			StatusText = "Authenticating...";
			statusCallback?.Invoke(StatusText);

			var user = await _svc.AuthenticateInteractiveAsync(msg => {
				StatusText = msg;
				statusCallback?.Invoke(msg);
			}, parentWindow);

			if (user == null)
			{
				StatusText = "Sign in failed or cancelled.";
				statusCallback?.Invoke(StatusText);
				return;
			}

			IsAuthenticated = true;
			StatusText = "Signed in.";
			statusCallback?.Invoke(StatusText);

			if (save)
			{
				_settings.LastClientId = clientId;
				SettingsStore.Save(_settings);
			}

			var prof = await _svc.GetUserProfileAsync();
			UserDisplayName = prof.DisplayName;
			if (prof.ThumbnailBytes != null && prof.ThumbnailBytes.Length > 0)
			{
				UserThumbnail = prof.ThumbnailBytes;
			}

			await LoadSharedItemsAsync();
		}

		public async Task LoadSharedItemsAsync()
		{
			SharedItems.Clear();
			var items = await _svc.ListSharedWithMeAsync();
			foreach (var s in items) SharedItems.Add(s);
			StatusText = SharedItems.Count == 0 ? "No shared items found." : $"Found {SharedItems.Count} shared items.";

			if (FolderRoots.Count == 0 && SharedItems.Count > 0)
			{
				foreach (var s in SharedItems)
				{
					var node = await BuildFolderNodeAsync(s);
					FolderRoots.Add(node);
				}
			}
		}

		public async Task ScanSelectedNodeAsync(DriveItemNode node)
		{
			if (node == null) return;
			StatusText = $"Scanning {node.Item.Name}...";
			
			// Expand node to show subfolders if not already expanded
			if (!node.IsExpanded)
			{
				await ExpandNodeAsync(node);
			}

			Videos.Clear();
			var videoExts = new[] { ".mp4", ".mov", ".mkv", ".avi", ".wmv" };
			
			// List children of this specific folder
			var fakeShared = new SharedItemInfo { Id = node.Item.Id, RemoteDriveId = node.Item.DriveId, RemoteItemId = node.Item.Id, Name = node.Item.Name };
			var children = await _svc.ListChildrenAsync(fakeShared);
			
			foreach (var c in children)
			{
				if (!c.IsFolder && videoExts.Any(e => c.Name.EndsWith(e, StringComparison.OrdinalIgnoreCase)))
				{
					Videos.Add(new DownloadItemViewModel(c));
				}
			}
			
			StatusText = $"Found {Videos.Count} video files in {node.Item.Name}.";
		}

		public async Task PopulateFolderRootsAsync(SharedItemInfo root)
		{
			FolderRoots.Clear();
			if (root == null) return;
			var node = await BuildFolderNodeAsync(root);
			FolderRoots.Add(node);
		}

		public async Task<DriveItemNode> BuildFolderNodeAsync(SharedItemInfo root)
		{
			var node = new DriveItemNode(new DriveItemInfo { Id = root.RemoteItemId, DriveId = root.RemoteDriveId, Name = root.Name, IsFolder = true });
			node.OnExpanded += async () => await ExpandNodeAsync(node);
			await ExpandNodeAsync(node);
			return node;
		}

		public async Task ExpandNodeAsync(DriveItemNode node)
		{
			var fakeShared = new SharedItemInfo { Id = node.Item.Id, RemoteDriveId = node.Item.DriveId, RemoteItemId = node.Item.Id, Name = node.Item.Name };
			var children = await _svc.ListChildrenAsync(fakeShared);
			
			// Use Dispatcher to update collection if needed, but for now assume UI thread
			node.Children.Clear();
			foreach (var c in children.Where(x => x.IsFolder))
			{
				var childNode = new DriveItemNode(c);
				childNode.OnExpanded += async () => await ExpandNodeAsync(childNode);
				node.Children.Add(childNode);
			}
		}

		public async Task ScanAsync(SharedItemInfo selected)
		{
			if (selected == null) return;
			StatusText = "Scanning...";
			Videos.Clear();
			var videoExts = new[] { ".mp4", ".mov", ".mkv", ".avi", ".wmv" };

			var rootChildren = await _svc.ListChildrenAsync(selected);
			var videos = rootChildren.Where(c => !c.IsFolder && videoExts.Any(e => c.Name.EndsWith(e, StringComparison.OrdinalIgnoreCase))).ToList();
			var subfolders = new System.Collections.Generic.Queue<DriveItemInfo>(rootChildren.Where(c => c.IsFolder));

			while (subfolders.Count > 0)
			{
				var folder = subfolders.Dequeue();
				var fakeShared = new SharedItemInfo { Id = folder.Id, RemoteDriveId = folder.DriveId, RemoteItemId = folder.Id, Name = folder.Name };
				var children = await _svc.ListChildrenAsync(fakeShared);
				foreach (var c in children)
				{
					if (c.IsFolder) subfolders.Enqueue(c);
					else if (videoExts.Any(e => c.Name.EndsWith(e, StringComparison.OrdinalIgnoreCase))) videos.Add(c);
				}
			}

			foreach (var v in videos) Videos.Add(new DownloadItemViewModel(v));
			StatusText = $"Found {videos.Count} video files.";
		}

		public Task<IList<DriveItemInfo>> GetChildrenAsync(SharedItemInfo shared)
		{
			return _svc.ListChildrenAsync(shared);
		}

		public async Task LoadRecentDownloadsAsync(int count = 20)
		{
			RecentDownloads.Clear();
			var recs = await _repo.GetRecentAsync(count);
			foreach (var r in recs) RecentDownloads.Add(r);
		}

	private void OpenFolder(string path)
	{
		try
		{
			if (OperatingSystem.IsWindows())
			{
				System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("explorer", '"' + path + '"') { UseShellExecute = true });
			}
			else if (OperatingSystem.IsLinux())
			{
				System.Diagnostics.Process.Start("xdg-open", path);
			}
			else if (OperatingSystem.IsMacOS())
			{
				System.Diagnostics.Process.Start("open", path);
			}
		}
		catch { }
	}

	public bool OpenPath(string path)
	{
		if (string.IsNullOrEmpty(path)) return false;
		return _launcher.Open(path);
	}

	public async Task<IList<DownloadItemViewModel>> ScanFolderAsync(DriveItemInfo folder)
	{
		var results = new System.Collections.Generic.List<DownloadItemViewModel>();
		var videoExts = new[] { ".mp4", ".mov", ".mkv", ".avi", ".wmv" };
		var queue = new System.Collections.Generic.Queue<DriveItemInfo>();
		queue.Enqueue(folder);
		while (queue.Count > 0)
		{
			var f = queue.Dequeue();
			var fakeShared = new SharedItemInfo { Id = f.Id, RemoteDriveId = f.DriveId, RemoteItemId = f.Id, Name = f.Name };
			var children = await _svc.ListChildrenAsync(fakeShared);
			foreach (var c in children)
			{
				if (c.IsFolder) queue.Enqueue(c);
				else if (videoExts.Any(e => c.Name.EndsWith(e, System.StringComparison.OrdinalIgnoreCase))) results.Add(new DownloadItemViewModel(c));
			}
		}
		return results;
	}

		private readonly System.Threading.SemaphoreSlim _downloadSemaphore = new System.Threading.SemaphoreSlim(3);

		public async Task DownloadAsync(DownloadItemViewModel item)
		{
			if (item == null) return;
			var downloads = _settings.LastDownloadFolder;
			if (string.IsNullOrEmpty(downloads))
			{
				StatusText = "Enter a local download folder in Settings first.";
				return;
			}

			Directory.CreateDirectory(downloads);

			if (!string.IsNullOrEmpty(item.File.Sha1Hash) && await _repo.HasHashAsync(item.File.Sha1Hash))
			{
				item.Status = "Already downloaded";
				return;
			}

			await _downloadSemaphore.WaitAsync();
			item.Status = "Downloading";
			
			// Use the configured download folder for the temporary file to ensure it's on the same drive
			var temp = Path.Combine(downloads, item.File.Name + ".part");
			if (File.Exists(temp)) File.Delete(temp);

			var progress = new Progress<long>(bytes =>
			{
				// update item progress and ETA
				item.UpdateProgress(bytes);
				if (!(item.File.Size.HasValue && item.File.Size.Value > 0))
				{
					// approximate when size missing
					item.Progress = Math.Min(100.0, item.Progress + 5);
				}
			});

			try
			{
				using (var fs = File.Create(temp))
				{
					var dl = await _svc.DownloadFileAsync(item.File, fs, progress, item.Cancellation.Token);
					if (!string.IsNullOrEmpty(dl.Sha1Hash) && await _repo.HasHashAsync(dl.Sha1Hash))
					{
						fs.Close();
						File.Delete(temp);
						item.Status = "Duplicate after hash check";
						return;
					}

					var dest = Path.Combine(downloads, item.File.Name);
					if (File.Exists(dest))
					{
						dest = Path.Combine(downloads, Path.GetFileNameWithoutExtension(item.File.Name) + "_" + Guid.NewGuid().ToString("n").Substring(0, 8) + Path.GetExtension(item.File.Name));
					}
					fs.Close();
					File.Move(temp, dest);

					var rec = new DownloadRecord
					{
						FileId = item.File.Id,
						Sha1Hash = dl.Sha1Hash,
						FileName = item.File.Name,
						Size = dl.Size,
						LocalPath = dest
					};
					await _repo.AddRecordAsync(rec);
					item.Status = "Completed";
					item.Progress = 100;
					StatusText = $"Downloaded {item.File.Name} to {dest}";
				}
			}
			catch (OperationCanceledException)
			{
				item.Status = "Canceled";
				if (File.Exists(temp)) File.Delete(temp);
			}
			catch (Exception ex)
			{
				item.Status = "Error";
				if (File.Exists(temp)) File.Delete(temp);
				StatusText = "Error: " + ex.Message;
			}
			finally
			{
				_downloadSemaphore.Release();
			}
		}
	}
}
