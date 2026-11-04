using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Summer.Models;

namespace Summer.Services;

public static class KeyConfigService
{
	private static readonly (string ActionName, string Command, string DefaultKey)[] DefaultRustBindings = new (string, string, string)[19]
	{
		("Вперёд", "+forward", "w"),
		("Назад", "+backward", "s"),
		("Влево", "+left", "a"),
		("Вправо", "+right", "d"),
		("Прыжок", "+jump", "space"),
		("Присесть", "+duck", "leftcontrol"),
		("Бег", "+sprint", "leftshift"),
		("Атака", "+attack", "mouse0"),
		("Перезарядка", "+reload", "r"),
		("Инвентарь", "inventory.toggle", "tab"),
		("Прицеливание", "+attack2", "mouse1"),
		("Доп. атака", "+attack3", "mouse2"),
		("Оглядеться", "+altlook", "leftalt"),
		("Действие", "+use", "e"),
		("Крафт", "craft.open", "q"),
		("Чат", "chat.open", "t"),
		("Ввод чата", "chat.open", "return"),
		("Голосовой чат", "+voice", "v"),
		("Карта", "+map", "m")
	};

	public static bool SyncKeyBindings(string? gameDirectory, LauncherSettings settings, GamePlacementService? placementService = null)
	{
		if (settings == null)
		{
			return false;
		}
		string text = GamePlacementService.FindRustClient(settings.InstallPath) ?? GamePlacementService.FindRustClient(gameDirectory ?? string.Empty);
		if (text == null)
		{
			return false;
		}
		if (!GamePlacementService.IsAllowedLauncherDirectory(Path.GetDirectoryName(text)))
		{
			return false;
		}
		string text2 = Path.Combine(Path.GetDirectoryName(text), "cfg", "keys.cfg");
		try
		{
			Directory.CreateDirectory(Path.GetDirectoryName(text2));
			List<string> contents = GenerateMergedConfig(text2, settings);
			File.WriteAllLines(text2 + ".summer.tmp", contents, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
			File.Move(text2 + ".summer.tmp", text2, overwrite: true);
			return true;
		}
		catch (Exception error)
		{
			ErrorLog.Write("Key bindings", error);
			return false;
		}
	}

	public static List<string> GenerateMergedConfig(string? existingFilePath, LauncherSettings settings)
	{
		Dictionary<string, string> dictionary = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
		List<string> list = ((!string.IsNullOrWhiteSpace(existingFilePath) && File.Exists(existingFilePath)) ? File.ReadAllLines(existingFilePath).ToList() : new List<string>());
		if (list.Count > 0)
		{
			foreach (string item in list)
			{
				if (TryReadBind(item, out string key, out string command))
				{
					dictionary[key] = command;
				}
			}
		}
		else
		{
			(string, string, string)[] defaultRustBindings = DefaultRustBindings;
			for (int i = 0; i < defaultRustBindings.Length; i++)
			{
				(string, string, string) tuple = defaultRustBindings[i];
				dictionary[tuple.Item3] = tuple.Item2;
			}
		}
		if (settings.KeyBinds != null)
		{
			foreach (KeyBindInfo keyBind in settings.KeyBinds)
			{
				string rustCommand = ActionNameToRustCommand(keyBind.ActionName);
				if (string.IsNullOrEmpty(rustCommand))
				{
					continue;
				}
				string text = ToRustKey(keyBind.Key);
				foreach (string item2 in (from kvp in dictionary
					where string.Equals(kvp.Value.Trim('"'), rustCommand, StringComparison.OrdinalIgnoreCase)
					select kvp.Key).ToList())
				{
					dictionary.Remove(item2);
				}
				if (!string.IsNullOrWhiteSpace(text))
				{
					dictionary[text] = rustCommand;
				}
			}
		}
		if (settings.MacroBinds != null)
		{
			foreach (KeyBindInfo macroBind in settings.MacroBinds)
			{
				if (string.IsNullOrWhiteSpace(macroBind.Key) || string.IsNullOrWhiteSpace(macroBind.Command))
				{
					continue;
				}
				string text2 = ToRustKey(macroBind.Key);
				if (string.IsNullOrWhiteSpace(text2))
				{
					continue;
				}
				string text3 = FormatMacroCommand(macroBind.Command, text2);
				if (string.IsNullOrWhiteSpace(text3))
				{
					continue;
				}
				if (text3.StartsWith("bind ", StringComparison.OrdinalIgnoreCase))
				{
					string text4 = text3.Substring(5).TrimStart();
					int num = text4.IndexOf(' ');
					if (num > 0)
					{
						string key2 = text4.Substring(0, num).Trim();
						string value = text4.Substring(num + 1).Trim();
						dictionary[key2] = value;
					}
					else
					{
						dictionary[text2] = text4;
					}
				}
				else
				{
					dictionary[text2] = text3;
				}
			}
		}
		List<string> list2 = new List<string>();
		HashSet<string> hashSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		foreach (string item3 in list)
		{
			string value2;
			if (!TryReadBind(item3, out string key3, out string command2))
			{
				list2.Add(item3);
			}
			else if (dictionary.TryGetValue(key3, out value2) && hashSet.Add(key3))
			{
				list2.Add((value2 == command2) ? item3 : ("bind " + key3 + " " + value2));
			}
		}
		foreach (var (text7, text8) in dictionary)
		{
			if (hashSet.Add(text7))
			{
				list2.Add("bind " + text7 + " " + text8);
			}
		}
		return list2;
	}

	private static bool TryReadBind(string line, out string key, out string command)
	{
		key = (command = string.Empty);
		string text = line.Trim();
		if (!text.StartsWith("bind ", StringComparison.OrdinalIgnoreCase))
		{
			return false;
		}
		string text2 = text;
		string[] array = text2.Substring(5, text2.Length - 5).Split(new char[2] { ' ', '\t' }, 2, StringSplitOptions.RemoveEmptyEntries);
		if (array.Length != 2)
		{
			return false;
		}
		key = array[0].Trim('"');
		command = array[1].Trim();
		if (key.Length > 0)
		{
			return command.Length > 0;
		}
		return false;
	}

	public static string ToRustKey(string? keyName)
	{
		if (string.IsNullOrWhiteSpace(keyName))
		{
			return string.Empty;
		}
		string text = keyName.Trim();
		switch (text.ToLowerInvariant())
		{
		case "лкм":
		case "lmb":
		case "mouse0":
			return "mouse0";
		case "пкм":
		case "rmb":
		case "mouse1":
			return "mouse1";
		case "скм":
		case "mmb":
		case "mouse2":
			return "mouse2";
		case "mb4":
		case "mouse3":
		case "боковая 1":
			return "mouse3";
		case "mb5":
		case "mouse4":
		case "боковая 2":
			return "mouse4";
		case "ctrl":
		case "lctrl":
		case "leftctrl":
			return "leftcontrol";
		case "rightctrl":
		case "rctrl":
			return "rightcontrol";
		case "lshift":
		case "leftshift":
		case "shift":
			return "leftshift";
		case "rshift":
		case "rightshift":
			return "rightshift";
		case "alt":
		case "lalt":
		case "leftalt":
			return "leftalt";
		case "ralt":
		case "rightalt":
			return "rightalt";
		case "пробел":
		case "space":
			return "space";
		case "return":
		case "ввод":
		case "enter":
			return "return";
		case "tab":
			return "tab";
		case "backspace":
		case "back":
			return "backspace";
		case "del":
		case "delete":
			return "delete";
		case "ins":
		case "insert":
			return "insert";
		case "esc":
		case "escape":
			return "escape";
		case "caps":
		case "capslock":
			return "capslock";
		case "uparrow":
		case "up":
			return "uparrow";
		case "downarrow":
		case "down":
			return "downarrow";
		case "leftarrow":
		case "left":
			return "leftarrow";
		case "right":
		case "rightarrow":
			return "rightarrow";
		case "home":
			return "home";
		case "end":
			return "end";
		case "pageup":
		case "prior":
			return "pageup";
		case "next":
		case "pagedown":
			return "pagedown";
		case "numpad0":
		case "d0":
			return "0";
		case "numpad1":
		case "d1":
			return "1";
		case "numpad2":
		case "d2":
			return "2";
		case "numpad3":
		case "d3":
			return "3";
		case "numpad4":
		case "d4":
			return "4";
		case "numpad5":
		case "d5":
			return "5";
		case "numpad6":
		case "d6":
			return "6";
		case "numpad7":
		case "d7":
			return "7";
		case "numpad8":
		case "d8":
			return "8";
		case "numpad9":
		case "d9":
			return "9";
		default:
			return text.ToLowerInvariant();
		}
	}

	public static string? ActionNameToRustCommand(string? actionName)
	{
		if (string.IsNullOrWhiteSpace(actionName))
		{
			return null;
		}
		switch (actionName.Trim().ToLowerInvariant())
		{
		case "вперёд":
		case "forward":
			return "+forward";
		case "назад":
		case "backward":
		case "back":
			return "+backward";
		case "влево":
		case "left":
			return "+left";
		case "вправо":
		case "right":
			return "+right";
		case "прыжок":
		case "jump":
			return "+jump";
		case "crouch":
		case "присесть":
		case "duck":
			return "+duck";
		case "sprint":
		case "run":
		case "бег":
			return "+sprint";
		case "attack":
		case "атака":
			return "+attack";
		case "reload":
		case "перезарядка":
			return "+reload";
		case "inventory":
		case "инвентарь":
			return "inventory.toggle";
		default:
			return null;
		}
	}

	public static string FormatMacroCommand(string commandTemplate, string rustKey)
	{
		return (commandTemplate ?? string.Empty).Replace("[кнопка]", rustKey, StringComparison.OrdinalIgnoreCase).Replace("[button]", rustKey, StringComparison.OrdinalIgnoreCase).Replace("[key]", rustKey, StringComparison.OrdinalIgnoreCase)
			.Replace("[количество]", "5", StringComparison.OrdinalIgnoreCase)
			.Replace("[n]", "0.8", StringComparison.OrdinalIgnoreCase)
			.Replace("[m]", "1.0", StringComparison.OrdinalIgnoreCase)
			.Trim();
	}
}
