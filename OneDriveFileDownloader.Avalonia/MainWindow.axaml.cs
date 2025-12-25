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

		var minimalBtn = this.FindControl<Button>("MinimalBtn");
		if (minimalBtn != null) minimalBtn.Click += MinimalBtn_Click;
		
		var dashboardBtn = this.FindControl<Button>("DashboardBtn");
		if (dashboardBtn != null) dashboardBtn.Click += DashboardBtn_Click;
		
		var explorerBtn = this.FindControl<Button>("ExplorerBtn");
		if (explorerBtn != null) explorerBtn.Click += ExplorerBtn_Click;
		
		var signInBtn = this.FindControl<Button>("SignInBtn");
		if (signInBtn != null) signInBtn.Click += SignInBtn_Click;
		
		var settingsBtn = this.FindControl<Button>("SettingsBtn");
		if (settingsBtn != null) settingsBtn.Click += SettingsBtn_Click;

		// show configured UX on startup
		var mainContent = this.FindControl<ContentControl>("MainContent");
		if (mainContent != null)
		{
			switch (_settings.SelectedUx)
			{
				case UxOption.Dashboard:
					mainContent.Content = new DashboardView { DataContext = _vm };
					_ = _vm.LoadRecentDownloadsAsync();
					break;
				case UxOption.Explorer:
					mainContent.Content = new ExplorerView { DataContext = _vm };
					_ = _vm.LoadSharedItemsAsync();
					break;
				default:
					mainContent.Content = new MinimalView { DataContext = _vm };
					break;
			}
		}
	}

	public void InitializeComponent()
	{
		AvaloniaXamlLoader.Load(this);
	}

	private void MinimalBtn_Click(object sender, RoutedEventArgs e)
	{
		var mainContent = this.FindControl<ContentControl>("MainContent");
		if (mainContent != null) mainContent.Content = new MinimalView { DataContext = _vm };
	}

	private void DashboardBtn_Click(object sender, RoutedEventArgs e)
	{
		var mainContent = this.FindControl<ContentControl>("MainContent");
		if (mainContent != null) mainContent.Content = new DashboardView { DataContext = _vm };
	}

	private void ExplorerBtn_Click(object sender, RoutedEventArgs e)
	{
		var mainContent = this.FindControl<ContentControl>("MainContent");
		if (mainContent != null) mainContent.Content = new ExplorerView { DataContext = _vm };
	}

	private void SettingsBtn_Click(object sender, RoutedEventArgs e)
	{
		var wnd = new SettingsWindow(_vm);
		_ = wnd.ShowDialog(this);
	}

	private void SignInBtn_Click(object sender, RoutedEventArgs e)
	{
		var wnd = new SignInWindow(_vm);
		_ = wnd.ShowDialog(this);
	}
}
