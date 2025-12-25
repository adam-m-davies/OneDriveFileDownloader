using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI;
using System;

namespace OneDriveFileDownloader.WinUI.Converters
{
    public class StatusToBrushConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value is string s)
            {
                return s switch
                {
                    "Downloading" => new SolidColorBrush(Windows.UI.Color.FromArgb(255, 0, 120, 215)), // blue
                    "Completed" => new SolidColorBrush(Windows.UI.Color.FromArgb(255, 16, 124, 16)), // green
                    "Canceled" => new SolidColorBrush(Windows.UI.Color.FromArgb(255, 128, 128, 128)), // gray
                    "Error" => new SolidColorBrush(Windows.UI.Color.FromArgb(255, 232, 17, 35)), // red
                    _ => new SolidColorBrush(Windows.UI.Colors.Transparent),
                };
            }
            return new SolidColorBrush(Windows.UI.Colors.Transparent);
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language) => throw new NotImplementedException();
    }
}