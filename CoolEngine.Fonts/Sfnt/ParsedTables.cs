namespace CoolEngine.Fonts.Sfnt;

/// <summary>
/// The state of one file's load, shared by all its faces: the tables checked and parsed so far, keyed by their records (tag,
/// offset and length) with the inputs each was read with; the inflated <c>SVG </c> documents and what is left of the file's
/// inflate budget; the parsed-table memory accounted so far against the file's budget; and the load work charged so far
/// against the file's work budget.
/// </summary>
/// <remarks>
/// Collection faces whose records point at the same tables share one parsed copy, and must read it with the same inputs
/// (Denys's "Same-inputs rule", 2026-11-26; see <see cref="TableInputs"/>), so each table record is parsed once per file.
/// Records that differ may not overlap (<see cref="SfntReader"/>'s overlap rule), so each byte of the file belongs to at most
/// one parsed table. Two per-file budgets bound its memory: all faces' gzip <c>SVG </c> documents
/// inflate within <see cref="SvgTable.InflateBudget"/> bytes together, and what the parsers allocate (accounted before they
/// allocate it, retained or not) may not pass <see cref="MaxParsedBytesPerFileByte"/> times the file's length. One per-file
/// budget bounds its work (Denys's "Rules + work budget", 2026-11-26): what the load walks (<see cref="Charge"/>) may not pass
/// <see cref="MaxWorkPerFileByte"/> times the file's length plus <see cref="WorkAllowance"/>. Each throws
/// <see cref="NotSupportedException"/>. Used during one load only, on one thread.
/// </remarks>
internal sealed class ParsedTables
{
    /// <summary>
    /// The parsed-table memory allowed per byte of the file. Measured on 2026-11-26 with <see cref="SfntReader.MeasureParsedBytes"/>
    /// over 198 files: the 192 font files of <c>C:\Windows\Fonts</c> that load (the other 10 have only symbol <c>cmap</c>s), the
    /// two repository fonts and the four Source Sans 3 TrueType and OpenType files. The largest ratio of accounted bytes to file
    /// length was 0.654 (AniMe Vision - MB_EN Regular.ttf: 6,357 bytes for a 9,728-byte file); re-measured the same day once the
    /// component lists, format-14 sets, overlap check and decoded names were accounted too: 0.784 (the same file, 7,629 bytes;
    /// then phagspa.ttf 0.676); re-measured once more after the cmap and SVG overlap checks and the name and cmap record sets
    /// were accounted: 0.812 (the same file, 7,901 bytes; then phagspa.ttf 0.679). The multiple, 8, is about 10 times that: a
    /// small font's fixed per-face costs weigh more, so small files keep room.
    /// </summary>
    internal const long MaxParsedBytesPerFileByte = 8;

    /// <summary>
    /// The load work allowed per byte of the file (Denys's "Rules + work budget", 2026-11-26): a backstop for routes to
    /// super-linear load work the rules have not closed. A unit of work is about one byte (or one element) walked: see
    /// <see cref="Charge"/>. Measured on 2026-11-26 with <see cref="SfntReader.MeasureWork"/> over 200 files: the 192 font
    /// files of <c>C:\Windows\Fonts</c> that load (the other 10 have only symbol <c>cmap</c>s), the two repository fonts, the
    /// four Source Sans 3 files and the two Myriad Pro TrueType files of the game's resources. The largest ratios of work to
    /// file length: 1.70 (unispace bd.ttf: 85,031 units for 49,980 bytes), 1.69 (mingliub.ttc), 1.67 (SimsunExtG.ttf,
    /// simsunb.ttf, SegoeIcons.ttf); the median 1.12. The multiple, 32, is about 19 times the largest: it leaves room for
    /// glyphs whose repeated flags declare many points in few bytes. A gzip <c>SVG </c> document charges only its compressed
    /// bytes, once: its inflated output is bounded by <see cref="SvgTable.InflateBudget"/>, not by this budget (and the
    /// document overlap rule keeps each compressed byte in one document).
    /// </summary>
    internal const long MaxWorkPerFileByte = 32;

    /// <summary>The work charged for each table parse, for its fixed reads (headers, the parse's own set-up).</summary>
    internal const long ParseWork = 64;

    /// <summary>Work allowed on top of <see cref="MaxWorkPerFileByte"/> times the file's length, so tiny files keep room.</summary>
    internal const long WorkAllowance = 1024 * 1024;

    private readonly Dictionary<TableRecord, (TableInputs Inputs, int Face, object? Table)> m_parsed = [];
    private readonly Dictionary<(long Offset, long Length), ReadOnlyMemory<byte>> m_svgDocuments = [];
    private readonly long m_parsedLimit;
    private readonly long m_workLimit;

    /// <param name="fileLength">The length of the sfnt or collection image.</param>
    /// <param name="unlimited">
    /// No parsed-memory or work limit (for the measurements behind <see cref="MaxParsedBytesPerFileByte"/> and
    /// <see cref="MaxWorkPerFileByte"/>).
    /// </param>
    /// <param name="workLimit">A work limit instead of the file's (for tests).</param>
    public ParsedTables(long fileLength, bool unlimited = false, long? workLimit = null)
    {
        m_parsedLimit = unlimited ? long.MaxValue : MaxParsedBytesPerFileByte * fileLength;
        m_workLimit = unlimited ? long.MaxValue : workLimit ?? MaxWorkPerFileByte * fileLength + WorkAllowance;
    }

    /// <summary>The parsed-table bytes accounted so far.</summary>
    public long ParsedBytes { get; private set; }

    /// <summary>The load work charged so far.</summary>
    public long Work { get; private set; }

    /// <summary>What is left of the file's <c>SVG </c> inflate budget.</summary>
    public long SvgBudget { get; set; } = SvgTable.InflateBudget;

    /// <summary>Inflated <c>SVG </c> documents by their position in the file, shared by every table that points at them.</summary>
    public Dictionary<(long Offset, long Length), ReadOnlyMemory<byte>> SvgDocuments => m_svgDocuments;

    /// <summary>How many tables the load has parsed: each distinct record (with its inputs) once.</summary>
    public int TablesParsed { get; private set; }

    /// <summary>
    /// The table parsed from <paramref name="record"/> with <paramref name="inputs"/>, parsing it on first use. Every face that
    /// shares a record must read it with the same inputs (Denys's "Same-inputs rule", 2026-11-26), so each record is parsed
    /// once per file.
    /// </summary>
    /// <param name="record">The table's record.</param>
    /// <param name="inputs">Everything besides the table's bytes that its parse depends on.</param>
    /// <param name="faceIndex">The face reading it, for the message.</param>
    /// <param name="parse">Parses the table.</param>
    /// <exception cref="InvalidDataException">An earlier face read the record with other inputs.</exception>
    public T Get<T>(TableRecord record, TableInputs inputs, int faceIndex, Func<T> parse)
        where T : class
    {
        if (Register(record, inputs, faceIndex) is { } table)
            return (T)table;

        Charge(ParseWork, "parsing the table at offset", record.Offset);
        var parsedTable = parse();
        TablesParsed++;
        m_parsed[record] = (inputs, faceIndex, parsedTable);
        return parsedTable;
    }

    /// <summary>
    /// Records that a face reads <paramref name="record"/> with <paramref name="inputs"/> (for a table parsed together with
    /// another, as <c>loca</c> with <c>glyf</c>); throws when an earlier face read it with other inputs.
    /// </summary>
    /// <returns>The table parsed from the record so far, if any.</returns>
    public object? Register(TableRecord record, TableInputs inputs, int faceIndex)
    {
        if (!m_parsed.TryGetValue(record, out var entry))
        {
            m_parsed.Add(record, (inputs, faceIndex, null));
            return null;
        }

        if (!entry.Inputs.Equals(inputs))
            throw new InvalidDataException(
                $"Faces {entry.Face} and {faceIndex} share the '{Tags.ToString(record.Tag)}' table ({record.Length} bytes at " +
                $"{record.Offset}) but read it with different inputs ({entry.Inputs}, then {inputs}): faces that share a table " +
                "must read it with the same glyph count, 'loca' format, number of metrics and companion table.");

        return entry.Table;
    }

    /// <summary>Accounts memory a parser is about to allocate; throws when the file's budget would be passed.</summary>
    /// <param name="bytes">The bytes about to be allocated.</param>
    /// <param name="what">What for, for the message: a constant, so no call allocates.</param>
    /// <param name="index">The glyph, name, face or position it is for, appended to the message; -1 for none.</param>
    public void Reserve(long bytes, string what, long index = -1)
    {
        ParsedBytes += bytes;
        if (ParsedBytes > m_parsedLimit)
            throw new NotSupportedException(
                $"Parsing the font needs more than {MaxParsedBytesPerFileByte} bytes per byte of the file ({m_parsedLimit} bytes), " +
                $"this library's limit; reached at {Describe(what, index)}.");
    }

    /// <summary>
    /// Charges load work, before the walk where its size is known: one unit per byte a parser walks (or per element it
    /// visits, where an element takes no bytes, as a repeated point flag); throws when the file's work budget would be passed.
    /// </summary>
    /// <param name="units">The work about to be done.</param>
    /// <param name="what">What for, for the message: a constant, so no call allocates (the message is made only to throw).</param>
    /// <param name="index">The glyph, segment, name, face or position it is for, appended to the message; -1 for none.</param>
    public void Charge(long units, string what, long index = -1)
    {
        Work += units;
        if (Work > m_workLimit)
            throw new NotSupportedException(
                $"Loading the font takes more work than this library allows ({m_workLimit} units: {MaxWorkPerFileByte} per byte of " +
                $"the file plus {WorkAllowance}); reached at {Describe(what, index)}.");
    }

    private static string Describe(string what, long index) => index < 0 ? what : $"{what} {index}";
}
