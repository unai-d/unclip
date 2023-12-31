using System.IO;
using System.Text;
using System.Data.SQLite;
using System.IO.Compression;
using System;
using System.Linq;
using System.Collections.Generic;

namespace Unai.Unclip
{
	/// <summary>
	/// Represents a Clip Studio Paint (CSP) file.
	/// </summary>
	public class CspFile : IDisposable
	{
		/// <summary>
		/// Represents the chunk of a <see cref="CspFile" />. A CSP file is composed of these, in a sequential layout.
		/// </summary>
		public struct CspChunk
		{
			public Tuple<long, long> Position;
			public string Type = null;
			public long Size = -1;

			public CspChunk(long startPos, long endPos, string type, long size)
			{
				Position = Tuple.Create(startPos, endPos);
				Type = type;
				Size = size;
			}
		}

		public struct CspLayer
		{
			public int Id = 0;
			public int CanvasId = 0;
			public string Name = null;
			public int MipmapId = 0;
			public int ThumbnailId = 0;

			public CspLayer(int id, int canvasId, string name, int mipmapId, int thumbnailId)
			{
				Id = id;
				CanvasId = canvasId;
				Name = name;
				MipmapId = mipmapId;
				ThumbnailId = thumbnailId;
			}
		}

		public struct CspLayerThumbnail
		{
			public int Id = 0;
			public int CanvasId = 0;
			public int LayerId = 0;
			public int Width = 0;
			public int Height = 0;
			public int OffscreenId = 0;

			public CspLayerThumbnail(int id, int canvasId, int layerId, int width, int height, int offscreenId)
			{
				Id = id;
				CanvasId = canvasId;
				LayerId = layerId;
				Width = width;
				Height = height;
				OffscreenId = offscreenId;
			}
		}

		public struct CspOffscreen
		{
			public int Id = 0;
			public int CanvasId = 0;
			public int LayerId = 0;
			public string ExternalDataId = null;

			public CspOffscreen(int id, int canvasId, int layerId, string externalDataId)
			{
				Id = id;
				CanvasId = canvasId;
				LayerId = layerId;
				ExternalDataId = externalDataId;
			}
		}

		public struct CspMipmap
		{
			public int Id = 0;
			public int CanvasId = 0;
			public int LayerId = 0;
			public int MipmapCount = 0;
			public int BaseMipmapInfoId = 0;

			public CspMipmap(int id, int canvasId, int layerId, int mipmapCount, int baseMipmapInfoId)
			{
				Id = id;
				CanvasId = canvasId;
				LayerId = layerId;
				MipmapCount = mipmapCount;
				BaseMipmapInfoId = baseMipmapInfoId;
			}
		}

		public struct CspMipmapInfo
		{
			public int Id = 0;
			public int CanvasId = 0;
			public int LayerId = 0;
			public double Scale = 0;
			public int OffscreenId = 0;
			public int NextId = 0;

			public CspMipmapInfo(int id, int canvasId, int layerId, double scale, int offscreenId, int nextId)
			{
				Id = id;
				CanvasId = canvasId;
				LayerId = layerId;
				Scale = scale;
				OffscreenId = offscreenId;
				NextId = nextId;
			}
		}

		public struct CspCanvasPreview
		{
			public int Id = 0;
			public int CanvasId = 0;
			public byte[] ImageData = null;
			public int ImageWidth = 0;
			public int ImageHeight = 0;

			public CspCanvasPreview(int id, int canvasId, byte[] imageData, int imageWidth, int imageHeight)
			{
				Id = id;
				CanvasId = canvasId;
				ImageData = imageData;
				ImageWidth = imageWidth;
				ImageHeight = imageHeight;
			}
		}

		Stream stream = null;
		BinaryReader br = null;
		string streamFilePath = null;
		Guid guid = Guid.NewGuid();

		List<CspChunk> chunks = new List<CspChunk>();
		Dictionary<string, Stream> externalData = new Dictionary<string, Stream>();

		Dictionary<int, CspLayer> layers = new Dictionary<int, CspLayer>();
		Dictionary<int, CspLayerThumbnail> thumbnails = new Dictionary<int, CspLayerThumbnail>();
		Dictionary<int, CspOffscreen> offscreens = new Dictionary<int, CspOffscreen>();
		Dictionary<int, CspMipmap> mipmaps = new Dictionary<int, CspMipmap>();
		Dictionary<int, CspMipmapInfo> mipmapInfos = new Dictionary<int, CspMipmapInfo>();
		Dictionary<int, CspCanvasPreview> canvasPreviews = new Dictionary<int, CspCanvasPreview>();

		public string FileId => string.IsNullOrEmpty(streamFilePath) ? guid.ToString("N") : Path.GetFileNameWithoutExtension(streamFilePath.Replace(Path.PathSeparator, '_'));
		public Dictionary<int, CspLayer> Layers => layers;
		public Dictionary<int, CspLayerThumbnail> Thumbnails => thumbnails;
		public Dictionary<int, CspOffscreen> Offscreens => offscreens;
		public Dictionary<int, CspMipmap> Mipmaps => mipmaps;
		public Dictionary<int, CspMipmapInfo> MipmapInfos => mipmapInfos;
		public Dictionary<int, CspCanvasPreview> CanvasPreviews => canvasPreviews;

		public CspFile(Stream cspStream)
		{
			stream = cspStream;
			ReadFromStream();
		}

		public CspFile(string filePath)
		{
			streamFilePath = Path.GetFullPath(filePath, Environment.CurrentDirectory);
			stream = File.OpenRead(streamFilePath);
			ReadFromStream();
		}

		public void ReadFromStream()
		{
			br = new BinaryReader(stream);

			string cspMagicNumber = Encoding.ASCII.GetString(br.ReadBytes(8)); // Read "CSFCHUNK" at address 0.
			stream.Seek(16, SeekOrigin.Current); // Jump 16 bytes forward.
			
			// Read all chunks.
			while (stream.Position < stream.Length)
			{
				long chunkStartPos = stream.Position;
				string chunkType = Encoding.ASCII.GetString(br.ReadBytes(8));
				long chunkDataSize = br.ReadInt64BE();
				Logger.Log($"Chunk at 0x{chunkStartPos:X8}: `{chunkType}`, {chunkDataSize} bytes.", LogType.Debug);
				long chunkEndPos = chunkStartPos + chunkDataSize + 16;

				chunks.Add(new CspChunk(chunkStartPos, chunkEndPos, chunkType, chunkDataSize));
				
				stream.Seek(chunkEndPos, SeekOrigin.Begin);
			}

			// Process SQLite chunk.
			CspChunk sqliteChunk = chunks.FirstOrDefault(c => c.Type == "CHNKSQLi");
			if (sqliteChunk.Type != "CHNKSQLi")
			{
				throw new InvalidDataException($"Cannot find SQLite chunk.");
			}

			stream.Seek(sqliteChunk.Position.Item1 + 16, SeekOrigin.Begin);

			string sqliteChunkFile = Path.Combine(Path.GetTempPath(), $"{FileId}.sqlite3");
			Logger.Log($"Saving CSP file SQLite chunk to file '{sqliteChunkFile}'.", LogType.Debug);
			byte[] sqliteData = br.ReadBytes((int)sqliteChunk.Size);
			File.WriteAllBytes(sqliteChunkFile, sqliteData);

			Logger.Log("Reading CSP file metadata from SQLite chunk…");
			using (var sqliteCon = new SQLiteConnection($"Data Source={sqliteChunkFile}"))
			{
				sqliteCon.Open();

				var sqliteCmd = sqliteCon.CreateCommand();

				sqliteCmd.CommandText = "select MainId, CanvasId, ImageData, ImageWidth, ImageHeight from CanvasPreview;";

				using (var reader = sqliteCmd.ExecuteReader())
				{
					while (reader.Read())
					{
						int canvasPreviewId = reader.GetInt32(0);
						int canvasId = reader.GetInt32(1);
						var imageData = reader.GetStream(2).ToByteArray();
						var imageWidth = reader.GetInt32(3);
						var imageHeight = reader.GetInt32(4);

						Logger.Log($"  Canvas preview: {imageData.Length} bytes, {imageWidth}×{imageHeight}.", LogType.Debug);
						canvasPreviews.Add(canvasPreviewId, new CspCanvasPreview(canvasPreviewId, canvasId, imageData, imageWidth, imageHeight));
					}
				}

				sqliteCmd.CommandText = "select MainId, CanvasId, LayerName, LayerUuid, LayerRenderMipmap, LayerRenderThumbnail from Layer;";

				using (var reader = sqliteCmd.ExecuteReader())
				{
					while (reader.Read())
					{
						int layerId = reader.GetInt32(0);
						int canvasId = reader.GetInt32(1);
						string layerName = reader.GetString(2);
						string layerUuid = reader.GetString(3);
						int layerRenderMipmap = reader.GetInt32(4);
						int layerRenderThumbnail = reader.GetInt32(5);

						Logger.Log($"  Layer #{layerId}: canvas #{canvasId}, mipmap #{layerRenderMipmap}, thumbnail #{layerRenderThumbnail}, '{layerName}'.", LogType.Debug);
						layers.Add(layerId, new CspLayer(layerId, canvasId, layerName, layerRenderMipmap, layerRenderThumbnail));
					}
				}

				sqliteCmd.CommandText = "select MainId, CanvasId, LayerId, ThumbnailCanvasWidth, ThumbnailCanvasHeight, ThumbnailOffscreen from LayerThumbnail;";

				using (var reader = sqliteCmd.ExecuteReader())
				{
					while (reader.Read())
					{
						int layerThumbnailId = reader.GetInt32(0);
						int canvasId = reader.GetInt32(1);
						int layerId = reader.GetInt32(2);
						int thumbnailWidth = reader.GetInt32(3);
						int thumbnailHeight = reader.GetInt32(4);
						int offscreenId = reader.GetInt32(5);

						Logger.Log($"  Layer thumbnail #{layerThumbnailId}: canvas #{canvasId}, layer #{layerId}, offscreen #{offscreenId}, {thumbnailWidth}×{thumbnailHeight}.", LogType.Debug);
						thumbnails.Add(layerThumbnailId, new CspLayerThumbnail(layerThumbnailId, canvasId, layerId, thumbnailWidth, thumbnailHeight, offscreenId));
					}
				}

				sqliteCmd.CommandText = "select MainId, CanvasId, LayerId, BlockData from Offscreen;";

				using (var reader = sqliteCmd.ExecuteReader())
				{
					while (reader.Read())
					{
						int offscreenId = reader.GetInt32(0);
						int canvasId = reader.GetInt32(1);
						int layerId = reader.GetInt32(2);
						string externalDataId = reader.GetString(3);

						Logger.Log($"  Offscreen #{offscreenId}: canvas #{canvasId}, layer #{layerId}, '{externalDataId}'.", LogType.Debug);
						offscreens.Add(offscreenId, new CspOffscreen(offscreenId, canvasId, layerId, externalDataId));
					}
				}

				sqliteCmd.CommandText = "select MainId, CanvasId, LayerId, MipmapCount, BaseMipmapInfo from Mipmap;";

				using (var reader = sqliteCmd.ExecuteReader())
				{
					while (reader.Read())
					{
						int mipmapId = reader.GetInt32(0);
						int canvasId = reader.GetInt32(1);
						int layerId = reader.GetInt32(2);
						int mipmapCount = reader.GetInt32(3);
						int baseMipmapInfoId = reader.GetInt32(4);

						mipmaps[mipmapId] = new CspMipmap(mipmapId, canvasId, layerId, mipmapCount, baseMipmapInfoId);
					}
				}

				sqliteCmd.CommandText = "select MainId, CanvasId, LayerId, ThisScale, Offscreen, NextIndex from MipmapInfo;";

				using (var reader = sqliteCmd.ExecuteReader())
				{
					while (reader.Read())
					{
						int mipmapInfoId = reader.GetInt32(0);
						int canvasId = reader.GetInt32(1);
						int layerId = reader.GetInt32(2);
						double scale = reader.GetDouble(3);
						int offscreenId = reader.GetInt32(4);
						int nextId = reader.GetInt32(5);

						mipmapInfos[mipmapInfoId] = new CspMipmapInfo(mipmapInfoId, canvasId, layerId, scale, offscreenId, nextId);
					}
				}

				sqliteCon.Close();
			}
		}

		public void Dispose()
		{
			br.Dispose();
			stream.Dispose();
		}

		public Stream GetLayerExternalData(int canvasId, int layerId)
		{
			var offscreen = offscreens.FirstOrDefault(os => os.Value.CanvasId == canvasId && os.Value.LayerId == layerId);
			var externalDataId = offscreen.Value.ExternalDataId;

			if (string.IsNullOrEmpty(externalDataId))
			{
				throw new InvalidDataException($"Layer {layerId} of canvas {canvasId} has no associated external data chunk.");
			}

			if (!externalData.ContainsKey(externalDataId))
			{
				foreach (var externalDataChunk in chunks.Where(c => c.Type == "CHNKExta" && GetExternalDataId(c) == externalDataId))
				{
					ParseExternalData(externalDataChunk);
				}
			}

			return externalData[externalDataId];
		}

		private string GetExternalDataId(CspChunk chunk)
		{
			if (chunk.Type != "CHNKExta") return null;

			stream.Seek(chunk.Position.Item1 + 16, SeekOrigin.Begin);
			long stringLength = br.ReadInt64BE();
			return Encoding.UTF8.GetString(br.ReadBytes((int)stringLength));
		}

		private void ParseExternalData(CspChunk chunk)
		{
			if (chunk.Type != "CHNKExta") return;

			stream.Seek(chunk.Position.Item1 + 16, SeekOrigin.Begin);

			long externalDataIdLen = br.ReadInt64BE();
			externalDataIdLen = Math.Clamp(externalDataIdLen, 0, 40);
			string externalDataId = Encoding.UTF8.GetString(br.ReadBytes((int)externalDataIdLen));
			long externalDataSize = br.ReadInt64BE();
			Logger.Log($"Reading external data chunk '{externalDataId}'…");
			Logger.Log($"  {externalDataSize} bytes.", LogType.Debug);

			MemoryStream externalDataMs = new MemoryStream();
			externalDataMs.SetLength((int)externalDataSize);

			while (stream.Position < chunk.Position.Item2)
			{
				long blockStartPos = stream.Position;
				long blockEndPos;

				uint blockNameLen = 0;
				uint blockDataLen = 0;

				// Read the first two integers.
				uint aSize = br.ReadUInt32BE();
				uint bSize = br.ReadUInt32BE();
				if (bSize == 0x0042006C /*"Bl"*/)
				{
					blockNameLen = aSize;
					stream.Seek(blockStartPos + sizeof(int), SeekOrigin.Begin); // Go back 4 bytes.
				}
				else
				{
					blockNameLen = bSize;
					blockDataLen = aSize;
				}

				string blockName = "<toobig>";
				if (blockNameLen < 256)
				{
					blockName = Encoding.BigEndianUnicode.GetString(br.ReadBytes((int)blockNameLen * 2));
				}
				else
				{
					stream.Seek(blockNameLen * 2, SeekOrigin.Current);
				}

				long blockDataStartPos = stream.Position;
				blockEndPos = blockDataStartPos + blockDataLen;

				Logger.Log($"  Data block at 0x{blockStartPos:X8}: A={aSize:X8} B={bSize:X8} '{blockName}', {blockDataLen} bytes.", LogType.Debug);

				switch (blockName)
				{
					case "BlockDataBeginChunk":
						int blockIndex = br.ReadInt32BE();
						int blockUncompressedSize = br.ReadInt32BE();
						int blockWidth = br.ReadInt32BE();
						int blockHeight = br.ReadInt32BE();
						int notEmpty = br.ReadInt32BE();
						Logger.Log($"    {blockUncompressedSize} bytes (uncompressed), {blockWidth}×{blockHeight}.", LogType.Debug);

						if (notEmpty > 0)
						{
							int blockLen = br.ReadInt32BE();
							uint blockDataLen2 = br.ReadUInt32();
							Logger.Log($"    Index {blockIndex}, {blockLen}/{blockDataLen2} bytes.", LogType.Debug);

							if (blockDataLen2 < blockLen - 4)
							{
								Logger.Log($"      Skipping more than 4 bytes in this block (that shouldn't happen).", LogType.Warning);
							}

							byte[] blockDataZlib = br.ReadBytes((int)blockDataLen2);

							byte[] blockData;
							using (var blockDataZlibStream = new ZLibStream(new MemoryStream(blockDataZlib), CompressionMode.Decompress))
							{
								blockData = blockDataZlibStream.ToByteArray();
								externalDataMs.Write(blockData); // TODO: Take `blockIndex` into account.
							}
							
							if (blockData.Length != blockUncompressedSize)
							{
								Logger.Log($"    Uncompressed size mismatch: expected {blockUncompressedSize}, got {blockData.Length}.", LogType.Warning);
							}

							blockEndPos = blockDataStartPos + 24 + blockLen;
						}
						else
						{
							Logger.Log($"    Index {blockIndex}, empty.", LogType.Debug);

							externalDataMs.Write(new byte[blockUncompressedSize]);

							blockEndPos = blockDataStartPos + 20;
						}
						break;

					case "BlockStatus":
					case "BlockCheckSum":
						int i0 = br.ReadInt32BE();
						blockUncompressedSize = br.ReadInt32BE();
						blockWidth = br.ReadInt32BE();
						blockHeight = br.ReadInt32BE();
						int i4 = br.ReadInt32BE(); // TODO: Maybe this field is CRC32 when block is checksum?
						int i5 = br.ReadInt32BE();
						Logger.Log($"    I0={i0:X8}/{i0} I1={blockUncompressedSize:X8} I2={blockWidth:X8} I3={blockHeight:X8} I4={i4:X8} I5={i5:X8}", LogType.Debug);

						blockEndPos = blockDataStartPos + 24 + blockDataLen;
						break;
				}

				stream.Seek(blockEndPos, SeekOrigin.Begin);
			}

			// Save external data to memory.
			externalData.Add(externalDataId, externalDataMs);
		}

		public Bitmap GetLayerRasterData(int canvasId, int layerId)
		{
			Logger.Log($"Loading pixel data from layer #{layerId} at canvas #{canvasId}…");

			var layer = layers.FirstOrDefault(l => l.Key == layerId && l.Value.CanvasId == canvasId);
			var thumbnail = thumbnails.FirstOrDefault(tn => tn.Key == layer.Value.ThumbnailId);
			var mipmap = mipmaps.FirstOrDefault(mm => mm.Key == layer.Value.MipmapId);
			var mipmapInfo = mipmapInfos.FirstOrDefault(mmi => mmi.Key == mipmap.Value.BaseMipmapInfoId);
			var offscreen = offscreens.FirstOrDefault(os => os.Key == mipmapInfo.Value.OffscreenId);
			var externalDataId = offscreen.Value.ExternalDataId;

			int pixelSize = 4; // BGR0
			int bgrRowSize = 256 * pixelSize;
			int bgrCompositeBlockSize = 256 * 320 * pixelSize; // 0x050000 (327680)
			int blockSize = 256 * 256; // 64 KiB

			int imageWidth = thumbnail.Value.Width;
			int imageHeight = thumbnail.Value.Height;

			int blocksPerRow = ((imageWidth + 255) / 256);
			int blocksPerColumn = ((imageHeight + 255) / 256);
			int paddedWidth = blocksPerRow * 256;
			int paddedHeight = blocksPerColumn * 256;

			Stream externalDataStream = GetLayerExternalData(canvasId, layerId);
			externalDataStream.Seek(0, SeekOrigin.Begin);
			byte[] externalDataArray = externalDataStream.ToByteArray();

			byte[] output = null;
			PixelFormat outputPixelFormat = PixelFormat.Unknown;

            Logger.Log($"  Image is {imageWidth}×{imageHeight}, padded is {paddedWidth}×{paddedHeight} ({paddedWidth * paddedHeight} total pixels).", LogType.Debug);

			bool grayscaleMode = false;
			
			// TODO: Get pixel format from layer struct.
			int grayscaleExpectedSize = paddedWidth * paddedHeight;
			int bgrExpectedSize = paddedWidth * paddedHeight * (pixelSize + 1);

			if (externalDataArray.Length == grayscaleExpectedSize)
			{
				grayscaleMode = true;
			}
			else if (externalDataArray.Length != bgrExpectedSize)
			{
				Logger.Log($"  Cannot infer external data chunk pixel format from byte count.", LogType.Warning);

				bool guessed = false;

				for (int y = blocksPerColumn; y < blocksPerColumn * 3; y++)
				{
					for (int x = blocksPerRow; x < blocksPerRow * 3; x++)
					{
						int guessedWidth = x * 256;
						int guessedHeight = y * 256;

						// TODO: Add grayscale support.
						if (externalDataArray.Length == guessedWidth * guessedHeight * (pixelSize + 1))
						{
							int paddedWidthDiff = (paddedWidth - imageWidth);
							int paddedHeightDiff = (paddedHeight - imageHeight);
							paddedWidth = guessedWidth;
							paddedHeight = guessedHeight;
							imageWidth = paddedWidth - paddedWidthDiff;
							imageHeight = paddedHeight - paddedHeightDiff;
							blocksPerRow = x;
							Logger.Log($"  Guessed resolution: {imageWidth}×{imageHeight}, padded {paddedWidth}×{paddedHeight}.", LogType.Debug);
							guessed = true;
							break;
						}
					}

					if (guessed) break;
				}

				if (!guessed)
				{
					Logger.Log("  Cannot infer external data chunk pixel format from block count.", LogType.Error);
				}
			}

			if (grayscaleMode)
			{
				Logger.Log($"  Guessed format: 100% scale grayscale.", LogType.Debug);

				int blockCount = (int)externalDataArray.Length / blockSize;

				output = new byte[paddedWidth * paddedHeight];
				outputPixelFormat = PixelFormat.Grayscale8;

				for (int blockIdx = 0; blockIdx < blockCount; blockIdx++)
				{
					byte[] block = new byte[blockSize];
					int blockAddr = (blockIdx * blockSize);
					Array.Copy(externalDataArray, blockAddr, block, 0, block.Length);

					int blockX = blockIdx % blocksPerRow;
					int blockY = blockIdx / blocksPerRow;

					for (int blockRow = 0; blockRow < 256; blockRow++)
					{
						int targetPtr =
							(blockX * 256) +
							(blockY * blockSize * blocksPerRow) +
							(blockRow * 256 * blocksPerRow);

						Array.Copy(block, blockRow * 256, output, targetPtr, 256);
					}
				}
			}
			else
			{
				Logger.Log($"  Guessed format: 100% scale alpha+BGR0.", LogType.Debug);

				output = new byte[paddedWidth * paddedHeight * pixelSize];
				outputPixelFormat = PixelFormat.B8G8R8A8;

				int blockCount = (int)externalDataArray.Length / bgrCompositeBlockSize;

				for (int blockIdx = 0; blockIdx < blockCount; blockIdx++)
				{
					int blockAddr = (blockIdx * bgrCompositeBlockSize);

					byte[] block = new byte[bgrCompositeBlockSize];
					Array.Copy(externalDataArray, blockAddr, block, 0, block.Length);

					int blockX = blockIdx % blocksPerRow;
					int blockY = blockIdx / blocksPerRow;

					byte[] alphaBlock = new byte[blockSize];
					Array.Copy(block, 0, alphaBlock, 0, alphaBlock.Length);

					byte[] bgrBlock = new byte[blockSize * pixelSize];
					Array.Copy(block, blockSize, bgrBlock, 0, bgrBlock.Length);

					for (int i = 0; i < alphaBlock.Length; i++)
					{
						int alphaBlockColumn = i % 256;
						int alphaBlockRow = i / 256;

						int targetPtr =
							(blockY * blockSize * blocksPerRow * pixelSize) +
							(blockX * bgrRowSize) +
							(alphaBlockRow * 256 * blocksPerRow * pixelSize) +
							(alphaBlockColumn * pixelSize) + (pixelSize - 1);
						
						if (targetPtr >= 0 && targetPtr < output.Length)
						{
							output[targetPtr] = alphaBlock[i];
						}
						else
						{
							Logger.Log($"OOB Blk{blockIdx}(Y{blockY},X{blockX}) Alpha Row{alphaBlockRow},Col{alphaBlockColumn} → {targetPtr}/{output.Length}", LogType.Error);
							break;
						}
					}

					for (int i = 0; i < bgrBlock.Length; i++)
					{
						int componentIndex = i % pixelSize;
						int pixelIndex = i / pixelSize;

						if (componentIndex == 3) continue; // Ignore fourth channel. Maybe used in CMYK color spaces?

						int blockColumn = pixelIndex % 256;
						int blockRow = pixelIndex / 256;

						int targetPtr =
							(blockY * blockSize * blocksPerRow * pixelSize) +
							(blockX * bgrRowSize) +
							(blockRow * 256 * blocksPerRow * pixelSize) +
							(blockColumn * pixelSize) + componentIndex;

						if (targetPtr >= 0 && targetPtr < output.Length)
						{
							output[targetPtr] = bgrBlock[i];
						}
						else
						{
							Logger.Log($"OOB Blk{blockIdx}(Y{blockY},X{blockX}) BGR0 Row{blockRow},Col{blockColumn} → {targetPtr}/{output.Length}", LogType.Error);
							break;
						}
					}
				}
			}

			return output != null ? new Bitmap(output, (uint)imageWidth, (uint)imageHeight, (uint)paddedWidth, (uint)paddedHeight, outputPixelFormat) : null;
		}
	}
}