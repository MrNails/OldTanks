using System.Globalization;
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

    /// <summary>
    /// <paramref name="source"/> as OpenTK's <c>Vector3.ToString</c> wrote it before OT-5: <c>(x, y, z)</c>, the numbers
    /// in the current culture and separated by its list separator and a space. System.Numerics writes <c>&lt;x, y, z&gt;</c>
    /// with the number-group separator (U+00A0 in some cultures, which the game's fonts do not have).
    /// </summary>
    public static string ToDisplayString(this Vector3 source)
    {
        var separator = CultureInfo.CurrentCulture.TextInfo.ListSeparator;

        return $"({source.X}{separator} {source.Y}{separator} {source.Z})";
    }

    /// <summary><paramref name="source"/> as OpenTK's <c>Vector2.ToString</c> wrote it: <c>(x, y)</c>, as <see cref="ToDisplayString(Vector3)"/>.</summary>
    public static string ToDisplayString(this Vector2 source)
    {
        var separator = CultureInfo.CurrentCulture.TextInfo.ListSeparator;

        return $"({source.X}{separator} {source.Y})";
    }

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
