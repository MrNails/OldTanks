using System.Numerics;
using CoolEngine.GraphicalEngine.Core;
using CoolEngine.Mathematics;
using CoolEngine.PhysicEngine;
using CoolEngine.PhysicEngine.Core.Collision;
using CoolEngine.Services;
using OldTanks.Infrastructure;
using OldTanks.Models;
using GameSettings = OldTanks.Services.EngineSettings;

namespace OldTanks.Tests.TestSupport;

/// <summary>
/// The scenes of the simulation goldens, built headless: no texture, no <c>FillObject</c>, no camera FOV, none of which
/// the physics step reads. <see cref="CreateDefaultScene"/> mirrors <c>OldTanks.Windows.MainWindow.InitDefaultObjects</c>
/// (that window needs OpenGL), and the other scenes are what the debug window's edits and Spawn can produce, plus the drive and jump inputs of
/// <c>HandleObjectMove</c>, which the game cannot produce today because <c>World.Player</c> is never set (KI-001).
/// </summary>
internal static class SimulationScenes
{
    /// <summary>The scene name <see cref="Cube"/>'s constructor reads from the cache.</summary>
    public const string CubeSceneName = "Cube_Cube.005";

    /// <summary>The scene name <see cref="Sphere"/>'s constructor reads from the cache.</summary>
    public const string SphereSceneName = "Sphere";

    /// <summary>
    /// The factor <c>OldTanks.Windows.MainWindow.HandleObjectMove</c> multiplies <c>MovementDirectionUnit</c> by (W and
    /// S). Copied from that method.
    /// </summary>
    public const float DriveForce = 15f;

    /// <summary>Puts a fake scene under each of the two names, as <c>WorldObjectTests.SetUp</c> does for the cube.</summary>
    public static void AddFakeScenes()
    {
        foreach (var name in new[] { CubeSceneName, SphereSceneName })
        {
            GlobalCache<Scene>.Default.AddOrUpdateItem(name,
                new Scene([new Mesh([Vector3.Zero, Vector3.One], Array.Empty<CoolEngine.GraphicalEngine.Core.Face>())]));
        }
    }

    public static void RemoveFakeScenes()
    {
        GlobalCache<Scene>.Default.RemoveItem(CubeSceneName);
        GlobalCache<Scene>.Default.RemoveItem(SphereSceneName);
    }

    /// <summary>
    /// Mirror of <c>OldTanks.Windows.MainWindow.InitDefaultObjects</c>: the same objects, values and list order, without
    /// textures. Statics 0-3, dynamics 4-5.
    /// </summary>
    public static World CreateDefaultScene()
    {
        var world = new World();

        var statics = new List<WorldObject>();

        var wall = new Cube { Size = new Vector3(10, 2, 5), Position = new Vector3(0, 0, -10), Name = "Wall 1" };
        wall.Collision = CubeCollision(wall);
        statics.Add(wall);

        wall = new Cube
        {
            Size = new Vector3(1, 3, 20),
            Position = new Vector3(9, 0, 0),
            Rotation = new Vector3(),
            Name = "Wall 2"
        };
        wall.Collision = CubeCollision(wall);
        statics.Add(wall);

        var floor = new Cube { Size = new Vector3(20, 1, 20), Position = new Vector3(0, -2, 0), Name = "Floor 1" };
        floor.Collision = CubeCollision(floor);
        statics.Add(floor);

        var staticSphere = new Sphere { Size = new Vector3(1, 1, 1), Position = new Vector3(0, 5, 0) };
        staticSphere.Collision = SphereCollision(staticSphere);
        statics.Add(staticSphere);

        foreach (var body in statics)
        {
            body.RigidBody.IsStatic = true;
            body.RigidBody.Restitution = 0.5f;
            world.WorldObjects.Add(body);
        }

        var dynamics = new List<WorldObject>();

        var dynamicCube = new Cube
        {
            Size = new Vector3(1),
            Position = new Vector3(0, 2, 1.5f),
            Rotation = new Vector3(MathHelper.DegreesToRadians(45), 0, 0),
            Name = "Dynamic Cube 1"
        };
        dynamicCube.Collision = CubeCollision(dynamicCube);
        dynamics.Add(dynamicCube);

        var dynamicSphere = new Sphere { Size = new Vector3(1, 1, 1), Position = new Vector3(0, 5, 0) };
        dynamicSphere.Collision = SphereCollision(dynamicSphere);
        dynamics.Add(dynamicSphere);

        foreach (var body in dynamics)
        {
            ApplyDynamicDefaults(body);
            world.WorldObjects.Add(body);
        }

        return world;
    }

    /// <summary>The settings <c>InitDefaultObjects</c> gives its dynamic objects, in its order.</summary>
    public static void ApplyDynamicDefaults(WorldObject body)
    {
        body.RigidBody.MaxSpeed = 2;
        body.RigidBody.MaxBackSpeed = 2;
        body.RigidBody.MaxSpeedMultiplier = 1;
        body.RigidBody.Restitution = 0.4f;
        body.RigidBody.DefaultJumpForce = -250;
    }

    public static Cube CreateCube(Vector3 size, Vector3 position, Vector3 rotation, string? name, bool isStatic)
    {
        var cube = new Cube { Size = size, Position = position, Rotation = rotation };

        if (name != null)
            cube.Name = name;

        cube.Collision = CubeCollision(cube);

        if (isStatic)
        {
            cube.RigidBody.IsStatic = true;
            cube.RigidBody.Restitution = 0.5f;
        }
        else
        {
            ApplyDynamicDefaults(cube);
        }

        return cube;
    }

    public static Sphere CreateDynamicSphere(Vector3 position)
    {
        var sphere = new Sphere { Size = new Vector3(1), Position = position };
        sphere.Collision = SphereCollision(sphere);
        ApplyDynamicDefaults(sphere);

        return sphere;
    }

    /// <summary>
    /// A cube as <c>OldTanks.UI.ImGuiUI.MainWindow.SpawnObjectOnClick</c> builds it (static, size 1, no restitution),
    /// then moved by the debug window's Position edit.
    /// </summary>
    public static Cube CreateSpawnedCube(Vector3 position)
    {
        var cube = new Cube { Size = Vector3.One, RigidBody = { IsStatic = true } };
        cube.Collision = CubeCollision(cube);
        cube.Position = position;

        return cube;
    }

    /// <summary>Floor 1 and one dynamic body of size 1 at <paramref name="startHeight"/> falling at terminal speed.</summary>
    public static World CreateTerminalDropScene(bool sphere, float startHeight)
    {
        var world = new World();
        world.WorldObjects.Add(CreateCube(new Vector3(20, 1, 20), new Vector3(0, -2, 0), Vector3.Zero, "Floor 1", true));

        var position = new Vector3(0, startHeight, 0);
        WorldObject body = sphere
            ? CreateDynamicSphere(position)
            : CreateCube(new Vector3(1), position, Vector3.Zero, "Drop body", false);

        // The debug window's Velocity control.
        body.RigidBody.Velocity = new Vector3(0, -PhysicsConstants.MaxFreeFallingSpeed, 0);
        world.WorldObjects.Add(body);

        return world;
    }

    /// <summary>Floor 1 and a dynamic cube sent upward faster than the terminal speed.</summary>
    public static World CreateUpwardFlipScene()
    {
        var world = new World();
        world.WorldObjects.Add(CreateCube(new Vector3(20, 1, 20), new Vector3(0, -2, 0), Vector3.Zero, "Floor 1", true));

        var cube = CreateCube(new Vector3(1), new Vector3(0, 5, 0), Vector3.Zero, "Upward cube", false);
        // The debug window's Velocity control.
        cube.RigidBody.Velocity = new Vector3(0, 100, 0);
        world.WorldObjects.Add(cube);

        return world;
    }

    /// <summary>The default scene with the dynamic cube moved over the static sphere (the debug window's Position edit).</summary>
    public static World CreateCubeOnStaticSphereScene()
    {
        var world = CreateDefaultScene();
        world.WorldObjects[4].Position = new Vector3(0.2f, 7, 0);

        return world;
    }

    /// <summary>
    /// Floor 1, a dynamic sphere, a bottom cube and a middle cube, in that list order: the sphere is listed before the
    /// cube under it, and the bottom cube before the middle one.
    /// </summary>
    public static World CreateStackScene()
    {
        var world = new World();
        world.WorldObjects.Add(CreateCube(new Vector3(20, 1, 20), new Vector3(0, -2, 0), Vector3.Zero, "Floor 1", true));
        world.WorldObjects.Add(CreateDynamicSphere(new Vector3(0, 1.2f, 0)));
        world.WorldObjects.Add(CreateCube(new Vector3(1), new Vector3(0, -0.9f, 0), Vector3.Zero, "Bottom cube", false));
        world.WorldObjects.Add(CreateCube(new Vector3(1), new Vector3(0, 0.15f, 0), Vector3.Zero, "Middle cube", false));

        return world;
    }

    /// <summary>
    /// Dynamic bodies listed before spawned static cubes (Spawn appends statics): a cube over a platform and under a
    /// ceiling 0.2 m above it, a sphere over a second platform, and a write to a static body's velocity.
    /// </summary>
    public static World CreateSpawnedStaticsScene()
    {
        var world = new World();
        world.WorldObjects.Add(CreateCube(new Vector3(1), new Vector3(0, -0.9f, 0), Vector3.Zero, "Jumping cube", false));
        world.WorldObjects.Add(CreateDynamicSphere(new Vector3(3, -0.9f, 0)));
        world.WorldObjects.Add(CreateSpawnedCube(new Vector3(0, -2, 0)));
        world.WorldObjects.Add(CreateSpawnedCube(new Vector3(3, -2, 0)));
        world.WorldObjects.Add(CreateSpawnedCube(new Vector3(0, 0.2f, 0)));

        // A debug-window write to a static body, which the Velocity setter ignores.
        world.WorldObjects[2].RigidBody.Velocity = new Vector3(1, 1, 1);

        return world;
    }

    /// <summary>Floor 1 and a dynamic cube resting on it, for a debug-window Velocity write to lift it off the ground.</summary>
    public static World CreateRestingCubeScene()
    {
        var world = new World();
        world.WorldObjects.Add(CreateCube(new Vector3(20, 1, 20), new Vector3(0, -2, 0), Vector3.Zero, "Floor 1", true));
        world.WorldObjects.Add(CreateCube(new Vector3(1), new Vector3(0, -0.9f, 0), Vector3.Zero, "Hopping cube", false));

        return world;
    }

    /// <summary>Two spawned static cubes that overlap: the step skips a static pair.</summary>
    public static World CreateOverlappingStaticsScene()
    {
        var world = new World();
        world.WorldObjects.Add(CreateSpawnedCube(Vector3.Zero));
        world.WorldObjects.Add(CreateSpawnedCube(new Vector3(0.5f, 0, 0)));

        return world;
    }

    /// <summary>Floor 1 and a dynamic cube at a heading of 30 degrees (any heading that is not a multiple of 90).</summary>
    public static World CreateDriveScene()
    {
        var world = new World();
        world.WorldObjects.Add(CreateCube(new Vector3(20, 1, 20), new Vector3(0, -2, 0), Vector3.Zero, "Floor 1", true));
        world.WorldObjects.Add(CreateCube(new Vector3(1), new Vector3(0, -1, 0),
            new Vector3(0, MathHelper.DegreesToRadians(30), 0), "Driven cube", false));

        return world;
    }

    /// <summary>
    /// The W (<paramref name="sign"/> 1) and S (-1) branches of <c>OldTanks.Windows.MainWindow.HandleObjectMove</c>,
    /// copied from it.
    /// </summary>
    public static void ApplyDrive(WorldObject body, float sign)
    {
        body.RigidBody.Force += new GameSettings().MovementDirectionUnit * DriveForce * sign;
    }

    /// <summary>The Space branch of <c>OldTanks.Windows.MainWindow.HandleObjectMove</c>, copied from it.</summary>
    public static void ApplyJump(WorldObject body)
    {
        body.RigidBody.Force += PhysicsConstants.GravityDirection * body.RigidBody.DefaultJumpForce;
        body.RigidBody.OnGround = false;
    }

    // The shapes are registered by the static constructors of Cube and Sphere, so they exist once an instance was made.
    private static Collision CubeCollision(WorldObject body) =>
        new(body, GlobalCache<CollisionData>.Default.GetItemOrDefault(CollisionConstants.CubeCollisionName)!);

    private static Collision SphereCollision(WorldObject body) =>
        new(body, GlobalCache<CollisionData>.Default.GetItemOrDefault(CollisionConstants.SphereCollisionName)!);
}
