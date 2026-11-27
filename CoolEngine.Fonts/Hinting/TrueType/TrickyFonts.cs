// Follows FreeType 2.13.2 src/truetype/ttobjs.c (tt_check_trickyness, tt_check_trickyness_family,
// tt_skip_pdffont_random_tag, tt_check_trickyness_sfnt_ids, tt_get_sfnt_checksum, tt_synth_sfnt_checksum), the names and
// table signatures copied from it (FreeType Project License; credit in docs).

using CoolEngine.Fonts.Sfnt;

namespace CoolEngine.Fonts.Hinting.TrueType;

/// <summary>
/// FreeType's "tricky" fonts: old CJK fonts whose glyphs are built by their bytecode, so FreeType never turns hinting off for
/// them and runs v40 without backward compatibility. A face is tricky when its family name (FreeType's <c>family_name</c>,
/// after an optional PDF subset tag) contains one of 20 names, or when its <c>cvt </c>, <c>fpgm</c> and <c>prep</c> tables
/// match one of 31 known (checksum, length) signatures; the checksums are always computed from the bytes, never taken from
/// the table directory, as FreeType does.
/// </summary>
internal static class TrickyFonts
{
    private static readonly string[] s_names =
    [
        "cpop", "DFGirl-W6-WIN-BF", "DFGothic-EB", "DFGyoSho-Lt", "DFHei", "DFHSGothic-W5", "DFHSMincho-W3", "DFHSMincho-W7",
        "DFKaiSho-SB", "DFKaiShu", "DFKai-SB", "DFMing", "DLC", "HuaTianKaiTi?", "HuaTianSongTi?", "Ming(for ISO10646)",
        "MingLiU", "MingMedium", "PMingLiU", "MingLi43",
    ];

    // (checksum, length) of cvt, fpgm and prep for each known face.
    private static readonly uint[,] s_ids =
    {
        { 0x05BCF058, 0x000002E4, 0x28233BF1, 0x000087C4, 0xA344A1EA, 0x000001E1 }, // MingLiU 1995
        { 0x05BCF058, 0x000002E4, 0x28233BF1, 0x000087C4, 0xA344A1EB, 0x000001E1 }, // MingLiU 1996-
        { 0x12C3EBB2, 0x00000350, 0xB680EE64, 0x000087A7, 0xCE939563, 0x00000758 }, // DFGothic-EB
        { 0x11E5EAD4, 0x00000350, 0xCE5956E9, 0x0000BC85, 0x8272F416, 0x00000045 }, // DFGyoSho-Lt
        { 0x1257EB46, 0x00000350, 0xF699D160, 0x0000715F, 0xD222F568, 0x000003BC }, // DFHei-Md-HK-BF
        { 0x1262EB4E, 0x00000350, 0xE86A5D64, 0x00007940, 0x7850F729, 0x000005FF }, // DFHSGothic-W5
        { 0x122DEB0A, 0x00000350, 0x3D16328A, 0x0000859B, 0xA93FC33B, 0x000002CB }, // DFHSMincho-W3
        { 0x125FEB26, 0x00000350, 0xA5ACC982, 0x00007EE1, 0x90999196, 0x0000041F }, // DFHSMincho-W7
        { 0x11E5EAD4, 0x00000350, 0x5A30CA3B, 0x00009063, 0x13A42602, 0x0000007E }, // DFKaiShu
        { 0x11E5EAD4, 0x00000350, 0xA6E78C01, 0x00008998, 0x13A42602, 0x0000007E }, // DFKaiShu, variant
        { 0x11E5EAD4, 0x00000360, 0x9DB282B2, 0x0000C06E, 0x53E6D7CA, 0x00000082 }, // DFKaiShu-Md-HK-BF
        { 0x1243EB18, 0x00000350, 0xBA0A8C30, 0x000074AD, 0xF3D83409, 0x0000037B }, // DFMing-Bd-HK-BF
        { 0x07DCF546, 0x00000308, 0x40FE7C90, 0x00008E2A, 0x608174B5, 0x0000007A }, // DLCLiShu
        { 0xEB891238, 0x00000308, 0xD2E4DCD4, 0x0000676F, 0x8EA5F293, 0x000003B8 }, // DLCHayBold
        { 0xFFFBFFFC, 0x00000008, 0x9C9E48B8, 0x0000BEA2, 0x70020112, 0x00000008 }, // HuaTianKaiTi
        { 0xFFFBFFFC, 0x00000008, 0x0A5A0483, 0x00017C39, 0x70020112, 0x00000008 }, // HuaTianSongTi
        { 0x00000000, 0x00000000, 0x40C92555, 0x000000E5, 0xA39B58E3, 0x0000117C }, // NEC fadpop7.ttf
        { 0x00000000, 0x00000000, 0x33C41652, 0x000000E5, 0x26D6C52A, 0x00000F6A }, // NEC fadrei5.ttf
        { 0x00000000, 0x00000000, 0x6DB1651D, 0x0000019D, 0x6C6E4B03, 0x00002492 }, // NEC fangot7.ttf
        { 0x00000000, 0x00000000, 0x40C92555, 0x000000E5, 0xDE51FAD0, 0x0000117C }, // NEC fangyo5.ttf
        { 0x00000000, 0x00000000, 0x85E47664, 0x000000E5, 0xA6C62831, 0x00001CAA }, // NEC fankyo5.ttf
        { 0x00000000, 0x00000000, 0x2D891CFD, 0x0000019D, 0xA0604633, 0x00001DE8 }, // NEC fanrgo5.ttf
        { 0x00000000, 0x00000000, 0x40AA774C, 0x000001CB, 0x9B5CAA96, 0x00001F9A }, // NEC fangot5.ttc
        { 0x00000000, 0x00000000, 0x0D3DE9CB, 0x00000141, 0xD4127766, 0x00002280 }, // NEC fanmin3.ttc
        { 0x00000000, 0x00000000, 0x4A692698, 0x000001F0, 0x340D4346, 0x00001FCA }, // NEC FA-Gothic, 1996
        { 0x00000000, 0x00000000, 0xCD34C604, 0x00000166, 0x6CF31046, 0x000022B0 }, // NEC FA-Minchou, 1996
        { 0x00000000, 0x00000000, 0x5DA75315, 0x0000019D, 0x40745A5F, 0x000022E0 }, // NEC FA-RoundGothicB, 1996
        { 0x00000000, 0x00000000, 0xF055FC48, 0x000001C2, 0x3900DED3, 0x00001E18 }, // NEC FA-RoundGothicM, 1996
        { 0x00170003, 0x00000060, 0xDBB4306E, 0x000058AA, 0xD643482A, 0x00000035 }, // MINGLI.TTF, 1992
        { 0x1269EB58, 0x00000350, 0x5CD5957A, 0x00006A4E, 0xF758323A, 0x00000380 }, // DFHei-Bd-WIN-HK-BF
        { 0x122FEB0B, 0x00000350, 0x7F10919A, 0x000070A9, 0x7CD7E7B7, 0x0000025C }, // DFMing-Md-WIN-HK-BF
    };

    private static readonly uint[] s_tags = [0x63767420, 0x6670676D, 0x70726570]; // cvt, fpgm, prep

    /// <summary>FreeType's <c>tt_check_trickyness</c>.</summary>
    public static bool IsTricky(FaceData face) =>
        IsTrickyFamily(FontFace.ReadNames(face).Family) || MatchesSignature(face.Tables);

    /// <summary>FreeType's <c>tt_check_trickyness_family</c>, after <c>tt_skip_pdffont_random_tag</c>.</summary>
    public static bool IsTrickyFamily(string name)
    {
        if (name.Length >= 8 && name[6] == '+' && name.AsSpan(0, 6).IndexOfAnyExceptInRange('A', 'Z') < 0)
            name = name[7..];

        foreach (var trick in s_names)
        {
            if (name.Contains(trick, StringComparison.Ordinal))
                return true;
        }

        return false;
    }

    /// <summary>FreeType's <c>tt_check_trickyness_sfnt_ids</c>.</summary>
    private static bool MatchesSignature(TableDirectory tables)
    {
        var faces = s_ids.GetLength(0);
        var matched = new int[faces];
        var seen = new bool[3];

        foreach (var record in tables.Records)
        {
            var k = Array.IndexOf(s_tags, record.Tag);
            if (k < 0)
                continue;

            seen[k] = true;
            uint checksum = 0;
            for (var j = 0; j < faces; j++)
            {
                if ((uint)record.Length != s_ids[j, 2 * k + 1])
                    continue;

                if (checksum == 0)
                    checksum = Checksum(tables, record);

                if (s_ids[j, 2 * k] == checksum)
                    matched[j]++;

                if (matched[j] == 3)
                    return true;
            }
        }

        // A table the face lacks matches a signature whose length for it is 0.
        for (var j = 0; j < faces; j++)
        {
            for (var k = 0; k < 3; k++)
            {
                if (!seen[k] && s_ids[j, 2 * k + 1] == 0)
                    matched[j]++;
            }

            if (matched[j] == 3)
                return true;
        }

        return false;
    }

    /// <summary>
    /// FreeType's <c>tt_get_sfnt_checksum</c>: the sum of the big-endian 32-bit words of the table found for the record's tag,
    /// over the record's length (a short tail padded with zeros); 0 when the face has no such (non-empty) table.
    /// </summary>
    private static uint Checksum(TableDirectory tables, TableRecord record)
    {
        if (!tables.TryGetRecord(record.Tag, out var found))
            return 0;

        var data = tables.Data.AsSpan();
        var offset = (long)found.Offset;
        var length = Math.Min(record.Length, data.Length - offset);
        uint sum = 0;
        var i = 0L;
        for (; length - i > 3; i += 4)
            sum += (uint)(data[(int)(offset + i)] << 24 | data[(int)(offset + i + 1)] << 16 | data[(int)(offset + i + 2)] << 8 |
                          data[(int)(offset + i + 3)]);

        for (var shift = 24; i < length; i++, shift -= 8)
            sum += (uint)data[(int)(offset + i)] << shift;

        return sum;
    }
}
