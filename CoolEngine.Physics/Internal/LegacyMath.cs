using System.Numerics;

namespace CoolEngine.Physics.Internal;

/// <summary>
/// Verbatim copies of the helpers today's physics calls in other projects, so the library needs no reference to them:
/// CoolEngine.Services.Extensions.VectorExtensions.ReplaceComponent, ArrayExtensions.ClosestPointIndex,
/// Common.Extensions.PrimitiveExtensions.ApproximateEqual with its default epsilon, and
/// Common.Helpers.MathHelper.QuadraticEquation.
/// </summary>
internal static class LegacyMath
{
    /// <summary>Each nonzero component of <paramref name="replacement"/> replaces the same component of <paramref name="source"/>.</summary>
    public static Vector3 ReplaceComponent(Vector3 source, in Vector3 replacement)
    {
        var local = source;

        if (replacement.X != 0) local.X = replacement.X;
        if (replacement.Y != 0) local.Y = replacement.Y;
        if (replacement.Z != 0) local.Z = replacement.Z;

        return local;
    }

    /// <summary>The index of the vertex nearest <paramref name="to"/> (the first on a tie), or −1 when none is nearer than float.MaxValue.</summary>
    public static int ClosestPointIndex(ReadOnlySpan<Vector3> vertices, in Vector3 to)
    {
        var length = float.MaxValue;
        var idx = -1;

        for (int i = 0; i < vertices.Length; i++)
        {
            var tmpLength = Vector3.Distance(vertices[i], to);
            if (length > tmpLength)
            {
                length = tmpLength;
                idx = i;
            }
        }

        return idx;
    }

    /// <summary><c>Math.Abs(left − right) &lt; float.Epsilon</c>: an exact comparison (ApproximateEqual's default epsilon).</summary>
    public static bool ExactlyEqual(float left, float right)
    {
        return Math.Abs(left - right) < float.Epsilon;
    }

    /// <summary>The real roots of <c>a·x² + b·x + c</c>, smaller first; false when there are none.</summary>
    public static bool QuadraticEquation(float a, float b, float c, out float x1, out float x2)
    {
        x1 = float.NaN;
        x2 = float.NaN;
        var discr = b * b - 4 * a * c;

        if (discr < 0)
            return false;

        if (discr == 0)
        {
            x1 = x2 = -0.5f * b / a;
            return true;
        }

        var q = (float)(b > 0
            ? -0.5f * (b + Math.Sqrt(discr))
            : -0.5f * (b - Math.Sqrt(discr)));

        x1 = q / a;
        x2 = c / q;

        if (x1 > x2)
            (x1, x2) = (x2, x1);

        return true;
    }
}
