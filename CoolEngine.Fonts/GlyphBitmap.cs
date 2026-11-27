namespace CoolEngine.Fonts;

/// <summary>
/// An 8-bit coverage bitmap of one glyph, as FreeType's <c>FT_Render_Glyph</c> with <c>FT_RENDER_MODE_NORMAL</c> makes it.
/// </summary>
public sealed class GlyphBitmap
{
    internal GlyphBitmap(int width, int height, int left, int top, byte[] pixels)
    {
        Width = width;
        Height = height;
        Left = left;
        Top = top;
        Pixels = pixels;
    }

    /// <summary>Columns.</summary>
    public int Width { get; }

    /// <summary>Rows.</summary>
    public int Height { get; }

    /// <summary>From the glyph origin to the bitmap's left column, in whole pixels (FreeType's <c>bitmap_left</c>).</summary>
    public int Left { get; }

    /// <summary>From the baseline up to the bitmap's top row, in whole pixels (FreeType's <c>bitmap_top</c>).</summary>
    public int Top { get; }

    /// <summary>Coverage 0 to 255, row 0 at the top, pitch <see cref="Width"/>, length <c>Width * Height</c>.</summary>
    public byte[] Pixels { get; }
}
