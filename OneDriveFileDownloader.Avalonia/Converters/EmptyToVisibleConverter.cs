using Avalonia.Data.Converters;
using System;
using System.Collections;
using System.Globalization;

namespace OneDriveFileDownloader.Avalonia.Converters
{
	public class EmptyToVisibleConverter : IValueConverter
	{
		public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
		{
			if (value == null) return true;
			if (value is int count) return count == 0;
			if (value is ICollection col) return col.Count == 0;
			if (value is IEnumerable en)
			{
				var enumerator = en.GetEnumerator();
				return !enumerator.MoveNext();
			}
			return false;
		}

		public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
	}
}
