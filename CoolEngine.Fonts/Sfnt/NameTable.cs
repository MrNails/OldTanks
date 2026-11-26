// Get follows FreeType 2.13.2 src/sfnt/sfobjs.c (tt_face_get_name, tt_name_ascii_from_utf16, tt_name_ascii_from_other)
// (FreeType Project License; credit in docs).

namespace CoolEngine.Fonts.Sfnt;

/// <summary>
/// The <c>name</c> table, read the way FreeType 2.13.2's <c>tt_face_get_name</c> reads it: one record per name ID chosen by
/// platform and language, converted to ASCII with <c>?</c> for every character outside 32..127.
/// </summary>
internal sealed class NameTable
{
    public const ushort FontFamily = 1;
    public const ushort FontSubfamily = 2;
    public const ushort TypographicFamily = 16;
    public const ushort TypographicSubfamily = 17;
    public const ushort WwsFamily = 21;
    public const ushort WwsSubfamily = 22;

    private const ushort PlatformUnicode = 0;
    private const ushort PlatformMacintosh = 1;
    private const ushort PlatformIso = 2;
    private const ushort PlatformWindows = 3;
    private const ushort MacRoman = 0;
    private const ushort MacEnglish = 0;
    private const ushort WindowsSymbol = 0;
    private const ushort WindowsUnicodeBmp = 1;
    private const ushort WindowsUnicodeFull = 10;

    private const int HeaderLength = 6;
    private const int RecordLength = 12;
    private const int LanguageTagRecordLength = 4;

    // The name IDs a face reads: decoded once at load (accounted then) and shared by every face of the table.
    private static readonly ushort[] s_faceNameIds =
        [FontFamily, FontSubfamily, TypographicFamily, TypographicSubfamily, WwsFamily, WwsSubfamily];

    private readonly ReadOnlyMemory<byte> m_data;
    private readonly NameRecord[] m_records;
    private readonly Dictionary<ushort, string?> m_names = [];

    private NameTable(ReadOnlyMemory<byte> data, NameRecord[] records)
    {
        m_data = data;
        m_records = records;
    }

    /// <param name="data">The table.</param>
    /// <param name="parsed">The file's load state, against whose parsed-memory budget the record array is accounted.</param>
    public static NameTable Parse(ReadOnlyMemory<byte> data, ParsedTables parsed)
    {
        // A version above 1 is accepted (Denys's safety-strict decision): FreeType reads language tags only in version 1.
        var r = new BigEndianReader(data.Span, "name");
        var version = r.U16(0);

        var count = r.U16(2);
        var storageOffset = r.U16(4);
        long storageStart = HeaderLength + (long)RecordLength * count;
        r.Check(HeaderLength, (long)RecordLength * count);

        // Version 1's language tags: a tag that is empty or whose string lies outside the storage is ignored (its strings are
        // never read), and a record naming it is skipped below, as FreeType does.
        bool[]? validTags = null;
        if (version == 1)
        {
            var languageTagCount = r.U16((int)storageStart);
            var languageTagsStart = storageStart + 2;
            storageStart = languageTagsStart + (long)LanguageTagRecordLength * languageTagCount;
            r.Check(languageTagsStart, (long)LanguageTagRecordLength * languageTagCount);

            parsed.Reserve(24 + languageTagCount, "the 'name' language tags");
            parsed.Charge((long)LanguageTagRecordLength * languageTagCount, "the 'name' language tags");
            validTags = new bool[languageTagCount];
            for (var i = 0; i < languageTagCount; i++)
            {
                var at = (int)languageTagsStart + LanguageTagRecordLength * i;
                int length = r.U16(at);
                validTags[i] = length != 0 && InStorage(r, storageStart, storageOffset + r.U16(at + 2), length);
            }
        }

        // Records in any order, and the same (platform, encoding, language, name ID) more than once, are accepted (Denys's
        // safety-strict decision; FreeType and OTS accept both): the choice scans the records in file order, as FreeType's
        // tt_face_get_name does. A record that is empty, whose string lies outside the storage, or (version 1) whose language
        // ID 0x8000 + n names a tag n past the list or ignored, is skipped, as FreeType skips it.
        parsed.Reserve(24 + 24L * count, "the 'name' records");
        parsed.Charge((long)RecordLength * count, "the 'name' records");
        var records = new List<NameRecord>(count);
        for (var i = 0; i < count; i++)
        {
            var at = HeaderLength + RecordLength * i;
            var record = new NameRecord(r.U16(at), r.U16(at + 2), r.U16(at + 4), r.U16(at + 6), r.U16(at + 8),
                storageOffset + r.U16(at + 10));
            if (record.Length == 0 || !InStorage(r, storageStart, record.Offset, record.Length))
                continue;

            if (validTags is not null && record.LanguageId >= 0x8000 &&
                (record.LanguageId - 0x8000 >= validTags.Length || !validTags[record.LanguageId - 0x8000]))
                continue;

            records.Add(record);
        }

        var table = new NameTable(data, records.ToArray());
        foreach (var nameId in s_faceNameIds)
            table.m_names[nameId] = table.Decode(nameId, parsed);

        return table;
    }

    /// <summary>
    /// The name with this ID, chosen and converted as FreeType does; null when there is none. Only the family and subfamily IDs
    /// a face reads (1, 2, 16, 17, 21, 22) are kept; any other ID gives null.
    /// </summary>
    public string? Get(ushort nameId) => m_names.GetValueOrDefault(nameId);

    private string? Decode(ushort nameId, ParsedTables parsed)
    {
        parsed.Charge((long)RecordLength * m_records.Length, "choosing name", nameId);
        int foundAppleRoman = -1, foundAppleEnglish = -1, foundWindows = -1, foundUnicode = -1;
        var isEnglish = false;

        for (var n = 0; n < m_records.Length; n++)
        {
            var record = m_records[n];
            if (record.NameId != nameId || record.Length == 0)
                continue;

            switch (record.PlatformId)
            {
                case PlatformUnicode:
                case PlatformIso:
                    foundUnicode = n;
                    break;

                case PlatformMacintosh:
                    if (record.LanguageId == MacEnglish)
                        foundAppleEnglish = n;
                    else if (record.EncodingId == MacRoman)
                        foundAppleRoman = n;
                    break;

                case PlatformWindows:
                    // A non-English Windows name only while nothing else has been found.
                    if ((foundWindows == -1 || (record.LanguageId & 0x3FF) == 0x009) &&
                        record.EncodingId is WindowsSymbol or WindowsUnicodeBmp or WindowsUnicodeFull)
                    {
                        isEnglish = (record.LanguageId & 0x3FF) == 0x009;
                        foundWindows = n;
                    }

                    break;
            }
        }

        var foundApple = foundAppleEnglish >= 0 ? foundAppleEnglish : foundAppleRoman;
        var chosen = foundWindows >= 0 && !(foundApple >= 0 && !isEnglish) ? foundWindows
            : foundApple >= 0 ? foundApple
            : foundUnicode;
        if (chosen < 0)
            return null;

        // The character array and the string, at most two bytes per character each, one character per byte of the record.
        parsed.Reserve(48 + 4L * m_records[chosen].Length, "name", nameId);
        parsed.Charge(m_records[chosen].Length, "decoding name", nameId);
        return chosen == foundApple ? SingleByteToAscii(m_records[chosen]) : Utf16ToAscii(m_records[chosen]);
    }

    /// <summary>Whether a string lies inside the storage (after the records and language tags, up to the table's end).</summary>
    private static bool InStorage(BigEndianReader r, long storageStart, long start, int length) =>
        start >= storageStart && start + length <= r.Length;

    private string Utf16ToAscii(NameRecord record)
    {
        var bytes = m_data.Span.Slice(record.Offset, record.Length);
        var chars = new char[record.Length / 2];
        var n = 0;
        for (; n < chars.Length; n++)
        {
            var code = bytes[2 * n] << 8 | bytes[2 * n + 1];
            if (code == 0)
                break;

            chars[n] = code is < 32 or > 127 ? '?' : (char)code;
        }

        return new string(chars, 0, n);
    }

    private string SingleByteToAscii(NameRecord record)
    {
        var bytes = m_data.Span.Slice(record.Offset, record.Length);
        var chars = new char[record.Length];
        var n = 0;
        for (; n < chars.Length; n++)
        {
            var code = bytes[n];
            if (code == 0)
                break;

            chars[n] = code is < 32 or > 127 ? '?' : (char)code;
        }

        return new string(chars, 0, n);
    }

    private readonly record struct NameRecord(ushort PlatformId, ushort EncodingId, ushort LanguageId, ushort NameId, int Length,
        int Offset);
}
