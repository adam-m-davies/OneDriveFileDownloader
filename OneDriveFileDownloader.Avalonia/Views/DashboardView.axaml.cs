using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace OneDriveFileDownloader.Avalonia.Views;

public partial class DashboardView : UserControl
{
	public DashboardView()
	{
		InitializeComponent();
	}

	public void InitializeComponent()
	{
		AvaloniaXamlLoader.Load(this);
	}
}
