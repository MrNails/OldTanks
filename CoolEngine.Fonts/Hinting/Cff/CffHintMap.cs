// Follows FreeType 2.13.2 src/psaux/pshints.c (cf2_hint_init, cf2_hint_* predicates, cf2_hintmap_init, cf2_hintmap_map,
// cf2_hintmap_adjustHints, cf2_hintmap_insertHint, cf2_hintmap_build), src/psaux/pshints.h (CF2_HintMapRec, CF2_StemHintRec,
// CF2_MAX_HINT_EDGES), src/psaux/psblues.h (CF2_HintRec, the edge flags, CF2_MIN_COUNTER) and src/psaux/psintrp.c
// (cf2_hintmask_*), Adobe's CFF engine in FreeType (FreeType Project License; credit in docs).

using CoolEngine.Fonts.Cff;

namespace CoolEngine.Fonts.Hinting.Cff;

/// <summary>One edge of a hint map (<c>CF2_HintRec</c>): its flags, its stem's index, its character-space and device-space position and the scale above it.</summary>
internal struct CffHint
{
    /// <summary>The <c>CF2_GhostBottom</c> .. <c>CF2_Synthetic</c> flags.</summary>
    public const int GhostBottom = 0x1, GhostTop = 0x2, PairBottom = 0x4, PairTop = 0x8, Locked = 0x10, Synthetic = 0x20;

    /// <summary><c>CF2_MIN_COUNTER</c>: half a pixel, the least counter adjustment leaves and the synthetic em box's margin.</summary>
    public const int MinCounter = 0x8000;

    public int Flags;
    public int Index;
    public int CsCoord;
    public int DsCoord;
    public int Scale;

    public readonly bool IsValid => Flags != 0;

    public readonly bool IsPair => (Flags & (PairBottom | PairTop)) != 0;

    public readonly bool IsPairTop => (Flags & PairTop) != 0;

    public readonly bool IsTop => (Flags & (PairTop | GhostTop)) != 0;

    public readonly bool IsBottom => (Flags & (PairBottom | GhostBottom)) != 0;

    public readonly bool IsLocked => (Flags & Locked) != 0;

    public readonly bool IsSynthetic => (Flags & Synthetic) != 0;
}

/// <summary>A hint mask (<c>CF2_HintMaskRec</c>): which stems are active, read from a charstring or set all on.</summary>
internal sealed class CffHintMask
{
    public readonly byte[] Mask = new byte[(Type2Interpreter.MaxHints + 7) / 8];

    public bool IsValid;
    public bool IsNew;
    public long BitCount;

    /// <summary><c>cf2_hintmask_init</c>.</summary>
    public void Reset()
    {
        IsValid = IsNew = false;
        BitCount = 0;
        Array.Clear(Mask);
    }

    public void CopyFrom(CffHintMask other)
    {
        IsValid = other.IsValid;
        IsNew = other.IsNew;
        BitCount = other.BitCount;
        other.Mask.CopyTo(Mask, 0);
    }

    /// <summary><c>cf2_hintmask_setCounts</c>: false past 96 stems (FreeType's error), the mask then left as it was.</summary>
    public bool SetCounts(long bitCount)
    {
        if (bitCount > Type2Interpreter.MaxHints)
            return false;

        BitCount = bitCount;
        IsValid = true;
        IsNew = true;
        return true;
    }

    /// <summary><c>cf2_hintmask_setAll</c>: every stem on (the unused bits of the last byte off); false past 96 stems.</summary>
    public bool SetAll(long bitCount)
    {
        if (!SetCounts(bitCount))
            return false;

        var byteCount = (int)((bitCount + 7) / 8);
        for (var i = 0; i < byteCount; i++)
            Mask[i] = 0xFF;

        if (byteCount > 0)
            Mask[byteCount - 1] &= (byte)~((1 << (-(int)bitCount & 7)) - 1);

        return true;
    }
}

/// <summary>
/// A hint map (<c>CF2_HintMapRec</c>): the active stems' edges sorted by character-space position, each with its device-space
/// position, mapping a character-space y to device space piecewise linearly. Up to 192 edges (<c>CF2_MAX_HINT_EDGES</c>).
/// </summary>
internal sealed class CffHintMap
{
    /// <summary><c>CF2_MAX_HINT_EDGES</c>.</summary>
    public const int MaxEdges = Type2Interpreter.MaxHints * 2;

    public readonly CffHint[] Edges = new CffHint[MaxEdges];

    public int Count;
    public int LastIndex;
    public bool IsValid;
    public bool Hinted;
    public int Scale;

    // Set by Init, which CffGlyphHints.Reset calls on every map before a run uses it (and EndCounterMask on the counter map).
    /// <summary>The map device positions of new stems are taken from (the initial map, which is its own).</summary>
    public CffHintMap InitialMap = null!;

    /// <summary><c>cf2_hintmap_init</c>.</summary>
    public void Init(bool hinted, int scale, CffHintMap initialMap)
    {
        Count = 0;
        LastIndex = 0;
        IsValid = false;
        Hinted = hinted;
        Scale = scale;
        InitialMap = initialMap;
    }

    /// <summary>A structure copy (<c>firstHintMap = hintMap</c>); the edges past the count are never read, so not copied.</summary>
    public void CopyFrom(CffHintMap other)
    {
        Array.Copy(other.Edges, Edges, other.Count);
        Count = other.Count;
        LastIndex = other.LastIndex;
        IsValid = other.IsValid;
        Hinted = other.Hinted;
        Scale = other.Scale;
        InitialMap = other.InitialMap;
    }

    /// <summary>
    /// <c>cf2_hintmap_map</c>: a character-space y in device space; uniform scale without hints (or unhinted), else the scale
    /// of the highest edge at or below it, from that edge's device position (below the first edge, the uniform scale from it).
    /// </summary>
    public int Map(int csCoord, ICffHintHost host)
    {
        if (Count == 0 || !Hinted)
            return CffFixed.MulFix32(csCoord, Scale);

        // The linear search from the last hit walks at most the map's edges.
        host.Charge(Count);
        var i = LastIndex;
        while (i < Count - 1 && csCoord >= Edges[i + 1].CsCoord)
            i++;

        while (i > 0 && csCoord < Edges[i].CsCoord)
            i--;

        LastIndex = i;
        ref readonly var edge = ref Edges[i];
        if (i == 0 && csCoord < edge.CsCoord)
            return Add(CffFixed.MulFix32(Sub(csCoord, edge.CsCoord), Scale), edge.DsCoord);

        return Add(CffFixed.MulFix32(Sub(csCoord, edge.CsCoord), edge.Scale), edge.DsCoord);
    }

    internal static int Add(int a, int b) => (int)((uint)a + (uint)b);

    internal static int Sub(int a, int b) => (int)((uint)a - (uint)b);
}

/// <summary>What the hint maps need of the interpreter: its glyph work budget and its error state.</summary>
internal interface ICffHintHost
{
    /// <summary>Charges glyph work (throws <see cref="NotSupportedException"/> past the budget).</summary>
    void Charge(long units);

    /// <summary>Records FreeType's error for the glyph (the first one stays).</summary>
    void SetError(string message);
}

/// <summary>
/// The hinting state of one charstring run (FreeType's locals of <c>cf2_interpT2CharString</c> and its glyph path): the
/// horizontal stem hints (vertical ones are only counted: the engine hints in y), the hint mask, the initial, first and
/// current hint maps, and the builds of a hint map (<c>cf2_hintmap_build</c>). Without hinting (<see cref="Hinted"/> false)
/// a build keeps only its bookkeeping (validity, the 96-stem error), which is all an unhinted outline depends on.
/// </summary>
internal sealed class CffGlyphHints
{
    // The hint moves (CF2_HintMoveRec: upper edge, move up) the second adjustment pass retries.
    private readonly int[] m_moveEdge = new int[CffHintMap.MaxEdges];
    private readonly int[] m_moveUp = new int[CffHintMap.MaxEdges];

    // The stems beyond 96 are only counted: any build with more than 96 stems fails the glyph.
    private readonly int[] m_stemMin = new int[Type2Interpreter.MaxHints];
    private readonly int[] m_stemMax = new int[Type2Interpreter.MaxHints];
    private readonly int[] m_stemMinDs = new int[Type2Interpreter.MaxHints];
    private readonly int[] m_stemMaxDs = new int[Type2Interpreter.MaxHints];
    private readonly bool[] m_stemUsed = new bool[Type2Interpreter.MaxHints];

    private readonly CffHintMask m_tempMask = new();
    private readonly CffHintMask m_workMask = new();
    private readonly CffHintMask m_counterMask = new();
    private readonly CffHintMap m_counterMap = new();

    // Set by Reset, which the interpreter calls at the start of every run, before any other member is used.
    private ICffHintHost m_host = null!;
    private CffBlues? m_blues;

    public readonly CffHintMask Mask = new();
    public readonly CffHintMap InitialMap = new();
    public readonly CffHintMap FirstMap = new();
    public readonly CffHintMap CurrentMap = new();

    /// <summary>The horizontal stems so far (only the first 96 are kept).</summary>
    public long HStemCount { get; private set; }

    /// <summary>The vertical stems so far.</summary>
    public long VStemCount { get; private set; }

    /// <summary>The run is hinted (FreeType's <c>font->hinted</c>).</summary>
    public bool Hinted { get; private set; }

    /// <summary>The character-space y of a seac accent's origin, added to its stems (<c>hintOriginY</c>).</summary>
    public int HintOriginY { get; private set; }

    /// <summary>The engine's vertical scale (<c>innerTransform.d</c>).</summary>
    public int ScaleY { get; private set; }

    /// <summary>
    /// Starts a run (<c>cf2_glyphpath_init</c>, <c>cf2_hintmask_init</c>, the stem arrays emptied).
    /// </summary>
    /// <param name="host">The interpreter: work budget and errors.</param>
    /// <param name="blues">The Font DICT's blue zones at the size; null when unhinted.</param>
    /// <param name="scaleY">The vertical scale.</param>
    /// <param name="hintOriginY">The run's origin y (a seac accent's offset, else 0).</param>
    public void Reset(ICffHintHost host, CffBlues? blues, int scaleY, int hintOriginY)
    {
        m_host = host;
        m_blues = blues;
        Hinted = blues is not null;
        ScaleY = scaleY;
        HintOriginY = hintOriginY;
        HStemCount = VStemCount = 0;
        Mask.Reset();
        InitialMap.Init(Hinted, scaleY, InitialMap);
        FirstMap.Init(Hinted, scaleY, InitialMap);
        CurrentMap.Init(Hinted, scaleY, InitialMap);
    }

    /// <summary>Adds a horizontal stem (<c>cf2_doStems</c> pushing a <c>CF2_StemHintRec</c>).</summary>
    public void AddHStem(int min, int max)
    {
        if (HStemCount < Type2Interpreter.MaxHints)
        {
            var i = (int)HStemCount;
            m_stemMin[i] = min;
            m_stemMax[i] = max;
            m_stemMinDs[i] = m_stemMaxDs[i] = 0;
            m_stemUsed[i] = false;
        }

        HStemCount++;
    }

    /// <summary>Adds vertical stems (counted only).</summary>
    public void AddVStems(long count) => VStemCount += count;

    /// <summary>
    /// The start of <c>cf2_hintmask_read</c> for the hint mask (<paramref name="counter"/> false) or a fresh counter mask: its
    /// counts set for the stems so far; returns the mask, whose first <paramref name="bytes"/> bytes the caller then reads from
    /// the charstring. Past 96 stems FreeType's error is recorded and <paramref name="bytes"/> is 0 (nothing is read).
    /// </summary>
    public CffHintMask BeginMask(bool counter, out int bytes)
    {
        var mask = Mask;
        if (counter)
        {
            mask = m_counterMask;
            mask.Reset();
        }

        bytes = 0;
        var bits = HStemCount + VStemCount;
        if (!mask.SetCounts(bits))
        {
            m_host.SetError($"{bits} stem hints, more than FreeType's {Type2Interpreter.MaxHints}");
            return mask;
        }

        bytes = (int)((bits + 7) / 8);
        m_host.Charge(bytes);
        return mask;
    }

    /// <summary>
    /// The end of a counter mask (<c>cntrmask</c>): a throw-away hint map is built from it with origin 0, which builds the
    /// initial map if it is not yet and fixes the device positions of the stems it holds.
    /// </summary>
    public void EndCounterMask()
    {
        m_counterMap.Init(Hinted, ScaleY, InitialMap);
        Build(m_counterMap, m_counterMask, 0, initialMap: false);
    }

    /// <summary>The current map is built with the current mask (<c>cf2_glyphpath_moveTo</c>, <c>lineTo</c>, <c>curveTo</c>).</summary>
    public void BuildCurrent() => Build(CurrentMap, Mask, HintOriginY, initialMap: false);

    /// <summary>The first map becomes a copy of the current one (at a moveto).</summary>
    public void SaveFirstMap()
    {
        if (Hinted)
            m_host.Charge(CurrentMap.Count);

        FirstMap.CopyFrom(CurrentMap);
    }

    /// <summary><c>cf2_hintmap_build</c>.</summary>
    private void Build(CffHintMap hintmap, CffHintMask hintMask, int hintOrigin, bool initialMap)
    {
        // The initial map comes first, built with every stem on.
        if (!initialMap && !hintmap.InitialMap.IsValid)
        {
            m_tempMask.Reset();
            Build(hintmap.InitialMap, m_tempMask, hintOrigin, initialMap: true);
        }

        if (!hintMask.IsValid)
        {
            var total = HStemCount + VStemCount;
            if (!hintMask.SetAll(total))
            {
                m_host.SetError($"{total} stem hints, more than FreeType's {Type2Interpreter.MaxHints}");
                return;
            }
        }

        hintmap.Count = 0;
        hintmap.LastIndex = 0;

        // Only the horizontal stems, which come first in the mask. ("Defense-in-depth. Should never return here.")
        var bitCount = (int)HStemCount;
        if (HStemCount > hintMask.BitCount)
            return;

        if (Hinted)
            Fill(hintmap, hintMask, hintOrigin, initialMap, bitCount);

        hintmap.IsValid = true;
        hintMask.IsNew = false;
    }

    /// <summary>The edges of <c>cf2_hintmap_build</c>: em box, captured and locked stems, the rest; adjusted and saved.</summary>
    private void Fill(CffHintMap hintmap, CffHintMask hintMask, int hintOrigin, bool initialMap, int bitCount)
    {
        // Fill runs only when Hinted, which Reset sets exactly when the blue zones are given.
        var blues = m_blues!;
        var tempMask = m_workMask;
        tempMask.CopyFrom(hintMask);

        // Synthetic em-box hints first.
        if (blues.DoEmBoxHints)
        {
            var dummy = default(CffHint);
            var bottom = blues.EmBoxBottomEdge;
            var top = blues.EmBoxTopEdge;
            InsertHint(hintmap, ref bottom, ref dummy);
            dummy = default;
            InsertHint(hintmap, ref dummy, ref top);
        }

        // Stems captured by a blue zone or already placed.
        m_host.Charge(bitCount);
        for (var i = 0; i < bitCount; i++)
        {
            var bit = (byte)(0x80 >> (i & 7));
            if ((tempMask.Mask[i >> 3] & bit) == 0)
                continue;

            var bottomEdge = HintInit(i, hintOrigin, hintmap.Scale, bottom: true);
            var topEdge = HintInit(i, hintOrigin, hintmap.Scale, bottom: false);
            if (bottomEdge.IsLocked || topEdge.IsLocked || blues.Capture(ref bottomEdge, ref topEdge))
            {
                InsertHint(hintmap, ref bottomEdge, ref topEdge);
                tempMask.Mask[i >> 3] &= (byte)~bit;
            }
        }

        if (initialMap)
        {
            // A locked edge at 0 when every edge is on one side of the baseline.
            if (hintmap.Count == 0 || hintmap.Edges[0].CsCoord > 0 || hintmap.Edges[hintmap.Count - 1].CsCoord < 0)
            {
                var edge = new CffHint { Flags = CffHint.GhostBottom | CffHint.Locked | CffHint.Synthetic, Scale = hintmap.Scale };
                var invalid = default(CffHint);
                InsertHint(hintmap, ref edge, ref invalid);
            }
        }
        else
        {
            // The remaining stems.
            m_host.Charge(bitCount);
            for (var i = 0; i < bitCount; i++)
            {
                if ((tempMask.Mask[i >> 3] & (0x80 >> (i & 7))) == 0)
                    continue;

                var bottomEdge = HintInit(i, hintOrigin, hintmap.Scale, bottom: true);
                var topEdge = HintInit(i, hintOrigin, hintmap.Scale, bottom: false);
                InsertHint(hintmap, ref bottomEdge, ref topEdge);
            }
        }

        AdjustHints(hintmap);

        // Remember where every stem of this map went, to place it there again in later maps.
        if (!initialMap)
        {
            m_host.Charge(hintmap.Count);
            for (var i = 0; i < hintmap.Count; i++)
            {
                ref readonly var edge = ref hintmap.Edges[i];
                if (edge.IsSynthetic)
                    continue;

                if (edge.IsTop)
                    m_stemMaxDs[edge.Index] = edge.DsCoord;
                else
                    m_stemMinDs[edge.Index] = edge.DsCoord;

                m_stemUsed[edge.Index] = true;
            }
        }
    }

    /// <summary><c>cf2_hint_init</c>: one edge of a stem (ghost, inverted or normal pair), its device position the saved one if the stem was placed before.</summary>
    private CffHint HintInit(int index, int hintOrigin, int scale, bool bottom)
    {
        var hint = default(CffHint);
        var min = m_stemMin[index];
        var max = m_stemMax[index];
        var width = CffHintMap.Sub(max, min);

        if (width == -21 << 16)
        {
            // Ghost bottom.
            if (bottom)
            {
                hint.CsCoord = max;
                hint.Flags = CffHint.GhostBottom;
            }
        }
        else if (width == -20 << 16)
        {
            // Ghost top.
            if (!bottom)
            {
                hint.CsCoord = min;
                hint.Flags = CffHint.GhostTop;
            }
        }
        else if (width < 0)
        {
            // An inverted pair: CoolType negates the width.
            if (bottom)
            {
                hint.CsCoord = max;
                hint.Flags = CffHint.PairBottom;
            }
            else
            {
                hint.CsCoord = min;
                hint.Flags = CffHint.PairTop;
            }
        }
        else if (bottom)
        {
            hint.CsCoord = min;
            hint.Flags = CffHint.PairBottom;
        }
        else
        {
            hint.CsCoord = max;
            hint.Flags = CffHint.PairTop;
        }

        // A top edge moves up by twice the darkening amount, which is 0 without stem darkening.
        hint.CsCoord = CffHintMap.Add(hint.CsCoord, hintOrigin);
        hint.Scale = scale;
        hint.Index = index;

        if (hint.Flags != 0 && m_stemUsed[index])
        {
            hint.DsCoord = hint.IsTop ? m_stemMaxDs[index] : m_stemMinDs[index];
            hint.Flags |= CffHint.Locked;
        }
        else
        {
            hint.DsCoord = CffFixed.MulFix32(hint.CsCoord, scale);
        }

        return hint;
    }

    /// <summary>
    /// <c>cf2_hintmap_insertHint</c>: inserts one edge or a pair in character-space order, dropping it where it would overlap
    /// (touch) an edge in character space or cross one in device space; an unlocked new stem is first placed by the initial
    /// map (its centre mapped, its width scaled uniformly).
    /// </summary>
    private void InsertHint(CffHintMap hintmap, ref CffHint bottomHintEdge, ref CffHint topHintEdge)
    {
        m_host.Charge(hintmap.Count + 1);

        var isPair = true;
        var firstIsTop = false;
        if (!bottomHintEdge.IsValid)
        {
            firstIsTop = true;
            isPair = false;
        }
        else if (!topHintEdge.IsValid)
        {
            isPair = false;
        }

        ref var first = ref firstIsTop ? ref topHintEdge : ref bottomHintEdge;
        ref var second = ref topHintEdge;

        if (isPair && topHintEdge.CsCoord < bottomHintEdge.CsCoord)
            return;

        var indexInsert = 0;
        for (; indexInsert < hintmap.Count; indexInsert++)
        {
            if (hintmap.Edges[indexInsert].CsCoord >= first.CsCoord)
                break;
        }

        // Overlaps in character space (touching included).
        if (indexInsert < hintmap.Count)
        {
            if (hintmap.Edges[indexInsert].CsCoord == first.CsCoord)
                return;

            if (isPair && hintmap.Edges[indexInsert].CsCoord <= second.CsCoord)
                return;

            if (hintmap.Edges[indexInsert].IsPairTop)
                return;
        }

        // Device positions from the initial map.
        if (hintmap.InitialMap.IsValid && !first.IsLocked)
        {
            if (isPair)
            {
                var halfSpan = CffHintMap.Sub(second.CsCoord, first.CsCoord) / 2;
                var midpoint = hintmap.InitialMap.Map(CffHintMap.Add(first.CsCoord, halfSpan), m_host);
                var halfWidth = CffFixed.MulFix32(halfSpan, hintmap.Scale);
                first.DsCoord = CffHintMap.Sub(midpoint, halfWidth);
                second.DsCoord = CffHintMap.Add(midpoint, halfWidth);
            }
            else
            {
                first.DsCoord = hintmap.InitialMap.Map(first.CsCoord, m_host);
            }
        }

        // Overlaps in device space (locked hints moved to blue zones can cross).
        if (indexInsert > 0 && first.DsCoord < hintmap.Edges[indexInsert - 1].DsCoord)
            return;

        if (indexInsert < hintmap.Count)
        {
            if (isPair ? second.DsCoord > hintmap.Edges[indexInsert].DsCoord : first.DsCoord > hintmap.Edges[indexInsert].DsCoord)
                return;
        }

        var iDst = isPair ? hintmap.Count + 1 : hintmap.Count;
        if (iDst >= CffHintMap.MaxEdges)
            return;

        var shift = isPair ? 2 : 1;
        Array.Copy(hintmap.Edges, indexInsert, hintmap.Edges, indexInsert + shift, hintmap.Count - indexInsert);
        hintmap.Edges[indexInsert] = first;
        hintmap.Count++;
        if (isPair)
        {
            hintmap.Edges[indexInsert + 1] = second;
            hintmap.Count++;
        }
    }

    /// <summary>
    /// <c>cf2_hintmap_adjustHints</c>: moves each unlocked stem (both edges together) so one edge lands on a pixel boundary,
    /// the smaller move when there is room either way, keeping half a pixel of counter; a second, top-down pass retries the
    /// moves up that a stem above blocked; then the scales between edges.
    /// </summary>
    private void AdjustHints(CffHintMap hintmap)
    {
        m_host.Charge(2L * hintmap.Count + 1);
        var moves = 0;
        var edges = hintmap.Edges;
        var count = hintmap.Count;

        for (var i = 0; i < count; i++)
        {
            var isPair = edges[i].IsPair;
            var j = isPair ? i + 1 : i;
            var dsCoordI = edges[i].DsCoord;
            var dsCoordJ = edges[j].DsCoord;

            if (!edges[i].IsLocked)
            {
                var fracDown = Fraction(dsCoordI);
                var fracUp = Fraction(dsCoordJ);
                var downMoveDown = 0 - fracDown;
                var upMoveDown = 0 - fracUp;
                var downMoveUp = fracDown == 0 ? 0 : (1 << 16) - fracDown;
                var upMoveUp = fracUp == 0 ? 0 : (1 << 16) - fracUp;
                var moveUp = Math.Min(downMoveUp, upMoveUp);
                var moveDown = Math.Max(downMoveDown, upMoveDown);
                int move;
                var saveEdge = false;

                if (j >= count - 1 || edges[j + 1].DsCoord >= CffHintMap.Add(dsCoordJ, moveUp + CffHint.MinCounter))
                {
                    if (i == 0 || edges[i - 1].DsCoord <= CffHintMap.Add(dsCoordI, moveDown - CffHint.MinCounter))
                        move = -moveDown < moveUp ? moveDown : moveUp;
                    else
                        move = moveUp;
                }
                else if (i == 0 || edges[i - 1].DsCoord <= CffHintMap.Add(dsCoordI, moveDown - CffHint.MinCounter))
                {
                    move = moveDown;
                    saveEdge = moveUp < -moveDown;
                }
                else
                {
                    move = 0;
                    saveEdge = true;
                }

                // Non-moves and moves down that are not optimal, below an unlocked edge, are retried in the second pass.
                if (saveEdge && j < count - 1 && !edges[j + 1].IsLocked)
                {
                    m_moveEdge[moves] = j;
                    m_moveUp[moves] = moveUp - move;
                    moves++;
                }

                edges[i].DsCoord = CffHintMap.Add(dsCoordI, move);
                if (isPair)
                    edges[j].DsCoord = CffHintMap.Add(dsCoordJ, move);
            }

            // The scales, avoiding a division by zero.
            if (i > 0 && edges[i].CsCoord != edges[i - 1].CsCoord)
            {
                edges[i - 1].Scale = (int)CffFixed.DivFix(CffHintMap.Sub(edges[i].DsCoord, edges[i - 1].DsCoord),
                    CffHintMap.Sub(edges[i].CsCoord, edges[i - 1].CsCoord));
            }

            if (isPair)
            {
                if (edges[j].CsCoord != edges[j - 1].CsCoord)
                {
                    edges[j - 1].Scale = (int)CffFixed.DivFix(CffHintMap.Sub(edges[j].DsCoord, edges[j - 1].DsCoord),
                        CffHintMap.Sub(edges[j].CsCoord, edges[j - 1].CsCoord));
                }

                i++;
            }
        }

        // The second pass, top-down: move up where there is room now.
        for (var k = moves; k > 0; k--)
        {
            var j = m_moveEdge[k - 1];
            var moveUp = m_moveUp[k - 1];
            if (edges[j + 1].DsCoord >= CffHintMap.Add(edges[j].DsCoord, moveUp + CffHint.MinCounter))
            {
                edges[j].DsCoord = CffHintMap.Add(edges[j].DsCoord, moveUp);
                if (edges[j].IsPair)
                    edges[j - 1].DsCoord = CffHintMap.Add(edges[j - 1].DsCoord, moveUp);
            }
        }
    }

    /// <summary><c>cf2_fixedFraction</c>.</summary>
    private static int Fraction(int value) => value - (int)((uint)value & 0xFFFF0000u);
}
