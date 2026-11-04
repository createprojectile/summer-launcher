using System;
using System.IO;
using System.Media;
using System.Text;
using System.Windows;
using System.Windows.Resources;

namespace Summer.Helpers;

public static class UiSound
{
	private static SoundPlayer? _player;

	private static MemoryStream? _buffer;

	private static byte[]? _header;

	private static byte[]? _pcm;

	private static bool _initialized;

	private static bool _enabled = true;

	private static int _volume = 70;

	public static void Initialize(bool enabled, double volume)
	{
		_enabled = enabled;
		int num = ClampVolume(volume);
		if (_initialized)
		{
			if (_volume != num)
			{
				_volume = num;
				RebuildPlayer();
			}
			return;
		}
		_volume = num;
		try
		{
			StreamResourceInfo resourceStream = Application.GetResourceStream(new Uri("pack://application:,,,/Assets/Sounds/ui-tap.wav", UriKind.Absolute));
			if (resourceStream == null)
			{
				return;
			}
			using (resourceStream.Stream)
			{
				using MemoryStream memoryStream = new MemoryStream();
				resourceStream.Stream.CopyTo(memoryStream);
				if (!TryParseWave(memoryStream.ToArray(), out _header, out _pcm))
				{
					return;
				}
			}
			_initialized = true;
			RebuildPlayer();
		}
		catch
		{
		}
	}

	public static void Configure(bool enabled, double volume)
	{
		_enabled = enabled;
		int num = ClampVolume(volume);
		if (num != _volume)
		{
			_volume = num;
			if (_initialized)
			{
				RebuildPlayer();
			}
		}
	}

	public static void PlayTap()
	{
		if (!_enabled || _volume <= 0)
		{
			return;
		}
		try
		{
			_player?.Play();
		}
		catch
		{
		}
	}

	private static void RebuildPlayer()
	{
		if (_header == null || _pcm == null)
		{
			return;
		}
		try
		{
			double num = (double)_volume / 100.0;
			byte[] array = new byte[_header.Length + _pcm.Length];
			Buffer.BlockCopy(_header, 0, array, 0, _header.Length);
			for (int i = 0; i + 1 < _pcm.Length; i += 2)
			{
				int value = (int)Math.Round((double)BitConverter.ToInt16(_pcm, i) * num, MidpointRounding.AwayFromZero);
				value = Math.Clamp(value, -32768, 32767);
				array[_header.Length + i] = (byte)value;
				array[_header.Length + i + 1] = (byte)(value >> 8);
			}
			MemoryStream memoryStream = new MemoryStream(array);
			SoundPlayer soundPlayer = new SoundPlayer(memoryStream);
			soundPlayer.Load();
			_player?.Dispose();
			_buffer?.Dispose();
			_player = soundPlayer;
			_buffer = memoryStream;
		}
		catch
		{
		}
	}

	private static int ClampVolume(double volume)
	{
		if (!double.IsFinite(volume))
		{
			return 70;
		}
		return (int)Math.Round(Math.Clamp(volume, 0.0, 100.0));
	}

	private static bool TryParseWave(byte[] bytes, out byte[] header, out byte[] pcm)
	{
		header = Array.Empty<byte>();
		pcm = Array.Empty<byte>();
		if (bytes.Length < 44 || Encoding.ASCII.GetString(bytes, 0, 4) != "RIFF" || Encoding.ASCII.GetString(bytes, 8, 4) != "WAVE")
		{
			return false;
		}
		int i = 12;
		int num = -1;
		int num2 = 0;
		int num3;
		for (; i + 8 <= bytes.Length; i += 8 + num3 + (num3 & 1))
		{
			string text = Encoding.ASCII.GetString(bytes, i, 4);
			num3 = BitConverter.ToInt32(bytes, i + 4);
			if (num3 < 0 || i + 8 + num3 > bytes.Length)
			{
				return false;
			}
			if (text == "data")
			{
				num = i + 8;
				num2 = num3;
				break;
			}
		}
		if (num < 0 || num2 < 2 || num2 % 2 != 0)
		{
			return false;
		}
		header = new byte[num];
		Buffer.BlockCopy(bytes, 0, header, 0, num);
		pcm = new byte[num2];
		Buffer.BlockCopy(bytes, num, pcm, 0, num2);
		return true;
	}
}
