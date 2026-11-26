using CoolEngine.GraphicalEngine.Core;
using CoolEngine.Tests.TestSupport;
using OpenTK.Mathematics;
using OpenTK.Windowing.GraphicsLibraryFramework;

namespace CoolEngine.Tests.GraphicalEngine.Core;

[TestFixture]
public sealed class CameraTests
{
    [Test]
    public void Rotate_SequenceOfDeltas_MatchesGolden()
    {
        var camera = new Camera(new Vector3(1, 2, 3), new Vector3(0.3f, -0.2f, -1f), Vector3.UnitY);

        // The third delta drives the pitch from 17 to -83, past the -80 clamp, so the clamp is part of the golden.
        camera.Rotate(10, 5);
        camera.Rotate(-25, 12);
        camera.Rotate(40, -100);
        camera.Rotate(3, 30);
        camera.Move(0.1f, CreateKeyboardState(Keys.W, Keys.D));
        camera.Move(0.25f, CreateKeyboardState(Keys.Space, Keys.LeftControl));
        camera.Rotate(-7.5f, -4);

        // Recorded on 2026-11-26 at 3d22d58 with OpenTK.Mathematics, before the move onto CoolEngine.Mathematics.
        // LookAt: 16 floats in memory order, the order GL receives them.
        float[] expectedRotation = [0.4410476f, 0.6839807f, -0.5810743f];
        float[] expectedPosition = [1.6281192f, 6.065714f, 3.0760999f];
        float[] expectedLookAt =
        [
            0.7965377f, -0.41352725f, -0.4410476f, 0f,
            -0f, 0.7295f, -0.6839809f, 0f,
            0.604589f, 0.5448165f, 0.5810742f, 0f,
            -3.1566343f, -5.4275765f, 3.0794683f, 1f
        ];

        Assert.Multiple(() =>
        {
            GoldenAssert.AreClose(expectedRotation, GoldenAssert.Floats(camera.Rotation), "Rotation");
            GoldenAssert.AreClose(expectedPosition, GoldenAssert.Floats(camera.Position), "Position");
            GoldenAssert.AreClose(expectedLookAt, GoldenAssert.Floats(camera.LookAt), "LookAt");
        });
    }

    // KeyboardState's constructor and SetKeyState are internal to OpenTK (the window fills it from GLFW).
    private static KeyboardState CreateKeyboardState(params Keys[] pressed)
    {
        var state = (KeyboardState)Activator.CreateInstance(typeof(KeyboardState), nonPublic: true)!;
        var setKeyState = typeof(KeyboardState).GetMethod("SetKeyState",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;

        foreach (var key in pressed)
            setKeyState.Invoke(state, [key, true]);

        return state;
    }
}
