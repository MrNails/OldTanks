using CoolEngine.PhysicEngine.Core.Collision;
using CoolEngine.Services;
using OldTanks.Infrastructure;
using OldTanks.Models;

namespace OldTanks.Tests.PhysicsParity;

/// <summary>
/// Today's <see cref="World"/> built from a <see cref="SceneDescription"/> with real <see cref="Cube"/> and
/// <see cref="Sphere"/> objects and the shapes their static constructors register. The object list mirrors the
/// library's slot order: a new body is inserted where its slot falls among the live slots.
/// </summary>
internal sealed class OldWorldRunner
{
    private readonly World m_world = new World();
    private readonly List<WorldObject?> m_bySlot = new List<WorldObject?>();
    private readonly CollisionData m_cubeShape;
    private readonly CollisionData m_sphereShape;

    /// <summary>Needs <c>EngineSettings.Current.CollisionIterations</c> and the fake scenes in the cache (the fixture's set-up).</summary>
    public OldWorldRunner(SceneDescription scene)
    {
        m_cubeShape = CubeShape();
        m_sphereShape = GetShape(CollisionConstants.SphereCollisionName, () => _ = new Sphere());

        foreach (var spec in scene.Bodies)
            Add(spec, m_bySlot.Count);
    }

    public World World => m_world;

    public IReadOnlyList<WorldObject?> BySlot => m_bySlot;

    /// <summary>The game's cube shape, registered by <see cref="Cube"/>'s static constructor.</summary>
    public static CollisionData CubeShape() => GetShape(CollisionConstants.CubeCollisionName, () => _ = new Cube());

    public void Add(BodySpec spec, int slot)
    {
        WorldObject worldObject = spec.IsSphere ? new Sphere() : new Cube();
        worldObject.Size = spec.Size;
        worldObject.Position = spec.Position;
        worldObject.Rotation = spec.Rotation;
        worldObject.Collision = new Collision(worldObject, spec.IsSphere ? m_sphereShape : m_cubeShape);
        worldObject.RigidBody.MaxSpeed = spec.MaxSpeed;
        worldObject.RigidBody.MaxBackSpeed = spec.MaxBackSpeed;
        worldObject.RigidBody.MaxSpeedMultiplier = spec.MaxSpeedMultiplier;
        worldObject.RigidBody.Weight = spec.Weight;
        worldObject.RigidBody.Velocity = spec.Velocity;
        worldObject.RigidBody.IsStatic = spec.IsStatic;

        while (m_bySlot.Count <= slot)
            m_bySlot.Add(null);

        var index = 0;
        for (var i = 0; i < slot; i++)
        {
            if (m_bySlot[i] != null)
                index++;
        }

        m_bySlot[slot] = worldObject;
        m_world.WorldObjects.Insert(index, worldObject);
    }

    public void Apply(TickEvent tickEvent)
    {
        if (tickEvent.Add != null)
            return;

        var worldObject = m_bySlot[tickEvent.Body]!;

        if (tickEvent.Force is { } force)
            worldObject.RigidBody.Force += force;

        if (tickEvent.Rotation is { } rotation)
            worldObject.Rotation = rotation;

        if (tickEvent.MakeStatic is { } isStatic)
            worldObject.RigidBody.IsStatic = isStatic;

        if (tickEvent.Remove)
        {
            m_world.WorldObjects.Remove(worldObject);
            m_bySlot[tickEvent.Body] = null;
        }
    }

    private static CollisionData GetShape(string name, Action registerShape)
    {
        // Constructing one object runs the type's static constructor, which registers its shape once per process.
        registerShape();

        return GlobalCache<CollisionData>.Default.GetItemOrDefault(name) ??
               throw new InvalidOperationException($"The shape {name} is not in GlobalCache<CollisionData>.");
    }
}
