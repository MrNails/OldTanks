namespace CoolEngine.Fonts;

/// <summary>How <see cref="FontFace.CreateScaledFont"/> sizes and hints a face.</summary>
/// <param name="PixelSize">
/// The nominal size in pixels per em, 1 to 4096, as <c>FT_Set_Pixel_Sizes(face, 0, PixelSize)</c>.
/// </param>
public sealed record ScaledFontOptions(int PixelSize)
{
    internal const int MinPixelSize = 1;
    internal const int MaxPixelSize = 4096;

    /// <summary>Grid fitting; <see cref="HintingMode.Native"/> by default.</summary>
    public HintingMode Hinting { get; init; } = HintingMode.Native;

    /// <summary>The TrueType interpreter mode used with <see cref="HintingMode.Native"/>; ignored for CFF faces.</summary>
    public TrueTypeInterpreterVersion Interpreter { get; init; } = TrueTypeInterpreterVersion.V40;

    /// <summary>User-space axis values; an axis not listed stays at its default.</summary>
    public IReadOnlyList<VariationCoordinate> Variation { get; init; } = [];
}
