using System.Numerics;
using System.Runtime.CompilerServices;

namespace CoolEngine.Services.Extensions;

public static class VectorExtensions
{
    // Identities since OT-5 (engine and ImGui both use System.Numerics): kept only because the debug window's binding
    // attributes (OldTanks.UI.ImGuiUI.MainWindow) name them as converters.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector3 ToGLVector3(this Vector3 source) => source;
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector3 ToSystemVector3(this Vector3 source) => source;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector2 ToGLVector2(this Vector2 source) => source;
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector2 ToSystemVector2(this Vector2 source) => source;

    public static Vector3 ReplaceComponent(this Vector3 source, in Vector3 componentToReplace)
    {
        var local = source;

        if (componentToReplace.X != 0) local.X = componentToReplace.X;
        if (componentToReplace.Y != 0) local.Y = componentToReplace.Y;
        if (componentToReplace.Z != 0) local.Z = componentToReplace.Z;

        return local;
    }

    public static bool GreaterOrEqualThan(this Vector3 source, in Vector3 target)
    {
        return source.X >= target.X &&
               source.Y >= target.Y &&
               source.Z >= target.Z;
    }

    public static bool LowerOrEqualThan(this Vector3 source, in Vector3 target)
    {
        return source.X <= target.X &&
               source.Y <= target.Y &&
               source.Z <= target.Z;
    }
}
