namespace CoolEngine.Fonts.Sfnt;

/// <summary>
/// The outline source of a face with no outline table (only bitmaps or SVG documents): every glyph is empty, with the
/// <c>hmtx</c> advance scaled as for an empty TrueType glyph.
/// </summary>
internal sealed class EmptyOutlineSource : IOutlineSource
{
    private readonly FaceData m_face;

    public EmptyOutlineSource(FaceData face) => m_face = face;

    public IGlyphLoader CreateLoader(ScaledFontOptions options, FontScale scale) => new Loader(m_face, scale);

    private sealed class Loader : IGlyphLoader
    {
        private readonly FaceData m_face;
        private readonly FontScale m_scale;

        public Loader(FaceData face, FontScale scale)
        {
            m_face = face;
            m_scale = scale;
        }

        public LoadedGlyph Load(int glyphIndex)
        {
            var (advance, bearing) = m_face.Hmtx.Get(glyphIndex);
            long left = -bearing;
            var scaled = FixedMath.MulFix(left + advance, m_scale.XScale) - FixedMath.MulFix(left, m_scale.XScale);

            return new LoadedGlyph(GlyphOutline.Empty, new GlyphMetrics(0, 0, 0, 0, (int)scaled), false);
        }
    }
}
