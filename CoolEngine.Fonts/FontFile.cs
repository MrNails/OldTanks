namespace CoolEngine.Fonts;

/// <summary>A loaded font file: one face, or several for a collection. Immutable, safe on any thread.</summary>
public sealed class FontFile
{
    internal FontFile(FontFormat format, IReadOnlyList<FontFace> faces)
    {
        Format = format;
        Faces = faces;
    }

    /// <summary>The container format the file was read as.</summary>
    public FontFormat Format { get; }

    /// <summary>The faces in file order; a collection gives several.</summary>
    public IReadOnlyList<FontFace> Faces { get; }
}
