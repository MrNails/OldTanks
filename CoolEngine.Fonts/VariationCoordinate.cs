namespace CoolEngine.Fonts;

/// <summary>A user-space value for one variation axis.</summary>
/// <param name="Tag">The four-character axis tag, such as <c>wght</c>.</param>
/// <param name="Value">The user-space value.</param>
public readonly record struct VariationCoordinate(string Tag, float Value);
