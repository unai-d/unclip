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
			if (args.Length == 0)
			{
				Application.Init();

				var app = new Application("io.github.unai-d.unclip", GLib.ApplicationFlags.None);
				app.Register(GLib.Cancellable.Current);

				var mainWindow = new GtkMainWindow();
				app.AddWindow(mainWindow);

				mainWindow.Show();
				Application.Run();

				return;
			}
				
			CspFile cspFile = new CspFile(args[0]);

			Logger.Log("CSP file overview:");
			foreach (var layer in cspFile.Layers)
			{
				Logger.Log($"  Layer #{layer.Key} '{layer.Value.Name}'.");
			}

			string outputDirectory = Path.GetFullPath(cspFile.FileId);
			Directory.CreateDirectory(outputDirectory);

			foreach (var canvasPreview in cspFile.CanvasPreviews)
			{
				Logger.Log($"Exporting canvas #{canvasPreview.Value.CanvasId}…");

				string canvasPreviewRasterOutputFilePath = $"{outputDirectory}/canvas_{canvasPreview.Value.CanvasId:D4}.png";

				File.WriteAllBytes(canvasPreviewRasterOutputFilePath, canvasPreview.Value.ImageData);
			}

			foreach (var layer in cspFile.Layers)
			{
				Logger.Log($"Exporting canvas #{layer.Value.CanvasId}, layer #{layer.Key} '{layer.Value.Name}'…");
				try
				{
					Bitmap layerRaster = cspFile.GetLayerRasterData(layer.Value.CanvasId, layer.Key);

					string layerRasterOutputFilePath = $"{outputDirectory}/canvas_{layer.Value.CanvasId:D4}/layer_{layer.Key:D4}_[{layer.Value.Name}].png";
					Directory.CreateDirectory(Path.GetDirectoryName(layerRasterOutputFilePath));
					layerRaster.SaveToFile(layerRasterOutputFilePath);
				}
				catch (Exception ex)
				{
					Logger.Log($"  Cannot export layer #{layer.Key}:\n{ex.Message}", LogType.Error);
				}
			}

			Logger.Log($"Output directory: '{outputDirectory}'.");
		}
	}
}