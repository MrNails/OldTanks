using System.Runtime.InteropServices;
using System.Runtime.InteropServices.Marshalling;

namespace CoolEngine.Fonts.Tests.Reference;

/// <summary>
/// Windows' DirectWrite through its COM interfaces (own interop, no package): the reference for what does not depend on a
/// rasteriser, such as glyph indices and design advance widths. An isolated factory per instance.
/// </summary>
internal sealed class DirectWriteReference : IDisposable
{
    private static readonly StrategyBasedComWrappers s_wrappers = new();

    private IDWriteFactory? m_factory;

    public DirectWriteReference()
    {
        var hr = DirectWriteNative.DWriteCreateFactory(DirectWriteNative.FactoryTypeIsolated, typeof(IDWriteFactory).GUID, out var factory);
        Marshal.ThrowExceptionForHR(hr);
        m_factory = Wrap<IDWriteFactory>(factory);
    }

    /// <summary>Opens a face of a font file (by path; DirectWrite reads it in place).</summary>
    public DirectWriteFace OpenFace(string path, int faceIndex)
    {
        var factory = m_factory ?? throw new ObjectDisposedException(nameof(DirectWriteReference));
        factory.CreateFontFileReference(path, 0, out var file);
        try
        {
            var fontFile = Wrap<IDWriteFontFile>(file, keepCallerReference: true);
            fontFile.Analyze(out var supported, out _, out var faceType, out var faceCount);
            Release(fontFile);
            if (supported == 0)
                throw new InvalidOperationException($"DirectWrite does not support {path}.");

            if (faceIndex >= faceCount)
                throw new InvalidOperationException($"DirectWrite finds {faceCount} faces in {path}, not {faceIndex + 1}.");

            factory.CreateFontFace(faceType, 1, file, (uint)faceIndex, 0, out var face);
            return new DirectWriteFace(face);
        }
        finally
        {
            Marshal.Release(file);
        }
    }

    /// <summary>
    /// <c>IDWriteFactory5::UnpackFontFile</c>: the sfnt (or collection) image DirectWrite makes of a WOFF or WOFF2 file, read from
    /// the unpacked stream with <c>GetFileSize</c> and <c>ReadFileFragment</c>.
    /// </summary>
    /// <exception cref="COMException">DirectWrite refuses the file.</exception>
    public unsafe byte[] UnpackFontFile(DWriteContainerType containerType, byte[] file)
    {
        var factory = (IDWriteFactory5)(m_factory ?? throw new ObjectDisposedException(nameof(DirectWriteReference)));

        IDWriteFontFileStream stream;
        fixed (byte* data = file)
            factory.UnpackFontFile((int)containerType, data, (uint)file.Length, out stream);

        try
        {
            stream.GetFileSize(out var size);
            var result = new byte[size];
            if (size == 0)
                return result;

            stream.ReadFileFragment(out var fragment, 0, size, out var context);
            try
            {
                new ReadOnlySpan<byte>(fragment, (int)size).CopyTo(result);
            }
            finally
            {
                stream.ReleaseFileFragment(context);
            }

            return result;
        }
        finally
        {
            Release(stream);
        }
    }

    public void Dispose()
    {
        if (m_factory is null)
            return;

        Release(m_factory);
        m_factory = null;
    }

    internal static void Release(object comObject)
    {
        if (comObject is ComObject wrapper)
            wrapper.FinalRelease();
    }

    /// <summary>Wraps a raw interface pointer; the wrapper holds its own reference, and ours is released unless <paramref name="keepCallerReference"/>.</summary>
    private static T Wrap<T>(nint pointer, bool keepCallerReference = false)
    {
        var wrapper = (T)s_wrappers.GetOrCreateObjectForComInstance(pointer, CreateObjectFlags.UniqueInstance);
        if (!keepCallerReference)
            Marshal.Release(pointer);

        return wrapper;
    }
}

/// <summary>One DirectWrite font face.</summary>
internal sealed unsafe class DirectWriteFace : IDisposable
{
    private IDWriteFontFace? m_face;

    public DirectWriteFace(IDWriteFontFace face) => m_face = face;

    private IDWriteFontFace Face => m_face ?? throw new ObjectDisposedException(nameof(DirectWriteFace));

    public int GlyphCount => Face.GetGlyphCount();

    /// <summary><c>IDWriteFontFace::GetGlyphIndices</c>.</summary>
    public ushort[] GetGlyphIndices(uint[] codePoints)
    {
        var glyphs = new ushort[codePoints.Length];
        fixed (uint* points = codePoints)
        fixed (ushort* indices = glyphs)
            Face.GetGlyphIndices(points, (uint)codePoints.Length, indices);

        return glyphs;
    }

    /// <summary>The design advance width of every glyph (<c>IDWriteFontFace::GetDesignGlyphMetrics</c>, horizontal).</summary>
    public uint[] GetDesignAdvances()
    {
        var count = GlyphCount;
        var glyphs = new ushort[count];
        for (var i = 0; i < count; i++)
            glyphs[i] = (ushort)i;

        var metrics = new DWriteGlyphMetrics[count];
        fixed (ushort* indices = glyphs)
        fixed (DWriteGlyphMetrics* output = metrics)
            Face.GetDesignGlyphMetrics(indices, (uint)count, output, 0);

        return metrics.Select(metric => metric.AdvanceWidth).ToArray();
    }

    public void Dispose()
    {
        if (m_face is null)
            return;

        DirectWriteReference.Release(m_face);
        m_face = null;
    }
}
