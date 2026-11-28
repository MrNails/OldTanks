using System.Numerics;
using CoolEngine.Mathematics;

namespace CoolEngine.Physics.Tests.TestSupport;

/// <summary>One body of a scene with today's forward limits (MaxSpeed, MaxBackSpeed, MaxSpeedMultiplier).</summary>
internal sealed record SceneBody(BodyDescription Description, float MaxSpeed, float MaxBackSpeed, float Multiplier);

/// <summary>Scenes for the library alone: the game's default scene and seeded N-body scenes.</summary>
internal static class Scenes
{
    /// <summary>OldTanks.Windows.MainWindow.InitDefaultObjects: two walls, a floor, a static sphere, a cube and a sphere.</summary>
    public static SceneBody[] Default() =>
    [
        new SceneBody(TestBodies.Cube(new Vector3(0, 0, -10), new Vector3(10, 2, 5), isStatic: true), 0, 0, 0),
        new SceneBody(TestBodies.Cube(new Vector3(9, 0, 0), new Vector3(1, 3, 20), isStatic: true), 0, 0, 0),
        new SceneBody(TestBodies.Floor(new Vector3(0, -2, 0), new Vector3(20, 1, 20)), 0, 0, 0),
        new SceneBody(TestBodies.Sphere(new Vector3(0, 5, 0), 1, isStatic: true), 0, 0, 0),
        new SceneBody(TestBodies.Cube(new Vector3(0, 2, 1.5f), new Vector3(1),
            new Vector3(MathHelper.DegreesToRadians(45), 0, 0)), 2, 2, 1),
        new SceneBody(TestBodies.Sphere(new Vector3(0, 5, 0), 1), 2, 2, 1),
    ];

    /// <summary>
    /// A static floor and <paramref name="count"/> − 1 bodies: a quarter spheres, a fifth static, random sizes, poses and
    /// velocities inside the limits, spread over an area that grows with the count.
    /// </summary>
    public static SceneBody[] Seeded(int seed, int count)
    {
        var random = new Random(seed);
        var spread = 4 + MathF.Sqrt(count);
        var bodies = new SceneBody[count];
        bodies[0] = new SceneBody(TestBodies.Floor(new Vector3(0, -2, 0), new Vector3(4 * spread, 1, 4 * spread)), 0, 0, 0);

        for (var i = 1; i < count; i++)
        {
            var isSphere = random.Next(4) == 0;
            var isStatic = random.Next(5) == 0;
            var maxSpeed = Next(random, 0.5f, 5f);
            var maxBackSpeed = Next(random, 0.5f, 5f);
            var multiplier = random.Next(2) == 0 ? 1f : 3f;
            var position = new Vector3(Next(random, -spread, spread), Next(random, -1, 8 + count / 10f),
                Next(random, -spread, spread));
            var velocity = new Vector3(Math.Clamp(Next(random, -6, 6), -maxBackSpeed, maxSpeed * multiplier),
                Next(random, -5, 5), Next(random, -3, 3));
            var mass = Next(random, 0.5f, 5f);

            var description = isSphere
                ? TestBodies.Sphere(position, Next(random, 0.3f, 2f), isStatic, velocity, mass)
                : TestBodies.Cube(position,
                    new Vector3(Next(random, 0.3f, 3f), Next(random, 0.3f, 3f), Next(random, 0.3f, 3f)),
                    new Vector3(Next(random, -3.2f, 3.2f), Next(random, -3.2f, 3.2f), Next(random, -3.2f, 3.2f)),
                    isStatic, velocity, mass);

            bodies[i] = new SceneBody(description, maxSpeed, maxBackSpeed, multiplier);
        }

        return bodies;
    }

    /// <summary>Adds every body to <paramref name="world"/> in order and gives the drive its limits.</summary>
    public static BodyHandle[] Build(PhysicsWorld world, TestDrive drive, SceneBody[] bodies)
    {
        var handles = new BodyHandle[bodies.Length];

        for (var i = 0; i < bodies.Length; i++)
        {
            handles[i] = world.Add(bodies[i].Description);
            drive.SetLimits(handles[i], bodies[i].MaxSpeed, bodies[i].MaxBackSpeed, bodies[i].Multiplier);
        }

        return handles;
    }

    private static float Next(Random random, float low, float high) => (float)(low + random.NextDouble() * (high - low));
}
