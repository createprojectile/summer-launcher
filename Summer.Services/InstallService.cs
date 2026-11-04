using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Summer.Models;

namespace Summer.Services;

public sealed class InstallService : IDisposable
{
	private sealed class CallbackProgress<T> : IProgress<T>
	{
		private readonly Action<T> _callback;

		public CallbackProgress(Action<T> callback)
		{
			_callback = callback;
		}

		public void Report(T value)
		{
			_callback(value);
		}
	}

	public const string ManifestFileName = "manifest.json";

	private static readonly HttpClient Http = CreateHttpClient();

	private readonly CdnDownloadService _downloads = new CdnDownloadService();

	private readonly string _cdnBase;

	private readonly GamePlacementService _placement;

	public InstallService(string? cdnBase = null, string? launcherDirectory = null)
	{
		_cdnBase = cdnBase ?? "https://client-summer-rust.b-cdn.net/SUMMER/";
		_placement = new GamePlacementService(launcherDirectory);
	}

	public async Task<GameManifest> LoadManifestAsync(CancellationToken cancellationToken = default(CancellationToken))
	{
		string cdnBase = _cdnBase;
		string manifestUrl = (cdnBase.EndsWith("manifest.json", StringComparison.OrdinalIgnoreCase) ? cdnBase : (cdnBase.TrimEnd('/') + "/manifest.json"));
		GameManifest gameManifest = GameManifest.Parse(await FetchTextWithRetriesAsync(new UriBuilder(manifestUrl)
		{
			Query = $"request={Guid.NewGuid():N}"
		}.Uri.AbsoluteUri, cancellationToken).ConfigureAwait(continueOnCapturedContext: false));
		if (string.IsNullOrWhiteSpace(gameManifest.Url))
		{
			gameManifest.Url = new Uri(new Uri(manifestUrl), ".").AbsoluteUri;
		}
		return gameManifest;
	}

	private static async Task<string> FetchTextWithRetriesAsync(string url, CancellationToken cancellationToken)
	{
		int attempt = 1;
		while (true)
		{
			try
			{
				using HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, url);
				using HttpResponseMessage response = await Http.SendAsync(request, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
				response.EnsureSuccessStatusCode();
				return await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
			}
			catch (OperationCanceledException)
			{
				throw;
			}
			catch (Exception) when (attempt < 3)
			{
				await Task.Delay(TimeSpan.FromSeconds(attempt), cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
			}
			attempt++;
		}
	}

	public async Task<InstallResult> EnsureInstalledAsync(LauncherSettings settings, IProgress<InstallProgress>? progress, CancellationToken cancellationToken = default(CancellationToken), bool forceFullVerification = false)
	{
		if (!_placement.IsLauncherLocationValid)
		{
			return new InstallResult(Success: false, string.Empty, "Переместите лаунчер в папку SUMMER, SummerRust или SUMMER RUST и запустите его снова. Если игра уже установлена — поместите его рядом с RustClient.exe.");
		}
		settings.InstallPath = _placement.LauncherDirectory;
		progress?.Report(new InstallProgress(0.0, "CHECKING", "Получаем актуальный манифест CDN…"));
		GameManifest manifest;
		try
		{
			manifest = await LoadManifestAsync(cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
		}
		catch (Exception ex) when (ex is not OperationCanceledException)
		{
			ErrorLog.Write("Manifest", ex);
			return new InstallResult(Success: false, string.Empty, "Не удалось получить манифест CDN: " + ex.Message);
		}
		string installPath = _placement.LauncherDirectory;
		try
		{
			Directory.CreateDirectory(installPath);
		}
		catch (Exception ex2)
		{
			ErrorLog.Write("Install folder", ex2);
			return new InstallResult(Success: false, manifest.Version, "Не удалось создать папку установки: " + ex2.Message);
		}
		progress?.Report(new InstallProgress(0.0, "SCANNING", "Сборка " + manifest.Version + ": сверяем файлы…"));
		HashSet<string> userConfigs = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "cfg/client.cfg", "cfg/keys.cfg", "cfg/key.cfg", "key.cfg", "keys.cfg", "Summer.exe", "SummerLauncher.exe", ".doorstop_version", "dotnet/.version" };
		string processPath = Environment.ProcessPath;
		if (processPath != null)
		{
			userConfigs.Add(Path.GetFileName(processPath));
		}
		manifest.Files.RemoveAll((CdnFileEntry cdnFileEntry) => userConfigs.Contains(cdnFileEntry.Path.Replace('\\', '/')));
		InstallState state = InstallState.TryLoad(installPath);
		List<CdnFileEntry> toDownload = new List<CdnFileEntry>();
		Dictionary<string, long> verifiedWriteTimes = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
		int checkedFiles = 0;
		InstallState checkpoint = state ?? InstallState.CreateNew(installPath, string.Empty);
		object checkpointLock = new object();
		int pendingCheckpointFiles = 0;
		Stopwatch verificationWatch = Stopwatch.StartNew();
		TimeSpan lastCheckpoint = TimeSpan.Zero;
		TimeSpan lastVerificationReport = TimeSpan.MinValue;
		int cachedFiles = 0;
		int hashedFiles = 0;
		int optionalFiles = 0;
		long hashedBytes = 0L;
		long scannedBytes = 0L;
		long manifestBytes = manifest.Files.Sum((CdnFileEntry cdnFileEntry) => Math.Max(0L, cdnFileEntry.Size));
		Dictionary<string, int> reasons = new Dictionary<string, int>();
		ErrorLog.Write("File verification", $"Начало: mode={(forceFullVerification ? "full" : "quick")}; folder={installPath}; manifest={manifest.Version}; cachedVersion={state?.Version ?? "none"}; files={manifest.Files.Count}");
		try
		{
			foreach (CdnFileEntry entry in manifest.Files)
			{
				cancellationToken.ThrowIfCancellationRequested();
				ReportVerification(entry, 0L);
				bool plannedForDownload = false;
				try
				{
					string? localPath = ToAbsolutePath(installPath, entry.Path);
					if (!forceFullVerification && IsPresenceOnlyFile(entry.Path) && localPath != null && File.Exists(localPath))
					{
						optionalFiles++;
						continue;
					}

					bool needsDownload = localPath == null
						|| !File.Exists(localPath)
						|| (entry.Size > 0 && new FileInfo(localPath).Length != entry.Size);

					if (!needsDownload)
					{
						long writeTime = File.GetLastWriteTimeUtc(localPath!).Ticks;
						if (!forceFullVerification
							&& state != null
							&& state.Files.TryGetValue(entry.Path, out string? cachedHash)
							&& string.Equals(cachedHash, entry.Sha256, StringComparison.OrdinalIgnoreCase)
							&& state.VerifiedWriteTimesUtc.TryGetValue(entry.Path, out long cachedWriteTime)
							&& cachedWriteTime == writeTime)
						{
							verifiedWriteTimes[entry.Path] = writeTime;
							cachedFiles++;
							continue;
						}
						if (string.IsNullOrWhiteSpace(entry.Sha256))
						{
							RecordVerified(entry, writeTime);
							continue;
						}

						string reason;
						if (forceFullVerification)
						{
							reason = "full_requested";
						}
						else if (state == null)
						{
							reason = "no_cache";
						}
						else if (state.Files.TryGetValue(entry.Path, out string? manifestHash))
						{
							reason = !string.Equals(manifestHash, entry.Sha256, StringComparison.OrdinalIgnoreCase)
								? "manifest_hash_changed"
								: "timestamp_changed_or_missing";
						}
						else
						{
							reason = "not_verified";
						}

						reasons[reason] = reasons.GetValueOrDefault(reason) + 1;
						if (entry.Size >= 67108864)
						{
							ErrorLog.Write("File verification", $"Чтение: {entry.Path}; bytes={entry.Size}; reason={reason}");
						}

						hashedFiles++;
						string actualHash = await CdnDownloadService.ComputeSha256Async(localPath!, cancellationToken, new CallbackProgress<long>((long read) =>
						{
							ReportVerification(entry, read);
						})).ConfigureAwait(continueOnCapturedContext: false);
						hashedBytes += new FileInfo(localPath!).Length;

						if (string.Equals(actualHash, entry.Sha256, StringComparison.OrdinalIgnoreCase))
						{
							if (File.GetLastWriteTimeUtc(localPath!).Ticks != writeTime)
							{
								throw new IOException("Файл изменился во время проверки: " + entry.Path);
							}

							RecordVerified(entry, writeTime);
							continue;
						}
					}

					toDownload.Add(entry);
					plannedForDownload = true;
					lock (checkpointLock)
					{
						if (checkpoint.Files.Remove(entry.Path) | checkpoint.VerifiedWriteTimesUtc.Remove(entry.Path))
						{
							pendingCheckpointFiles++;
						}
					}
				}
				finally
				{
					checkedFiles++;
					scannedBytes += Math.Max(0L, entry.Size);
					if (plannedForDownload)
					{
						reasons["missing_size_or_hash_mismatch"] = reasons.GetValueOrDefault("missing_size_or_hash_mismatch") + 1;
					}
				}
			}
			SaveCheckpoint(force: true);
			if (manifest.Files.Count > 0)
			{
				List<CdnFileEntry> files = manifest.Files;
				ReportVerification(files[files.Count - 1], 0L, force: true);
			}
			cancellationToken.ThrowIfCancellationRequested();
			ErrorLog.Write("File verification", $"Проверка завершена: seconds={verificationWatch.Elapsed.TotalSeconds:0.00}; cached={cachedFiles}; optional={optionalFiles}; hashed={hashedFiles}; hashedBytes={hashedBytes}; download={toDownload.Count}; reasons={string.Join(",", reasons.Select((KeyValuePair<string, int> pair) => $"{pair.Key}:{pair.Value}"))}");
			long totalBytes = toDownload.Sum((CdnFileEntry f) => f.Size);
			if (totalBytes > 0 && !HasFreeSpace(installPath, totalBytes))
			{
				DriveInfo driveInfo = new DriveInfo(Path.GetFullPath(installPath));
				long bytes = (long)((double)totalBytes * 1.1);
				return new InstallResult(Success: false, manifest.Version, "Мало места на диске: нужно ~" + FormatSize(bytes) + ", свободно " + FormatSize(driveInfo.AvailableFreeSpace));
			}
			object transferLock;
			long downloaded;
			long verified;
			long resumedBytes;
			Stopwatch transferWatch;
			TimeSpan lastReport;
			string lastStage;
			if (toDownload.Count > 0)
			{
				ConcurrentQueue<CdnFileEntry> queue = new ConcurrentQueue<CdnFileEntry>(toDownload);
				transferLock = new object();
				Dictionary<string, long> downloadedByFile = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
				Dictionary<string, long> verifiedByFile = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
				downloaded = 0L;
				verified = 0L;
				foreach (CdnFileEntry item in toDownload)
				{
					string text2 = (ToAbsolutePath(installPath, item.Path) ?? throw new InvalidOperationException("Небезопасный путь в манифесте: " + item.Path)) + ".part";
					long num2 = (File.Exists(text2) ? Math.Min(new FileInfo(text2).Length, item.Size) : 0);
					downloadedByFile[item.Path] = num2;
					downloaded += num2;
				}
				resumedBytes = downloaded;
				transferWatch = Stopwatch.StartNew();
				lastReport = TimeSpan.MinValue;
				lastStage = string.Empty;
				Exception firstError = null;
				ReportTransfer(null, force: true);
				IEnumerable<Task> tasks = from _ in Enumerable.Range(0, Math.Clamp(Environment.ProcessorCount, 2, 4))
					select Task.Run(async () =>
					{
						while (true)
						{
							if (Volatile.Read(in firstError) != null || !queue.TryDequeue(out CdnFileEntry entry2))
							{
								break;
							}
							cancellationToken.ThrowIfCancellationRequested();
							try
							{
								string absolute = ToAbsolutePath(installPath, entry2.Path) ?? throw new InvalidOperationException("Небезопасный путь в манифесте: " + entry2.Path);
								string text4 = string.Join("/", entry2.Path.Replace('\\', '/').Split('/').Select(Uri.EscapeDataString));
								string url = ((!string.IsNullOrWhiteSpace(entry2.Url)) ? entry2.Url : (manifest.Url.TrimEnd('/') + "/" + text4 + "?sha256=" + Uri.EscapeDataString(entry2.Sha256)));
								CallbackProgress<long> bytesDone = new CallbackProgress<long>((long num3) =>
								{
									lock (transferLock)
									{
										downloaded += num3 - downloadedByFile[entry2.Path];
										downloadedByFile[entry2.Path] = num3;
										ReportTransfer(entry2);
									}
								});
								CallbackProgress<long> verificationProgress = new CallbackProgress<long>((long num3) =>
								{
									lock (transferLock)
									{
										verified += num3 - verifiedByFile.GetValueOrDefault(entry2.Path);
										verifiedByFile[entry2.Path] = num3;
										ReportTransfer(entry2);
									}
								});
								await _downloads.DownloadFileAsync(url, absolute, entry2.Size, entry2.Sha256, bytesDone, cancellationToken, verificationProgress).ConfigureAwait(continueOnCapturedContext: false);
								RecordVerified(entry2, File.GetLastWriteTimeUtc(absolute).Ticks);
								lock (transferLock)
								{
									verified += entry2.Size - verifiedByFile.GetValueOrDefault(entry2.Path);
									verifiedByFile[entry2.Path] = entry2.Size;
									ReportTransfer(entry2, force: true, Path.GetFileName(entry2.Path) + " готов");
								}
							}
							catch (Exception ex4) when (ex4 is not OperationCanceledException)
							{
								Interlocked.CompareExchange(ref firstError, new IOException("Ошибка загрузки " + entry2.Path + ": " + ex4.Message, ex4), null);
							}
						}
					}, cancellationToken);
				try
				{
					await Task.WhenAll(tasks).ConfigureAwait(continueOnCapturedContext: false);
					cancellationToken.ThrowIfCancellationRequested();
				}
				catch (OperationCanceledException)
				{
					return new InstallResult(Success: false, manifest.Version, "Установка отменена");
				}
				if (firstError != null)
				{
					ErrorLog.Write("Download", firstError);
					return new InstallResult(Success: false, manifest.Version, firstError.Message);
				}
				ReportTransfer(null, force: true, "Скачанные файлы проверены, сохраняем результат…");
			}
			cancellationToken.ThrowIfCancellationRequested();
			string text3 = GamePlacementService.FindRustClient(installPath);
			if (text3 == null)
			{
				return new InstallResult(Success: false, manifest.Version, "Клиент RustClient.exe не найден в папке Summer.");
			}
			InstallState installState = InstallState.CreateNew(installPath, manifest.Version);
			installState.ClientExePath = Path.GetRelativePath(installPath, text3).Replace('\\', '/');
			foreach (CdnFileEntry file in manifest.Files)
			{
				installState.Files[file.Path] = file.Sha256;
				if (verifiedWriteTimes.TryGetValue(file.Path, out var value4))
				{
					installState.VerifiedWriteTimesUtc[file.Path] = value4;
				}
				else
				{
					installState.Files.Remove(file.Path);
				}
			}
			installState.Save();
			pendingCheckpointFiles = 0;
			string detail = ((totalBytes > 0) ? $"Клиент {manifest.Version} установлен ({FormatSize(totalBytes)})" : ("Клиент " + manifest.Version + " уже актуален"));
			progress?.Report(new InstallProgress(100.0, "READY", detail));
			return new InstallResult(Success: true, manifest.Version, null);
			void ReportTransfer(CdnFileEntry? cdnFileEntry = null, bool force = false, string? text5 = null)
			{
				lock (transferLock)
				{
					string text4 = ((downloaded >= totalBytes) ? "VERIFYING" : "DOWNLOADING");
					TimeSpan elapsed = transferWatch.Elapsed;
					if (force || !(text4 == lastStage) || !(elapsed - lastReport < TimeSpan.FromMilliseconds(120.0)))
					{
						lastReport = elapsed;
						lastStage = text4;
						long num3 = ((text4 == "VERIFYING") ? verified : downloaded);
						double percent = ((totalBytes > 0) ? Math.Clamp(100.0 * (double)num3 / (double)totalBytes, 0.0, 100.0) : 100.0);
						string speed = ((text4 == "DOWNLOADING" && elapsed.TotalSeconds > 1.0) ? $"{(double)Math.Max(0L, downloaded - resumedBytes) / elapsed.TotalSeconds / 1048576.0:0.0} МБ/с" : "");
						progress?.Report(new InstallProgress(percent, text4, text5 ?? cdnFileEntry?.Path ?? "Подготовка загрузки…", speed, FormatSize(num3) + " / " + FormatSize(totalBytes)));
					}
				}
			}
			void ReportVerification(CdnFileEntry cdnFileEntry, long fileBytes, bool force = false)
			{
				TimeSpan elapsed = verificationWatch.Elapsed;
				if (force || !(lastVerificationReport != TimeSpan.MinValue) || !(elapsed - lastVerificationReport < TimeSpan.FromMilliseconds(150.0)))
				{
					lastVerificationReport = elapsed;
					long num3 = scannedBytes + Math.Min(fileBytes, Math.Max(0L, cdnFileEntry.Size));
					double value5;
					if (manifestBytes > 0)
					{
						value5 = (double)num3 / (double)manifestBytes;
					}
					else
					{
						value5 = ((manifest.Files.Count > 0) ? ((double)checkedFiles / (double)manifest.Files.Count) : 1.0);
					}
					string sizeText = FormatSize(num3) + " / " + FormatSize(manifestBytes);
					progress?.Report(new InstallProgress(100.0 * Math.Clamp(value5, 0.0, 1.0), "SCANNING", $"{cdnFileEntry.Path} • файлов: {checkedFiles}/{manifest.Files.Count} • пропущено: {cachedFiles + optionalFiles}", "", sizeText));
				}
			}
		}
		finally
		{
			try
			{
				SaveCheckpoint(force: true);
			}
			catch (Exception error)
			{
				ErrorLog.Write("Verification checkpoint", error);
			}
			ErrorLog.Write("File verification", $"Окончание: cancelled={cancellationToken.IsCancellationRequested}; seconds={verificationWatch.Elapsed.TotalSeconds:0.00}; checked={checkedFiles}; cached={cachedFiles}; hashed={hashedFiles}; hashedBytes={hashedBytes}; download={toDownload.Count}");
		}
		void RecordVerified(CdnFileEntry cdnFileEntry, long value5)
		{
			lock (checkpointLock)
			{
				verifiedWriteTimes[cdnFileEntry.Path] = value5;
				checkpoint.Files[cdnFileEntry.Path] = cdnFileEntry.Sha256;
				checkpoint.VerifiedWriteTimesUtc[cdnFileEntry.Path] = value5;
				pendingCheckpointFiles++;
				SaveCheckpoint();
			}
		}
		void SaveCheckpoint(bool force = false)
		{
			lock (checkpointLock)
			{
				if (pendingCheckpointFiles != 0 && (force || pendingCheckpointFiles >= 10 || !(verificationWatch.Elapsed - lastCheckpoint < TimeSpan.FromSeconds(5.0))))
				{
					checkpoint.Save();
					pendingCheckpointFiles = 0;
					lastCheckpoint = verificationWatch.Elapsed;
				}
			}
		}
	}

	private static bool IsPresenceOnlyFile(string path)
	{
		string text = path.Replace('\\', '/');
		if (!text.StartsWith("temp/", StringComparison.OrdinalIgnoreCase) && !text.StartsWith("RustClient_Data/StreamingAssets/WorkshopSource/", StringComparison.OrdinalIgnoreCase) && !text.StartsWith("RustClient_Data/StreamingAssets/MenuVideo/", StringComparison.OrdinalIgnoreCase) && !text.StartsWith("RustClient_Data/StreamingAssets/keymidi/", StringComparison.OrdinalIgnoreCase))
		{
			return text.StartsWith("RustClient_Data/StreamingAssets/Instruments/", StringComparison.OrdinalIgnoreCase);
		}
		return true;
	}

	internal static string? ToAbsolutePath(string installPath, string relativePath)
	{
		try
		{
			string fullPath = Path.GetFullPath(installPath);
			string fullPath2 = Path.GetFullPath(Path.Combine(fullPath, relativePath.Replace('/', Path.DirectorySeparatorChar)));
			return GamePlacementService.IsPathWithinDirectory(fullPath2, fullPath) ? fullPath2 : null;
		}
		catch (Exception)
		{
			return null;
		}
	}

	private static bool HasFreeSpace(string installPath, long neededBytes)
	{
		try
		{
			return new DriveInfo(Path.GetFullPath(installPath)).AvailableFreeSpace >= (long)((double)neededBytes * 1.1);
		}
		catch (Exception)
		{
			return true;
		}
	}

	private static string FormatSize(long bytes)
	{
		if (bytes < 1073741824)
		{
			return $"{(double)bytes / 1048576.0:0.0} МБ";
		}
		return $"{(double)bytes / 1073741824.0:0.00} ГБ";
	}

	private static HttpClient CreateHttpClient()
	{
		return new HttpClient
		{
			Timeout = TimeSpan.FromSeconds(30.0),
			DefaultRequestHeaders = { { "User-Agent", "SummerLauncher/1.4.7" } }
		};
	}

	public void Dispose()
	{
		_downloads.Dispose();
	}
}
