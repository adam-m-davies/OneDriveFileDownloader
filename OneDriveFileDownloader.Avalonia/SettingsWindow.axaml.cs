using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using OneDriveFileDownloader.Core.Services;
using Avalonia.VisualTree;

namespace OneDriveFileDownloader.Avalonia;

public partial class SettingsWindow : Window
{
	private readonly OneDriveFileDownloader.UI.ViewModels.MainViewModel _vm;

	public SettingsWindow()
	{
		InitializeComponent();
	}

	public SettingsWindow(OneDriveFileDownloader.UI.ViewModels.MainViewModel vm)
	{
		InitializeComponent();
		_vm = vm;

		var clientIdBox = this.FindControl<TextBox>("ClientIdBox");
		var folderBox = this.FindControl<TextBox>("FolderBox");
		var scanOnSelectionBox = this.FindControl<CheckBox>("ScanOnSelectionBox");
		var browseBtn = this.FindControl<Button>("BrowseBtn");
		var clearClientBtn = this.FindControl<Button>("ClearClientBtn");
		var saveBtn = this.FindControl<Button>("SaveBtn");
		var cancelBtn = this.FindControl<Button>("CancelBtn");
		var uxBox = this.FindControl<ComboBox>("UxBox");

		var settings = SettingsStore.Load();
		if (clientIdBox != null) clientIdBox.Text = settings.LastClientId ?? string.Empty;
		if (folderBox != null) folderBox.Text = settings.LastDownloadFolder ?? string.Empty;
		if (scanOnSelectionBox != null) scanOnSelectionBox.IsChecked = settings.ScanOnSelection;
		if (uxBox != null) uxBox.SelectedIndex = settings.SelectedUx switch { UxOption.Dashboard => 1, UxOption.Explorer => 2, _ => 0 };

		if (browseBtn != null)
		{
			browseBtn.Click += async (s, e) =>
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

				if (!string.IsNullOrEmpty(folderPath) && folderBox != null) folderBox.Text = folderPath;
			}
			catch { /* ignore user cancellation or failures */ }
		};
		}

		if (clearClientBtn != null) clearClientBtn.Click += (s, e) => { if (clientIdBox != null) clientIdBox.Text = string.Empty; };

		if (saveBtn != null)
		{
			saveBtn.Click += (s, e) =>
			{
				// read Selected UX from UxBox
				var ux = uxBox?.SelectedIndex switch { 1 => UxOption.Dashboard, 2 => UxOption.Explorer, _ => UxOption.Minimal };
				var s2 = new Settings { 
					LastClientId = clientIdBox?.Text?.Trim() ?? string.Empty, 
					LastDownloadFolder = folderBox?.Text?.Trim() ?? string.Empty, 
					SelectedUx = ux, 
					ScanOnSelection = scanOnSelectionBox?.IsChecked == true 
				};
				SettingsStore.Save(s2);
				if (_vm != null) _vm.UpdateSettings(s2);
				Close();
			};
		}

		if (cancelBtn != null) cancelBtn.Click += (s, e) => Close();
	}

	public void InitializeComponent()
	{
		AvaloniaXamlLoader.Load(this);
	}
}