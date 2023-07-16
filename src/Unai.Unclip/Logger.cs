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
		public delegate void LogHandler(string text, LogType type);
		public static event LogHandler OnLog;

		public static void Log(string text, LogType type = LogType.Info)
		{
			string ansiEscapeCodeForColor = string.Empty;
			switch (type)
			{
				case LogType.Warning: ansiEscapeCodeForColor = "\x1b[93m"; break;
				case LogType.Error: ansiEscapeCodeForColor = "\x1b[91m"; break;
				case LogType.Debug: ansiEscapeCodeForColor = "\x1b[96m"; break;
				case LogType.Success: ansiEscapeCodeForColor = "\x1b[92m"; break;
			};

			if (type != LogType.Debug || PrintDebugLogs) Console.WriteLine(ansiEscapeCodeForColor + text + "\x1b[0m");

			if (OnLog != null)
			{
				OnLog(text, type);
			}
		}
	}
}