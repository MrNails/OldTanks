using System.Numerics;
using System.Runtime.Intrinsics;
using Numerics = System.Numerics;

namespace CoolEngine.Mathematics.Tests.TestSupport;

/// <summary>
/// Seeded random inputs for the oracle tests, which loop over <see cref="Count"/> inputs inside one test.
/// A test takes its own generator from <see cref="CreateRandom"/>, so every run and every test sees the same sequence
/// (a seeded <see cref="Random"/> is stable across runs and .NET versions). The ranges are the plan's: components and
/// translations in [-100, 100], angles in [-2π, 2π], scale factors in [0.5, 2], uniformly distributed unit quaternions.
/// The vector and quaternion generators return System.Numerics' types, which the library uses as its float vectors and
/// quaternion; the matrix generators return the library's matrices.
/// </summary>
internal static class Inputs
{
    /// <summary>The number of inputs every oracle test loops over.</summary>
    public const int Count = 256;

    /// <summary>The seed of every oracle test unless it needs a second, independent sequence.</summary>
    public const int Seed = 20261125;

    private const double ComponentLimit = 100.0;
    private const double AngleLimit = 2.0 * Math.PI;
    private const double MinScaleFactor = 0.5;
    private const double MaxScaleFactor = 2.0;

    public static Random CreateRandom(int seed = Seed)
    {
        return new Random(seed);
    }

    /// <summary>The context string that names input <paramref name="index"/> in a failure message.</summary>
    public static string Label(int index)
    {
        return $"input {index}";
    }

    /// <summary>A vector component or a translation in [-100, 100].</summary>
    public static float NextComponent(this Random random)
    {
        return (float)random.NextComponentD();
    }

    /// <inheritdoc cref="NextComponent"/>
    public static double NextComponentD(this Random random)
    {
        return NextInRange(random, -ComponentLimit, ComponentLimit);
    }

    /// <summary>A vector with every component in [-100, 100].</summary>
    public static Vector2 NextVector2(this Random random)
    {
        return new Vector2(random.NextComponent(), random.NextComponent());
    }

    /// <inheritdoc cref="NextVector2"/>
    public static Vector3 NextVector3(this Random random)
    {
        return new Vector3(random.NextComponent(), random.NextComponent(), random.NextComponent());
    }

    /// <inheritdoc cref="NextVector2"/>
    public static Vector4 NextVector4(this Random random)
    {
        return new Vector4(random.NextComponent(), random.NextComponent(), random.NextComponent(), random.NextComponent());
    }

    /// <summary>A unit quaternion, uniform over all rotations (<see cref="NextUnitQuaternion"/>).</summary>
    public static Quaternion NextRotation(this Random random)
    {
        var (x, y, z, w) = random.NextUnitQuaternion();
        return new Quaternion(x, y, z, w);
    }

    /// <summary>A matrix with every element in [-100, 100].</summary>
    public static Matrix2 NextMatrix2(this Random random)
    {
        return Matrix2.FromColumns(random.NextVector2(), random.NextVector2());
    }

    /// <inheritdoc cref="NextMatrix2"/>
    public static Matrix3 NextMatrix3(this Random random)
    {
        return Matrix3.FromColumns(random.NextVector3(), random.NextVector3(), random.NextVector3());
    }

    /// <inheritdoc cref="NextMatrix2"/>
    public static Matrix4 NextMatrix4(this Random random)
    {
        return Matrix4.FromColumns(random.NextVector4(), random.NextVector4(), random.NextVector4(), random.NextVector4());
    }

    /// <summary>
    /// A TRS matrix <c>T * R * S</c>: scale per axis in [0.5, 2] first, then a uniformly random rotation, then a
    /// translation in [-100, 100]. Built with System.Numerics (row vectors, <c>S * R * T</c>) and moved over byte for
    /// byte, so the input does not depend on the library's own factories.
    /// </summary>
    public static Matrix4 NextTrs(this Random random)
    {
        return random.NextTrs(out _);
    }

    /// <inheritdoc cref="NextTrs(Random)"/>
    /// <param name="random">The generator.</param>
    /// <param name="scaleFactors">The per-axis scale factors of the S part, which are the lengths of the first three columns.</param>
    public static Matrix4 NextTrs(this Random random, out Vector3 scaleFactors)
    {
        var (x, y, z, w) = random.NextUnitQuaternion();
        scaleFactors = new Vector3(random.NextScaleFactor(), random.NextScaleFactor(), random.NextScaleFactor());
        var scale = Numerics.Matrix4x4.CreateScale(scaleFactors);
        var rotation = Numerics.Matrix4x4.CreateFromQuaternion(new Quaternion(x, y, z, w));
        var translation = Numerics.Matrix4x4.CreateTranslation(random.NextComponent(), random.NextComponent(), random.NextComponent());

        return Oracle.FromNumerics(scale * rotation * translation);
    }

    /// <summary>The largest 1-norm condition number a <c>NextWellConditionedMatrix</c> input may have.</summary>
    public const double MaxConditionNumber = 1e3;

    /// <summary>
    /// <see cref="NextMatrix2"/>, drawn again until its 1-norm condition number <c>‖M‖₁ ‖M⁻¹‖₁</c> is at most
    /// <see cref="MaxConditionNumber"/>: an inverse compared with an oracle's must not depend on how two algorithms round
    /// an ill-conditioned problem.
    /// </summary>
    public static Matrix2 NextWellConditionedMatrix2(this Random random)
    {
        while (true)
        {
            var matrix = random.NextMatrix2();
            if (ConditionNumber1(MathAssert.Components(matrix), 2) <= MaxConditionNumber)
                return matrix;
        }
    }

    /// <inheritdoc cref="NextWellConditionedMatrix2"/>
    public static Matrix3 NextWellConditionedMatrix3(this Random random)
    {
        while (true)
        {
            var matrix = random.NextMatrix3();
            if (ConditionNumber1(MathAssert.Components(matrix), 3) <= MaxConditionNumber)
                return matrix;
        }
    }

    /// <inheritdoc cref="NextWellConditionedMatrix2"/>
    public static Matrix4 NextWellConditionedMatrix4(this Random random)
    {
        while (true)
        {
            var matrix = random.NextMatrix4();
            if (ConditionNumber1(MathAssert.Components(matrix), 4) <= MaxConditionNumber)
                return matrix;
        }
    }

    /// <summary>
    /// <c>κ₁ = ‖M‖₁ ‖M⁻¹‖₁</c> (the 1-norm is the largest column sum of absolute values) of an n × n matrix given
    /// column-major, in double, with the inverse from Gauss-Jordan elimination with partial pivoting; infinity when the
    /// matrix is singular in double.
    /// </summary>
    public static double ConditionNumber1(ReadOnlySpan<float> columnMajor, int n)
    {
        // a is n × 2n row-major: [M | I], reduced to [I | M⁻¹].
        var a = new double[n, 2 * n];
        for (var row = 0; row < n; row++)
        {
            for (var column = 0; column < n; column++)
                a[row, column] = columnMajor[column * n + row];

            a[row, n + row] = 1.0;
        }

        for (var pivotColumn = 0; pivotColumn < n; pivotColumn++)
        {
            var pivotRow = pivotColumn;
            for (var row = pivotColumn + 1; row < n; row++)
            {
                if (Math.Abs(a[row, pivotColumn]) > Math.Abs(a[pivotRow, pivotColumn]))
                    pivotRow = row;
            }

            if (a[pivotRow, pivotColumn] == 0.0)
                return double.PositiveInfinity;

            for (var column = 0; column < 2 * n; column++)
                (a[pivotColumn, column], a[pivotRow, column]) = (a[pivotRow, column], a[pivotColumn, column]);

            var pivot = a[pivotColumn, pivotColumn];
            for (var column = 0; column < 2 * n; column++)
                a[pivotColumn, column] /= pivot;

            for (var row = 0; row < n; row++)
            {
                if (row == pivotColumn)
                    continue;

                var factor = a[row, pivotColumn];
                for (var column = 0; column < 2 * n; column++)
                    a[row, column] -= factor * a[pivotColumn, column];
            }
        }

        var norm = 0.0;
        var inverseNorm = 0.0;
        for (var column = 0; column < n; column++)
        {
            var sum = 0.0;
            var inverseSum = 0.0;
            for (var row = 0; row < n; row++)
            {
                sum += Math.Abs(columnMajor[column * n + row]);
                inverseSum += Math.Abs(a[row, n + column]);
            }

            norm = Math.Max(norm, sum);
            inverseNorm = Math.Max(inverseNorm, inverseSum);
        }

        return norm * inverseNorm;
    }

    /// <summary>The upper-left 3 × 3 of <see cref="NextTrs(Random)"/>: a random rotation times a per-axis scale.</summary>
    public static Matrix3 NextRotationScale3(this Random random)
    {
        var trs = random.NextTrs();
        return Matrix3.FromColumns(trs.Column0.AsVector128().AsVector3(), trs.Column1.AsVector128().AsVector3(),
            trs.Column2.AsVector128().AsVector3());
    }

    /// <summary>A 2D rotation by a random angle times a per-axis scale in [0.5, 2]: <c>R * S</c>.</summary>
    public static Matrix2 NextRotationScale2(this Random random)
    {
        var (sin, cos) = MathF.SinCos(random.NextAngle());
        var scaleX = random.NextScaleFactor();
        var scaleY = random.NextScaleFactor();

        // Column j of R * S is column j of R times scale j.
        return Matrix2.FromColumns(new Vector2(cos, sin) * scaleX, new Vector2(-sin, cos) * scaleY);
    }

    /// <summary>An angle in radians in [-2π, 2π].</summary>
    public static float NextAngle(this Random random)
    {
        return (float)random.NextAngleD();
    }

    /// <inheritdoc cref="NextAngle"/>
    public static double NextAngleD(this Random random)
    {
        return NextInRange(random, -AngleLimit, AngleLimit);
    }

    /// <summary>A per-axis scale factor of a TRS matrix in [0.5, 2].</summary>
    public static float NextScaleFactor(this Random random)
    {
        return (float)random.NextScaleFactorD();
    }

    /// <inheritdoc cref="NextScaleFactor"/>
    public static double NextScaleFactorD(this Random random)
    {
        return NextInRange(random, MinScaleFactor, MaxScaleFactor);
    }

    /// <summary>
    /// The components of a unit quaternion (vector part, then scalar), uniform over all rotations; computed in double
    /// and rounded to float, so its length is 1 within a few float ulps.
    /// </summary>
    public static (float X, float Y, float Z, float W) NextUnitQuaternion(this Random random)
    {
        var (x, y, z, w) = random.NextUnitQuaternionD();
        return ((float)x, (float)y, (float)z, (float)w);
    }

    /// <summary>The components of a unit quaternion (vector part, then scalar), uniform over all rotations.</summary>
    public static (double X, double Y, double Z, double W) NextUnitQuaternionD(this Random random)
    {
        // Shoemake, "Uniform random rotations" (Graphics Gems III): uniform on the unit 3-sphere.
        var u1 = random.NextDouble();
        var u2 = random.NextDouble();
        var u3 = random.NextDouble();
        var a = Math.Sqrt(1.0 - u1);
        var b = Math.Sqrt(u1);

        return (a * Math.Sin(2.0 * Math.PI * u2), a * Math.Cos(2.0 * Math.PI * u2),
            b * Math.Sin(2.0 * Math.PI * u3), b * Math.Cos(2.0 * Math.PI * u3));
    }

    private static double NextInRange(Random random, double min, double max)
    {
        return min + (max - min) * random.NextDouble();
    }
}
