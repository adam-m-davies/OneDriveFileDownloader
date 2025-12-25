using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace OneDriveFileDownloader.Avalonia.Views;

public partial class MinimalView : UserControl
{
	public MinimalView()
	{
		InitializeComponent();
	}

	public void InitializeComponent()
	{
		AvaloniaXamlLoader.Load(this);
	}
}
