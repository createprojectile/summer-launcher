using System;
using System.Threading;
using System.Threading.Tasks;
using Summer.Helpers;
using Summer.Services;

namespace Summer.ViewModels;

public sealed class SplashViewModel : ViewModelBase, IDisposable
{
	private readonly UpdateService _updateService;

	private readonly CancellationTokenSource _cancellationTokenSource = new CancellationTokenSource();

	private double _progress;

	private string _stage = "ИНИЦИАЛИЗАЦИЯ";

	private string _detail = "Подготовка лаунчера…";

	private int _stageIndex;

	private string? _errorMessage;

	private bool _isCompleted;

	private bool _disposed;

	public double Progress
	{
		get
		{
			return _progress;
		}
		private set
		{
			if (SetProperty(ref _progress, value, "Progress"))
			{
				OnPropertyChanged("PercentLabel");
			}
		}
	}

	public string PercentLabel => $"{Math.Round(Progress):0}%";

	public string Stage
	{
		get
		{
			return _stage;
		}
		private set
		{
			SetProperty(ref _stage, value, "Stage");
		}
	}

	public string Detail
	{
		get
		{
			return _detail;
		}
		private set
		{
			SetProperty(ref _detail, value, "Detail");
		}
	}

	public int StageIndex
	{
		get
		{
			return _stageIndex;
		}
		private set
		{
			if (SetProperty(ref _stageIndex, value, "StageIndex"))
			{
				OnPropertiesChanged("IsUpdateStageActive", "IsFilesStageActive", "IsLaunchStageActive");
			}
		}
	}

	public bool IsUpdateStageActive => StageIndex == 0;

	public bool IsFilesStageActive => StageIndex == 1;

	public bool IsLaunchStageActive => StageIndex == 2;

	public string? ErrorMessage
	{
		get
		{
			return _errorMessage;
		}
		private set
		{
			if (SetProperty(ref _errorMessage, value, "ErrorMessage"))
			{
				OnPropertyChanged("HasError");
			}
		}
	}

	public bool HasError => !string.IsNullOrWhiteSpace(ErrorMessage);

	public string VersionLabel => "CLIENT " + UpdateService.ClientVersion + "  ·  PC";

	public event Action? StartupCompleted;

	public SplashViewModel(UpdateService updateService)
	{
		_updateService = updateService;
	}

	public async Task RunAsync()
	{
		try
		{
			Progress<LaunchProgress> progress = new Progress<LaunchProgress>(UpdateProgress);
			await _updateService.RunStartupAsync(progress, _cancellationTokenSource.Token);
			if (!_cancellationTokenSource.IsCancellationRequested)
			{
				_isCompleted = true;
				StartupCompleted?.Invoke();
			}
		}
		catch (OperationCanceledException)
		{
		}
		catch (Exception ex2)
		{
			ErrorMessage = "Не удалось завершить проверку. Закройте лаунчер и попробуйте снова.";
			Detail = ex2.Message;
		}
	}

	private void UpdateProgress(LaunchProgress progress)
	{
		Progress = progress.Percent;
		Stage = progress.Stage;
		Detail = progress.Detail;
		StageIndex = progress.StageIndex;
	}

	public void Cancel()
	{
		if (_disposed || _isCompleted)
		{
			return;
		}
		try
		{
			_cancellationTokenSource.Cancel();
		}
		catch (ObjectDisposedException)
		{
		}
	}

	public void Dispose()
	{
		if (!_disposed)
		{
			_disposed = true;
			Cancel();
			_cancellationTokenSource.Dispose();
		}
	}
}
