namespace CoolEngine.Fonts;

/// <summary>One axis of a variable font, in user-space values.</summary>
/// <param name="Tag">The four-character axis tag, such as <c>wght</c>.</param>
/// <param name="Minimum">The smallest value.</param>
/// <param name="Default">The default instance's value.</param>
/// <param name="Maximum">The largest value.</param>
/// <param name="Name">The axis name from the <c>name</c> table.</param>
/// <param name="IsHidden">The font asks applications not to show this axis.</param>
public readonly record struct VariationAxis(string Tag, float Minimum, float Default, float Maximum, string Name, bool IsHidden);
