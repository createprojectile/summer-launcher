using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Media;

namespace Summer.Controls;

public sealed class Sparkline : FrameworkElement
{
	public static readonly DependencyProperty ValuesProperty = DependencyProperty.Register("Values", typeof(IEnumerable<double>), typeof(Sparkline), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

	public static readonly DependencyProperty StrokeProperty = DependencyProperty.Register("Stroke", typeof(Brush), typeof(Sparkline), new FrameworkPropertyMetadata(Brushes.White, FrameworkPropertyMetadataOptions.AffectsRender));

	public static readonly DependencyProperty FillProperty = DependencyProperty.Register("Fill", typeof(Brush), typeof(Sparkline), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

	public static readonly DependencyProperty StrokeThicknessProperty = DependencyProperty.Register("StrokeThickness", typeof(double), typeof(Sparkline), new FrameworkPropertyMetadata(1.2, FrameworkPropertyMetadataOptions.AffectsRender));

	public static readonly DependencyProperty ShowEndpointProperty = DependencyProperty.Register("ShowEndpoint", typeof(bool), typeof(Sparkline), new FrameworkPropertyMetadata(true, FrameworkPropertyMetadataOptions.AffectsRender));

	public IEnumerable<double>? Values
	{
		get
		{
			return (IEnumerable<double>)GetValue(ValuesProperty);
		}
		set
		{
			SetValue(ValuesProperty, value);
		}
	}

	public Brush? Stroke
	{
		get
		{
			return (Brush)GetValue(StrokeProperty);
		}
		set
		{
			SetValue(StrokeProperty, value);
		}
	}

	public Brush? Fill
	{
		get
		{
			return (Brush)GetValue(FillProperty);
		}
		set
		{
			SetValue(FillProperty, value);
		}
	}

	public double StrokeThickness
	{
		get
		{
			return (double)GetValue(StrokeThicknessProperty);
		}
		set
		{
			SetValue(StrokeThicknessProperty, value);
		}
	}

	public bool ShowEndpoint
	{
		get
		{
			return (bool)GetValue(ShowEndpointProperty);
		}
		set
		{
			SetValue(ShowEndpointProperty, value);
		}
	}

	public Sparkline()
	{
		CacheMode = new BitmapCache();
	}

	protected override void OnRender(DrawingContext drawingContext)
	{
		base.OnRender(drawingContext);
		double actualWidth = ActualWidth;
		double actualHeight = ActualHeight;
		if (actualWidth <= 1.0 || actualHeight <= 1.0 || Values == null)
		{
			return;
		}
		List<double> list = new List<double>();
		foreach (double value in Values)
		{
			if (!double.IsNaN(value) && !double.IsInfinity(value))
			{
				list.Add(value);
			}
		}
		if (list.Count < 2)
		{
			return;
		}
		double num = Math.Max(0.1, StrokeThickness);
		double num2 = num + 0.5;
		double num3 = Math.Max(1.0, actualHeight - num2 * 2.0);
		double num4 = list.Min();
		double num5 = list.Max();
		if (num5 - num4 < 0.001)
		{
			num5 = num4 + 1.0;
		}
		Point[] array = new Point[list.Count];
		for (int i = 0; i < list.Count; i++)
		{
			double x = (double)i * actualWidth / (double)(list.Count - 1);
			double y = num2 + (1.0 - (list[i] - num4) / (num5 - num4)) * num3;
			array[i] = new Point(x, y);
		}
		if (Fill != null)
		{
			StreamGeometry streamGeometry = new StreamGeometry();
			using (StreamGeometryContext streamGeometryContext = streamGeometry.Open())
			{
				streamGeometryContext.BeginFigure(new Point(array[0].X, actualHeight), isFilled: true, isClosed: true);
				streamGeometryContext.LineTo(array[0], isStroked: true, isSmoothJoin: false);
				for (int j = 1; j < array.Length; j++)
				{
					streamGeometryContext.LineTo(array[j], isStroked: true, isSmoothJoin: false);
				}
				streamGeometryContext.LineTo(new Point(array[^1].X, actualHeight), isStroked: true, isSmoothJoin: false);
			}
			streamGeometry.Freeze();
			drawingContext.DrawGeometry(Fill, null, streamGeometry);
		}
		StreamGeometry streamGeometry2 = new StreamGeometry();
		using (StreamGeometryContext streamGeometryContext2 = streamGeometry2.Open())
		{
			streamGeometryContext2.BeginFigure(array[0], isFilled: false, isClosed: false);
			for (int k = 1; k < array.Length; k++)
			{
				streamGeometryContext2.LineTo(array[k], isStroked: true, isSmoothJoin: false);
			}
		}
		streamGeometry2.Freeze();
		Pen pen = new Pen(Stroke ?? Brushes.White, num)
		{
			LineJoin = PenLineJoin.Round,
			StartLineCap = PenLineCap.Round,
			EndLineCap = PenLineCap.Round
		};
		drawingContext.DrawGeometry(null, pen, streamGeometry2);
		if (ShowEndpoint)
		{
			drawingContext.DrawEllipse(Stroke ?? Brushes.White, null, array[^1], num + 1.4, num + 1.4);
		}
	}
}
