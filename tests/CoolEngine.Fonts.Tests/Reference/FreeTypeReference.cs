using System.Runtime.InteropServices;
using System.Text;
using FreeTypeSharp;
using static FreeTypeSharp.FT;

namespace CoolEngine.Fonts.Tests.Reference;

/// <summary>A FreeType outline: 26.6 points with their kinds and the contour ends.</summary>
public sealed record FreeTypeOutline(OutlinePoint[] Points, int[] ContourEnds);

/// <summary>A glyph as FreeType loads (and optionally renders) it.</summary>
public sealed record FreeTypeGlyph(FreeTypeOutline Outline, GlyphMetrics Metrics, FreeTypeBitmap? Bitmap);

/// <summary>A FreeType coverage bitmap, row 0 at the top, pitch = width.</summary>
public sealed record FreeTypeBitmap(int Width, int Height, int Left, int Top, byte[] Pixels);

/// <summary>
/// FreeType 2.13.2 through FreeTypeSharp 3.1.0's native <c>freetype.dll</c>: the reference for everything scaled, hinted or
/// rasterised. One library per instance; use one per fixture (or per thread) and never share a face across threads.
/// </summary>
internal sealed unsafe class FreeTypeReference : IDisposable
{
    private FT_LibraryRec_* m_library;

    public FreeTypeReference()
    {
        FT_LibraryRec_* library;
        Check(FT_Init_FreeType(&library), "FT_Init_FreeType");
        m_library = library;
    }

    /// <summary>FreeType's version, such as 2.13.2.</summary>
    public Version Version
    {
        get
        {
            int major, minor, patch;
            FT_Library_Version(m_library, &major, &minor, &patch);
            return new Version(major, minor, patch);
        }
    }

    /// <summary>Sets the TrueType driver's <c>interpreter-version</c> property: 35 or 40.</summary>
    public void SetInterpreterVersion(int version)
    {
        var module = Encoding.ASCII.GetBytes("truetype\0");
        var property = Encoding.ASCII.GetBytes("interpreter-version\0");
        var value = (uint)version;
        fixed (byte* modulePointer = module)
        fixed (byte* propertyPointer = property)
            Check(FT_Property_Set(m_library, modulePointer, propertyPointer, &value), "FT_Property_Set");
    }

    /// <summary>Opens a face from bytes, which stay pinned for the face's lifetime.</summary>
    public FreeTypeFace OpenFace(byte[] data, int faceIndex) => new(m_library, data, faceIndex);

    /// <summary>
    /// <c>FT_Outline_Get_Bitmap</c> into a zeroed 8-bit bitmap: the outline (26.6, bottom-left of the bitmap at 0, 0) rendered by
    /// the smooth rasteriser. Row 0 of the result is the top.
    /// </summary>
    public byte[] RenderOutline(GlyphOutline outline, int width, int height)
    {
        var points = outline.Points.Span;
        var contours = outline.ContourEndPoints.Span;

        FT_Outline_ ftOutline;
        Check(FT_Outline_New(m_library, (uint)points.Length, contours.Length, &ftOutline), "FT_Outline_New");
        try
        {
            for (var i = 0; i < points.Length; i++)
            {
                ftOutline.points[i].x = points[i].X;
                ftOutline.points[i].y = points[i].Y;
                ftOutline.tags[i] = points[i].Kind switch
                {
                    OutlinePointKind.OnCurve => 1,
                    OutlinePointKind.Quadratic => 0,
                    _ => 2,
                };
            }

            for (var i = 0; i < contours.Length; i++)
                ftOutline.contours[i] = (short)contours[i];

            var pixels = new byte[width * height];
            fixed (byte* buffer = pixels)
            {
                FT_Bitmap_ bitmap;
                FT_Bitmap_Init(&bitmap);
                bitmap.rows = (uint)height;
                bitmap.width = (uint)width;
                bitmap.pitch = width;
                bitmap.buffer = buffer;
                bitmap.num_grays = 256;
                bitmap.pixel_mode = FT_Pixel_Mode_.FT_PIXEL_MODE_GRAY;
                Check(FT_Outline_Get_Bitmap(m_library, &ftOutline, &bitmap), "FT_Outline_Get_Bitmap");
            }

            return pixels;
        }
        finally
        {
            FT_Outline_Done(m_library, &ftOutline);
        }
    }

    public void Dispose()
    {
        if (m_library is null)
            return;

        FT_Done_FreeType(m_library);
        m_library = null;
    }

    internal static void Check(FT_Error error, string function)
    {
        if (error != FT_Error.FT_Err_Ok)
            throw new InvalidOperationException($"{function} failed: {error} (0x{(int)error:X}).");
    }
}

/// <summary>One FreeType face over pinned bytes.</summary>
internal sealed unsafe class FreeTypeFace : IDisposable
{
    private GCHandle m_pin;
    private FT_FaceRec_* m_face;

    public FreeTypeFace(FT_LibraryRec_* library, byte[] data, int faceIndex)
    {
        m_pin = GCHandle.Alloc(data, GCHandleType.Pinned);
        FT_FaceRec_* face;
        var error = FT_New_Memory_Face(library, (byte*)m_pin.AddrOfPinnedObject(), data.Length, faceIndex, &face);
        if (error != FT_Error.FT_Err_Ok)
        {
            m_pin.Free();
            throw new InvalidOperationException($"FT_New_Memory_Face failed: {error} (0x{(int)error:X}).");
        }

        m_face = face;
    }

    public int NumFaces => (int)m_face->num_faces;

    /// <summary><c>FT_FACE_FLAG_MULTIPLE_MASTERS</c>: FreeType found variation data (<c>fvar</c>) it can use.</summary>
    public bool HasMultipleMasters => ((long)m_face->face_flags & (1L << 8)) != 0;

    public int NumGlyphs => (int)m_face->num_glyphs;

    public string FamilyName => ReadString(m_face->family_name);

    public string StyleName => ReadString(m_face->style_name);

    public int UnitsPerEm => m_face->units_per_EM;

    public short Ascender => m_face->ascender;

    public short Descender => m_face->descender;

    public short Height => m_face->height;

    /// <summary><c>FT_Get_Char_Index</c> on the charmap FreeType selected when it opened the face.</summary>
    public int GetCharIndex(int codePoint) => (int)FT_Get_Char_Index(m_face, (nuint)(uint)codePoint);

    /// <summary><c>FT_Get_Advance(face, glyph, FT_LOAD_NO_SCALE)</c>: the design advance in font units.</summary>
    public int GetDesignAdvance(int glyphIndex)
    {
        nint advance;
        FreeTypeReference.Check(FT_Get_Advance(m_face, (uint)glyphIndex, FT_LOAD.FT_LOAD_NO_SCALE, &advance), "FT_Get_Advance");
        return (int)advance;
    }

    /// <summary><c>FT_Face_GetCharVariantIndex</c>.</summary>
    public int GetCharVariantIndex(int codePoint, int selector) =>
        (int)FT_Face_GetCharVariantIndex(m_face, (nuint)(uint)codePoint, (nuint)(uint)selector);

    /// <summary><c>FT_FACE_FLAG_SCALABLE</c>: FreeType scales the face's outlines.</summary>
    public bool IsScalable => ((long)m_face->face_flags & 1L) != 0;

    /// <summary><c>num_fixed_sizes</c>: the bitmap strikes FreeType offers.</summary>
    public int NumFixedSizes => m_face->num_fixed_sizes;

    /// <summary><c>FT_Set_Pixel_Sizes(face, 0, size)</c>.</summary>
    public void SetPixelSize(int size) => FreeTypeReference.Check(FT_Set_Pixel_Sizes(m_face, 0, (uint)size), "FT_Set_Pixel_Sizes");

    /// <summary><c>FT_Set_Pixel_Sizes(face, 0, size)</c>, returning its error instead of throwing.</summary>
    public FT_Error TrySetPixelSize(int size) => FT_Set_Pixel_Sizes(m_face, 0, (uint)size);

    /// <summary>The size's <c>FT_Size_Metrics</c> (26.6).</summary>
    public LineMetrics SizeMetrics
    {
        get
        {
            var metrics = m_face->size->metrics;
            return new LineMetrics((int)metrics.ascender, (int)metrics.descender, (int)metrics.height, (int)metrics.max_advance);
        }
    }

    /// <summary>
    /// <c>FT_Load_Glyph</c> unhinted, then <c>FT_Render_Glyph(FT_RENDER_MODE_NORMAL)</c>, returning the first error instead of
    /// throwing: for the cases where FreeType gives up and this library does not.
    /// </summary>
    public (FT_Error Load, FT_Error Render) TryLoadAndRender(int glyphIndex)
    {
        var flags = FT_LOAD.FT_LOAD_NO_BITMAP | FT_LOAD.FT_LOAD_NO_AUTOHINT | FT_LOAD.FT_LOAD_NO_HINTING;
        var load = FT_Load_Glyph(m_face, (uint)glyphIndex, flags);
        if (load != FT_Error.FT_Err_Ok)
            return (load, FT_Error.FT_Err_Ok);

        return (load, FT_Render_Glyph(m_face->glyph, FT_Render_Mode_.FT_RENDER_MODE_NORMAL));
    }

    /// <summary>
    /// <c>FT_Load_Glyph</c> with <c>FT_LOAD_NO_BITMAP | FT_LOAD_NO_AUTOHINT | FT_LOAD_TARGET_NORMAL</c>, plus
    /// <c>FT_LOAD_NO_HINTING</c> unless <paramref name="hinting"/>; then, if <paramref name="render"/>,
    /// <c>FT_Render_Glyph(FT_RENDER_MODE_NORMAL)</c>.
    /// </summary>
    public FreeTypeGlyph LoadGlyph(int glyphIndex, bool hinting, bool render)
    {
        var flags = FT_LOAD.FT_LOAD_NO_BITMAP | FT_LOAD.FT_LOAD_NO_AUTOHINT;
        if (!hinting)
            flags |= FT_LOAD.FT_LOAD_NO_HINTING;

        FreeTypeReference.Check(FT_Load_Glyph(m_face, (uint)glyphIndex, flags), "FT_Load_Glyph");

        var slot = m_face->glyph;
        var outline = ReadOutline(&slot->outline);
        var m = slot->metrics;
        var metrics = new GlyphMetrics((int)m.width, (int)m.height, (int)m.horiBearingX, (int)m.horiBearingY, (int)m.horiAdvance);

        FreeTypeBitmap? bitmap = null;
        if (render)
        {
            FreeTypeReference.Check(FT_Render_Glyph(slot, FT_Render_Mode_.FT_RENDER_MODE_NORMAL), "FT_Render_Glyph");
            var b = slot->bitmap;
            var width = (int)b.width;
            var height = (int)b.rows;
            var pixels = new byte[width * height];
            for (var y = 0; y < height; y++)
            {
                for (var x = 0; x < width; x++)
                    pixels[y * width + x] = b.buffer[y * b.pitch + x];
            }

            bitmap = new FreeTypeBitmap(width, height, slot->bitmap_left, slot->bitmap_top, pixels);
        }

        return new FreeTypeGlyph(outline, metrics, bitmap);
    }

    public void Dispose()
    {
        if (m_face is null)
            return;

        FT_Done_Face(m_face);
        m_face = null;
        m_pin.Free();
    }

    private static FreeTypeOutline ReadOutline(FT_Outline_* outline)
    {
        var points = new OutlinePoint[outline->n_points];
        for (var i = 0; i < points.Length; i++)
        {
            var kind = (outline->tags[i] & 3) switch
            {
                1 => OutlinePointKind.OnCurve,
                0 => OutlinePointKind.Quadratic,
                _ => OutlinePointKind.Cubic,
            };

            points[i] = new OutlinePoint((int)outline->points[i].x, (int)outline->points[i].y, kind);
        }

        var contours = new int[outline->n_contours];
        for (var i = 0; i < contours.Length; i++)
            contours[i] = outline->contours[i];

        return new FreeTypeOutline(points, contours);
    }

    private static string ReadString(byte* value) => value is null ? "" : Marshal.PtrToStringAnsi((nint)value) ?? "";
}
