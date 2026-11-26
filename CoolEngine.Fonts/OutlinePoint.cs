namespace CoolEngine.Fonts;

/// <summary>One outline point in 26.6 fixed-point pixels, y up, relative to the glyph origin.</summary>
public readonly record struct OutlinePoint(int X, int Y, OutlinePointKind Kind);
