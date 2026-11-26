namespace CoolEngine.Fonts;

/// <summary>A named instance of a variable font.</summary>
/// <param name="Name">The instance's subfamily name from the <c>name</c> table.</param>
/// <param name="Coordinates">User-space values, one per axis, in <see cref="FontFace.Axes"/> order.</param>
public sealed record NamedInstance(string Name, IReadOnlyList<float> Coordinates);
