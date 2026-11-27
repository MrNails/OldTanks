// Follows FreeType 2.13.2 src/psaux/psintrp.c (cf2_interpT2CharString, cf2_doStems, cf2_doFlex, cf2_doBlend,
// cf2_hintmask_*), src/psaux/psstack.c (the operand stack), src/psaux/psread.c (cf2_buf_readByte), src/psaux/pshints.c
// (cf2_glyphpath_* without hinting or darkening, the validity bookkeeping of cf2_hintmap_build), src/psaux/psfont.c
// (cf2_getGlyphOutline, the per-glyph vsindex) and src/psaux/psft.c (cf2_getSeacComponent, the subroutine buffers)
// (FreeType Project License; credit in docs).

namespace CoolEngine.Fonts.Cff;

/// <summary>
/// The Type 2 (CFF) and CFF2 charstring interpreter, unhinted, as FreeType 2.13.2's Adobe engine (<c>psintrp.c</c>) runs it
/// with hinting and stem darkening off: the same operators, the same operand types and wrapping 16.16 arithmetic, the same
/// leniencies (operands an operator does not use are dropped, <c>roll</c> works on the bottom of the stack, hints after a
/// hint mask are ignored) and an error wherever FreeType's interpreter sets one. Points go to a <see cref="CffOutlineBuilder"/>
/// in whole font units. One per glyph loader; not thread-safe.
/// </summary>
/// <remarks>
/// FreeType's limits, each an <see cref="InvalidDataException"/> for the glyph when passed (FreeType fails the glyph there):
/// 48 operands in CFF (<c>CF2_OPERAND_STACK_SIZE</c>, <c>psfont.h</c>) and 513 in CFF2 (the Top DICT's <c>maxstack</c>
/// clamped to <c>CFF2_MAX_STACK</c> and <c>CFF2_DEFAULT_STACK</c>, both 513, <c>cffparse.h</c>); 16 nested subroutine calls
/// (<c>CF2_MAX_SUBR</c>, <c>psfont.h</c>: "only 10 are allowed but there exist fonts like `HiraKakuProN-W3.ttf' ... that
/// exceed this limit"); 96 stem hints when a hint mask is read or built (<c>CF2_MAX_HINTS</c>, <c>pshints.h</c>); 32
/// storage slots for <c>put</c> and <c>get</c> (<c>CF2_STORAGE_SIZE</c>; an index past them is ignored, as FreeType ignores
/// it); 20,000,000 operators and numbers per charstring run (<c>instructionLimit</c> in <c>cf2_interpT2CharString</c>; an
/// accent of a seac runs with its own count). The errors, all FreeType's: an operand stack overflow or underflow, an
/// operator reading an operand past the stack's top, an integer operator (subroutine number, <c>vsindex</c>, a blend count,
/// a seac code, a <c>put</c>, <c>get</c>, <c>index</c> or <c>roll</c> argument) given a 16.16 value, a subroutine number
/// out of range, a <c>return</c> from the charstring itself, more than 96 stems, a seac code with no glyph in the Standard
/// Encoding and charset, a seac inside a seac accent, a CFF2 <c>vsindex</c> after a <c>blend</c>, a <c>blend</c> whose
/// <c>vsindex</c> the variation store lacks, whose count passes the stack or whose operands the stack lacks, and the
/// operator limit. Not errors, as in FreeType: a number or mask cut by the end of its charstring or subroutine (the missing
/// bytes read as 0), a charstring or subroutine ending without <c>endchar</c> or <c>return</c>.
/// The work per glyph is bounded twice. FreeType's own bound: at most three runs (the glyph and a seac's two components),
/// each of at most 20,000,000 operators. This library's, below it (Denys's "Glyph and size budgets", 2026-11-27): one work
/// budget per glyph load, <see cref="GlyphWorkBudget"/> units shared by all its runs, charged as the work happens (1 per
/// operator or number, plus the operands a stack-clearing operator consumes, the elements a <c>roll</c> moves, the operands a
/// <c>blend</c> reads, the bytes a hint mask takes and the glyphs a seac scans); past it, <see cref="NotSupportedException"/>,
/// where FreeType runs on.
/// </remarks>
internal sealed class Type2Interpreter
{
    /// <summary>FreeType's <c>CF2_OPERAND_STACK_SIZE</c>.</summary>
    public const int CffStackSize = 48;

    /// <summary>FreeType's CFF2 stack: <c>maxstack</c> clamped to 513 both ways.</summary>
    public const int Cff2StackSize = 513;

    /// <summary>FreeType's <c>CF2_MAX_SUBR</c>.</summary>
    public const int MaxSubrNesting = 16;

    /// <summary>FreeType's <c>CF2_MAX_HINTS</c>.</summary>
    public const int MaxHints = 96;

    /// <summary>FreeType's <c>CF2_STORAGE_SIZE</c>.</summary>
    public const int StorageSize = 32;

    /// <summary>FreeType's <c>instructionLimit</c>.</summary>
    public const int InstructionLimit = 20_000_000;

    /// <summary>
    /// This library's work budget for one glyph load, all its charstring runs together (Denys's "Glyph and size budgets",
    /// 2026-11-27). Measured on 2026-11-27 over every glyph of SourceSans3-Regular.otf, SourceSans3VF-Upright.otf (default
    /// instance) and ROGFonts-Regular_0.otf: the largest work charged to one glyph was 1,140 units (SourceSans3VF-Upright.otf
    /// glyph 2299; 532 in SourceSans3-Regular.otf, 179 in ROGFonts-Regular_0.otf; the means 311, 148 and 54). About 100 times
    /// that is 114,000, below the floor of 1,000,000 units kept because the CFF corpus here is small and holds no heavy CJK
    /// glyph: so 1,000,000. A glyph refused at it has run about a million operators (about 15 ms on the test machine).
    /// </summary>
    internal const long GlyphWorkBudget = 1_000_000;

    // The Adobe engine's unhinted scale, 1/64 in 16.16 (cf2_getScaleAndHintFlag): device units are 1/1024 font unit.
    private const int UnhintedScale = 0x0400;

    private const int FixedMax = 0x7FFFFFFF;

    // Operators (psintrp.c's cf2_cmd* and cf2_esc*).
    private const int OpHstem = 1, OpVstem = 3, OpVmoveto = 4, OpRlineto = 5, OpHlineto = 6, OpVlineto = 7, OpRrcurveto = 8;
    private const int OpCallsubr = 10, OpReturn = 11, OpEscape = 12, OpEndchar = 14, OpVsindex = 15, OpBlend = 16;
    private const int OpHstemhm = 18, OpHintmask = 19, OpCntrmask = 20, OpRmoveto = 21, OpHmoveto = 22, OpVstemhm = 23;
    private const int OpRcurveline = 24, OpRlinecurve = 25, OpVvcurveto = 26, OpHhcurveto = 27, OpShortInt = 28;
    private const int OpCallgsubr = 29, OpVhcurveto = 30, OpHvcurveto = 31;

    private const int EscAnd = 3, EscOr = 4, EscNot = 5, EscAbs = 9, EscAdd = 10, EscSub = 11, EscDiv = 12, EscNeg = 14;
    private const int EscEq = 15, EscCallothersubr = 16, EscPop = 17, EscDrop = 18, EscPut = 20, EscGet = 21, EscIfelse = 22;
    private const int EscRandom = 23, EscMul = 24, EscSqrt = 26, EscDup = 27, EscExch = 28, EscIndex = 29, EscRoll = 30;
    private const int EscHflex = 34, EscFlex = 35, EscHflex1 = 36, EscFlex1 = 37, EscReserved38 = 38;

    private static readonly bool[] s_hflex = [true, false, true, true, true, false, true, false, true, false, true, false];
    private static readonly bool[] s_flex = [true, true, true, true, true, true, true, true, true, true, true, true];
    private static readonly bool[] s_hflex1 = [true, true, true, true, true, false, true, false, true, true, true, false];
    private static readonly bool[] s_flex1 = [true, true, true, true, true, true, true, true, true, true, false, false];

    private readonly CffTable m_table;
    private readonly CffOutlineBuilder m_builder;
    private readonly Frame[] m_frames;
    private readonly uint[] m_random;
    // The blend vector's weights (FreeType's BV): 1 for the default master, then one per region; reused, grown as needed.
    private int[] m_blendVector = [CffFixed.One];

    // The glyph being run. Run sets m_fontDict before Interpret, the only reader, runs.
    private int m_glyph;
    private CffFontDict m_fontDict = null!;
    private long m_work;
    private long m_workBudget = GlyphWorkBudget;
    private int m_fontDictIndex;
    private uint m_vsindex;
    private bool m_usedBlend;
    private string? m_error;

    public Type2Interpreter(CffTable table, CffOutlineBuilder builder)
    {
        m_table = table;
        m_builder = builder;
        var stackSize = table.IsCff2 ? Cff2StackSize : CffStackSize;
        m_frames = [new Frame(stackSize), new Frame(stackSize)];

        // FreeType seeds `random' (cff_subfont_load) from its cff driver's "random-seed" property, by default a value made
        // from memory addresses; with the property set to 0, from the Private DICT's initialRandomSeed (made positive, 0 when
        // the Font DICT has no Private DICT). Ours is the latter: the only deterministic choice, equal to FreeType's under that
        // property.
        m_random = new uint[Math.Max(1, table.FontDicts.Length)];
        for (var i = 0; i < m_random.Length; i++)
            m_random[i] = (uint)(table.FontDicts.Length == 0 ? table.Top.RandomSeed : table.FontDicts[i].RandomSeed);
    }

    /// <summary>
    /// Runs the glyph's charstring into the builder (reset first), as FreeType's <c>cf2_getGlyphOutline</c>: the charstring
    /// (with its seac accents), then the last contour closed.
    /// </summary>
    /// <exception cref="InvalidDataException">FreeType's interpreter fails the glyph (the reason is in the message).</exception>
    /// <exception cref="NotSupportedException">The outline passes <see cref="CffOutlineBuilder.MaxPoints"/>.</exception>
    /// <exception cref="NotSupportedException">The glyph's charstrings pass <see cref="GlyphWorkBudget"/>.</exception>
    public void Run(int glyphIndex)
    {
        m_glyph = glyphIndex;
        m_error = null;
        m_work = 0;
        m_builder.Reset();

        m_fontDict = m_table.FontDictOf(glyphIndex);
        m_fontDictIndex = m_table.FontDictIndexOf(glyphIndex);

        // Per glyph, as cf2_font_setup sets them: the Private DICT's vsindex, no blend used yet.
        m_vsindex = m_fontDict.Vsindex;
        m_usedBlend = false;

        var charStrings = m_table.CharStrings;
        Interpret(m_frames[0], charStrings.Start(glyphIndex), charStrings.Start(glyphIndex) + charStrings.Length(glyphIndex),
            doingSeac: false, 0, 0);

        if (m_error is not null)
            throw new InvalidDataException($"Glyph {glyphIndex}'s charstring fails as FreeType's interpreter fails it: {m_error}.");

        // cf2_outline_close.
        m_builder.CloseContour();
    }

    /// <summary><c>cf2_interpT2CharString</c>: one charstring (the glyph's or a seac accent's) with its own stack and hints.</summary>
    private void Interpret(Frame f, int start, int end, bool doingSeac, int curX, int curY)
    {
        var data = m_table.Data.Span;
        f.Reset(start, end);
        var isCff2 = m_table.IsCff2;
        var instructionLimit = InstructionLimit;

        while (true)
        {
            int op;
            if (f.BufPtr[f.Index] >= f.BufEnd[f.Index])
            {
                // The end of a buffer: a return from a subroutine, an endchar for the charstring (CFF and CFF2).
                op = f.Index != 0 ? OpReturn : OpEndchar;
            }
            else
            {
                op = ReadByte(f, data);

                // Explicit return and endchar are ignored in CFF2.
                if (isCff2 && op is OpReturn or OpEndchar)
                    op = 0;
            }

            if (m_error is not null)
                return;

            if (--instructionLimit == 0)
            {
                SetError($"more than {InstructionLimit - 1} operators and numbers in one charstring run (FreeType's limit)");
                return;
            }

            Charge(1);

            switch (op)
            {
                case OpVsindex:
                    if (!isCff2)
                        break;

                    if (m_usedBlend)
                    {
                        SetError("a vsindex after a blend");
                        return;
                    }

                    var vsindex = PopInt(f);
                    if (vsindex >= 0)
                        m_vsindex = (uint)vsindex;

                    break;

                case OpBlend:
                    if (!isCff2)
                        break;

                    if (!Blend(f))
                        return;

                    m_usedBlend = true;
                    continue;

                case OpHstemhm:
                case OpHstem:
                    // Hints after the mask is computed are ignored.
                    if (!f.MaskValid)
                        DoStems(f, ref f.HStems);

                    break;

                case OpVstemhm:
                case OpVstem:
                    if (!f.MaskValid)
                        DoStems(f, ref f.VStems);

                    break;

                case OpVmoveto:
                    curY = Add(curY, PopFixed(f));
                    MoveTo(f, curX, curY);
                    break;

                case OpRlineto:
                {
                    var count = f.Count;
                    for (var i = 0; i < count; i += 2)
                    {
                        curX = Add(curX, GetReal(f, i));
                        curY = Add(curY, GetReal(f, i + 1));
                        LineTo(f, curX, curY);
                    }

                    Clear(f);
                    continue;
                }

                case OpHlineto:
                case OpVlineto:
                {
                    var count = f.Count;
                    var isX = op == OpHlineto;
                    for (var i = 0; i < count; i++)
                    {
                        var v = GetReal(f, i);
                        if (isX)
                            curX = Add(curX, v);
                        else
                            curY = Add(curY, v);

                        isX = !isX;
                        LineTo(f, curX, curY);
                    }

                    Clear(f);
                    continue;
                }

                case OpRcurveline:
                case OpRrcurveto:
                {
                    var count = f.Count;
                    var i = 0;
                    while (i + 6 <= count)
                    {
                        var x1 = Add(GetReal(f, i), curX);
                        var y1 = Add(GetReal(f, i + 1), curY);
                        var x2 = Add(GetReal(f, i + 2), x1);
                        var y2 = Add(GetReal(f, i + 3), y1);
                        var x3 = Add(GetReal(f, i + 4), x2);
                        var y3 = Add(GetReal(f, i + 5), y2);
                        CurveTo(f, x1, y1, x2, y2, x3, y3);
                        curX = x3;
                        curY = y3;
                        i += 6;
                    }

                    if (op == OpRcurveline)
                    {
                        curX = Add(curX, GetReal(f, i));
                        curY = Add(curY, GetReal(f, i + 1));
                        LineTo(f, curX, curY);
                    }

                    Clear(f);
                    continue;
                }

                case OpCallgsubr:
                case OpCallsubr:
                {
                    if (f.Index > MaxSubrNesting || f.Index + 1 > MaxSubrNesting)
                    {
                        SetError($"subroutines nested more than {MaxSubrNesting} deep (FreeType's CF2_MAX_SUBR)");
                        return;
                    }

                    var number = PopInt(f);
                    var subrs = op == OpCallgsubr ? m_table.GlobalSubrs : m_fontDict.LocalSubrs;
                    var bias = op == OpCallgsubr ? m_table.GlobalBias : m_fontDict.LocalBias;
                    var index = (uint)(number + bias);
                    if (index >= (uint)subrs.Count)
                    {
                        SetError($"{(op == OpCallgsubr ? "global" : "local")} subroutine {number} (index {(int)index}) of {subrs.Count}");
                        return;
                    }

                    f.Index++;
                    f.BufPtr[f.Index] = subrs.Start((int)index);
                    f.BufEnd[f.Index] = subrs.Start((int)index) + subrs.Length((int)index);
                    continue;
                }

                case OpReturn:
                    if (f.Index < 1)
                    {
                        SetError("a return from the charstring itself");
                        return;
                    }

                    f.Index--;
                    continue;

                case OpEscape:
                {
                    var op2 = ReadByte(f, data);
                    if (!Escape(f, op2, ref curX, ref curY, isCff2, out var clear))
                        return;

                    if (!clear)
                        continue;

                    break;
                }

                case OpEndchar:
                    CloseOpenPath(f);

                    // An endchar with 4 or 5 operands is an accented character (seac) in CFF; not in CFF2.
                    if (!isCff2 && f.Count > 1)
                    {
                        if (doingSeac)
                        {
                            SetError("a seac inside a seac accent");
                            return;
                        }

                        var achar = PopInt(f);
                        var bchar = PopInt(f);
                        var accentY = PopFixed(f);
                        var accentX = PopFixed(f);
                        if (m_error is not null)
                            return;

                        // FreeType draws the accent first, at its offset, then the base character.
                        if (!Seac(achar, accentX, accentY) || !Seac(bchar, 0, 0))
                            return;
                    }

                    return;

                case OpCntrmask:
                case OpHintmask:
                {
                    // Hints after the mask is computed: the operator is ignored (its mask bytes then read as charstring).
                    if (f.Count > 1 && f.MaskValid)
                        break;

                    // Operands here are an implied vstem.
                    DoStems(f, ref f.VStems);
                    var total = f.HStems + f.VStems;
                    if (op == OpHintmask)
                    {
                        if (!ReadMask(f, data, total))
                            break;

                        f.MaskValid = true;
                        f.MaskIsNew = true;
                        f.MaskBitCount = total;
                    }
                    else if (ReadMask(f, data, total))
                    {
                        // The counter mask builds a throw-away hint map, which only makes the initial map valid.
                        f.InitialMapValid = true;
                    }

                    break;
                }

                case OpRmoveto:
                    curY = Add(curY, PopFixed(f));
                    curX = Add(curX, PopFixed(f));
                    MoveTo(f, curX, curY);
                    break;

                case OpHmoveto:
                    curX = Add(curX, PopFixed(f));
                    MoveTo(f, curX, curY);
                    break;

                case OpRlinecurve:
                {
                    var count = f.Count;
                    var i = 0;
                    while (i + 6 < count)
                    {
                        curX = Add(curX, GetReal(f, i));
                        curY = Add(curY, GetReal(f, i + 1));
                        LineTo(f, curX, curY);
                        i += 2;
                    }

                    while (i < count)
                    {
                        var x1 = Add(GetReal(f, i), curX);
                        var y1 = Add(GetReal(f, i + 1), curY);
                        var x2 = Add(GetReal(f, i + 2), x1);
                        var y2 = Add(GetReal(f, i + 3), y1);
                        var x3 = Add(GetReal(f, i + 4), x2);
                        var y3 = Add(GetReal(f, i + 5), y2);
                        CurveTo(f, x1, y1, x2, y2, x3, y3);
                        curX = x3;
                        curY = y3;
                        i += 6;
                    }

                    Clear(f);
                    continue;
                }

                case OpVvcurveto:
                case OpHhcurveto:
                {
                    // A count not of the form 4n or 4n + 1 loses its second bit, and the first operands are skipped.
                    var count1 = f.Count;
                    var count = count1 & ~2;
                    var i = count1 - count;
                    var vertical = op == OpVvcurveto;
                    while (i < count)
                    {
                        int x1, y1, x2, y2, x3, y3;
                        if (vertical)
                        {
                            // An odd count leaves dx1 first.
                            if (((count - i) & 1) != 0)
                                x1 = Add(GetReal(f, i++), curX);
                            else
                                x1 = curX;

                            y1 = Add(GetReal(f, i), curY);
                            x2 = Add(GetReal(f, i + 1), x1);
                            y2 = Add(GetReal(f, i + 2), y1);
                            x3 = x2;
                            y3 = Add(GetReal(f, i + 3), y2);
                        }
                        else
                        {
                            // An odd count leaves dy1 first.
                            if (((count - i) & 1) != 0)
                                y1 = Add(GetReal(f, i++), curY);
                            else
                                y1 = curY;

                            x1 = Add(GetReal(f, i), curX);
                            x2 = Add(GetReal(f, i + 1), x1);
                            y2 = Add(GetReal(f, i + 2), y1);
                            x3 = Add(GetReal(f, i + 3), x2);
                            y3 = y2;
                        }

                        CurveTo(f, x1, y1, x2, y2, x3, y3);
                        curX = x3;
                        curY = y3;
                        i += 4;
                    }

                    Clear(f);
                    continue;
                }

                case OpVhcurveto:
                case OpHvcurveto:
                {
                    var count1 = f.Count;
                    var count = count1 & ~2;
                    var i = count1 - count;
                    var alternate = op == OpHvcurveto;
                    while (i < count)
                    {
                        int x1, y1, x2, y2, x3, y3;
                        if (alternate)
                        {
                            x1 = Add(GetReal(f, i), curX);
                            y1 = curY;
                            x2 = Add(GetReal(f, i + 1), x1);
                            y2 = Add(GetReal(f, i + 2), y1);
                            y3 = Add(GetReal(f, i + 3), y2);
                            if (count - i == 5)
                            {
                                x3 = Add(GetReal(f, i + 4), x2);
                                i++;
                            }
                            else
                            {
                                x3 = x2;
                            }

                            alternate = false;
                        }
                        else
                        {
                            x1 = curX;
                            y1 = Add(GetReal(f, i), curY);
                            x2 = Add(GetReal(f, i + 1), x1);
                            y2 = Add(GetReal(f, i + 2), y1);
                            x3 = Add(GetReal(f, i + 3), x2);
                            if (count - i == 5)
                            {
                                y3 = Add(GetReal(f, i + 4), y2);
                                i++;
                            }
                            else
                            {
                                y3 = y2;
                            }

                            alternate = true;
                        }

                        CurveTo(f, x1, y1, x2, y2, x3, y3);
                        curX = x3;
                        curY = y3;
                        i += 4;
                    }

                    Clear(f);
                    continue;
                }

                case OpShortInt:
                {
                    var b1 = ReadByte(f, data);
                    var b2 = ReadByte(f, data);
                    PushInt(f, (short)(b1 << 8 | b2));
                    continue;
                }

                case >= 32 and <= 246:
                    PushInt(f, op - 139);
                    continue;

                case >= 247 and <= 250:
                    PushInt(f, (op - 247) * 256 + ReadByte(f, data) + 108);
                    continue;

                case >= 251 and <= 254:
                    PushInt(f, -((op - 251) * 256 + ReadByte(f, data) + 108));
                    continue;

                case 255:
                {
                    var b1 = (uint)ReadByte(f, data);
                    var b2 = (uint)ReadByte(f, data);
                    var b3 = (uint)ReadByte(f, data);
                    var b4 = (uint)ReadByte(f, data);
                    PushFixed(f, (int)(b1 << 24 | b2 << 16 | b3 << 8 | b4));
                    continue;
                }

                // Reserved operators (0, 2, 9, 13, 17), and vsindex and blend in CFF, clear the stack.
            }

            Clear(f);
        }
    }

    /// <summary>The two-byte operators; false when the run must stop (an error).</summary>
    private bool Escape(Frame f, int op2, ref int curX, ref int curY, bool isCff2, out bool clear)
    {
        clear = false;
        switch (op2)
        {
            case EscHflex:
                DoFlex(f, ref curX, ref curY, s_hflex, conditionalLastRead: false);
                return true;

            case EscFlex:
                DoFlex(f, ref curX, ref curY, s_flex, conditionalLastRead: false);
                clear = true;
                return true;

            case EscHflex1:
                DoFlex(f, ref curX, ref curY, s_hflex1, conditionalLastRead: false);
                return true;

            case EscFlex1:
                DoFlex(f, ref curX, ref curY, s_flex1, conditionalLastRead: true);
                return true;
        }

        // Always reserved (8, 13, 19, 25, 31, 32), unknown in CFF2 or past 37, or for Type 1 only: the stack is cleared.
        if (isCff2 || op2 >= EscReserved38 || op2 is 8 or 13 or 19 or 25 or 31 or 32)
        {
            clear = true;
            return true;
        }

        switch (op2)
        {
            case EscAnd:
            {
                var arg2 = PopFixed(f);
                var arg1 = PopFixed(f);
                PushInt(f, arg1 != 0 && arg2 != 0 ? 1 : 0);
                return true;
            }

            case EscOr:
            {
                var arg2 = PopFixed(f);
                var arg1 = PopFixed(f);
                PushInt(f, arg1 != 0 || arg2 != 0 ? 1 : 0);
                return true;
            }

            case EscNot:
                PushInt(f, PopFixed(f) == 0 ? 1 : 0);
                return true;

            case EscAbs:
            {
                var arg = PopFixed(f);
                PushFixed(f, arg < -FixedMax ? FixedMax : CffFixed.Abs32(arg));
                return true;
            }

            case EscAdd:
            {
                var summand2 = PopFixed(f);
                var summand1 = PopFixed(f);
                PushFixed(f, Add(summand1, summand2));
                return true;
            }

            case EscSub:
            {
                var subtrahend = PopFixed(f);
                var minuend = PopFixed(f);
                PushFixed(f, (int)((uint)minuend - (uint)subtrahend));
                return true;
            }

            case EscDiv:
            {
                var divisor = PopFixed(f);
                var dividend = PopFixed(f);
                PushFixed(f, CffFixed.DivFix32(dividend, divisor));
                return true;
            }

            case EscNeg:
            {
                var arg = PopFixed(f);
                PushFixed(f, arg < -FixedMax ? FixedMax : -arg);
                return true;
            }

            case EscEq:
            {
                var arg2 = PopFixed(f);
                var arg1 = PopFixed(f);
                PushInt(f, arg1 == arg2 ? 1 : 0);
                return true;
            }

            case EscCallothersubr:
            case EscPop:
                // Type 1 only; in CFF they do nothing, and the stack is kept.
                return true;

            case EscDrop:
                PopFixed(f);
                return true;

            case EscPut:
            {
                var index = (uint)PopInt(f);
                var value = PopFixed(f);
                if (index < StorageSize)
                    f.Storage[index] = value;

                return true;
            }

            case EscGet:
            {
                var index = (uint)PopInt(f);
                if (index < StorageSize)
                    PushFixed(f, f.Storage[index]);

                return true;
            }

            case EscIfelse:
            {
                var cond2 = PopFixed(f);
                var cond1 = PopFixed(f);
                var arg2 = PopFixed(f);
                var arg1 = PopFixed(f);
                PushFixed(f, cond1 <= cond2 ? arg1 : arg2);
                return true;
            }

            case EscRandom:
            {
                // The low 16 bits plus one: a number in (0, 1]; then FreeType's 32-bit xorshift.
                var state = m_random[m_fontDictIndex];
                var r = (int)((state & 0xFFFF) + 1);
                state ^= state << 13;
                state ^= state >> 17;
                state ^= state << 5;
                m_random[m_fontDictIndex] = state;
                PushFixed(f, r);
                return true;
            }

            case EscMul:
            {
                var factor2 = PopFixed(f);
                var factor1 = PopFixed(f);
                PushFixed(f, CffFixed.MulFix32(factor1, factor2));
                return true;
            }

            case EscSqrt:
            {
                var arg = PopFixed(f);
                if (arg > 0)
                {
                    // Babylonian method from a start that keeps the addition in range. It ends within 12 iterations for
                    // every positive 16.16 argument (checked over all of them in OT-4 WP2's review), so, as FreeType's, the
                    // loop has no cap.
                    // root is a 64-bit FT_Fixed in FreeType; the result is stored back in the 32-bit argument.
                    long root = arg < 10 ? arg : arg >> 1;
                    long newRoot;
                    for (;;)
                    {
                        newRoot = (root + CffFixed.DivFix(arg, root) + 1) >> 1;
                        if (newRoot == root)
                            break;

                        root = newRoot;
                    }

                    arg = (int)newRoot;
                }
                else
                {
                    arg = 0;
                }

                PushFixed(f, arg);
                return true;
            }

            case EscDup:
            {
                var arg = PopFixed(f);
                PushFixed(f, arg);
                PushFixed(f, arg);
                return true;
            }

            case EscExch:
            {
                var arg2 = PopFixed(f);
                var arg1 = PopFixed(f);
                PushFixed(f, arg2);
                PushFixed(f, arg1);
                return true;
            }

            case EscIndex:
            {
                var index = PopInt(f);
                var size = f.Count;
                if (size > 0)
                {
                    // Index 0 is the top; out of range: the bottom or, for a negative index, the top.
                    var position = index < 0 ? size - 1 : index >= size ? 0 : size - 1 - index;
                    PushFixed(f, GetReal(f, position));
                }

                return true;
            }

            case EscRoll:
            {
                var shift = PopInt(f);
                var count = PopInt(f);
                Roll(f, count, shift);
                return true;
            }

            default:
                // dotsection (0), the Type 1 hint and seac operators (1, 2, 6, 7) and setcurrentpoint (33): nothing.
                clear = true;
                return true;
        }
    }

    /// <summary>
    /// <c>cf2_stack_roll</c>: FreeType rolls the bottom <paramref name="count"/> elements of the stack (not the top ones the
    /// specification names) by <paramref name="shift"/>.
    /// </summary>
    private void Roll(Frame f, int count, int shift)
    {
        if (count < 2)
            return;

        Charge(Math.Min(count, f.Count));

        if (count > f.Count)
        {
            SetError("a roll of more elements than the stack holds");
            return;
        }

        shift = shift < 0 ? -(-shift % count) : shift % count;
        if (shift == 0)
            return;

        var startIndex = -1;
        var index = -1;
        var lastValue = 0;
        var lastIsInt = true;
        for (var i = 0; i < count; i++)
        {
            if (startIndex == index)
            {
                startIndex++;
                index = startIndex;
                lastValue = f.Values[index];
                lastIsInt = f.IsInt[index];
            }

            index += shift;
            if (index >= count)
                index -= count;
            else if (index < 0)
                index += count;

            var tempValue = f.Values[index];
            var tempIsInt = f.IsInt[index];
            f.Values[index] = lastValue;
            f.IsInt[index] = lastIsInt;
            lastValue = tempValue;
            lastIsInt = tempIsInt;
        }
    }

    /// <summary><c>cf2_doStems</c>: adds the operand pairs as stems (an odd first operand is the width); clears the stack.</summary>
    private void DoStems(Frame f, ref long stems)
    {
        var count = f.Count;
        var first = count & 1;
        stems += (count - first + 1) / 2;
        Clear(f);
    }

    /// <summary><c>cf2_stack_clear</c>, charging the operands the operator consumed.</summary>
    private void Clear(Frame f)
    {
        Charge(f.Count);
        f.Clear();
    }

    /// <summary>Charges glyph work; past <see cref="GlyphWorkBudget"/>, <see cref="NotSupportedException"/> (FreeType runs on).</summary>
    private void Charge(long units)
    {
        m_work += units;
        if (m_work > m_workBudget)
            throw new NotSupportedException(
                $"Glyph {m_glyph}'s charstrings take more work than this library allows for one glyph ({m_workBudget} units).");
    }

    /// <summary>The work the last <see cref="Run"/> charged (for measurements and tests).</summary>
    internal long Work => m_work;

    /// <summary>A work budget instead of <see cref="GlyphWorkBudget"/> (for measurements: <see cref="long.MaxValue"/>).</summary>
    internal long WorkBudget
    {
        get => m_workBudget;
        set => m_workBudget = value;
    }

    /// <summary><c>cf2_doFlex</c>: two curves from the operands <paramref name="read"/> marks, the rest following the flex form.</summary>
    private void DoFlex(Frame f, ref int curX, ref int curY, bool[] read, bool conditionalLastRead)
    {
        Span<int> vals = stackalloc int[14];
        vals[0] = curX;
        vals[1] = curY;
        var index = 0;
        var isHFlex = !read[9];
        var top = isHFlex ? 9 : 10;

        for (var i = 0; i < top; i++)
        {
            vals[i + 2] = vals[i];
            if (read[i])
                vals[i + 2] = Add(vals[i + 2], GetReal(f, index++));
        }

        if (isHFlex)
            vals[9 + 2] = curY;

        if (conditionalLastRead)
        {
            // cf2_fixedAbs wraps: an int.MinValue difference stays negative.
            var lastIsX = CffFixed.Abs32(Sub(vals[10], curX)) > CffFixed.Abs32(Sub(vals[11], curY));
            var lastValue = GetReal(f, index);
            if (lastIsX)
            {
                vals[12] = Add(vals[10], lastValue);
                vals[13] = curY;
            }
            else
            {
                vals[12] = curX;
                vals[13] = Add(vals[11], lastValue);
            }
        }
        else
        {
            vals[12] = read[10] ? Add(vals[10], GetReal(f, index++)) : curX;
            vals[13] = read[11] ? Add(vals[11], GetReal(f, index)) : curY;
        }

        for (var j = 0; j < 2; j++)
            CurveTo(f, vals[j * 6 + 2], vals[j * 6 + 3], vals[j * 6 + 4], vals[j * 6 + 5], vals[j * 6 + 6], vals[j * 6 + 7]);

        Clear(f);
        curX = vals[12];
        curY = vals[13];
    }

    /// <summary>
    /// The CFF2 <c>blend</c> operator (<c>cf2_doBlend</c>): each of the last <c>n</c> values plus its deltas weighted by the
    /// blend vector (at the default instance every weight is 0); false when the run must stop.
    /// </summary>
    private bool Blend(Frame f)
    {
        var store = m_table.VariationStore;
        if (m_vsindex >= (uint)store.DataCount)
        {
            SetError($"a blend with vsindex {m_vsindex}, but the variation store has {store.DataCount} entries");
            return false;
        }

        var numBlends = (uint)PopInt(f);
        if (numBlends > Cff2StackSize)
        {
            SetError($"a blend of {numBlends} values, more than the {Cff2StackSize}-operand stack");
            return false;
        }

        var weights = BlendVector(m_vsindex);
        var numOperands = (long)numBlends * weights.Length;
        var count = f.Count;
        Charge(Math.Min(numOperands, count));
        if (numOperands > count)
        {
            // FreeType reads below the stack (an error) and fails the glyph.
            SetError($"a blend of {numBlends} values of {weights.Length} operands each, but the stack holds {count}");
            return false;
        }

        var first = count - (int)numOperands;
        if (weights.Length == 1)
        {
            // No regions: each value is itself, made 16.16. Only the integers among them change, so only they are visited
            // (FreeType visits every value; the result is the same, and a run of blends stays linear in its operators).
            f.FixIntegersFrom(first);
            return true;
        }

        // Each blend consumes numBlends × (regions + 1) operands and leaves numBlends, so the work stays linear.
        var delta = first + (int)numBlends;
        for (var i = 0; i < (int)numBlends; i++)
        {
            var sum = GetReal(f, first + i);
            for (var j = 1; j < weights.Length; j++)
                sum = Add(sum, CffFixed.MulFix32(weights[j], GetReal(f, delta++)));

            f.Values[first + i] = sum;
            f.IsInt[first + i] = false;
        }

        f.Truncate(first, first + (int)numBlends);
        return true;
    }

    /// <summary>
    /// The blend vector of a <c>vsindex</c> (FreeType's <c>cff_blend_build_vector</c>): 1 for the default master, then one
    /// weight per region. At the default instance, which is all WP2 loads, every region weighs 0 (FreeType's vector without
    /// design coordinates). One buffer, as FreeType keeps one vector, so memory stays at the largest vector used.
    /// </summary>
    private ReadOnlySpan<int> BlendVector(uint vsindex)
    {
        var length = m_table.VariationStore.RegionIndexCount((int)vsindex) + 1;
        if (m_blendVector.Length < length)
        {
            m_blendVector = new int[length];
            m_blendVector[0] = CffFixed.One;
        }

        return m_blendVector.AsSpan(0, length);
    }

    /// <summary>
    /// <c>cf2_getSeacComponent</c> and the component run: the glyph whose charset SID is the Standard Encoding's for
    /// <paramref name="code"/>, run from the current point (<paramref name="x"/>, <paramref name="y"/>); false on an error.
    /// </summary>
    private bool Seac(int code, int x, int y)
    {
        var glyph = -1;
        var sids = m_table.Sids;
        if (sids is not null && code is >= 0 and <= 255)
        {
            var sid = CffPredefined.StandardEncoding[code];
            Charge(sids.Length);
            for (var n = 0; n < sids.Length; n++)
            {
                if (sids[n] == sid)
                {
                    glyph = n;
                    break;
                }
            }
        }

        if (glyph < 0)
        {
            SetError($"a seac character code {code} with no glyph in the Standard Encoding and charset");
            return false;
        }

        var charStrings = m_table.CharStrings;
        Interpret(m_frames[1], charStrings.Start(glyph), charStrings.Start(glyph) + charStrings.Length(glyph), doingSeac: true, x, y);
        return m_error is null;
    }

    /// <summary>Reads a hint or counter mask of <paramref name="bits"/> bits (<c>cf2_hintmask_read</c>); false when it is not read.</summary>
    private bool ReadMask(Frame f, ReadOnlySpan<byte> data, long bits)
    {
        if (bits > MaxHints)
        {
            SetError($"{bits} stem hints, more than FreeType's {MaxHints}");
            return false;
        }

        Charge((bits + 7) / 8);
        for (var i = 0; i < (bits + 7) / 8; i++)
            ReadByte(f, data);

        return true;
    }

    // ---- The glyph path (pshints.c's cf2_glyphpath_*, without hinting or darkening).

    /// <summary>
    /// The validity bookkeeping of <c>cf2_hintmap_build</c>, which is all of it that matters unhinted: the initial map and the
    /// mask become valid (all stems on when no mask was read, an error past 96 stems), the mask is no longer new.
    /// </summary>
    private void BuildHintMap(Frame f)
    {
        var total = f.HStems + f.VStems;
        if (!f.InitialMapValid)
        {
            if (total > MaxHints)
                SetError($"{total} stem hints, more than FreeType's {MaxHints}");
            else
                f.InitialMapValid = true;
        }

        if (!f.MaskValid)
        {
            if (total > MaxHints)
            {
                SetError($"{total} stem hints, more than FreeType's {MaxHints}");
                return;
            }

            f.MaskValid = true;
            f.MaskIsNew = true;
            f.MaskBitCount = total;
        }

        if (f.HStems > f.MaskBitCount)
            return;

        f.HintMapValid = true;
        f.MaskIsNew = false;
    }

    /// <summary><c>cf2_glyphpath_moveTo</c>.</summary>
    private void MoveTo(Frame f, int x, int y)
    {
        CloseOpenPath(f);
        f.CurrentX = f.StartX = x;
        f.CurrentY = f.StartY = y;
        f.MoveIsPending = true;

        if (!f.HintMapValid || f.MaskIsNew)
            BuildHintMap(f);
    }

    /// <summary><c>cf2_glyphpath_lineTo</c>: a zero-length line is dropped unless a new hint mask awaits.</summary>
    private void LineTo(Frame f, int x, int y)
    {
        var newHintMap = f.MaskIsNew && !f.PathIsClosing;
        if (f.CurrentX == x && f.CurrentY == y && !newHintMap)
            return;

        int p0X = f.CurrentX, p0Y = f.CurrentY;
        if (f.MoveIsPending)
        {
            PushMove(f, p0X, p0Y);
            f.MoveIsPending = false;
            f.PathIsOpen = true;
        }

        if (f.ElemIsQueued)
            PushPrevElem(f, p0X, p0Y);

        f.ElemIsQueued = true;
        f.PrevIsCurve = false;
        f.Prev[0] = p0X;
        f.Prev[1] = p0Y;
        f.Prev[2] = x;
        f.Prev[3] = y;

        if (newHintMap)
            BuildHintMap(f);

        f.CurrentX = x;
        f.CurrentY = y;
    }

    /// <summary><c>cf2_glyphpath_curveTo</c>.</summary>
    private void CurveTo(Frame f, int x1, int y1, int x2, int y2, int x3, int y3)
    {
        int p0X = f.CurrentX, p0Y = f.CurrentY;
        if (f.MoveIsPending)
        {
            PushMove(f, p0X, p0Y);
            f.MoveIsPending = false;
            f.PathIsOpen = true;
        }

        if (f.ElemIsQueued)
            PushPrevElem(f, p0X, p0Y);

        f.ElemIsQueued = true;
        f.PrevIsCurve = true;
        f.Prev[0] = p0X;
        f.Prev[1] = p0Y;
        f.Prev[2] = x1;
        f.Prev[3] = y1;
        f.Prev[4] = x2;
        f.Prev[5] = y2;
        f.Prev[6] = x3;
        f.Prev[7] = y3;

        if (f.MaskIsNew)
            BuildHintMap(f);

        f.CurrentX = x3;
        f.CurrentY = y3;
    }

    /// <summary><c>cf2_glyphpath_closeOpenPath</c>: a closing line back to the start, then the queued element.</summary>
    private void CloseOpenPath(Frame f)
    {
        if (!f.PathIsOpen)
            return;

        f.PathIsClosing = true;
        LineTo(f, f.StartX, f.StartY);
        if (f.ElemIsQueued)
            PushPrevElem(f, f.OffsetStartX, f.OffsetStartY);

        f.MoveIsPending = true;
        f.PathIsOpen = false;
        f.PathIsClosing = false;
        f.ElemIsQueued = false;
    }

    /// <summary><c>cf2_glyphpath_pushMove</c>: the builder's moveTo; the path's first point is the start of its first segment.</summary>
    private void PushMove(Frame f, int x, int y)
    {
        // A first subpath without a moveto: FreeType synthesises one to set up its hint map.
        if (!f.HintMapValid)
            MoveTo(f, f.StartX, f.StartY);

        m_builder.MoveTo();
        f.CurrentDsX = Device(x);
        f.CurrentDsY = Device(y);
        f.OffsetStartX = x;
        f.OffsetStartY = y;
    }

    /// <summary>
    /// <c>cf2_glyphpath_pushPrevElem</c> without offsets: the queued element goes to the builder (a line only when it has
    /// length in device units), then the line to the next element's start when it is elsewhere.
    /// </summary>
    private void PushPrevElem(Frame f, int nextX, int nextY)
    {
        if (!f.PrevIsCurve)
        {
            var x1 = Device(f.Prev[2]);
            var y1 = Device(f.Prev[3]);
            if (x1 != f.CurrentDsX || y1 != f.CurrentDsY)
            {
                m_builder.LineTo(f.CurrentDsX, f.CurrentDsY, x1, y1);
                f.CurrentDsX = x1;
                f.CurrentDsY = y1;
            }
        }
        else
        {
            var x3 = Device(f.Prev[6]);
            var y3 = Device(f.Prev[7]);
            m_builder.CubeTo(f.CurrentDsX, f.CurrentDsY, Device(f.Prev[2]), Device(f.Prev[3]), Device(f.Prev[4]),
                Device(f.Prev[5]), x3, y3);
            f.CurrentDsX = x3;
            f.CurrentDsY = y3;
        }

        // The line joining the element's end to the next element's start (none when they meet, as without offsets they do
        // except where a closing segment returns to the start).
        var nx = Device(nextX);
        var ny = Device(nextY);
        if (nx != f.CurrentDsX || ny != f.CurrentDsY)
        {
            m_builder.LineTo(f.CurrentDsX, f.CurrentDsY, nx, ny);
            f.CurrentDsX = nx;
            f.CurrentDsY = ny;
        }
    }

    /// <summary>A character-space 16.16 value in the unhinted device space: <c>FT_MulFix(v, 1/64)</c>.</summary>
    private static int Device(int value) => CffFixed.MulFix32(value, UnhintedScale);

    // ---- The operand stack (psstack.c) and the buffers (psread.c).

    /// <summary>
    /// <c>cf2_buf_readByte</c>: the next byte, or 0 past the end of the buffer. FreeType's buffers carry no error pointer
    /// (<c>FT_ZERO</c> in <c>psft.c</c>), so reading past the end is not an error: a number or mask cut by the end reads
    /// zeros.
    /// </summary>
    private static int ReadByte(Frame f, ReadOnlySpan<byte> data)
    {
        ref var ptr = ref f.BufPtr[f.Index];
        return ptr < f.BufEnd[f.Index] ? data[ptr++] : 0;
    }

    private void PushInt(Frame f, int value)
    {
        if (f.Count == f.Values.Length)
        {
            SetError($"an operand stack overflow (more than {f.Values.Length} operands)");
            return;
        }

        f.Values[f.Count] = value;
        f.IsInt[f.Count] = true;
        f.IntPositions[f.IntCount++] = f.Count;
        f.Count++;
    }

    private void PushFixed(Frame f, int value)
    {
        if (f.Count == f.Values.Length)
        {
            SetError($"an operand stack overflow (more than {f.Values.Length} operands)");
            return;
        }

        f.Values[f.Count] = value;
        f.IsInt[f.Count] = false;
        f.Count++;
    }

    /// <summary><c>cf2_stack_popInt</c>: an integer operand; a 16.16 one is an error (and stays on the stack).</summary>
    private int PopInt(Frame f)
    {
        if (f.Count == 0)
        {
            SetError("an operand stack underflow");
            return 0;
        }

        if (!f.IsInt[f.Count - 1])
        {
            SetError("a 16.16 operand where an integer is required");
            return 0;
        }

        f.IntCount--;
        return f.Values[--f.Count];
    }

    /// <summary><c>cf2_stack_popFixed</c>: any operand as 16.16.</summary>
    private int PopFixed(Frame f)
    {
        if (f.Count == 0)
        {
            SetError("an operand stack underflow");
            return 0;
        }

        f.Count--;
        if (!f.IsInt[f.Count])
            return f.Values[f.Count];

        f.IntCount--;
        return (int)((uint)f.Values[f.Count] << 16);
    }

    /// <summary><c>cf2_stack_getReal</c>: operand <paramref name="index"/> from the bottom, as 16.16.</summary>
    private int GetReal(Frame f, int index)
    {
        if ((uint)index >= (uint)f.Count)
        {
            SetError("an operator reading past the operands on the stack");
            return 0;
        }

        return f.IsInt[index] ? (int)((uint)f.Values[index] << 16) : f.Values[index];
    }

    private void SetError(string message) => m_error ??= message;

    private static int Add(int a, int b) => (int)((uint)a + (uint)b);

    private static int Sub(int a, int b) => (int)((uint)a - (uint)b);

    /// <summary>The state of one charstring run (the glyph's, or a seac accent's): FreeType's locals of <c>cf2_interpT2CharString</c>.</summary>
    private sealed class Frame
    {
        public readonly int[] Values;
        public readonly bool[] IsInt;

        // The positions of the integer operands, ascending (pushes and pops happen at the top): a blend without regions
        // converts just these. Roll moves operands without updating it, but roll exists only in CFF and blend only in CFF2.
        public readonly int[] IntPositions;
        public int IntCount;
        public readonly int[] Storage = new int[StorageSize];
        public readonly int[] BufPtr = new int[MaxSubrNesting + 1];
        public readonly int[] BufEnd = new int[MaxSubrNesting + 1];
        public readonly int[] Prev = new int[8];

        public int Count;
        public int Index;

        public long HStems;
        public long VStems;
        public bool MaskValid;
        public bool MaskIsNew;
        public long MaskBitCount;
        public bool HintMapValid;
        public bool InitialMapValid;

        public bool MoveIsPending;
        public bool PathIsOpen;
        public bool PathIsClosing;
        public bool ElemIsQueued;
        public bool PrevIsCurve;
        public int CurrentX, CurrentY, StartX, StartY, OffsetStartX, OffsetStartY, CurrentDsX, CurrentDsY;

        public Frame(int stackSize)
        {
            Values = new int[stackSize];
            IsInt = new bool[stackSize];
            IntPositions = new int[stackSize];
        }

        /// <summary>Empties the operand stack (<c>cf2_stack_clear</c>).</summary>
        public void Clear()
        {
            Count = 0;
            IntCount = 0;
        }

        /// <summary>Keeps the bottom <paramref name="count"/> operands, those from <paramref name="first"/> now 16.16.</summary>
        public void Truncate(int first, int count)
        {
            while (IntCount > 0 && IntPositions[IntCount - 1] >= first)
                IntCount--;

            Count = count;
        }

        /// <summary>Makes every integer operand from <paramref name="first"/> up a 16.16 one of the same value.</summary>
        public void FixIntegersFrom(int first)
        {
            while (IntCount > 0 && IntPositions[IntCount - 1] >= first)
            {
                var position = IntPositions[--IntCount];
                Values[position] = (int)((uint)Values[position] << 16);
                IsInt[position] = false;
            }
        }

        public void Reset(int start, int end)
        {
            Count = 0;
            IntCount = 0;
            Index = 0;
            BufPtr[0] = start;
            BufEnd[0] = end;
            Array.Clear(Storage);
            HStems = VStems = 0;
            MaskValid = MaskIsNew = HintMapValid = InitialMapValid = false;
            MaskBitCount = 0;
            MoveIsPending = true;
            PathIsOpen = PathIsClosing = ElemIsQueued = PrevIsCurve = false;
            CurrentX = CurrentY = StartX = StartY = OffsetStartX = OffsetStartY = CurrentDsX = CurrentDsY = 0;
        }
    }
}
