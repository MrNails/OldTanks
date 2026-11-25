using CoolEngine.Imaging.Jpeg;
using CoolEngine.Imaging.Png;

namespace CoolEngine.Imaging;

/// <summary>
/// Decodes PNG and JPEG files into <see cref="RgbaImage"/>. The format is detected from the file's signature, not its extension.
/// </summary>
/// <remarks>
/// Pixel values are returned as stored: gamma (<c>gAMA</c>, <c>sRGB</c>), ICC profiles and EXIF orientation are ignored.
/// Corrupt input throws <see cref="InvalidDataException"/>; valid input using a feature this decoder does not implement
/// (for example arithmetic-coded or 12-bit JPEG) throws <see cref="NotSupportedException"/>.
/// </remarks>
public static class ImageDecoder
{
    /// <summary>
    /// The largest image accepted, in pixels (1 GiB of RGBA8). Headers announcing more are rejected before anything is allocated.
    /// </summary>
    public const long MaxPixelCount = 16384L * 16384L;

    private static ReadOnlySpan<byte> PngSignature => [0x89, (byte)'P', (byte)'N', (byte)'G', 0x0D, 0x0A, 0x1A, 0x0A];

    public static ImageFormat DetectFormat(ReadOnlySpan<byte> data)
    {
        if (data.StartsWith(PngSignature))
            return ImageFormat.Png;

        if (data.Length >= 3 && data[0] == 0xFF && data[1] == 0xD8 && data[2] == 0xFF)
            return ImageFormat.Jpeg;

        return ImageFormat.Unknown;
    }

    /// <param name="data">The whole encoded file.</param>
    /// <param name="flipVertically">Return the bottom row first, as OpenGL texture uploads expect.</param>
    public static RgbaImage Decode(ReadOnlySpan<byte> data, bool flipVertically = false) => DetectFormat(data) switch
    {
        ImageFormat.Png => PngDecoder.Decode(data, flipVertically),
        ImageFormat.Jpeg => JpegDecoder.Decode(data, flipVertically),
        _ => throw new NotSupportedException("Unknown image format: only PNG and JPEG are supported."),
    };

    public static RgbaImage Load(string path, bool flipVertically = false) => Decode(File.ReadAllBytes(path), flipVertically);

    /// <summary>Reads the file asynchronously and decodes it on the thread pool, never on the calling thread.</summary>
    public static async Task<RgbaImage> LoadAsync(string path, bool flipVertically = false, CancellationToken cancellationToken = default)
    {
        var data = await File.ReadAllBytesAsync(path, cancellationToken).ConfigureAwait(false);

        // Decoding is CPU-bound and the engine awaits this from the GL thread; keep that thread free.
        return await Task.Run(() => Decode(data, flipVertically), cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Rejects zero, negative or oversized dimensions before a decoder allocates the pixel buffer.</summary>
    internal static void ValidateSize(long width, long height)
    {
        if (width <= 0 || height <= 0)
            throw new InvalidDataException($"Invalid image size {width}x{height}.");

        if (width * height > MaxPixelCount)
            throw new NotSupportedException($"Image size {width}x{height} exceeds the {MaxPixelCount}-pixel limit.");
    }
}
