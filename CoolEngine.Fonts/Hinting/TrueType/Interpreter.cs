// Follows FreeType 2.13.2 src/truetype/ttinterp.c (TT_RunIns, the opcode tables, code ranges, function and instruction
// definitions, the call stack, the runaway counters) and src/truetype/ttinterp.h (TT_ExecContextRec) (FreeType Project
// License; credit in docs).

namespace CoolEngine.Fonts.Hinting.TrueType;

/// <summary>An interpreter error, named as FreeType's error codes; <see cref="None"/> for success.</summary>
internal enum InterpreterError
{
    None,
    InvalidOpcode,
    StackOverflow,
    CodeOverflow,
    BadArgument,
    DivideByZero,
    InvalidReference,
    DebugOpcode,
    EndfInExecStream,
    NestedDefs,
    InvalidCodeRange,
    ExecutionTooLong,
    TooManyFunctionDefs,
    TooManyInstructionDefs,
    DefInGlyfBytecode,
}

/// <summary>The three code ranges, numbered as FreeType's <c>tt_coderange_*</c>.</summary>
internal static class CodeRange
{
    public const int Font = 1;   // fpgm
    public const int Cvt = 2;    // prep
    public const int Glyph = 3;  // a glyph program
}

/// <summary>
/// The TrueType bytecode interpreter, a port of FreeType 2.13.2's <c>ttinterp.c</c> in its default (non-pedantic) mode: it
/// skips exactly what FreeType skips (out-of-range point, cvt and storage references read as 0 or are ignored; a stack
/// underflow pops zeros) and stops with an <see cref="InterpreterError"/> wherever FreeType's <c>TT_RunIns</c> returns one.
/// One instance holds one size's execution context (FreeType's <c>TT_ExecContextRec</c>): the stack, the function and
/// instruction definitions, the cvt, the storage, the twilight zone and the super-rounding state, which persist between
/// runs as they do in FreeType. A glyph program's first write to the cvt or the storage makes a working copy, which the
/// other programs of the same glyph load (a composite's components and its own program) share and the next load drops;
/// writes to the twilight zone persist from glyph to glyph, as in FreeType. Not thread-safe.
/// </summary>
/// <remarks>
/// <para>
/// Every limit is FreeType's effective one, each array indexed only after a bounds check against its allocated size, never
/// against what <c>maxp</c> declares (the CVE-2021-24093 class):
/// the stack holds <c>maxp.maxStackElements + 32</c> values (<c>TT_Load_Context</c>); the call stack 32 frames
/// (<c>Init_Context</c>); the function table <c>max(maxp.maxFunctionDefs, 64)</c> entries (<c>tt_face_load_maxp</c>), function
/// numbers 0..65535; the instruction-definition table <c>maxp.maxInstructionDefs</c> entries, opcodes 0..255; the storage
/// <c>maxp.maxStorage</c> values; the cvt the <c>cvt </c> table's length / 2 values; the twilight zone
/// <c>min(maxp.maxTwilightPoints, 65531) + 4</c> points (<c>tt_face_load_maxp</c>, <c>tt_size_init_bytecode</c>), lowered for each
/// run to <c>max(30, 2 * (points + cvt entries))</c> (<c>TT_RunIns</c>).
/// </para>
/// <para>
/// Execution is bounded per run: at most 1,000,000 instructions (FreeType's
/// <c>TT_CONFIG_OPTION_MAX_RUNNABLE_OPCODES</c>), and LOOPCALL iterations and backward jumps each at most
/// <c>max(50, 10 * points) + max(50, cvt entries / 10)</c> for a glyph program or <c>300 + 22 * cvt entries</c> for
/// <c>fpgm</c> and <c>prep</c>, capped at <c>100 * numGlyphs</c> (<c>TT_RunIns</c>); past any of them the run stops with
/// <see cref="InterpreterError.ExecutionTooLong"/>. No recursion: CALL, LOOPCALL and instruction definitions use the fixed
/// call stack.
/// </para>
/// </remarks>
internal sealed partial class Interpreter
{
    /// <summary>FreeType's <c>TT_CONFIG_OPTION_MAX_RUNNABLE_OPCODES</c>: the most instructions one run may execute.</summary>
    public const ulong MaxRunnableOpcodes = 1_000_000;

    /// <summary>FreeType's call stack size (<c>exec->callSize</c>, set in <c>Init_Context</c>).</summary>
    public const int CallStackSize = 32;

    /// <summary>The stack's reserve above <c>maxp.maxStackElements</c> (<c>TT_Load_Context</c>: "broken fonts like arialbs").</summary>
    public const int StackReserve = 32;

    /// <summary>The fewest function definitions FreeType allocates (<c>tt_face_load_maxp</c>: "Keystrokes MT").</summary>
    public const int MinFunctionDefs = 64;

    // opcode_length: 1 for most, the total length of the push instructions; negative for NPUSHB (-1) and NPUSHW (-2).
    private static readonly sbyte[] s_opcodeLength = BuildOpcodeLengths();

    // Pop_Push_Count: arguments popped in the high nibble, values pushed in the low nibble.
    private static readonly byte[] s_popPush = BuildPopPush();

    private readonly int m_version;
    private readonly int m_glyphCount;

    private readonly long[] m_stack;
    private int m_top;
    private int m_args;
    private int m_newTop;

    private readonly CallRecord[] m_callStack = new CallRecord[CallStackSize];
    private int m_callTop;

    private readonly DefRecord[] m_functionDefs;
    private int m_numFunctionDefs;
    private int m_maxFunc;
    private readonly DefRecord[] m_instructionDefs;
    private int m_numInstructionDefs;
    private int m_maxIns;

    // Function number -> slot, and opcode -> slot (-1: none): FreeType searches its tables linearly for the first entry with
    // the number, and since FDEF and IDEF reuse that first entry, each number fills at most one slot, so an index finds the
    // same entry in O(1). (A slot FDEF or IDEF counts and then leaves unfilled, on an out-of-range number, keeps number 0
    // in FreeType; that run fails, and with it the size, before any search could meet it.)
    private readonly Dictionary<int, int> m_functionIndex = [];
    private readonly int[] m_instructionIndex = CreateInstructionIndex();

    private readonly byte[]?[] m_rangeCode = new byte[]?[4];
    private readonly long[] m_rangeSize = new long[4];
    private byte[] m_code = [];
    private long m_codeSize;
    private long m_ip;
    private int m_curRange;
    private int m_iniRange;

    private readonly long[] m_cvtBase;
    private long[] m_cvt;
    private long[]? m_glyfCvt;
    private readonly long[] m_storageBase;
    private long[] m_storage;
    private long[]? m_glyfStorage;

    private byte m_opcode;
    private long m_length;
    private bool m_stepIns;
    private InterpreterError m_error;

    private ulong m_loopcallCounter;
    private ulong m_loopcallCounterMax;
    private ulong m_negJumpCounter;
    private ulong m_negJumpCounterMax;

    // The super-rounding parameters (exec->period, phase, threshold); they persist between runs, as in FreeType.
    private long m_period = 64;

    // The work budget of the current glyph load or size set-up (see Charge).
    private long m_work;
    private long m_workLimit = long.MaxValue;
    private string m_workName = "";
    private long m_phase;
    private long m_threshold;

    /// <param name="version">35 or 40.</param>
    /// <param name="stackSize"><c>maxStackElements + 32</c>.</param>
    /// <param name="functionDefs"><c>max(maxFunctionDefs, 64)</c>.</param>
    /// <param name="instructionDefs"><c>maxInstructionDefs</c>.</param>
    /// <param name="storageSize"><c>maxStorage</c>.</param>
    /// <param name="cvtSize">The cvt's number of entries.</param>
    /// <param name="twilightPoints">The twilight zone's points (<c>min(maxTwilightPoints, 65531) + 4</c>).</param>
    /// <param name="glyphCount"><c>maxp.numGlyphs</c>, which caps the runaway counters.</param>
    public Interpreter(int version, int stackSize, int functionDefs, int instructionDefs, int storageSize, int cvtSize,
        int twilightPoints, int glyphCount)
    {
        m_version = version;
        m_glyphCount = glyphCount;
        m_stack = new long[stackSize];
        m_functionDefs = new DefRecord[functionDefs];
        m_instructionDefs = new DefRecord[instructionDefs];
        m_storageBase = new long[storageSize];
        m_storage = m_storageBase;
        m_cvtBase = new long[cvtSize];
        m_cvt = m_cvtBase;
        Twilight = HintZone.Allocate(twilightPoints);
        TwilightSize = twilightPoints;
        Pts = new HintZone();
        m_zp0 = m_zp1 = m_zp2 = Pts;
    }

    /// <summary>The graphics state (FreeType's <c>exec->GS</c>).</summary>
    public GraphicsState GS;

    /// <summary>The twilight zone, whose points persist between runs.</summary>
    public HintZone Twilight { get; }

    /// <summary>The twilight zone's allocated size; <see cref="HintZone.PointCount"/> is lowered per run, restored per load.</summary>
    public int TwilightSize { get; }

    /// <summary>The glyph zone of the run (FreeType's <c>exec->pts</c>); empty for <c>fpgm</c> and <c>prep</c>.</summary>
    public HintZone Pts { get; set; }

    /// <summary>The cvt, scaled to the size (FreeType's <c>size->cvt</c>).</summary>
    public long[] Cvt => m_cvtBase;

    /// <summary>The storage area (FreeType's <c>size->storage</c>).</summary>
    public long[] Storage => m_storageBase;

    /// <summary>Pixels per em (<c>tt_metrics.ppem</c>), 0 while <c>fpgm</c> runs.</summary>
    public int Ppem { get; set; }

    /// <summary>The size's scale (<c>tt_metrics.scale</c>), 0 while <c>fpgm</c> runs; SSW and WCVTF use it.</summary>
    public long Scale { get; set; }

    /// <summary>The scales MD, MDRP and IP apply to unscaled points (<c>metrics.x_scale</c>, <c>y_scale</c>).</summary>
    public long XScale { get; set; }

    /// <inheritdoc cref="XScale"/>
    public long YScale { get; set; }

    /// <summary>The point size in 26.6 (<c>exec->pointSize</c>), which MPS returns in v40.</summary>
    public long PointSize { get; set; }

    /// <summary>FreeType's <c>exec->grayscale</c>: GETINFO's grayscale bit.</summary>
    public bool Grayscale { get; set; }

    /// <summary>FreeType's <c>exec->subpixel_hinting_lean</c> (v40).</summary>
    public bool SubpixelHintingLean { get; set; }

    /// <summary>FreeType's <c>exec->grayscale_cleartype</c> (v40).</summary>
    public bool GrayscaleCleartype { get; set; }

    /// <summary>FreeType's <c>exec->backward_compatibility</c> (v40): x moves are ignored, y moves stop after IUP on both axes.</summary>
    public bool BackwardCompatibility { get; set; }

    /// <summary>FreeType's <c>exec->is_composite</c>.</summary>
    public bool IsComposite { get; set; }

    /// <summary>
    /// A hook for OT-4 WP5 (variations): whether the face has a blend, which turns on GETINFO bit 10, GETVARIATION and GETDATA.
    /// Nothing sets it yet, so every face runs as FreeType 2.13.2 runs a face without a blend (measured identical on every
    /// variable font of the corpus at its default instance).
    /// </summary>
    public bool HasBlend { get; set; }

    /// <summary>A hook for OT-4 WP5: the number of variation axes GETVARIATION pushes when <see cref="HasBlend"/>.</summary>
    public int AxisCount { get; set; }

    /// <summary>The work charged since <see cref="SetWorkBudget"/>: see <see cref="Charge"/>.</summary>
    public long Work => m_work;

    private bool IsV40 => m_version == 40;

    /// <summary>FreeType's <c>TT_Set_CodeRange</c>.</summary>
    public void SetCodeRange(int range, byte[]? code, long size)
    {
        m_rangeCode[range] = code;
        m_rangeSize[range] = size;
    }

    /// <summary>FreeType's <c>TT_Clear_CodeRange</c>.</summary>
    public void ClearCodeRange(int range)
    {
        m_rangeCode[range] = null;
        m_rangeSize[range] = 0;
    }

    /// <summary>
    /// Runs the given range from its start (FreeType's <c>TT_Goto_CodeRange</c> then <c>TT_RunIns</c>), with the call stack and
    /// the stack emptied first. The caller has set the graphics state and the zones.
    /// </summary>
    public InterpreterError Run(int range)
    {
        m_code = m_rangeCode[range]!;
        m_codeSize = m_rangeSize[range];
        m_ip = 0;
        m_curRange = range;
        m_top = 0;
        m_callTop = 0;
        return RunInstructions();
    }

    /// <summary>Sets the three zone pointers to the glyph zone, as <c>TT_Load_Context</c> and <c>TT_Run_Context</c> do.</summary>
    public void ResetZones() => m_zp0 = m_zp1 = m_zp2 = Pts;

    /// <summary>Starts a work budget (one glyph load, or one size's set-up): the work counter restarts at 0.</summary>
    /// <param name="limit">The most work allowed.</param>
    /// <param name="what">What the budget covers, for the exception's message.</param>
    public void SetWorkBudget(long limit, string what)
    {
        m_work = 0;
        m_workLimit = limit;
        m_workName = what;
    }

    /// <summary>
    /// Charges work to the current budget before it is done: 1 per instruction executed or skipped, plus the points SHZ, SHC,
    /// IUP, FLIPRGON and FLIPRGOFF touch, the iterations of the looping instructions (SHP, SHPIX, IP, ALIGNRP, FLIPPT, DELTAP,
    /// DELTAC), the values MINDEX moves, and the entries of the cvt and storage copies a glyph program makes.
    /// </summary>
    /// <exception cref="NotSupportedException">The budget is exceeded: a library limit, which FreeType does not have.</exception>
    public void Charge(long units)
    {
        m_work += units;
        if (m_work > m_workLimit)
            throw new NotSupportedException(
                $"Hinting needs more work than this library allows for {m_workName} ({m_workLimit} units; charged {m_work}).");
    }

    /// <summary>FreeType's <c>TT_RunIns</c>.</summary>
    private InterpreterError RunInstructions()
    {
        ulong insCounter = 0;

        // Restrict the twilight zone to a heuristic size (TT_RunIns).
        var numTwilight = Math.Max(30L, 2L * (Pts.PointCount + (long)m_cvtBase.Length));
        if (Twilight.PointCount > numTwilight)
        {
            if (numTwilight > 0xFFFF)
                numTwilight = 0xFFFF;

            Twilight.PointCount = (int)numTwilight;
        }

        // The loop detectors (TT_RunIns): LOOPCALL iterations and backward jumps.
        m_loopcallCounter = 0;
        m_negJumpCounter = 0;
        var cvtSize = (ulong)m_cvtBase.Length;
        if (Pts.PointCount != 0)
            m_loopcallCounterMax = Math.Max(50UL, 10UL * (ulong)Pts.PointCount) + Math.Max(50UL, cvtSize / 10);
        else
            m_loopcallCounterMax = 300 + 22 * cvtSize;

        if (m_loopcallCounterMax > 100 * (ulong)m_glyphCount)
            m_loopcallCounterMax = 100 * (ulong)m_glyphCount;

        m_negJumpCounterMax = m_loopcallCounterMax;

        m_iniRange = m_curRange;

        ComputeFuncs();

        m_iupXCalled = false;
        m_iupYCalled = false;

        do
        {
            m_opcode = m_code[m_ip];
            m_length = s_opcodeLength[m_opcode];
            if (m_length < 0)
            {
                if (m_ip + 1 >= m_codeSize)
                    return InterpreterError.CodeOverflow;

                m_length = 2 - m_length * m_code[m_ip + 1];
            }

            if (m_ip + m_length > m_codeSize)
                return InterpreterError.CodeOverflow;

            var pops = s_popPush[m_opcode] >> 4;
            m_args = m_top - pops;

            // A stack underflow pops zeros in FreeType's default mode.
            if (m_args < 0)
            {
                for (var i = 0; i < pops; i++)
                    m_stack[i] = 0;

                m_args = 0;
            }

            if (m_opcode == 0x91 && HasBlend)
                m_newTop = m_args + AxisCount;
            else
                m_newTop = m_args + (s_popPush[m_opcode] & 15);

            if (m_newTop > m_stack.Length)
                return InterpreterError.StackOverflow;

            m_stepIns = true;
            m_error = InterpreterError.None;

            Charge(1);
            Execute(m_args);

            if (m_error != InterpreterError.None)
                return m_error;

            m_top = m_newTop;

            if (m_stepIns)
                m_ip += m_length;

            if (++insCounter > MaxRunnableOpcodes)
                return InterpreterError.ExecutionTooLong;

            if (m_ip >= m_codeSize)
            {
                if (m_callTop > 0)
                    return InterpreterError.CodeOverflow;

                return InterpreterError.None;
            }
        } while (true);
    }

    /// <summary>
    /// <c>TT_Load_Context</c>'s reset of the cvt and storage pointers: the working copies glyph programs write to (made on the
    /// first write of a glyph load, shared by the programs of its components and of the composite) are dropped.
    /// </summary>
    public void DropWorkingCopies()
    {
        m_cvt = m_cvtBase;
        m_storage = m_storageBase;
    }

    /// <summary>FreeType's <c>Ins_Goto_CodeRange</c>.</summary>
    private bool GotoCodeRange(int range, long ip)
    {
        if (range is < 1 or > 3)
        {
            m_error = InterpreterError.BadArgument;
            return false;
        }

        var code = m_rangeCode[range];
        if (code is null)
        {
            m_error = InterpreterError.InvalidCodeRange;
            return false;
        }

        // A CALL as the last instruction returns to the first byte after the range, so ip may equal the size.
        if (ip > m_rangeSize[range])
        {
            m_error = InterpreterError.CodeOverflow;
            return false;
        }

        m_code = code;
        m_codeSize = m_rangeSize[range];
        m_ip = ip;
        m_curRange = range;
        return true;
    }

    /// <summary>FreeType's <c>SkipCode</c>: steps to the next instruction without executing it.</summary>
    private bool SkipCode()
    {
        Charge(1);
        m_ip += m_length;

        if (m_ip < m_codeSize)
        {
            m_opcode = m_code[m_ip];
            m_length = s_opcodeLength[m_opcode];
            if (m_length < 0)
            {
                if (m_ip + 1 >= m_codeSize)
                {
                    m_error = InterpreterError.CodeOverflow;
                    return false;
                }

                m_length = 2 - m_length * m_code[m_ip + 1];
            }

            if (m_ip + m_length <= m_codeSize)
                return true;
        }

        m_error = InterpreterError.CodeOverflow;
        return false;
    }

    /// <summary>FreeType's <c>GetShortIns</c>.</summary>
    private short GetShortIns()
    {
        m_ip += 2;
        return (short)((m_code[m_ip - 2] << 8) + m_code[m_ip - 1]);
    }

    private static int[] CreateInstructionIndex()
    {
        var index = new int[256];
        Array.Fill(index, -1);
        return index;
    }

    private static sbyte[] BuildOpcodeLengths()
    {
        var lengths = new sbyte[256];
        Array.Fill(lengths, (sbyte)1);
        lengths[0x40] = -1;
        lengths[0x41] = -2;
        for (var i = 0; i < 8; i++)
        {
            lengths[0xB0 + i] = (sbyte)(2 + i);
            lengths[0xB8 + i] = (sbyte)(3 + 2 * i);
        }

        return lengths;
    }

    private static byte[] BuildPopPush()
    {
        static byte P(int pop, int push) => (byte)(pop << 4 | push);

        var table = new byte[256];
        void Set(int first, int last, byte value)
        {
            for (var i = first; i <= last; i++)
                table[i] = value;
        }

        Set(0x06, 0x0B, P(2, 0));          // SPvTL, SFvTL, SPvFS, SFvFS
        table[0x0C] = P(0, 2);             // GPv
        table[0x0D] = P(0, 2);             // GFv
        table[0x0F] = P(5, 0);             // ISECT
        Set(0x10, 0x17, P(1, 0));          // SRP0..2, SZP0..2, SZPS, SLOOP
        table[0x1A] = P(1, 0);             // SMD
        Set(0x1C, 0x1F, P(1, 0));          // JMPR, SCvTCi, SSwCi, SSW
        table[0x20] = P(1, 2);             // DUP
        table[0x21] = P(1, 0);             // POP
        table[0x23] = P(2, 2);             // SWAP
        table[0x24] = P(0, 1);             // DEPTH
        table[0x25] = P(1, 1);             // CINDEX
        table[0x26] = P(1, 0);             // MINDEX
        table[0x27] = P(2, 0);             // ALIGNPTS
        table[0x29] = P(1, 0);             // UTP
        table[0x2A] = P(2, 0);             // LOOPCALL
        table[0x2B] = P(1, 0);             // CALL
        table[0x2C] = P(1, 0);             // FDEF
        table[0x2E] = P(1, 0);             // MDAP[0]
        table[0x2F] = P(1, 0);             // MDAP[1]
        Set(0x34, 0x38, P(1, 0));          // SHC, SHZ, SHPIX
        table[0x3A] = P(2, 0);             // MSIRP
        table[0x3B] = P(2, 0);
        table[0x3E] = P(2, 0);             // MIAP
        table[0x3F] = P(2, 0);
        table[0x42] = P(2, 0);             // WS
        table[0x43] = P(1, 1);             // RS
        table[0x44] = P(2, 0);             // WCvtP
        table[0x45] = P(1, 1);             // RCvt
        table[0x46] = P(1, 1);             // GC
        table[0x47] = P(1, 1);
        table[0x48] = P(2, 0);             // SCFS
        table[0x49] = P(2, 1);             // MD
        table[0x4A] = P(2, 1);
        table[0x4B] = P(0, 1);             // MPPEM
        table[0x4C] = P(0, 1);             // MPS
        table[0x4F] = P(1, 0);             // DEBUG
        Set(0x50, 0x55, P(2, 1));          // LT .. NEQ
        table[0x56] = P(1, 1);             // ODD
        table[0x57] = P(1, 1);             // EVEN
        table[0x58] = P(1, 0);             // IF
        table[0x5A] = P(2, 1);             // AND
        table[0x5B] = P(2, 1);             // OR
        table[0x5C] = P(1, 1);             // NOT
        Set(0x5D, 0x5F, P(1, 0));          // DeltaP1, SDB, SDS
        Set(0x60, 0x63, P(2, 1));          // ADD, SUB, DIV, MUL
        Set(0x64, 0x6F, P(1, 1));          // ABS, NEG, FLOOR, CEILING, ROUND, NROUND
        table[0x70] = P(2, 0);             // WCvtF
        Set(0x71, 0x77, P(1, 0));          // DeltaP2, DeltaP3, DeltaC1..3, SROUND, S45Round
        table[0x78] = P(2, 0);             // JROT
        table[0x79] = P(2, 0);             // JROF
        table[0x7E] = P(1, 0);             // SANGW
        table[0x7F] = P(1, 0);             // AA
        table[0x81] = P(2, 0);             // FlipRgON
        table[0x82] = P(2, 0);             // FlipRgOFF
        table[0x85] = P(1, 0);             // ScanCTRL
        table[0x86] = P(2, 0);             // SDPvTL
        table[0x87] = P(2, 0);
        table[0x88] = P(1, 1);             // GetINFO
        table[0x89] = P(1, 0);             // IDEF
        table[0x8A] = P(3, 3);             // ROLL
        table[0x8B] = P(2, 1);             // MAX
        table[0x8C] = P(2, 1);             // MIN
        table[0x8D] = P(1, 0);             // ScanTYPE
        table[0x8E] = P(2, 0);             // InstCTRL
        table[0x92] = P(0, 1);             // GETDATA
        for (var i = 0; i < 8; i++)
        {
            table[0xB0 + i] = P(0, i + 1); // PushB
            table[0xB8 + i] = P(0, i + 1); // PushW
        }

        Set(0xC0, 0xDF, P(1, 0));          // MDRP
        Set(0xE0, 0xFF, P(2, 0));          // MIRP
        return table;
    }

    /// <summary>A function or instruction definition (FreeType's <c>TT_DefRecord</c>).</summary>
    private struct DefRecord
    {
        public int Range;
        public long Start;
        public long End;
        public long Opc;
        public bool Active;
    }

    /// <summary>A call frame (FreeType's <c>TT_CallRec</c>).</summary>
    private struct CallRecord
    {
        public int CallerRange;
        public long CallerIp;
        public long CurCount;
        public bool IsInstruction;
        public int Def;
    }
}
