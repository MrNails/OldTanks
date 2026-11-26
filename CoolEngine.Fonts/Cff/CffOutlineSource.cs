using CoolEngine.Fonts.Sfnt;

namespace CoolEngine.Fonts.Cff;

/// <summary>
/// Outlines from a <c>CFF </c> or <c>CFF2</c> table. A stub until OT-4 WP2: the face loads (its sfnt tables, character map
/// and metrics work), and loading any glyph throws <see cref="NotSupportedException"/>.
/// </summary>
internal sealed class CffOutlineSource : IOutlineSource
{
    /// <param name="face">The face's tables; WP2 parses and checks the CFF table here, at load.</param>
    /// <param name="isCff2">The table is <c>CFF2</c> rather than <c>CFF </c>.</param>
    public CffOutlineSource(FaceData face, bool isCff2)
    {
    }

    public IGlyphLoader CreateLoader(ScaledFontOptions options, FontScale scale) => new StubLoader();

    private sealed class StubLoader : IGlyphLoader
    {
        public LoadedGlyph Load(int glyphIndex) => throw new NotSupportedException("CFF outlines: OT-4 WP2");
    }
}
