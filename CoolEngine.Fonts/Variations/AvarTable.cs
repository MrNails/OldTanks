// Follows FreeType 2.13.2 src/truetype/ttgxvar.c (ft_var_load_avar, and the 'avar' step of ft_var_to_normalized) (FreeType
// Project License; credit in docs).

using CoolEngine.Fonts.Sfnt;

namespace CoolEngine.Fonts.Variations;

/// <summary>
/// The <c>avar</c> table, version 1.0: one segment map per axis, which <see cref="Map"/> applies to normalized coordinates
/// exactly as FreeType's <c>ft_var_to_normalized</c> applies it. Immutable.
/// </summary>
/// <remarks>
/// Checked at load (<see cref="InvalidDataException"/>): a header or a segment map past the table. <see cref="NotSupportedException"/>:
/// version 2.0 with <c>fvar</c>'s axis count (Denys's decision at the OT-4 go; FreeType 2.13.2 applies its item variation
/// store). Accepted and applied as FreeType applies them (Denys, 2026-11-27: "Apply as FreeType"): map values outside -1..1,
/// fromCoordinate values that do not ascend (FreeType's <c>FT_MulDiv</c> by 0 gives 0x7FFFFFFF), maps without the -1, 0 and 1
/// anchors; the coordinates are refused only where FreeType refuses them, when a mapped coordinate leaves -1..1
/// (<see cref="VariationInstance"/>). Ignored as FreeType ignores the whole table (<see cref="Parse"/> returns null): a version
/// other than 1.0 and 2.0, an axis count other than <c>fvar</c>'s (any version), a map whose own pairs take more bytes than the
/// table has.
/// </remarks>
internal sealed class AvarTable
{
    private readonly (long From, long To)[][] m_maps;

    private AvarTable((long, long)[][] maps) => m_maps = maps;

    /// <summary>The table, or null when FreeType ignores it.</summary>
    /// <param name="data">The <c>avar</c> table.</param>
    /// <param name="axisCount"><c>fvar</c>'s axis count.</param>
    /// <param name="parsed">The file's load state.</param>
    public static AvarTable? Parse(ReadOnlySpan<byte> data, int axisCount, ParsedTables parsed)
    {
        var r = new BigEndianReader(data, "avar");
        if (!r.Contains(0, 8))
            throw new InvalidDataException($"The 'avar' table is {data.Length} bytes long, shorter than its 8-byte header.");

        // ft_var_load_avar ignores the table for another version, then for another axis count.
        var version = r.U32(0);
        if (version is not (0x00010000 or 0x00020000) || r.U32(4) != axisCount)
            return null;

        if (version == 0x00020000)
            throw new NotSupportedException("The font's 'avar' table is version 2.0, which is not supported.");

        parsed.Reserve(64 + 32L * axisCount, "the 'avar' segment maps");
        var maps = new (long, long)[axisCount][];
        var at = 8;
        for (var i = 0; i < axisCount; i++)
        {
            if (!r.Contains(at, 2))
                throw new InvalidDataException($"The 'avar' segment map of axis {i} at {at} runs past the table.");

            int count = r.U16(at);
            at += 2;

            // FreeType drops the whole table when one map's pairs alone outgrow it.
            if (4L * count > data.Length)
                return null;

            if (!r.Contains(at, 4L * count))
                throw new InvalidDataException($"The 'avar' segment map of axis {i} ({count} mappings at {at}) runs past the table.");

            parsed.Charge(4L * count, "the 'avar' segment map of axis", i);
            parsed.Reserve(24 + 16L * count, "the 'avar' segment map of axis", i);
            var map = new (long From, long To)[count];

            // FT_fdot14ToFixed.
            for (var j = 0; j < count; j++)
                map[j] = (r.I16(at + 4 * j) * 4L, r.I16(at + 4 * j + 2) * 4L);

            maps[i] = map;
            at += 4 * count;
        }

        return new AvarTable(maps);
    }

    /// <summary>The <c>avar</c> step of <c>ft_var_to_normalized</c>: each normalized coordinate through its axis's map.</summary>
    public void Map(long[] normalized)
    {
        for (var i = 0; i < normalized.Length && i < m_maps.Length; i++)
        {
            var map = m_maps[i];
            for (var j = 1; j < map.Length; j++)
            {
                if (normalized[i] < map[j].From)
                {
                    normalized[i] = FixedMath.MulDiv(normalized[i] - map[j - 1].From, map[j].To - map[j - 1].To,
                        map[j].From - map[j - 1].From) + map[j - 1].To;
                    break;
                }
            }
        }
    }
}
