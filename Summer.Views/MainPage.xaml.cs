using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using Summer.Models;
using Summer.Services;
using Summer.ViewModels;

namespace Summer.Views;

public partial class MainPage : UserControl, IComponentConnector, IStyleConnector
{
	private const double HeroCoverOpacity = 0.78;

	private static readonly TimeSpan HeroCoverFadeDuration = TimeSpan.FromMilliseconds(420.0);

	private MainViewModel? _viewModel;

	private Image? _activeCover;

	private string? _currentCoverUri;

	private int _coverDecodeWidth;

	public MainPage()
	{
		InitializeComponent();
		HeroCoverA.CacheMode = new BitmapCache();
		HeroCoverB.CacheMode = new BitmapCache();
		HeroCoverLayer.SizeChanged += (object _, SizeChangedEventArgs _) =>
		{
			RefreshCoverResolution();
		};
		DataContextChanged += OnDataContextChanged;
	}

	private void ServerCard_OnMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
	{
		CopyServerConnect(sender);
	}

	private void ServerCard_OnPreviewKeyDown(object sender, KeyEventArgs e)
	{
		Key key = e.Key;
		if (key == Key.Return || key == Key.Space)
		{
			CopyServerConnect(sender);
			e.Handled = true;
		}
	}

	private void CopyServerConnect(object sender)
	{
		if (sender is not ListBoxItem { IsEnabled: not false, DataContext: ServerInfo dataContext } listBoxItem)
		{
			return;
		}
		listBoxItem.IsSelected = true;
		string text = $"connect {dataContext.Host}:{dataContext.Port}";
		try
		{
			Clipboard.SetDataObject(text, copy: true);
			(Window.GetWindow(this) as MainWindow)?.ShowConnectCopied(text);
		}
		catch (ExternalException error)
		{
			ErrorLog.Write("Copy server connect", error);
		}
	}

	private void OnLoaded(object sender, RoutedEventArgs e)
	{
		if (_activeCover == null || _coverDecodeWidth != GetCoverDecodeWidth())
		{
			InitializeHeroCover();
		}
		AnimateIn(HeroCard, 0, 320, 12.0);
		AnimateIn(ServerPanel, 70, 320, 10.0);
		AnimateIn(Footer, 150, 240, 0.0);
	}

	private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
	{
		if (_viewModel != null)
		{
			_viewModel.PropertyChanged -= OnViewModelPropertyChanged;
		}
		_viewModel = e.NewValue as MainViewModel;
		if (_viewModel != null)
		{
			_viewModel.PropertyChanged += OnViewModelPropertyChanged;
			if (IsLoaded)
			{
				InitializeHeroCover();
			}
		}
		else
		{
			ClearHeroCover();
		}
	}

	private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
	{
		if (e.PropertyName == "SelectedServer")
		{
			AnimateServerInfo();
			CrossFadeHeroCover();
		}
	}

	private void InitializeHeroCover()
	{
		string text = _viewModel?.SelectedServer.CoverImage;
		if (string.IsNullOrWhiteSpace(text))
		{
			ClearHeroCover();
			return;
		}
		HeroCoverA.BeginAnimation(UIElement.OpacityProperty, null);
		HeroCoverB.BeginAnimation(UIElement.OpacityProperty, null);
		HeroCoverA.Source = LoadCover(text);
		HeroCoverA.Opacity = 0.78;
		HeroCoverB.Source = null;
		HeroCoverB.Opacity = 0.0;
		_currentCoverUri = text;
		_activeCover = HeroCoverA;
	}

	private void CrossFadeHeroCover()
	{
		string text = _viewModel?.SelectedServer.CoverImage;
		if (string.IsNullOrWhiteSpace(text))
		{
			ClearHeroCover();
		}
		else if (_activeCover == null)
		{
			InitializeHeroCover();
		}
		else if (!(text == _currentCoverUri))
		{
			Image image = ((_activeCover == HeroCoverA) ? HeroCoverB : HeroCoverA);
			Image activeCover = _activeCover;
			ImageSource imageSource = LoadCover(text);
			if (imageSource != null)
			{
				image.BeginAnimation(UIElement.OpacityProperty, null);
				image.Source = imageSource;
				image.Opacity = 0.0;
				CubicEase easingFunction = new CubicEase
				{
					EasingMode = EasingMode.EaseInOut
				};
				image.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(0.0, 0.78, HeroCoverFadeDuration)
				{
					EasingFunction = easingFunction
				});
				activeCover.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation
				{
					To = 0.0,
					Duration = HeroCoverFadeDuration,
					EasingFunction = easingFunction
				});
				_activeCover = image;
				_currentCoverUri = text;
			}
		}
	}

	private void ClearHeroCover()
	{
		HeroCoverA.BeginAnimation(UIElement.OpacityProperty, null);
		HeroCoverB.BeginAnimation(UIElement.OpacityProperty, null);
		HeroCoverA.Source = null;
		HeroCoverB.Source = null;
		HeroCoverA.Opacity = 0.78;
		HeroCoverB.Opacity = 0.0;
		_activeCover = null;
		_currentCoverUri = null;
	}

	protected override void OnDpiChanged(DpiScale oldDpi, DpiScale newDpi)
	{
		base.OnDpiChanged(oldDpi, newDpi);
		RefreshCoverResolution();
	}

	private int GetCoverDecodeWidth()
	{
		return (int)Math.Ceiling(Math.Max(1.0, HeroCoverLayer.ActualWidth) * VisualTreeHelper.GetDpi(this).DpiScaleX);
	}

	private void RefreshCoverResolution()
	{
		if (IsLoaded && _coverDecodeWidth != GetCoverDecodeWidth())
		{
			InitializeHeroCover();
		}
	}

	private ImageSource? LoadCover(string uri)
	{
		try
		{
			BitmapImage bitmapImage = new BitmapImage();
			bitmapImage.BeginInit();
			bitmapImage.UriSource = new Uri(uri, UriKind.RelativeOrAbsolute);
			_coverDecodeWidth = GetCoverDecodeWidth();
			bitmapImage.DecodePixelWidth = _coverDecodeWidth;
			bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
			bitmapImage.EndInit();
			bitmapImage.Freeze();
			return bitmapImage;
		}
		catch (Exception)
		{
			return null;
		}
	}

	private void AnimateServerInfo()
	{
		CubicEase easingFunction = new CubicEase
		{
			EasingMode = EasingMode.EaseOut
		};
		ServerInfoPanel.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(0.15, 1.0, TimeSpan.FromMilliseconds(190.0))
		{
			EasingFunction = easingFunction
		});
		if (ServerInfoPanel.RenderTransform is TranslateTransform translateTransform)
		{
			translateTransform.BeginAnimation(TranslateTransform.XProperty, new DoubleAnimation(10.0, 0.0, TimeSpan.FromMilliseconds(220.0))
			{
				EasingFunction = easingFunction
			});
		}
	}

	private static void AnimateIn(FrameworkElement element, int delayMs, int durationMs, double offsetY)
	{
		CubicEase easingFunction = new CubicEase
		{
			EasingMode = EasingMode.EaseOut
		};
		TimeSpan value = TimeSpan.FromMilliseconds(delayMs);
		TimeSpan timeSpan = TimeSpan.FromMilliseconds(durationMs);
		element.BeginAnimation(UIElement.OpacityProperty, null);
		element.Opacity = 0.0;
		element.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(0.0, 1.0, timeSpan)
		{
			BeginTime = value,
			EasingFunction = easingFunction
		});
		if (offsetY > 0.0 && element.RenderTransform is TranslateTransform translateTransform)
		{
			translateTransform.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(offsetY, 0.0, timeSpan)
			{
				BeginTime = value,
				EasingFunction = easingFunction
			});
		}
	}

	private void SupportLink_OnRequestNavigate(object sender, RequestNavigateEventArgs e)
	{
		if (e.Uri != null && (e.Uri.Scheme == Uri.UriSchemeHttp || e.Uri.Scheme == Uri.UriSchemeHttps))
		{
			try
			{
				Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri)
				{
					UseShellExecute = true
				});
			}
			catch
			{
			}
		}
		e.Handled = true;
	}
}
