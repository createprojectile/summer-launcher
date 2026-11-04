using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace Summer.Helpers;

public sealed class RoundedRectConverter : IMultiValueConverter
{
	public double Radius { get; set; } = 10.0;

	public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
	{
		if (values.Length < 2 || !(values[0] is double num) || !(values[1] is double num2) || num <= 0.0 || num2 <= 0.0)
		{
			return Binding.DoNothing;
		}
		double num3 = ((values.Length > 2 && values[2] is Thickness thickness) ? Math.Max(Math.Max(thickness.Left, thickness.Right), Math.Max(thickness.Top, thickness.Bottom)) : 0.0);
		double num4 = Math.Max(0.0, Radius - num3);
		RectangleGeometry rectangleGeometry = new RectangleGeometry(new Rect(0.0, 0.0, num, num2), num4, num4);
		rectangleGeometry.Freeze();
		return rectangleGeometry;
	}

	public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
	{
		throw new NotSupportedException();
	}
}
