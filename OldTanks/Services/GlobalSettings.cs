using CoolEngine.Core;
using OpenTK.Mathematics;

namespace OldTanks.Services;

public static class GlobalSettings
{
    public static Settings UserSettings { get; } = new Settings();
    public static int MaxDepthLength { get; set; } = 2000;

}