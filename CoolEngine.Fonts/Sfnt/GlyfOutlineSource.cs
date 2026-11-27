// Follows FreeType 2.13.2 src/truetype/ttgload.c (load_truetype_glyph, tt_get_metrics, TT_Process_Simple_Glyph,
// TT_Process_Composite_Component, TT_Process_Composite_Glyph, tt_loader_set_pp, compute_glyph_metrics, and their variation
// paths), src/truetype/ttmtx.c (TT_Get_VMetrics) and src/sfnt/ttmtx.c (tt_face_get_metrics with the HVAR and VVAR advance
// adjustments) (FreeType Project License; credit in docs).

using CoolEngine.Fonts.Hinting.TrueType;
using CoolEngine.Fonts.Variations;

namespace CoolEngine.Fonts.Sfnt;

/// <summary>
/// TrueType outlines from <c>glyf</c>, scaled to 26.6 exactly as FreeType 2.13.2's <c>ttgload.c</c> scales them: each point and
/// each phantom point through <c>FT_MulFix</c>, composite components transformed with <c>FT_MulFix</c> on 16.16 matrices and
/// offset in pixels, the outline shifted so the first phantom point (x minimum minus the left side bearing) is the origin.
/// With variation coordinates, the glyph's <c>gvar</c> deltas apply before scaling, as FreeType applies them.
/// </summary>
internal sealed class GlyfOutlineSource : IOutlineSource
{
    private readonly FaceData m_face;

    public GlyfOutlineSource(FaceData face) => m_face = face;

    public IGlyphLoader CreateLoader(ScaledFontOptions options, FontScale scale, VariationInstance? variation)
    {
        var hinter = options.Hinting == HintingMode.Native
            ? TrueTypeHinterFactory.Create(m_face, scale, options.Interpreter, variation)
            : null;

        return new GlyfGlyphLoader(m_face, scale, hinter, variation);
    }
}

/// <summary>Loads glyphs of one face at one size; reuses its buffers, so not thread-safe.</summary>
/// <remarks>
/// With variation coordinates (FreeType's face after <c>FT_Set_Var_Design_Coordinates</c>, not its default instance), each
/// glyph load follows FreeType's variation paths: the <c>hmtx</c> advance adjusted by <c>HVAR</c> (and a <c>vmtx</c> one by
/// <c>VVAR</c>), the vertical metrics emulated from <c>MVAR</c>'s typographic ascender and descender; the glyph's <c>gvar</c>
/// deltas (<see cref="GvarTable.ComputeDeltas"/>) added to its points and phantom points (a composite's: to its component
/// offsets and phantom points) in font units, rounded (<c>FT_fixedToInt</c>) and unrounded (<c>FT_fixedToFdot6</c>); a simple
/// glyph scaled from the unrounded 26.6 points (<c>(FT_MulFix(u, scale) + 32) &gt;&gt; 6</c>), even at the defaults; with
/// <c>HVAR</c> (<c>VVAR</c>) the phantom points' deltas dropped, and a hinted simple glyph's horizontal (vertical) phantom
/// points scaled from the unvaried ones, as FreeType does to avoid adjusting an advance twice. The variation work of one glyph
/// load (the deltas read, the tuples' scalars, the interpolation) is charged to the hinter's glyph budget when hinting, else to
/// <see cref="VariationWorkBudget"/>: past it, <see cref="NotSupportedException"/> (a library limit; Denys's "Glyph and size
/// budgets", 2026-11-27).
/// </remarks>
internal sealed class GlyfGlyphLoader : IGlyphLoader, IWorkCounter
{
    /// <summary>
    /// The variation work one unhinted glyph load may charge (see the remarks). Measured on 2026-11-27 over every variable
    /// TrueType face of <c>C:\Windows\Fonts</c> (bahnschrift.ttf, CascadiaCode.ttf, CascadiaMono.ttf, SegUIVar.ttf, SitkaVF.ttf,
    /// SitkaVF-Italic.ttf) and SourceSans3VF-Upright.ttf, every glyph at every named instance and each axis's extremes: the
    /// largest was 2,539 units (SitkaVF-Italic.ttf glyph 560 at its second instance). About 100 times that is 253,900, below
    /// Denys's floor of 1,000,000 units: so 1,000,000. (Hinted loads charge the hinter's glyph budget instead, the programs and
    /// the variations together: at most 4,299 units measured, SitkaVF.ttf glyph 217, v40.)
    /// </summary>
    internal const long VariationWorkBudget = 1_000_000;

    private const int PhantomCount = 4;

    // The point arrays one composite program run copies: x, y and tags in the loader, original and unscaled x and y in the
    // hinter.
    private const int CompositeZoneArrays = 7;

    private readonly FaceData m_face;
    private readonly GlyfTable m_glyf;
    private readonly FontScale m_scale;
    private readonly ITrueTypeHinter? m_hinter;
    private readonly SimpleGlyph m_simple = new();

    // The variation coordinates, and what of them the loads read; null without coordinates.
    private readonly VariationInstance? m_variation;
    private readonly GvarTable? m_gvar;
    private readonly HvarTable? m_hvar;
    private readonly HvarTable? m_vvar;
    private readonly DeltaScratch? m_scratch;
    private readonly long m_variationBudget;
    private long m_variationWork;
    private int m_glyph;

    // The outline being built (FreeType's gloader base): 26.6 points, on-curve tags and contour ends.
    private long[] m_x = new long[64];
    private long[] m_y = new long[64];
    private byte[] m_tags = new byte[64];
    private int[] m_contours = new int[16];
    private int m_pointCount;
    private int m_contourCount;

    // The phantom points of the glyph being loaded: pp1, pp2 (horizontal) and pp3, pp4 (vertical).
    private Phantoms m_phantoms;
    private bool m_overlap;

    // Whether this load runs glyph programs (FreeType's IS_HINTED on the loader's flags): false without a hinter, or when prep
    // turned hinting off with INSTCTRL.
    private bool m_hinted;

    public GlyfGlyphLoader(FaceData face, FontScale scale, ITrueTypeHinter? hinter, VariationInstance? variation = null)
        : this(face, scale, hinter, variation, VariationWorkBudget)
    {
    }

    /// <summary>A loader with another variation work budget (to measure the work real fonts take, and in tests).</summary>
    internal GlyfGlyphLoader(FaceData face, FontScale scale, ITrueTypeHinter? hinter, VariationInstance? variation,
        long variationBudget)
    {
        m_face = face;
        m_glyf = face.Glyf!;
        m_scale = scale;
        m_hinter = hinter;
        m_variation = variation;
        m_variationBudget = variationBudget;
        if (variation is not null)
        {
            m_gvar = variation.Tables.Gvar;
            m_hvar = variation.Tables.Hvar;
            m_vvar = face.Vmtx is null ? null : variation.Tables.Vvar;
            m_scratch = new DeltaScratch();
        }
    }

    /// <summary>The variation work the last load charged to the loader's own budget (unhinted loads; for measurements).</summary>
    internal long VariationWork => m_variationWork;

    public LoadedGlyph Load(int glyphIndex)
    {
        m_pointCount = 0;
        m_contourCount = 0;
        m_overlap = false;
        m_variationWork = 0;
        m_glyph = glyphIndex;

        // tt_loader_init: throws when fpgm or prep failed, as FreeType fails every hinted load then.
        m_hinted = m_hinter?.BeginGlyph() ?? false;

        LoadGlyph(glyphIndex, isTopLevel: true);

        // Translate so that pp1 is the origin (FreeType's TT_Load_Glyph), whatever head.flags bit 1 says.
        var originX = m_phantoms.X1;
        var points = new OutlinePoint[m_pointCount];
        long xMin = 0, yMin = 0, xMax = 0, yMax = 0;
        for (var i = 0; i < m_pointCount; i++)
        {
            var x = m_x[i] - originX;
            var y = m_y[i];
            points[i] = new OutlinePoint((int)x, (int)y,
                (m_tags[i] & GlyfTable.OnCurvePoint) != 0 ? OutlinePointKind.OnCurve : OutlinePointKind.Quadratic);

            if (i == 0)
            {
                xMin = xMax = x;
                yMin = yMax = y;
                continue;
            }

            xMin = Math.Min(xMin, x);
            xMax = Math.Max(xMax, x);
            yMin = Math.Min(yMin, y);
            yMax = Math.Max(yMax, y);
        }

        var outline = m_pointCount == 0 ? GlyphOutline.Empty : new GlyphOutline(points, m_contours[..m_contourCount]);

        // FreeType's compute_glyph_metrics: the control box of the final outline, the advance from the phantom points.
        var metrics = new GlyphMetrics((int)(xMax - xMin), (int)(yMax - yMin), (int)xMin, (int)yMax,
            (int)(m_phantoms.X2 - m_phantoms.X1));

        if (m_hinter is not null)
            metrics = m_hinter.FitMetrics(glyphIndex, metrics);

        return new LoadedGlyph(outline, metrics, m_overlap);
    }

    /// <summary>
    /// Charges the load's variation work: to the hinter's glyph budget when hinting (shared with the programs), else to this
    /// loader's own.
    /// </summary>
    public void Charge(long units)
    {
        if (m_hinter is not null)
        {
            m_hinter.ChargeWork(units);
            return;
        }

        m_variationWork += units;
        if (m_variationWork > m_variationBudget)
            throw new NotSupportedException(
                $"Glyph {m_glyph}'s variations take more work than this library allows for one glyph load ({m_variationBudget} " +
                $"units; charged {m_variationWork}).");
    }

    /// <summary>FreeType's <c>load_truetype_glyph</c>: appends the glyph's points and leaves its phantom points in <see cref="m_phantoms"/>.</summary>
    private void LoadGlyph(int glyphIndex, bool isTopLevel)
    {
        var data = m_glyf.GetGlyph(glyphIndex);
        var r = new BigEndianReader(data, "glyf");
        var contourCount = data.Length == 0 ? 0 : r.I16(0);
        var empty = contourCount == 0;

        // load_truetype_glyph (loader->bbox): an empty glyph (no data, or a header of 0 contours) has a zero box ("a space
        // glyph"), whatever an earlier glyph of the load or its own header says; any other takes its header's.
        SetPhantoms(glyphIndex, empty ? 0 : r.I16(2), empty ? 0 : r.I16(8));

        if (empty)
        {
            // An empty glyph varies its phantom points only (no outline, no contours).
            if (m_variation is not null)
            {
                Span<long> x = [m_phantoms.X1, m_phantoms.X2, m_phantoms.X3, m_phantoms.X4];
                Span<long> y = [m_phantoms.Y1, m_phantoms.Y2, m_phantoms.Y3, m_phantoms.Y4];
                VaryRounded(glyphIndex, x, y, 0, []);
            }

            m_phantoms = ScalePhantoms(m_phantoms);
            return;
        }

        if (contourCount > 0)
            LoadSimple(data, glyphIndex);
        else
            LoadComposite(data, glyphIndex, isTopLevel);
    }

    /// <summary>
    /// FreeType's <c>tt_get_metrics</c> and <c>tt_loader_set_pp</c>, in font units, with the vertical metrics emulated from
    /// <c>OS/2</c> or <c>hhea</c>; with variation coordinates, the advances adjusted by <c>HVAR</c> and <c>VVAR</c> and the emulated
    /// vertical metrics read from <c>MVAR</c>'s values.
    /// </summary>
    private void SetPhantoms(int glyphIndex, int xMin, int yMax)
    {
        var (advance, bearing) = m_face.Hmtx.Get(glyphIndex);
        if (m_hvar is not null)
            advance = (ushort)(advance + m_hvar.AdvanceDelta(glyphIndex, m_variation!.Normalized, this));

        // TT_Get_VMetrics: vmtx when the face has vertical metrics, else the typographic (or hhea) ascender and descender.
        short topBearing;
        ushort advanceHeight;
        if (m_face.Vmtx is { } vmtx)
        {
            (advanceHeight, topBearing) = vmtx.Get(glyphIndex);
            if (m_vvar is not null)
                advanceHeight = (ushort)(advanceHeight + m_vvar.AdvanceDelta(glyphIndex, m_variation!.Normalized, this));
        }
        else
        {
            short ascender, descender;
            if (m_face.Os2 is { } os2)
                (ascender, descender) = m_variation is null
                    ? (os2.TypoAscender, os2.TypoDescender)
                    : (m_variation.TypoAscender, m_variation.TypoDescender);
            else
                (ascender, descender) = (m_face.Hhea.Ascender, m_face.Hhea.Descender);

            topBearing = (short)(ascender - yMax);
            advanceHeight = (ushort)Math.Abs(ascender - descender);
        }

        long x1 = xMin - bearing;
        long x3 = 0;

        // FreeType's v40 grayscale hinting puts the vertical phantom points at half the advance.
        if (m_hinter is { Version: TrueTypeInterpreterVersion.V40 })
            x3 = advance / 2;

        long y3 = yMax + topBearing;
        m_phantoms = new Phantoms(x1, 0, x1 + advance, 0, x3, y3, x3, y3 - advanceHeight);
    }

    /// <summary>The phantom points scaled as <c>load_truetype_glyph</c> scales them: pp1.y and pp2.y are left as they are.</summary>
    private Phantoms ScalePhantoms(Phantoms p) => p with
    {
        X1 = FixedMath.MulFix(p.X1, m_scale.XScale),
        X2 = FixedMath.MulFix(p.X2, m_scale.XScale),
        X3 = FixedMath.MulFix(p.X3, m_scale.XScale),
        Y3 = FixedMath.MulFix(p.Y3, m_scale.YScale),
        X4 = FixedMath.MulFix(p.X4, m_scale.XScale),
        Y4 = FixedMath.MulFix(p.Y4, m_scale.YScale),
    };

    /// <summary>
    /// <c>TT_Vary_Apply_Glyph_Deltas</c> for an empty or composite glyph: the rounded deltas added to the points in font units,
    /// the phantom points (the last four) taken as the glyph's unless <c>HVAR</c> (<c>VVAR</c>) varies its advances.
    /// </summary>
    /// <param name="glyphIndex">The glyph.</param>
    /// <param name="x">The points' x in font units, the phantom points last; changed in place.</param>
    /// <param name="y">The points' y.</param>
    /// <param name="pointCount">The points before the phantom points.</param>
    /// <param name="contourEnds">The contours of those points.</param>
    private void VaryRounded(int glyphIndex, Span<long> x, Span<long> y, int pointCount, ReadOnlySpan<int> contourEnds)
    {
        if (!ComputeDeltas(glyphIndex, x, y, pointCount, contourEnds))
            return;

        var scratch = m_scratch!;
        for (var i = 0; i < pointCount + PhantomCount; i++)
        {
            x[i] += FixedMath.FixedToInt(scratch.DeltaX[i]);
            y[i] += FixedMath.FixedToInt(scratch.DeltaY[i]);
        }

        TakeVariedPhantoms(x, y, pointCount);
    }

    /// <summary>The deltas of the glyph's points (phantom points last), with those of the phantom points <c>HVAR</c> or <c>VVAR</c> vary dropped.</summary>
    private bool ComputeDeltas(int glyphIndex, ReadOnlySpan<long> x, ReadOnlySpan<long> y, int pointCount, ReadOnlySpan<int> contourEnds)
    {
        if (m_gvar is null)
            return false;

        var count = pointCount + PhantomCount;
        var scratch = m_scratch!;
        if (!m_gvar.ComputeDeltas(glyphIndex, m_variation!.Normalized, x, y, count, contourEnds, scratch, this))
            return false;

        if (m_hvar is not null)
        {
            scratch.DeltaX[count - 4] = scratch.DeltaY[count - 4] = 0;
            scratch.DeltaX[count - 3] = scratch.DeltaY[count - 3] = 0;
        }

        if (m_vvar is not null)
        {
            scratch.DeltaX[count - 2] = scratch.DeltaY[count - 2] = 0;
            scratch.DeltaX[count - 1] = scratch.DeltaY[count - 1] = 0;
        }

        return true;
    }

    /// <summary>The end of <c>TT_Vary_Apply_Glyph_Deltas</c>: the loader's phantom points from the varied points, unless HVAR or VVAR.</summary>
    private void TakeVariedPhantoms(ReadOnlySpan<long> x, ReadOnlySpan<long> y, int pointCount)
    {
        var n = pointCount;
        if (m_hvar is null)
            m_phantoms = m_phantoms with { X1 = x[n], Y1 = y[n], X2 = x[n + 1], Y2 = y[n + 1] };

        if (m_vvar is null)
            m_phantoms = m_phantoms with { X3 = x[n + 2], Y3 = y[n + 2], X4 = x[n + 3], Y4 = y[n + 3] };
    }

    /// <summary>FreeType's <c>TT_Load_Simple_Glyph</c> and <c>TT_Process_Simple_Glyph</c>.</summary>
    private void LoadSimple(ReadOnlySpan<byte> data, int glyphIndex)
    {
        var glyph = m_simple;
        GlyfTable.ReadSimple(data, glyphIndex, glyph);
        var n = glyph.PointCount;
        var total = n + PhantomCount;

        // The points and the phantom points in font units.
        var x = new long[total];
        var y = new long[total];
        long[]? unscaledX = m_hinted ? new long[total] : null;
        long[]? unscaledY = m_hinted ? new long[total] : null;
        for (var i = 0; i < n; i++)
        {
            x[i] = glyph.X[i];
            y[i] = glyph.Y[i];
        }

        var p = m_phantoms;
        (x[n], y[n]) = (p.X1, p.Y1);
        (x[n + 1], y[n + 1]) = (p.X2, p.Y2);
        (x[n + 2], y[n + 2]) = (p.X3, p.Y3);
        (x[n + 3], y[n + 3]) = (p.X4, p.Y4);

        // Deltas apply to the unscaled data: rounded to the points, unrounded (26.6) to what is scaled.
        long[]? unroundedX = null, unroundedY = null;
        if (m_variation is not null)
        {
            unroundedX = new long[total];
            unroundedY = new long[total];
            for (var i = 0; i < total; i++)
            {
                unroundedX[i] = x[i] * 64;
                unroundedY[i] = y[i] * 64;
            }

            if (ComputeDeltas(glyphIndex, x, y, n, glyph.ContourEnds.AsSpan(0, glyph.ContourCount)))
            {
                var scratch = m_scratch!;
                for (var i = 0; i < total; i++)
                {
                    unroundedX[i] += FixedMath.FixedToFdot6(scratch.DeltaX[i]);
                    unroundedY[i] += FixedMath.FixedToFdot6(scratch.DeltaY[i]);
                    x[i] += FixedMath.FixedToInt(scratch.DeltaX[i]);
                    y[i] += FixedMath.FixedToInt(scratch.DeltaY[i]);
                }

                TakeVariedPhantoms(x, y, n);
            }
        }

        if (unscaledX is not null)
        {
            Array.Copy(x, unscaledX, total);
            Array.Copy(y, unscaledY!, total);
        }

        // Scale every point and the phantom points: a varied glyph from its unrounded 26.6 coordinates.
        if (unroundedX is not null)
        {
            for (var i = 0; i < total; i++)
            {
                x[i] = (FixedMath.MulFix(unroundedX[i], m_scale.XScale) + 32) >> 6;
                y[i] = (FixedMath.MulFix(unroundedY![i], m_scale.YScale) + 32) >> 6;
            }
        }
        else
        {
            for (var i = 0; i < total; i++)
            {
                x[i] = FixedMath.MulFix(x[i], m_scale.XScale);
                y[i] = FixedMath.MulFix(y[i], m_scale.YScale);
            }
        }

        var tags = new byte[total];
        for (var i = 0; i < n; i++)
            tags[i] = (byte)(glyph.Flags[i] & GlyfTable.OnCurvePoint);

        // The phantom points: the scaled ones, except that with HVAR (VVAR) a hinted load scales the unvaried pp1 and pp2 (pp3
        // and pp4) instead.
        var phantoms = new Phantoms(x[n], y[n], x[n + 1], y[n + 1], x[n + 2], y[n + 2], x[n + 3], y[n + 3]);
        if (m_hinted && m_hvar is not null)
            phantoms = phantoms with
            {
                X1 = FixedMath.MulFix(m_phantoms.X1, m_scale.XScale), Y1 = m_phantoms.Y1,
                X2 = FixedMath.MulFix(m_phantoms.X2, m_scale.XScale), Y2 = m_phantoms.Y2,
            };

        if (m_hinted && m_vvar is not null)
            phantoms = phantoms with
            {
                X3 = FixedMath.MulFix(m_phantoms.X3, m_scale.XScale), Y3 = FixedMath.MulFix(m_phantoms.Y3, m_scale.YScale),
                X4 = FixedMath.MulFix(m_phantoms.X4, m_scale.XScale), Y4 = FixedMath.MulFix(m_phantoms.Y4, m_scale.YScale),
            };

        m_phantoms = phantoms;

        if (m_hinted)
        {
            var zone = new GlyphZone(x, y, unscaledX, unscaledY, tags, glyph.ContourEnds[..glyph.ContourCount], total,
                glyph.ContourCount);
            var instructions = data.Slice(glyph.InstructionOffset, glyph.InstructionLength);
            if (m_hinter!.HintGlyph(zone, instructions, isComposite: false))
                m_phantoms = new Phantoms(x[n], y[n], x[n + 1], y[n + 1], x[n + 2], y[n + 2], x[n + 3], y[n + 3]);
        }

        if ((glyph.Flags[0] & GlyfTable.OverlapSimple) != 0)
            m_overlap = true;

        // Append to the outline (FT_GlyphLoader_Add).
        EnsurePoints(m_pointCount + n);
        EnsureContours(m_contourCount + glyph.ContourCount);
        Array.Copy(x, 0, m_x, m_pointCount, n);
        Array.Copy(y, 0, m_y, m_pointCount, n);
        Array.Copy(tags, 0, m_tags, m_pointCount, n);
        for (var c = 0; c < glyph.ContourCount; c++)
            m_contours[m_contourCount + c] = glyph.ContourEnds[c] + m_pointCount;

        m_pointCount += n;
        m_contourCount += glyph.ContourCount;
    }

    /// <summary>The composite branch of FreeType's <c>load_truetype_glyph</c>.</summary>
    private void LoadComposite(ReadOnlySpan<byte> data, int glyphIndex, bool isTopLevel)
    {
        var components = new List<GlyphComponent>();
        var instructions = GlyfTable.ReadComposite(data, glyphIndex, m_face.GlyphCount, components);
        var instructionBytes = data.Slice(instructions.Offset, instructions.Length).ToArray();

        // FT_OUTLINE_OVERLAP comes from the first component of the glyph asked for.
        if (isTopLevel && (components[0].Flags & GlyfTable.OverlapCompound) != 0)
            m_overlap = true;

        if (m_variation is not null)
            VaryComposite(glyphIndex, components);

        m_phantoms = ScalePhantoms(m_phantoms);

        var startPoint = m_pointCount;
        var startContour = m_contourCount;

        foreach (var component in components)
        {
            var saved = m_phantoms;
            var basePoints = m_pointCount;

            LoadGlyph(component.GlyphIndex, isTopLevel: false);

            if ((component.Flags & GlyfTable.UseMyMetrics) == 0)
                m_phantoms = saved;

            if (m_pointCount == basePoints)
                continue;

            PlaceComponent(component, startPoint, basePoints);
        }

        // FreeType hints a composite only when its last component announces instructions and there are some.
        var last = components[^1];
        if (m_hinted && (last.Flags & GlyfTable.WeHaveInstructions) != 0 && instructionBytes.Length > 0 &&
            m_pointCount > startPoint)
            HintComposite(startPoint, startContour, instructionBytes);
    }

    /// <summary>
    /// The variation step of <c>load_truetype_glyph</c>'s composite branch: one point per component record (its two arguments)
    /// and the phantom points, each component a contour of its own; the deltas give each offset component (ARGS_ARE_XY_VALUES)
    /// new arguments, kept in 16 bits as FreeType keeps them, and the glyph new phantom points.
    /// </summary>
    private void VaryComposite(int glyphIndex, List<GlyphComponent> components)
    {
        var limit = components.Count;
        var x = new long[limit + PhantomCount];
        var y = new long[limit + PhantomCount];
        var contours = new int[limit];
        for (var i = 0; i < limit; i++)
        {
            x[i] = components[i].Argument1;
            y[i] = components[i].Argument2;
            contours[i] = i;
        }

        var p = m_phantoms;
        (x[limit], y[limit]) = (p.X1, p.Y1);
        (x[limit + 1], y[limit + 1]) = (p.X2, p.Y2);
        (x[limit + 2], y[limit + 2]) = (p.X3, p.Y3);
        (x[limit + 3], y[limit + 3]) = (p.X4, p.Y4);

        VaryRounded(glyphIndex, x, y, limit, contours);

        for (var i = 0; i < limit; i++)
        {
            if ((components[i].Flags & GlyfTable.ArgsAreXyValues) != 0)
                components[i] = components[i] with { Argument1 = (short)x[i], Argument2 = (short)y[i] };
        }
    }

    /// <summary>FreeType's <c>TT_Process_Composite_Component</c>: transform, then offset, the points of the component just loaded.</summary>
    private void PlaceComponent(GlyphComponent component, int startPoint, int basePoints)
    {
        var flags = component.Flags;
        var haveScale = (flags & (GlyfTable.WeHaveAScale | GlyfTable.WeHaveAnXyScale | GlyfTable.WeHaveATwoByTwo)) != 0;

        if (haveScale)
        {
            for (var i = basePoints; i < m_pointCount; i++)
            {
                var px = m_x[i];
                var py = m_y[i];
                m_x[i] = FixedMath.MulFix(px, component.Xx) + FixedMath.MulFix(py, component.Xy);
                m_y[i] = FixedMath.MulFix(px, component.Yx) + FixedMath.MulFix(py, component.Yy);
            }
        }

        long dx, dy;
        if ((flags & GlyfTable.ArgsAreXyValues) == 0)
        {
            // Anchor points, both checked at load: point Argument1 of the composite so far meets point Argument2 of this one.
            var k = startPoint + component.Argument1;
            var l = basePoints + component.Argument2;
            dx = m_x[k] - m_x[l];
            dy = m_y[k] - m_y[l];
        }
        else
        {
            dx = component.Argument1;
            dy = component.Argument2;
            if (dx == 0 && dy == 0)
                return;

            // Without TT_CONFIG_OPTION_COMPONENT_OFFSET_SCALED (FreeType's default), only SCALED_COMPONENT_OFFSET scales.
            if (haveScale && (flags & GlyfTable.ScaledComponentOffset) != 0)
            {
                dx = FixedMath.MulFix(dx, FixedMath.Hypot(component.Xx, component.Xy));
                dy = FixedMath.MulFix(dy, FixedMath.Hypot(component.Yy, component.Yx));
            }

            dx = FixedMath.MulFix(dx, m_scale.XScale);
            dy = FixedMath.MulFix(dy, m_scale.YScale);

            if ((flags & GlyfTable.RoundXyToGrid) != 0 && m_hinted)
            {
                // The x offset is rounded only in v35, where advance widths are hinted too.
                if (m_hinter!.Version == TrueTypeInterpreterVersion.V35)
                    dx = FixedMath.PixRound(dx);

                dy = FixedMath.PixRound(dy);
            }
        }

        if (dx == 0 && dy == 0)
            return;

        for (var i = basePoints; i < m_pointCount; i++)
        {
            m_x[i] += dx;
            m_y[i] += dy;
        }
    }

    /// <summary>FreeType's <c>TT_Process_Composite_Glyph</c>: the composite's own program over its placed components.</summary>
    private void HintComposite(int startPoint, int startContour, byte[] instructions)
    {
        var n = m_pointCount - startPoint;
        var total = n + PhantomCount;

        // Each instructed level of a nested composite copies all its points again (x, y and tags here; the original and
        // unscaled points in the hinter): charged to the glyph load's work budget before the copies are made.
        m_hinter!.ChargeWork(CompositeZoneArrays * (long)total);
        var x = new long[total];
        var y = new long[total];
        var tags = new byte[total];
        Array.Copy(m_x, startPoint, x, 0, n);
        Array.Copy(m_y, startPoint, y, 0, n);
        Array.Copy(m_tags, startPoint, tags, 0, n);

        // The components' programs touched some points: the composite's program starts with none touched.
        for (var i = 0; i < n; i++)
            tags[i] &= unchecked((byte)~0x18);

        var p = m_phantoms;
        (x[n], y[n]) = (p.X1, p.Y1);
        (x[n + 1], y[n + 1]) = (p.X2, p.Y2);
        (x[n + 2], y[n + 2]) = (p.X3, p.Y3);
        (x[n + 3], y[n + 3]) = (p.X4, p.Y4);

        var contourCount = m_contourCount - startContour;
        var contours = new int[contourCount];
        for (var c = 0; c < contourCount; c++)
            contours[c] = m_contours[startContour + c] - startPoint;

        var zone = new GlyphZone(x, y, null, null, tags, contours, total, contourCount, startPoint);
        var takePhantoms = m_hinter!.HintGlyph(zone, instructions, isComposite: true);

        Array.Copy(x, 0, m_x, startPoint, n);
        Array.Copy(y, 0, m_y, startPoint, n);
        Array.Copy(tags, 0, m_tags, startPoint, n);
        if (takePhantoms)
            m_phantoms = new Phantoms(x[n], y[n], x[n + 1], y[n + 1], x[n + 2], y[n + 2], x[n + 3], y[n + 3]);
    }

    private void EnsurePoints(int count)
    {
        if (count <= m_x.Length)
            return;

        var size = Math.Max(count, m_x.Length * 2);
        Array.Resize(ref m_x, size);
        Array.Resize(ref m_y, size);
        Array.Resize(ref m_tags, size);
    }

    private void EnsureContours(int count)
    {
        if (count > m_contours.Length)
            Array.Resize(ref m_contours, Math.Max(count, m_contours.Length * 2));
    }

    /// <summary>
    /// pp1 = (X1, Y1), pp2 = (X2, Y2), pp3 = (X3, Y3), pp4 = (X4, Y4); pp1.y and pp2.y are 0 unless a <c>gvar</c> delta moves
    /// them (FreeType keeps them, unscaled, as it keeps them).
    /// </summary>
    private readonly record struct Phantoms(long X1, long Y1, long X2, long Y2, long X3, long Y3, long X4, long Y4);
}
