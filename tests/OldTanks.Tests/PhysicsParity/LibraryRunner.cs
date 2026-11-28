using System.Numerics;
using CoolEngine.PhysicEngine;
using CoolEngine.PhysicEngine.Core.Collision;
using CoolEngine.Physics;
using CoolEngine.Physics.Shapes;

namespace OldTanks.Tests.PhysicsParity;

/// <summary>
/// A <see cref="PhysicsWorld"/> built from the same <see cref="SceneDescription"/> as <see cref="OldWorldRunner"/> and
/// driven by <see cref="WorldSpaceDrive"/>: today's settings, today's cube shape converted once, each body's orientation
/// and half-extents from its rotation and size by the game's conversion, and its velocity, given in today's local terms,
/// clamped as today's setter clamps it and turned into world space with the heading.
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

    public WorldSpaceDrive Drive { get; } = new WorldSpaceDrive();

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

    public BodyHandle Add(BodySpec spec)
    {
        // Today's Sphere keeps width, height and length equal to the width.
        var size = spec.IsSphere ? new Vector3(spec.Size.X) : spec.Size;
        var velocity = WorldSpaceDrive.ToWorldVelocity(spec.Velocity, spec.MaxSpeed, spec.MaxBackSpeed,
            spec.MaxSpeedMultiplier, spec.Rotation.Y);
        var description = new BodyDescription(spec.IsSphere ? m_sphereShape : m_cubeShape, spec.Position,
            WorldSpaceDrive.ToOrientation(spec.Rotation), WorldSpaceDrive.ToHalfExtents(size))
        {
            Velocity = velocity,
            Mass = spec.Weight,
            IsStatic = spec.IsStatic
        };

        var handle = World.Add(description);
        Drive.Register(handle, spec.MaxSpeed, spec.MaxBackSpeed, spec.MaxSpeedMultiplier, spec.Rotation.Y);

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

        if (tickEvent.Force is { } force)
            Drive.AddLocalForce(World, handle, force);

        if (tickEvent.Velocity is { } velocity)
            Drive.SetLocalVelocity(World, handle, velocity);

        if (tickEvent.Rotation is { } rotation)
        {
            World.SetOrientation(handle, WorldSpaceDrive.ToOrientation(rotation));
            Drive.SetHeading(World, handle, rotation.Y);
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
