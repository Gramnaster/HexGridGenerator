using System.Buffers.Binary;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO.Compression;
using HexGrid.App.Rendering;

namespace HexGrid.App.Tests;

public class PngWriterTests
{
    [Theory]
    [InlineData(1, 1)] // a single band shorter than the band height
    [InlineData(64, 64)] // exactly one full band
    [InlineData(150, 130)] // two full bands and a two-row remainder
    public void Write_AntialiasedTranslucentArt_DecodesToIdenticalPixels(int width, int height)
    {
        // Arrange: translucent antialiased strokes on a transparent background exercise every
        // channel, including partial alpha, which is where a channel-order or filter bug would show.
        using Bitmap source = DrawArt(width, height);
        using var png = new MemoryStream();

        // Act
        PngWriter.Write(source, dpi: 300, png);
        png.Position = 0;
        using var decoded = new Bitmap(png);

        // Assert
        Assert.Equal(source.Size, decoded.Size);
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                Assert.Equal(source.GetPixel(x, y).ToArgb(), decoded.GetPixel(x, y).ToArgb());
            }
        }
    }

    [Fact]
    public void Write_ImageData_InflatesWithValidChecksumToEveryRow()
    {
        // Arrange: ZLibStream verifies the trailing Adler-32 and throws on a mismatch, so a clean
        // inflate proves the per-band checksums were combined correctly.
        const int width = 150;
        const int height = 130;
        using Bitmap source = DrawArt(width, height);
        using var png = new MemoryStream();

        // Act
        PngWriter.Write(source, dpi: 300, png);

        // Assert
        using var zlib = new ZLibStream(new MemoryStream(ImageData(png.ToArray())), CompressionMode.Decompress);
        using var inflated = new MemoryStream();
        zlib.CopyTo(inflated);
        Assert.Equal(height * (1 + (width * 4)), inflated.Length);
    }

    [Fact]
    public void Write_EndChunk_CarriesTheSpecifiedCrc()
    {
        // Arrange: IEND has no data, so its CRC is a fixed, well-known value. It checks the CRC
        // table and its use without re-implementing CRC-32 in the test.
        using var source = new Bitmap(4, 4, PixelFormat.Format32bppArgb);
        using var png = new MemoryStream();

        // Act
        PngWriter.Write(source, dpi: 96, png);

        // Assert
        byte[] expected = [0, 0, 0, 0, (byte)'I', (byte)'E', (byte)'N', (byte)'D', 0xAE, 0x42, 0x60, 0x82];
        Assert.Equal(expected, png.ToArray()[^12..]);
    }

    [Fact]
    public void Write_Dpi_DecodesAsTheBitmapResolution()
    {
        // Arrange
        using var source = new Bitmap(4, 4, PixelFormat.Format32bppArgb);
        using var png = new MemoryStream();

        // Act
        PngWriter.Write(source, dpi: 300, png);
        png.Position = 0;
        using var decoded = new Bitmap(png);

        // Assert: PNG stores pixels per metre as an integer, so 300 dpi comes back as 299.9994.
        Assert.Equal(300, decoded.HorizontalResolution, precision: 2);
        Assert.Equal(300, decoded.VerticalResolution, precision: 2);
    }

    private static Bitmap DrawArt(int width, int height)
    {
        var bitmap = new Bitmap(width, height, PixelFormat.Format32bppArgb);
        using Graphics g = Graphics.FromImage(bitmap);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.Clear(Color.Transparent);
        using var pen = new Pen(Color.FromArgb(140, 200, 30, 90), 3f);
        using var brush = new SolidBrush(Color.FromArgb(90, 20, 120, 220));
        g.DrawLine(pen, 0, 0, width, height);
        g.DrawLine(pen, width, 0, 0, height);
        g.FillEllipse(brush, width / 4f, height / 4f, width / 2f, height / 2f);
        return bitmap;
    }

    /// <summary>Concatenates the data of every IDAT chunk: the PNG's single zlib stream.</summary>
    private static byte[] ImageData(byte[] png)
    {
        using var data = new MemoryStream();
        int offset = 8; // past the signature
        while (offset < png.Length)
        {
            int length = BinaryPrimitives.ReadInt32BigEndian(png.AsSpan(offset));
            if (png.AsSpan(offset + 4, 4).SequenceEqual("IDAT"u8))
            {
                data.Write(png, offset + 8, length);
            }

            offset += 12 + length;
        }

        return data.ToArray();
    }
}
