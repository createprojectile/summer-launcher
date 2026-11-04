using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Threading;
using Summer.Helpers;
using Summer.Models;
using Summer.Services;

namespace Summer.ViewModels;

public sealed class SettingsViewModel : ViewModelBase, IDisposable
{
	private readonly SettingsService _settingsService;

	private readonly LauncherSettings _settings;

	private readonly GamePlacementService _placement;

	private readonly UpdateService _updateService;

	private readonly CancellationTokenSource _lifetimeToken = new CancellationTokenSource();

	private readonly DispatcherTimer _saveTimer;

	private readonly AsyncRelayCommand _checkForUpdatesCommand;

	private readonly AsyncRelayCommand _loadConfigCommand;

	private bool _isFpsBoosterEnabled;

	private bool _isUiSoundEnabled = true;

	private double _uiSoundVolume = 70.0;

	private string _launchArguments = string.Empty;

	private bool _minimizeOnGameLaunch = true;

	private bool _isCheckingUpdates;

	private bool _isLoadingConfig;

	private bool _isResetDialogOpen;

	private string _saveStateText = LocalizationService.Instance["Settings_AutoSaveEnabled"];

	private bool _isSaveError;

	private bool _isDirty;

	private readonly HashSet<KeyBindInfo> _changedBindings = new HashSet<KeyBindInfo>();

	private string _updateStatusTitle = LocalizationService.Instance["Settings_UpdateNotChecked"];

	private string _updateStatusDescription = LocalizationService.Instance["Settings_UpdateNotCheckedDesc"];

	private bool _isUpdateAvailable;

	private string _configStatusText = "Стандартная конфигурация Summer";

	private bool _disposed;

	public string InstallPath => _placement.LauncherDirectory;

	public string GameDirectoryStatusText
	{
		get
		{
			LocalizationService instance = LocalizationService.Instance;
			if (!_placement.IsLauncherLocationValid)
			{
				return "Переместите лаунчер в папку SUMMER, SummerRust или SUMMER RUST и запустите его снова. Если игра уже установлена — поместите его рядом с RustClient.exe.";
			}
			string installPath = InstallPath;
			if (string.IsNullOrWhiteSpace(installPath))
			{
				return instance["Settings_DirEmpty"];
			}
			if (!Directory.Exists(installPath))
			{
				return instance["Settings_DirNotFound"];
			}
			if (_placement.IsGameInstalled(installPath, out string rustExePath))
			{
				return instance["Settings_ClientFound"] + Path.GetFileName(rustExePath);
			}
			return instance["Settings_ClientNotFound"];
		}
	}

	public string GameDirectoryStatusColor
	{
		get
		{
			if (!_placement.IsLauncherLocationValid)
			{
				return "#FF6B6B";
			}
			string installPath = InstallPath;
			if (string.IsNullOrWhiteSpace(installPath))
			{
				return "#8F8F8F";
			}
			if (!Directory.Exists(installPath))
			{
				return "#FF6B6B";
			}
			if (!_placement.IsGameInstalled(installPath, out string _))
			{
				return "#FAAD14";
			}
			return "#52C41A";
		}
	}

	public bool IsFpsBoosterEnabled
	{
		get
		{
			return _isFpsBoosterEnabled;
		}
		set
		{
			if (SetProperty(ref _isFpsBoosterEnabled, value, "IsFpsBoosterEnabled"))
			{
				_settings.FpsBoosterEnabled = value;
				OnPropertyChanged("FpsBoosterStatusText");
				OnPropertyChanged("FpsBoosterDescription");
				MarkDirty();
			}
		}
	}

	public string FpsBoosterStatusText
	{
		get
		{
			if (!IsFpsBoosterEnabled)
			{
				return LocalizationService.Instance["Settings_Disabled"];
			}
			return LocalizationService.Instance["Settings_Enabled"];
		}
	}

	public string FpsBoosterDescription
	{
		get
		{
			if (!IsFpsBoosterEnabled)
			{
				return LocalizationService.Instance["Settings_FpsBoosterDescInactive"];
			}
			return LocalizationService.Instance["Settings_FpsBoosterDescActive"];
		}
	}

	public bool IsUiSoundEnabled
	{
		get
		{
			return _isUiSoundEnabled;
		}
		set
		{
			if (SetProperty(ref _isUiSoundEnabled, value, "IsUiSoundEnabled"))
			{
				_settings.UiSoundsEnabled = value;
				UiSound.Configure(value, UiSoundVolume);
				MarkDirty();
			}
		}
	}

	public double UiSoundVolume
	{
		get
		{
			return _uiSoundVolume;
		}
		set
		{
			double num = Math.Clamp(Math.Round(value), 0.0, 100.0);
			if (SetProperty(ref _uiSoundVolume, num, "UiSoundVolume"))
			{
				_settings.UiSoundVolume = num;
				UiSound.Configure(IsUiSoundEnabled, num);
				MarkDirty();
			}
		}
	}

	public string LaunchArguments
	{
		get
		{
			return _launchArguments;
		}
		set
		{
			if (SetProperty(ref _launchArguments, value, "LaunchArguments"))
			{
				_settings.LaunchArguments = value;
				MarkDirty();
			}
		}
	}

	public bool MinimizeOnGameLaunch
	{
		get
		{
			return _minimizeOnGameLaunch;
		}
		set
		{
			if (SetProperty(ref _minimizeOnGameLaunch, value, "MinimizeOnGameLaunch"))
			{
				_settings.MinimizeOnGameLaunch = value;
				OnPropertyChanged("MinimizeOnLaunchStatusText");
				OnPropertyChanged("MinimizeOnLaunchDescription");
				MarkDirty();
			}
		}
	}

	public string MinimizeOnLaunchStatusText
	{
		get
		{
			if (!_minimizeOnGameLaunch)
			{
				return LocalizationService.Instance["Settings_KeepOpenOnLaunch"];
			}
			return LocalizationService.Instance["Settings_MinimizeOnLaunch"];
		}
	}

	public string MinimizeOnLaunchDescription
	{
		get
		{
			if (!_minimizeOnGameLaunch)
			{
				return LocalizationService.Instance["Settings_KeepOpenOnLaunchDesc"];
			}
			return LocalizationService.Instance["Settings_MinimizeOnLaunchDesc"];
		}
	}

	public ObservableCollection<KeyBindInfo> KeyBinds { get; }

	public ObservableCollection<KeyBindInfo> MacroBinds { get; }

	public RelayCommand ResetKeyBindsCommand { get; }

	public string VersionLabel => "RUST · CLIENT " + UpdateService.ClientVersion;

	public string CurrentVersion => UpdateService.ClientVersion;

	public string SaveStateText
	{
		get
		{
			return _saveStateText;
		}
		private set
		{
			SetProperty(ref _saveStateText, value, "SaveStateText");
		}
	}

	public bool IsSaveError
	{
		get
		{
			return _isSaveError;
		}
		private set
		{
			SetProperty(ref _isSaveError, value, "IsSaveError");
		}
	}

	public bool IsCheckingUpdates
	{
		get
		{
			return _isCheckingUpdates;
		}
		private set
		{
			if (SetProperty(ref _isCheckingUpdates, value, "IsCheckingUpdates"))
			{
				_checkForUpdatesCommand.RaiseCanExecuteChanged();
			}
		}
	}

	public string UpdateStatusTitle
	{
		get
		{
			return _updateStatusTitle;
		}
		private set
		{
			SetProperty(ref _updateStatusTitle, value, "UpdateStatusTitle");
		}
	}

	public string UpdateStatusDescription
	{
		get
		{
			return _updateStatusDescription;
		}
		private set
		{
			SetProperty(ref _updateStatusDescription, value, "UpdateStatusDescription");
		}
	}

	public bool IsUpdateAvailable
	{
		get
		{
			return _isUpdateAvailable;
		}
		private set
		{
			SetProperty(ref _isUpdateAvailable, value, "IsUpdateAvailable");
		}
	}

	public AsyncRelayCommand CheckForUpdatesCommand => _checkForUpdatesCommand;

	public bool IsLoadingConfig
	{
		get
		{
			return _isLoadingConfig;
		}
		private set
		{
			if (SetProperty(ref _isLoadingConfig, value, "IsLoadingConfig"))
			{
				_loadConfigCommand.RaiseCanExecuteChanged();
			}
		}
	}

	public string ConfigStatusText
	{
		get
		{
			return _configStatusText;
		}
		private set
		{
			SetProperty(ref _configStatusText, value, "ConfigStatusText");
		}
	}

	public AsyncRelayCommand LoadConfigCommand => _loadConfigCommand;

	public bool IsResetDialogOpen
	{
		get
		{
			return _isResetDialogOpen;
		}
		private set
		{
			SetProperty(ref _isResetDialogOpen, value, "IsResetDialogOpen");
		}
	}

	public RelayCommand ShowResetDialogCommand { get; }

	public RelayCommand CancelResetCommand { get; }

	public RelayCommand ConfirmResetCommand { get; }

	public string Language
	{
		get
		{
			return _settings.Language;
		}
		set
		{
			string text = (string.Equals(value, "en", StringComparison.OrdinalIgnoreCase) ? "en" : "ru");
			if (_settings.Language != text)
			{
				_settings.Language = text;
				LocalizationService.Instance.CurrentLanguage = text;
				OnPropertyChanged("Language");
				OnPropertyChanged("IsRussian");
				OnPropertyChanged("IsEnglish");
				MarkDirty();
			}
		}
	}

	public bool IsRussian => string.Equals(Language, "ru", StringComparison.OrdinalIgnoreCase);

	public bool IsEnglish => string.Equals(Language, "en", StringComparison.OrdinalIgnoreCase);

	public RelayCommand<string> SetLanguageCommand { get; }

	public SettingsViewModel(SettingsService settingsService, LauncherSettings settings, UpdateService updateService, GamePlacementService? placement = null)
	{
		_settingsService = settingsService;
		_settings = settings;
		LocalizationService.Instance.CurrentLanguage = settings.Language;
		LocalizationService.Instance.PropertyChanged += (object? _, PropertyChangedEventArgs _) =>
		{
			OnPropertyChanged("FpsBoosterStatusText");
			OnPropertyChanged("FpsBoosterDescription");
			OnPropertyChanged("MinimizeOnLaunchStatusText");
			OnPropertyChanged("MinimizeOnLaunchDescription");
			OnPropertyChanged("GameDirectoryStatusText");
			if (!_isDirty && !IsCheckingUpdates)
			{
				SaveStateText = LocalizationService.Instance[IsSaveError ? "Settings_SaveFailed" : "Settings_AllSaved"];
				UpdateStatusTitle = LocalizationService.Instance["Settings_UpdateNotChecked"];
				UpdateStatusDescription = LocalizationService.Instance["Settings_UpdateNotCheckedDesc"];
			}
			OnPropertyChanged("SaveStateText");
			OnPropertyChanged("UpdateStatusTitle");
			OnPropertyChanged("UpdateStatusDescription");
		};
		_placement = placement ?? new GamePlacementService();
		_settings.InstallPath = _placement.LauncherDirectory;
		_updateService = updateService;
		_isFpsBoosterEnabled = settings.FpsBoosterEnabled;
		_isUiSoundEnabled = settings.UiSoundsEnabled;
		_uiSoundVolume = Math.Clamp(settings.UiSoundVolume, 0.0, 100.0);
		_launchArguments = settings.LaunchArguments;
		_minimizeOnGameLaunch = settings.MinimizeOnGameLaunch;
		SetLanguageCommand = new RelayCommand<string>(SetLanguage);
		KeyBinds = new ObservableCollection<KeyBindInfo>(settings.KeyBinds);
		foreach (KeyBindInfo keyBind in KeyBinds)
		{
			keyBind.PropertyChanged += OnKeyBindPropertyChanged;
		}
		MacroBinds = new ObservableCollection<KeyBindInfo>(settings.MacroBinds);
		foreach (KeyBindInfo macroBind in MacroBinds)
		{
			macroBind.PropertyChanged += OnKeyBindPropertyChanged;
		}
		CancelResetCommand = new RelayCommand(() =>
		{
			IsResetDialogOpen = false;
		});
		ConfirmResetCommand = new RelayCommand(ResetGameSettings);
		ShowResetDialogCommand = new RelayCommand(() =>
		{
			IsResetDialogOpen = true;
		});
		ResetKeyBindsCommand = new RelayCommand(ResetKeyBindsToDefaults);
		_checkForUpdatesCommand = new AsyncRelayCommand(CheckForUpdatesAsync, () => !IsCheckingUpdates);
		_loadConfigCommand = new AsyncRelayCommand(LoadGameConfigAsync, () => !IsLoadingConfig);
		_saveTimer = new DispatcherTimer(DispatcherPriority.Background)
		{
			Interval = TimeSpan.FromMilliseconds(550.0)
		};
		_saveTimer.Tick += OnSaveTimerTick;
	}

	private void SetLanguage(string? lang)
	{
		if (!string.IsNullOrWhiteSpace(lang))
		{
			Language = lang;
		}
	}

	private void MarkDirty()
	{
		_isDirty = true;
		IsSaveError = false;
		SaveStateText = LocalizationService.Instance["Settings_SavingChanges"];
		_saveTimer.Stop();
		_saveTimer.Start();
	}

	private void OnSaveTimerTick(object? sender, EventArgs e)
	{
		_saveTimer.Stop();
		SaveNow();
	}

	public void SaveNow()
	{
		_saveTimer.Stop();
		_settings.InstallPath = InstallPath?.Trim().Trim('"') ?? string.Empty;
		_settings.FpsBoosterEnabled = IsFpsBoosterEnabled;
		_settings.UiSoundsEnabled = IsUiSoundEnabled;
		_settings.UiSoundVolume = UiSoundVolume;
		_settings.MinimizeOnGameLaunch = MinimizeOnGameLaunch;
		_settings.Language = Language;
		_settings.LaunchArguments = LaunchArguments.Trim();
		_settings.KeyBinds = KeyBinds.ToList();
		_settings.MacroBinds = MacroBinds.ToList();
		bool flag = _settingsService.Save(_settings);
		if (flag && _changedBindings.Count > 0 && GamePlacementService.FindRustClient(_settings.InstallPath) != null)
		{
			flag = KeyConfigService.SyncKeyBindings(_settings.InstallPath, new LauncherSettings
			{
				InstallPath = _settings.InstallPath,
				KeyBinds = KeyBinds.Where(_changedBindings.Contains).ToList(),
				MacroBinds = MacroBinds.Where(_changedBindings.Contains).ToList()
			});
			if (flag)
			{
				_changedBindings.Clear();
			}
		}
		_isDirty = !flag;
		IsSaveError = !flag;
		SaveStateText = (flag ? LocalizationService.Instance["Settings_AllSaved"] : LocalizationService.Instance["Settings_SaveFailed"]);
	}

	public void Flush()
	{
		SaveNow();
	}

	private async Task CheckForUpdatesAsync()
	{
		IsCheckingUpdates = true;
		IsUpdateAvailable = false;
		UpdateStatusTitle = LocalizationService.Instance["Settings_UpdateChecking"];
		UpdateStatusDescription = LocalizationService.Instance["Settings_UpdateCheckingDesc"];
		try
		{
			UpdateCheckResult updateCheckResult = await _updateService.CheckForGameUpdatesAsync(_lifetimeToken.Token);
			IsUpdateAvailable = updateCheckResult.UpdateAvailable;
			UpdateStatusTitle = updateCheckResult.Title;
			UpdateStatusDescription = updateCheckResult.Description;
		}
		catch (OperationCanceledException)
		{
		}
		catch (Exception)
		{
			UpdateStatusTitle = LocalizationService.Instance["Settings_UpdateCheckFailed"];
			UpdateStatusDescription = LocalizationService.Instance["Settings_UpdateCheckFailedDesc"];
		}
		finally
		{
			IsCheckingUpdates = false;
		}
	}

	private async Task LoadGameConfigAsync()
	{
		IsLoadingConfig = true;
		ConfigStatusText = "Загружаем summer-competitive.json…";
		try
		{
			await Task.Delay(1300, _lifetimeToken.Token);
			IsFpsBoosterEnabled = true;
			LaunchArguments = "-high -maxmem=8192 -window-mode exclusive";
			ApplyCompetitiveBinds();
			SaveNow();
			ConfigStatusText = "Конфиг «Summer Competitive» применён.";
		}
		catch (OperationCanceledException)
		{
		}
		catch (Exception)
		{
			ConfigStatusText = "Не удалось применить конфиг.";
		}
		finally
		{
			IsLoadingConfig = false;
		}
	}

	private void ApplyCompetitiveBinds()
	{
		foreach (KeyBindInfo keyBind in KeyBinds)
		{
			KeyBindInfo keyBindInfo = keyBind;
			keyBindInfo.Key = keyBind.ActionName switch
			{
				"Присесть" => "C", 
				"Бег" => "Shift", 
				"Атака" => "ЛКМ", 
				_ => keyBind.Key, 
			};
		}
	}

	private void ResetGameSettings()
	{
		IsFpsBoosterEnabled = false;
		MinimizeOnGameLaunch = true;
		LaunchArguments = string.Empty;
		ResetKeyBindsToDefaults();
		ConfigStatusText = "Настройки игры восстановлены по умолчанию.";
		IsResetDialogOpen = false;
		SaveNow();
	}

	private void ResetKeyBindsToDefaults()
	{
		List<KeyBindInfo> source = KeyBindInfo.CreateDefaults();
		foreach (KeyBindInfo bind in KeyBinds)
		{
			KeyBindInfo keyBindInfo = source.FirstOrDefault((KeyBindInfo x) => x.ActionName == bind.ActionName);
			if (keyBindInfo != null)
			{
				bind.Key = keyBindInfo.Key;
			}
		}
		List<KeyBindInfo> source2 = KeyBindInfo.CreateMacroBinds();
		foreach (KeyBindInfo macro in MacroBinds)
		{
			KeyBindInfo keyBindInfo2 = source2.FirstOrDefault((KeyBindInfo x) => x.ActionName == macro.ActionName);
			if (keyBindInfo2 != null)
			{
				macro.Key = keyBindInfo2.Key;
			}
		}
		_settings.KeyBinds = KeyBinds.ToList();
		_settings.MacroBinds = MacroBinds.ToList();
		SaveNow();
	}

	private void OnKeyBindPropertyChanged(object? sender, PropertyChangedEventArgs e)
	{
		if (e.PropertyName == "Key")
		{
			if (sender is KeyBindInfo item)
			{
				_changedBindings.Add(item);
			}
			_settings.KeyBinds = KeyBinds.ToList();
			_settings.MacroBinds = MacroBinds.ToList();
			MarkDirty();
		}
	}

	public void Dispose()
	{
		if (_disposed)
		{
			return;
		}
		_disposed = true;
		_lifetimeToken.Cancel();
		_saveTimer.Stop();
		_saveTimer.Tick -= OnSaveTimerTick;
		if (_isDirty)
		{
			SaveNow();
		}
		foreach (KeyBindInfo keyBind in KeyBinds)
		{
			keyBind.PropertyChanged -= OnKeyBindPropertyChanged;
		}
		foreach (KeyBindInfo macroBind in MacroBinds)
		{
			macroBind.PropertyChanged -= OnKeyBindPropertyChanged;
		}
		_lifetimeToken.Dispose();
	}
}
