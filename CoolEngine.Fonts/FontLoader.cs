using System.Buffers.Binary;
using CoolEngine.Fonts.Sfnt;
using CoolEngine.Fonts.Woff;

namespace CoolEngine.Fonts;

/// <summary>
/// Loads TrueType, OpenType, collection, WOFF and WOFF2 files into <see cref="FontFile"/>s. Strict: nonconforming data throws
/// <see cref="InvalidDataException"/> while loading (the rules are listed on <see cref="SfntReader"/>); a format or feature
/// out of scope, or valid data past one of the library's limits (such as <see cref="MaxFileSize"/>), throws
/// <see cref="NotSupportedException"/>. Stateless: safe on any thread.
/// </summary>
public static class FontLoader
{
    /// <summary>The largest file accepted, 128 MiB; also the limit for a decoded WOFF or WOFF2 font.</summary>
    public const long MaxFileSize = 128L * 1024 * 1024;

    private const uint TrueTypeVersion = 0x00010000;
    private const uint AppleTrueTypeTag = 0x74727565;  // true
    private const uint OpenTypeCffTag = 0x4F54544F;    // OTTO
    private const uint CollectionTag = 0x74746366;     // ttcf
    private const uint WoffTag = 0x774F4646;           // wOFF
    private const uint Woff2Tag = 0x774F4632;          // wOF2

    /// <summary>Reads the signature, not the file extension.</summary>
    /// <returns>The format; <see cref="FontFormat.Unknown"/> for anything else, including fewer than four bytes.</returns>
    public static FontFormat DetectFormat(ReadOnlySpan<byte> data)
    {
        if (data.Length < 4)
            return FontFormat.Unknown;

        return BinaryPrimitives.ReadUInt32BigEndian(data) switch
        {
            TrueTypeVersion or AppleTrueTypeTag => FontFormat.TrueType,
            OpenTypeCffTag => FontFormat.OpenTypeCff,
            CollectionTag => FontFormat.Collection,
            WoffTag => FontFormat.Woff,
            Woff2Tag => FontFormat.Woff2,
            _ => FontFormat.Unknown,
        };
    }

    /// <summary>Parses a whole font file. The bytes are copied, so the caller may reuse the array.</summary>
    /// <exception cref="InvalidDataException">The data breaks a strict rule.</exception>
    /// <exception cref="NotSupportedException">
    /// The format or a feature it uses is out of scope, or the data passes a library limit (it is longer than
    /// <see cref="MaxFileSize"/>, for one).
    /// </exception>
    public static FontFile Load(byte[] data)
    {
        ArgumentNullException.ThrowIfNull(data);
        CheckSize(data.LongLength);

        return LoadOwned((byte[])data.Clone());
    }

    /// <summary>Reads and parses a font file.</summary>
    /// <inheritdoc cref="Load(byte[])"/>
    public static FontFile Load(string path)
    {
        ArgumentNullException.ThrowIfNull(path);
        CheckSize(new FileInfo(path).Length);

        return LoadOwned(File.ReadAllBytes(path));
    }

    /// <summary>Reads a font file asynchronously, then parses it on the thread pool (inside <see cref="Task.Run(Action)"/>).</summary>
    /// <inheritdoc cref="Load(byte[])"/>
    public static async Task<FontFile> LoadAsync(string path, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(path);
        CheckSize(new FileInfo(path).Length);

        var data = await File.ReadAllBytesAsync(path, cancellationToken).ConfigureAwait(false);

        return await Task.Run(() => LoadOwned(data), cancellationToken).ConfigureAwait(false);
    }

    private static FontFile LoadOwned(byte[] data)
    {
        CheckSize(data.LongLength);

        var format = DetectFormat(data);
        var sfnt = format switch
        {
            FontFormat.TrueType or FontFormat.OpenTypeCff or FontFormat.Collection => data,
            FontFormat.Woff => WoffDecoder.Decode(data),
            FontFormat.Woff2 => Woff2Decoder.Decode(data),
            _ => throw new NotSupportedException("The data is not a TrueType, OpenType, collection, WOFF or WOFF2 font."),
        };

        CheckSize(sfnt.LongLength);

        return new FontFile(format, SfntReader.ReadFaces(sfnt));
    }

    private static void CheckSize(long length)
    {
        if (length > MaxFileSize)
            throw new NotSupportedException($"The font is {length} bytes long, more than the {MaxFileSize}-byte limit.");
    }
}
