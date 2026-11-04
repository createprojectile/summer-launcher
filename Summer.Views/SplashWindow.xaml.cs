using System;
using System.Windows;
using System.Windows.Markup;
using Summer.Helpers;
using Summer.ViewModels;

namespace Summer.Views;

public partial class SplashWindow : Window, IComponentConnector
{
	public SplashWindow()
	{
		DwmWindowEffects.ConfigureWindow(this);
		InitializeComponent();
		DwmWindowEffects.ConfigureSurface(this, WindowRoot, Surface);
	}

	protected override void OnSourceInitialized(EventArgs e)
	{
		base.OnSourceInitialized(e);
		DwmWindowEffects.Apply(this);
	}

	protected override void OnClosed(EventArgs e)
	{
		if (DataContext is SplashViewModel splashViewModel)
		{
			splashViewModel.Cancel();
		}
		base.OnClosed(e);
	}

	private void CloseButton_OnClick(object sender, RoutedEventArgs e)
	{
		Close();
	}
}
