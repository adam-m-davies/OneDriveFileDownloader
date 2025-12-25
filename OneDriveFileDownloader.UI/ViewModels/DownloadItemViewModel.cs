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

        private string _status;
        public string Status
        {
            get => _status;
            set
            {
                if (Set(ref _status, value))
                {
                    RaisePropertyChanged(nameof(IsDownloading));
                    RaisePropertyChanged(nameof(IsError));
                }
            }
        }

        public bool IsDownloading => string.Equals(Status, "Downloading", StringComparison.OrdinalIgnoreCase);
        public bool IsError => string.Equals(Status, "Error", StringComparison.OrdinalIgnoreCase);

        // ETA and speed tracking
        private long _lastBytes = 0;
        private DateTime _lastProgressAt = DateTime.MinValue;
        public double SpeedBytesPerSecond { get; private set; }
        public double? EstimatedSecondsRemaining { get; private set; }

        private CancellationTokenSource _cancellation = new CancellationTokenSource();
        public CancellationTokenSource Cancellation => _cancellation;

        public DownloadItemViewModel(DriveItemInfo file)
        {
            File = file;
            Status = "Pending";
            Progress = 0;
            SpeedBytesPerSecond = 0;
            EstimatedSecondsRemaining = null;
        }

        public void Cancel() => _cancellation.Cancel();

        private int _retryCount = 0;
        public int RetryCount { get => _retryCount; private set => Set(ref _retryCount, value); }
        public int MaxRetries { get; } = 3;
        public bool IsRetryAllowed => RetryCount < MaxRetries;

        public void ResetForRetry()
        {
            if (!IsRetryAllowed) return;
            RetryCount = RetryCount + 1;
            try { _cancellation.Cancel(); } catch { }
            _cancellation = new CancellationTokenSource();
            Status = "Pending";
            Progress = 0;
            SpeedBytesPerSecond = 0;
            EstimatedSecondsRemaining = null;
            _lastBytes = 0;
            _lastProgressAt = DateTime.MinValue;
        }

        public void UpdateProgress(long totalBytes)
        {
            var now = DateTime.UtcNow;
            if (_lastProgressAt == DateTime.MinValue)
            {
                _lastProgressAt = now;
                _lastBytes = totalBytes;
                return;
            }

            var elapsed = (now - _lastProgressAt).TotalSeconds;
            if (elapsed <= 0) return;
            var delta = totalBytes - _lastBytes;
            if (delta < 0) delta = 0;
            SpeedBytesPerSecond = delta / elapsed;
            _lastBytes = totalBytes;
            _lastProgressAt = now;

            if (File.Size.HasValue && SpeedBytesPerSecond > 0)
            {
                var remaining = (double)(File.Size.Value - totalBytes);
                EstimatedSecondsRemaining = Math.Max(0, remaining / SpeedBytesPerSecond);
            }

            // update percentage progress based on file size if available
            if (File.Size.HasValue && File.Size.Value > 0)
            {
                Progress = Math.Min(100.0, (totalBytes / (double)File.Size.Value) * 100.0);
            }
        }
    }
}
