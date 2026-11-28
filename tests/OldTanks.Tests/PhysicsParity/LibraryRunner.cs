using System.Numerics;
using CoolEngine.Mathematics;
using CoolEngine.PhysicEngine;
using CoolEngine.PhysicEngine.Core.Collision;
using CoolEngine.Physics;
using CoolEngine.Physics.Shapes;

namespace OldTanks.Tests.PhysicsParity;

/// <summary>
/// A <see cref="PhysicsWorld"/> built from the same <see cref="SceneDescription"/> as <see cref="OldWorldRunner"/>:
/// today's settings, today's cube shape converted once, the stage-1 linear part and velocity frame from each body's
/// rotation and size, and velocities clamped as today's setter clamps them when the object is made.
/// </summary>
internal sealed class LibraryRunner
{
    private readonly PolygonShape m_cubeShape;
    private readonly SphereShape m_sphereShape = new SphereShape();
    private readonly List<BodyHandle?> m_bySlot = new List<BodyHandle?>();

    public LibraryRunner(SceneDescription scene, CollisionData cubeShape)
    {
        m_cubeShape = ToShape(cubeShape);
        World = new PhysicsWorld(Settings(), scene.Bodies.Count);

        foreach (var spec in scene.Bodies)
            Add(spec);
    }

    public PhysicsWorld World { get; }

    public StageOneDrive Drive { get; } = new StageOneDrive();

    public IReadOnlyList<BodyHandle?> BySlot => m_bySlot;

    /// <summary>Today's numbers: PhysicsConstants.GravityDirection, FreeFallingAcceleration, MaxFreeFallingSpeed; 5 sub-steps.</summary>
    public static PhysicsSettings Settings() =>
        new PhysicsSettings(PhysicsConstants.GravityDirection, PhysicsConstants.FreeFallingAcceleration,
            PhysicsConstants.MaxFreeFallingSpeed, 5);

    /// <summary>A polygon <see cref="CollisionData"/> as a <see cref="PolygonShape"/>: the same vertices, index lists and normals.</summary>
    public static PolygonShape ToShape(CollisionData data)
    {
        var faces = new ShapeFace[data.Faces.Count];
        for (var i = 0; i < faces.Length; i++)
            faces[i] = new ShapeFace(data.Faces[i].Indices, data.Faces[i].Normal);

        return new PolygonShape(data.Vertices, faces);
    }

    /// <summary>
    /// <c>WorldObject.ApplyTransformation</c>'s rotation × scale with the Matrix4 factories: rotation Z (roll) ×
    /// rotation Y (−pitch) × rotation X (yaw) × scale (width / 2, height / 2, length / 2).
    /// </summary>
    public static Matrix3 Linear(Vector3 rotation, Vector3 size) =>
        new Matrix3(Matrix4.CreateRotationZ(rotation.Z) *
                    (Matrix4.CreateRotationY(-rotation.Y) *
                     (Matrix4.CreateRotationX(rotation.X) * Matrix4.CreateScale(size.X / 2, size.Y / 2, size.Z / 2))));

    /// <summary><c>WorldObject.Move</c>'s frame: <c>Matrix3.CreateRotationY(Rotation.Y).Transposed()</c>.</summary>
    public static Matrix3 VelocityFrame(Vector3 rotation) => Matrix3.CreateRotationY(rotation.Y).Transposed();

    public BodyHandle Add(BodySpec spec)
    {
        // Today's Sphere keeps width, height and length equal to the width.
        var size = spec.IsSphere ? new Vector3(spec.Size.X) : spec.Size;
        var description = new BodyDescription(spec.IsSphere ? m_sphereShape : m_cubeShape, spec.Position,
            Linear(spec.Rotation, size), size, VelocityFrame(spec.Rotation))
        {
            Velocity = StageOneDrive.ClampForward(spec.Velocity, spec.MaxSpeed, spec.MaxBackSpeed,
                spec.MaxSpeedMultiplier),
            Mass = spec.Weight,
            IsStatic = spec.IsStatic
        };

        var handle = World.Add(description);
        Drive.SetLimits(handle, spec.MaxSpeed, spec.MaxBackSpeed, spec.MaxSpeedMultiplier);

        while (m_bySlot.Count <= handle.Slot)
            m_bySlot.Add(null);
        m_bySlot[handle.Slot] = handle;

        return handle;
    }

    public void Apply(TickEvent tickEvent)
    {
        if (tickEvent.Add != null)
            return;

        var handle = m_bySlot[tickEvent.Body]!.Value;

        if (tickEvent.Force is { } force && !World.IsStatic(handle))
            World.AddForce(handle, force);

        if (tickEvent.Velocity is { } velocity && !World.IsStatic(handle))
            World.SetVelocity(handle, Drive.ClampToLimits(handle, velocity));

        if (tickEvent.Rotation is { } rotation)
        {
            World.SetLinear(handle, Linear(rotation, World.GetSize(handle)));
            World.SetVelocityFrame(handle, VelocityFrame(rotation));
        }

        if (tickEvent.MakeStatic is { } isStatic)
            World.SetStatic(handle, isStatic);

        if (tickEvent.Remove)
        {
            World.Remove(handle);
            m_bySlot[tickEvent.Body] = null;
        }
    }

    public void RunTick(float timeStep) => Drive.RunTick(World, timeStep);
}
