using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using OneDriveFileDownloader.Core.Services;

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
			var dlg = new OpenFolderDialog();
			var res = await dlg.ShowAsync(this);
			if (!string.IsNullOrEmpty(res)) FolderBox.Text = res;
		};

		SaveBtn.Click += (s, e) =>
		{
			var s2 = new Settings { LastClientId = ClientIdBox.Text?.Trim() ?? string.Empty, LastDownloadFolder = FolderBox.Text?.Trim() ?? string.Empty };
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