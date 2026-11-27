// Follows FreeType 2.13.2 src/truetype/ttgxvar.c (tt_face_vary_cvt) (FreeType Project License; credit in docs).

using CoolEngine.Fonts.Sfnt;

namespace CoolEngine.Fonts.Variations;

/// <summary>
/// The <c>cvar</c> table: the cvt's deltas at normalized coordinates, applied before <c>prep</c> runs, as FreeType's
/// <c>tt_face_vary_cvt</c> computes them (<see cref="ComputeDeltas"/>). Read only for a variable face with a <c>cvt </c> table
/// (FreeType reads it only then). Immutable.
/// </summary>
/// <remarks>
/// Checked at load (<see cref="InvalidDataException"/>; FreeType fails the coordinates or ignores the tuple on each): a header
/// past the table; a tuple naming a shared tuple <c>gvar</c> does not have (none without <c>gvar</c>); the tuple data as
/// <see cref="TupleVariations.Validate"/> checks it. Accepted as FreeType reads it: a version other than 1.0 (FreeType ignores
/// the table: <see cref="Parse"/> returns null); point numbers at or past the cvt's size (ignored); and FreeType's reading of a
/// tuple that uses the shared point numbers when those are "all points": it applies nothing for that tuple (its test is on the
/// tuple's own point numbers), and neither does this library.
/// </remarks>
internal sealed class CvarTable
{
    public const uint Tag = 0x63766172; // cvar

    private readonly ReadOnlyMemory<byte> m_data;
    private readonly long[] m_sharedTuples;
    private readonly int m_axisCount;

    private CvarTable(ReadOnlyMemory<byte> data, long[] sharedTuples, int axisCount)
    {
        m_data = data;
        m_sharedTuples = sharedTuples;
        m_axisCount = axisCount;
    }

    /// <summary>The table, or null when FreeType ignores it (a version other than 1.0).</summary>
    /// <param name="data">The table.</param>
    /// <param name="axisCount"><c>fvar</c>'s axis count.</param>
    /// <param name="gvar">The face's <c>gvar</c> (its shared tuples); null when it has none.</param>
    /// <param name="cvtEntries">The cvt's entries.</param>
    /// <param name="parsed">The file's load state.</param>
    public static CvarTable? Parse(ReadOnlyMemory<byte> data, int axisCount, GvarTable? gvar, int cvtEntries, ParsedTables parsed)
    {
        var r = new BigEndianReader(data.Span, "cvar");
        if (!r.Contains(0, 4))
            throw new InvalidDataException($"The 'cvar' table is {r.Length} bytes long, shorter than its version.");

        if (r.U32(0) != 0x00010000)
            return null;

        var shared = gvar is null ? [] : gvar.SharedTuples.ToArray();
        TupleVariations.Validate(data.Span, 4, axisCount, shared.Length / axisCount, cvtEntries, 1, new StoreName("cvar", -1),
            parsed);
        return new CvarTable(data, shared, axisCount);
    }

    /// <summary>
    /// <c>tt_face_vary_cvt</c>: each cvt entry's delta in 26.6 (<c>FT_fixedToFdot6</c> of the 16.16 sum) at the normalized
    /// coordinates.
    /// </summary>
    /// <param name="coords">The normalized coordinates, 16.16.</param>
    /// <param name="cvtEntries">The cvt's entries.</param>
    /// <param name="work">The size's work counter (the scalars, the points and deltas read, the entries summed).</param>
    public int[] ComputeDeltas(ReadOnlySpan<long> coords, int cvtEntries, IWorkCounter work)
    {
        var data = m_data.Span;
        var headers = new List<TupleHeader>();
        var sharedAt = TupleVariations.ReadHeaders(data, 4, m_axisCount, headers, out _);
        work.Charge(cvtEntries + (long)headers.Count * m_axisCount);
        var sums = new long[cvtEntries];
        var sharedPoints = new ushort[16];
        var sharedCount = 0;
        if (sharedAt >= 0)
        {
            var at = sharedAt;
            sharedCount = TupleVariations.ReadPoints(data, ref at, ref sharedPoints);
        }

        var privatePoints = new ushort[16];
        var deltas = new long[16];
        foreach (var header in headers)
        {
            var apply = TupleVariations.Scalar(data, header, m_sharedTuples, coords);
            if (apply == 0)
                continue;

            var pos = header.DataAt;
            bool allPoints;
            ushort[] points;
            int pointCount;
            if (header.HasPrivatePoints)
            {
                pointCount = TupleVariations.ReadPoints(data, ref pos, ref privatePoints);
                points = privatePoints;
                allPoints = pointCount == 0;
            }
            else
            {
                // FreeType tests the tuple's own point numbers for "all points": shared "all points" apply to nothing.
                pointCount = sharedCount;
                points = sharedPoints;
                allPoints = false;
            }

            var count = pointCount == 0 ? cvtEntries : pointCount;
            work.Charge(3L * count);
            if (deltas.Length < count)
                deltas = new long[count];

            TupleVariations.ReadDeltas(data, ref pos, count, deltas);
            if (allPoints)
            {
                for (var j = 0; j < cvtEntries; j++)
                    sums[j] += FixedMath.MulFix(deltas[j], apply);

                continue;
            }

            for (var j = 0; j < pointCount; j++)
            {
                int index = points[j];
                if (index < cvtEntries)
                    sums[index] += FixedMath.MulFix(deltas[j], apply);
            }
        }

        var result = new int[cvtEntries];
        for (var i = 0; i < cvtEntries; i++)
            result[i] = (int)FixedMath.FixedToFdot6(sums[i]);

        return result;
    }
}
