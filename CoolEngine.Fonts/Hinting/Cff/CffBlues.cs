// Follows FreeType 2.13.2 src/psaux/psblues.c (cf2_blues_init, cf2_blues_capture), src/psaux/psblues.h (the zones, the
// synthetic em box, CF2_MIN_COUNTER) and src/psaux/psft.c (cf2_getBlueMetrics, cf2_getBlueValues and its siblings,
// cf2_getLanguageGroup), Adobe's CFF engine in FreeType (FreeType Project License; credit in docs).

using CoolEngine.Fonts.Cff;

namespace CoolEngine.Fonts.Hinting.Cff;

/// <summary>
/// The blue zones of one Font DICT at one vertical scale, as FreeType 2.13.2's Adobe engine builds them
/// (<c>cf2_blues_init</c>) with stem darkening off: up to 7 zones from BlueValues (the first a bottom zone, the others top
/// zones) and 5 bottom zones from OtherBlues, each flat edge moved to the nearest FamilyBlues or FamilyOtherBlues edge less
/// than a pixel away, BlueScale clamped to the tallest zone, overshoot suppressed (with a boost up to half a pixel) below
/// BlueScale, the flat edges rounded to whole pixels; or, for LanguageGroup 1 without real zones, the synthetic em-box ghost
/// hints. <see cref="Capture"/> is <c>cf2_blues_capture</c>. All arithmetic is the engine's: 32-bit 16.16 values with
/// wrapping sums. Immutable once built.
/// </summary>
internal sealed class CffBlues
{
    /// <summary><c>CF2_MAX_BLUES</c> + <c>CF2_MAX_OTHERBLUES</c>.</summary>
    private const int MaxZones = 12;

    // CF2_ICF_Top and CF2_ICF_Bottom (psblues.h): the em box for a 1000-unit em, 880 and -120.
    private const int IcfTop = 880 << 16, IcfBottom = -120 << 16;

    // cf2_doubleToFixed(.6): (int)(0.6 * 65536 + 0.5).
    private const int SixTenths = 39322;

    private readonly Zone[] m_zones = new Zone[MaxZones];

    /// <param name="hints">The Font DICT's Private DICT values (at the size's variation coordinates for CFF2).</param>
    /// <param name="scale">The engine's vertical scale (<c>innerTransform.d</c>): font units to 16.16 pixels.</param>
    public CffBlues(CffPrivateHints hints, int scale)
    {
        Scale = scale;

        // cf2_getBlueMetrics: BlueScale is stored 1000 times larger; BlueShift and BlueFuzz are integers.
        var blueScale = (int)CffFixed.DivFix(hints.BlueScale, 1000 << 16);
        BlueShift = IntToFixed(hints.BlueShift);
        BlueFuzz = IntToFixed(hints.BlueFuzz);

        var blueValues = hints.BlueValues;
        var numBlueValues = hints.BlueValueCount;

        // The synthetic em-box heuristic: an ideographic Private DICT (LanguageGroup 1) without real zones, or with the dummy
        // zones Adobe's tools write at -250 and 1100.
        if (hints.LanguageGroup == 1 &&
            (numBlueValues == 0 ||
             (numBlueValues == 4 && IntToFixed(blueValues[0]) < IcfBottom && IntToFixed(blueValues[1]) < IcfBottom &&
              IntToFixed(blueValues[2]) > IcfTop && IntToFixed(blueValues[3]) > IcfTop)))
        {
            // The edges move outward by an epsilon (0x.0001), and their device positions by CF2_MIN_COUNTER.
            var bottom = IcfBottom - 1;
            EmBoxBottomEdge = new CffHint
            {
                CsCoord = bottom,
                DsCoord = FixedRound(CffFixed.MulFix(bottom, scale)) - CffHint.MinCounter,
                Scale = scale,
                Flags = CffHint.GhostBottom | CffHint.Locked | CffHint.Synthetic,
            };

            var top = IcfTop + 1;
            EmBoxTopEdge = new CffHint
            {
                CsCoord = top,
                DsCoord = FixedRound(CffFixed.MulFix(top, scale)) + CffHint.MinCounter,
                Scale = scale,
                Flags = CffHint.GhostTop | CffHint.Locked | CffHint.Synthetic,
            };

            DoEmBoxHints = true;
            BlueScale = blueScale;
            return;
        }

        var maxZoneHeight = 0;
        var count = 0;

        // BlueValues: the first pair is a bottom zone (its top edge flat), the others top zones (their bottom edge flat). A
        // zone of negative height is dropped.
        for (var i = 0; i < numBlueValues; i += 2)
        {
            var zoneBottom = IntToFixed(blueValues[i]);
            var zoneTop = IntToFixed(blueValues[i + 1]);
            var zoneHeight = Sub(zoneTop, zoneBottom);
            if (zoneHeight < 0)
                continue;

            if (zoneHeight > maxZoneHeight)
                maxZoneHeight = zoneHeight;

            var bottomZone = i == 0;
            m_zones[count++] = new Zone(zoneBottom, zoneTop, bottomZone ? zoneTop : zoneBottom, 0, bottomZone);
        }

        // OtherBlues: bottom zones. FreeType reads the values in pairs up to the count, so an odd count reads one slot past it.
        var otherBlues = hints.OtherBlues;
        for (var i = 0; i < hints.OtherBlueCount; i += 2)
        {
            var zoneBottom = IntToFixed(otherBlues[i]);
            var zoneTop = IntToFixed(otherBlues[i + 1]);
            var zoneHeight = Sub(zoneTop, zoneBottom);
            if (zoneHeight < 0)
                continue;

            if (zoneHeight > maxZoneHeight)
                maxZoneHeight = zoneHeight;

            m_zones[count++] = new Zone(zoneBottom, zoneTop, zoneTop, 0, true);
        }

        // FamilyBlues and FamilyOtherBlues: the nearest family flat edge within one device pixel replaces the zone's.
        var csUnitsPerPixel = (int)CffFixed.DivFix(1 << 16, scale);
        var familyBlues = hints.FamilyBlues;
        var familyOtherBlues = hints.FamilyOtherBlues;
        for (var i = 0; i < count; i++)
        {
            ref var zone = ref m_zones[i];
            var flatEdge = zone.CsFlatEdge;
            var minDiff = int.MaxValue;
            if (zone.BottomZone)
            {
                for (var j = 0; j < hints.FamilyOtherBlueCount; j += 2)
                {
                    var flatFamilyEdge = IntToFixed(familyOtherBlues[j + 1]);
                    var diff = CffFixed.Abs32(Sub(flatEdge, flatFamilyEdge));
                    if (diff < minDiff && diff < csUnitsPerPixel)
                    {
                        zone.CsFlatEdge = flatFamilyEdge;
                        minDiff = diff;
                        if (diff == 0)
                            break;
                    }
                }

                // The first FamilyBlues pair is a bottom zone.
                if (hints.FamilyBlueCount >= 2)
                {
                    var flatFamilyEdge = IntToFixed(familyBlues[1]);
                    var diff = CffFixed.Abs32(Sub(flatEdge, flatFamilyEdge));
                    if (diff < minDiff && diff < csUnitsPerPixel)
                        zone.CsFlatEdge = flatFamilyEdge;
                }
            }
            else
            {
                for (var j = 2; j < hints.FamilyBlueCount; j += 2)
                {
                    var flatFamilyEdge = IntToFixed(familyBlues[j]);
                    var diff = CffFixed.Abs32(Sub(flatEdge, flatFamilyEdge));
                    if (diff < minDiff && diff < csUnitsPerPixel)
                    {
                        zone.CsFlatEdge = flatFamilyEdge;
                        minDiff = diff;
                        if (diff == 0)
                            break;
                    }
                }
            }
        }

        // BlueScale is clamped so the tallest zone stays below one pixel.
        if (maxZoneHeight > 0)
        {
            var limit = CffFixed.DivFix(1 << 16, maxZoneHeight);
            if (blueScale > limit)
                blueScale = (int)limit;
        }

        // Below BlueScale, overshoot is suppressed and the zones are boosted: 0.6 pixel at scale 0 down to 0 at BlueScale,
        // below half a pixel.
        if (scale < blueScale)
        {
            SuppressOvershoot = true;
            Boost = (int)(SixTenths - CffFixed.MulDiv(SixTenths, scale, blueScale));
            if (Boost > 0x7FFF)
                Boost = 0x7FFF;
        }

        // The flat edges in device space, rounded after the boost.
        for (var i = 0; i < count; i++)
        {
            ref var zone = ref m_zones[i];
            var scaled = CffFixed.MulFix(zone.CsFlatEdge, scale);
            zone.DsFlatEdge = FixedRound(zone.BottomZone ? scaled - Boost : scaled + Boost);
        }

        Count = count;
        BlueScale = blueScale;
    }

    /// <summary>The vertical scale the zones were built for.</summary>
    public int Scale { get; }

    /// <summary>The number of zones.</summary>
    public int Count { get; }

    /// <summary>BlueScale (16.16), clamped to the tallest zone.</summary>
    public int BlueScale { get; }

    /// <summary>BlueShift in 16.16 font units.</summary>
    public int BlueShift { get; }

    /// <summary>BlueFuzz in 16.16 font units.</summary>
    public int BlueFuzz { get; }

    /// <summary>The size is below BlueScale: captured edges go to the zone's flat edge.</summary>
    public bool SuppressOvershoot { get; }

    /// <summary>The device-space boost of the flat edges at small sizes (16.16, below half a pixel).</summary>
    public int Boost { get; }

    /// <summary>The synthetic em-box ghost hints replace the zones (LanguageGroup 1).</summary>
    public bool DoEmBoxHints { get; }

    public CffHint EmBoxTopEdge { get; }

    public CffHint EmBoxBottomEdge { get; }

    /// <summary>
    /// <c>cf2_blues_capture</c>: whether a zone captures the stem's bottom edge (bottom zones) or top edge (top zones), zones
    /// searched in order, both kinds in one pass; a captured stem's valid edges move by the same amount (to the flat edge when
    /// overshoot is suppressed; at least a pixel beyond it when BlueShift asks for overshoot; else to the nearest pixel) and
    /// are locked.
    /// </summary>
    public bool Capture(ref CffHint bottomHintEdge, ref CffHint topHintEdge)
    {
        var csFuzz = BlueFuzz;
        var dsMove = 0;
        var captured = false;

        for (var i = 0; i < Count; i++)
        {
            ref readonly var zone = ref m_zones[i];
            if (zone.BottomZone && bottomHintEdge.IsBottom)
            {
                if (Sub(zone.CsBottomEdge, csFuzz) <= bottomHintEdge.CsCoord && bottomHintEdge.CsCoord <= Add(zone.CsTopEdge, csFuzz))
                {
                    int dsNew;
                    if (SuppressOvershoot)
                        dsNew = zone.DsFlatEdge;
                    else if (Sub(zone.CsTopEdge, bottomHintEdge.CsCoord) >= BlueShift)
                        dsNew = Math.Min(FixedRound(bottomHintEdge.DsCoord), Sub(zone.DsFlatEdge, 1 << 16));
                    else
                        dsNew = FixedRound(bottomHintEdge.DsCoord);

                    dsMove = Sub(dsNew, bottomHintEdge.DsCoord);
                    captured = true;
                    break;
                }
            }

            if (!zone.BottomZone && topHintEdge.IsTop)
            {
                if (Sub(zone.CsBottomEdge, csFuzz) <= topHintEdge.CsCoord && topHintEdge.CsCoord <= Add(zone.CsTopEdge, csFuzz))
                {
                    int dsNew;
                    if (SuppressOvershoot)
                        dsNew = zone.DsFlatEdge;
                    else if (Sub(topHintEdge.CsCoord, zone.CsBottomEdge) >= BlueShift)
                        dsNew = Math.Max(FixedRound(topHintEdge.DsCoord), Add(zone.DsFlatEdge, 1 << 16));
                    else
                        dsNew = FixedRound(topHintEdge.DsCoord);

                    dsMove = Sub(dsNew, topHintEdge.DsCoord);
                    captured = true;
                    break;
                }
            }
        }

        if (captured)
        {
            if (bottomHintEdge.IsValid)
            {
                bottomHintEdge.DsCoord = Add(bottomHintEdge.DsCoord, dsMove);
                bottomHintEdge.Flags |= CffHint.Locked;
            }

            if (topHintEdge.IsValid)
            {
                topHintEdge.DsCoord = Add(topHintEdge.DsCoord, dsMove);
                topHintEdge.Flags |= CffHint.Locked;
            }
        }

        return captured;
    }

    /// <summary><c>cf2_blueToFixed</c> (<c>cf2_intToFixed</c>): the low 16 bits of a font-unit value as 16.16.</summary>
    private static int IntToFixed(long value) => (int)((uint)value << 16);

    /// <summary><c>cf2_fixedRound</c> of a value FreeType computes in its 64-bit <c>FT_Long</c> and rounds in 32 bits.</summary>
    internal static int FixedRound(long value) => (int)(((uint)value + 0x8000u) & 0xFFFF0000u);

    private static int Add(int a, int b) => (int)((uint)a + (uint)b);

    private static int Sub(int a, int b) => (int)((uint)a - (uint)b);

    /// <summary><c>CF2_BlueRec</c>.</summary>
    private struct Zone(int csBottomEdge, int csTopEdge, int csFlatEdge, int dsFlatEdge, bool bottomZone)
    {
        public readonly int CsBottomEdge = csBottomEdge;
        public readonly int CsTopEdge = csTopEdge;
        public int CsFlatEdge = csFlatEdge;
        public int DsFlatEdge = dsFlatEdge;
        public readonly bool BottomZone = bottomZone;
    }
}
