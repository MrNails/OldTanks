// Coordinate normalization follows FreeType 2.13.2 src/truetype/ttgxvar.c (TT_Set_Var_Design, ft_var_to_normalized) and the
// line metrics follow tt_apply_mvar (FreeType Project License; credit in docs).

using CoolEngine.Fonts.Sfnt;

namespace CoolEngine.Fonts.Variations;

/// <summary>The variation tables of a variable face (one whose <c>fvar</c> header FreeType accepts), checked at load. Immutable.</summary>
internal sealed class FaceVariations
{
    public required FvarTable Fvar { get; init; }

    /// <summary>The face's public axes and named instances (named from its own <c>name</c> table), read-only and shared.</summary>
    public required FvarNames Names { get; init; }

    /// <summary>Null without <c>avar</c>, or when FreeType ignores it.</summary>
    public AvarTable? Avar { get; init; }

    /// <summary>Null without <c>gvar</c> (always for a CFF2 face, whose outlines vary through <c>blend</c>).</summary>
    public GvarTable? Gvar { get; init; }

    /// <summary>Null without <c>cvar</c> or <c>cvt </c>, or when FreeType ignores it.</summary>
    public CvarTable? Cvar { get; init; }

    /// <summary>Null without <c>HVAR</c>, or when FreeType ignores it.</summary>
    public HvarTable? Hvar { get; init; }

    /// <summary>Null without <c>VVAR</c> (read only beside <c>vhea</c> and <c>vmtx</c>), or when FreeType ignores it.</summary>
    public HvarTable? Vvar { get; init; }

    /// <summary>Null without <c>MVAR</c>, or when FreeType ignores it.</summary>
    public MvarTable? Mvar { get; init; }

    public int AxisCount => Fvar.AxisCount;
}

/// <summary>A result that may be absent, so <see cref="ParsedTables.Get{T}"/> can share it.</summary>
internal sealed record Optional<T>(T? Value) where T : class;

/// <summary>
/// One set of variation coordinates of a face, as FreeType holds it after <c>FT_Set_Var_Design_Coordinates</c> with every axis
/// given: the normalized coordinates (FreeType's <c>blend->normalizedcoords</c>) and the face metrics <c>MVAR</c> changed.
/// Immutable; shared by the loaders of one <see cref="ScaledFont"/>.
/// </summary>
/// <remarks>
/// The user coordinates become 16.16 values (each rounded to the nearest 1/65536, halves away from zero), then are normalized
/// as <c>ft_var_to_normalized</c> normalizes them: clamped to the axis range, divided by the distance from the default to the
/// minimum or maximum with <c>FT_DivFix</c>, then mapped through <c>avar</c>. An axis not given stays at its default. Even at
/// the defaults the face then has a blend, as FreeType's has: its glyphs are scaled from unrounded 26.6 coordinates and its
/// hinting programs see GETINFO bit 10, GETVARIATION and GETDATA.
/// </remarks>
internal sealed class VariationInstance
{
    /// <summary>
    /// The work one set of coordinates may take to set up (the <c>MVAR</c> deltas: each record, and each region and axis of the
    /// records read), a library limit (Denys's "Glyph and size budgets"). Measured on 2026-11-27 over every variable face of
    /// <c>C:\Windows\Fonts</c> and the test fonts at every named instance and axis extreme: at most 5 units (SitkaVF-Italic.ttf).
    /// 100 times that is far below Denys's floor of 1,000,000 units: so 1,000,000.
    /// </summary>
    internal const long SetUpWorkBudget = 1_000_000;

    private const double MaxDesignMagnitude = 1L << 46;

    private VariationInstance(FaceVariations tables, long[] design, long[] normalized)
    {
        Tables = tables;
        Design = design;
        Normalized = normalized;
    }

    public FaceVariations Tables { get; }

    /// <summary>The design coordinates, 16.16, in axis order.</summary>
    public long[] Design { get; }

    /// <summary>The normalized coordinates after <c>avar</c>, 16.16, in axis order.</summary>
    public long[] Normalized { get; }

    /// <summary>The face's ascender after <c>MVAR</c> (FreeType's <c>face->ascender</c>), font units.</summary>
    public short Ascender { get; private init; }

    public short Descender { get; private init; }

    public short Height { get; private init; }

    /// <summary><c>OS/2</c>'s typographic ascender after <c>MVAR</c>'s <c>hasc</c> (the emulated vertical metrics read it).</summary>
    public short TypoAscender { get; private init; }

    public short TypoDescender { get; private init; }

    /// <summary>The coordinates of <paramref name="coordinates"/> for the face; every tag must name an axis of it.</summary>
    /// <exception cref="ArgumentOutOfRangeException">A tag names no axis, is repeated, or a value is not finite.</exception>
    /// <exception cref="InvalidDataException">The face's <c>avar</c> maps the coordinates outside -1..1, which FreeType refuses.</exception>
    public static VariationInstance Create(FontFace face, FaceVariations tables, IReadOnlyList<VariationCoordinate> coordinates)
    {
        var axes = tables.Fvar.Axes;
        var design = new long[axes.Length];
        for (var i = 0; i < axes.Length; i++)
            design[i] = axes[i].Default;

        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var coordinate in coordinates)
        {
            if (coordinate.Tag is null || !seen.Add(coordinate.Tag))
                throw new ArgumentOutOfRangeException(nameof(coordinates), coordinate.Tag,
                    "A variation coordinate names no axis tag, or names one twice.");

            if (!float.IsFinite(coordinate.Value))
                throw new ArgumentOutOfRangeException(nameof(coordinates), coordinate.Value,
                    $"The variation coordinate of '{coordinate.Tag}' is not a finite number.");

            var found = false;
            for (var i = 0; i < axes.Length; i++)
            {
                if (coordinate.Tag.Length != 4 || Tags.ToString(axes[i].Tag) != coordinate.Tag)
                    continue;

                design[i] = ToFixed(coordinate.Value);
                found = true;
            }

            if (!found)
                throw new ArgumentOutOfRangeException(nameof(coordinates), coordinate.Tag, $"The face has no '{coordinate.Tag}' axis.");
        }

        var normalized = Normalize(tables, design);

        // tt_set_mm_blend refuses a normalized coordinate outside -1..1 (only an avar map can make one), so
        // FT_Set_Var_Design_Coordinates fails for these coordinates.
        for (var i = 0; i < normalized.Length; i++)
        {
            if (normalized[i] is < -0x10000 or > 0x10000)
                throw new InvalidDataException(
                    $"The font's 'avar' maps the '{Tags.ToString(axes[i].Tag)}' coordinate to {normalized[i] / 65536.0}, outside " +
                    "-1..1; FreeType refuses such coordinates.");
        }

        var work = new WorkCounter(SetUpWorkBudget, "Setting up the variation coordinates");
        var (hasc, hdsc, hlgp) = tables.Mvar?.LineDeltas(normalized, work) ?? (0, 0, 0);
        var os2 = face.Data.Os2;

        // tt_apply_mvar: the deltas go to whatever ascender, descender and height the face chose, in FT_Short arithmetic.
        var lineGap = (short)(face.Height - face.Ascender + face.Descender);
        var ascender = (short)(face.Ascender + hasc);
        var descender = (short)(face.Descender + hdsc);
        return new VariationInstance(tables, design, normalized)
        {
            Ascender = ascender,
            Descender = descender,
            Height = (short)(ascender - descender + lineGap + hlgp),
            TypoAscender = (short)((os2?.TypoAscender ?? 0) + hasc),
            TypoDescender = (short)((os2?.TypoDescender ?? 0) + hdsc),
        };
    }

    /// <summary>A user value as a 16.16 <c>FT_Fixed</c>: rounded to the nearest 1/65536, halves away from zero.</summary>
    public static long ToFixed(float value)
    {
        var scaled = Math.Round(value * 65536.0, MidpointRounding.AwayFromZero);
        return (long)Math.Clamp(scaled, -MaxDesignMagnitude, MaxDesignMagnitude);
    }

    /// <summary><c>ft_var_to_normalized</c> for every axis, then <c>avar</c>.</summary>
    public static long[] Normalize(FaceVariations tables, ReadOnlySpan<long> design)
    {
        var axes = tables.Fvar.Axes;
        var normalized = new long[axes.Length];
        for (var i = 0; i < axes.Length; i++)
        {
            var (coord, axis) = (design[i], axes[i]);
            if (coord > axis.Default)
                normalized[i] = coord >= axis.Maximum ? 0x10000 : FixedMath.DivFix(coord - axis.Default, axis.Maximum - axis.Default);
            else if (coord < axis.Default)
                normalized[i] = coord <= axis.Minimum ? -0x10000 : FixedMath.DivFix(coord - axis.Default, axis.Default - axis.Minimum);
            else
                normalized[i] = 0;
        }

        tables.Avar?.Map(normalized);
        return normalized;
    }
}
