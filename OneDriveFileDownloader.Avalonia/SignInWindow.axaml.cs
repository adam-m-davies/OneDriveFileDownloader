using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using OneDriveFileDownloader.UI.ViewModels;
using OneDriveFileDownloader.Core.Services;
using System;

namespace OneDriveFileDownloader.Avalonia;

public partial class SignInWindow : Window
{
	private readonly MainViewModel _vm;

	public SignInWindow()
	{
		InitializeComponent();
	}

	public SignInWindow(MainViewModel vm)
	{
		InitializeComponent();

		_vm = vm;
		var clientIdBox = this.FindControl<TextBox>("ClientIdBox");
		var saveCheck = this.FindControl<CheckBox>("SaveCheck");
		var saveBtn = this.FindControl<Button>("SaveBtn");
		var cancelBtn = this.FindControl<Button>("CancelBtn");

		var settings = SettingsStore.Load();
		if (clientIdBox != null) clientIdBox.Text = settings.LastClientId ?? string.Empty;

		if (saveBtn != null)
		{
			saveBtn.Click += async (s, e) =>
			{
				var clientId = clientIdBox?.Text?.Trim();
				if (string.IsNullOrEmpty(clientId)) return;
				
				var statusTxt = this.FindControl<TextBlock>("StatusText");
				if (saveBtn != null) saveBtn.IsEnabled = false;

				IntPtr? handle = null;
				try { handle = this.TryGetPlatformHandle()?.Handle; } catch { }

				if (_vm != null)
				{
					await _vm.SignInAsync(clientId, saveCheck?.IsChecked == true, handle, msg => {
						Dispatcher.UIThread.Post(() => {
							if (statusTxt != null) statusTxt.Text = msg;
						});
					});
				}
				Close();
			};
		}

		if (cancelBtn != null) cancelBtn.Click += (s, e) => Close();
	}

public void InitializeComponent() => AvaloniaXamlLoader.Load(this);
}
