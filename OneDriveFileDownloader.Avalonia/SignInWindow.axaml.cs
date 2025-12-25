using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using OneDriveFileDownloader.UI.ViewModels;
using OneDriveFileDownloader.Core.Services;
using System;

namespace OneDriveFileDownloader.Avalonia;

public partial class SignInWindow : Window
{
	private readonly MainViewModel _vm;
	public SignInWindow(MainViewModel vm)
	{
		InitializeComponent();
		_vm = vm;
		var settings = SettingsStore.Load();
		ClientIdBox.Text = settings.LastClientId ?? string.Empty;

		SaveBtn.Click += async (s, e) =>
		{
			var clientId = ClientIdBox.Text?.Trim();
			if (string.IsNullOrEmpty(clientId)) return;
			await _vm.SignInAsync(clientId, SaveCheck.IsChecked == true);
			Close();
		};

		CancelBtn.Click += (s, e) => Close();











}
	public void InitializeComponent() => AvaloniaXamlLoader.Load(this);
}