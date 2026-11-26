using System.Globalization;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace CoolEngine.Mathematics.Tests.TestSupport;

/// <summary>
/// Component-wise assertions in the two forms of the plan's tolerance table.
/// <list type="bullet">
/// <item><c>Exact</c>: bitwise per component; any NaN equals any NaN, +0 and -0 differ.</item>
/// <item><c>Near</c>: per component, pass when <c>actual == expected</c> (IEEE: equal infinities pass, NaN never does),
/// otherwise when the difference and the bound <c>tolerance * scale</c> are finite and <c>|actual - expected| &lt;= bound</c>;
/// the caller passes the scale (one for every component, or one per component).</item>
/// </list>
/// The rules live in the pure functions <see cref="ExactFailure(float, float)"/> and
/// <see cref="NearFailure(double, double, double, double)"/> (null or the failure text; MathAssertTests tests them); the
/// assertions call them per component. Every failure names the caller's context (an oracle test passes
/// <see cref="Inputs.Label"/>, so the failing input index is in the message) and the component index. Overloads cover
/// spans, scalars, System.Numerics' Vector2/3/4 and the library's matrices, reading components from fields
/// (<c>Components</c>, matrices column after column), so an assertion does not depend on the memory layout it may be
/// testing; <see cref="Floats{T}"/> and <see cref="Doubles{T}"/> read memory order for the layout tests. Inside
/// <c>Assert.Multiple</c> every failing component is reported, not only the first.
/// </summary>
internal static class MathAssert
{
    // The plan's tolerance table. "D" is the double column.

    /// <summary>Sums of products (Dot, Cross, Length, products, Determinant): times the caller's scale.</summary>
    public const float SumsOfProducts = 1e-6f;
    public const double SumsOfProductsD = 1e-14;

    /// <summary>Trigonometric factories and Slerp: absolute (scale 1).</summary>
    public const float Trigonometric = 1e-6f;
    public const double TrigonometricD = 1e-14;

    /// <summary>LookAt and projections: times <c>max(1, |expected|)</c>.</summary>
    public const float Projection = 1e-5f;
    public const double ProjectionD = 1e-12;

    /// <summary>Inverse: absolute for <c>M * M.Inverted()</c> against Identity, times <c>max(1, |expected|)</c> against the oracle.</summary>
    public const float Inverse = 1e-4f;
    public const double InverseD = 1e-10;

    #region Exact

    public static void Exact(float actual, float expected, string? context = null)
    {
        Exact([actual], [expected], context);
    }

    public static void Exact(double actual, double expected, string? context = null)
    {
        Exact([actual], [expected], context);
    }

    public static void Exact(ReadOnlySpan<float> actual, ReadOnlySpan<float> expected, string? context = null)
    {
        if (!SameLength(actual.Length, expected.Length, context))
            return;

        for (var i = 0; i < expected.Length; i++)
        {
            var failure = ExactFailure(actual[i], expected[i]);
            if (failure is not null)
                Assert.Fail(Prefix(context, i) + failure);
        }
    }

    public static void Exact(ReadOnlySpan<double> actual, ReadOnlySpan<double> expected, string? context = null)
    {
        if (!SameLength(actual.Length, expected.Length, context))
            return;

        for (var i = 0; i < expected.Length; i++)
        {
            var failure = ExactFailure(actual[i], expected[i]);
            if (failure is not null)
                Assert.Fail(Prefix(context, i) + failure);
        }
    }

    public static void Exact(Vector2 actual, Vector2 expected, string? context = null)
    {
        Exact(Components(actual), Components(expected), context);
    }

    public static void Exact(Vector3 actual, Vector3 expected, string? context = null)
    {
        Exact(Components(actual), Components(expected), context);
    }

    public static void Exact(Vector4 actual, Vector4 expected, string? context = null)
    {
        Exact(Components(actual), Components(expected), context);
    }

    public static void Exact(Matrix2 actual, Matrix2 expected, string? context = null)
    {
        Exact(Components(actual), Components(expected), context);
    }

    public static void Exact(Matrix3 actual, Matrix3 expected, string? context = null)
    {
        Exact(Components(actual), Components(expected), context);
    }

    public static void Exact(Matrix4 actual, Matrix4 expected, string? context = null)
    {
        Exact(Components(actual), Components(expected), context);
    }

    #endregion

    #region Near

    public static void Near(Matrix2 actual, Matrix2 expected, float tolerance, float scale = 1f, string? context = null)
    {
        Near(Components(actual), Components(expected), tolerance, scale, context);
    }

    public static void Near(Matrix2 actual, Matrix2 expected, float tolerance, ReadOnlySpan<float> scales, string? context = null)
    {
        Near(Components(actual), Components(expected), tolerance, scales, context);
    }

    public static void Near(Matrix3 actual, Matrix3 expected, float tolerance, float scale = 1f, string? context = null)
    {
        Near(Components(actual), Components(expected), tolerance, scale, context);
    }

    public static void Near(Matrix3 actual, Matrix3 expected, float tolerance, ReadOnlySpan<float> scales, string? context = null)
    {
        Near(Components(actual), Components(expected), tolerance, scales, context);
    }

    public static void Near(Matrix4 actual, Matrix4 expected, float tolerance, float scale = 1f, string? context = null)
    {
        Near(Components(actual), Components(expected), tolerance, scale, context);
    }

    public static void Near(Matrix4 actual, Matrix4 expected, float tolerance, ReadOnlySpan<float> scales, string? context = null)
    {
        Near(Components(actual), Components(expected), tolerance, scales, context);
    }


    public static void Near(Vector2 actual, Vector2 expected, float tolerance, float scale = 1f, string? context = null)
    {
        Near(Components(actual), Components(expected), tolerance, scale, context);
    }

    public static void Near(Vector2 actual, Vector2 expected, float tolerance, ReadOnlySpan<float> scales, string? context = null)
    {
        Near(Components(actual), Components(expected), tolerance, scales, context);
    }

    public static void Near(Vector3 actual, Vector3 expected, float tolerance, float scale = 1f, string? context = null)
    {
        Near(Components(actual), Components(expected), tolerance, scale, context);
    }

    public static void Near(Vector3 actual, Vector3 expected, float tolerance, ReadOnlySpan<float> scales, string? context = null)
    {
        Near(Components(actual), Components(expected), tolerance, scales, context);
    }

    public static void Near(Vector4 actual, Vector4 expected, float tolerance, float scale = 1f, string? context = null)
    {
        Near(Components(actual), Components(expected), tolerance, scale, context);
    }

    public static void Near(Vector4 actual, Vector4 expected, float tolerance, ReadOnlySpan<float> scales, string? context = null)
    {
        Near(Components(actual), Components(expected), tolerance, scales, context);
    }

    public static void Near(float actual, float expected, float tolerance, float scale = 1f, string? context = null)
    {
        Near([actual], [expected], tolerance, scale, context);
    }

    public static void Near(double actual, double expected, double tolerance, double scale = 1.0, string? context = null)
    {
        Near([actual], [expected], tolerance, scale, context);
    }

    public static void Near(ReadOnlySpan<float> actual, ReadOnlySpan<float> expected, float tolerance, float scale = 1f,
        string? context = null)
    {
        if (!SameLength(actual.Length, expected.Length, context))
            return;

        for (var i = 0; i < expected.Length; i++)
            NearComponent(actual[i], expected[i], tolerance, scale, context, i);
    }

    public static void Near(ReadOnlySpan<float> actual, ReadOnlySpan<float> expected, float tolerance, ReadOnlySpan<float> scales,
        string? context = null)
    {
        if (!SameLength(actual.Length, expected.Length, context) || !SameLength(scales.Length, expected.Length, context, "scales"))
            return;

        for (var i = 0; i < expected.Length; i++)
            NearComponent(actual[i], expected[i], tolerance, scales[i], context, i);
    }

    public static void Near(ReadOnlySpan<double> actual, ReadOnlySpan<double> expected, double tolerance, double scale = 1.0,
        string? context = null)
    {
        if (!SameLength(actual.Length, expected.Length, context))
            return;

        for (var i = 0; i < expected.Length; i++)
            NearComponent(actual[i], expected[i], tolerance, scale, context, i);
    }

    public static void Near(ReadOnlySpan<double> actual, ReadOnlySpan<double> expected, double tolerance, ReadOnlySpan<double> scales,
        string? context = null)
    {
        if (!SameLength(actual.Length, expected.Length, context) || !SameLength(scales.Length, expected.Length, context, "scales"))
            return;

        for (var i = 0; i < expected.Length; i++)
            NearComponent(actual[i], expected[i], tolerance, scales[i], context, i);
    }

    #endregion

    #region Scales

    /// <summary>The sums-of-products scale: the sum of the absolute values of the terms that build the result.</summary>
    public static float SumOfAbs(params ReadOnlySpan<float> terms)
    {
        var sum = 0.0;
        foreach (var term in terms)
            sum += Math.Abs(term);

        return (float)sum;
    }

    /// <inheritdoc cref="SumOfAbs(ReadOnlySpan{float})"/>
    public static double SumOfAbs(params ReadOnlySpan<double> terms)
    {
        var sum = 0.0;
        foreach (var term in terms)
            sum += Math.Abs(term);

        return sum;
    }

    /// <summary>The relative scale <c>max(1, |expected|)</c>.</summary>
    public static float AtLeastOne(float expected)
    {
        return MathF.Max(1f, MathF.Abs(expected));
    }

    /// <inheritdoc cref="AtLeastOne(float)"/>
    public static double AtLeastOne(double expected)
    {
        return Math.Max(1.0, Math.Abs(expected));
    }

    /// <summary>The relative scale <c>max(1, |expected|)</c> of every component, for the per-component <c>Near</c>.</summary>
    public static float[] AtLeastOne(ReadOnlySpan<float> expected)
    {
        var scales = new float[expected.Length];
        for (var i = 0; i < expected.Length; i++)
            scales[i] = AtLeastOne(expected[i]);

        return scales;
    }

    /// <inheritdoc cref="AtLeastOne(ReadOnlySpan{float})"/>
    public static double[] AtLeastOne(ReadOnlySpan<double> expected)
    {
        var scales = new double[expected.Length];
        for (var i = 0; i < expected.Length; i++)
            scales[i] = AtLeastOne(expected[i]);

        return scales;
    }

    /// <summary>
    /// The sums-of-products scale of a matrix product, per element in column-major order: <c>Σk |a[r,k] b[k,c]|</c>.
    /// </summary>
    public static float[] ProductScales(Matrix2 a, Matrix2 b)
    {
        return ProductScales(Components(a), Components(b), 2, 2);
    }

    /// <inheritdoc cref="ProductScales(Matrix2, Matrix2)"/>
    public static float[] ProductScales(Matrix3 a, Matrix3 b)
    {
        return ProductScales(Components(a), Components(b), 3, 3);
    }

    /// <inheritdoc cref="ProductScales(Matrix2, Matrix2)"/>
    public static float[] ProductScales(Matrix4 a, Matrix4 b)
    {
        return ProductScales(Components(a), Components(b), 4, 4);
    }

    /// <summary>The sums-of-products scale of <c>M * v</c>, per component: <c>Σk |m[r,k] v[k]|</c>.</summary>
    public static float[] ProductScales(Matrix2 matrix, Vector2 vector)
    {
        return ProductScales(Components(matrix), Components(vector), 2, 1);
    }

    /// <inheritdoc cref="ProductScales(Matrix2, Vector2)"/>
    public static float[] ProductScales(Matrix3 matrix, Vector3 vector)
    {
        return ProductScales(Components(matrix), Components(vector), 3, 1);
    }

    /// <inheritdoc cref="ProductScales(Matrix2, Vector2)"/>
    public static float[] ProductScales(Matrix4 matrix, Vector4 vector)
    {
        return ProductScales(Components(matrix), Components(vector), 4, 1);
    }

    /// <summary>
    /// The sums-of-products scale of a determinant: the sum of the absolute values of its Leibniz terms (the permanent
    /// of the element-wise absolute matrix).
    /// </summary>
    public static float DeterminantScale(Matrix2 matrix)
    {
        return (float)AbsPermanent(Components(matrix), 2, 0, 0);
    }

    /// <inheritdoc cref="DeterminantScale(Matrix2)"/>
    public static float DeterminantScale(Matrix3 matrix)
    {
        return (float)AbsPermanent(Components(matrix), 3, 0, 0);
    }

    /// <inheritdoc cref="DeterminantScale(Matrix2)"/>
    public static float DeterminantScale(Matrix4 matrix)
    {
        return (float)AbsPermanent(Components(matrix), 4, 0, 0);
    }

    /// <summary>
    /// The sums-of-products scale of rotating <paramref name="vector"/> by the unit quaternion <paramref name="rotation"/>,
    /// per component. The expanded terms of <c>v + 2w(u × v) + 2u × (u × v)</c> are the same products as the terms of
    /// the rotation matrix times the vector, so one scale serves both forms: for component i (j, k the next two)
    /// <c>|v_i| + |2w u_j v_k| + |2w u_k v_j| + |2u_j u_i v_j| + |2u_j² v_i| + |2u_k² v_i| + |2u_k u_i v_k|</c>.
    /// </summary>
    public static float[] RotationScales(Quaternion rotation, Vector3 vector)
    {
        float[] u = [rotation.X, rotation.Y, rotation.Z];
        float[] v = [vector.X, vector.Y, vector.Z];
        var w = rotation.W;
        var scales = new float[3];
        for (var i = 0; i < 3; i++)
        {
            var j = (i + 1) % 3;
            var k = (i + 2) % 3;
            scales[i] = SumOfAbs(v[i], 2f * w * u[j] * v[k], 2f * w * u[k] * v[j], 2f * u[j] * u[i] * v[j], 2f * u[j] * u[j] * v[i],
                2f * u[k] * u[k] * v[i], 2f * u[k] * u[i] * v[k]);
        }

        return scales;
    }

    // a is n × n and b is n × m, both column-major; the result is n × m, column-major.
    private static float[] ProductScales(float[] a, float[] b, int n, int m)
    {
        var scales = new float[n * m];
        for (var column = 0; column < m; column++)
        {
            for (var row = 0; row < n; row++)
            {
                var sum = 0.0;
                for (var k = 0; k < n; k++)
                    sum += Math.Abs((double)a[k * n + row] * b[column * n + k]);

                scales[column * n + row] = (float)sum;
            }
        }

        return scales;
    }

    // Σ over the permutations of the rows still free: |m[row, column]| times the permanent of the rest.
    private static double AbsPermanent(float[] columnMajor, int n, int row, int usedColumns)
    {
        if (row == n)
            return 1.0;

        var sum = 0.0;
        for (var column = 0; column < n; column++)
        {
            if ((usedColumns & (1 << column)) != 0)
                continue;

            sum += Math.Abs(columnMajor[column * n + row]) * AbsPermanent(columnMajor, n, row + 1, usedColumns | (1 << column));
        }

        return sum;
    }

    #endregion

    #region Components

    /// <summary>The elements of a matrix read from its column fields, column after column (column-major).</summary>
    public static float[] Components(Matrix2 value)
    {
        return [.. Components(value.Column0), .. Components(value.Column1)];
    }

    /// <inheritdoc cref="Components(Matrix2)"/>
    public static float[] Components(Matrix3 value)
    {
        return [.. Components(value.Column0), .. Components(value.Column1), .. Components(value.Column2)];
    }

    /// <inheritdoc cref="Components(Matrix2)"/>
    public static float[] Components(Matrix4 value)
    {
        return [.. Components(value.Column0), .. Components(value.Column1), .. Components(value.Column2), .. Components(value.Column3)];
    }

    /// <summary>The components of a System.Numerics vector read from its fields: X, Y, Z, W.</summary>
    public static float[] Components(Vector2 value)
    {
        return [value.X, value.Y];
    }

    /// <inheritdoc cref="Components(Vector2)"/>
    public static float[] Components(Vector3 value)
    {
        return [value.X, value.Y, value.Z];
    }

    /// <inheritdoc cref="Components(Vector2)"/>
    public static float[] Components(Vector4 value)
    {
        return [value.X, value.Y, value.Z, value.W];
    }

    /// <summary>
    /// The components of a float library type in memory order (vectors X, Y, Z, W; matrices column after column).
    /// </summary>
    public static float[] Floats<T>(T value) where T : unmanaged
    {
        return Components<T, float>(value);
    }

    /// <summary>The components of a double library type in memory order.</summary>
    public static double[] Doubles<T>(T value) where T : unmanaged
    {
        return Components<T, double>(value);
    }

    private static TScalar[] Components<T, TScalar>(T value) where T : unmanaged where TScalar : unmanaged
    {
        var size = Unsafe.SizeOf<T>();
        var scalarSize = Unsafe.SizeOf<TScalar>();
        if (size % scalarSize != 0)
            throw new ArgumentException($"{typeof(T).Name} ({size} B) is not a whole number of {typeof(TScalar).Name} components.");

        return MemoryMarshal.Cast<T, TScalar>(new ReadOnlySpan<T>(in value)).ToArray();
    }

    #endregion

    #region Pure comparison cores

    /// <summary>
    /// The <c>Exact</c> rule for one component: null when the bits are equal or both values are NaN (any payload),
    /// otherwise the failure text. +0 and -0 differ.
    /// </summary>
    public static string? ExactFailure(float actual, float expected)
    {
        if (BitwiseEqual(actual, expected))
            return null;

        return $"expected {Format(expected)} (0x{BitConverter.SingleToUInt32Bits(expected):X8}), " +
               $"actual {Format(actual)} (0x{BitConverter.SingleToUInt32Bits(actual):X8}), exact comparison";
    }

    /// <inheritdoc cref="ExactFailure(float, float)"/>
    public static string? ExactFailure(double actual, double expected)
    {
        if (BitwiseEqual(actual, expected))
            return null;

        return $"expected {Format(expected)} (0x{BitConverter.DoubleToUInt64Bits(expected):X16}), " +
               $"actual {Format(actual)} (0x{BitConverter.DoubleToUInt64Bits(actual):X16}), exact comparison";
    }

    /// <summary>
    /// The <c>Near</c> rule for one component: null when <c>actual == expected</c> (IEEE, so equal infinities pass and NaN
    /// never does); otherwise the failure text when the difference or the bound <c>tolerance * scale</c> is not finite, or
    /// the difference exceeds the bound. Floats arrive widened to double, so the difference and the bound carry no float
    /// rounding.
    /// </summary>
    public static string? NearFailure(double actual, double expected, double tolerance, double scale)
    {
        if (actual == expected)
            return null;

        var difference = Math.Abs(actual - expected);
        var bound = tolerance * scale;
        if (double.IsFinite(difference) && double.IsFinite(bound) && difference <= bound)
            return null;

        return $"expected {Format(expected)}, actual {Format(actual)}, " +
               $"|difference| {Format(difference)} > tolerance {Format(tolerance)} * scale {Format(scale)} = {Format(bound)}" +
               (double.IsFinite(difference) && double.IsFinite(bound) ? "" : " (not finite)");
    }

    #endregion

    private static void NearComponent(double actual, double expected, double tolerance, double scale, string? context, int index)
    {
        var failure = NearFailure(actual, expected, tolerance, scale);
        if (failure is not null)
            Assert.Fail(Prefix(context, index) + failure);
    }

    private static bool SameLength(int actual, int expected, string? context, string what = "component count")
    {
        if (actual == expected)
            return true;

        Assert.Fail($"{(context is null ? "" : context + ": ")}{what} {actual}, expected {expected}");
        return false;
    }

    private static bool BitwiseEqual(float actual, float expected)
    {
        return float.IsNaN(actual) && float.IsNaN(expected)
               || BitConverter.SingleToUInt32Bits(actual) == BitConverter.SingleToUInt32Bits(expected);
    }

    private static bool BitwiseEqual(double actual, double expected)
    {
        return double.IsNaN(actual) && double.IsNaN(expected)
               || BitConverter.DoubleToUInt64Bits(actual) == BitConverter.DoubleToUInt64Bits(expected);
    }

    private static string Prefix(string? context, int index)
    {
        return context is null ? $"component {index}: " : $"{context}: component {index}: ";
    }

    private static string Format(float value)
    {
        return value.ToString("R", CultureInfo.InvariantCulture);
    }

    private static string Format(double value)
    {
        return value.ToString("R", CultureInfo.InvariantCulture);
    }
}
