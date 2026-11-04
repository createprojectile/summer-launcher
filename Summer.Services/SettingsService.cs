using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Summer.Models;

namespace Summer.Services;

public sealed class SettingsService
{
	private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
	{
		PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
		PropertyNameCaseInsensitive = true,
		WriteIndented = true,
		AllowTrailingCommas = true,
		ReadCommentHandling = JsonCommentHandling.Skip
	};

	private readonly object _fileLock = new object();

	private readonly string _settingsPath;

	public string SettingsPath => _settingsPath;

	public string? LastError { get; private set; }

	public SettingsService(string? settingsPath = null)
	{
		_settingsPath = settingsPath ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "SummerLauncher", "launcher-settings.json");
	}

	public LauncherSettings Load()
	{
		lock (_fileLock)
		{
			LastError = null;
			try
			{
				if (!File.Exists(_settingsPath))
				{
					return LauncherSettings.CreateDefault();
				}
				LauncherSettings? obj = JsonSerializer.Deserialize<LauncherSettings>(File.ReadAllText(_settingsPath, Encoding.UTF8), JsonOptions) ?? LauncherSettings.CreateDefault();
				Normalize(obj);
				return obj;
			}
			catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
			{
				LastError = "Не удалось прочитать настройки: " + ex.Message;
				TryBackupBrokenFile();
				return LauncherSettings.CreateDefault();
			}
		}
	}

	public bool Save(LauncherSettings settings)
	{
		lock (_fileLock)
		{
			LastError = null;
			string text = _settingsPath + "." + Guid.NewGuid().ToString("N") + ".tmp";
			try
			{
				string directoryName = Path.GetDirectoryName(_settingsPath);
				if (!string.IsNullOrWhiteSpace(directoryName))
				{
					Directory.CreateDirectory(directoryName);
				}
				JsonObject jsonObject = JsonSerializer.SerializeToNode(settings, JsonOptions)?.AsObject() ?? new JsonObject();
				jsonObject.Remove("cdnBaseUrl");
				File.WriteAllText(text, jsonObject.ToJsonString(JsonOptions), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
				File.Move(text, _settingsPath, overwrite: true);
				return true;
			}
			catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
			{
				LastError = "Не удалось сохранить настройки: " + ex.Message;
				return false;
			}
			finally
			{
				try
				{
					if (File.Exists(text))
					{
						File.Delete(text);
					}
				}
				catch
				{
				}
			}
		}
	}

	private static void Normalize(LauncherSettings settings)
	{
		settings.InstallPath = settings.InstallPath?.Trim().Trim('"') ?? string.Empty;
		if (settings.LaunchArguments == null)
		{
			settings.LaunchArguments = string.Empty;
		}
		settings.UiSoundVolume = (double.IsFinite(settings.UiSoundVolume) ? Math.Clamp(settings.UiSoundVolume, 0.0, 100.0) : 70.0);
		settings.SelectedServerId = (string.IsNullOrWhiteSpace(settings.SelectedServerId) ? "main" : settings.SelectedServerId.Trim().ToLowerInvariant());
		settings.KeyBinds = MergeBinds(settings.KeyBinds, KeyBindInfo.CreateDefaults());
		settings.MacroBinds = MergeBinds(settings.MacroBinds, KeyBindInfo.CreateMacroBinds());
	}

	private static List<KeyBindInfo> MergeBinds(List<KeyBindInfo>? savedBinds, List<KeyBindInfo> defaults)
	{
		if (savedBinds == null)
		{
			savedBinds = new List<KeyBindInfo>();
		}
		return defaults.Select((KeyBindInfo defaultBind) =>
		{
			KeyBindInfo keyBindInfo = savedBinds.FirstOrDefault((KeyBindInfo bind) => string.Equals(bind.ActionName, defaultBind.ActionName, StringComparison.OrdinalIgnoreCase));
			return new KeyBindInfo
			{
				ActionName = defaultBind.ActionName,
				Description = defaultBind.Description,
				Command = defaultBind.Command,
				Key = ((keyBindInfo == null || string.IsNullOrWhiteSpace(keyBindInfo.Key)) ? defaultBind.Key : keyBindInfo.Key.Trim())
			};
		}).ToList();
	}

	private void TryBackupBrokenFile()
	{
		try
		{
			if (File.Exists(_settingsPath))
			{
				string destFileName = _settingsPath + ".corrupt-" + DateTime.Now.ToString("yyyyMMdd-HHmmss");
				File.Move(_settingsPath, destFileName);
			}
		}
		catch
		{
		}
	}
}
