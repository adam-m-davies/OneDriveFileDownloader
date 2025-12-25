using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using OneDriveFileDownloader.Core.Services;
using OneDriveFileDownloader.Core.Models;
using OneDriveFileDownloader.Console.Services;
using System.Linq;
using System.Threading.Tasks;
using System;
using System.IO;

namespace OneDriveFileDownloader.WinUI
{
    public sealed partial class MainWindow : NavigationView
    {
        private readonly MainViewModel _vm = new MainViewModel();
        private readonly Settings _settings;

        public MainWindow()
        {
            this.InitializeComponent();

            _settings = SettingsStore.Load();

            // set DataContext
            this.DataContext = _vm;

            // show selected UX in status
            StatusText.Text = $"UI Experience: {_settings.SelectedUx}";

            // if we have a saved client id, leave it preconfigured (ViewModel handles it)
            if (!string.IsNullOrEmpty(_settings.LastClientId))
            {
                // leave Sign-in button text as-is; ViewModel pre-configures the service
            }
        }

        private async void SignInButton_Click(object sender, RoutedEventArgs e)
        {
            // prompt for client id in a simple dialog
            var dialog = new ContentDialog { Title = "Sign in", PrimaryButtonText = "Sign in", CloseButtonText = "Cancel" };
            var stack = new StackPanel();
            var tb = new TextBox { PlaceholderText = "ClientId", Text = _settings.LastClientId ?? string.Empty };
            var saveCheck = new CheckBox { Content = "Save client id", IsChecked = true };
            stack.Children.Add(tb);
            stack.Children.Add(saveCheck);
            dialog.Content = stack;

            var res = await dialog.ShowAsync();
            if (res == ContentDialogResult.Primary)
            {
                var clientId = tb.Text.Trim();
                if (string.IsNullOrEmpty(clientId)) return;
                await _vm.SignInAsync(clientId, saveCheck.IsChecked == true);

                // load shared items handled by ViewModel; UI is bound to ViewModel collections
                // update Sign-in button text
                if (saveCheck.IsChecked == true) SignInButton.Content = "Sign in (use saved client id)";
            }
        }

        private async Task LoadSharedItems()
        {
            SharedItemsList.Items.Clear();
            var items = await _svc.ListSharedWithMeAsync();
            foreach (var s in items) SharedItemsList.Items.Add(s);
            if (SharedItemsList.Items.Count == 0)
            {
                StatusText.Text = "No shared items found.";
            }
            else StatusText.Text = $"Found {SharedItemsList.Items.Count} shared items.";
        }

        private void SharedItemsList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // placeholder
        }

        private async void Nav_ItemInvoked(Microsoft.UI.Xaml.Controls.NavigationView sender, Microsoft.UI.Xaml.Controls.NavigationViewItemInvokedEventArgs args)
        {
            if (args.InvokedItemContainer == null) return;
            if (args.InvokedItemContainer == SettingsNav)
            {
                var dialog = new ContentDialog { Title = "Settings", PrimaryButtonText = "Save", CloseButtonText = "Cancel" };
                var stack = new StackPanel();
                var folderBox = new TextBox { PlaceholderText = "Local downloads folder", Text = _settings.LastDownloadFolder ?? string.Empty };
                var uxBox = new ComboBox { ItemsSource = Enum.GetValues(typeof(OneDriveFileDownloader.Console.Services.UxOption)).Cast<object>(), SelectedItem = _settings.SelectedUx };
                var clearBtn = new Button { Content = "Clear saved Client ID", Margin = new Microsoft.UI.Xaml.Thickness(0,8,0,0) };
                clearBtn.Click += (s, e2) => { _settings.LastClientId = null; SettingsStore.Save(_settings); StatusText.Text = "Cleared saved Client ID."; };

                stack.Children.Add(new TextBlock { Text = "Local downloads folder" });
                stack.Children.Add(folderBox);
                stack.Children.Add(new TextBlock { Text = "UI Experience" , Margin = new Microsoft.UI.Xaml.Thickness(0,8,0,0)});
                stack.Children.Add(uxBox);
                stack.Children.Add(clearBtn);
                dialog.Content = stack;
                var res = await dialog.ShowAsync();
                if (res == ContentDialogResult.Primary)
                {
                    _settings.LastDownloadFolder = folderBox.Text?.Trim();
                    if (uxBox.SelectedItem is OneDriveFileDownloader.Console.Services.UxOption ux)
                    {
                        _settings.SelectedUx = ux;
                    }
                    SettingsStore.Save(_settings);
                    StatusText.Text = "Settings saved.";
                }
            }
        }

        private async void ScanButton_Click(object sender, RoutedEventArgs e)
        {
            if (SharedItemsList.SelectedItem is not OneDriveFileDownloader.Core.Models.SharedItemInfo si)
            {
                StatusText.Text = "Select a shared item first.";
                return;
            }

            await _vm.ScanAsync(si);
        }

        private async void VideosList_ItemClick(object sender, Microsoft.UI.Xaml.Controls.ItemClickEventArgs e)
        {
            if (e.ClickedItem is ViewModels.DownloadItemViewModel vm)
            {
                await _vm.DownloadAsync(vm);
            }
        }

        private async void DownloadSelectedButton_Click(object sender, RoutedEventArgs e)
        {
            if (VideosList.SelectedItem is not ViewModels.DownloadItemViewModel vm)
            {
                StatusText.Text = "Select a video first.";
                return;
            }

            // call viewmodel download
            await _vm.DownloadAsync(vm);
        }
    }
}
