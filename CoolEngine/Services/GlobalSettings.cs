using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;

namespace CoolEngine.Services;

public static class GlobalSettings
{
    public static readonly int DefaultCharacterSize = 16;
    public static readonly int DefaultFontSize = 20;

    public static readonly int MaxInstanceCount = 100;

    private static int s_collisionIterations;

    public static int CollisionIterations
    {
        get => s_collisionIterations;
        set
        {
            if (value < 1 || value > 128)
                throw new ArgumentOutOfRangeException(nameof(value));

            s_collisionIterations = value;
        }
    }

    public static readonly ReaderWriterLockSlim GlobalLock = new ReaderWriterLockSlim();

    public static Matrix4 Projection;
    public static Matrix4 ScreenProjection;
    public static bool PhysicsEnable;
    
    public static float WindowWidth { get; set; }
    public static float WindowHeight { get; set; }

    /// <summary>
    /// <para>Get y for ortho matrix for window.</para>
    /// <para>Use only if when top argument equal WindowHeight and bottom argument equal 0 when you create ortho matrix</para>
    /// </summary>
    /// <param name="y">You y</param>
    /// <returns>Actual y</returns>
    public static float GetWindowY(float y)
    {
        return WindowHeight - y;
    }

    // public static Vector3 ScreenToWorldCoord(in Matrix4 cameraView, in Vector2 screenCoord)
    // {
    //     
    //     var worldProjection = (cameraView * Projection).Inverted();
    //     
    //     return new Vector3();
    // }
}