using System.Numerics;
using CoolEngine.Mathematics;
using CoolEngine.Physics.Shapes;

namespace CoolEngine.Physics;

/// <summary>
/// What <see cref="PhysicsWorld.Add"/> needs to make a body. Validated by <c>Add</c>, not here, so <c>default</c> is
/// representable (and rejected there).
/// </summary>
/// <remarks>
/// Stage 1 (transitional): the body is given today's way. <see cref="Linear"/> is the game's rotation × scale
/// (<c>new Matrix3(CreateRotationZ(roll) * (CreateRotationY(-pitch) * (CreateRotationX(yaw) * CreateScale(size / 2))))</c>
/// with the Matrix4 factories), <see cref="Size"/> today's <c>WorldObject.Size</c> (a sphere's width is
/// <c>Size.X</c>), and <see cref="VelocityFrame"/> the frame that turns the stored velocity into world space
/// (<c>Matrix3.CreateRotationY(heading).Transposed()</c>). Velocity and force are in that frame.
/// </remarks>
public readonly struct BodyDescription
{
    public BodyDescription(Shape shape, Vector3 position, Matrix3 linear, Vector3 size, Matrix3 velocityFrame)
    {
        Shape = shape;
        Position = position;
        Linear = linear;
        Size = size;
        VelocityFrame = velocityFrame;
        Mass = 1;
    }

    public Shape Shape { get; }

    public Vector3 Position { get; }

    public Matrix3 Linear { get; }

    public Vector3 Size { get; }

    public Matrix3 VelocityFrame { get; }

    /// <summary>Zero by default.</summary>
    public Vector3 Velocity { get; init; }

    /// <summary>Zero by default.</summary>
    public Vector3 Force { get; init; }

    /// <summary>1 by default (today's <c>RigidBody.Weight</c>); used only as <c>Force / Mass</c>.</summary>
    public float Mass { get; init; }

    /// <summary>False by default.</summary>
    public bool IsStatic { get; init; }
}
