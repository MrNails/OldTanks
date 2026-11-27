// Follows FreeType 2.13.2 src/truetype/ttobjs.h (TT_GraphicsState) and src/truetype/ttinterp.c (tt_default_graphics_state)
// (FreeType Project License; credit in docs).

namespace CoolEngine.Fonts.Hinting.TrueType;

/// <summary>
/// The TrueType graphics state, field for field as FreeType 2.13.2's <c>TT_GraphicsState</c>: reference points, the three
/// unit vectors (2.14), the loop counter, rounding, cut-ins, delta settings, <c>INSTCTRL</c> flags and zone pointers.
/// </summary>
internal struct GraphicsState
{
    public ushort Rp0;
    public ushort Rp1;
    public ushort Rp2;

    public short DualVectorX;
    public short DualVectorY;
    public short ProjVectorX;
    public short ProjVectorY;
    public short FreeVectorX;
    public short FreeVectorY;

    public long Loop;
    public long MinimumDistance;
    public int RoundState;
    public bool AutoFlip;
    public long ControlValueCutIn;
    public long SingleWidthCutIn;
    public long SingleWidthValue;
    public ushort DeltaBase;
    public ushort DeltaShift;
    public byte InstructControl;
    public bool ScanControl;
    public int ScanType;

    public ushort Gep0;
    public ushort Gep1;
    public ushort Gep2;

    /// <summary>FreeType's <c>tt_default_graphics_state</c>.</summary>
    public static GraphicsState Default => new()
    {
        DualVectorX = 0x4000,
        ProjVectorX = 0x4000,
        FreeVectorX = 0x4000,
        Loop = 1,
        MinimumDistance = 64,
        RoundState = RoundMode.ToGrid,
        AutoFlip = true,
        ControlValueCutIn = 68,
        DeltaBase = 9,
        DeltaShift = 3,
        Gep0 = 1,
        Gep1 = 1,
        Gep2 = 1,
    };
}

/// <summary>The rounding states, numbered as FreeType's <c>TT_Round_*</c>.</summary>
internal static class RoundMode
{
    public const int ToHalfGrid = 0;
    public const int ToGrid = 1;
    public const int ToDoubleGrid = 2;
    public const int DownToGrid = 3;
    public const int UpToGrid = 4;
    public const int Off = 5;
    public const int Super = 6;
    public const int Super45 = 7;
}

/// <summary>
/// A glyph zone as FreeType's <c>TT_GlyphZoneRec</c>: original, current and unscaled points, tags and contour ends. The
/// twilight zone and the glyph zone are two instances; a zone pointer of the interpreter references one of them.
/// </summary>
internal sealed class HintZone
{
    /// <summary>Point flags kept in <see cref="Tags"/>: on curve, touched in x, touched in y.</summary>
    public const byte TagOn = 0x01;
    public const byte TouchX = 0x08;
    public const byte TouchY = 0x10;
    public const byte TouchBoth = TouchX | TouchY;

    public long[] OrgX = [];
    public long[] OrgY = [];
    public long[] CurX = [];
    public long[] CurY = [];
    public long[] OrusX = [];
    public long[] OrusY = [];
    public byte[] Tags = [];

    /// <summary>Contour ends as FreeType keeps them: indices into the whole outline, so <see cref="FirstPoint"/> is subtracted.</summary>
    public int[] Contours = [];

    /// <summary>The usable number of points (FreeType's <c>n_points</c>); the arrays may be longer.</summary>
    public int PointCount;

    public int ContourCount;

    /// <summary>The zone's first point in the whole outline (non-zero only for a nested composite's own program).</summary>
    public int FirstPoint;

    /// <summary>A zone of <paramref name="count"/> zeroed points and no contours (the twilight zone).</summary>
    public static HintZone Allocate(int count) => new()
    {
        OrgX = new long[count],
        OrgY = new long[count],
        CurX = new long[count],
        CurY = new long[count],
        OrusX = new long[count],
        OrusY = new long[count],
        Tags = new byte[count],
        PointCount = count,
    };
}
