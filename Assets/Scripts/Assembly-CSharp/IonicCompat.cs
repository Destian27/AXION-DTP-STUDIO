// Pengganti Ionic.Zlib.CF.dll (dikompilasi untuk mscorlib 2.0 Compact Framework,
// tidak bisa dipakai di Unity 6 -> error CS0012). API yang dipakai SaveProvider dipertahankan.
using System;
using System.IO;
using SysCompression = System.IO.Compression;

namespace Ionic.Crc
{
	public class CRC32
	{
		private static readonly uint[] table = BuildTable();
		private uint register = 0xFFFFFFFFu;

		public long TotalBytesRead { get; private set; }

		public int Crc32Result
		{
			get { return unchecked((int)(~register)); }
		}

		private static uint[] BuildTable()
		{
			uint[] t = new uint[256];
			for (uint i = 0; i < 256; i++)
			{
				uint c = i;
				for (int k = 0; k < 8; k++)
					c = (c & 1u) != 0 ? (0xEDB88320u ^ (c >> 1)) : (c >> 1);
				t[i] = c;
			}
			return t;
		}

		public void Reset()
		{
			register = 0xFFFFFFFFu;
			TotalBytesRead = 0;
		}

		public void SlurpBlock(byte[] block, int offset, int count)
		{
			if (block == null) throw new ArgumentNullException("block");
			for (int i = 0; i < count; i++)
				register = (register >> 8) ^ table[(register ^ block[offset + i]) & 0xFFu];
			TotalBytesRead += count;
		}
	}
}

namespace Ionic.Zlib
{
	public enum CompressionMode
	{
		Compress = 0,
		Decompress = 1
	}

	public class GZipStream : Stream
	{
		private readonly SysCompression.GZipStream inner;

		public GZipStream(Stream stream, CompressionMode mode)
		{
			inner = new SysCompression.GZipStream(stream,
				mode == CompressionMode.Compress ? SysCompression.CompressionMode.Compress : SysCompression.CompressionMode.Decompress);
		}

		public override bool CanRead { get { return inner.CanRead; } }
		public override bool CanSeek { get { return false; } }
		public override bool CanWrite { get { return inner.CanWrite; } }
		public override long Length { get { throw new NotSupportedException(); } }
		public override long Position
		{
			get { throw new NotSupportedException(); }
			set { throw new NotSupportedException(); }
		}
		public override void Flush() { inner.Flush(); }
		public override int Read(byte[] buffer, int offset, int count) { return inner.Read(buffer, offset, count); }
		public override long Seek(long offset, SeekOrigin origin) { throw new NotSupportedException(); }
		public override void SetLength(long value) { throw new NotSupportedException(); }
		public override void Write(byte[] buffer, int offset, int count) { inner.Write(buffer, offset, count); }

		protected override void Dispose(bool disposing)
		{
			if (disposing) inner.Dispose();
			base.Dispose(disposing);
		}
	}
}
