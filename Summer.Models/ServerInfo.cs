using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Summer.Helpers;

namespace Summer.Models;

public sealed class ServerInfo : ViewModelBase
{
	private string _wipeCountdown = "2 ДНЯ";

	private int _online;

	private int _capacity = 150;

	private int _ping = 24;

	private int _queue;

	private double _uptime = 99.9;

	private string _map = string.Empty;

	private bool _isOnline = true;

	public required string Id { get; init; }

	public required string Code { get; init; }

	public required string Name { get; init; }

	public required string Description { get; init; }

	public required string Mode { get; init; }

	public required string Wipe { get; init; }

	public string Host { get; init; } = string.Empty;

	public int Port { get; init; } = 5555;

	public int QueryPort { get; init; } = 5557;

	public string Gs4uUrl { get; init; } = string.Empty;

	public required string Map
	{
		get
		{
			return _map;
		}
		set
		{
			SetProperty(ref _map, value, "Map");
		}
	}

	public int Online
	{
		get
		{
			return _online;
		}
		set
		{
			if (SetProperty(ref _online, value, "Online"))
			{
				OnPropertyChanged("OnlineLabel");
				OnPropertyChanged("FillPercent");
				OnPropertyChanged("FillPercentLabel");
				OnPropertyChanged("PeakOnlineLabel");
			}
		}
	}

	public int Capacity
	{
		get
		{
			return _capacity;
		}
		set
		{
			if (SetProperty(ref _capacity, value, "Capacity"))
			{
				OnPropertyChanged("CapacityLabel");
				OnPropertyChanged("CapacityValueLabel");
				OnPropertyChanged("FillPercent");
				OnPropertyChanged("FillPercentLabel");
			}
		}
	}

	public bool HasWipe { get; init; } = true;

	public int Ping
	{
		get
		{
			return _ping;
		}
		set
		{
			if (SetProperty(ref _ping, value, "Ping"))
			{
				OnPropertyChanged("PingLabel");
			}
		}
	}

	public int Queue
	{
		get
		{
			return _queue;
		}
		set
		{
			if (SetProperty(ref _queue, value, "Queue"))
			{
				OnPropertyChanged("QueueLabel");
			}
		}
	}

	public double Uptime
	{
		get
		{
			return _uptime;
		}
		set
		{
			if (SetProperty(ref _uptime, value, "Uptime"))
			{
				OnPropertyChanged("UptimeLabel");
			}
		}
	}

	public bool IsOnline
	{
		get
		{
			return _isOnline;
		}
		set
		{
			SetProperty(ref _isOnline, value, "IsOnline");
		}
	}

	public string WipeCountdown
	{
		get
		{
			return _wipeCountdown;
		}
		set
		{
			if (SetProperty(ref _wipeCountdown, value, "WipeCountdown"))
			{
				OnPropertyChanged("WipeCountdownLabel");
			}
		}
	}

	public IReadOnlyList<string> Tags { get; init; } = Array.Empty<string>();

	public IReadOnlyList<double> OnlineHistory { get; init; } = Array.Empty<double>();

	public string CoverImage { get; init; } = string.Empty;

	public string CapacityLabel => "СЛОТОВ " + Capacity.ToString("N0", CultureInfo.GetCultureInfo("ru-RU"));

	public string CapacityValueLabel => Capacity.ToString("N0", CultureInfo.GetCultureInfo("ru-RU"));

	public string OnlineLabel => Online.ToString("N0", CultureInfo.GetCultureInfo("ru-RU"));

	public double FillPercent
	{
		get
		{
			if (Capacity <= 0)
			{
				return 0.0;
			}
			return Math.Clamp((double)Online * 100.0 / (double)Capacity, 0.0, 100.0);
		}
	}

	public string FillPercentLabel => $"{FillPercent:0}%";

	public string PingLabel => $"{Ping} МС";

	public string QueueLabel => Queue.ToString(CultureInfo.GetCultureInfo("ru-RU"));

	public string UptimeLabel => Uptime.ToString("0.0", CultureInfo.GetCultureInfo("ru-RU")) + "%";

	public string WipeCountdownLabel => "ДО ВАЙПА " + WipeCountdown;

	public string PeakOnlineLabel
	{
		get
		{
			if (OnlineHistory.Count <= 0)
			{
				return OnlineLabel;
			}
			return Math.Max(OnlineHistory.Max(), Online).ToString("N0", CultureInfo.GetCultureInfo("ru-RU"));
		}
	}
}
