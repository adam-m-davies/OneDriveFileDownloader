using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using OneDriveFileDownloader.UI.ViewModels;
using OneDriveFileDownloader.Avalonia.Views;
using OneDriveFileDownloader.Core.Services;
using System;
using System.Threading.Tasks;

namespace OneDriveFileDownloader.Avalonia;

public partial class MainWindow : Window
{
	private MainViewModel _vm;
	private Settings _settings;

	public MainWindow()
	{
		InitializeComponent();

		_vm = new MainViewModel();
		DataContext = _vm;

		_vm.RequestSignIn += () => {
			var wnd = new SignInWindow(_vm);
			_ = wnd.ShowDialog(this);
		};

		_vm.RequestSettings += () => {
			var wnd = new SettingsWindow(_vm);
			_ = wnd.ShowDialog(this);
		};

		_vm.RequestSharingUrl += async () => {
			var dialog = new AddSharingUrlDialog();
			var result = await dialog.ShowDialog<string>(this);
			return result;
		};

		_vm.RequestNavigate += (viewName) => {
			var mainContent = this.FindControl<ContentControl>("MainContent");
			if (mainContent == null) return;

			switch (viewName)
			{
				case "Dashboard":
					mainContent.Content = new DashboardView { DataContext = _vm };
					_ = _vm.LoadRecentDownloadsAsync();
					break;
				case "Explorer":
					mainContent.Content = new ExplorerView { DataContext = _vm };
					_ = _vm.LoadSharedItemsAsync();
					break;
				default:
					mainContent.Content = new MinimalView { DataContext = _vm };
					_ = global::Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(async () => {
						await _vm.LoadSharedItemsAsync();
						if (_vm.SharedItems.Count > 0) await _vm.ScanAsync(_vm.SharedItems[0]);
					});
					break;
			}
		};

		// load settings and reflect chosen UX
		_settings = SettingsStore.Load();
		_vm.StatusText = $"UI Experience: {_settings.SelectedUx}";

		// show configured UX on startup
		_vm.NavigateCommand.Execute(_settings.SelectedUx.ToString());
	}

	public void InitializeComponent()
	{
		AvaloniaXamlLoader.Load(this);
	}
}
