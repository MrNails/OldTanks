using System.Runtime.InteropServices;
using System.Text;
using FreeTypeSharp;
using static FreeTypeSharp.FT;

namespace CoolEngine.Fonts.Tests.Reference;

/// <summary>FreeType's <c>FT_Var_Axis</c> (FT_Fixed and FT_ULong are 64-bit in the reference DLL).</summary>
[StructLayout(LayoutKind.Sequential)]
internal unsafe struct FtVarAxis
{
    public byte* Name;
    public nint Minimum;
    public nint Default;
    public nint Maximum;
    public nuint Tag;
    public uint NameId;
}

/// <summary>FreeType's <c>FT_Var_Named_Style</c>.</summary>
[StructLayout(LayoutKind.Sequential)]
internal unsafe struct FtVarNamedStyle
{
    public nint* Coordinates;
    public uint NameId;
    public uint PostScriptNameId;
}

/// <summary>FreeType's <c>FT_MM_Var</c>.</summary>
[StructLayout(LayoutKind.Sequential)]
internal unsafe struct FtMMVar
{
    public uint AxisCount;
    public uint DesignCount;
    public uint NamedStyleCount;
    public FtVarAxis* Axes;
    public FtVarNamedStyle* NamedStyles;
}

/// <summary>The variation functions of FreeType 2.13.2 that FreeTypeSharp 3.1.0 does not bind, imported from its native DLL.</summary>
internal static unsafe partial class FreeTypeVariationsNative
{
    private const string Library = "freetype";

    [LibraryImport(Library)]
    public static partial FT_Error FT_Get_MM_Var(FT_FaceRec_* face, FtMMVar** master);

    [LibraryImport(Library)]
    public static partial FT_Error FT_Done_MM_Var(FT_LibraryRec_* library, FtMMVar* master);

    [LibraryImport(Library)]
    public static partial FT_Error FT_Set_Var_Design_Coordinates(FT_FaceRec_* face, uint count, nint* coordinates);

    [LibraryImport(Library)]
    public static partial FT_Error FT_Set_Named_Instance(FT_FaceRec_* face, uint instanceIndex);

    [LibraryImport(Library)]
    public static partial FT_Error FT_Get_Var_Axis_Flags(FtMMVar* master, uint axisIndex, uint* flags);

    [LibraryImport(Library)]
    public static partial FT_Error FT_Get_Var_Blend_Coordinates(FT_FaceRec_* face, uint count, nint* coordinates);
}

/// <summary>An axis as FreeType's <c>FT_MM_Var</c> gives it: 16.16 values, the name ID and the axis flags.</summary>
public sealed record FreeTypeAxis(string Tag, long Minimum, long Default, long Maximum, uint NameId, uint Flags);

/// <summary>A named instance as <c>FT_MM_Var</c> gives it: 16.16 coordinates and the subfamily name ID.</summary>
public sealed record FreeTypeInstance(long[] Coordinates, uint NameId);

/// <summary>
/// FreeType 2.13.2 as the reference for variable faces: one library (set to an interpreter version), faces opened fresh, given
/// design coordinates with <c>FT_Set_Var_Design_Coordinates</c> (every axis) and then a pixel size, as a
/// <see cref="ScaledFont"/> with <see cref="ScaledFontOptions.Variation"/> is made. Not thread-safe.
/// </summary>
internal sealed unsafe class FreeTypeVariations : IDisposable
{
    private FT_LibraryRec_* m_library;

    /// <param name="version">The TrueType interpreter version: 35 or 40.</param>
    public FreeTypeVariations(int version = 40)
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

    /// <summary>
    /// Opens a face (bytes pinned for its lifetime), sets the design coordinates (16.16, one per axis) unless null, then the
    /// pixel size unless 0.
    /// </summary>
    public VariedFreeTypeFace Open(byte[] data, int faceIndex, long[]? design, int pixelSize) =>
        new(m_library, data, faceIndex, design, pixelSize);

    /// <summary>The axes and named instances of <c>FT_Get_MM_Var</c>, read from a face of its own (the call gives a face a blend).</summary>
    public (FreeTypeAxis[] Axes, FreeTypeInstance[] Instances) ReadMMVar(byte[] data, int faceIndex)
    {
        using var face = Open(data, faceIndex, null, 0);
        return face.ReadMMVar(m_library);
    }

    public void Dispose()
    {
        if (m_library is null)
            return;

        FT_Done_FreeType(m_library);
        m_library = null;
    }
}

/// <summary>One FreeType face with design coordinates, at one pixel size.</summary>
internal sealed unsafe class VariedFreeTypeFace : IDisposable
{
    private GCHandle m_pin;
    private FT_FaceRec_* m_face;

    public VariedFreeTypeFace(FT_LibraryRec_* library, byte[] data, int faceIndex, long[]? design, int pixelSize)
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
        if (design is not null)
        {
            var coordinates = design.Select(value => (nint)value).ToArray();
            fixed (nint* pointer = coordinates)
                FreeTypeReference.Check(FreeTypeVariationsNative.FT_Set_Var_Design_Coordinates(m_face, (uint)coordinates.Length, pointer),
                    "FT_Set_Var_Design_Coordinates");
        }

        if (pixelSize > 0)
            FreeTypeReference.Check(FT_Set_Pixel_Sizes(m_face, 0, (uint)pixelSize), "FT_Set_Pixel_Sizes");
    }

    /// <summary><c>FT_FACE_FLAG_MULTIPLE_MASTERS</c>.</summary>
    public bool HasMultipleMasters => ((long)m_face->face_flags & (1L << 8)) != 0;

    /// <summary>The size's <c>FT_Size_Metrics</c> (26.6).</summary>
    public LineMetrics SizeMetrics
    {
        get
        {
            var metrics = m_face->size->metrics;
            return new LineMetrics((int)metrics.ascender, (int)metrics.descender, (int)metrics.height, (int)metrics.max_advance);
        }
    }

    /// <summary><c>FT_Get_Var_Blend_Coordinates</c>: the normalized coordinates FreeType uses (16.16), one per axis.</summary>
    public long[] BlendCoordinates(int axisCount)
    {
        var coordinates = new nint[axisCount];
        fixed (nint* pointer = coordinates)
            FreeTypeReference.Check(FreeTypeVariationsNative.FT_Get_Var_Blend_Coordinates(m_face, (uint)axisCount, pointer),
                "FT_Get_Var_Blend_Coordinates");

        return coordinates.Select(value => (long)value).ToArray();
    }

    /// <summary>
    /// <c>FT_Load_Glyph</c> with <c>FT_LOAD_NO_BITMAP | FT_LOAD_NO_AUTOHINT | FT_LOAD_TARGET_NORMAL</c>, plus
    /// <c>FT_LOAD_NO_HINTING</c> unless <paramref name="hinting"/>, then (if asked) <c>FT_Render_Glyph(FT_RENDER_MODE_NORMAL)</c>;
    /// FreeType's error, with the glyph when there is none.
    /// </summary>
    public FT_Error TryLoad(int glyphIndex, bool hinting, bool render, out FreeTypeGlyph? glyph)
    {
        glyph = null;
        var flags = FT_LOAD.FT_LOAD_NO_BITMAP | FT_LOAD.FT_LOAD_NO_AUTOHINT;
        if (!hinting)
            flags |= FT_LOAD.FT_LOAD_NO_HINTING;

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

    /// <summary><c>FT_Get_MM_Var</c>, its axis flags (<c>FT_Get_Var_Axis_Flags</c>), then <c>FT_Done_MM_Var</c>.</summary>
    public (FreeTypeAxis[] Axes, FreeTypeInstance[] Instances) ReadMMVar(FT_LibraryRec_* library)
    {
        FtMMVar* master;
        FreeTypeReference.Check(FreeTypeVariationsNative.FT_Get_MM_Var(m_face, &master), "FT_Get_MM_Var");
        try
        {
            var axes = new FreeTypeAxis[master->AxisCount];
            for (var i = 0; i < axes.Length; i++)
            {
                var axis = master->Axes[i];
                uint flags;
                FreeTypeReference.Check(FreeTypeVariationsNative.FT_Get_Var_Axis_Flags(master, (uint)i, &flags), "FT_Get_Var_Axis_Flags");
                var tag = (uint)axis.Tag;
                var tagText = new string([(char)(tag >> 24), (char)((tag >> 16) & 0xFF), (char)((tag >> 8) & 0xFF), (char)(tag & 0xFF)]);
                axes[i] = new FreeTypeAxis(tagText, axis.Minimum, axis.Default, axis.Maximum, axis.NameId, flags);
            }

            var instances = new FreeTypeInstance[master->NamedStyleCount];
            for (var i = 0; i < instances.Length; i++)
            {
                var style = master->NamedStyles[i];
                var coordinates = new long[axes.Length];
                for (var j = 0; j < coordinates.Length; j++)
                    coordinates[j] = style.Coordinates[j];

                instances[i] = new FreeTypeInstance(coordinates, style.NameId);
            }

            return (axes, instances);
        }
        finally
        {
            FreeTypeVariationsNative.FT_Done_MM_Var(library, master);
        }
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
