using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace Summer.Helpers;

public sealed class StringNotEmptyToVisibilityConverter : IValueConverter
{
	public bool Invert { get; set; }

	public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
	{
		return (!string.IsNullOrWhiteSpace(value as string) == Invert) ? Visibility.Collapsed : Visibility.Visible;
	}

	public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
	{
		return Binding.DoNothing;
	}
}
