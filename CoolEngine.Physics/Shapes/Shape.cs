namespace CoolEngine.Physics.Shapes;

/// <summary>
/// A collision shape: <see cref="PolygonShape"/> or <see cref="SphereShape"/>, nothing else (a closed hierarchy).
/// Immutable, shared by reference between bodies, and safe to read from any thread.
/// </summary>
public abstract class Shape
{
    private protected Shape()
    {
    }
}
