namespace CoolEngine.Imaging;

/// <summary>
/// A decoded image: <see cref="Width"/> × <see cref="Height"/> RGBA8 pixels, row after row.
/// Row 0 is the top of the picture unless it was decoded with <c>flipVertically</c>, which puts the bottom row first
/// the way OpenGL texture uploads expect.
/// </summary>
public sealed class RgbaImage
{
    public RgbaImage(int width, int height, Rgba32[] pixels)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(width);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(height);
        ArgumentNullException.ThrowIfNull(pixels);

        if (pixels.Length != (long)width * height)
            throw new ArgumentException($"Expected {(long)width * height} pixels for {width}x{height}, got {pixels.Length}.", nameof(pixels));

        Width = width;
        Height = height;
        Pixels = pixels;
    }

    public int Width { get; }

    public int Height { get; }

    /// <summary>Exactly <c>Width * Height</c> pixels, row-major.</summary>
    public Rgba32[] Pixels { get; }

    /// <exception cref="ArgumentOutOfRangeException"><paramref name="x"/> is outside [0, Width) or <paramref name="y"/> outside [0, Height).</exception>
    public Rgba32 this[int x, int y]
    {
        get
        {
            ArgumentOutOfRangeException.ThrowIfNegative(x);
            ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(x, Width);
            ArgumentOutOfRangeException.ThrowIfNegative(y);
            ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(y, Height);

            return Pixels[y * Width + x];
        }
    }
}
