using System;
using System.Buffers;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;

namespace Summer.Services;

public sealed class CdnDownloadService : IDisposable
{
	public const string DefaultCdnBaseUrl = "https://client-summer-rust.b-cdn.net/SUMMER/";

	private readonly HttpClient _http = CreateClient();

	private static HttpClient CreateClient()
	{
		return new HttpClient(new SocketsHttpHandler
		{
			AutomaticDecompression = DecompressionMethods.None,
			PooledConnectionLifetime = TimeSpan.FromMinutes(10.0),
			ConnectTimeout = TimeSpan.FromSeconds(15.0)
		})
		{
			Timeout = TimeSpan.FromMinutes(15.0),
			DefaultRequestHeaders = 
			{
				{ "User-Agent", "SummerLauncher/1.4.7" },
				{ "Accept", "*/*" }
			}
		};
	}

	public async Task DownloadFileAsync(string url, string destination, long expectedSize, string expectedSha256, IProgress<long>? bytesDone, CancellationToken cancellationToken = default(CancellationToken), IProgress<long>? verificationProgress = null)
	{
		if (expectedSize < 0)
		{
			throw new ArgumentOutOfRangeException("expectedSize");
		}
		string part = destination + ".part";
		Directory.CreateDirectory(Path.GetDirectoryName(destination));
		int attempt = 1;
		while (true)
		{
			try
			{
				await DownloadAttemptAsync(url, part, expectedSize, expectedSha256, destination, bytesDone, cancellationToken, verificationProgress).ConfigureAwait(continueOnCapturedContext: false);
				break;
			}
			catch (OperationCanceledException)
			{
				throw;
			}
			catch (Exception ex2) when (attempt < 3 && IsTransient(ex2))
			{
				if (ex2 is InvalidDataException && File.Exists(part) && expectedSize > 0 && new FileInfo(part).Length >= expectedSize)
				{
					File.Delete(part);
				}
				await Task.Delay(TimeSpan.FromSeconds(1 << attempt - 1), cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
			}
			attempt++;
		}
	}

	private async Task DownloadAttemptAsync(string url, string partPath, long expectedSize, string expectedSha256, string destination, IProgress<long>? bytesDone, CancellationToken cancellationToken, IProgress<long>? verificationProgress)
	{
		long existing = (File.Exists(partPath) ? new FileInfo(partPath).Length : 0);
		if (expectedSize > 0 && existing > expectedSize)
		{
			File.Delete(partPath);
			existing = 0L;
		}
		bytesDone?.Report(existing);
		if (existing == expectedSize && expectedSize > 0)
		{
			await VerifyAndCommitAsync(partPath, destination, expectedSize, expectedSha256, verificationProgress, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
			return;
		}
		using HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, url);
		if (existing > 0)
		{
			request.Headers.Range = new RangeHeaderValue(existing, null);
		}
		using HttpResponseMessage response = await _http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
		if (response.StatusCode == HttpStatusCode.NotModified || response.StatusCode >= HttpStatusCode.BadRequest)
		{
			throw new HttpRequestException($"CDN ответил {(int)response.StatusCode} для {url}");
		}
		long start = existing;
		if (response.StatusCode == HttpStatusCode.OK)
		{
			// Сервер проигнорировал Range и отдал файл целиком — начинаем сначала.
			start = 0L;
		}
		else if (response.StatusCode == HttpStatusCode.PartialContent)
		{
			ContentRangeHeaderValue? contentRange = response.Content.Headers.ContentRange;
			if (contentRange == null || contentRange.From != existing)
			{
				throw new InvalidDataException("CDN вернул некорректный диапазон докачки");
			}
		}
		else
		{
			throw new InvalidDataException("CDN вернул некорректный диапазон докачки");
		}
		bytesDone?.Report(start);
		await using (Stream remote = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(continueOnCapturedContext: false))
		{
			await using FileStream local = new FileStream(partPath, FileMode.OpenOrCreate, FileAccess.Write, FileShare.None, 65536, useAsync: true);
			local.SetLength(start);
			local.Seek(start, SeekOrigin.Begin);
			long copied = start;
			long reported = -1L;
			byte[] copyBuffer = new byte[262144];
			while (true)
			{
				int num;
				int read = (num = await remote.ReadAsync(copyBuffer, cancellationToken).ConfigureAwait(continueOnCapturedContext: false));
				if (num <= 0)
				{
					break;
				}
				await local.WriteAsync(copyBuffer.AsMemory(0, read), cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
				copied += read;
				if (bytesDone != null && reported != copied)
				{
					reported = copied;
					bytesDone.Report(copied);
				}
			}
		}
		await VerifyAndCommitAsync(partPath, destination, expectedSize, expectedSha256, verificationProgress, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
	}

	private static async Task VerifyAndCommitAsync(string partPath, string destination, long expectedSize, string expectedSha256, IProgress<long>? verificationProgress, CancellationToken cancellationToken)
	{
		verificationProgress?.Report(0L);
		FileInfo fileInfo = new FileInfo(partPath);
		if (expectedSize > 0 && fileInfo.Length != expectedSize)
		{
			throw new InvalidDataException($"Размер {Path.GetFileName(destination)} не совпадает: {fileInfo.Length} вместо {expectedSize}");
		}
		if (!string.IsNullOrWhiteSpace(expectedSha256) && !string.Equals(await ComputeSha256Async(partPath, cancellationToken, verificationProgress).ConfigureAwait(continueOnCapturedContext: false), expectedSha256, StringComparison.OrdinalIgnoreCase))
		{
			throw new InvalidDataException("Контрольная сумма " + Path.GetFileName(destination) + " не совпала");
		}
		cancellationToken.ThrowIfCancellationRequested();
		File.Move(partPath, destination, overwrite: true);
	}

	public static async Task<string> ComputeSha256Async(string filePath, CancellationToken cancellationToken = default(CancellationToken), IProgress<long>? bytesRead = null)
	{
		string result;
		await using (FileStream stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, 1, FileOptions.Asynchronous | FileOptions.SequentialScan))
		{
			using IncrementalHash hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
			byte[] buffer = ArrayPool<byte>.Shared.Rent(1048576);
			try
			{
				long total = 0L;
				int num;
				while ((num = await stream.ReadAsync(buffer.AsMemory(), cancellationToken).ConfigureAwait(continueOnCapturedContext: false)) > 0)
				{
					cancellationToken.ThrowIfCancellationRequested();
					hash.AppendData(buffer, 0, num);
					total += num;
					bytesRead?.Report(total);
				}
				result = Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant();
			}
			finally
			{
				ArrayPool<byte>.Shared.Return(buffer);
			}
		}
		return result;
	}

	private static bool IsTransient(Exception exception)
	{
		if (exception is HttpRequestException || exception is IOException || exception is UnauthorizedAccessException || exception is InvalidDataException)
		{
			return true;
		}
		return false;
	}

	public void Dispose()
	{
		_http.Dispose();
	}
}
