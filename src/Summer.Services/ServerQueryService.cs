using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Summer.Models;

namespace Summer.Services;

public sealed class ServerQueryService : IDisposable
{
	private static readonly byte[] InfoQuery = new byte[25]
	{
		255, 255, 255, 255, 84, 83, 111, 117, 114, 99,
		101, 32, 69, 110, 103, 105, 110, 101, 32, 81,
		117, 101, 114, 121, 0
	};

	private static readonly Regex Gs4uPlayersRegex = new Regex("itemprop=\"playersOnline\">\\s*(\\d+)\\s*</b>\\s*&nbsp;.*?&nbsp;\\s*<b>\\s*(\\d+)\\s*</b>", RegexOptions.Compiled | RegexOptions.Singleline);

	private static readonly Regex Gs4uMapRegex = new Regex("<meta\\s+name=\"twitter:description\"\\s+content=\"[^\"]*?карта\\s+([^,]+),", RegexOptions.IgnoreCase | RegexOptions.Compiled);

	private readonly HttpClient _httpClient;

	private bool _disposed;

	public ServerQueryService()
	{
		_httpClient = new HttpClient
		{
			Timeout = TimeSpan.FromSeconds(5.0)
		};
		_httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) SummerLauncher/1.0");
	}

	public async Task<ServerQueryResult> QueryServerAsync(ServerInfo server, CancellationToken cancellationToken = default(CancellationToken))
	{
		if (string.IsNullOrWhiteSpace(server.Host) || server.QueryPort <= 0)
		{
			return new ServerQueryResult(IsOnline: true, server.Name, server.Map, server.Online, server.Capacity, server.Ping);
		}
		ServerQueryResult serverQueryResult = await QueryA2SAsync(server.Host, server.QueryPort, TimeSpan.FromSeconds(2.5), cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
		if (serverQueryResult != null && serverQueryResult.IsOnline)
		{
			return serverQueryResult;
		}
		if (!string.IsNullOrWhiteSpace(server.Gs4uUrl))
		{
			ServerQueryResult serverQueryResult2 = await QueryGs4uFallbackAsync(server.Gs4uUrl, TimeSpan.FromSeconds(3.0), cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
			if (serverQueryResult2 != null && serverQueryResult2.IsOnline)
			{
				return serverQueryResult2;
			}
		}
		return new ServerQueryResult(IsOnline: false, server.Name, server.Map, 0, server.Capacity, 999);
	}

	public async Task<ServerQueryResult?> QueryA2SAsync(string host, int queryPort, TimeSpan timeout, CancellationToken cancellationToken = default(CancellationToken))
	{
		Stopwatch sw = Stopwatch.StartNew();
		using CancellationTokenSource timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
		timeoutCts.CancelAfter(timeout);
		try
		{
			using UdpClient client = new UdpClient();
			client.Connect(host, queryPort);
			await client.SendAsync(InfoQuery.AsMemory(), timeoutCts.Token).ConfigureAwait(continueOnCapturedContext: false);
			byte[] buffer = (await client.ReceiveAsync(timeoutCts.Token).ConfigureAwait(continueOnCapturedContext: false)).Buffer;
			if (buffer.Length < 5)
			{
				return null;
			}
			if (buffer[4] == 65 && buffer.Length >= 9)
			{
				byte[] array = new byte[InfoQuery.Length + 4];
				Buffer.BlockCopy(InfoQuery, 0, array, 0, InfoQuery.Length);
				Buffer.BlockCopy(buffer, 5, array, InfoQuery.Length, 4);
				sw.Restart();
				await client.SendAsync(array.AsMemory(), timeoutCts.Token).ConfigureAwait(continueOnCapturedContext: false);
				buffer = (await client.ReceiveAsync(timeoutCts.Token).ConfigureAwait(continueOnCapturedContext: false)).Buffer;
			}
			sw.Stop();
			int pingMs = (int)Math.Max(1L, sw.ElapsedMilliseconds);
			if (buffer.Length >= 6 && buffer[4] == 73)
			{
				using (MemoryStream input = new MemoryStream(buffer, 5, buffer.Length - 5))
				{
					using BinaryReader binaryReader = new BinaryReader(input, Encoding.UTF8);
					binaryReader.ReadByte();
					string serverName = ReadNullTerminatedString(binaryReader);
					string text = ReadNullTerminatedString(binaryReader);
					ReadNullTerminatedString(binaryReader);
					ReadNullTerminatedString(binaryReader);
					binaryReader.ReadInt16();
					byte online = binaryReader.ReadByte();
					byte maxPlayers = binaryReader.ReadByte();
					return new ServerQueryResult(IsOnline: true, serverName, string.IsNullOrWhiteSpace(text) ? "summer-rust.top" : text, online, maxPlayers, pingMs);
				}
			}
			return null;
		}
		catch (OperationCanceledException)
		{
			return null;
		}
		catch
		{
			return null;
		}
	}

	public async Task<ServerQueryResult?> QueryGs4uFallbackAsync(string gs4uUrl, TimeSpan timeout, CancellationToken cancellationToken = default(CancellationToken))
	{
		try
		{
			using CancellationTokenSource timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
			timeoutCts.CancelAfter(timeout);
			using HttpResponseMessage response = await _httpClient.GetAsync(gs4uUrl, HttpCompletionOption.ResponseContentRead, timeoutCts.Token).ConfigureAwait(continueOnCapturedContext: false);
			if (!response.IsSuccessStatusCode)
			{
				return null;
			}
			string input = await response.Content.ReadAsStringAsync(timeoutCts.Token).ConfigureAwait(continueOnCapturedContext: false);
			Match match = Gs4uPlayersRegex.Match(input);
			if (!match.Success)
			{
				return null;
			}
			int online = (int.TryParse(match.Groups[1].Value, out var result) ? result : 0);
			int maxPlayers = (int.TryParse(match.Groups[2].Value, out var result2) ? result2 : 150);
			string map = "summer-rust.top";
			Match match2 = Gs4uMapRegex.Match(input);
			if (match2.Success)
			{
				map = match2.Groups[1].Value.Trim();
			}
			return new ServerQueryResult(IsOnline: true, "SUMMER NOLIMIT | X2", map, online, maxPlayers, 35);
		}
		catch
		{
			return null;
		}
	}

	private static string ReadNullTerminatedString(BinaryReader reader)
	{
		List<byte> list = new List<byte>();
		try
		{
			byte item;
			while ((item = reader.ReadByte()) != 0)
			{
				list.Add(item);
			}
		}
		catch (EndOfStreamException)
		{
		}
		return Encoding.UTF8.GetString(list.ToArray());
	}

	public void Dispose()
	{
		if (!_disposed)
		{
			_disposed = true;
			_httpClient.Dispose();
		}
	}
}
