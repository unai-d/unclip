using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Gtk;
using static Gtk.Builder;

namespace Unai.Unclip
{
	class MainWindow : Window
	{
		[Object] private FileChooserButton uiInputFileChooser;
		[Object] private FileChooserButton uiOutputDirectoryFileChooser;
		[Object] private Button uiStartButton;
		[Object] private TreeView uiLayers;
		[Object] private Image uiLayerPreview;
		[Object] private TextView uiLogOutput;
		[Object] private ScrolledWindow uiLogOutputScroll;

		TextMark logOutputEnd;
		TreeStore layerTree = new(typeof(string));
		Dictionary<TreePath, int> layerRowToLayerIdTable = new();
		Dictionary<int, Gdk.Pixbuf> cachedLayerPixelData = new();

		CspFile cspFile = null;

		public MainWindow() : this(new Builder("MainWindow.glade"))
		{

		}

		private MainWindow(Builder builder) : base(builder.GetRawOwnedObject("MainWindow"))
		{
			builder.Autoconnect(this);

			DeleteEvent += Window_DeleteEvent;

			Logger.OnLog += (text, type) =>
			{
				GLib.GSourceFunc t = () =>
				{
					TextIter endIter = uiLogOutput.Buffer.EndIter;
					uiLogOutput.Buffer.Insert(ref endIter, text + "\n");

					// Scroll does not work properly without this.
					if (Environment.OSVersion.Platform != PlatformID.Win32NT) // This throws a StackOverflowException on Windows.
					{
						while (Gtk.Application.EventsPending())
						{
							Gtk.Application.RunIteration();
						}
					}
					
					uiLogOutput.ScrollToMark(logOutputEnd, 0.0, true, 0.5, 1.0);
					return false;
				};
				Gdk.Threads.AddIdle(200, t);
			};

			logOutputEnd = uiLogOutput.Buffer.CreateMark("end", uiLogOutput.Buffer.EndIter, false);

			// Initialize layer tree view.

			uiLayers.Model = layerTree;
			uiLayers.AppendColumn(new TreeViewColumn() { Title = "Layers" });

			var textRend = new CellRendererText();
			uiLayers.Columns[0].PackStart(textRend, true);
			uiLayers.Columns[0].AddAttribute(textRend, "markup", 0);
		}

		private void Window_DeleteEvent(object sender, DeleteEventArgs a)
		{
			Gtk.Application.Quit();
		}

		private void UpdateOutputDirectory(object sender, EventArgs e)
		{

		}

		private void UpdateInputFile(object sender, EventArgs e)
		{
			string path = uiInputFileChooser.File.Path;
			Logger.Log($"Loading input file '{path}'…");
			
			try
			{
				cspFile = new CspFile(path);
			}
			catch (Exception ex)
			{
				string errorMessage = "Cannot load CSP file: " + ex.Message;

				Gtk.MessageDialog errorDialog = new MessageDialog(this, DialogFlags.Modal, MessageType.Error, ButtonsType.Ok, false, null);
				errorDialog.Text = errorMessage;
				errorDialog.SecondaryText = ex.StackTrace ?? "No stack trace available.";
				errorDialog.Response += (s, e) => errorDialog.Hide();
				errorDialog.Show();
			}

			cachedLayerPixelData.Clear();
			UpdateLayerTreeView();
		}

		private void UpdateLayerTreeView()
		{
			layerTree.Clear();

			if (cspFile == null || cspFile.Layers == null) return;

			Dictionary<int, TreeIter> canvasTreeIters = new();
			foreach (var layer in cspFile.Layers)
			{
				int canvasId = layer.Value.CanvasId;
				int layerId = layer.Value.Id;
				string layerName = layer.Value.Name;

				if (!canvasTreeIters.ContainsKey(canvasId))
				{
					TreeIter canvasTreeIter = layerTree.AppendValues($"Canvas {canvasId}");
					canvasTreeIters.Add(canvasId, canvasTreeIter);
				}

				TreeIter layerTreeIter = layerTree.AppendValues(canvasTreeIters[canvasId], string.IsNullOrEmpty(layerName) ? $"<i>Layer {layerId}</i>" : layerName);
				TreePath layerTreePath = layerTree.GetPath(layerTreeIter);
				layerRowToLayerIdTable[layerTreePath] = layer.Key;
			}
		}

		private void StartConversion(object sender, EventArgs e)
		{
			Gtk.MessageDialog infoDialog = new MessageDialog(this, DialogFlags.Modal, MessageType.Info, ButtonsType.Ok, false, null);
			infoDialog.Response += (s, e) => infoDialog.Hide();

			if (cspFile == null)
			{
				infoDialog.Text = "Please, choose the CSP project file that you want to extract first.";
				infoDialog.Show();
				return;
			}

			if (uiOutputDirectoryFileChooser.File == null)
			{
				infoDialog.Text = "Please, choose the folder where the layers contained in the CSP file will be extracted at.";
				infoDialog.Show();
				return;
			}

			Task.Run(ConversionThread);
		}

		private void ConversionThread()
		{
			ProgressBar pb = new ProgressBar();

			Gtk.MessageDialog infoDialog = new MessageDialog(this, DialogFlags.Modal, MessageType.Info, ButtonsType.None, false, null);
			infoDialog.Text = "Hold on…";
			infoDialog.ContentArea.PackStart(pb, true, true, 0);
			infoDialog.ShowAll();
			infoDialog.Show();

			var logAndShow = (string text, LogType type) =>
			{
				Logger.Log(text, type);
				infoDialog.Text = text;
			};

			string outputDirectory = System.IO.Path.Combine(System.IO.Path.GetFullPath(uiOutputDirectoryFileChooser.File.Path), cspFile.FileId);

			logAndShow($"Creating folder at '{outputDirectory}'…", LogType.Info);

			Directory.CreateDirectory(outputDirectory);
			
			int progress = 0;
			foreach (var canvasPreview in cspFile.CanvasPreviews)
			{
				logAndShow($"Extracting canvas preview #{canvasPreview.Value.CanvasId}…", LogType.Info);
				pb.Fraction = progress / (float)cspFile.CanvasPreviews.Count;

				string canvasPreviewRasterOutputFilePath = $"{outputDirectory}/canvas_{canvasPreview.Value.CanvasId:D4}.png";
				File.WriteAllBytes(canvasPreviewRasterOutputFilePath, canvasPreview.Value.ImageData);

				progress++;
			}

			progress = 0;
			foreach (var layer in cspFile.Layers)
			{
				logAndShow($"Extracting canvas #{layer.Value.CanvasId}, layer #{layer.Key} '{layer.Value.Name}'…", LogType.Info);
				pb.Fraction = progress / (float)cspFile.Layers.Count;

				try
				{
					Bitmap layerRaster = cspFile.GetLayerRasterData(layer.Value.CanvasId, layer.Key);

					string layerRasterOutputFilePath = $"{outputDirectory}/canvas_{layer.Value.CanvasId:D4}/layer_{layer.Key:D4}_[{layer.Value.Name}].png";
					Directory.CreateDirectory(System.IO.Path.GetDirectoryName(layerRasterOutputFilePath));
					layerRaster.SaveToFile(layerRasterOutputFilePath);
				}
				catch (Exception ex)
				{
					Logger.Log($"Cannot export layer #{layer.Key}:\n{ex.Message}", LogType.Error);
				}

				progress++;
			}

			infoDialog.Hide();
		}

		private void UpdateLayerPreview(object sender, RowActivatedArgs e)
		{
			if (e.Path.Depth != 2)
			{
				return;
			}

			var layerMainId = layerRowToLayerIdTable[e.Path];

			Gdk.Pixbuf pixbuf = cachedLayerPixelData.GetValueOrDefault(layerMainId);

			if (pixbuf == null)
			{
				var layer = cspFile.Layers[layerMainId];

				Bitmap layerPreview = null;
				try
				{
					layerPreview = cspFile.GetLayerRasterData(layer.CanvasId, layer.Id);
				}
				catch (Exception ex)
				{
					Logger.Log(ex.Message, LogType.Error);
					Gtk.MessageDialog errorDialog = new MessageDialog(this, DialogFlags.Modal, MessageType.Error, ButtonsType.Ok, false, null);
					errorDialog.Text = $"Cannot get pixel data from layer #{layerMainId}";
					errorDialog.SecondaryText = ex.Message + "\n\n" + (ex.StackTrace ?? "No stack trace available.");
					errorDialog.Response += (s, e) => errorDialog.Hide();
					errorDialog.Show();
					return;
				}

				if (layerPreview != null)
				{
					pixbuf = new Gdk.Pixbuf(Utils.BgraToRgba(layerPreview.Pixels), Gdk.Colorspace.Rgb, true, 8, (int)layerPreview.Width, (int)layerPreview.Height, (int)layerPreview.InternalWidth * 4);
					cachedLayerPixelData.Add(layerMainId, pixbuf);
				}
				else
				{
					Gtk.MessageDialog errorDialog = new MessageDialog(this, DialogFlags.Modal, MessageType.Error, ButtonsType.Ok, false, null);
					errorDialog.Text = $"Layer #{layerMainId} returned a null pixel buffer.";
					errorDialog.Response += (s, e) => errorDialog.Hide();
					errorDialog.Show();
				}
			}

			if (pixbuf != null) 
			{
				uiLayerPreview.Pixbuf = pixbuf;
			}
		}
	}
}
