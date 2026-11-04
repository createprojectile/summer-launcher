using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Summer.Models;

public sealed class LauncherSettings
{
	public string InstallPath { get; set; } = DefaultInstallPath;

	public bool FpsBoosterEnabled { get; set; }

	public bool UiSoundsEnabled { get; set; } = true;

	public double UiSoundVolume { get; set; } = 70.0;

	public bool MinimizeOnGameLaunch { get; set; } = true;

	public string LaunchArguments { get; set; } = string.Empty;

	public string SelectedServerId { get; set; } = "main";

	public string? LauncherUpdateUrl { get; set; }

	public string Language { get; set; } = "ru";

	public string GraphicsPreset { get; set; } = "optimal";

	public List<KeyBindInfo> KeyBinds { get; set; } = KeyBindInfo.CreateDefaults();

	public List<KeyBindInfo> MacroBinds { get; set; } = KeyBindInfo.CreateMacroBinds();

	[JsonIgnore]
	public static string DefaultInstallPath => string.Empty;

	public static LauncherSettings CreateDefault()
	{
		return new LauncherSettings
		{
			InstallPath = string.Empty,
			FpsBoosterEnabled = false,
			UiSoundsEnabled = true,
			UiSoundVolume = 70.0,
			MinimizeOnGameLaunch = true,
			LaunchArguments = string.Empty,
			SelectedServerId = "main",
			Language = "ru",
			GraphicsPreset = "optimal",
			KeyBinds = KeyBindInfo.CreateDefaults(),
			MacroBinds = KeyBindInfo.CreateMacroBinds()
		};
	}
}
