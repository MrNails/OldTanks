using CoolEngine.Fonts.Sfnt;

namespace CoolEngine.Fonts.Hinting.TrueType;

/// <summary>Creates the TrueType hinter for one size. A stub until OT-4 WP4 writes the interpreter.</summary>
internal static class TrueTypeHinterFactory
{
    /// <param name="face">The face's tables (<c>fpgm</c>, <c>prep</c>, <c>cvt </c>, <c>maxp</c> limits).</param>
    /// <param name="scale">The size.</param>
    /// <param name="version">The interpreter mode.</param>
    /// <exception cref="NotSupportedException">Always, until WP4.</exception>
    public static ITrueTypeHinter Create(FaceData face, FontScale scale, TrueTypeInterpreterVersion version) =>
        throw new NotSupportedException("TrueType hinting: OT-4 WP4");
}
