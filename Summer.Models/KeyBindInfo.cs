using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.InteropServices;

namespace Summer.Models;

public sealed class KeyBindInfo : INotifyPropertyChanged
{
	private string _actionName = string.Empty;

	private string _key = string.Empty;

	private string _description = string.Empty;

	private string _command = string.Empty;

	public string ActionName
	{
		get
		{
			return _actionName;
		}
		set
		{
			if (!(_actionName == value))
			{
				_actionName = value;
				PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("ActionName"));
			}
		}
	}

	public string Key
	{
		get
		{
			return _key;
		}
		set
		{
			if (!(_key == value))
			{
				_key = value;
				PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Key"));
			}
		}
	}

	public string Description
	{
		get
		{
			return _description;
		}
		set
		{
			if (!(_description == value))
			{
				_description = value;
				PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Description"));
			}
		}
	}

	public string Command
	{
		get
		{
			return _command;
		}
		set
		{
			if (!(_command == value))
			{
				_command = value;
				PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Command"));
			}
		}
	}

	public event PropertyChangedEventHandler? PropertyChanged;

	public static List<KeyBindInfo> CreateDefaults()
	{
		int num = 10;
		List<KeyBindInfo> list = new List<KeyBindInfo>(num);
		CollectionsMarshal.SetCount(list, num);
		Span<KeyBindInfo> span = CollectionsMarshal.AsSpan(list);
		span[0] = new KeyBindInfo
		{
			ActionName = "Вперёд",
			Key = "W"
		};
		span[1] = new KeyBindInfo
		{
			ActionName = "Назад",
			Key = "S"
		};
		span[2] = new KeyBindInfo
		{
			ActionName = "Влево",
			Key = "A"
		};
		span[3] = new KeyBindInfo
		{
			ActionName = "Вправо",
			Key = "D"
		};
		span[4] = new KeyBindInfo
		{
			ActionName = "Прыжок",
			Key = "Space"
		};
		span[5] = new KeyBindInfo
		{
			ActionName = "Присесть",
			Key = "Ctrl"
		};
		span[6] = new KeyBindInfo
		{
			ActionName = "Бег",
			Key = "Shift"
		};
		span[7] = new KeyBindInfo
		{
			ActionName = "Атака",
			Key = "ЛКМ"
		};
		span[8] = new KeyBindInfo
		{
			ActionName = "Перезарядка",
			Key = "R"
		};
		span[9] = new KeyBindInfo
		{
			ActionName = "Инвентарь",
			Key = "Tab"
		};
		return list;
	}

	public static List<KeyBindInfo> CreateMacroBinds()
	{
		int num = 19;
		List<KeyBindInfo> list = new List<KeyBindInfo>(num);
		CollectionsMarshal.SetCount(list, num);
		Span<KeyBindInfo> span = CollectionsMarshal.AsSpan(list);
		span[0] = new KeyBindInfo
		{
			ActionName = "Автобег",
			Description = "Для отключения нажми W и SHIFT",
			Command = "bind [кнопка] buttons.forward; buttons.sprint"
		};
		span[1] = new KeyBindInfo
		{
			ActionName = "Быстрый крафт бинтов",
			Description = "Количество задаётся параметром [количество]",
			Command = "bind [кнопка] craft.add -2072273936 [количество]"
		};
		span[2] = new KeyBindInfo
		{
			ActionName = "Автолазер",
			Description = "+lighttoggle; +buttons.attack",
			Command = "bind [кнопка] +lighttoggle; +buttons.attack"
		};
		span[3] = new KeyBindInfo
		{
			ActionName = "Автоплавание",
			Description = "Для отключения нажми W, SHIFT и ПРОБЕЛ",
			Command = "bind [кнопка] buttons.forward; buttons.sprint; buttons.jump"
		};
		span[4] = new KeyBindInfo
		{
			ActionName = "Автоудар",
			Description = "Для отключения нажми ЛКМ",
			Command = "bind [кнопка] buttons.attack"
		};
		span[5] = new KeyBindInfo
		{
			ActionName = "Автофокус карты",
			Description = "+map; +focusmap",
			Command = "bind [кнопка] +map; +focusmap"
		};
		span[6] = new KeyBindInfo
		{
			ActionName = "Автофонарик",
			Description = "+lighttoggle; +buttons.attack2",
			Command = "bind [кнопка] +lighttoggle; +buttons.attack2"
		};
		span[7] = new KeyBindInfo
		{
			ActionName = "Безопасный килл",
			Description = "kill",
			Command = "bind [кнопка] kill"
		};
		span[8] = new KeyBindInfo
		{
			ActionName = "Буст войсчата",
			Description = "[N] — обычная громкость игры, [M] — обычная громкость войсчата",
			Command = "bind [кнопка] ~meta.exec \"audio.game 0.1\" \"audio.voices 5\"; meta.exec \"audio.game [N]\" \"audio.voices [M]\""
		};
		span[9] = new KeyBindInfo
		{
			ActionName = "Быстрый сброс предмета",
			Description = "+buttons.dropitemstack",
			Command = "bind [кнопка] +buttons.dropitemstack"
		};
		span[10] = new KeyBindInfo
		{
			ActionName = "ВКЛ / ВЫКЛ звуков игры",
			Description = "~meta.exec \"audio.master 0\"; meta.exec \"audio.master 1\"",
			Command = "bind [кнопка] ~meta.exec \"audio.master 0\"; meta.exec \"audio.master 1\""
		};
		span[11] = new KeyBindInfo
		{
			ActionName = "ВКЛ / ВЫКЛ звуков шагов и войсчата",
			Description = "[M] — обычная громкость войсчата",
			Command = "bind [кнопка] ~meta.exec \"effects.footsteps 0\" \"audio.voices 0\"; meta.exec \"effects.footsteps 1\" \"audio.voices [M]\""
		};
		span[12] = new KeyBindInfo
		{
			ActionName = "Отключение чата",
			Description = "~meta.exec \"chat.clear\" \"chat.enabled False\"; meta.exec \"chat.enabled True\"",
			Command = "bind [кнопка] ~meta.exec \"chat.clear\" \"chat.enabled False\"; meta.exec \"chat.enabled True\""
		};
		span[13] = new KeyBindInfo
		{
			ActionName = "Бинокль",
			Description = "+fov 90; fov 70",
			Command = "bind [кнопка] +fov 90; fov 70"
		};
		span[14] = new KeyBindInfo
		{
			ActionName = "Сброс рюкзака",
			Description = "Требуется немного подержать кнопку",
			Command = "bind [кнопка] +buttons.backpack"
		};
		span[15] = new KeyBindInfo
		{
			ActionName = "Отключение табличек",
			Description = "+attack2; +meta.if_true \"global.censorsigns 1\"; +meta.if_false \"global.censorsigns 0\"",
			Command = "bind [кнопка] +attack2; +meta.if_true \"global.censorsigns 1\"; +meta.if_false \"global.censorsigns 0\""
		};
		span[16] = new KeyBindInfo
		{
			ActionName = "Очистка оперативки",
			Description = "gc.collect; pool.clear_assets; pool.clear_memory; pool.clear_prefabs; decal.clear",
			Command = "bind [кнопка] gc.collect; pool.clear_assets; pool.clear_memory; pool.clear_prefabs; decal.clear"
		};
		span[17] = new KeyBindInfo
		{
			ActionName = "Умная консоль",
			Description = "Консоль, combatlog, пинг и FPS одной клавишей",
			Command = "bind [кнопка] client.consoletoggle; console.clear; combatlog; client.ping; client.fps"
		};
		span[18] = new KeyBindInfo
		{
			ActionName = "Переключение LookAtRadius",
			Description = "Min 0.01 / Default 0.2 / Max 10",
			Command = "bind [кнопка] ~meta.exec \"client.lookatradius 0.01\" \"chat.add 0 0 LookAtRadius-MIN\"; meta.exec \"client.lookatradius 0.2\" \"chat.add 0 0 LookAtRadius-DEFAULT\"; meta.exec \"client.lookatradius 10\" \"chat.add 0 0 LookAtRadius-MAX\""
		};
		return list;
	}

	public KeyBindInfo Clone()
	{
		return new KeyBindInfo
		{
			ActionName = ActionName,
			Key = Key,
			Description = Description,
			Command = Command
		};
	}
}
