using System;
using System.IO;
using Gtk;

namespace Unai.Unclip
{
	class Program
	{
		[STAThread]
		public static void Main(string[] args)
		{
			Application.Init();

			var app = new Application("io.github.unai-d.unclip", GLib.ApplicationFlags.None);
			app.Register(GLib.Cancellable.Current);

			var mainWindow = new MainWindow();
			app.AddWindow(mainWindow);

			mainWindow.Show();
			Application.Run();

			return;
		}
	}
}