using OneDriveFileDownloader.Core.Models;
using System;
using System.Threading;

namespace OneDriveFileDownloader.UI.ViewModels
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
            set
            {
                if (Set(ref _status, value))
                {
                    RaisePropertyChanged(nameof(IsDownloading));
                }
            }
        }

        public bool IsDownloading => string.Equals(Status, "Downloading", StringComparison.OrdinalIgnoreCase);

        private double _speedBytesPerSec;
        public double SpeedBytesPerSec
        {
            get => _speedBytesPerSec;
            set
            {
                if (Set(ref _speedBytesPerSec, value))
                {
                    RaisePropertyChanged(nameof(EstimatedRemaining));
                }
            }
        }

        public string EstimatedRemaining
        {
            get
            {
                if (File.Size.HasValue && SpeedBytesPerSec > 0 && Progress < 100)
                {
                    var remaining = (File.Size.Value * (100 - Progress) / 100.0) / SpeedBytesPerSec;
                    return TimeSpan.FromSeconds(Math.Max(0, remaining)).ToString(@"hh\:mm\:ss");
                }
                return string.Empty;
            }
        }

        private int _retryCount = 0;
        public int RetryCount
        {
            get => _retryCount;
            set => Set(ref _retryCount, value);
        }

        public void Retry()
        {
            RetryCount++;
            Cancel();
            try { Cancellation.Dispose(); } catch { }
            // create a new CTS for future download
            Cancellation = new System.Threading.CancellationTokenSource();
            Status = "Pending";
            Progress = 0;
        }

        public CancellationTokenSource Cancellation { get; private set; } = new CancellationTokenSource();

        public DownloadItemViewModel(DriveItemInfo file)
        {
            File = file;
            Status = "Pending";
            Progress = 0;
        }

        public void Cancel() => Cancellation.Cancel();
    }
}
