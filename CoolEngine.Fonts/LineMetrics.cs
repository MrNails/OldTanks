namespace CoolEngine.Fonts;

/// <summary>A scaled font's global metrics in 26.6 fixed-point pixels, as FreeType's <c>FT_Size_Metrics</c>.</summary>
/// <param name="Ascender">The face's ascender, rounded up to a whole pixel.</param>
/// <param name="Descender">The face's descender (negative below the baseline), rounded down to a whole pixel.</param>
/// <param name="Height">The baseline-to-baseline distance, rounded to a whole pixel.</param>
/// <param name="MaxAdvance">The largest advance width, rounded to a whole pixel.</param>
public readonly record struct LineMetrics(int Ascender, int Descender, int Height, int MaxAdvance);
