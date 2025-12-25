using Microsoft.UI.Xaml.Data;
using System;

namespace OneDriveFileDownloader.WinUI.Converters
{
	public class StatusToGlyphConverter : IValueConverter
	{
		public object Convert(object value, Type targetType, object parameter, string language)
		{
			if (value is string s)
			{
				return s switch
				{
					"Downloading" => "⏳",
					"Completed" => "✔",
					"Canceled" => "✖",
					"Error" => "⚠",
					_ => string.Empty,
				};
			}
			return string.Empty;
		}

		public object ConvertBack(object value, Type targetType, object parameter, string language) => throw new NotImplementedException();
	}
}
