using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Threading;
using Summer.Helpers;
using Summer.Models;
using Summer.Services;

namespace Summer;

public partial class App : Application
{
	private NavigationService? _navigationService;

	protected override void OnStartup(StartupEventArgs e)
	{
		base.OnStartup(e);
		DispatcherUnhandledException += (object _, DispatcherUnhandledExceptionEventArgs args) =>
		{
			ErrorLog.Write("UI", args.Exception);
		};
		AppDomain.CurrentDomain.UnhandledException += (object _, UnhandledExceptionEventArgs args) =>
		{
			ErrorLog.Write("Process", args.ExceptionObject?.ToString() ?? "Unknown error");
		};
		TaskScheduler.UnobservedTaskException += (object? _, UnobservedTaskExceptionEventArgs args) =>
		{
			ErrorLog.Write("Task", args.Exception);
		};
		UpdateService.CleanupOldBinary();
		SettingsService settingsService = new SettingsService();
		LauncherSettings launcherSettings = settingsService.Load();
		UiSound.Initialize(launcherSettings.UiSoundsEnabled, launcherSettings.UiSoundVolume);
		EventManager.RegisterClassHandler(typeof(ButtonBase), ButtonBase.ClickEvent, new RoutedEventHandler(OnButtonClick));
		UpdateService updateService = new UpdateService(launcherSettings);
		InstallService installService = new InstallService();
		GamePlacementService gamePlacementService = new GamePlacementService();
		ServerQueryService serverQueryService = new ServerQueryService();
		_navigationService = new NavigationService(settingsService, launcherSettings, updateService, installService, gamePlacementService, serverQueryService);
		_navigationService.Start();
	}

	protected override void OnExit(ExitEventArgs e)
	{
		base.OnExit(e);
	}

	private static void OnButtonClick(object sender, RoutedEventArgs e)
	{
		if (sender is not RepeatButton)
		{
			UiSound.PlayTap();
		}
	}
}
