using System;
using System.Threading.Tasks;
using System.Windows.Input;

namespace Summer.Helpers;

public sealed class AsyncRelayCommand : ICommand
{
	private readonly Func<Task> _execute;

	private readonly Func<bool>? _canExecute;

	private bool _isExecuting;

	public bool IsExecuting => _isExecuting;

	public event EventHandler? CanExecuteChanged;

	public AsyncRelayCommand(Func<Task> execute, Func<bool>? canExecute = null)
	{
		_execute = execute ?? throw new ArgumentNullException("execute");
		_canExecute = canExecute;
	}

	public bool CanExecute(object? parameter)
	{
		if (!_isExecuting)
		{
			return _canExecute?.Invoke() ?? true;
		}
		return false;
	}

	public async void Execute(object? parameter)
	{
		if (!CanExecute(parameter))
		{
			return;
		}
		_isExecuting = true;
		RaiseCanExecuteChanged();
		try
		{
			await _execute();
		}
		catch (OperationCanceledException)
		{
		}
		catch (Exception)
		{
		}
		finally
		{
			_isExecuting = false;
			RaiseCanExecuteChanged();
		}
	}

	public void RaiseCanExecuteChanged()
	{
		CanExecuteChanged?.Invoke(this, EventArgs.Empty);
	}
}
