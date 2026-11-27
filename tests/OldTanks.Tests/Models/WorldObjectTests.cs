using System.Numerics;
using CoolEngine.GraphicalEngine.Core;
using CoolEngine.Mathematics;
using CoolEngine.Services;
using OldTanks.Models;

namespace OldTanks.Tests.Models;

/// <summary>
/// Golden values of <see cref="WorldObject"/>'s transforms, recorded on today's code (OT-5, before the move onto
/// CoolEngine.Mathematics) and compared within <c>1e-5 × max(1, |expected|)</c> per element: the move may round
/// differently in the last bits, while a transposed or reordered product misses by far more. Since OT-14 (KI-002) the
/// Move and SetCameraData inputs are given in radians (the recorded degree values, converted); the expected values are
/// unchanged.
/// </summary>
[TestFixture]
public sealed class WorldObjectTests
{
    // The scene name Cube's constructor reads from the cache; the game's loader puts the real model there.
    private const string CubeSceneName = "Cube_Cube.005";

    private const float RelativeTolerance = 1e-5f;

    [SetUp]
    public void SetUp()
    {
        GlobalCache<Scene>.Default.AddOrUpdateItem(CubeSceneName,
            new Scene([new Mesh([Vector3.Zero, Vector3.One], Array.Empty<Face>())]));
    }

    [TearDown]
    public void TearDown()
    {
        GlobalCache<Scene>.Default.RemoveItem(CubeSceneName);
    }

    [Test]
    public void ApplyTransformation_FixedState_MatchesGolden()
    {
        var first = CreateCube(new Vector3(2, 4, 6), new Vector3(0.3f, -0.6f, 0.9f), new Vector3(1, 2, 3));
        var second = CreateCube(new Vector3(1, 1, 1), new Vector3(0, 1.2f, 0), new Vector3(-5, 0, 2.5f));
        var third = CreateCube(new Vector3(0.5f, 3, 1.5f), new Vector3(-1.1f, 0.25f, 2f), new Vector3(10, -4, -7));

        first.ApplyTransformation();
        second.ApplyTransformation();
        third.ApplyTransformation();

        // Recorded on 2026-11-26 at 3d22d58 with OpenTK.Mathematics, before the move onto CoolEngine.Mathematics.
        // 16 floats in memory order, the order GL receives them.
        float[] expectedFirst =
        [
            0.51303685f, 0.6465076f, -0.5646425f, 0f,
            -1.2892338f, 1.4491105f, 0.48780674f, 0f,
            1.7004001f, 0.71654016f, 2.3654196f, 0f,
            1f, 2f, 3f, 1f
        ];
        float[] expectedSecond =
        [
            0.18117885f, 0f, 0.46601954f, 0f,
            0f, 0.5f, 0f, 0f,
            -0.46601954f, 0f, 0.18117885f, 0f,
            -5f, 0f, 2.5f, 1f
        ];
        float[] expectedThird =
        [
            -0.10080246f, 0.22025739f, 0.06185099f, 0f,
            -0.7563138f, 0.017590195f, -1.2952528f, 0f,
            -0.57275397f, -0.35468686f, 0.32962117f, 0f,
            10f, -4f, -7f, 1f
        ];

        Assert.Multiple(() =>
        {
            AssertGolden(expectedFirst, Floats(first.Transformation), "first");
            AssertGolden(expectedSecond, Floats(second.Transformation), "second");
            AssertGolden(expectedThird, Floats(third.Transformation), "third");
        });
    }

    [Test]
    public void Move_FixedVelocityAndRotation_MatchesGolden()
    {
        // Only Rotation.Y turns the velocity, in radians since OT-14 (KI-002); the recorded degree inputs are
        // converted here, so the expected values are the recorded ones.
        var first = MoveOnce(new Vector3(0.3f, 0, -0.2f));
        var second = MoveOnce(new Vector3(0, MathHelper.DegreesToRadians(30), 0));
        var third = MoveOnce(new Vector3(0.1f, MathHelper.DegreesToRadians(-135), 0.4f));

        // Recorded on 2026-11-26 at 3d22d58 with OpenTK.Mathematics, before the move onto CoolEngine.Mathematics.
        float[] expectedFirst = [1.2820001f, 2.0019336f, 2.8f];
        float[] expectedSecond = [1.3442192f, 2.0019336f, 2.967795f];
        float[] expectedThird = [0.65917456f, 2.0019336f, 2.9420173f];

        Assert.Multiple(() =>
        {
            AssertGolden(expectedFirst, Floats(first), "first");
            AssertGolden(expectedSecond, Floats(second), "second");
            AssertGolden(expectedThird, Floats(third), "third");
        });
    }

    [Test]
    public void SetCameraData_FixedOffset_MatchesGolden()
    {
        // Only Rotation.Y turns the camera offset, in radians since OT-14 (KI-002); the recorded degree inputs are
        // converted here, so the expected values are the recorded ones.
        var first = CameraPositionAfterSettingPosition(new Vector3(2, 4, 6),
            new Vector3(0, MathHelper.DegreesToRadians(40), 0),
            new Vector3(-3, 2, 0.5f), new Vector2(25, 10), new Vector3(1, 2, 3));
        var second = CameraPositionAfterSettingPosition(new Vector3(1, 1, 1),
            new Vector3(0.2f, MathHelper.DegreesToRadians(-75), 0.1f),
            new Vector3(-1, 1, 0), new Vector2(-130, 35), new Vector3(-4, 0.5f, 9));
        var third = CameraPositionAfterSettingPosition(new Vector3(3, 0.5f, 2), new Vector3(0, 0, 0),
            new Vector3(2, -1, 4), new Vector2(90, -15), new Vector3(0.5f, -2, 1));

        // Recorded on 2026-11-26 at 3d22d58 with OpenTK.Mathematics, before the move onto CoolEngine.Mathematics.
        float[] expectedFirst = [-1.6477005f, 6.49056f, 2.808189f];
        float[] expectedSecond = [-4.1408563f, 2.3927286f, 8.798836f];
        float[] expectedThird = [4.5f, -2.1982877f, -1.190671f];

        Assert.Multiple(() =>
        {
            AssertGolden(expectedFirst, Floats(first), "first");
            AssertGolden(expectedSecond, Floats(second), "second");
            AssertGolden(expectedThird, Floats(third), "third");
        });
    }

    [TestCase(30f)]
    [TestCase(90f)]
    [TestCase(-135f)]
    public void Move_RotationAboutY_MovesAlongDrawnForwardAxis(float degrees)
    {
        var radians = MathHelper.DegreesToRadians(degrees);
        var cube = CreateCube(new Vector3(2, 2, 2), new Vector3(0, radians, 0), new Vector3(1, 2, 3));
        cube.RigidBody.MaxSpeed = 10;
        cube.RigidBody.MaxSpeedMultiplier = 1;
        cube.RigidBody.MaxBackSpeed = 10;
        cube.RigidBody.Velocity = new Vector3(2, 0, 0);
        cube.ApplyTransformation();
        // The drawn local X axis, projected onto the ground plane.
        var forward = Vector2.Normalize(new Vector2(cube.Transformation.Column0.X, cube.Transformation.Column0.Z));
        var start = cube.Position;

        cube.Move(0.1f);

        var delta = cube.Position - start;
        var moved = Vector2.Normalize(new Vector2(delta.X, delta.Z));
        Assert.Multiple(() =>
        {
            Assert.That(moved.X, Is.EqualTo(forward.X).Within(1e-5f), "moved X against the drawn forward X");
            Assert.That(moved.Y, Is.EqualTo(forward.Y).Within(1e-5f), "moved Z against the drawn forward Z");
            Assert.That(moved.X, Is.EqualTo(MathF.Cos(radians)).Within(1e-5f), "moved X against cos");
            Assert.That(moved.Y, Is.EqualTo(MathF.Sin(radians)).Within(1e-5f), "moved Z against sin");
        });
    }

    [Test]
    public void SetCameraData_QuarterTurnAboutY_PlacesCameraBehindDrawnFacing()
    {
        // Position + half the height + 1 up - the drawn forward (0, 0, 1) at a quarter turn.
        var cameraPosition = CameraPositionAfterSettingPosition(new Vector3(1, 1, 1),
            new Vector3(0, MathHelper.DegreesToRadians(90), 0), new Vector3(-1, 1, 0), new Vector2(0, 0),
            new Vector3(1, 2, 3));

        Assert.Multiple(() =>
        {
            Assert.That(cameraPosition.X, Is.EqualTo(1f).Within(1e-5f), "X");
            Assert.That(cameraPosition.Y, Is.EqualTo(3.5f).Within(1e-5f), "Y");
            Assert.That(cameraPosition.Z, Is.EqualTo(2f).Within(1e-5f), "Z");
        });
    }

    private static Cube CreateCube(Vector3 size, Vector3 rotation, Vector3 position) =>
        new Cube { Size = size, Rotation = rotation, Position = position };

    private static Vector3 MoveOnce(Vector3 rotation)
    {
        var cube = CreateCube(new Vector3(2, 2, 2), rotation, new Vector3(1, 2, 3));
        // The velocity setter clamps X to [-MaxBackSpeed, MaxSpeed × MaxSpeedMultiplier], all 0 by default.
        cube.RigidBody.MaxSpeed = 10;
        cube.RigidBody.MaxSpeedMultiplier = 1;
        cube.RigidBody.MaxBackSpeed = 10;
        cube.RigidBody.Velocity = new Vector3(3, 1, -2);

        cube.Move(0.1f);

        return cube.Position;
    }

    // Setting Position is the public path that calls the private SetCameraData; Rotation's setter does not call it.
    private static Vector3 CameraPositionAfterSettingPosition(Vector3 size, Vector3 rotation, Vector3 cameraOffset,
        Vector2 cameraOffsetAngle, Vector3 position)
    {
        var camera = new Camera();
        var cube = new Cube
        {
            Camera = camera,
            Size = size,
            Rotation = rotation,
            CameraOffset = cameraOffset,
            CameraOffsetAngle = cameraOffsetAngle
        };

        cube.Position = position;

        return camera.Position;
    }

    private static void AssertGolden(float[] expected, float[] actual, string what)
    {
        Assert.That(actual, Has.Length.EqualTo(expected.Length), $"{what}: element count");

        for (var i = 0; i < expected.Length; i++)
        {
            var tolerance = RelativeTolerance * MathF.Max(1f, MathF.Abs(expected[i]));
            Assert.That(actual[i], Is.EqualTo(expected[i]).Within(tolerance), $"{what}[{i}]");
        }
    }

    // The 16 floats in memory order, the order GL receives them: Column0 to Column3 (the bytes of Row0 to Row3 of the
    // OpenTK matrix the goldens were recorded from).
    private static float[] Floats(Matrix4 m) =>
    [
        m.Column0.X, m.Column0.Y, m.Column0.Z, m.Column0.W,
        m.Column1.X, m.Column1.Y, m.Column1.Z, m.Column1.W,
        m.Column2.X, m.Column2.Y, m.Column2.Z, m.Column2.W,
        m.Column3.X, m.Column3.Y, m.Column3.Z, m.Column3.W
    ];

    private static float[] Floats(Vector3 v) => [v.X, v.Y, v.Z];
}
