using System.IO;
using System.Linq;

namespace Unai.Unclip
{
	public static class Utils
	{
		public static byte[] ToByteArray(this Stream stream)
		{
			byte[] ret;
			using (MemoryStream ms = new MemoryStream())
			{
				stream.CopyTo(ms);
				ret = ms.ToArray();
			}
			return ret;
		}

		public static long ReadInt64BE(this BinaryReader br)
		{
			return System.Buffers.Binary.BinaryPrimitives.ReverseEndianness(br.ReadInt64());
		}

		public static int ReadInt32BE(this BinaryReader br)
		{
			return System.Buffers.Binary.BinaryPrimitives.ReverseEndianness(br.ReadInt32());
		}

		public static uint ReadUInt32BE(this BinaryReader br)
		{
			return System.Buffers.Binary.BinaryPrimitives.ReverseEndianness(br.ReadUInt32());
		}

		public static string ByteArrayToHexDump(byte[] input, int count = 0)
		{
			if (count < 1) count = input.Length;
			return string.Join(' ', input.Take(count).Select(x => x.ToString("X2")));
		}

		public static string ChangeExtension(this string path, string extension)
		{
			return path.Substring(0, path.LastIndexOf(Path.GetExtension(path))) + extension;
		}
	}
}