using CoolEngine.Fonts.Sfnt;

namespace CoolEngine.Fonts.Hinting.TrueType;

/// <summary>Creates the TrueType hinter for one size.</summary>
internal static class TrueTypeHinterFactory
{
    /// <param name="face">The face's tables (<c>fpgm</c>, <c>prep</c>, <c>cvt </c>, <c>hdmx</c>, <c>maxp</c> limits).</param>
    /// <param name="scale">The size.</param>
    /// <param name="version">The interpreter mode.</param>
    /// <remarks>
    /// Runs <c>fpgm</c> and <c>prep</c> at once; their failure is reported by every hinted glyph load
    /// (<see cref="ITrueTypeHinter.BeginGlyph"/>), as FreeType reports it.
    /// </remarks>
    /// <param name="variation">The variation coordinates (the blend), or null for none (OT-4 WP5).</param>
    public static ITrueTypeHinter Create(FaceData face, FontScale scale, TrueTypeInterpreterVersion version,
        Variations.VariationInstance? variation = null) =>
        new TrueTypeHinter(face, scale, version, variation);
}
