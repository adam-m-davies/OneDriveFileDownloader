using Avalonia.Data.Converters;
using System;
using System.Globalization;

namespace OneDriveFileDownloader.Avalonia.Converters
{
	public class CountToVisibleConverter : IValueConverter
	{
		public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
		{
			if (value is int count)
			{
				int threshold = 0;
				if (parameter != null && int.TryParse(parameter.ToString(), out int p))
					threshold = p;
				
				return count > threshold;
			}
			return false;
		}

		public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
	}
}
