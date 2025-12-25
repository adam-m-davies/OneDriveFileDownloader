using Avalonia.Data.Converters;
using System;
using System.Globalization;

namespace OneDriveFileDownloader.Avalonia.Converters
{
	public class SecondsToEtaConverter : IValueConverter
	{
		public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
		{
			if (value == null) return string.Empty;
			if (value is double d)
			{
				if (double.IsNaN(d) || double.IsInfinity(d)) return string.Empty;
				var ts = TimeSpan.FromSeconds(d);
				if (ts.TotalHours >= 1) return ts.ToString(@"h\:mm\:ss");
				return ts.ToString(@"m\:ss");
			}
			return string.Empty;
		}

		public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
	}
}