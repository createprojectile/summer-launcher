using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;

namespace Summer.Helpers;

public static class WindowChrome
{
	public static readonly DependencyProperty RoundedClipProperty = DependencyProperty.RegisterAttached("RoundedClip", typeof(double), typeof(WindowChrome), new PropertyMetadata(0.0, OnRoundedClipChanged));

	public static readonly DependencyProperty IsDragAreaProperty = DependencyProperty.RegisterAttached("IsDragArea", typeof(bool), typeof(WindowChrome), new PropertyMetadata(false, OnIsDragAreaChanged));

	public static void SetRoundedClip(DependencyObject element, double value)
	{
		element.SetValue(RoundedClipProperty, value);
	}

	public static double GetRoundedClip(DependencyObject element)
	{
		return (double)element.GetValue(RoundedClipProperty);
	}

	private static void OnRoundedClipChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
	{
		if (d is not FrameworkElement frameworkElement)
		{
			return;
		}
		frameworkElement.SizeChanged -= OnSizeChanged;
		frameworkElement.Loaded -= OnLoaded;
		if ((double)e.NewValue > 0.0)
		{
			frameworkElement.SizeChanged += OnSizeChanged;
			frameworkElement.Loaded += OnLoaded;
			if (frameworkElement.IsLoaded)
			{
				ApplyClip(frameworkElement);
			}
		}
		else
		{
			frameworkElement.Clip = null;
		}
	}

	private static void OnLoaded(object sender, RoutedEventArgs e)
	{
		if (sender is FrameworkElement element)
		{
			ApplyClip(element);
		}
	}

	private static void OnSizeChanged(object sender, SizeChangedEventArgs e)
	{
		if (sender is FrameworkElement element)
		{
			ApplyClip(element);
		}
	}

	private static void ApplyClip(FrameworkElement element)
	{
		double roundedClip = GetRoundedClip(element);
		if (!(roundedClip <= 0.0) && !(element.ActualWidth <= 0.0) && !(element.ActualHeight <= 0.0))
		{
			element.Clip = new RectangleGeometry(new Rect(0.0, 0.0, element.ActualWidth, element.ActualHeight), roundedClip, roundedClip);
		}
	}

	public static void SetIsDragArea(DependencyObject element, bool value)
	{
		element.SetValue(IsDragAreaProperty, value);
	}

	public static bool GetIsDragArea(DependencyObject element)
	{
		return (bool)element.GetValue(IsDragAreaProperty);
	}

	private static void OnIsDragAreaChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
	{
		if (d is FrameworkElement frameworkElement)
		{
			frameworkElement.MouseLeftButtonDown -= OnMouseLeftButtonDown;
			object newValue = e.NewValue;
			if (newValue is bool && (bool)newValue)
			{
				frameworkElement.MouseLeftButtonDown += OnMouseLeftButtonDown;
			}
		}
	}

	private static void OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
	{
		if (e.ChangedButton != MouseButton.Left || e.ButtonState != MouseButtonState.Pressed || !(sender is DependencyObject dependencyObject))
		{
			return;
		}
		Window window = Window.GetWindow(dependencyObject);
		if (window == null || (e.OriginalSource is DependencyObject origin && FindInteractiveAncestor(origin) != null))
		{
			return;
		}
		try
		{
			window.DragMove();
		}
		catch (InvalidOperationException)
		{
		}
	}

	private static DependencyObject? FindInteractiveAncestor(DependencyObject origin)
	{
		for (DependencyObject dependencyObject = origin; dependencyObject != null; dependencyObject = ((dependencyObject is Visual) ? VisualTreeHelper.GetParent(dependencyObject) : LogicalTreeHelper.GetParent(dependencyObject)))
		{
			if (dependencyObject is ButtonBase or TextBoxBase or PasswordBox or ScrollBar or Selector or ListBoxItem or Slider)
			{
				return dependencyObject;
			}
		}
		return null;
	}
}
