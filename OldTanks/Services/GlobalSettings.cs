using OpenTK.Mathematics;

namespace OldTanks.Services;

public static class GlobalSettings
{
    public static Settings UserSettings { get; } = new Settings();
    public static int MaxDepthLength { get; set; } = 2000;

    public static Vector3 MovementDirectionUnit { get; set; } = Vector3.UnitX;
}