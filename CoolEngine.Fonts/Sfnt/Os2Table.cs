// Parse follows FreeType 2.13.2 src/sfnt/ttload.c (tt_face_load_os2) and src/sfnt/sfobjs.c (an OS/2 table that fails to
// load is treated as absent) (FreeType Project License; credit in docs).

namespace CoolEngine.Fonts.Sfnt;

/// <summary>The fields of the optional <c>OS/2</c> table that FreeType's face metrics and names depend on.</summary>
/// <remarks>
/// Read as FreeType 2.13.2 reads it: a 78-byte frame at the table's offset in the file, whatever the table's own length (so a
/// 68-byte Apple version-0 table takes its last fields from the bytes that follow it), then 8, 10 and 4 more bytes for
/// versions 1, 2 and 5 and above. A frame that runs past the end of the file, or the version 0xFFFF, makes the table absent,
/// as FreeType treats it. Accepted, by Denys's safety-strict decision of 2026-11-26: a table shorter than its version claims,
/// and a version above 5 (its known prefix is read).
/// </remarks>
internal sealed class Os2Table
{
    /// <summary><c>fsSelection</c> bit 7: the typographic metrics are the line metrics.</summary>
    public const ushort UseTypoMetrics = 1 << 7;

    /// <summary><c>fsSelection</c> bit 8: the family follows the weight, width, slope model.</summary>
    public const ushort WeightWidthSlope = 1 << 8;

    private const ushort Absent = 0xFFFF;

    private static readonly Os2Table s_absent = new(Absent, 0, 0, 0, 0, 0, 0);

    private Os2Table(ushort version, ushort fsSelection, short typoAscender, short typoDescender, short typoLineGap,
        ushort winAscent, ushort winDescent)
    {
        Version = version;
        FsSelection = fsSelection;
        TypoAscender = typoAscender;
        TypoDescender = typoDescender;
        TypoLineGap = typoLineGap;
        WinAscent = winAscent;
        WinDescent = winDescent;
    }

    /// <summary>False when FreeType would treat the table as absent.</summary>
    public bool IsPresent => Version != Absent;

    public ushort Version { get; }

    public ushort FsSelection { get; }

    public short TypoAscender { get; }

    public short TypoDescender { get; }

    public short TypoLineGap { get; }

    public ushort WinAscent { get; }

    public ushort WinDescent { get; }

    /// <param name="file">The whole file.</param>
    /// <param name="offset">The table's offset in the file.</param>
    public static Os2Table Parse(ReadOnlySpan<byte> file, int offset)
    {
        var r = new BigEndianReader(file, "font");
        if (!r.Contains(offset, 78))
            return s_absent;

        var version = r.U16(offset);
        var frames = 78 + (version >= 1 ? 8 : 0) + (version >= 2 ? 10 : 0) + (version >= 5 ? 4 : 0);
        if (!r.Contains(offset, frames) || version == Absent)
            return s_absent;

        return new Os2Table(version, r.U16(offset + 62), r.I16(offset + 68), r.I16(offset + 70), r.I16(offset + 72),
            r.U16(offset + 74), r.U16(offset + 76));
    }
}
