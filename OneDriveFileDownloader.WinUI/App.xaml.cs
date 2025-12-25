using Microsoft.UI.Xaml;

namespace OneDriveFileDownloader.WinUI
{
    public partial class App : Application
    {
        public App()
        {
            this.InitializeComponent();
        }

        protected override void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
        {
            _ = new MainWindow();
            base.OnLaunched(args);
        }
    }
}
