using CoolEngine.Mathematics.Tests.TestSupport;

namespace CoolEngine.Mathematics.Tests;

[TestFixture]
public sealed class MathHelperTests
{
    [Test]
    public void Constants_PiFamily_MatchMathF()
    {
        Assert.Multiple(() =>
        {
            MathAssert.Exact(MathHelper.Pi, MathF.PI, nameof(MathHelper.Pi));
            MathAssert.Exact(MathHelper.TwoPi, 2 * MathF.PI, nameof(MathHelper.TwoPi));
            MathAssert.Exact(MathHelper.PiOver2, MathF.PI / 2, nameof(MathHelper.PiOver2));
            MathAssert.Exact(MathHelper.PiOver4, MathF.PI / 4, nameof(MathHelper.PiOver4));
        });
    }

    [TestCase(0f, 0f)]
    [TestCase(180f, MathF.PI)]
    [TestCase(-90f, -MathF.PI / 2)]
    public void DegreesToRadians_Float_ConvertsHandValues(float degrees, float expected)
    {
        MathAssert.Near(MathHelper.DegreesToRadians(degrees), expected, 1e-6f);
    }

    [TestCase(0.0, 0.0)]
    [TestCase(180.0, Math.PI)]
    [TestCase(-90.0, -Math.PI / 2)]
    public void DegreesToRadians_Double_ConvertsHandValues(double degrees, double expected)
    {
        MathAssert.Near(MathHelper.DegreesToRadians(degrees), expected, 1e-15);
    }

    [TestCase(0f, 0f)]
    [TestCase(MathF.PI, 180f)]
    [TestCase(-MathF.PI / 2, -90f)]
    public void RadiansToDegrees_Float_ConvertsHandValues(float radians, float expected)
    {
        MathAssert.Near(MathHelper.RadiansToDegrees(radians), expected, 1e-4f);
    }

    [TestCase(0.0, 0.0)]
    [TestCase(Math.PI, 180.0)]
    [TestCase(-Math.PI / 2, -90.0)]
    public void RadiansToDegrees_Double_ConvertsHandValues(double radians, double expected)
    {
        MathAssert.Near(MathHelper.RadiansToDegrees(radians), expected, 1e-12);
    }
}
