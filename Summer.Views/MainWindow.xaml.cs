using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using Summer.Helpers;

namespace Summer.Views;

public partial class MainWindow : Window, IComponentConnector
{
	private readonly DispatcherTimer _connectToastTimer = new DispatcherTimer
	{
		Interval = TimeSpan.FromSeconds(2.6)
	};

	private int _connectToastVersion;

	public MainWindow()
	{
		DwmWindowEffects.ConfigureWindow(this);
		InitializeComponent();
		DwmWindowEffects.ConfigureSurface(this, WindowRoot, Surface);
		_connectToastTimer.Tick += (object? _, EventArgs _) =>
		{
			HideConnectToast();
		};
		Closed += (object? _, EventArgs _) =>
		{
			_connectToastTimer.Stop();
		};
	}

	public void ShowConnectCopied(string connect)
	{
		_connectToastVersion++;
		_connectToastTimer.Stop();
		ConnectToastAddress.Text = connect;
		ConnectToast.Visibility = Visibility.Visible;
		ConnectToast.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(1.0, TimeSpan.FromMilliseconds(160.0)));
		((TranslateTransform)ConnectToast.RenderTransform).BeginAnimation(TranslateTransform.XProperty, new DoubleAnimation(12.0, 0.0, TimeSpan.FromMilliseconds(180.0))
		{
			EasingFunction = new CubicEase
			{
				EasingMode = EasingMode.EaseOut
			}
		});
		ConnectToastLifetime.BeginAnimation(RangeBase.ValueProperty, new DoubleAnimation(100.0, 0.0, _connectToastTimer.Interval));
		_connectToastTimer.Start();
	}

	private void HideConnectToast()
	{
		_connectToastTimer.Stop();
		int version = _connectToastVersion;
		DoubleAnimation doubleAnimation = new DoubleAnimation(0.0, TimeSpan.FromMilliseconds(160.0));
		doubleAnimation.Completed += (object? _, EventArgs _) =>
		{
			if (version == _connectToastVersion)
			{
				ConnectToast.Visibility = Visibility.Collapsed;
			}
		};
		ConnectToast.BeginAnimation(UIElement.OpacityProperty, doubleAnimation);
	}

	protected override void OnSourceInitialized(EventArgs e)
	{
		base.OnSourceInitialized(e);
		DwmWindowEffects.Apply(this);
	}

	private void PageHost_OnIsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
	{
		if (sender is FrameworkElement { IsVisible: not false } frameworkElement)
		{
			frameworkElement.BeginAnimation(UIElement.OpacityProperty, null);
			frameworkElement.Opacity = 1.0;
			CubicEase easingFunction = new CubicEase
			{
				EasingMode = EasingMode.EaseOut
			};
			frameworkElement.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(0.0, 1.0, TimeSpan.FromMilliseconds(240.0))
			{
				EasingFunction = easingFunction
			});
			if (frameworkElement.RenderTransform is TranslateTransform translateTransform)
			{
				translateTransform.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(12.0, 0.0, TimeSpan.FromMilliseconds(300.0))
				{
					EasingFunction = easingFunction
				});
			}
		}
	}

	private void MinimizeButton_OnClick(object sender, RoutedEventArgs e)
	{
		WindowState = WindowState.Minimized;
	}

	private void CloseButton_OnClick(object sender, RoutedEventArgs e)
	{
		Close();
	}

	private void SocialLinkButton_OnClick(object sender, RoutedEventArgs e)
	{
		if (sender is not Button { CommandParameter: string commandParameter } || !Uri.TryCreate(commandParameter, UriKind.Absolute, out Uri result) || (result.Scheme != Uri.UriSchemeHttp && result.Scheme != Uri.UriSchemeHttps))
		{
			return;
		}
		try
		{
			Process.Start(new ProcessStartInfo(result.AbsoluteUri)
			{
				UseShellExecute = true
			});
		}
		catch
		{
		}
	}
}
