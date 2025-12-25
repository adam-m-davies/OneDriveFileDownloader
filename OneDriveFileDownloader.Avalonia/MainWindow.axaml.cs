using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using OneDriveFileDownloader.UI.ViewModels;
using OneDriveFileDownloader.Avalonia.Views;

namespace OneDriveFileDownloader.Avalonia;

public partial class MainWindow : Window
{
	private MainViewModel _vm;

	public MainWindow()
	{
		InitializeComponent();
		_vm = new MainViewModel();
		DataContext = _vm;

		MinimalBtn.Click += MinimalBtn_Click;
		DashboardBtn.Click += DashboardBtn_Click;
		ExplorerBtn.Click += ExplorerBtn_Click;
		SettingsBtn.Click += SettingsBtn_Click;

		// show Minimal view on startup
		MainContent.Content = new MinimalView { DataContext = _vm };
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
}
