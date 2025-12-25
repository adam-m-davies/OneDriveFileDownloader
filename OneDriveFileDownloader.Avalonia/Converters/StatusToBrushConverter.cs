using Avalonia.Data.Converters;
using Avalonia.Media;
using System;
using System.Globalization;

namespace OneDriveFileDownloader.Avalonia.Converters
{
	public class StatusToBrushConverter : IValueConverter
	{
		public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
		{
			var s = value as string ?? string.Empty;
			switch (s.ToLowerInvariant())
			{
				case "completed":
					return Brushes.Green;
				case "downloading":
					return Brushes.DodgerBlue;
				case "error":
					return Brushes.Red;
				case "canceled":
					return Brushes.Orange;
				default:
					return Brushes.Gray;
			}
		}

		public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
	}
}