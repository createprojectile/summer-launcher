using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Shell;

namespace Summer.Helpers;

internal static class DwmWindowEffects
{
	internal static bool IsSupported => OperatingSystem.IsWindowsVersionAtLeast(10, 0, 22000);

	internal static void ConfigureWindow(Window window)
	{
		window.SetResourceReference(FrameworkElement.StyleProperty, IsSupported ? "ChromeWindowStyle" : "LayeredChromeWindowStyle");
		if (IsSupported)
		{
			System.Windows.Shell.WindowChrome.SetWindowChrome(window, new System.Windows.Shell.WindowChrome
			{
				CaptionHeight = 58.0,
				GlassFrameThickness = new Thickness(1.0),
				ResizeBorderThickness = new Thickness(0.0),
				CornerRadius = new CornerRadius(0.0),
				UseAeroCaptionButtons = false
			});
		}
	}

	internal static void ConfigureSurface(Window window, Grid root, Border surface)
	{
		if (IsSupported)
		{
			surface.CornerRadius = new CornerRadius(0.0);
			surface.BorderThickness = new Thickness(0.0);
			return;
		}
		root.Margin = new Thickness(10.0);
		root.Children.Insert(0, new Border
		{
			Style = (Style)window.FindResource("WindowShadowStyle"),
			IsHitTestVisible = false
		});
		WindowChrome.SetRoundedClip(surface, 8.0);
	}

	internal static void Apply(Window window)
	{
		if (IsSupported)
		{
			nint handle = new WindowInteropHelper(window).Handle;
			SetAttribute(handle, 20, 1);
			SetAttribute(handle, 33, 2);
			SetAttribute(handle, 34, 2763306);
		}
	}

	private static void SetAttribute(nint handle, int attribute, int value)
	{
		DwmSetWindowAttribute(handle, attribute, ref value, 4);
	}

	[DllImport("dwmapi.dll", ExactSpelling = true)]
	private static extern int DwmSetWindowAttribute(nint hwnd, int attribute, ref int value, int size);
}
