using CoolEngine.Fonts.Sfnt;

namespace CoolEngine.Fonts.Hinting.Cff;

/// <summary>
/// The seam for CFF stem hinting (<see cref="HintingMode.Native"/> on a CFF or CFF2 face). A stub until OT-4 WP6, which
/// adds FreeType's Adobe-engine hinting here.
/// </summary>
internal static class CffHinterFactory
{
    /// <summary>Makes the hinter for a face at one size.</summary>
    /// <exception cref="NotSupportedException">Always: CFF hinting comes with OT-4 WP6.</exception>
    public static object Create(FaceData face, FontScale scale) => throw new NotSupportedException("CFF hinting: OT-4 WP6");
}
