using System.Numerics;

namespace CoolEngine.Services.Misc;

public static class QuaternionHelper
{
    /// <summary>
    /// The rotation by <paramref name="angle"/> radians about <paramref name="axis"/>, computed exactly as OpenTK 4.9.4's
    /// <c>Quaternion.FromAxisAngle</c> does (the same bits): an axis with a zero squared length gives
    /// <see cref="Quaternion.Identity"/>; otherwise the axis is normalized (times <c>1 / length</c>), the quaternion is
    /// <c>(axis · sin(angle / 2), cos(angle / 2))</c>, and the result is normalized again. System.Numerics'
    /// <see cref="Quaternion.CreateFromAxisAngle"/> does neither: it expects a unit axis, and the camera passes a non-unit
    /// one. Calls sin and cos, so the result follows the OS math library.
    /// </summary>
    public static Quaternion FromAxisAngle(Vector3 axis, float angle)
    {
        var lengthSquared = axis.X * axis.X + axis.Y * axis.Y + axis.Z * axis.Z;
        if (lengthSquared == 0f)
            return Quaternion.Identity;

        angle *= 0.5f;

        var scale = 1f / MathF.Sqrt(lengthSquared);
        var sin = MathF.Sin(angle);

        var x = axis.X * scale * sin;
        var y = axis.Y * scale * sin;
        var z = axis.Z * scale * sin;
        var w = MathF.Cos(angle);

        var inverseLength = 1f / MathF.Sqrt(w * w + (x * x + y * y + z * z));

        return new Quaternion(x * inverseLength, y * inverseLength, z * inverseLength, w * inverseLength);
    }
}
