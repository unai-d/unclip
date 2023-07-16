using System;
using System.IO;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace Unai.Unclip
{
	public enum PixelFormat
	{
		Unknown,
		Grayscale8,
		B8G8R8A8
	}

	public class Bitmap
	{
		byte[] pixels;
		uint width;
		uint height;
		uint internalWidth;
		uint internalHeight;
		PixelFormat pixelFormat;

		public byte[] Pixels => pixels;
		public uint Width => width;
		public uint Height => height;
		public uint InternalWidth => internalWidth;
		public uint InternalHeight => internalHeight;

		public Bitmap(byte[] pixels, uint width, uint height, uint internalWidth, uint internalHeight, PixelFormat pixelFormat)
		{
			this.pixels = pixels;
			this.width = width;
			this.height = height;
			this.internalWidth = internalWidth;
			this.internalHeight = internalHeight;
			this.pixelFormat = pixelFormat;
		}

		private Image GetImage()
		{
			Image img = null;
			switch (pixelFormat)
			{
				case PixelFormat.B8G8R8A8:
					img = Image.LoadPixelData<Bgra32>(pixels, (int)internalWidth, (int)internalHeight);
					break;
				case PixelFormat.Grayscale8:
					img = Image.LoadPixelData<L8>(pixels, (int)internalWidth, (int)internalHeight);
					break;
				default:
					throw new Exception($"Unsupported pixel format: `{pixelFormat}`.");
			}
			img.Mutate(i => i.Crop(new Rectangle(0, 0, (int)width, (int)height)));
			return img;
		}

		public void SaveToStream(Stream stream)
		{
			using Image img = GetImage();
			img.SaveAsPng(stream);
		}

		public void SaveToFile(string path)
		{
			using Image img = GetImage();
			img.Save(path);
		}
	}
}