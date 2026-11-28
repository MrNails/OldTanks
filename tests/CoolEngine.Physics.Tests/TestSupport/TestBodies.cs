using System.Numerics;
using CoolEngine.Mathematics;
using CoolEngine.Physics.Shapes;

namespace CoolEngine.Physics.Tests.TestSupport;

/// <summary>
/// Builds worlds and body descriptions the way the game will: today's numbers in the settings; the orientation from
/// today's Euler angles, the half-extents from today's size, and the velocity given in today's local terms turned into
/// world space with the heading. The world tests build bodies only here.
/// </summary>
internal static class TestBodies
{
    public const float TickTime = 1f / 60f;

    private const float GravityAcceleration = 9.80665f;

    private static readonly PolygonShape s_cube = TestShapes.Cube();
    private static readonly SphereShape s_sphere = new SphereShape();

    public static PolygonShape CubeShape => s_cube;

    public static SphereShape SphereShape => s_sphere;

    /// <summary>Today's terminal speed: the float product 9.80665f × 9, as PhysicsConstants.MaxFreeFallingSpeed.</summary>
    public static float TerminalSpeed => Multiply(GravityAcceleration, 9);

    public static PhysicsSettings Settings(int subStepCount = 5) =>
        new PhysicsSettings(new Vector3(0, -1, 0), GravityAcceleration, TerminalSpeed, subStepCount);

    public static PhysicsWorld World(int subStepCount = 5, int initialCapacity = 16) =>
        new PhysicsWorld(Settings(subStepCount), initialCapacity);

    /// <summary>A cube of today's <paramref name="size"/> and Euler <paramref name="rotation"/>; <paramref name="velocity"/> in today's local terms.</summary>
    public static BodyDescription Cube(Vector3 position, Vector3 size, Vector3 rotation = default, bool isStatic = false,
        Vector3 velocity = default, float mass = 1) =>
        Polygon(s_cube, position, size, rotation, isStatic, velocity, mass);

    public static BodyDescription Polygon(PolygonShape shape, Vector3 position, Vector3 size, Vector3 rotation = default,
        bool isStatic = false, Vector3 velocity = default, float mass = 1) =>
        new BodyDescription(shape, position, Orientation(rotation), size / 2)
        {
            Velocity = Frame(rotation.Y) * velocity,
            Mass = mass,
            IsStatic = isStatic
        };

    public static BodyDescription Sphere(Vector3 position, float diameter, bool isStatic = false,
        Vector3 velocity = default, float mass = 1) =>
        new BodyDescription(s_sphere, position, Orientation(Vector3.Zero), new Vector3(diameter) / 2)
        {
            Velocity = Frame(0) * velocity,
            Mass = mass,
            IsStatic = isStatic
        };

    public static BodyDescription Floor(Vector3 position, Vector3 size) => Cube(position, size, isStatic: true);

    /// <summary>
    /// The game's conversion of today's Euler angles (X yaw, Y pitch, Z roll, radians) to the orientation:
    /// <c>qZ(roll) × (qY(−pitch) × qX(yaw))</c>, today's rotation Z × rotation Y (−pitch) × rotation X.
    /// </summary>
    public static Quaternion Orientation(Vector3 rotation) =>
        Quaternion.CreateFromAxisAngle(Vector3.UnitZ, rotation.Z) *
        (Quaternion.CreateFromAxisAngle(Vector3.UnitY, -rotation.Y) *
         Quaternion.CreateFromAxisAngle(Vector3.UnitX, rotation.X));

    /// <summary>
    /// Today's <c>WorldObject.Move</c> frame for the heading (today's Rotation.Y): <c>Matrix3.CreateRotationY(heading).Transposed()</c>,
    /// which turns today's local velocity into world space; its Column0 is the forward axis.
    /// </summary>
    public static Matrix3 Frame(float heading) => Matrix3.CreateRotationY(heading).Transposed();

    /// <summary>
    /// Stage 1's reference (today's <c>WorldObject.ApplyTransformation</c> without the translation, with the Matrix4
    /// factories): rotation Z (roll) × rotation Y (−pitch) × rotation X (yaw) × scale (size / 2).
    /// </summary>
    public static Matrix3 StageOneLinear(Vector3 rotation, Vector3 size) =>
        new Matrix3(Matrix4.CreateRotationZ(rotation.Z) *
                    (Matrix4.CreateRotationY(-rotation.Y) *
                     (Matrix4.CreateRotationX(rotation.X) * Matrix4.CreateScale(size.X / 2, size.Y / 2, size.Z / 2))));

    // A method, so the product is computed at run time in float, as PhysicsConstants' static initializer does.
    private static float Multiply(float left, float right) => left * right;
}
