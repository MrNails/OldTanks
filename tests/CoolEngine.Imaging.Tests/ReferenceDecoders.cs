using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats;
using Bitmap = System.Drawing.Bitmap;
using GdiRectangle = System.Drawing.Rectangle;
using Rgba32 = CoolEngine.Imaging.Rgba32;
using SixLaborsRgba32 = SixLabors.ImageSharp.PixelFormats.Rgba32;

namespace CoolEngine.Imaging.Tests;

/// <summary>The two decoders <c>CoolEngine.Imaging</c> is compared with, both returning row-major RGBA8 with row 0 at the top.</summary>
internal static class ReferenceDecoders
{
    /// <summary>ImageSharp, keeping pixels as stored (no ICC conversion).</summary>
    public static (int Width, int Height, Rgba32[] Pixels) ImageSharp(byte[] encoded)
    {
        var options = new DecoderOptions { ColorProfileHandling = ColorProfileHandling.Preserve };
        using var image = Image.Load<SixLaborsRgba32>(options, encoded);

        var source = new SixLaborsRgba32[image.Width * image.Height];
        image.CopyPixelDataTo(source);

        var pixels = new Rgba32[source.Length];
        for (var i = 0; i < source.Length; i++)
            pixels[i] = new Rgba32(source[i].R, source[i].G, source[i].B, source[i].A);

        return (image.Width, image.Height, pixels);
    }

    /// <summary>Windows' GDI+ through System.Drawing, without colour management.</summary>
    public static (int Width, int Height, Rgba32[] Pixels) Windows(byte[] encoded)
    {
        using var stream = new MemoryStream(encoded, writable: false);
        using var bitmap = new Bitmap(stream, useIcm: false);

        var width = bitmap.Width;
        var height = bitmap.Height;
        var data = bitmap.LockBits(new GdiRectangle(0, 0, width, height), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);

        try
        {
            var pixels = new Rgba32[width * height];
            var row = new byte[width * 4];

            for (var y = 0; y < height; y++)
            {
                Marshal.Copy(data.Scan0 + y * data.Stride, row, 0, row.Length);

                // Format32bppArgb is B, G, R, A in memory.
                for (var x = 0; x < width; x++)
                    pixels[y * width + x] = new Rgba32(row[x * 4 + 2], row[x * 4 + 1], row[x * 4], row[x * 4 + 3]);
            }

            return (width, height, pixels);
        }
        finally
        {
            bitmap.UnlockBits(data);
        }
    }
}
