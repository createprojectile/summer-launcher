using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Summer.Controls;

public sealed class SegmentedProgressBar : ProgressBar
{
	public static readonly DependencyProperty SegmentCountProperty = DependencyProperty.Register("SegmentCount", typeof(int), typeof(SegmentedProgressBar), new FrameworkPropertyMetadata(40, FrameworkPropertyMetadataOptions.AffectsRender, null, CoerceSegmentCount));

	public static readonly DependencyProperty SegmentGapProperty = DependencyProperty.Register("SegmentGap", typeof(double), typeof(SegmentedProgressBar), new FrameworkPropertyMetadata(2.0, FrameworkPropertyMetadataOptions.AffectsRender, null, CoerceNonNegative));

	public static readonly DependencyProperty SegmentCornerRadiusProperty = DependencyProperty.Register("SegmentCornerRadius", typeof(double), typeof(SegmentedProgressBar), new FrameworkPropertyMetadata(1.0, FrameworkPropertyMetadataOptions.AffectsRender, null, CoerceNonNegative));

	public int SegmentCount
	{
		get
		{
			return (int)GetValue(SegmentCountProperty);
		}
		set
		{
			SetValue(SegmentCountProperty, value);
		}
	}

	public double SegmentGap
	{
		get
		{
			return (double)GetValue(SegmentGapProperty);
		}
		set
		{
			SetValue(SegmentGapProperty, value);
		}
	}

	public double SegmentCornerRadius
	{
		get
		{
			return (double)GetValue(SegmentCornerRadiusProperty);
		}
		set
		{
			SetValue(SegmentCornerRadiusProperty, value);
		}
	}

	protected override void OnValueChanged(double oldValue, double newValue)
	{
		base.OnValueChanged(oldValue, newValue);
		InvalidateVisual();
	}

	protected override void OnMinimumChanged(double oldMinimum, double newMinimum)
	{
		base.OnMinimumChanged(oldMinimum, newMinimum);
		InvalidateVisual();
	}

	protected override void OnMaximumChanged(double oldMaximum, double newMaximum)
	{
		base.OnMaximumChanged(oldMaximum, newMaximum);
		InvalidateVisual();
	}

	protected override void OnRender(DrawingContext drawingContext)
	{
		base.OnRender(drawingContext);
		double actualWidth = ActualWidth;
		double actualHeight = ActualHeight;
		if (actualWidth <= 0.0 || actualHeight <= 0.0)
		{
			return;
		}
		int num = Math.Max(1, SegmentCount);
		double num2 = Math.Max(0.0, SegmentGap);
		if (num2 * (double)(num - 1) >= actualWidth)
		{
			num2 = 0.0;
		}
		double num3 = (actualWidth - num2 * (double)(num - 1)) / (double)num;
		if (num3 <= 0.0)
		{
			return;
		}
		double num4 = Maximum - Minimum;
		double value = ((num4 > 0.0) ? ((Value - Minimum) / num4) : 0.0);
		value = Math.Clamp(value, 0.0, 1.0);
		double num5 = actualWidth * value;
		Brush brush = Background ?? Brushes.Transparent;
		Brush brush2 = Foreground ?? Brushes.White;
		double num6 = Math.Clamp(SegmentCornerRadius, 0.0, Math.Min(num3, actualHeight) / 2.0);
		for (int i = 0; i < num; i++)
		{
			double num7 = (double)i * (num3 + num2);
			Rect rectangle = new Rect(num7, 0.0, num3, actualHeight);
			drawingContext.DrawRoundedRectangle(brush, null, rectangle, num6, num6);
			double num8 = Math.Clamp(num5 - num7, 0.0, num3);
			if (!(num8 <= 0.01))
			{
				double num9 = ((num8 >= num3 - 0.01) ? num6 : Math.Min(num6, num8 / 2.0));
				drawingContext.DrawRoundedRectangle(brush2, null, new Rect(num7, 0.0, num8, actualHeight), num9, num9);
			}
		}
	}

	private static object CoerceSegmentCount(DependencyObject d, object baseValue)
	{
		return Math.Clamp((int)baseValue, 1, 120);
	}

	private static object CoerceNonNegative(DependencyObject d, object baseValue)
	{
		return Math.Max(0.0, (double)baseValue);
	}
}
