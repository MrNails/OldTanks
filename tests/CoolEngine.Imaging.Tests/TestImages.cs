using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Formats.Png;
using SixLaborsRgba32 = SixLabors.ImageSharp.PixelFormats.Rgba32;

namespace CoolEngine.Imaging.Tests;

/// <summary>A synthetic gradient image and its PNG and JPEG encodings by ImageSharp.</summary>
internal static class TestImages
{
    private const int JpegQuality = 90;

    /// <summary>
    /// R = 255·x/(w−1), G = 255·y/(h−1), B = 255·(x+y)/(w+h−2); A = 255·((x + 2y) mod 7)/6 with alpha, else 255.
    /// </summary>
    public static Image<SixLaborsRgba32> Source(int width, int height, bool withAlpha)
    {
        var image = new Image<SixLaborsRgba32>(width, height);

        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var r = 255 * x / (width - 1);
                var g = 255 * y / (height - 1);
                var b = 255 * (x + y) / (width + height - 2);
                var a = withAlpha ? 255 * ((x + 2 * y) % 7) / 6 : 255;
                image[x, y] = new SixLaborsRgba32((byte)r, (byte)g, (byte)b, (byte)a);
            }
        }

        return image;
    }

    /// <summary>The source with alpha as a PNG of exactly the requested colour type, bit depth and interlacing.</summary>
    /// <exception cref="InvalidOperationException">ImageSharp wrote a different header than requested.</exception>
    public static byte[] EncodePng(int width, int height, PngColorType colorType, PngBitDepth bitDepth, PngInterlaceMode interlace)
    {
        using var image = Source(width, height, withAlpha: true);
        using var stream = new MemoryStream();
        image.Save(stream, new PngEncoder { ColorType = colorType, BitDepth = bitDepth, InterlaceMethod = interlace });
        var encoded = stream.ToArray();

        // IHDR data starts at byte 16: width, height, bit depth (24), colour type (25), compression, filter, interlace (28).
        if (encoded[24] != (byte)bitDepth || encoded[25] != (byte)colorType || encoded[28] != (byte)interlace)
        {
            throw new InvalidOperationException(
                $"ImageSharp encoded {colorType}/{bitDepth}/{interlace} as colour type {encoded[25]}, depth {encoded[24]}, interlace {encoded[28]}.");
        }

        return encoded;
    }

    /// <summary>The source without alpha as a JPEG at quality 90.</summary>
    public static byte[] EncodeJpeg(int width, int height, JpegColorType colorType, bool progressive, int restartInterval, bool interleaved)
    {
        using var image = Source(width, height, withAlpha: false);
        using var stream = new MemoryStream();
        var encoder = new JpegEncoder
        {
            Quality = JpegQuality,
            ColorType = colorType,
            Progressive = progressive,
            RestartInterval = restartInterval,
            Interleaved = interleaved,
        };
        image.Save(stream, encoder);

        return stream.ToArray();
    }
}
