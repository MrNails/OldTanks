using OpenTK.Mathematics;

namespace GraphicalEngine.Services;

public static class GlobalSettings
{
    public static readonly int DefaultCharacterSize = 16;
    public static readonly int DefaultFontSize = 20;

    public static Matrix4 Projection { get; set; }
    public static Matrix4 ScreenProjection { get; set; }
    
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
}