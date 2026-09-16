using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using FluentAssertions;
using Xunit;
using Xunit.Abstractions;

namespace MoneyFlow.Tests.UiTests;

public class IconGenerationTests
{
    private readonly ITestOutputHelper _output;
    private const string SourceImagePath = @"C:\Users\Parth\.gemini\antigravity-ide\brain\e3fff0c5-0774-4fb2-aa08-cd4dad9cb57c\.user_uploaded\media_1789534078334.png";
    private const string OutputDir = @"d:\parth fun project\like tally\MoneyFlow-Desktop\MoneyFlow.Desktop\Resources";
    private const string OutputIcoPath = OutputDir + @"\app.ico";

    public IconGenerationTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void GenerateAndVerifyAppIcon()
    {
        File.Exists(SourceImagePath).Should().BeTrue();

        using var srcBmp = new Bitmap(SourceImagePath);

        // 1. Detect bounding box of the purple logo
        // Background is pure white/near-white.
        int minX = srcBmp.Width, maxX = 0, minY = srcBmp.Height, maxY = 0;

        for (int y = 0; y < srcBmp.Height; y++)
        {
            for (int x = 0; x < srcBmp.Width; x++)
            {
                var c = srcBmp.GetPixel(x, y);
                // Background is white: R > 245, G > 245, B > 245
                if (c.R < 245 || c.G < 245 || c.B < 245)
                {
                    if (x < minX) minX = x;
                    if (x > maxX) maxX = x;
                    if (y < minY) minY = y;
                    if (y > maxY) maxY = y;
                }
            }
        }

        _output.WriteLine($"Logo bounds: X=[{minX}..{maxX}], Y=[{minY}..{maxY}], Size=({maxX - minX + 1}x{maxY - minY + 1})");

        int logoW = maxX - minX + 1;
        int logoH = maxY - minY + 1;

        // Make it a square with equal padding for balanced icon representation
        int side = Math.Max(logoW, logoH);
        int pad = (int)(side * 0.04); // 4% padding around logo for crisp edge framing
        int targetSide = side + pad * 2;

        int cropCenterX = (minX + maxX) / 2;
        int cropCenterY = (minY + maxY) / 2;

        int cropX = cropCenterX - targetSide / 2;
        int cropY = cropCenterY - targetSide / 2;

        // 2. Create high-resolution transparent master bitmap (512x512)
        int masterSize = 512;
        using var masterBmp = new Bitmap(masterSize, masterSize, PixelFormat.Format32bppArgb);

        // Sample and de-mat from source
        for (int ty = 0; ty < masterSize; ty++)
        {
            for (int tx = 0; tx < masterSize; tx++)
            {
                double srcX = cropX + (double)tx * targetSide / masterSize;
                double srcY = cropY + (double)ty * targetSide / masterSize;

                int sx = Math.Clamp((int)Math.Round(srcX), 0, srcBmp.Width - 1);
                int sy = Math.Clamp((int)Math.Round(srcY), 0, srcBmp.Height - 1);

                var c = srcBmp.GetPixel(sx, sy);

                // Precise alpha de-matting against white (255, 255, 255)
                // In RGB: if pixel was composited as C_obs = a * C_true + (1 - a) * 255
                // The channel with largest deviation from 255 gives the strongest alpha signal.
                // For purple/blue, Green is lowest (G is absorbed, R and B are reflected).
                // So max(255 - R, 255 - G, 255 - B) / 255 is proportional to opacity.
                int maxDelta = Math.Max(255 - c.R, Math.Max(255 - c.G, 255 - c.B));

                if (maxDelta <= 4)
                {
                    // Pure white background
                    masterBmp.SetPixel(tx, ty, Color.FromArgb(0, 0, 0, 0));
                }
                else if (maxDelta < 35)
                {
                    // Anti-aliased outer edge: smooth alpha ramp
                    double alphaNorm = (maxDelta - 4.0) / (35.0 - 4.0);
                    int alpha = Math.Clamp((int)(alphaNorm * 255), 0, 255);

                    // De-mat color to preserve pure purple hue at edges
                    double a = Math.Max(0.05, alpha / 255.0);
                    int r = Math.Clamp((int)((c.R - (1 - a) * 255) / a), 0, 255);
                    int g = Math.Clamp((int)((c.G - (1 - a) * 255) / a), 0, 255);
                    int b = Math.Clamp((int)((c.B - (1 - a) * 255) / a), 0, 255);

                    masterBmp.SetPixel(tx, ty, Color.FromArgb(alpha, r, g, b));
                }
                else
                {
                    // Solid logo interior: full opacity with exact source color
                    masterBmp.SetPixel(tx, ty, Color.FromArgb(255, c.R, c.G, c.B));
                }
            }
        }

        // Save a PNG preview for visual inspection
        Directory.CreateDirectory(OutputDir);
        masterBmp.Save(Path.Combine(OutputDir, "app_preview.png"), ImageFormat.Png);

        // Required resolutions: 16, 20, 24, 32, 48, 64, 128, 256
        int[] resolutions = [16, 20, 24, 32, 48, 64, 128, 256];

        var imageStreams = new (int Size, byte[] Data)[resolutions.Length];
        for (int i = 0; i < resolutions.Length; i++)
        {
            int sz = resolutions[i];
            using var resized = new Bitmap(sz, sz, PixelFormat.Format32bppArgb);
            using (var g = Graphics.FromImage(resized))
            {
                g.SmoothingMode = SmoothingMode.HighQuality;
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                g.CompositingQuality = CompositingQuality.HighQuality;
                g.Clear(Color.Transparent);
                g.DrawImage(masterBmp, new Rectangle(0, 0, sz, sz));
            }

            if (sz <= 64)
            {
                // Standard 32bpp DIB format for maximum Windows GDI & Win32 shell compatibility
                imageStreams[i] = (sz, CreateDibPayload(resized));
            }
            else
            {
                // PNG format for high-res (128, 256) per modern Windows icon standard
                using var ms = new MemoryStream();
                resized.Save(ms, ImageFormat.Png);
                imageStreams[i] = (sz, ms.ToArray());
            }
        }

        // 3. Build multi-resolution ICO file
        using (var fs = new FileStream(OutputIcoPath, FileMode.Create, FileAccess.Write))
        using (var bw = new BinaryWriter(fs))
        {
            // ICONDIR
            bw.Write((ushort)0); // idReserved
            bw.Write((ushort)1); // idType = 1 (ICO)
            bw.Write((ushort)resolutions.Length); // idCount

            // Compute offset for image data: Header (6) + entries (16 * count)
            int offset = 6 + (16 * resolutions.Length);

            // ICONDIRENTRY for each resolution
            for (int i = 0; i < resolutions.Length; i++)
            {
                var (sz, data) = imageStreams[i];
                bw.Write((byte)(sz == 256 ? 0 : sz)); // bWidth
                bw.Write((byte)(sz == 256 ? 0 : sz)); // bHeight
                bw.Write((byte)0); // bColorCount
                bw.Write((byte)0); // bReserved
                bw.Write((ushort)1); // wPlanes
                bw.Write((ushort)32); // wBitCount
                bw.Write((uint)data.Length); // dwBytesInRes
                bw.Write((uint)offset); // dwImageOffset

                offset += data.Length;
            }

            // Write image payloads
            for (int i = 0; i < resolutions.Length; i++)
            {
                bw.Write(imageStreams[i].Data);
            }
        }

        // 4. Verify the created ICO file
        File.Exists(OutputIcoPath).Should().BeTrue();
        var fi = new FileInfo(OutputIcoPath);
        fi.Length.Should().BeGreaterThan(1000);

        // Verify all 8 resolutions are present in the ICO directory
        using var fsRead = new FileStream(OutputIcoPath, FileMode.Open, FileAccess.Read);
        using var br = new BinaryReader(fsRead);

        ushort reserved = br.ReadUInt16();
        ushort type = br.ReadUInt16();
        ushort count = br.ReadUInt16();

        reserved.Should().Be(0);
        type.Should().Be(1);
        count.Should().Be(8);

        for (int i = 0; i < count; i++)
        {
            byte w = br.ReadByte();
            byte h = br.ReadByte();
            int actualW = w == 0 ? 256 : w;
            int actualH = h == 0 ? 256 : h;
            br.ReadByte(); // bColorCount
            br.ReadByte(); // bReserved
            ushort planes = br.ReadUInt16();
            ushort bitCount = br.ReadUInt16();
            uint bytesInRes = br.ReadUInt32();
            uint imgOffset = br.ReadUInt32();

            actualW.Should().Be(resolutions[i]);
            actualH.Should().Be(resolutions[i]);
            planes.Should().Be(1);
            bitCount.Should().Be(32);
            bytesInRes.Should().BeGreaterThan(0);
            imgOffset.Should().BeGreaterThan(0);

            _output.WriteLine($"Verified ICO Entry {i}: {actualW}x{actualH} @ {bitCount}bpp, {bytesInRes} bytes at offset {imgOffset}");
        }

        // Verify it loads with System.Drawing.Icon
        using var loadedIcon = new Icon(OutputIcoPath);
        loadedIcon.Width.Should().BeGreaterThan(0);
        loadedIcon.Height.Should().BeGreaterThan(0);
    }

    private static byte[] CreateDibPayload(Bitmap bmp)
    {
        int w = bmp.Width;
        int h = bmp.Height;

        // BITMAPINFOHEADER is 40 bytes
        // In ICO DIB, biHeight is 2 * h (height of XOR mask + AND mask)
        int xorSize = w * h * 4;
        int andRowBytes = ((w + 31) / 32) * 4;
        int andSize = andRowBytes * h;
        int totalSize = 40 + xorSize + andSize;

        byte[] dib = new byte[totalSize];
        using var ms = new MemoryStream(dib);
        using var bw = new BinaryWriter(ms);

        // BITMAPINFOHEADER
        bw.Write((uint)40); // biSize
        bw.Write((int)w); // biWidth
        bw.Write((int)(h * 2)); // biHeight (XOR + AND)
        bw.Write((ushort)1); // biPlanes
        bw.Write((ushort)32); // biBitCount
        bw.Write((uint)0); // biCompression (BI_RGB)
        bw.Write((uint)(xorSize + andSize)); // biSizeImage
        bw.Write((int)0); // biXPelsPerMeter
        bw.Write((int)0); // biYPelsPerMeter
        bw.Write((uint)0); // biClrUsed
        bw.Write((uint)0); // biClrImportant

        // XOR mask: 32bpp ARGB in bottom-up order
        for (int y = h - 1; y >= 0; y--)
        {
            for (int x = 0; x < w; x++)
            {
                var c = bmp.GetPixel(x, y);
                bw.Write(c.B);
                bw.Write(c.G);
                bw.Write(c.R);
                bw.Write(c.A);
            }
        }

        // AND mask: 1bpp in bottom-up order (0 for opaque/visible, 1 for transparent)
        for (int y = h - 1; y >= 0; y--)
        {
            byte curByte = 0;
            int bitPos = 7;
            int bytesWritten = 0;

            for (int x = 0; x < w; x++)
            {
                var c = bmp.GetPixel(x, y);
                if (c.A == 0)
                {
                    curByte |= (byte)(1 << bitPos);
                }

                bitPos--;
                if (bitPos < 0)
                {
                    bw.Write(curByte);
                    bytesWritten++;
                    curByte = 0;
                    bitPos = 7;
                }
            }

            if (bitPos != 7)
            {
                bw.Write(curByte);
                bytesWritten++;
            }

            // Pad row to 4-byte boundary
            while (bytesWritten % 4 != 0)
            {
                bw.Write((byte)0);
                bytesWritten++;
            }
        }

        return dib;
    }

    [Fact]
    public void VerifyCompiledExecutableHasEmbeddedIcon()
    {
        string exePath = @"d:\parth fun project\like tally\MoneyFlow-Desktop\MoneyFlow.Desktop\bin\Debug\net8.0-windows\MoneyFlow.Desktop.exe";
        File.Exists(exePath).Should().BeTrue();

        using var icon = Icon.ExtractAssociatedIcon(exePath);
        icon.Should().NotBeNull();
        icon!.Width.Should().BeGreaterThan(0);
        icon.Height.Should().BeGreaterThan(0);

        using var bmp = icon.ToBitmap();
        bmp.Width.Should().BeGreaterThan(0);
        bmp.Height.Should().BeGreaterThan(0);

        // Verify the extracted icon is the purple logo
        // Check for presence of purple/blue pixels in the center
        bool foundPurplePixel = false;
        for (int y = 0; y < bmp.Height; y++)
        {
            for (int x = 0; x < bmp.Width; x++)
            {
                var c = bmp.GetPixel(x, y);
                // Purple: Blue > 120, Red > 60, Green < Blue - 30
                if (c.A > 150 && c.B > 120 && c.R > 60 && c.B > c.G + 30)
                {
                    foundPurplePixel = true;
                    break;
                }
            }
            if (foundPurplePixel) break;
        }

        foundPurplePixel.Should().BeTrue("Extracted icon from MoneyFlow.Desktop.exe should contain the purple gradient logo");
    }
}
