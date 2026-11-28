using System.Numerics;
using CoolEngine.Physics.Shapes;

namespace CoolEngine.Physics;

/// <summary>
/// What <see cref="PhysicsWorld.Add"/> needs to make a body. Validated by <c>Add</c>, not here, so <c>default</c> is
/// representable (and rejected there).
/// </summary>
/// <remarks>
/// <see cref="Orientation"/> is a unit quaternion and <see cref="HalfExtents"/> half the body's size along its own axes
/// (a sphere's three are equal, its radius). Velocity and force are in world space.
/// </remarks>
public readonly struct BodyDescription
{
    public BodyDescription(Shape shape, Vector3 position, Quaternion orientation, Vector3 halfExtents)
    {
        Shape = shape;
        Position = position;
        Orientation = orientation;
        HalfExtents = halfExtents;
        Mass = 1;
    }

    public Shape Shape { get; }

    public Vector3 Position { get; }

    public Quaternion Orientation { get; }

    public Vector3 HalfExtents { get; }

    /// <summary>World space; zero by default.</summary>
    public Vector3 Velocity { get; init; }

    /// <summary>World space; zero by default.</summary>
    public Vector3 Force { get; init; }

    /// <summary>1 by default (today's <c>RigidBody.Weight</c>); used only as <c>Force / Mass</c>.</summary>
    public float Mass { get; init; }

    /// <summary>False by default.</summary>
    public bool IsStatic { get; init; }
}
