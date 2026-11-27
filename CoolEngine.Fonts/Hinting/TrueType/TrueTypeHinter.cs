// Follows FreeType 2.13.2 src/truetype/ttobjs.c (tt_size_init_bytecode, tt_size_run_fpgm, tt_size_run_prep,
// tt_size_ready_bytecode), src/truetype/ttgload.c (tt_loader_init, TT_Hint_Glyph, compute_glyph_metrics),
// src/truetype/ttinterp.c (TT_Load_Context, TT_Run_Context), src/truetype/ttpload.c (tt_face_load_cvt) and
// src/base/ftobjs.c (ft_glyphslot_grid_fit_metrics) (FreeType Project License; credit in docs).

using CoolEngine.Fonts.Sfnt;

namespace CoolEngine.Fonts.Hinting.TrueType;

/// <summary>
/// The TrueType hinter of one size, as FreeType 2.13.2 hints at <c>FT_LOAD_TARGET_NORMAL</c> with interpreter version 35 or 40
/// on a fresh <c>FT_Size</c>: at creation it runs <c>fpgm</c> (ppem and scale 0), clears the twilight zone and the storage,
/// scales the <c>cvt</c> and runs <c>prep</c> twice, first with the rendering flags of a new execution context (no grayscale,
/// no ClearType) and then, as FreeType's first glyph load re-executes it after the flags change, with the flags of a grayscale
/// load (v35: grayscale; v40: subpixel hinting and grayscale ClearType); the second run starts from the first's storage,
/// twilight zone and graphics state. Each glyph program then starts from the graphics state <c>prep</c> left, with its
/// vectors, zone pointers, rounding and loop reset (<c>TT_Run_Context</c>).
/// </summary>
/// <remarks>
/// Errors follow FreeType's default mode (Denys: "Throw where FreeType throws"): an error in <c>fpgm</c> or <c>prep</c> makes
/// every hinted glyph load of the size fail (<see cref="BeginGlyph"/> throws <see cref="InvalidDataException"/>); an error in a
/// glyph program stops that program and is otherwise ignored, so the glyph keeps the moves made before it.
/// <para>
/// Work budgets (Denys, 2026-11-27: "Glyph and size budgets"), a library limit FreeType does not have, so on a hostile font
/// ours refuses where FreeType runs on: every program run of one glyph load (each component's and the composite's) shares
/// <see cref="GlyphWorkBudget"/>, and the size's set-up (the cvt, storage, twilight, stack and definition-table
/// allocations, <c>fpgm</c> and both <c>prep</c> runs) shares <see cref="SizeWorkBudget"/>; the work is counted as
/// <see cref="Interpreter.Charge"/> describes, plus the points each composite program's zone copies (see
/// <see cref="ChargeWork"/>). Past either, <see cref="NotSupportedException"/>: from the glyph load, or from <c>CreateScaledFont</c>.
/// Not thread-safe.
/// </para>
/// </remarks>
internal sealed class TrueTypeHinter : ITrueTypeHinter
{
    /// <summary>
    /// The work one glyph load may charge (see the remarks). Measured on 2026-11-27 (re-measured after the composite zone
    /// copies were charged) over every TrueType face of <c>C:\Windows\Fonts</c> (214 faces) and the test fonts (the
    /// repository's Arial and Times New Roman, SourceSans3-Regular.ttf, SourceSans3VF-Upright.ttf), every glyph at 9, 16, 72 and
    /// 400 px in v35 and v40: the largest was 19,897 units (arial.ttf glyph 395 at 9 and 16 px, v40), then 18,913 (arial.ttf
    /// glyph 2577, 72 and 400 px, v40) and 14,659 (verdana.ttf glyph 80). The budget is about 100 times the largest.
    /// </summary>
    internal const long GlyphWorkBudget = 2_000_000;

    /// <summary>
    /// The work one size's set-up may charge (see the remarks). Measured with <see cref="GlyphWorkBudget"/> (re-measured after
    /// the definition tables' allocation was charged): the largest was 44,511 units (cambria.ttc, both faces, 9 and 16 px, v35),
    /// then 44,295 (cambria.ttc at 72 and 400 px) and 44,141 (calibri.ttf at 72 and 400 px). The budget is about 100 times the
    /// largest.
    /// </summary>
    internal const long SizeWorkBudget = 4_500_000;

    private const uint CvtTag = 0x63767420;   // cvt
    private const uint FpgmTag = 0x6670676D;  // fpgm
    private const uint PrepTag = 0x70726570;  // prep

    private readonly Interpreter m_interpreter;
    private readonly TrueTypeInterpreterVersion m_version;
    private readonly bool m_isV40;
    private readonly long m_scale;
    private readonly int m_ppem;
    private readonly ReadOnlyMemory<byte> m_faceCvt;
    private readonly byte[] m_prep;
    private readonly bool m_tricky;
    private readonly bool m_fixedPitch;
    private readonly HdmxTable? m_hdmx;
    private readonly int m_hdmxWidths;
    private readonly long m_glyphWorkBudget;

    // cvar's deltas (26.6) for each cvt entry; null without a blend or without cvar.
    private readonly int[]? m_cvtDeltas;

    private byte[] m_glyphCode = new byte[256];
    private GraphicsState m_sizeGS;
    private string? m_failure;
    private bool m_useHdmx;

    /// <param name="face">The face's tables.</param>
    /// <param name="scale">The size.</param>
    /// <param name="version">The interpreter mode.</param>
    /// <param name="variation">
    /// The variation coordinates, or null for none (OT-4 WP5). With them the face has a blend, as FreeType's has after
    /// <c>FT_Set_Var_Design_Coordinates</c>: <c>cvar</c>'s deltas go into the cvt before <c>prep</c> runs (FreeType's
    /// <c>tt_face_vary_cvt</c>, its 26.6 values kept in 32 bits), and GETINFO bit 10, GETVARIATION and GETDATA answer.
    /// </param>
    public TrueTypeHinter(FaceData face, FontScale scale, TrueTypeInterpreterVersion version,
        Variations.VariationInstance? variation = null)
        : this(face, scale, version, GlyphWorkBudget, SizeWorkBudget, variation)
    {
    }

    /// <summary>A hinter with other work budgets (to measure the work real fonts take, and in tests).</summary>
    internal TrueTypeHinter(FaceData face, FontScale scale, TrueTypeInterpreterVersion version, long glyphWorkBudget,
        long sizeWorkBudget, Variations.VariationInstance? variation = null)
    {
        m_version = version;
        m_glyphWorkBudget = glyphWorkBudget;
        m_isV40 = version == TrueTypeInterpreterVersion.V40;
        m_scale = scale.XScale;
        m_ppem = scale.PixelSize;

        var tables = face.Tables;
        var maxp = face.Maxp;

        // tt_face_load_cvt: each entry a signed 16-bit value, kept in 26.6 (read from the table for each prep run).
        m_faceCvt = tables.Get(CvtTag);
        var cvtEntries = m_faceCvt.Length / 2;

        var fpgm = tables.Get(FpgmTag).ToArray();
        m_prep = tables.Get(PrepTag).ToArray();

        m_tricky = TrickyFonts.IsTricky(face);
        m_fixedPitch = face.Post.IsFixedPitch;
        m_hdmx = face.Hdmx;
        m_hdmxWidths = m_hdmx?.FindWidths(m_ppem) ?? -1;

        // FreeType's effective limits (TT_Load_Context, tt_face_load_maxp, tt_size_init_bytecode); see Interpreter. Their
        // allocations are charged to the size's budget before they are made.
        var twilight = Math.Min((int)maxp.MaxTwilightPoints, 0xFFFF - 4) + 4;
        var stack = maxp.MaxStackElements + Interpreter.StackReserve;
        var functionDefs = Math.Max((int)maxp.MaxFunctionDefs, Interpreter.MinFunctionDefs);
        var allocation = (long)cvtEntries + maxp.MaxStorage + twilight + stack + functionDefs + maxp.MaxInstructionDefs;
        if (allocation > sizeWorkBudget)
            throw new NotSupportedException(
                $"Hinting needs more work than this library allows for the size's set-up ({sizeWorkBudget} units).");

        m_interpreter = new Interpreter(m_isV40 ? 40 : 35, stack,
            functionDefs, maxp.MaxInstructionDefs, maxp.MaxStorage,
            cvtEntries, twilight, face.GlyphCount)
        {
            PointSize = m_ppem * 64L,
        };

        m_interpreter.SetWorkBudget(sizeWorkBudget, "the size's set-up");
        m_interpreter.Charge(allocation);

        // The blend (OT-4 WP5): the hooks, and cvar's deltas in the cvt (its work charged to the size's set-up).
        if (variation is not null)
        {
            m_interpreter.HasBlend = true;
            m_interpreter.AxisCount = variation.Normalized.Length;
            m_interpreter.VariationCoordinates = variation.Normalized;
            m_cvtDeltas = variation.Tables.Cvar?.ComputeDeltas(variation.Normalized, cvtEntries, new InterpreterWork(m_interpreter));
        }

        Initialize(fpgm);
        SizeWork = m_interpreter.Work;
    }

    public TrueTypeInterpreterVersion Version => m_version;

    /// <summary>The work the size's set-up charged (for measurements).</summary>
    internal long SizeWork { get; }

    /// <summary>The work the current or last glyph load charged (for measurements).</summary>
    internal long GlyphWork => m_interpreter.Work;

    public bool BeginGlyph()
    {
        if (m_failure is not null)
            throw new InvalidDataException(m_failure);

        // TT_Load_Context: the size's graphics state and the whole twilight zone; a new glyph budget.
        var interpreter = m_interpreter;
        interpreter.SetWorkBudget(m_glyphWorkBudget, "one glyph load");
        interpreter.GS = m_sizeGS;
        interpreter.Twilight.PointCount = interpreter.TwilightSize;
        interpreter.DropWorkingCopies();
        interpreter.Ppem = m_ppem;
        interpreter.Scale = m_scale;

        // tt_loader_init: INSTCTRL selector 1 in prep turns hinting off for the size; selector 2 resets the graphics state to the
        // default before selector 3 is read, so with selector 2 set v40 keeps backward compatibility.
        var instructControl = m_sizeGS.InstructControl;
        var hinted = (instructControl & 1) == 0;
        if ((instructControl & 2) != 0)
            instructControl = 0;

        // v40 keeps backward compatibility unless the font is tricky or prep opts out with INSTCTRL selector 3.
        interpreter.BackwardCompatibility = m_isV40 && !m_tricky && (instructControl & 4) == 0;

        // The hdmx advance, unless v40 runs in backward compatibility or the font is fixed-pitch.
        m_useHdmx = m_hdmxWidths >= 0 && !(m_isV40 && interpreter.BackwardCompatibility) && !m_fixedPitch;

        return hinted;
    }

    public void ChargeWork(long units) => m_interpreter.Charge(units);

    public bool HintGlyph(GlyphZone zone, ReadOnlySpan<byte> instructions, bool isComposite)
    {
        var interpreter = m_interpreter;
        var n = zone.PointCount;
        var x = zone.X;
        var y = zone.Y;

        var pts = new HintZone
        {
            CurX = x,
            CurY = y,
            Tags = zone.Tags,
            PointCount = n,
            ContourCount = zone.ContourCount,
            FirstPoint = zone.FirstPoint,
            Contours = new int[zone.ContourCount],
        };

        for (var c = 0; c < zone.ContourCount; c++)
            pts.Contours[c] = zone.ContourEnds[c] + zone.FirstPoint;

        // The original points are the current ones before the phantom points are rounded.
        if (instructions.Length > 0)
        {
            pts.OrgX = x[..n];
            pts.OrgY = y[..n];
        }

        interpreter.GS = m_sizeGS;

        // A composite's program refers to the already hinted components, at scale 1.
        if (isComposite)
        {
            interpreter.XScale = interpreter.YScale = 0x10000;
            pts.OrusX = x[..n];
            pts.OrusY = y[..n];
        }
        else
        {
            interpreter.XScale = interpreter.YScale = m_scale;
            pts.OrusX = zone.UnscaledX ?? x[..n];
            pts.OrusY = zone.UnscaledY ?? y[..n];
        }

        x[n - 4] = FixedMath.PixRound(x[n - 4]);
        x[n - 3] = FixedMath.PixRound(x[n - 3]);
        y[n - 2] = FixedMath.PixRound(y[n - 2]);
        y[n - 1] = FixedMath.PixRound(y[n - 1]);

        if (instructions.Length > 0)
        {
            if (m_glyphCode.Length < instructions.Length)
                m_glyphCode = new byte[instructions.Length];

            instructions.CopyTo(m_glyphCode);
            interpreter.SetCodeRange(CodeRange.Glyph, m_glyphCode, instructions.Length);
            interpreter.IsComposite = isComposite;
            interpreter.Pts = pts;
            ResetForRun();

            // FreeType's default mode ignores a glyph program's error.
            interpreter.Run(CodeRange.Glyph);
        }

        // v40 backward compatibility keeps the unhinted phantom points (no x movement means no new advance).
        return !(m_isV40 && interpreter.BackwardCompatibility);
    }

    public GlyphMetrics FitMetrics(int glyphIndex, GlyphMetrics metrics)
    {
        long advance = metrics.Advance;
        if (m_useHdmx)
            advance = m_hdmx!.GetWidth(m_hdmxWidths, glyphIndex) * 64L;

        // ft_glyphslot_grid_fit_metrics.
        var right = FixedMath.PixCeil((long)metrics.BearingX + metrics.Width);
        var bottom = FixedMath.PixFloor((long)metrics.BearingY - metrics.Height);
        var bearingX = FixedMath.PixFloor(metrics.BearingX);
        var bearingY = FixedMath.PixCeil(metrics.BearingY);

        return new GlyphMetrics((int)(right - bearingX), (int)(bearingY - bottom), (int)bearingX, (int)bearingY,
            (int)FixedMath.PixRound(advance));
    }

    /// <summary>
    /// <c>tt_size_init_bytecode</c> and <c>tt_size_run_fpgm</c>, then <c>tt_size_ready_bytecode</c> and the re-execution of
    /// <c>prep</c> in the first <c>tt_loader_init</c>.
    /// </summary>
    private void Initialize(byte[] fpgm)
    {
        var interpreter = m_interpreter;

        // fpgm runs with ppem and scale 0.
        m_sizeGS = GraphicsState.Default;
        LoadContext();
        interpreter.Ppem = 0;
        interpreter.Scale = 0;
        interpreter.XScale = interpreter.YScale = 0;
        interpreter.SetCodeRange(CodeRange.Font, fpgm, fpgm.Length);
        interpreter.ClearCodeRange(CodeRange.Cvt);
        interpreter.ClearCodeRange(CodeRange.Glyph);
        if (fpgm.Length > 0)
        {
            var error = interpreter.Run(CodeRange.Font);
            if (error != InterpreterError.None)
            {
                m_failure = $"The font program ('fpgm') failed: {error}.";
                return;
            }
        }

        // tt_size_ready_bytecode: the twilight zone and the storage start at zero, the graphics state at its default.
        var twilight = interpreter.Twilight;
        Array.Clear(twilight.OrgX);
        Array.Clear(twilight.OrgY);
        Array.Clear(twilight.CurX);
        Array.Clear(twilight.CurY);
        Array.Clear(interpreter.Storage);
        interpreter.Charge(twilight.OrgX.Length + interpreter.Storage.Length);
        m_sizeGS = GraphicsState.Default;

        if (!RunPrep())
            return;

        // The first glyph load finds the rendering flags changed and runs prep again.
        if (m_isV40)
        {
            interpreter.SubpixelHintingLean = true;
            interpreter.GrayscaleCleartype = true;
        }
        else
        {
            interpreter.Grayscale = true;
        }

        RunPrep();
    }

    /// <summary><c>tt_size_run_prep</c>; false (with <see cref="m_failure"/> set) when it fails.</summary>
    private bool RunPrep()
    {
        var interpreter = m_interpreter;

        // Scale the cvt: the unscaled values are signed 16-bit, taken as 26.6, plus cvar's deltas in FreeType's 32-bit face->cvt.
        var scale = m_scale >> 6;
        var cvt = interpreter.Cvt;
        var source = m_faceCvt.Span;
        interpreter.Charge(cvt.Length);
        for (var i = 0; i < cvt.Length; i++)
        {
            var unscaled = (short)(source[2 * i] << 8 | source[2 * i + 1]) * 64;
            if (m_cvtDeltas is not null)
                unscaled = unchecked(unscaled + m_cvtDeltas[i]);

            cvt[i] = FixedMath.MulFix(unscaled, scale);
        }

        LoadContext();
        interpreter.SetCodeRange(CodeRange.Cvt, m_prep, m_prep.Length);
        interpreter.ClearCodeRange(CodeRange.Glyph);

        var error = InterpreterError.None;
        if (m_prep.Length > 0)
            error = interpreter.Run(CodeRange.Cvt);

        // The Microsoft rasteriser does not let prep change these.
        ref var gs = ref interpreter.GS;
        gs.DualVectorX = gs.ProjVectorX = gs.FreeVectorX = 0x4000;
        gs.DualVectorY = gs.ProjVectorY = gs.FreeVectorY = 0;
        gs.Rp0 = gs.Rp1 = gs.Rp2 = 0;
        gs.Gep0 = gs.Gep1 = gs.Gep2 = 1;
        gs.Loop = 1;
        m_sizeGS = gs;

        if (error != InterpreterError.None)
        {
            m_failure = $"The control value program ('prep') failed at {m_ppem} pixels per em: {error}.";
            return false;
        }

        return true;
    }

    /// <summary>
    /// <c>TT_Load_Context</c>: the size's graphics state, metrics and whole twilight zone; an empty glyph zone (fpgm and prep
    /// have no points).
    /// </summary>
    private void LoadContext()
    {
        var interpreter = m_interpreter;
        interpreter.GS = m_sizeGS;
        interpreter.Ppem = m_ppem;
        interpreter.Scale = m_scale;
        interpreter.XScale = interpreter.YScale = m_scale;
        interpreter.Twilight.PointCount = interpreter.TwilightSize;
        interpreter.DropWorkingCopies();
        interpreter.Pts = new HintZone();
        interpreter.ResetZones();
    }

    /// <summary>Charges work to the interpreter's current budget (the size's set-up while the hinter is made).</summary>
    private sealed class InterpreterWork(Interpreter interpreter) : Variations.IWorkCounter
    {
        public void Charge(long units) => interpreter.Charge(units);
    }

    /// <summary><c>TT_Run_Context</c>: zones, vectors, rounding and loop reset for a glyph program.</summary>
    private void ResetForRun()
    {
        var interpreter = m_interpreter;
        interpreter.ResetZones();
        ref var gs = ref interpreter.GS;
        gs.Gep0 = gs.Gep1 = gs.Gep2 = 1;
        gs.ProjVectorX = gs.FreeVectorX = gs.DualVectorX = 0x4000;
        gs.ProjVectorY = gs.FreeVectorY = gs.DualVectorY = 0;
        gs.RoundState = RoundMode.ToGrid;
        gs.Loop = 1;
    }
}
