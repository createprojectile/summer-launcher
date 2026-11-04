using System;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace Summer.Services;

public sealed class GamePlacementService
{
	public const string LocationWarning = "Переместите лаунчер в папку SUMMER, SummerRust или SUMMER RUST и запустите его снова. Если игра уже установлена — поместите его рядом с RustClient.exe.";

	public string LauncherDirectory { get; }

	public bool IsLauncherLocationValid
	{
		get
		{
			if (Directory.Exists(LauncherDirectory))
			{
				return IsAllowedLauncherDirectory(LauncherDirectory);
			}
			return false;
		}
	}

	public bool IsInGameDirectory
	{
		get
		{
			string rustExePath;
			return IsLauncherInGameFolder(out rustExePath);
		}
	}

	public GamePlacementService(string? launcherDirectory = null)
	{
		LauncherDirectory = Path.TrimEndingDirectorySeparator(Path.GetFullPath(launcherDirectory ?? AppContext.BaseDirectory));
	}

	public static bool IsAllowedLauncherDirectory(string? directory)
	{
		if (string.IsNullOrWhiteSpace(directory))
		{
			return false;
		}
		try
		{
			string fileName = Path.GetFileName(Path.TrimEndingDirectorySeparator(Path.GetFullPath(directory)));
			return new string[3] { "SUMMER", "SummerRust", "SUMMER RUST" }.Contains(fileName, StringComparer.OrdinalIgnoreCase);
		}
		catch
		{
			return false;
		}
	}

	public bool IsLauncherInGameFolder(out string? rustExePath)
	{
		return IsGameInstalled(LauncherDirectory, out rustExePath);
	}

	public bool IsGameInstalled(string? configuredInstallPath, out string? rustExePath)
	{
		rustExePath = (IsLauncherLocationValid ? FindRustClient(LauncherDirectory) : null);
		return rustExePath != null;
	}

	public GameLocationStatus CheckLocation(string? configuredInstallPath, out string? rustExePath)
	{
		if (IsGameInstalled(configuredInstallPath, out rustExePath))
		{
			return GameLocationStatus.LauncherInGameFolder;
		}
		rustExePath = null;
		return GameLocationStatus.GameNotFound;
	}

	public bool CanLaunchGame(string? configuredInstallPath, out string? rustExePath, out string statusMessage)
	{
		rustExePath = null;
		string launcherDirectory = LauncherDirectory;
		if (!IsLauncherLocationValid)
		{
			statusMessage = "Переместите лаунчер в папку SUMMER, SummerRust или SUMMER RUST и запустите его снова. Если игра уже установлена — поместите его рядом с RustClient.exe.";
			return false;
		}
		if (!IsGameInstalled(launcherDirectory, out rustExePath))
		{
			rustExePath = null;
			statusMessage = "RustClient.exe не найден рядом с лаунчером:\n" + launcherDirectory + "\n\nСкачайте клиент через лаунчер или поместите лаунчер рядом с установленной игрой.";
			return false;
		}
		statusMessage = "Игра обнаружена в директории игры (" + launcherDirectory + ").";
		return true;
	}

	public Process? LaunchGame(string rustExePath, string arguments, string? configuredGameDir, out string? error)
	{
		error = null;
		string launcherDirectory = LauncherDirectory;
		if (!IsLauncherLocationValid)
		{
			error = "Переместите лаунчер в папку SUMMER, SummerRust или SUMMER RUST и запустите его снова. Если игра уже установлена — поместите его рядом с RustClient.exe.";
			return null;
		}
		if (string.IsNullOrWhiteSpace(configuredGameDir) || !string.Equals(Path.TrimEndingDirectorySeparator(Path.GetFullPath(configuredGameDir.Trim().Trim('"'))), LauncherDirectory, StringComparison.OrdinalIgnoreCase))
		{
			error = "Запуск разрешён только из папки лаунчера.";
			return null;
		}
		string text = rustExePath?.Trim().Trim('"');
		if (string.IsNullOrWhiteSpace(text) || !File.Exists(text))
		{
			error = "Исполняемый файл игры не найден:\n" + text;
			return null;
		}
		if (!IsPathWithinDirectory(text, launcherDirectory))
		{
			error = $"Запуск отклонен: исполняемый файл {text} находится вне директории игры, указанной в настройках ({launcherDirectory}).";
			return null;
		}
		string fileName = Path.GetFileName(text);
		if (!string.Equals(fileName, "RustClient.exe", StringComparison.OrdinalIgnoreCase))
		{
			error = "Запуск отклонен: исполняемый файл " + fileName + " не является клиентом Rust. Ожидается RustClient.exe.";
			return null;
		}
		if (!IsGameInstalled(launcherDirectory, out string rustExePath2) || !string.Equals(Path.GetFullPath(text), rustExePath2, StringComparison.OrdinalIgnoreCase))
		{
			error = "Запуск отклонен: исполняемый файл не совпадает с клиентом в выбранной папке SUMMER.";
			return null;
		}
		string directoryName = Path.GetDirectoryName(text);
		if (string.IsNullOrEmpty(directoryName) || !Directory.Exists(directoryName))
		{
			error = "Папка игры не найдена: " + directoryName;
			return null;
		}
		ProcessStartInfo startInfo = new ProcessStartInfo
		{
			FileName = text,
			Arguments = (arguments ?? string.Empty),
			WorkingDirectory = directoryName,
			UseShellExecute = true
		};
		try
		{
			Process process = Process.Start(startInfo);
			if (process == null)
			{
				error = "Не удалось создать процесс RustClient.exe";
				return null;
			}
			return process;
		}
		catch (Exception ex)
		{
			error = "Ошибка запуска " + Path.GetFileName(text) + ": " + ex.Message;
			return null;
		}
	}

	[Obsolete("Запуск без проверки директории игры из настроек лаунчера запрещён.")]
	public Process? LaunchGame(string rustExePath, string arguments, out string? error)
	{
		error = "Запуск игры без проверки директории игры из настроек лаунчера запрещён. Укажите папку с игрой в настройках.";
		return null;
	}

	public static bool IsPathWithinDirectory(string childPath, string parentDirectory)
	{
		if (string.IsNullOrWhiteSpace(childPath) || string.IsNullOrWhiteSpace(parentDirectory))
		{
			return false;
		}
		try
		{
			string fullPath = Path.GetFullPath(childPath.Trim().Trim('"'));
			string relativePath = Path.GetRelativePath(Path.GetFullPath(parentDirectory.Trim().Trim('"')), fullPath);
			return !relativePath.Equals(".", StringComparison.Ordinal) && !relativePath.Equals("..", StringComparison.Ordinal) && !relativePath.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal) && !relativePath.StartsWith(".." + Path.AltDirectorySeparatorChar, StringComparison.Ordinal) && !Path.IsPathRooted(relativePath);
		}
		catch
		{
			return false;
		}
	}

	public static string? FindRustClient(string directory)
	{
		string text = directory?.Trim().Trim('"');
		if (string.IsNullOrWhiteSpace(text) || !Directory.Exists(text))
		{
			return null;
		}
		try
		{
			string text2 = Path.Combine(Path.GetFullPath(text), "RustClient.exe");
			if (File.Exists(text2) && (File.GetAttributes(text2) & FileAttributes.ReparsePoint) == 0)
			{
				return text2;
			}
		}
		catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
		{
			return null;
		}
		return null;
	}
}
