using System.Globalization;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace CoolEngine.Mathematics.Tests.TestSupport;

/// <summary>
/// Component-wise assertions in the two forms of the plan's tolerance table.
/// <list type="bullet">
/// <item><c>Exact</c>: bitwise per component; any NaN equals any NaN, +0 and -0 differ.</item>
/// <item><c>Near</c>: per component <c>|actual - expected| &lt;= tolerance * scale</c>; the caller passes the scale
/// (one for every component, or one per component). A NaN or infinite difference always fails.</item>
/// </list>
/// Every failure names the caller's context (an oracle test passes <see cref="Inputs.Label"/>, so the failing input
/// index is in the message) and the component index. The cores take spans of components; each library type adds
/// its one-line overloads in the stage that adds the type, reading its components from its fields
/// (<c>Components</c>), so an assertion does not depend on the memory layout it may be testing; <see cref="Floats{T}"/>
/// and <see cref="Doubles{T}"/> read memory order for the layout tests. Inside <c>Assert.Multiple</c> every failing
/// component is reported, not only the first.
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
            if (BitwiseEqual(actual[i], expected[i]))
                continue;

            Assert.Fail($"{Prefix(context, i)}expected {Format(expected[i])} (0x{BitConverter.SingleToUInt32Bits(expected[i]):X8}), " +
                        $"actual {Format(actual[i])} (0x{BitConverter.SingleToUInt32Bits(actual[i]):X8}), exact comparison");
        }
    }

    public static void Exact(ReadOnlySpan<double> actual, ReadOnlySpan<double> expected, string? context = null)
    {
        if (!SameLength(actual.Length, expected.Length, context))
            return;

        for (var i = 0; i < expected.Length; i++)
        {
            if (BitwiseEqual(actual[i], expected[i]))
                continue;

            Assert.Fail($"{Prefix(context, i)}expected {Format(expected[i])} (0x{BitConverter.DoubleToUInt64Bits(expected[i]):X16}), " +
                        $"actual {Format(actual[i])} (0x{BitConverter.DoubleToUInt64Bits(actual[i]):X16}), exact comparison");
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

    #endregion

    #region Near

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

    #endregion

    #region Components

    /// <summary>The components of a vector read from its fields, X, Y, Z, W, independent of the memory layout.</summary>
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

    private static void NearComponent(double actual, double expected, double tolerance, double scale, string? context, int index)
    {
        // Floats arrive widened to double, so the difference and the bound are computed without float rounding.
        var difference = Math.Abs(actual - expected);
        var bound = tolerance * scale;
        if (difference <= bound)
            return;

        Assert.Fail($"{Prefix(context, index)}expected {Format(expected)}, actual {Format(actual)}, " +
                    $"|difference| {Format(difference)} > tolerance {Format(tolerance)} * scale {Format(scale)} = {Format(bound)}");
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
