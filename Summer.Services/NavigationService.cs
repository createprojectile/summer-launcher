using System;
using System.ComponentModel;
using System.Windows;
using Summer.Models;
using Summer.ViewModels;
using Summer.Views;

namespace Summer.Services;

public sealed class NavigationService
{
	private readonly SettingsService _settingsService;

	private readonly LauncherSettings _settings;

	private readonly UpdateService _updateService;

	private readonly InstallService _installService;

	private readonly GamePlacementService _gamePlacementService;

	private readonly ServerQueryService _serverQueryService;

	private Window? _currentWindow;

	private bool _isStarted;

	public NavigationService(SettingsService settingsService, LauncherSettings settings, UpdateService updateService, InstallService installService, GamePlacementService gamePlacementService, ServerQueryService serverQueryService)
	{
		_settingsService = settingsService;
		_settings = settings;
		_updateService = updateService;
		_installService = installService;
		_gamePlacementService = gamePlacementService;
		_serverQueryService = serverQueryService;
	}

	public void Start()
	{
		if (!_isStarted)
		{
			_isStarted = true;
			ShowSplash();
		}
	}

	private void ShowSplash()
	{
		SplashViewModel viewModel = new SplashViewModel(_updateService);
		SplashWindow window = new SplashWindow
		{
			DataContext = viewModel
		};
		bool isClosed = false;
		window.Closed += (object? _, EventArgs _) =>
		{
			isClosed = true;
			viewModel.Dispose();
			if (_currentWindow == window)
			{
				_currentWindow = null;
			}
		};
		viewModel.StartupCompleted += () =>
		{
			if (!isClosed)
			{
				ShowMain();
			}
		};
		Present(window);
		_ = viewModel.RunAsync();
	}

	private void ShowMain()
	{
		MainViewModel viewModel = new MainViewModel(_settingsService, _settings, _updateService, _installService, _gamePlacementService, _serverQueryService);
		MainWindow window = new MainWindow
		{
			DataContext = viewModel
		};
		window.Closing += (object? _, CancelEventArgs _) =>
		{
			viewModel.Settings.Flush();
			_settingsService.Save(_settings);
		};
		window.Closed += (object? _, EventArgs _) =>
		{
			viewModel.Dispose();
			if (_currentWindow == window)
			{
				_currentWindow = null;
			}
		};
		Present(window);
		_ = viewModel.InitializeAsync();
	}

	private void Present(Window window)
	{
		Window currentWindow = _currentWindow;
		_currentWindow = window;
		if (Application.Current != null)
		{
			Application.Current.MainWindow = window;
		}
		window.Show();
		window.Activate();
		if (currentWindow != null && currentWindow != window)
		{
			currentWindow.Close();
		}
	}
}
