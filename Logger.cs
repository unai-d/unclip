using System;
using System.Collections.Generic;

namespace Unai.Unclip
{
	public enum LogType
	{
		Info,
		Warning,
		Error,
		Debug,
		Success
	}

	public static class Logger
	{
		static ConsoleColor DefaultConsoleForegroundColor = Console.ForegroundColor;
		public static bool PrintDebugLogs => !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("DEBUG"));
		public static void Log(string text, LogType type = LogType.Info)
		{
			string ansiEscapeCodeForColor = type switch
			{
				LogType.Info => string.Empty,
				LogType.Warning => "\x1b[93m",
				LogType.Error => "\x1b[91m",
				LogType.Debug => "\x1b[96m",
				LogType.Success => "\x1b[92m",
				_ => string.Empty
			};
			if (type != LogType.Debug) Console.WriteLine(ansiEscapeCodeForColor + text + "\x1b[0m");
		}
	}
}