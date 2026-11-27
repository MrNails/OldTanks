using System.Runtime.InteropServices;
using System.Text;
using FreeTypeSharp;
using static FreeTypeSharp.FT;

namespace CoolEngine.Fonts.Tests.Reference;

/// <summary>
/// FreeType 2.13.2 as the reference for TrueType hinting: one library set to interpreter version 35 or 40, faces opened at one
/// pixel size, glyphs loaded with <c>FT_LOAD_NO_BITMAP | FT_LOAD_NO_AUTOHINT | FT_LOAD_TARGET_NORMAL</c> (hinted), optionally
/// with <c>FT_LOAD_PEDANTIC</c>, returning FreeType's error instead of throwing. Each face is opened fresh, so its
/// <c>FT_Size</c> runs <c>fpgm</c> and <c>prep</c> as a new <see cref="ScaledFont"/> does. Not thread-safe.
/// </summary>
internal sealed unsafe class FreeTypeHinting : IDisposable
{
    private FT_LibraryRec_* m_library;

    /// <param name="version">The interpreter version: 35 or 40.</param>
    public FreeTypeHinting(int version)
    {
        FT_LibraryRec_* library;
        FreeTypeReference.Check(FT_Init_FreeType(&library), "FT_Init_FreeType");
        m_library = library;

        var module = Encoding.ASCII.GetBytes("truetype\0");
        var property = Encoding.ASCII.GetBytes("interpreter-version\0");
        var value = (uint)version;
        fixed (byte* modulePointer = module)
        fixed (byte* propertyPointer = property)
            FreeTypeReference.Check(FT_Property_Set(m_library, modulePointer, propertyPointer, &value), "FT_Property_Set");
    }

    /// <param name="version">The library's interpreter version for <paramref name="interpreter"/>.</param>
    public static int VersionNumber(TrueTypeInterpreterVersion interpreter) => interpreter == TrueTypeInterpreterVersion.V35 ? 35 : 40;

    /// <summary>Opens a face from bytes (pinned for its lifetime) and sets its pixel size.</summary>
    public HintedFreeTypeFace Open(byte[] data, int faceIndex, int pixelSize) => new(m_library, data, faceIndex, pixelSize);

    public void Dispose()
    {
        if (m_library is null)
            return;

        FT_Done_FreeType(m_library);
        m_library = null;
    }
}

/// <summary>One FreeType face at one pixel size, for hinted loads.</summary>
internal sealed unsafe class HintedFreeTypeFace : IDisposable
{
    private GCHandle m_pin;
    private FT_FaceRec_* m_face;

    public HintedFreeTypeFace(FT_LibraryRec_* library, byte[] data, int faceIndex, int pixelSize)
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
        FreeTypeReference.Check(FT_Set_Pixel_Sizes(m_face, 0, (uint)pixelSize), "FT_Set_Pixel_Sizes");
    }

    /// <summary><c>FT_FACE_FLAG_TRICKY</c>.</summary>
    public bool IsTricky => ((long)m_face->face_flags & (1L << 13)) != 0;

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
    /// A hinted <c>FT_Load_Glyph</c> (plus <c>FT_LOAD_PEDANTIC</c> if asked) and, if <paramref name="render"/>,
    /// <c>FT_Render_Glyph(FT_RENDER_MODE_NORMAL)</c>; FreeType's error, with the glyph when there is none.
    /// </summary>
    public FT_Error TryLoad(int glyphIndex, bool render, out FreeTypeGlyph? glyph, bool pedantic = false)
    {
        glyph = null;
        var flags = FT_LOAD.FT_LOAD_NO_BITMAP | FT_LOAD.FT_LOAD_NO_AUTOHINT;
        if (pedantic)
            flags |= FT_LOAD.FT_LOAD_PEDANTIC;

        var error = FT_Load_Glyph(m_face, (uint)glyphIndex, flags);
        if (error != FT_Error.FT_Err_Ok)
            return error;

        var slot = m_face->glyph;
        var outline = ReadOutline(&slot->outline);
        var m = slot->metrics;
        var metrics = new GlyphMetrics((int)m.width, (int)m.height, (int)m.horiBearingX, (int)m.horiBearingY, (int)m.horiAdvance);

        FreeTypeBitmap? bitmap = null;
        if (render)
        {
            error = FT_Render_Glyph(slot, FT_Render_Mode_.FT_RENDER_MODE_NORMAL);
            if (error != FT_Error.FT_Err_Ok)
                return error;

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

        glyph = new FreeTypeGlyph(outline, metrics, bitmap);
        return FT_Error.FT_Err_Ok;
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
}
