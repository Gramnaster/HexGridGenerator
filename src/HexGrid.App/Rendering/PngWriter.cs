using System.Buffers.Binary;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO.Compression;
using System.Runtime.InteropServices;

namespace HexGrid.App.Rendering;

/// <summary>
/// Writes a bitmap as an 8-bit RGBA PNG, compressing horizontal bands of rows in parallel.
/// </summary>
/// <remarks>
/// <para>
/// Replaces Bitmap.Save with the PNG format, whose encoder runs on one thread and took as long as
/// rasterising the grid on A0 and larger canvases. The chunks written match what GDI+ writes
/// (IHDR, sRGB, gAMA, pHYs, IDAT, IEND), and decoding the file gives back the exact pixels.
/// </para>
/// <para>
/// PNG holds a single zlib stream, but that stream may be split across any number of IDAT chunks.
/// Each band is deflated independently and ends with a sync flush, which closes it on a byte
/// boundary with a non-final block, so the bands concatenate into one valid deflate stream. The
/// Adler-32 checksums of the bands are combined arithmetically instead of re-reading the pixels.
/// See RFC 1950 (zlib), RFC 1951 (deflate) and https://www.w3.org/TR/png-3/ for the formats.
/// </para>
/// </remarks>
public static class PngWriter
{
    // Rows per independently compressed band. Small enough to give every core work on a 1080p
    // export, large enough that restarting the compressor costs no measurable file size.
    private const int BandRows = 64;

    private const uint AdlerModulus = 65521;

    // Largest run of bytes the Adler-32 sums can absorb before they must be reduced (RFC 1950).
    private const int AdlerChunk = 5552;

    // PNG filter type 2 (Up): each byte minus the byte above it. Rows of grid art repeat almost
    // exactly, so most bytes become zero and compress far better than raw rows.
    private const byte FilterUp = 2;

    private static readonly byte[] Signature = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];

    // zlib header for deflate with a 32 KB window at the default compression level.
    private static readonly byte[] ZlibHeader = [0x78, 0x9C];

    // An empty final deflate block with fixed Huffman codes. Closes the stream after the last band.
    private static readonly byte[] FinalDeflateBlock = [0x03, 0x00];

    private static readonly uint[] CrcTable = BuildCrcTable();

    /// <summary>Encodes <paramref name="bitmap"/> to <paramref name="output"/>, tagged with <paramref name="dpi"/>.</summary>
    public static void Write(Bitmap bitmap, int dpi, Stream output)
    {
        ArgumentNullException.ThrowIfNull(bitmap);
        ArgumentNullException.ThrowIfNull(output);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(dpi);

        int width = bitmap.Width;
        int height = bitmap.Height;
        // SS003: integer ceiling division is intentional, so a partial last band still counts.
#pragma warning disable SS003
        int bandCount = (height + BandRows - 1) / BandRows;
#pragma warning restore SS003
        var bands = new byte[bandCount][];
        var bandAdlers = new uint[bandCount];

        BitmapData pixels = bitmap.LockBits(
            new Rectangle(0, 0, width, height), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
        try
        {
            Parallel.For(0, bandCount, band =>
                (bands[band], bandAdlers[band]) = CompressBand(pixels, band * BandRows, Math.Min(height, (band + 1) * BandRows)));
        }
        finally
        {
            bitmap.UnlockBits(pixels);
        }

        long rowBytes = 1 + (width * 4L);
        uint adler = bandAdlers[0];
        for (int band = 1; band < bandCount; band++)
        {
            long bandBytes = (Math.Min(height, (band + 1) * BandRows) - (band * BandRows)) * rowBytes;
            adler = CombineAdler(adler, bandAdlers[band], bandBytes);
        }

        output.Write(Signature);
        WriteChunk(output, "IHDR"u8, Header(width, height));
        WriteChunk(output, "sRGB"u8, [0]); // rendering intent: perceptual
        WriteChunk(output, "gAMA"u8, BigEndian(45455)); // 1/2.2, the sRGB gamma, scaled by 100000
        WriteChunk(output, "pHYs"u8, PhysicalSize(dpi));

        WriteChunk(output, "IDAT"u8, ZlibHeader);
        foreach (byte[] band in bands)
        {
            WriteChunk(output, "IDAT"u8, band);
        }

        WriteChunk(output, "IDAT"u8, [.. FinalDeflateBlock, .. BigEndian(adler)]);
        WriteChunk(output, "IEND"u8, []);
    }

    /// <summary>Filters and deflates rows [<paramref name="top"/>, <paramref name="bottom"/>), returning the bytes and their Adler-32.</summary>
    private static (byte[] Deflated, uint Adler) CompressBand(BitmapData pixels, int top, int bottom)
    {
        int width = pixels.Width;
        var current = new byte[width * 4];
        var above = new byte[width * 4];
        var filtered = new byte[1 + (width * 4)];
        filtered[0] = FilterUp;
        uint adler = 1;

        // The row above the first row of the image is defined as all zeros, which is how a fresh
        // array starts. Every other band reads its predecessor's last row.
        if (top > 0)
        {
            Marshal.Copy(RowAddress(pixels, top - 1), above, 0, above.Length);
        }

        using var buffer = new MemoryStream();
        using (var deflate = new DeflateStream(buffer, CompressionLevel.Optimal, leaveOpen: true))
        {
            for (int y = top; y < bottom; y++)
            {
                Marshal.Copy(RowAddress(pixels, y), current, 0, current.Length);

                // GDI+ stores each pixel as B, G, R, A in memory. PNG wants R, G, B, A.
                for (int i = 0; i < current.Length; i += 4)
                {
                    filtered[i + 1] = (byte)(current[i + 2] - above[i + 2]);
                    filtered[i + 2] = (byte)(current[i + 1] - above[i + 1]);
                    filtered[i + 3] = (byte)(current[i] - above[i]);
                    filtered[i + 4] = (byte)(current[i + 3] - above[i + 3]);
                }

                deflate.Write(filtered);
                adler = UpdateAdler(adler, filtered);
                (current, above) = (above, current);
            }

            // A sync flush, not the end of the stream: the bytes so far end on a byte boundary with
            // no final-block marker, so the next band's bytes can follow them directly. Whatever
            // Dispose appends after this point is the stream terminator, which is not wanted here.
            deflate.Flush();
            return (buffer.ToArray(), adler);
        }
    }

    private static nint RowAddress(BitmapData pixels, int y) => pixels.Scan0 + ((nint)y * pixels.Stride);

    private static byte[] Header(int width, int height)
    {
        var header = new byte[13];
        BinaryPrimitives.WriteInt32BigEndian(header, width);
        BinaryPrimitives.WriteInt32BigEndian(header.AsSpan(4), height);
        header[8] = 8; // bits per channel
        header[9] = 6; // colour type: RGB with alpha
        return header; // compression, filter method and interlace stay 0
    }

    private static byte[] PhysicalSize(int dpi)
    {
        uint pixelsPerMetre = (uint)Math.Round(dpi / 0.0254, MidpointRounding.AwayFromZero);
        var physical = new byte[9];
        BinaryPrimitives.WriteUInt32BigEndian(physical, pixelsPerMetre);
        BinaryPrimitives.WriteUInt32BigEndian(physical.AsSpan(4), pixelsPerMetre);
        physical[8] = 1; // unit: metre
        return physical;
    }

    private static byte[] BigEndian(uint value)
    {
        var bytes = new byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(bytes, value);
        return bytes;
    }

    private static void WriteChunk(Stream output, ReadOnlySpan<byte> type, ReadOnlySpan<byte> data)
    {
        Span<byte> word = stackalloc byte[4];
        BinaryPrimitives.WriteInt32BigEndian(word, data.Length);
        output.Write(word);
        output.Write(type);
        output.Write(data);

        // The CRC covers the type and the data, not the length.
        uint crc = UpdateCrc(UpdateCrc(0xFFFFFFFFu, type), data) ^ 0xFFFFFFFFu;
        BinaryPrimitives.WriteUInt32BigEndian(word, crc);
        output.Write(word);
    }

    private static uint UpdateCrc(uint crc, ReadOnlySpan<byte> data)
    {
        foreach (byte b in data)
        {
            crc = CrcTable[(crc ^ b) & 0xFF] ^ (crc >> 8);
        }

        return crc;
    }

    /// <summary>The CRC-32 lookup table from the PNG specification, Annex D.</summary>
    private static uint[] BuildCrcTable()
    {
        var table = new uint[256];
        for (uint n = 0; n < 256; n++)
        {
            uint c = n;
            for (int k = 0; k < 8; k++)
            {
                c = (c & 1) != 0 ? 0xEDB88320u ^ (c >> 1) : c >> 1;
            }

            table[n] = c;
        }

        return table;
    }

    private static uint UpdateAdler(uint adler, ReadOnlySpan<byte> data)
    {
        uint a = adler & 0xFFFF;
        uint b = adler >> 16;
        while (!data.IsEmpty)
        {
            int n = Math.Min(AdlerChunk, data.Length);
            foreach (byte x in data[..n])
            {
                a += x;
                b += a;
            }

            a %= AdlerModulus;
            b %= AdlerModulus;
            data = data[n..];
        }

        return (b << 16) | a;
    }

    /// <summary>
    /// Adler-32 of two byte runs joined end to end, given each run's checksum and the second run's
    /// length. The same arithmetic as zlib's adler32_combine.
    /// </summary>
    private static uint CombineAdler(uint first, uint second, long secondLength)
    {
        uint remainder = (uint)(secondLength % AdlerModulus);
        uint a = (first & 0xFFFF) + (second & 0xFFFF) + AdlerModulus - 1;
        ulong b = ((ulong)remainder * (first & 0xFFFF) % AdlerModulus) + (first >> 16) + (second >> 16) + AdlerModulus - remainder;
        return (a % AdlerModulus) | ((uint)(b % AdlerModulus) << 16);
    }
}
