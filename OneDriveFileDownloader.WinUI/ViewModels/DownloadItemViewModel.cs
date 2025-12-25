using OneDriveFileDownloader.Core.Models;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace OneDriveFileDownloader.WinUI.ViewModels
{
    public class DownloadItemViewModel : ViewModelBase
    {
        public DriveItemInfo File { get; }

        private double _progress;
        public double Progress
        {
            get => _progress;
            set => Set(ref _progress, value);
        }

        private string? _status;
        public string? Status
        {
            get => _status;
            set => Set(ref _status, value);
        }

        public CancellationTokenSource Cancellation { get; } = new CancellationTokenSource();

        public DownloadItemViewModel(DriveItemInfo file)
        {
            File = file;
            Status = "Pending";
            Progress = 0;
        }

        public void Cancel() => Cancellation.Cancel();
    }
}
