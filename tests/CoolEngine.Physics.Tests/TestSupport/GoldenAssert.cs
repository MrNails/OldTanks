using System.Numerics;

namespace CoolEngine.Physics.Tests.TestSupport;

/// <summary>
/// Compares values against golden floats recorded on today's code, within <c>1e-5 × max(1, |expected|)</c> per element.
/// A copy of CoolEngine.Tests.TestSupport.GoldenAssert (this project references only the library).
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

    public static float[] Floats(Vector3 v) => [v.X, v.Y, v.Z];

    public static float[] Floats(IEnumerable<Vector3> vectors) => vectors.SelectMany(Floats).ToArray();
}
