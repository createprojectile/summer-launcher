using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using Summer.Helpers;
using Summer.Models;
using Summer.Services;
using Summer.Views;

namespace Summer.ViewModels;

public sealed class MainViewModel : ViewModelBase, IDisposable
{
	private readonly SettingsService _settingsService;

	private readonly LauncherSettings _settings;

	private readonly InstallService _installService;

	private readonly GamePlacementService _gamePlacementService;

	private readonly ServerQueryService _serverQueryService;

	private readonly CancellationTokenSource _lifetimeToken = new CancellationTokenSource();

	private readonly AsyncRelayCommand _playCommand;

	private Process? _gameProcess;

	private ServerInfo _selectedServer;

	private LauncherSection _currentSection;

	private bool _isLoading;

	private bool _isClientRunning;

	private double _loadingProgress;

	private string _loadingTitle = string.Empty;

	private string _loadingDetail = string.Empty;

	private string _idleStatusText = "ГОТОВ К ЗАПУСКУ";

	private bool _disposed;

	private InstallState? _localInstallState;

	private string? _remoteVersion;

	private bool _isInstalling;

	private CancellationTokenSource? _installCts;

	private Task<bool>? _installTask;

	private bool _fullVerificationRequested;

	private bool _isPaused;

	private string _downloadSpeedText = string.Empty;

	private string _downloadSizeText = string.Empty;

	private readonly TaskCompletionSource<bool> _initTcs = new TaskCompletionSource<bool>();

	private string? _installError;

	private readonly DispatcherTimer _wipeTimer;

	private readonly DispatcherTimer _serverQueryTimer;

	private static readonly TimeSpan MoscowUtcOffset = TimeSpan.FromHours(3.0);

	public bool IsLauncherLocationValid => _gamePlacementService.IsLauncherLocationValid;

	public SettingsViewModel Settings { get; }

	public IReadOnlyList<ServerInfo> Servers { get; }

	public IReadOnlyList<ToolLink> Tools { get; private set; }

	public RelayCommand<string> NavigateCommand { get; }

	public RelayCommand ConfigureCommand { get; }

	public RelayCommand BackToHomeCommand { get; }

	public RelayCommand<ToolLink> OpenToolCommand { get; }

	public AsyncRelayCommand TogglePauseCommand { get; }

	public RelayCommand CancelDownloadCommand { get; }

	public RelayCommand<string> SelectGraphicsPresetCommand { get; }

	public AsyncRelayCommand PlayCommand => _playCommand;

	public AsyncRelayCommand FullVerifyCommand { get; }

	public ServerInfo SelectedServer
	{
		get
		{
			return _selectedServer;
		}
		set
		{
			if (value != null && SetProperty(ref _selectedServer, value, "SelectedServer"))
			{
				_settings.SelectedServerId = value.Id;
				_settingsService.Save(_settings);
				OnPropertiesChanged("SelectedServer", "OnlinePlayersText", "SelectedServerIdleText");
			}
		}
	}

	public string OnlinePlayersText => SelectedServer.Online.ToString("N0", CultureInfo.GetCultureInfo("ru-RU"));

	public string SelectedServerIdleText => $"{SelectedServer.Name} · {OnlinePlayersText} {LocalizationService.Instance["Server_Players"]}";

	public bool IsLoading
	{
		get
		{
			return _isLoading;
		}
		private set
		{
			if (SetProperty(ref _isLoading, value, "IsLoading"))
			{
				OnPropertiesChanged("IsNotLoading", "PlayButtonText", "CanChangeServer");
				_playCommand.RaiseCanExecuteChanged();
				FullVerifyCommand?.RaiseCanExecuteChanged();
			}
		}
	}

	public bool IsNotLoading => !IsLoading;

	public bool CanChangeServer => !IsLoading;

	public bool IsInstalling
	{
		get
		{
			return _isInstalling;
		}
		private set
		{
			if (SetProperty(ref _isInstalling, value, "IsInstalling"))
			{
				OnPropertiesChanged("IsNotInstalling", "PlayButtonText");
			}
		}
	}

	public bool IsNotInstalling => !IsInstalling;

	public bool IsPaused
	{
		get
		{
			return _isPaused;
		}
		private set
		{
			if (SetProperty(ref _isPaused, value, "IsPaused"))
			{
				OnPropertiesChanged("PauseButtonText", "PauseButtonTooltip", "PauseButtonIcon");
			}
		}
	}

	public string DownloadSpeedText
	{
		get
		{
			return _downloadSpeedText;
		}
		private set
		{
			SetProperty(ref _downloadSpeedText, value, "DownloadSpeedText");
		}
	}

	public string DownloadSizeText
	{
		get
		{
			return _downloadSizeText;
		}
		private set
		{
			SetProperty(ref _downloadSizeText, value, "DownloadSizeText");
		}
	}

	public string PauseButtonText
	{
		get
		{
			if (!IsPaused)
			{
				return LocalizationService.Instance["Btn_Pause"];
			}
			return LocalizationService.Instance["Btn_Resume"];
		}
	}

	public string PauseButtonTooltip
	{
		get
		{
			if (!IsPaused)
			{
				return LocalizationService.Instance["Btn_PauseTooltip"];
			}
			return LocalizationService.Instance["Btn_ResumeTooltip"];
		}
	}

	public string PauseButtonIcon
	{
		get
		{
			if (!IsPaused)
			{
				return "⏸";
			}
			return "▶";
		}
	}

	public string PlayButtonText
	{
		get
		{
			LocalizationService instance = LocalizationService.Instance;
			if (_isInstalling)
			{
				return instance["Btn_Installing"];
			}
			if (IsLoading)
			{
				return instance["Btn_Launching"];
			}
			if (IsClientRunning)
			{
				return instance["Btn_InGame"];
			}
			if (IsInstallNeeded)
			{
				return instance["Btn_Download"];
			}
			if (IsUpdateAvailable)
			{
				return instance["Btn_Update"];
			}
			return instance["Btn_Play"];
		}
	}

	public bool IsInstallNeeded
	{
		get
		{
			if (!_gamePlacementService.IsGameInstalled(_settings.InstallPath, out string _))
			{
				return true;
			}
			return false;
		}
	}

	public bool IsUpdateAvailable
	{
		get
		{
			if (!IsInstallNeeded && _localInstallState != null && _remoteVersion != null)
			{
				return !string.Equals(_remoteVersion, _localInstallState.Version, StringComparison.OrdinalIgnoreCase);
			}
			return false;
		}
	}

	public double LoadingProgress
	{
		get
		{
			return _loadingProgress;
		}
		private set
		{
			SetProperty(ref _loadingProgress, value, "LoadingProgress");
		}
	}

	public string LoadingTitle
	{
		get
		{
			return _loadingTitle;
		}
		private set
		{
			SetProperty(ref _loadingTitle, value, "LoadingTitle");
		}
	}

	public string LoadingDetail
	{
		get
		{
			return _loadingDetail;
		}
		private set
		{
			SetProperty(ref _loadingDetail, value, "LoadingDetail");
		}
	}

	public string IdleStatusText
	{
		get
		{
			return _idleStatusText;
		}
		private set
		{
			if (SetProperty(ref _idleStatusText, value, "IdleStatusText"))
			{
				OnPropertiesChanged("IsReadyToLaunch", "IsUpdateStatus");
			}
		}
	}

	public bool IsReadyToLaunch
	{
		get
		{
			if (!string.Equals(IdleStatusText, "ГОТОВ К ЗАПУСКУ", StringComparison.OrdinalIgnoreCase))
			{
				return string.Equals(IdleStatusText, "READY TO LAUNCH", StringComparison.OrdinalIgnoreCase);
			}
			return true;
		}
	}

	public bool IsUpdateStatus => string.Equals(IdleStatusText, LocalizationService.Instance["Status_UpdateAvailable"], StringComparison.Ordinal);

	public bool IsClientRunning
	{
		get
		{
			return _isClientRunning;
		}
		private set
		{
			if (SetProperty(ref _isClientRunning, value, "IsClientRunning"))
			{
				OnPropertyChanged("IdleStatusDetail");
				OnPropertyChanged("PlayButtonText");
				_playCommand.RaiseCanExecuteChanged();
				FullVerifyCommand?.RaiseCanExecuteChanged();
			}
		}
	}

	public string IdleStatusDetail
	{
		get
		{
			if (!IsClientRunning)
			{
				return "Система готова к запуску";
			}
			return "Клиент Rust запущен";
		}
	}

	public LauncherSection CurrentSection
	{
		get
		{
			return _currentSection;
		}
		private set
		{
			if (SetProperty(ref _currentSection, value, "CurrentSection"))
			{
				OnPropertiesChanged("IsHomeActive", "IsGraphicsActive", "IsToolsActive", "IsSettingsActive");
			}
		}
	}

	public bool IsHomeActive => CurrentSection == LauncherSection.Home;

	public bool IsGraphicsActive => CurrentSection == LauncherSection.Graphics;

	public bool IsToolsActive => CurrentSection == LauncherSection.Tools;

	public bool IsSettingsActive => CurrentSection == LauncherSection.Settings;

	public string SelectedGraphicsPreset
	{
		get
		{
			return _settings.GraphicsPreset;
		}
		set
		{
			if (!string.Equals(_settings.GraphicsPreset, value, StringComparison.OrdinalIgnoreCase))
			{
				_settings.GraphicsPreset = value;
				_settingsService.Save(_settings);
				ApplyGraphicsPreset(value);
				OnPropertiesChanged("SelectedGraphicsPreset", "IsCombatPresetSelected", "IsOptimalPresetSelected", "IsUltraPresetSelected");
			}
			else
			{
				ApplyGraphicsPreset(value);
			}
		}
	}

	public bool IsCombatPresetSelected => string.Equals(SelectedGraphicsPreset, "combat", StringComparison.OrdinalIgnoreCase);

	public bool IsOptimalPresetSelected => string.Equals(SelectedGraphicsPreset, "optimal", StringComparison.OrdinalIgnoreCase);

	public bool IsUltraPresetSelected => string.Equals(SelectedGraphicsPreset, "ultra", StringComparison.OrdinalIgnoreCase);

	public MainViewModel(SettingsService settingsService, LauncherSettings settings, UpdateService updateService, InstallService installService, GamePlacementService gamePlacementService, ServerQueryService serverQueryService)
	{
		MainViewModel mainViewModel = this;
		_settingsService = settingsService;
		_settings = settings;
		_installService = installService;
		_gamePlacementService = gamePlacementService;
		_settings.InstallPath = gamePlacementService.LauncherDirectory;
		_serverQueryService = serverQueryService;
		_localInstallState = InstallState.TryLoad(settings.InstallPath);
		Servers = CreateServers();
		_selectedServer = Servers.FirstOrDefault((ServerInfo server) => server.Id == settings.SelectedServerId) ?? Servers[0];
		settings.SelectedServerId = _selectedServer.Id;
		foreach (ServerInfo server in Servers)
		{
			server.PropertyChanged += (object? s, PropertyChangedEventArgs e) =>
			{
				if (s == mainViewModel._selectedServer)
				{
					bool flag;
					switch (e.PropertyName)
					{
					case "Online":
					case "Capacity":
					case "Ping":
						flag = true;
						break;
					default:
						flag = false;
						break;
					}
					if (flag)
					{
						mainViewModel.OnPropertiesChanged("OnlinePlayersText", "SelectedServerIdleText");
					}
				}
			};
		}
		Settings = new SettingsViewModel(settingsService, settings, updateService, gamePlacementService);
		Settings.PropertyChanged += (object? s, PropertyChangedEventArgs e) =>
		{
			if (e.PropertyName == "InstallPath")
			{
				mainViewModel._localInstallState = InstallState.TryLoad(mainViewModel._settings.InstallPath);
				mainViewModel.RefreshInstallUi();
				mainViewModel.FullVerifyCommand?.RaiseCanExecuteChanged();
			}
		};
		Tools = CreateTools();
		LocalizationService.Instance.PropertyChanged += (object? _, PropertyChangedEventArgs e) =>
		{
			bool flag;
			switch (e.PropertyName)
			{
			case null:
			case "Item[]":
			case "CurrentLanguage":
				flag = true;
				break;
			default:
				flag = false;
				break;
			}
			if (flag)
			{
				mainViewModel.Tools = CreateTools();
				mainViewModel.RefreshInstallUi();
				mainViewModel.OnPropertiesChanged("OnlinePlayersText", "SelectedServerIdleText", "PlayButtonText", "IdleStatusText", "PauseButtonText", "PauseButtonTooltip", "Tools");
			}
		};
		NavigateCommand = new RelayCommand<string>(NavigateTo);
		ConfigureCommand = new RelayCommand(() =>
		{
			mainViewModel.NavigateTo(LauncherSection.Settings);
		});
		BackToHomeCommand = new RelayCommand(() =>
		{
			mainViewModel.NavigateTo(LauncherSection.Home);
		});
		OpenToolCommand = new RelayCommand<ToolLink>(OpenTool);
		TogglePauseCommand = new AsyncRelayCommand(TogglePauseAsync);
		CancelDownloadCommand = new RelayCommand(CancelDownload);
		SelectGraphicsPresetCommand = new RelayCommand<string>(SelectGraphicsPreset);
		_wipeTimer = new DispatcherTimer
		{
			Interval = TimeSpan.FromSeconds(1.0)
		};
		_wipeTimer.Tick += (object? _, EventArgs _) =>
		{
			mainViewModel.UpdateWipeCountdown();
		};
		_wipeTimer.Start();
		UpdateWipeCountdown();
		_serverQueryTimer = new DispatcherTimer
		{
			Interval = TimeSpan.FromSeconds(30.0)
		};
		_serverQueryTimer.Tick += async (object? _, EventArgs _) =>
		{
			await mainViewModel.RefreshServersOnlineAsync();
		};
		_serverQueryTimer.Start();
		_playCommand = new AsyncRelayCommand(PlayAsync, () => mainViewModel.IsLauncherLocationValid && !mainViewModel.IsLoading && !mainViewModel.IsClientRunning);
		FullVerifyCommand = new AsyncRelayCommand(async () =>
		{
			mainViewModel.NavigateTo(LauncherSection.Home);
			await mainViewModel.RunInstallAsync(forceFullVerification: true).ConfigureAwait(continueOnCapturedContext: true);
		}, () => mainViewModel.IsLauncherLocationValid && !mainViewModel.IsLoading && !mainViewModel.IsClientRunning);
	}

	public async Task InitializeAsync()
	{
		try
		{
			_remoteVersion = (await _installService.LoadManifestAsync(_lifetimeToken.Token).ConfigureAwait(continueOnCapturedContext: true)).Version;
		}
		catch (Exception)
		{
			_remoteVersion = null;
		}
		finally
		{
			_initTcs.TrySetResult(result: true);
		}
		RefreshInstallUi();
		_ = RefreshServersOnlineAsync();
	}

	public async Task RefreshServersOnlineAsync()
	{
		try
		{
			foreach (ServerInfo server in Servers)
			{
				if (string.IsNullOrWhiteSpace(server.Host) || server.QueryPort <= 0)
				{
					continue;
				}
				ServerQueryResult serverQueryResult = await _serverQueryService.QueryServerAsync(server, _lifetimeToken.Token).ConfigureAwait(continueOnCapturedContext: true);
				if (serverQueryResult.IsOnline)
				{
					server.Online = serverQueryResult.Online;
					server.Capacity = serverQueryResult.MaxPlayers;
					server.Ping = serverQueryResult.PingMs;
					server.IsOnline = true;
					if (!string.IsNullOrWhiteSpace(serverQueryResult.Map))
					{
						server.Map = serverQueryResult.Map;
					}
				}
				else
				{
					server.IsOnline = false;
				}
			}
			OnPropertiesChanged("OnlinePlayersText", "SelectedServerIdleText");
		}
		catch
		{
		}
	}

	private void RefreshInstallUi()
	{
		OnPropertiesChanged("IsInstallNeeded", "IsUpdateAvailable", "PlayButtonText");
		if (!IsLoading && !IsClientRunning)
		{
			LocalizationService instance = LocalizationService.Instance;
			MainViewModel mainViewModel = this;
			string idleStatusText;
			if (_installError != null)
			{
				idleStatusText = instance["Status_InstallError"];
			}
			else if (IsUpdateAvailable)
			{
				idleStatusText = instance["Status_UpdateAvailable"];
			}
			else
			{
				idleStatusText = (IsInstallNeeded ? instance["Status_InstallNeeded"] : instance["Status_Ready"]);
			}
			mainViewModel.IdleStatusText = idleStatusText;
		}
	}

	private Task<bool> RunInstallAsync(bool forceFullVerification = false)
	{
		Task<bool> installTask = _installTask;
		if (installTask == null || installTask.IsCompleted)
		{
			return _installTask = RunInstallCoreAsync(forceFullVerification);
		}
		return _installTask;
	}

	private async Task<bool> RunInstallCoreAsync(bool forceFullVerification)
	{
		if (!IsLauncherLocationValid)
		{
			return false;
		}
		_fullVerificationRequested = forceFullVerification;
		_installError = null;
		IsInstalling = true;
		IsPaused = false;
		IsLoading = true;
		LoadingProgress = 0.0;
		IdleStatusText = "УСТАНОВКА КЛИЕНТА";
		DownloadSpeedText = string.Empty;
		DownloadSizeText = string.Empty;
		OnPropertiesChanged("PlayButtonText");
		_installCts?.Dispose();
		_installCts = CancellationTokenSource.CreateLinkedTokenSource(_lifetimeToken.Token);
		CancellationToken ct = _installCts.Token;
		Progress<InstallProgress> progress = new Progress<InstallProgress>((InstallProgress step) =>
		{
			if (!ct.IsCancellationRequested)
			{
				LoadingProgress = step.Percent;
				LoadingTitle = step.Stage;
				LoadingDetail = step.Detail;
				DownloadSpeedText = step.Speed;
				DownloadSizeText = step.SizeText;
			}
		});
		InstallResult installResult;
		try
		{
			installResult = await _installService.EnsureInstalledAsync(_settings, progress, ct, forceFullVerification).ConfigureAwait(continueOnCapturedContext: true);
		}
		catch (OperationCanceledException)
		{
			if (IsPaused)
			{
				IdleStatusText = "ПАУЗА СКАЧИВАНИЯ";
				DownloadSpeedText = "0 МБ/с";
				return false;
			}
			IsInstalling = false;
			IsLoading = false;
			DownloadSpeedText = string.Empty;
			DownloadSizeText = string.Empty;
			RefreshInstallUi();
			return false;
		}
		catch (Exception ex2)
		{
			ErrorLog.Write("Installation", ex2);
			installResult = new InstallResult(Success: false, string.Empty, ex2.Message);
		}
		if (IsPaused)
		{
			IdleStatusText = "ПАУЗА СКАЧИВАНИЯ";
			DownloadSpeedText = "0 МБ/с";
			return false;
		}
		if (ct.IsCancellationRequested)
		{
			IsInstalling = false;
			IsLoading = false;
			DownloadSpeedText = string.Empty;
			DownloadSizeText = string.Empty;
			RefreshInstallUi();
			return false;
		}
		IsInstalling = false;
		IsLoading = false;
		DownloadSpeedText = string.Empty;
		if (installResult.Success)
		{
			_localInstallState = InstallState.TryLoad(_settings.InstallPath);
			Settings.SaveNow();
			IdleStatusText = "УСТАНОВКА ЗАВЕРШЕНА";
		}
		else
		{
			ErrorLog.Write("Installation", installResult.Error ?? "Unknown error");
			_installError = installResult.Error;
			_localInstallState = InstallState.TryLoad(_settings.InstallPath);
			IdleStatusText = "ОШИБКА УСТАНОВКИ";
			LoadingTitle = "ОШИБКА УСТАНОВКИ";
			LoadingDetail = installResult.Error ?? "Неизвестная ошибка";
		}
		RefreshInstallUi();
		return installResult.Success;
	}

	private async Task TogglePauseAsync()
	{
		if (!IsInstalling)
		{
			return;
		}
		if (!IsPaused)
		{
			IsPaused = true;
			LoadingTitle = "ПАУЗА";
			DownloadSpeedText = "0 МБ/с";
			IdleStatusText = "ПАУЗА СКАЧИВАНИЯ";
			try
			{
				_installCts?.Cancel();
				return;
			}
			catch
			{
				return;
			}
		}
		if (_installTask != null)
		{
			await _installTask.ConfigureAwait(continueOnCapturedContext: true);
		}
		if (IsInstalling && IsPaused)
		{
			IsPaused = false;
			IdleStatusText = "СКАЧИВАНИЕ КЛИЕНТА";
			_ = RunInstallAsync(_fullVerificationRequested);
		}
	}

	private void CancelDownload()
	{
		if (IsInstalling)
		{
			IsPaused = false;
			IsInstalling = false;
			IsLoading = false;
			DownloadSpeedText = string.Empty;
			DownloadSizeText = string.Empty;
			try
			{
				_installCts?.Cancel();
			}
			catch
			{
			}
			RefreshInstallUi();
		}
	}

	private void LaunchRustGame()
	{
		if (!_gamePlacementService.CanLaunchGame(_settings.InstallPath, out string rustExePath, out string statusMessage))
		{
			MessageBox.Show(statusMessage, "Запуск игры", MessageBoxButton.OK, MessageBoxImage.Exclamation);
			return;
		}
		IsLoading = true;
		LoadingProgress = 0.0;
		IdleStatusText = "ЗАПУСК КЛИЕНТА";
		LoadingTitle = "Запуск RustClient.exe";
		LoadingDetail = "Старт процесса игры…";
		try
		{
			string arguments = _settings.LaunchArguments ?? string.Empty;
			Process process = _gamePlacementService.LaunchGame(rustExePath, arguments, _settings.InstallPath, out string error);
			if (process == null || error != null)
			{
				ErrorLog.Write("Game launch", error ?? "Process.Start returned null");
				MessageBox.Show(error ?? "Не удалось запустить процесс игры.", "Ошибка запуска Rust", MessageBoxButton.OK, MessageBoxImage.Hand);
				IdleStatusText = "ОШИБКА ЗАПУСКА";
				return;
			}
			_gameProcess = process;
			IsClientRunning = true;
			IdleStatusText = "КЛИЕНТ ЗАПУЩЕН";
			if (_settings.MinimizeOnGameLaunch)
			{
				MinimizeLauncher();
			}
			try
			{
				process.EnableRaisingEvents = true;
				process.Exited += (object? _, EventArgs _) =>
				{
					Application.Current?.Dispatcher.Invoke(() =>
					{
						IsClientRunning = false;
						_gameProcess = null;
						RefreshInstallUi();
						if (_settings.MinimizeOnGameLaunch)
						{
							RestoreLauncher();
						}
					});
				};
			}
			catch
			{
			}
		}
		finally
		{
			IsLoading = false;
		}
	}

	private static void MinimizeLauncher()
	{
		Application.Current?.Dispatcher.Invoke(() =>
		{
			Window window = Application.Current.MainWindow ?? Application.Current.Windows.OfType<MainWindow>().FirstOrDefault();
			if (window != null)
			{
				window.WindowState = WindowState.Minimized;
			}
		});
	}

	private static void RestoreLauncher()
	{
		Application.Current?.Dispatcher.Invoke(() =>
		{
			Window window = Application.Current.MainWindow ?? Application.Current.Windows.OfType<MainWindow>().FirstOrDefault();
			if (window != null)
			{
				if (window.WindowState == WindowState.Minimized)
				{
					window.WindowState = WindowState.Normal;
				}
				window.Activate();
				window.Focus();
			}
		});
	}

	private async Task PlayAsync()
	{
		if (!IsLauncherLocationValid || IsLoading || IsClientRunning)
		{
			return;
		}
		if (!_initTcs.Task.IsCompleted)
		{
			IsLoading = true;
			LoadingProgress = 0.0;
			LoadingTitle = "ПОДКЛЮЧЕНИЕ К CDN";
			LoadingDetail = "Сверка версии сборки…";
			try
			{
				await _initTcs.Task.ConfigureAwait(continueOnCapturedContext: true);
			}
			finally
			{
				IsLoading = false;
			}
		}
		if (await RunInstallAsync().ConfigureAwait(continueOnCapturedContext: true))
		{
			LaunchRustGame();
		}
	}

	private void NavigateTo(string? sectionName)
	{
		LauncherSection result;
		if (string.Equals(sectionName, "Home", StringComparison.OrdinalIgnoreCase))
		{
			NavigateTo(LauncherSection.Home);
		}
		else if (string.Equals(sectionName, "Graphics", StringComparison.OrdinalIgnoreCase))
		{
			NavigateTo(LauncherSection.Graphics);
		}
		else if (string.Equals(sectionName, "Tools", StringComparison.OrdinalIgnoreCase))
		{
			NavigateTo(LauncherSection.Tools);
		}
		else if (string.Equals(sectionName, "Settings", StringComparison.OrdinalIgnoreCase))
		{
			NavigateTo(LauncherSection.Settings);
		}
		else if (Enum.TryParse<LauncherSection>(sectionName, ignoreCase: true, out result))
		{
			NavigateTo(result);
		}
	}

	private void NavigateTo(LauncherSection section)
	{
		CurrentSection = section;
		switch (section)
		{
		case LauncherSection.Settings:
			Settings.Flush();
			break;
		case LauncherSection.Home:
			_localInstallState = InstallState.TryLoad(_settings.InstallPath);
			RefreshInstallUi();
			break;
		}
	}

	public void SelectGraphicsPreset(string? preset)
	{
		if (!string.IsNullOrWhiteSpace(preset))
		{
			SelectedGraphicsPreset = preset;
		}
	}

	public void ApplyGraphicsPreset(string presetId)
	{
		GraphicsPresetService.ApplyPreset(null, presetId, _settings, _gamePlacementService);
	}

	public static DateTime GetNextSaturdayWipeUtc()
	{
		DateTime dateTime = DateTime.UtcNow + MoscowUtcOffset;
		int num = (int)(6 - dateTime.DayOfWeek + 7) % 7;
		DateTime dateTime2 = dateTime.Date.AddDays(num).AddHours(15.0);
		if (num == 0 && dateTime >= dateTime2)
		{
			dateTime2 = dateTime2.AddDays(7.0);
		}
		return dateTime2 - MoscowUtcOffset;
	}

	public static string FormatWipeCountdown(TimeSpan remaining)
	{
		if (remaining <= TimeSpan.Zero)
		{
			return "СЕГОДНЯ";
		}
		if (remaining.TotalDays >= 1.0)
		{
			int value = (int)remaining.TotalDays;
			int hours = remaining.Hours;
			return $"{value} Д {hours} Ч";
		}
		if (remaining.TotalHours >= 1.0)
		{
			int value2 = (int)remaining.TotalHours;
			int minutes = remaining.Minutes;
			return $"{value2} Ч {minutes} М";
		}
		return $"{remaining.Minutes} М {remaining.Seconds} С";
	}

	private void UpdateWipeCountdown()
	{
		string wipeCountdown = FormatWipeCountdown(GetNextSaturdayWipeUtc() - DateTime.UtcNow);
		foreach (ServerInfo server in Servers)
		{
			if (server.HasWipe)
			{
				server.WipeCountdown = wipeCountdown;
			}
		}
	}

	private static void OpenTool(ToolLink? tool)
	{
		if (tool == null || string.IsNullOrWhiteSpace(tool.Url))
		{
			return;
		}
		try
		{
			if (Uri.TryCreate(tool.Url, UriKind.Absolute, out Uri result) && (result.Scheme == Uri.UriSchemeHttp || result.Scheme == Uri.UriSchemeHttps))
			{
				Process.Start(new ProcessStartInfo(result.AbsoluteUri)
				{
					UseShellExecute = true
				});
			}
		}
		catch (Exception)
		{
		}
	}

	private static IReadOnlyList<ToolLink> CreateTools()
	{
		bool isEnglish = LocalizationService.Instance.IsEnglish;
		return
		[
			new ToolLink
			{
				Title = (isEnglish ? "Official Blog" : "Официальный блог"),
				Description = (isEnglish ? "Information about new game updates and content." : "Информация о новом контенте в игре."),
				Url = "https://rust.facepunch.com/news",
				Source = "FACE PUNCH",
				Icon = ToolIcon("IconNews")
			},
			new ToolLink
			{
				Title = (isEnglish ? "Official Wiki" : "Официальная википедия"),
				Description = (isEnglish ? "Helpful articles for server admins and developers." : "Полезные статьи для админов и разработчиков."),
				Url = "https://wiki.facepunch.com/rust",
				Source = "FACE PUNCH WIKI",
				Icon = ToolIcon("IconBook")
			},
			new ToolLink
			{
				Title = (isEnglish ? "Raid Calculator" : "Калькулятор рейдов"),
				Description = (isEnglish ? "Simple way to calculate base raid costs." : "Простой способ рассчитать стоимость рейда базы."),
				Url = "https://rustexplore.com/ru/tools/raid-calculator",
				Source = "RUSTEXPLORE",
				Icon = ToolIcon("IconCalculator")
			},
			new ToolLink
			{
				Title = (isEnglish ? "Community Wiki" : "Фанатская википедия"),
				Description = (isEnglish ? "Information about items, skins and gameplay." : "Информация о предметах, скинах и прочем."),
				Url = "https://rusthelp.com",
				Source = "RUSTHELP",
				Icon = ToolIcon("IconGlobe")
			},
			new ToolLink
			{
				Title = (isEnglish ? "Breeding Calculator" : "Калькулятор скрещивания"),
				Description = (isEnglish ? "Tool for optimal plant gene crossbreeding." : "Инструмент для выведения генов растений."),
				Url = "https://rustbreeder.com",
				Source = "RUSTBREEDER",
				Icon = ToolIcon("IconLeaf")
			},
			new ToolLink
			{
				Title = (isEnglish ? "3D Monuments" : "3D монументы"),
				Description = (isEnglish ? "Loot locations and radiation zones on monuments." : "Лут и зона радиации на монументах."),
				Url = "https://rustmaps.com/monuments",
				Source = "RUSTMAPS",
				Icon = ToolIcon("IconMap")
			},
			new ToolLink
			{
				Title = (isEnglish ? "Circuit Builder" : "Конструктор электросхем"),
				Description = (isEnglish ? "Design and simulate electrical circuits in Rust." : "Создание электрических схем с возможностью проверки."),
				Url = "https://rustrician.io",
				Source = "RUSTRICIAN",
				Icon = ToolIcon("IconChip")
			}
		];
	}

	private static Geometry ToolIcon(string resourceKey)
	{
		return (Application.Current?.TryFindResource(resourceKey) as Geometry) ?? Geometry.Empty;
	}

	private static IReadOnlyList<ServerInfo> CreateServers()
	{
		return
		[
			new ServerInfo
			{
				Id = "main",
				Code = "01",
				Name = "MAIN",
				Description = "Классический Rust с размеренной прогрессией и живой экономикой. Соберите команду, удержите территорию и переживите этот вайп вместе с Summer.",
				Mode = "2X · SOLO / DUO / TRIO",
				Wipe = "СУББОТА 15:00",
				HasWipe = true,
				Host = "144.31.12.240",
				Port = 5555,
				QueryPort = 5557,
				Gs4uUrl = "https://www.gs4u.net/ru/s/432992",
				Map = "summer-rust.top",
				Online = 1,
				Capacity = 150,
				Ping = 24,
				Queue = 0,
				Uptime = 99.9,
				WipeCountdown = "2 ДНЯ",
				CoverImage = "pack://application:,,,/Assets/Servers/main.jpg",
				Tags = ["PVP", "X2", "WEEKLY"],
				OnlineHistory =
				[
					5.0, 8.0, 12.0, 19.0, 25.0, 34.0, 45.0, 58.0, 72.0, 85.0,
					96.0, 110.0, 118.0, 124.0, 131.0, 138.0, 142.0, 145.0, 148.0, 146.0,
					141.0, 130.0, 95.0, 1.0
				]
			},
			new ServerInfo
			{
				Id = "combat",
				Code = "02",
				Name = "COMBAT",
				Description = "Чистый PvP без долгой прокачки. Арена, постоянные стычки и агрессивный лут — для тех, кто выбирает бой вместо фермы.",
				Mode = "FFA · ARENA",
				Wipe = string.Empty,
				HasWipe = false,
				Host = "144.31.12.240",
				Port = 4444,
				QueryPort = 4446,
				Map = "summer-rust.top",
				Online = 0,
				Capacity = 100,
				Ping = 31,
				Queue = 0,
				Uptime = 99.7,
				WipeCountdown = string.Empty,
				CoverImage = "pack://application:,,,/Assets/Servers/combat.jpg",
				Tags = ["PVP", "FFA", "INSTANT"],
				OnlineHistory = new double[24]
			}
		];
	}

	public void Dispose()
	{
		if (!_disposed)
		{
			_disposed = true;
			_wipeTimer.Stop();
			_serverQueryTimer.Stop();
			try
			{
				_installCts?.Cancel();
				_installCts?.Dispose();
			}
			catch
			{
			}
			_lifetimeToken.Cancel();
			_gameProcess?.Dispose();
			Settings.Dispose();
			_serverQueryService.Dispose();
			_lifetimeToken.Dispose();
			_installService.Dispose();
		}
	}
}
