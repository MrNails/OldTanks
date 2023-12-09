using OpenTK.Mathematics;

namespace CoolEngine.Services;

public sealed class EngineSettings
{
    public static readonly int DefaultCharacterSize = 16;
    public static readonly int DefaultFontSize = 20;

    public static readonly int MaxInstanceCount = 100;
    
    private static EngineSettings? _current;
    
    private int m_collisionIterations;

    public static EngineSettings Current => _current ??= new EngineSettings();

    public int CollisionIterations
    {
        get => m_collisionIterations;
        set
        {
            if (value < 1 || value > 128)
                throw new ArgumentOutOfRangeException(nameof(value));

            m_collisionIterations = value;
        }
    }

    public readonly ReaderWriterLockSlim GlobalLock = new ReaderWriterLockSlim();

    public Matrix4 Projection { get; set; }
    public Matrix4 ScreenProjection { get; set; }
    public bool PhysicsEnable { get; set; }
    
    public float WindowWidth { get; set; }
    public float WindowHeight { get; set; }

    /// <summary>
    /// <para>Get y for ortho matrix for window.</para>
    /// <para>Use only when top argument equal WindowHeight and bottom argument equal 0 when you create ortho matrix</para>
    /// </summary>
    /// <param name="y">Input y</param>
    /// <returns>Actual y</returns>
    public float GetWindowY(float y)
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