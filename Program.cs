using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;

namespace Unai.Unclip
{
	class Program
	{
		public static void Main(string[] args)
		{
			CspFile cspFile = new CspFile(args[0]);

			Logger.Log("CSP file overview:");
			foreach (var layer in cspFile.Layers)
			{
				Logger.Log($"  Layer #{layer.Key} '{layer.Value.Name}':");
				Logger.Log($"    Render mipmap ID: {layer.Value.MipmapId}.");
			}

			string outputDirectory = Path.GetFullPath(cspFile.FileId);

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