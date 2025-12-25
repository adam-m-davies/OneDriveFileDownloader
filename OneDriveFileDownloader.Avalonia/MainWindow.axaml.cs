using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using OneDriveFileDownloader.UI.ViewModels;
using OneDriveFileDownloader.Avalonia.Views;
using OneDriveFileDownloader.Core.Services;
using System;

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

		// load settings and reflect chosen UX
		_settings = SettingsStore.Load();
		_vm.StatusText = $"UI Experience: {_settings.SelectedUx}";

		MinimalBtn.Click += MinimalBtn_Click;
		DashboardBtn.Click += DashboardBtn_Click;
		ExplorerBtn.Click += ExplorerBtn_Click;
		SignInBtn.Click += SignInBtn_Click;
		SettingsBtn.Click += SettingsBtn_Click;

		// show configured UX on startup
		switch (_settings.SelectedUx)
		{
			case UxOption.Dashboard:
				MainContent.Content = new DashboardView { DataContext = _vm };
				_ = _vm.LoadRecentDownloadsAsync();
				break;
			case UxOption.Explorer:
				MainContent.Content = new ExplorerView { DataContext = _vm };
				_ = _vm.LoadSharedItemsAsync();
				break;
			default:
				MainContent.Content = new MinimalView { DataContext = _vm };
				break;
		}
	}

	public void InitializeComponent()
	{
		AvaloniaXamlLoader.Load(this);
	}

	private void MinimalBtn_Click(object sender, RoutedEventArgs e)
	{
		MainContent.Content = new MinimalView { DataContext = _vm };
	}

	private void DashboardBtn_Click(object sender, RoutedEventArgs e)
	{
		MainContent.Content = new DashboardView { DataContext = _vm };
	}

	private void ExplorerBtn_Click(object sender, RoutedEventArgs e)
	{
		MainContent.Content = new ExplorerView { DataContext = _vm };
	}

	private void SettingsBtn_Click(object sender, RoutedEventArgs e)
	{
		var wnd = new SettingsWindow();
		_ = wnd.ShowDialog(this);
	}

	private void SignInBtn_Click(object sender, RoutedEventArgs e)
	{
		var wnd = new SignInWindow(_vm);
		_ = wnd.ShowDialog(this);
	}
}
