using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;

namespace OneDriveFileDownloader.Avalonia.Views;

public partial class AddSharingUrlDialog : Window
{
    private TextBox _urlTextBox;

    public AddSharingUrlDialog()
    {
        InitializeComponent();
        _urlTextBox = this.FindControl<TextBox>("UrlTextBox");
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }

    private void OnCancel(object sender, RoutedEventArgs e)
    {
        Close(null);
    }

    private void OnAdd(object sender, RoutedEventArgs e)
    {
        var url = _urlTextBox?.Text?.Trim();
        Close(url);
    }
}
