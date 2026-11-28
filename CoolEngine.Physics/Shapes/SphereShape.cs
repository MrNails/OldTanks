namespace CoolEngine.Physics.Shapes;

/// <summary>
/// A sphere. It holds no data: the centre is the body's position and the radius the body's X half-extent (a sphere's
/// three half-extents are equal), so one instance serves every sphere.
/// </summary>
public sealed class SphereShape : Shape
{
    public SphereShape()
    {
    }
}
