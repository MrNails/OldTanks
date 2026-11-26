namespace CoolEngine.Mathematics.Tests.TestSupport;

/// <summary>The comparison rules of <see cref="MathAssert"/>, tested on its pure cores (null means pass).</summary>
[TestFixture]
public sealed class MathAssertTests
{
    [Test]
    public void Near_FiniteActualInfiniteExpected_Fails()
    {
        // The scale max(1, |expected|) is infinite here, so the bound is too: an infinite bound must not pass everything.
        Assert.That(MathAssert.NearFailure(1.0, double.PositiveInfinity, 1e-6, MathAssert.AtLeastOne(double.PositiveInfinity)),
            Is.Not.Null);
    }

    [Test]
    public void Near_EqualInfinities_Pass()
    {
        Assert.That(MathAssert.NearFailure(double.PositiveInfinity, double.PositiveInfinity, 1e-6, double.PositiveInfinity), Is.Null);
    }

    [Test]
    public void Near_NaNActual_Fails()
    {
        Assert.That(MathAssert.NearFailure(double.NaN, 1.0, 1e-6, 1.0), Is.Not.Null);
    }

    [Test]
    public void Near_WithinBound_Passes()
    {
        Assert.That(MathAssert.NearFailure(1.0000005, 1.0, 1e-6, 1.0), Is.Null);
    }

    [Test]
    public void Near_OutsideBound_Fails()
    {
        Assert.That(MathAssert.NearFailure(1.000002, 1.0, 1e-6, 1.0), Is.Not.Null);
    }

    [Test]
    public void Exact_DifferentNaNPayloads_Pass()
    {
        var negativeNaNWithPayload = BitConverter.UInt32BitsToSingle(0xFFC00001);

        Assert.That(MathAssert.ExactFailure(negativeNaNWithPayload, float.NaN), Is.Null);
    }

    [Test]
    public void Exact_SignedZeros_Fail()
    {
        Assert.That(MathAssert.ExactFailure(0f, float.NegativeZero), Is.Not.Null);
    }
}
