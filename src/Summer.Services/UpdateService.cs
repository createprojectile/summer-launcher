using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Reflection;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using Summer.Models;

namespace Summer.Services;

public sealed class UpdateService : IDisposable
{
	public const string DefaultLauncherUpdateUrl = "https://summer-launcher.b-cdn.net/launcher/launcher-version.json";

	private readonly LauncherSettings? _settings;

	private readonly string? _customUpdateUrl;

	private readonly HttpClient _httpClient;

	private readonly bool _ownsHttpClient;

	private bool _disposed;

	public static string ClientVersion
	{
		get
		{
			try
			{
				string processPath = Environment.ProcessPath;
				if (!string.IsNullOrEmpty(processPath) && File.Exists(processPath))
				{
					string fileName = Path.GetFileName(processPath);
					string directoryName = Path.GetDirectoryName(processPath);
					if (!string.IsNullOrEmpty(directoryName))
					{
						string path = Path.Combine(directoryName, ".launcher_version");
						if (File.Exists(path))
						{
							string text = File.ReadAllText(path).Trim();
							if (Version.TryParse(text, out Version _))
							{
								return text;
							}
						}
					}
					if (fileName.StartsWith("Summer", StringComparison.OrdinalIgnoreCase))
					{
						string productVersion = FileVersionInfo.GetVersionInfo(processPath).ProductVersion;
						if (!string.IsNullOrWhiteSpace(productVersion))
						{
							int num = productVersion.IndexOf('+');
							string text2 = ((num > 0) ? productVersion.Substring(0, num).Trim() : productVersion.Trim());
							if (Version.TryParse(text2, out Version result2) && result2 > new Version(1, 0, 0))
							{
								return text2;
							}
						}
					}
				}
				Assembly assembly = typeof(UpdateService).Assembly;
				string text3 = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
				if (!string.IsNullOrWhiteSpace(text3))
				{
					int num2 = text3.IndexOf('+');
					string text4 = ((num2 > 0) ? text3.Substring(0, num2).Trim() : text3.Trim());
					if (Version.TryParse(text4, out Version result3) && result3 > new Version(1, 0, 0))
					{
						return text4;
					}
				}
				Version version = assembly.GetName().Version;
				if (version != null && version.Major > 0 && version > new Version(1, 0, 0))
				{
					return $"{version.Major}.{version.Minor}.{Math.Max(0, version.Build)}";
				}
			}
			catch
			{
			}
			return "1.0.0";
		}
	}

	public UpdateService(LauncherSettings? settings = null, string? updateUrl = null, HttpClient? httpClient = null)
	{
		_settings = settings;
		_customUpdateUrl = updateUrl;
		if (httpClient != null)
		{
			_httpClient = httpClient;
			_ownsHttpClient = false;
			return;
		}
		_httpClient = new HttpClient
		{
			Timeout = TimeSpan.FromSeconds(25.0),
			DefaultRequestHeaders = 
			{
				{
					"User-Agent",
					"SummerLauncher/" + ClientVersion
				},
				{ "Accept", "application/json, application/octet-stream, */*" }
			}
		};
		_ownsHttpClient = true;
	}

	public static void CleanupOldBinary()
	{
		try
		{
			string processPath = Environment.ProcessPath;
			if (string.IsNullOrEmpty(processPath))
			{
				return;
			}
			string directoryName = Path.GetDirectoryName(processPath);
			if (string.IsNullOrEmpty(directoryName) || !Directory.Exists(directoryName))
			{
				return;
			}
			foreach (string item in Directory.EnumerateFiles(directoryName, "*.old"))
			{
				try
				{
					File.Delete(item);
				}
				catch
				{
				}
			}
		}
		catch
		{
		}
	}

	public string GetManifestUrl()
	{
		if (!string.IsNullOrWhiteSpace(_customUpdateUrl))
		{
			return _customUpdateUrl.Trim();
		}
		if (!string.IsNullOrWhiteSpace(_settings?.LauncherUpdateUrl))
		{
			return _settings.LauncherUpdateUrl.Trim();
		}
		return "https://summer-launcher.b-cdn.net/launcher/launcher-version.json";
	}

	public async Task<LauncherUpdateManifest?> CheckForLauncherUpdateAsync(CancellationToken cancellationToken = default(CancellationToken))
	{
		try
		{
			string manifestUrl = GetManifestUrl();
			if (string.IsNullOrWhiteSpace(manifestUrl))
			{
				return null;
			}
			string value = (manifestUrl.Contains('?') ? "&" : "?");
			string requestUri = $"{manifestUrl}{value}t={DateTimeOffset.UtcNow.ToUnixTimeSeconds()}";
			using HttpResponseMessage response = await _httpClient.GetAsync(requestUri, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
			if (!response.IsSuccessStatusCode)
			{
				return null;
			}
			LauncherUpdateManifest launcherUpdateManifest = LauncherUpdateManifest.Parse(await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(continueOnCapturedContext: false));
			if (launcherUpdateManifest == null || string.IsNullOrWhiteSpace(launcherUpdateManifest.Version))
			{
				return null;
			}
			if (IsRemoteVersionNewer(launcherUpdateManifest.Version, ClientVersion))
			{
				return launcherUpdateManifest;
			}
		}
		catch
		{
		}
		return null;
	}

	public async Task DownloadAndApplyUpdateAsync(LauncherUpdateManifest update, IProgress<LaunchProgress>? progress, CancellationToken cancellationToken = default(CancellationToken))
	{
		if (update == null || string.IsNullOrWhiteSpace(update.Url))
		{
			throw new ArgumentException("Манифест обновления не содержит корректного URL для скачивания.", "update");
		}
		string currentExe = Environment.ProcessPath;
		if (string.IsNullOrWhiteSpace(currentExe) || !File.Exists(currentExe))
		{
			throw new InvalidOperationException("Не удалось определить исполняемый файл лаунчера для обновления.");
		}
		if (string.Equals(Path.GetFileName(currentExe), "dotnet.exe", StringComparison.OrdinalIgnoreCase))
		{
			return;
		}
		string currentDir = Path.GetDirectoryName(currentExe);
		string tempNewExe = currentExe + ".new";
		string oldExe = currentExe + ".old";
		if (File.Exists(tempNewExe))
		{
			try
			{
				File.Delete(tempNewExe);
			}
			catch
			{
			}
		}
		progress?.Report(new LaunchProgress(10.0, "ОБНОВЛЕНИЕ ЛАУНЧЕРА", "Подключение к Bunny CDN…", 0));
		using (HttpResponseMessage response = await _httpClient.GetAsync(update.Url, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(continueOnCapturedContext: false))
		{
			response.EnsureSuccessStatusCode();
			long totalBytes = response.Content.Headers.ContentLength ?? ((update.Size > 0) ? update.Size : (-1));
			await using Stream contentStream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
			await using FileStream fileStream = new FileStream(tempNewExe, FileMode.Create, FileAccess.Write, FileShare.None, 81920, useAsync: true);
			byte[] buffer = new byte[81920];
			long totalRead = 0L;
			while (true)
			{
				int num;
				int bytesRead = (num = await contentStream.ReadAsync(buffer.AsMemory(0, buffer.Length), cancellationToken).ConfigureAwait(continueOnCapturedContext: false));
				if (num <= 0)
				{
					break;
				}
				await fileStream.WriteAsync(buffer.AsMemory(0, bytesRead), cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
				totalRead += bytesRead;
				if (totalBytes > 0)
				{
					int num2 = 10 + (int)((double)totalRead / (double)totalBytes * 75.0);
					double value = (double)totalRead / 1048576.0;
					double value2 = (double)totalBytes / 1048576.0;
					progress?.Report(new LaunchProgress(num2, "ОБНОВЛЕНИЕ ЛАУНЧЕРА", $"Загрузка: {value:F1} / {value2:F1} МБ…", 0));
				}
				else
				{
					double value3 = (double)totalRead / 1048576.0;
					progress?.Report(new LaunchProgress(35.0, "ОБНОВЛЕНИЕ ЛАУНЧЕРА", $"Загрузка: {value3:F1} МБ…", 0));
				}
			}
		}
		if (!string.IsNullOrWhiteSpace(update.Sha256))
		{
			progress?.Report(new LaunchProgress(90.0, "ОБНОВЛЕНИЕ ЛАУНЧЕРА", "Проверка контрольной суммы файла…", 0));
			using SHA256 sha = SHA256.Create();
			await using FileStream fileStream = File.OpenRead(tempNewExe);
			string text = Convert.ToHexString(await sha.ComputeHashAsync(fileStream, cancellationToken).ConfigureAwait(continueOnCapturedContext: false));
			if (!string.Equals(text, update.Sha256, StringComparison.OrdinalIgnoreCase))
			{
				try
				{
					File.Delete(tempNewExe);
				}
				catch
				{
				}
				throw new InvalidDataException($"Контрольная сумма скачанного файла ({text}) не совпадает с CDN ({update.Sha256}).");
			}
		}
		progress?.Report(new LaunchProgress(98.0, "ОБНОВЛЕНИЕ ЛАУНЧЕРА", "Применение обновления и перезапуск…", 0));
		await Task.Delay(150, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
		if (File.Exists(oldExe))
		{
			try
			{
				File.Delete(oldExe);
			}
			catch
			{
				string destFileName = Path.Combine(currentDir, $"Summer.{Guid.NewGuid():N}.old");
				try
				{
					File.Move(oldExe, destFileName);
				}
				catch
				{
				}
			}
		}
		File.Move(currentExe, oldExe);
		File.Move(tempNewExe, currentExe);
		try
		{
			File.WriteAllText(Path.Combine(currentDir, ".launcher_version"), update.Version.Trim());
		}
		catch
		{
		}
		Process.Start(new ProcessStartInfo
		{
			FileName = currentExe,
			WorkingDirectory = currentDir,
			UseShellExecute = true
		});
		Environment.Exit(0);
	}

	public async Task RunStartupAsync(IProgress<LaunchProgress>? progress, CancellationToken cancellationToken = default(CancellationToken))
	{
		progress?.Report(new LaunchProgress(0.0, "ИНИЦИАЛИЗАЦИЯ", "Подключение к сети Summer…", 0));
		await Task.Delay(50, cancellationToken);
		progress?.Report(new LaunchProgress(15.0, "ПРОВЕРКА ОБНОВЛЕНИЙ", "Сверка версии сборки с CDN…", 0));
		LauncherUpdateManifest update = await CheckForLauncherUpdateAsync(cancellationToken);
		if (update != null)
		{
			try
			{
				await DownloadAndApplyUpdateAsync(update, progress, cancellationToken);
				return;
			}
			catch (Exception ex) when (!update.Mandatory && !cancellationToken.IsCancellationRequested)
			{
				progress?.Report(new LaunchProgress(25.0, "ПРОВЕРКА ОБНОВЛЕНИЙ", "Пропуск необязательного обновления: " + ex.Message, 0));
				await Task.Delay(400, cancellationToken);
			}
		}
		await AnimateStageAsync(progress, 25, 60, "ПРОВЕРКА ОБНОВЛЕНИЙ", 0, ["Подключение к CDN…", "Версия актуальна"], 8, cancellationToken);
		await Task.Delay(30, cancellationToken);
		await AnimateStageAsync(progress, 60, 100, "ГОТОВО", 2, ["Загрузка профиля…", "Готово к работе"], 8, cancellationToken);
		await Task.Delay(50, cancellationToken);
	}

	public async Task<UpdateCheckResult> CheckForGameUpdatesAsync(CancellationToken cancellationToken = default(CancellationToken))
	{
		try
		{
			LauncherUpdateManifest launcherUpdateManifest = await CheckForLauncherUpdateAsync(cancellationToken);
			if (launcherUpdateManifest != null)
			{
				return new UpdateCheckResult(UpdateAvailable: true, "Найдено обновление лаунчера", "Доступна новая версия " + launcherUpdateManifest.Version + ". Перезапустите лаунчер для её автоматической установки.");
			}
			return new UpdateCheckResult(UpdateAvailable: false, "Обновлений не найдено", "Установлена актуальная версия лаунчера — " + ClientVersion + ".");
		}
		catch
		{
			return new UpdateCheckResult(UpdateAvailable: false, "Проверка не удалась", "Не удалось связаться с сервером обновлений Bunny CDN.");
		}
	}

	public static bool IsRemoteVersionNewer(string remoteVersion, string localVersion)
	{
		if (string.IsNullOrWhiteSpace(remoteVersion))
		{
			return false;
		}
		if (string.Equals(remoteVersion.Trim(), localVersion.Trim(), StringComparison.OrdinalIgnoreCase))
		{
			return false;
		}
		if (Version.TryParse(remoteVersion.Trim(), out Version result) && Version.TryParse(localVersion.Trim(), out Version result2))
		{
			return result > result2;
		}
		return !string.Equals(remoteVersion.Trim(), localVersion.Trim(), StringComparison.OrdinalIgnoreCase);
	}

	private static async Task AnimateStageAsync(IProgress<LaunchProgress>? progress, int from, int to, string stage, int stageIndex, IReadOnlyList<string> details, int stepDelayMs, CancellationToken cancellationToken)
	{
		int totalSteps = Math.Max(1, to - from);
		for (int value = from; value <= to; value++)
		{
			double num = (double)(value - from) / (double)totalSteps;
			int index = Math.Min(details.Count - 1, (int)(num * (double)details.Count));
			progress?.Report(new LaunchProgress(value, stage, details[index], stageIndex));
			await Task.Delay(stepDelayMs, cancellationToken);
		}
	}

	public void Dispose()
	{
		if (!_disposed)
		{
			_disposed = true;
			if (_ownsHttpClient)
			{
				_httpClient.Dispose();
			}
		}
	}
}
