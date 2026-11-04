using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Animation;
using Summer.Helpers;
using Summer.ViewModels;

namespace Summer.Views;

public partial class SettingsPage : UserControl, IComponentConnector
{
	public SettingsPage()
	{
		InitializeComponent();
	}

	private void OnLoaded(object sender, RoutedEventArgs e)
	{
		AnimateCard(GameCard, 0, 300);
		AnimateCard(InterfaceCard, 70, 300);
		AnimateCard(ControlsCard, 140, 300);
		AnimateCard(MaintenanceCard, 210, 300);
	}

	private void OnVolumeSliderPreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
	{
		if (DataContext is SettingsViewModel { IsUiSoundEnabled: not false })
		{
			UiSound.PlayTap();
		}
	}

	private static void AnimateCard(FrameworkElement element, int delayMs, int durationMs)
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
		if (element.RenderTransform is TranslateTransform translateTransform)
		{
			translateTransform.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(14.0, 0.0, timeSpan)
			{
				BeginTime = value,
				EasingFunction = easingFunction
			});
		}
	}
}
