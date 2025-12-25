using Microsoft.UI.Xaml.Data;
using System;

namespace OneDriveFileDownloader.WinUI.Converters
{
    public class SecondsToEtaConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value == null) return string.Empty;
            if (value is double d)
            {
                if (double.IsNaN(d) || double.IsInfinity(d)) return string.Empty;
                var ts = TimeSpan.FromSeconds(d);
                if (ts.TotalHours >= 1) return ts.ToString("h\:mm\:ss");
                return ts.ToString("m\:ss");
            }
            if (value is double? dd && dd.HasValue)
            {
                var ts = TimeSpan.FromSeconds(dd.Value);
                return ts.ToString("m\:ss");
            }
            return string.Empty;
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language) => throw new NotImplementedException();
    }
}