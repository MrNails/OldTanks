using System.Numerics;
using System.Runtime.CompilerServices;
using CoolEngine.GraphicalEngine.Core;
using CoolEngine.Mathematics;
using CoolEngine.PhysicEngine.Core;
using CoolEngine.PhysicEngine.Core.Collision;
using CoolEngine.Services;
using CoolEngine.Services.Interfaces;
using OldTanks.Infrastructure;
using OldTanks.Models;
using Face = CoolEngine.GraphicalEngine.Core.Face;

namespace CoolEngine.Physics.Benchmarks;

/// <summary>Today's code as the baseline (transitional until WP5): the default scene in a World, and today's shapes.</summary>
internal static class OldCode
{
    /// <summary>
    /// OldTanks.Windows.MainWindow.InitDefaultObjects without textures: two walls, a floor and a static sphere, then a
    /// dynamic cube and sphere with limits 2, 2 and 1; 5 collision iterations.
    /// </summary>
    public static World DefaultWorld()
    {
        EngineSettings.Current.CollisionIterations = 5;
        var cubeShape = CubeShape();
        _ = new Sphere();
        var sphereShape = GlobalCache<CollisionData>.Default.GetItemOrDefault(CollisionConstants.SphereCollisionName)!;

        var world = new World();
        Add(new Cube { Size = new Vector3(10, 2, 5), Position = new Vector3(0, 0, -10) }, cubeShape, true);
        Add(new Cube { Size = new Vector3(1, 3, 20), Position = new Vector3(9, 0, 0) }, cubeShape, true);
        Add(new Cube { Size = new Vector3(20, 1, 20), Position = new Vector3(0, -2, 0) }, cubeShape, true);
        Add(new Sphere { Size = new Vector3(1, 1, 1), Position = new Vector3(0, 5, 0) }, sphereShape, true);
        Add(new Cube
        {
            Size = new Vector3(1),
            Position = new Vector3(0, 2, 1.5f),
            Rotation = new Vector3(MathHelper.DegreesToRadians(45), 0, 0)
        }, cubeShape, false);
        Add(new Sphere { Size = new Vector3(1, 1, 1), Position = new Vector3(0, 5, 0) }, sphereShape, false);

        return world;

        void Add(WorldObject worldObject, CollisionData shape, bool isStatic)
        {
            worldObject.Collision = new Collision(worldObject, shape);
            if (isStatic)
            {
                worldObject.RigidBody.IsStatic = true;
            }
            else
            {
                worldObject.RigidBody.MaxSpeed = 2;
                worldObject.RigidBody.MaxBackSpeed = 2;
                worldObject.RigidBody.MaxSpeedMultiplier = 1;
            }

            world.WorldObjects.Add(worldObject);
        }
    }

    /// <summary>The game's cube shape, registered by Cube's static constructor.</summary>
    public static CollisionData CubeShape()
    {
        RegisterFakeScenes();
        _ = new Cube();

        return GlobalCache<CollisionData>.Default.GetItemOrDefault(CollisionConstants.CubeCollisionName)!;
    }

    // The scene names Cube's and Sphere's constructors read from the cache; the game's loader puts the real models there.
    private static void RegisterFakeScenes()
    {
        GlobalCache<Scene>.Default.AddOrUpdateItem("Cube_Cube.005",
            new Scene([new Mesh([Vector3.Zero, Vector3.One], Array.Empty<Face>())]));
        GlobalCache<Scene>.Default.AddOrUpdateItem("Sphere",
            new Scene([new Mesh([Vector3.Zero, Vector3.One], Array.Empty<Face>())]));
    }
}

/// <summary>A body for today's Collision with a transformation set from outside (as CoolEngine.Tests' helper).</summary>
internal sealed class OldBody : IPhysicObject
{
    private Vector3 m_position;
    private Vector3 m_size;

    public Vector3 Position
    {
        get => m_position;
        set => m_position = value;
    }

    public Vector3 Rotation { get; set; }

    public float X
    {
        get => m_position.X;
        set => m_position.X = value;
    }

    public float Y
    {
        get => m_position.Y;
        set => m_position.Y = value;
    }

    public float Z
    {
        get => m_position.Z;
        set => m_position.Z = value;
    }

    public Vector3 Size
    {
        get => m_size;
        set => m_size = value;
    }

    public float Width
    {
        get => m_size.X;
        set => m_size.X = value;
    }

    public float Height
    {
        get => m_size.Y;
        set => m_size.Y = value;
    }

    public float Length
    {
        get => m_size.Z;
        set => m_size.Z = value;
    }

    public Matrix4 Transformation { get; set; }

    public Collision? Collision { get; set; }

    public RigidBody RigidBody { get; } = new RigidBody();

    public bool NeedTransformationApply => false;

    public void ApplyTransformation() => Collision?.UpdateCollision();

    public static OldBody Create(CollisionData shape, Matrix4 transformation, Vector3 position, Vector3 size)
    {
        var body = new OldBody { Transformation = transformation, Position = position, Size = size };
        body.Collision = new Collision(body, shape);
        body.ApplyTransformation();

        return body;
    }
}

/// <summary>One checksum for both sides of a category, so the old and new loops compile the same consumer.</summary>
internal static class Checksum
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static uint Add(uint checksum, bool collides, Vector3 normal, float depth) =>
        (checksum * 31) ^ (collides ? 1u : 0u) ^ BitConverter.SingleToUInt32Bits(normal.X) ^
        BitConverter.SingleToUInt32Bits(normal.Y) ^ BitConverter.SingleToUInt32Bits(normal.Z) ^
        BitConverter.SingleToUInt32Bits(depth);
}
