namespace CoolEngine.Physics.Shapes;

/// <summary>
/// A sphere. It holds no data: the centre is the body's position and the radius half the body's width
/// (stage 1: <c>Size.X / 2</c>, as today's <c>PhysicObject.Width / 2</c>), so one instance serves every sphere.
/// </summary>
public sealed class SphereShape : Shape
{
    public SphereShape()
    {
    }
}
