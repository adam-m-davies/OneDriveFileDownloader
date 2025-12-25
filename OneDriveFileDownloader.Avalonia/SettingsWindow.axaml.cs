using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using OneDriveFileDownloader.Core.Services;
using Avalonia.VisualTree;

namespace OneDriveFileDownloader.Avalonia;

public partial class SettingsWindow : Window
{

	public SettingsWindow()
	{
		InitializeComponent();

		var settings = SettingsStore.Load();
		ClientIdBox.Text = settings.LastClientId ?? string.Empty;
		FolderBox.Text = settings.LastDownloadFolder ?? string.Empty;

		BrowseBtn.Click += async (s, e) =>
		{
			// Use StorageProvider API when available (recommended). Fall back to OpenFolderDialog for compatibility.
			try
			{
				var provider = this.StorageProvider ?? (this.GetVisualRoot() as TopLevel)?.StorageProvider;
				string folderPath = null;
				if (provider != null)
				{
					// Use reflection to call a folder-pick method if present (PickFolderAsync / TryPickFolderAsync / PickSingleFolderAsync)
					var m = provider.GetType().GetMethod("PickFolderAsync") ?? provider.GetType().GetMethod("TryPickFolderAsync") ?? provider.GetType().GetMethod("PickSingleFolderAsync");
					if (m != null)
					{
						var task = m.Invoke(provider, Array.Empty<object>()) as System.Threading.Tasks.Task;
						if (task != null)
						{
							await task.ConfigureAwait(false);
							var resultProp = task.GetType().GetProperty("Result");
							var resObj = resultProp?.GetValue(task);
							if (resObj != null)
							{
								var pathProp = resObj.GetType().GetProperty("Path") ?? resObj.GetType().GetProperty("FullPath") ?? resObj.GetType().GetProperty("LocalPath");
								if (pathProp != null)
									folderPath = pathProp.GetValue(resObj) as string;
								else
									folderPath = resObj.ToString();
							}
						}
					}
				}

				if (string.IsNullOrEmpty(folderPath))
				{
					// fallback
					var dlg = new OpenFolderDialog();
					var res = await dlg.ShowAsync(this);
					folderPath = res;
				}

				if (!string.IsNullOrEmpty(folderPath)) FolderBox.Text = folderPath;
			}
			catch { /* ignore user cancellation or failures */ }
		};

		ClearClientBtn.Click += (s, e) => { ClientIdBox.Text = string.Empty; };

		SaveBtn.Click += (s, e) =>
		{
			// read Selected UX from UxBox
			var ux = UxBox.SelectedIndex switch { 1 => UxOption.Dashboard, 2 => UxOption.Explorer, _ => UxOption.Minimal };
			var s2 = new Settings { LastClientId = ClientIdBox.Text?.Trim() ?? string.Empty, LastDownloadFolder = FolderBox.Text?.Trim() ?? string.Empty, SelectedUx = ux };
			SettingsStore.Save(s2);
			Close();
		};

		CancelBtn.Click += (s, e) => Close();
	}

	public void InitializeComponent()
	{
		AvaloniaXamlLoader.Load(this);
	}
}