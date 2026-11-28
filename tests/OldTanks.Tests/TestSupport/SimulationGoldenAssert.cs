using System.Globalization;
using System.Text;

namespace OldTanks.Tests.TestSupport;

/// <summary>
/// Comparison of recorded simulation values. <see cref="AreClose"/> is the golden comparison (within
/// <c>1e-5 × max(1, |expected|)</c> per element, the project's golden tolerance); <see cref="AreBitEqual"/> serves only
/// where both values come from the same process (two runs, or a state before and after). Every failure message ends
/// with <c>actual as C#: </c> and the actual values as a C# literal, which is how the goldens are recorded.
/// </summary>
internal static class SimulationGoldenAssert
{
    private const float RelativeTolerance = 1e-5f;

    private const int ValuesPerLine = 7;

    /// <summary>Fails when the lengths differ or an element differs by more than the golden tolerance.</summary>
    public static void AreClose(float[] expected, float[] actual, string what)
    {
        string? failure = null;

        if (expected.Length != actual.Length)
        {
            failure = $"{what}: expected {expected.Length} values, actual {actual.Length}.";
        }
        else
        {
            for (var i = 0; i < expected.Length; i++)
            {
                if (IsClose(expected[i], actual[i]))
                    continue;

                failure = $"{what}: first difference at [{i}], expected {Format(expected[i])}, actual {Format(actual[i])}.";
                break;
            }
        }

        Assert.That(failure, Is.Null, () => $"{failure} actual as C#: {ToCSharpLiteral(actual)}");
    }

    /// <summary>Fails when the lengths differ or an element differs in any bit.</summary>
    public static void AreBitEqual(float[] expected, float[] actual, string what)
    {
        string? failure = null;

        if (expected.Length != actual.Length)
        {
            failure = $"{what}: expected {expected.Length} values, actual {actual.Length}.";
        }
        else
        {
            for (var i = 0; i < expected.Length; i++)
            {
                var expectedBits = BitConverter.SingleToInt32Bits(expected[i]);
                var actualBits = BitConverter.SingleToInt32Bits(actual[i]);

                if (expectedBits == actualBits)
                    continue;

                failure = $"{what}: first difference at [{i}], expected {Format(expected[i])} (0x{expectedBits:X8}), " +
                          $"actual {Format(actual[i])} (0x{actualBits:X8}).";
                break;
            }
        }

        Assert.That(failure, Is.Null, () => $"{failure} actual as C#: {ToCSharpLiteral(actual)}");
    }

    /// <summary>
    /// The values as a C# array literal: round-trip ("R") literals, <c>-0f</c> for negative zero, <c>float.NaN</c> and
    /// the infinities by name, seven values per line when there are more than seven.
    /// </summary>
    public static string ToCSharpLiteral(float[] values)
    {
        if (values.Length <= ValuesPerLine)
            return "[" + string.Join(", ", values.Select(Literal)) + "]";

        var builder = new StringBuilder();
        builder.Append("[\n");

        for (var i = 0; i < values.Length; i += ValuesPerLine)
        {
            var count = Math.Min(ValuesPerLine, values.Length - i);
            builder.Append("    ");
            builder.Append(string.Join(", ", values.Skip(i).Take(count).Select(Literal)));
            builder.Append(i + count < values.Length ? ",\n" : "\n");
        }

        builder.Append(']');
        return builder.ToString();
    }

    /// <summary>The rows as a C# array-of-arrays literal, one <see cref="ToCSharpLiteral(float[])"/> per row.</summary>
    public static string ToCSharpLiteral(float[][] rows)
    {
        return "[\n" + string.Join(",\n", rows.Select(ToCSharpLiteral)) + "\n]";
    }

    private static bool IsClose(float expected, float actual)
    {
        if (float.IsNaN(expected) || float.IsNaN(actual))
            return float.IsNaN(expected) && float.IsNaN(actual);

        // Equal values include equal infinities, whose difference is NaN; an infinite value is close to nothing else.
        if (expected == actual)
            return true;

        if (float.IsInfinity(expected) || float.IsInfinity(actual))
            return false;

        return !(Math.Abs(actual - expected) > RelativeTolerance * Math.Max(1f, Math.Abs(expected)));
    }

    private static string Format(float value) => value.ToString("R", CultureInfo.InvariantCulture);

    private static string Literal(float value)
    {
        if (float.IsNaN(value))
            return "float.NaN";
        if (float.IsPositiveInfinity(value))
            return "float.PositiveInfinity";
        if (float.IsNegativeInfinity(value))
            return "float.NegativeInfinity";

        return value.ToString("R", CultureInfo.InvariantCulture) + "f";
    }
}
