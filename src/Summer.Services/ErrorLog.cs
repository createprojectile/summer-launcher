using System;
using System.IO;

namespace Summer.Services;

public static class ErrorLog
{
	private static readonly object Sync = new object();

	public static string PathToLog { get; internal set; } = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SummerLauncher", "errors.log");

	public static void Write(string area, Exception error)
	{
		Write(area, error.ToString());
	}

	public static void Write(string area, string error)
	{
		try
		{
			lock (Sync)
			{
				Directory.CreateDirectory(Path.GetDirectoryName(PathToLog));
				if (File.Exists(PathToLog) && new FileInfo(PathToLog).Length > 5242880)
				{
					File.Move(PathToLog, PathToLog + ".old", overwrite: true);
				}
				File.AppendAllText(PathToLog, $"[{DateTimeOffset.Now:O}] [{area}] {error}{Environment.NewLine}");
			}
		}
		catch
		{
		}
	}
}
