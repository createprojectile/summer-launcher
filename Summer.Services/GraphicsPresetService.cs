using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Summer.Models;

namespace Summer.Services;

public static class GraphicsPresetService
{
	public const string PresetCombat = "combat";

	public const string PresetOptimal = "optimal";

	public const string PresetUltra = "ultra";

	public const string CombatConfigContent = "graphics.shadowlights \"0\"\r\ngraphics.shadowmode \"2\"\r\ngraphics.shadowquality \"0\"\r\ngraphicssettings.shadowcascades \"1\"\r\ngraphicssettings.shadowdistancepercent \"0\"\r\ngraphicssettings.shadowmaskmode \"0\"\r\ngraphicssettings.shadowresolution \"0\"\r\ngraphicssettings.globaltexturemipmaplimit \"2\"\r\ngraphics.af \"2\"\r\ngraphics.lodbias \"0.6\"\r\ngraphics.shaderlod \"2\"\r\ngraphicssettings.anisotropicfiltering \"1\"\r\nmesh.quality \"30\"\r\nterrain.quality \"100\"\r\ngraphics.contactshadows \"False\"\r\neffects.ao \"False\"\r\ntree.meshes \"100\"\r\ntree.quality \"150\"\r\nwater.quality \"0\"\r\nwater.reflections \"0\"\r\ngrass.displacement \"True\"\r\ngrass.quality \"0\"\r\ngraphics.grassshadows \"False\"\r\ngraphics.impostorshadows \"False\"\r\nreflection.planarreflections \"True\"\r\ngraphics.volumetric_clouds \"0\"\r\neffects.sharpen \"True\"\r\neffects.antialiasing \"0\"";

	public const string OptimalConfigContent = "graphics.shadowlights \"0\"\r\ngraphics.shadowmode \"2\"\r\ngraphics.shadowquality \"0\"\r\ngraphicssettings.shadowcascades \"4\"\r\ngraphicssettings.shadowdistancepercent \"100\"\r\ngraphicssettings.shadowmaskmode \"0\"\r\ngraphicssettings.shadowresolution \"3\"\r\ngraphicssettings.globaltexturemipmaplimit \"1\"\r\ngraphics.af \"8\"\r\ngraphics.lodbias \"1\"\r\ngraphics.shaderlod \"3\"\r\ngraphicssettings.anisotropicfiltering \"1\"\r\nmesh.quality \"150\"\r\nterrain.quality \"100\"\r\ngraphics.contactshadows \"False\"\r\neffects.ao \"True\"\r\ntree.meshes \"100\"\r\ntree.quality \"150\"\r\nwater.quality \"0\"\r\nwater.reflections \"2\"\r\ngrass.displacement \"True\"\r\ngrass.quality \"100\"\r\ngraphics.grassshadows \"True\"\r\ngraphics.impostorshadows \"False\"\r\nreflection.planarreflections \"True\"\r\ngraphics.volumetric_clouds \"1\"\r\neffects.sharpen \"True\"\r\neffects.antialiasing \"2\"";

	public const string UltraConfigContent = "graphics.shadowlights \"0\"\r\ngraphics.shadowmode \"2\"\r\ngraphics.shadowquality \"0\"\r\ngraphicssettings.shadowcascades \"4\"\r\ngraphicssettings.shadowdistancepercent \"100\"\r\ngraphicssettings.shadowmaskmode \"0\"\r\ngraphicssettings.shadowresolution \"3\"\r\ngraphicssettings.globaltexturemipmaplimit \"0\"\r\ngraphics.af \"8\"\r\ngraphics.lodbias \"1\"\r\ngraphics.shaderlod \"5\"\r\ngraphicssettings.anisotropicfiltering \"0\"\r\nmesh.quality \"150\"\r\nterrain.quality \"100\"\r\ngraphics.contactshadows \"True\"\r\neffects.ao \"True\"\r\ntree.meshes \"100\"\r\ntree.quality \"500\"\r\nwater.quality \"0\"\r\nwater.reflections \"2\"\r\ngrass.displacement \"True\"\r\ngrass.quality \"100\"\r\ngraphics.grassshadows \"True\"\r\ngraphics.impostorshadows \"False\"\r\nreflection.planarreflections \"False\"\r\ngraphics.volumetric_clouds \"4\"\r\neffects.sharpen \"True\"\r\neffects.antialiasing \"3\"";

	public static string NormalizePresetId(string? presetId)
	{
		if (string.IsNullOrWhiteSpace(presetId))
		{
			return "optimal";
		}
		string text = presetId.Trim().ToLowerInvariant();
		if (text.Contains("combat") || text.Contains("комбат") || text.Contains("2-комбат") || text.Contains("pvp"))
		{
			return "combat";
		}
		if (text.Contains("ultra") || text.Contains("ультра") || text.Contains("4-ультра") || text.Contains("макс"))
		{
			return "ultra";
		}
		return "optimal";
	}

	public static string GetPresetConfig(string presetId)
	{
		string text = NormalizePresetId(presetId);
		string text2;
		if (text == "combat")
		{
			text2 = "combat.cfg";
		}
		else
		{
			text2 = ((!(text == "ultra")) ? "optimal.cfg" : "ultra.cfg");
		}
		string text3 = text2;
		string[] array = new string[3]
		{
			Path.Combine(AppContext.BaseDirectory, "Assets", "Presets", text3),
			Path.Combine(AppContext.BaseDirectory, "Presets", text3),
			Path.Combine(AppContext.BaseDirectory, text3)
		};
		foreach (string path in array)
		{
			try
			{
				if (File.Exists(path))
				{
					string text4 = File.ReadAllText(path, Encoding.UTF8);
					if (!string.IsNullOrWhiteSpace(text4))
					{
						return text4;
					}
				}
			}
			catch
			{
			}
		}
		if (!(text == "combat"))
		{
			if (text == "ultra")
			{
				return "graphics.shadowlights \"0\"\r\ngraphics.shadowmode \"2\"\r\ngraphics.shadowquality \"0\"\r\ngraphicssettings.shadowcascades \"4\"\r\ngraphicssettings.shadowdistancepercent \"100\"\r\ngraphicssettings.shadowmaskmode \"0\"\r\ngraphicssettings.shadowresolution \"3\"\r\ngraphicssettings.globaltexturemipmaplimit \"0\"\r\ngraphics.af \"8\"\r\ngraphics.lodbias \"1\"\r\ngraphics.shaderlod \"5\"\r\ngraphicssettings.anisotropicfiltering \"0\"\r\nmesh.quality \"150\"\r\nterrain.quality \"100\"\r\ngraphics.contactshadows \"True\"\r\neffects.ao \"True\"\r\ntree.meshes \"100\"\r\ntree.quality \"500\"\r\nwater.quality \"0\"\r\nwater.reflections \"2\"\r\ngrass.displacement \"True\"\r\ngrass.quality \"100\"\r\ngraphics.grassshadows \"True\"\r\ngraphics.impostorshadows \"False\"\r\nreflection.planarreflections \"False\"\r\ngraphics.volumetric_clouds \"4\"\r\neffects.sharpen \"True\"\r\neffects.antialiasing \"3\"";
			}
			return "graphics.shadowlights \"0\"\r\ngraphics.shadowmode \"2\"\r\ngraphics.shadowquality \"0\"\r\ngraphicssettings.shadowcascades \"4\"\r\ngraphicssettings.shadowdistancepercent \"100\"\r\ngraphicssettings.shadowmaskmode \"0\"\r\ngraphicssettings.shadowresolution \"3\"\r\ngraphicssettings.globaltexturemipmaplimit \"1\"\r\ngraphics.af \"8\"\r\ngraphics.lodbias \"1\"\r\ngraphics.shaderlod \"3\"\r\ngraphicssettings.anisotropicfiltering \"1\"\r\nmesh.quality \"150\"\r\nterrain.quality \"100\"\r\ngraphics.contactshadows \"False\"\r\neffects.ao \"True\"\r\ntree.meshes \"100\"\r\ntree.quality \"150\"\r\nwater.quality \"0\"\r\nwater.reflections \"2\"\r\ngrass.displacement \"True\"\r\ngrass.quality \"100\"\r\ngraphics.grassshadows \"True\"\r\ngraphics.impostorshadows \"False\"\r\nreflection.planarreflections \"True\"\r\ngraphics.volumetric_clouds \"1\"\r\neffects.sharpen \"True\"\r\neffects.antialiasing \"2\"";
		}
		return "graphics.shadowlights \"0\"\r\ngraphics.shadowmode \"2\"\r\ngraphics.shadowquality \"0\"\r\ngraphicssettings.shadowcascades \"1\"\r\ngraphicssettings.shadowdistancepercent \"0\"\r\ngraphicssettings.shadowmaskmode \"0\"\r\ngraphicssettings.shadowresolution \"0\"\r\ngraphicssettings.globaltexturemipmaplimit \"2\"\r\ngraphics.af \"2\"\r\ngraphics.lodbias \"0.6\"\r\ngraphics.shaderlod \"2\"\r\ngraphicssettings.anisotropicfiltering \"1\"\r\nmesh.quality \"30\"\r\nterrain.quality \"100\"\r\ngraphics.contactshadows \"False\"\r\neffects.ao \"False\"\r\ntree.meshes \"100\"\r\ntree.quality \"150\"\r\nwater.quality \"0\"\r\nwater.reflections \"0\"\r\ngrass.displacement \"True\"\r\ngrass.quality \"0\"\r\ngraphics.grassshadows \"False\"\r\ngraphics.impostorshadows \"False\"\r\nreflection.planarreflections \"True\"\r\ngraphics.volumetric_clouds \"0\"\r\neffects.sharpen \"True\"\r\neffects.antialiasing \"0\"";
	}

	public static bool ApplyPreset(string? gameDirectory, string presetId, LauncherSettings settings, GamePlacementService? placementService = null)
	{
		string presetConfig = GetPresetConfig(NormalizePresetId(presetId));
		string text = GamePlacementService.FindRustClient(settings.InstallPath) ?? GamePlacementService.FindRustClient(gameDirectory ?? string.Empty);
		if (text == null)
		{
			return false;
		}
		if (!GamePlacementService.IsAllowedLauncherDirectory(Path.GetDirectoryName(text)))
		{
			return false;
		}
		string text2 = Path.Combine(Path.GetDirectoryName(text), "cfg", "client.cfg");
		try
		{
			Directory.CreateDirectory(Path.GetDirectoryName(text2));
			List<string> contents = MergePresetIntoConfig(File.Exists(text2) ? File.ReadAllText(text2, Encoding.UTF8) : null, presetConfig);
			if (File.Exists(text2))
			{
				File.SetAttributes(text2, File.GetAttributes(text2) & ~FileAttributes.ReadOnly);
			}
			File.WriteAllLines(text2 + ".summer.tmp", contents, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
			File.Move(text2 + ".summer.tmp", text2, overwrite: true);
			return true;
		}
		catch (Exception error)
		{
			ErrorLog.Write("Graphics preset", error);
			return false;
		}
	}

	public static List<string> MergePresetIntoConfig(string? existingConfigContent, string presetContent)
	{
		List<string> list = (from l in presetContent.Split(new char[2] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
			select l.Trim() into l
			where !string.IsNullOrWhiteSpace(l) && !l.StartsWith("#") && !l.StartsWith("//")
			select l).ToList();
		List<(string, string)> list2 = new List<(string, string)>();
		string value;
		foreach (string item in list)
		{
			if (TryParseConfigLine(item, out string key, out value))
			{
				list2.Add((key, item));
			}
		}
		if (string.IsNullOrWhiteSpace(existingConfigContent))
		{
			return list2.Select(((string Key, string FullLine) e) => e.FullLine).ToList();
		}
		List<string> list3 = existingConfigContent.Split(new string[3] { "\r\n", "\r", "\n" }, StringSplitOptions.None).ToList();
		Dictionary<string, int> dictionary = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
		for (int num = 0; num < list3.Count; num++)
		{
			if (TryParseConfigLine(list3[num], out string key2, out value))
			{
				dictionary[key2] = num;
			}
		}
		List<string> list4 = new List<string>(list3);
		HashSet<string> hashSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		foreach (var (text, text2) in list2)
		{
			if (dictionary.TryGetValue(text, out var value2))
			{
				list4[value2] = text2;
			}
			else if (hashSet.Add(text))
			{
				list4.Add(text2);
			}
		}
		return list4;
	}

	public static bool TryParseConfigLine(string line, out string key, out string value)
	{
		key = string.Empty;
		value = string.Empty;
		if (string.IsNullOrWhiteSpace(line))
		{
			return false;
		}
		string text = line.Trim();
		if (text.StartsWith("#") || text.StartsWith("//"))
		{
			return false;
		}
		int num = text.IndexOf(' ');
		if (num <= 0)
		{
			return false;
		}
		key = text.Substring(0, num).Trim();
		value = text.Substring(num + 1).Trim();
		return key.Length > 0;
	}
}
