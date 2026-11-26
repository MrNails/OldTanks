using OpenTK.Mathematics;

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

    /// <summary>The 16 floats in memory order, the order GL receives them: Row0 to Row3 of OpenTK's matrix.</summary>
    public static float[] Floats(Matrix4 m) =>
    [
        m.Row0.X, m.Row0.Y, m.Row0.Z, m.Row0.W,
        m.Row1.X, m.Row1.Y, m.Row1.Z, m.Row1.W,
        m.Row2.X, m.Row2.Y, m.Row2.Z, m.Row2.W,
        m.Row3.X, m.Row3.Y, m.Row3.Z, m.Row3.W
    ];

    public static float[] Floats(Vector3 v) => [v.X, v.Y, v.Z];

    public static float[] Floats(IEnumerable<Vector3> vectors) => vectors.SelectMany(Floats).ToArray();
}
