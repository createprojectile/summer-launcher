using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using Summer.Services;

namespace Summer.Models;

public sealed class InstallState
{
	private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
	{
		PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
		WriteIndented = true
	};

	[JsonPropertyName("version")]
	public string Version { get; set; } = string.Empty;

	[JsonPropertyName("files")]
	public Dictionary<string, string> Files { get; set; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

	[JsonPropertyName("verifiedWriteTimesUtc")]
	public Dictionary<string, long> VerifiedWriteTimesUtc { get; set; } = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);

	[JsonPropertyName("clientExePath")]
	public string ClientExePath { get; set; } = string.Empty;

	[JsonIgnore]
	public string InstallPath { get; private set; } = string.Empty;

	public static string GetStateDirectory(string installPath)
	{
		return Path.Combine(installPath, ".summer-launcher");
	}

	public static string GetStatePath(string installPath)
	{
		return Path.Combine(GetStateDirectory(installPath), "state.json");
	}

	public static InstallState? TryLoad(string installPath)
	{
		if (string.IsNullOrWhiteSpace(installPath))
		{
			return null;
		}
		try
		{
			string statePath = GetStatePath(installPath);
			if (!File.Exists(statePath))
			{
				return null;
			}
			using FileStream utf8Json = File.OpenRead(statePath);
			InstallState installState = JsonSerializer.Deserialize<InstallState>(utf8Json, JsonOptions);
			if (installState == null)
			{
				return null;
			}
			installState.InstallPath = installPath;
			installState.Files = new Dictionary<string, string>(installState.Files ?? new Dictionary<string, string>(), StringComparer.OrdinalIgnoreCase);
			installState.VerifiedWriteTimesUtc = new Dictionary<string, long>(installState.VerifiedWriteTimesUtc ?? new Dictionary<string, long>(), StringComparer.OrdinalIgnoreCase);
			return installState;
		}
		catch (Exception value)
		{
			ErrorLog.Write("Verification cache", $"Не удалось прочитать {GetStatePath(installPath)}: {value}");
			return null;
		}
	}

	public void Save()
	{
		Directory.CreateDirectory(GetStateDirectory(InstallPath));
		string statePath = GetStatePath(InstallPath);
		string text = statePath + ".tmp";
		using (FileStream utf8Json = File.Create(text))
		{
			JsonSerializer.Serialize(utf8Json, this, JsonOptions);
		}
		File.Move(text, statePath, overwrite: true);
	}

	public static InstallState CreateNew(string installPath, string version)
	{
		return new InstallState
		{
			InstallPath = installPath,
			Version = version
		};
	}
}
