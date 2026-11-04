using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace Summer.Helpers;

public sealed class BoolToVisibilityConverter : IValueConverter
{
	public bool Invert { get; set; }

	public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
	{
		bool flag = default;
		int num;
		if (value is bool)
		{
			flag = (bool)value;
			num = 1;
		}
		else
		{
			num = 0;
		}
		bool flag2 = (byte)((uint)num & (flag ? 1u : 0u)) != 0;
		if (Invert)
		{
			flag2 = !flag2;
		}
		return (!flag2) ? Visibility.Collapsed : Visibility.Visible;
	}

	public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
	{
		bool flag = value is Visibility && (Visibility)value == Visibility.Visible;
		return Invert ? (!flag) : flag;
	}
}
