using System.Runtime.CompilerServices;
using Matrix4 = CoolEngine.Mathematics.Matrix4;

namespace CoolEngine.GraphicalEngine.Services;

/// <summary>
/// Today's projections (standard Z, clip depth [-1, 1]) as <see cref="Matrix4"/>: OpenTK's factories, their bytes
/// reinterpreted (OpenTK's row-vector matrix has the memory of our column-vector matrix of the same transform).
/// Temporary for OT-5: it goes in M7, when the renderer switches to reversed Z and the callers use
/// <see cref="Matrix4.CreatePerspectiveFieldOfView"/> and <see cref="Matrix4.CreateOrthographicOffCenter"/>.
/// </summary>
public static class LegacyProjections
{
    /// <summary>OpenTK's <c>Matrix4.CreatePerspectiveFieldOfView</c> (it throws as OpenTK does).</summary>
    public static Matrix4 CreatePerspectiveFieldOfView(float fovy, float aspect, float depthNear, float depthFar)
    {
        return Unsafe.BitCast<OpenTK.Mathematics.Matrix4, Matrix4>(
            OpenTK.Mathematics.Matrix4.CreatePerspectiveFieldOfView(fovy, aspect, depthNear, depthFar));
    }

    /// <summary>OpenTK's <c>Matrix4.CreateOrthographicOffCenter</c>.</summary>
    public static Matrix4 CreateOrthographicOffCenter(float left, float right, float bottom, float top, float depthNear,
        float depthFar)
    {
        return Unsafe.BitCast<OpenTK.Mathematics.Matrix4, Matrix4>(
            OpenTK.Mathematics.Matrix4.CreateOrthographicOffCenter(left, right, bottom, top, depthNear, depthFar));
    }
}
