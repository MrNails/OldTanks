// Follows FreeType 2.13.2 src/truetype/ttgxvar.c (ft_var_load_gvar, TT_Vary_Apply_Glyph_Deltas, tt_interpolate_deltas,
// tt_delta_interpolate, tt_delta_shift) (FreeType Project License; credit in docs).

using CoolEngine.Fonts.Sfnt;

namespace CoolEngine.Fonts.Variations;

/// <summary>Counts per-glyph work against a budget; <see cref="Charge"/> throws <see cref="NotSupportedException"/> past it.</summary>
internal interface IWorkCounter
{
    void Charge(long units);
}

/// <summary>A work counter with its own budget, for work outside a glyph load (a size's variation set-up).</summary>
/// <param name="budget">The most work allowed.</param>
/// <param name="what">What the budget covers, for the message.</param>
internal sealed class WorkCounter(long budget, string what) : IWorkCounter
{
    public long Work { get; private set; }

    public void Charge(long units)
    {
        Work += units;
        if (Work > budget)
            throw new NotSupportedException(
                $"{what} takes more work than this library allows ({budget} units; charged {Work}).");
    }
}

/// <summary>
/// The <c>gvar</c> table: per-glyph tuple variation data, checked in full at load, and the deltas of a glyph at normalized
/// coordinates as FreeType's <c>TT_Vary_Apply_Glyph_Deltas</c> computes them (<see cref="ComputeDeltas"/>). Immutable.
/// </summary>
/// <remarks>
/// <para>
/// Checked at load (<see cref="InvalidDataException"/>; FreeType fails the coordinates or the glyph on each): a header past the
/// table; a version other than 1.0; an axis count other than <c>fvar</c>'s; the shared tuples or the glyph offsets past the
/// table; glyph offsets that decrease or lie past the table (FreeType clamps them); and, for every glyph the face has, its data
/// as <see cref="TupleVariations.Validate"/> checks it (the header and tuple headers inside the glyph's data, a shared tuple
/// index below the shared tuple count, every point number and delta inside its tuple's data, no run past its count).
/// </para>
/// <para>
/// Accepted, read as FreeType reads it: a glyph count other than <c>maxp</c>'s (glyphs past <c>gvar</c>'s count have no
/// variations; data of glyphs past <c>maxp</c>'s is never read); flags bits other than bit 0; point numbers at or past the
/// glyph's point count (ignored), repeated or unsorted; bytes left in a tuple's data; peak and intermediate tuples out of
/// order or past -1..1 (the scalar is computed as FreeType computes it).
/// </para>
/// </remarks>
internal sealed class GvarTable
{
    public const uint Tag = 0x67766172; // gvar

    private const int HeaderLength = 20;

    private readonly ReadOnlyMemory<byte> m_data;
    private readonly int[] m_offsets;
    private readonly long[] m_sharedTuples;

    private GvarTable(ReadOnlyMemory<byte> data, int axisCount, int[] offsets, long[] sharedTuples)
    {
        m_data = data;
        AxisCount = axisCount;
        m_offsets = offsets;
        m_sharedTuples = sharedTuples;
    }

    public int AxisCount { get; }

    /// <summary>The shared tuples, 16.16, <see cref="AxisCount"/> per tuple (FreeType's <c>blend->tuplecoords</c>).</summary>
    public ReadOnlySpan<long> SharedTuples => m_sharedTuples;

    public int SharedTupleCount => m_sharedTuples.Length / AxisCount;

    /// <param name="data">The table.</param>
    /// <param name="axisCount"><c>fvar</c>'s axis count.</param>
    /// <param name="glyf">The face's <c>glyf</c> table (its glyphs' point counts); null for a face without one.</param>
    /// <param name="glyphCount"><c>maxp.numGlyphs</c>.</param>
    /// <param name="parsed">The file's load state.</param>
    public static GvarTable Parse(ReadOnlyMemory<byte> data, int axisCount, GlyfTable? glyf, int glyphCount, ParsedTables parsed)
    {
        var r = new BigEndianReader(data.Span, "gvar");
        if (!r.Contains(0, HeaderLength))
            throw new InvalidDataException($"The 'gvar' table is {r.Length} bytes long, shorter than its {HeaderLength}-byte header.");

        var version = r.U32(0);
        if (version != 0x00010000)
            throw new InvalidDataException($"The 'gvar' table's version is 0x{version:X8}, not 1.0.");

        if (r.U16(4) != axisCount)
            throw new InvalidDataException($"The 'gvar' table has {r.U16(4)} axes, but 'fvar' has {axisCount}.");

        int sharedCount = r.U16(6);
        var sharedAt = r.U32(8);
        int gvarGlyphs = r.U16(12);
        var longOffsets = (r.U16(14) & 1) != 0;
        var dataAt = r.U32(16);

        if (sharedCount > 0 && !r.Contains(sharedAt, 2L * axisCount * sharedCount))
            throw new InvalidDataException($"The 'gvar' table's {sharedCount} shared tuples at {sharedAt} run past the table.");

        var entry = longOffsets ? 4 : 2;
        if (!r.Contains(HeaderLength, (long)entry * (gvarGlyphs + 1)))
            throw new InvalidDataException($"The 'gvar' table's {gvarGlyphs + 1} glyph offsets run past the table.");

        parsed.Charge(2L * axisCount * sharedCount + (long)entry * (gvarGlyphs + 1), "the 'gvar' header");
        parsed.Reserve(64 + 8L * axisCount * sharedCount + 4L * (gvarGlyphs + 1), "the 'gvar' offsets");
        var shared = new long[axisCount * sharedCount];
        for (var i = 0; i < shared.Length; i++)
            shared[i] = r.I16((int)sharedAt + 2 * i) * 4L;

        var offsets = new int[gvarGlyphs + 1];
        long previous = 0;
        for (var i = 0; i <= gvarGlyphs; i++)
        {
            var value = dataAt + (longOffsets ? r.U32(HeaderLength + 4 * i) : r.U16(HeaderLength + 2 * i) * 2L);
            if (value < previous)
                throw new InvalidDataException($"The 'gvar' table's glyph offset {i} ({value}) is smaller than the one before ({previous}).");

            if (value > r.Length)
                throw new InvalidDataException($"The 'gvar' table's glyph offset {i} ({value}) lies past the {r.Length}-byte table.");

            offsets[i] = (int)value;
            previous = value;
        }

        var table = new GvarTable(data, axisCount, offsets, shared);
        var checkedGlyphs = Math.Min(gvarGlyphs, glyphCount);
        for (var g = 0; g < checkedGlyphs; g++)
        {
            var glyph = table.GlyphData(g);
            if (glyph.Length == 0)
                continue;

            var points = (glyf?.VariationPointCount(g) ?? 0) + 4;
            TupleVariations.Validate(glyph, 0, axisCount, sharedCount, points, 2, new StoreName("gvar", g), parsed);
        }

        return table;
    }

    /// <summary>The glyph's variation data; empty when it has none.</summary>
    public ReadOnlySpan<byte> GlyphData(int glyphIndex) =>
        glyphIndex + 1 < m_offsets.Length ? m_data.Span[m_offsets[glyphIndex]..m_offsets[glyphIndex + 1]] : ReadOnlySpan<byte>.Empty;

    /// <summary>
    /// The tuple loop of <c>TT_Vary_Apply_Glyph_Deltas</c>: the summed deltas (16.16) of each of the <paramref name="count"/>
    /// points (the outline's, then the four phantom points) at the normalized coordinates; false (deltas untouched) when the
    /// glyph has no variation data.
    /// </summary>
    /// <param name="glyphIndex">The glyph.</param>
    /// <param name="coords">The normalized coordinates, 16.16.</param>
    /// <param name="x">The points' x in font units (the phantom points last).</param>
    /// <param name="y">The points' y.</param>
    /// <param name="count">The points, phantom points included.</param>
    /// <param name="contourEnds">The outline's contour ends (none for the phantom points).</param>
    /// <param name="scratch">Reused buffers.</param>
    /// <param name="work">The glyph load's work counter.</param>
    public bool ComputeDeltas(int glyphIndex, ReadOnlySpan<long> coords, ReadOnlySpan<long> x, ReadOnlySpan<long> y, int count,
        ReadOnlySpan<int> contourEnds, DeltaScratch scratch, IWorkCounter work)
    {
        var data = GlyphData(glyphIndex);
        if (data.Length == 0)
            return false;

        scratch.Ensure(count);
        var deltaX = scratch.DeltaX;
        var deltaY = scratch.DeltaY;
        Array.Clear(deltaX, 0, count);
        Array.Clear(deltaY, 0, count);

        var headers = scratch.Headers;
        var sharedAt = TupleVariations.ReadHeaders(data, 0, AxisCount, headers, out _);
        work.Charge(2L * count + (long)headers.Count * AxisCount);

        var sharedCount = 0;
        if (sharedAt >= 0)
        {
            var pos = sharedAt;
            sharedCount = TupleVariations.ReadPoints(data, ref pos, ref scratch.SharedPoints);
            work.Charge(sharedCount);
        }

        // points_org: the points in 16.16.
        var orgX = scratch.OrgX;
        var orgY = scratch.OrgY;
        for (var j = 0; j < count; j++)
        {
            orgX[j] = x[j] << 16;
            orgY[j] = y[j] << 16;
        }

        foreach (var header in headers)
        {
            var apply = TupleVariations.Scalar(data, header, m_sharedTuples, coords);
            if (apply == 0)
                continue;

            var pos = header.DataAt;
            ushort[] points;
            int pointCount;
            if (header.HasPrivatePoints)
            {
                pointCount = TupleVariations.ReadPoints(data, ref pos, ref scratch.PrivatePoints);
                points = scratch.PrivatePoints;
            }
            else
            {
                pointCount = sharedCount;
                points = scratch.SharedPoints;
            }

            var deltaCount = pointCount == 0 ? count : pointCount;
            scratch.EnsureDeltas(deltaCount);
            work.Charge(3L * deltaCount + count);
            TupleVariations.ReadDeltas(data, ref pos, deltaCount, scratch.TupleX);
            TupleVariations.ReadDeltas(data, ref pos, deltaCount, scratch.TupleY);

            if (pointCount == 0)
            {
                for (var j = 0; j < count; j++)
                {
                    deltaX[j] += FixedMath.MulFix(scratch.TupleX[j], apply);
                    deltaY[j] += FixedMath.MulFix(scratch.TupleY[j], apply);
                }

                continue;
            }

            // Interpolate the missing deltas, as the IUP instruction does.
            work.Charge(2L * count);
            var outX = scratch.OutX;
            var outY = scratch.OutY;
            var hasDelta = scratch.HasDelta;
            for (var j = 0; j < count; j++)
            {
                hasDelta[j] = false;
                outX[j] = orgX[j];
                outY[j] = orgY[j];
            }

            for (var j = 0; j < pointCount; j++)
            {
                int index = points[j];
                if (index >= count)
                    continue;

                hasDelta[index] = true;
                outX[index] += FixedMath.MulFix(scratch.TupleX[j], apply);
                outY[index] += FixedMath.MulFix(scratch.TupleY[j], apply);
            }

            InterpolateDeltas(contourEnds, outX, outY, orgX, orgY, hasDelta);

            for (var j = 0; j < count; j++)
            {
                deltaX[j] += outX[j] - orgX[j];
                deltaY[j] += outY[j] - orgY[j];
            }
        }

        return true;
    }

    /// <summary><c>tt_interpolate_deltas</c>.</summary>
    private static void InterpolateDeltas(ReadOnlySpan<int> contourEnds, long[] outX, long[] outY, long[] inX, long[] inY,
        bool[] hasDelta)
    {
        var point = 0;
        foreach (var endPoint in contourEnds)
        {
            var firstPoint = point;
            while (point <= endPoint && !hasDelta[point])
                point++;

            if (point <= endPoint)
            {
                var firstDelta = point;
                var curDelta = point;
                point++;

                while (point <= endPoint)
                {
                    if (hasDelta[point])
                    {
                        Interpolate(curDelta + 1, point - 1, curDelta, point, inX, inY, outX, outY);
                        curDelta = point;
                    }

                    point++;
                }

                if (curDelta == firstDelta)
                {
                    Shift(firstPoint, endPoint, curDelta, inX, inY, outX, outY);
                }
                else
                {
                    Interpolate(curDelta + 1, endPoint, curDelta, firstDelta, inX, inY, outX, outY);
                    if (firstDelta > 0)
                        Interpolate(firstPoint, firstDelta - 1, curDelta, firstDelta, inX, inY, outX, outY);
                }
            }

            // FreeType's contour loop does not reset `point': the next contour starts after this one's end.
            point = Math.Max(point, endPoint + 1);
        }
    }

    /// <summary><c>tt_delta_shift</c>.</summary>
    private static void Shift(int p1, int p2, int reference, long[] inX, long[] inY, long[] outX, long[] outY)
    {
        var dx = outX[reference] - inX[reference];
        var dy = outY[reference] - inY[reference];
        if (dx == 0 && dy == 0)
            return;

        for (var p = p1; p < reference; p++)
        {
            outX[p] += dx;
            outY[p] += dy;
        }

        for (var p = reference + 1; p <= p2; p++)
        {
            outX[p] += dx;
            outY[p] += dy;
        }
    }

    /// <summary><c>tt_delta_interpolate</c>, x then y (the reference points swapped as FreeType swaps them, the swap kept).</summary>
    private static void Interpolate(int p1, int p2, int ref1, int ref2, long[] inX, long[] inY, long[] outX, long[] outY)
    {
        if (p1 > p2)
            return;

        InterpolateAxis(p1, p2, ref ref1, ref ref2, inX, outX);
        InterpolateAxis(p1, p2, ref ref1, ref ref2, inY, outY);
    }

    private static void InterpolateAxis(int p1, int p2, ref int ref1, ref int ref2, long[] input, long[] output)
    {
        if (input[ref1] > input[ref2])
            (ref1, ref2) = (ref2, ref1);

        var in1 = input[ref1];
        var in2 = input[ref2];
        var out1 = output[ref1];
        var out2 = output[ref2];
        var d1 = out1 - in1;
        var d2 = out2 - in2;

        if (in1 == in2 && out1 != out2)
            return;

        var scale = in1 != in2 ? FixedMath.DivFix(out2 - out1, in2 - in1) : 0;
        for (var p = p1; p <= p2; p++)
        {
            var value = input[p];
            if (value <= in1)
                value += d1;
            else if (value >= in2)
                value += d2;
            else
                value = out1 + FixedMath.MulFix(value - in1, scale);

            output[p] = value;
        }
    }
}

/// <summary>The buffers one glyph loader reuses for <see cref="GvarTable.ComputeDeltas"/>.</summary>
internal sealed class DeltaScratch
{
    public long[] DeltaX = new long[64];
    public long[] DeltaY = new long[64];
    public long[] OrgX = new long[64];
    public long[] OrgY = new long[64];
    public long[] OutX = new long[64];
    public long[] OutY = new long[64];
    public bool[] HasDelta = new bool[64];
    public long[] TupleX = new long[64];
    public long[] TupleY = new long[64];
    public ushort[] SharedPoints = new ushort[64];
    public ushort[] PrivatePoints = new ushort[64];
    public readonly List<TupleHeader> Headers = [];

    public void Ensure(int count)
    {
        if (count <= DeltaX.Length)
            return;

        var size = Math.Max(count, DeltaX.Length * 2);
        DeltaX = new long[size];
        DeltaY = new long[size];
        OrgX = new long[size];
        OrgY = new long[size];
        OutX = new long[size];
        OutY = new long[size];
        HasDelta = new bool[size];
    }

    public void EnsureDeltas(int count)
    {
        if (count <= TupleX.Length)
            return;

        var size = Math.Max(count, TupleX.Length * 2);
        TupleX = new long[size];
        TupleY = new long[size];
    }
}
