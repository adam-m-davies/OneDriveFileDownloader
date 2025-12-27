using Avalonia.Data.Converters;
using System;
using System.Globalization;

namespace OneDriveFileDownloader.Avalonia.Converters
{
	public class StatusToGlyphConverter : IValueConverter
	{
		public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
		{
			var s = (value as string) ?? string.Empty;
			s = s.ToLowerInvariant();
			return s switch
			{
				"pending" => "○",
				"downloading" => "⏳",
				"completed" => "✔",
				"error" => "⚠",
				"canceled" => "✖",
				_ => string.Empty,
			};
		}

		public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotSupportedException();
	}
}