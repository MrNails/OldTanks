using System.Numerics;
using CoolEngine.Mathematics;

namespace CoolEngine.Tests.TestSupport;

/// <summary>
/// Compares values against golden floats recorded on today's code (OT-5, before the move onto
/// CoolEngine.Mathematics), within <c>1e-5 × max(1, |expected|)</c> per element: the move may round differently
/// in the last bits, while a transposed or reordered product misses by far more.
/// </summary>
internal static class GoldenAssert
{
    private const float RelativeTolerance = 1e-5f;

    public static void AreClose(float[] expected, float[] actual, string what)
    {

        Assert.That(actual, Has.Length.EqualTo(expected.Length), $"{what}: element count");

        for (var i = 0; i < expected.Length; i++)
        {
            var tolerance = RelativeTolerance * MathF.Max(1f, MathF.Abs(expected[i]));
            Assert.That(actual[i], Is.EqualTo(expected[i]).Within(tolerance), $"{what}[{i}]");
        }
    }

    /// <summary>
    /// The 16 floats in memory order, the order GL receives them: Column0 to Column3 (the bytes of Row0 to Row3 of the
    /// OpenTK matrix the goldens were recorded from).
    /// </summary>
    public static float[] Floats(Matrix4 m) =>
    [
        m.Column0.X, m.Column0.Y, m.Column0.Z, m.Column0.W,
        m.Column1.X, m.Column1.Y, m.Column1.Z, m.Column1.W,
        m.Column2.X, m.Column2.Y, m.Column2.Z, m.Column2.W,
        m.Column3.X, m.Column3.Y, m.Column3.Z, m.Column3.W
    ];

    public static float[] Floats(Vector3 v) => [v.X, v.Y, v.Z];

    public static float[] Floats(IEnumerable<Vector3> vectors) => vectors.SelectMany(Floats).ToArray();
}
