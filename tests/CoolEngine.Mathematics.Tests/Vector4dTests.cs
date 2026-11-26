using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using CoolEngine.Mathematics.Tests.TestSupport;
using Numerics = System.Numerics;
using OpenTKMath = OpenTK.Mathematics;

namespace CoolEngine.Mathematics.Tests;

[TestFixture]
public sealed class Vector4dTests
{
    [Test]
    public void Layout_SizeAndFieldOrder_MatchSizeInBytes()
    {
        var vector = new Vector4d { X = 1.0, Y = 2.0, Z = 3.0, W = 4.0 };

        var memory = MemoryMarshal.Cast<Vector4d, double>(new ReadOnlySpan<Vector4d>(in vector)).ToArray();

        Assert.Multiple(() =>
        {
            Assert.That(Unsafe.SizeOf<Vector4d>(), Is.EqualTo(Vector4d.SizeInBytes));
            Assert.That(Unsafe.SizeOf<Vector4d>(), Is.EqualTo(32));
            Assert.That(memory, Is.EqualTo(new[] { 1.0, 2.0, 3.0, 4.0 }));
        });
    }

    [Test]
    public void Constructor_Components_SetsFields()
    {
        Assert.Multiple(() =>
        {
            MathAssert.Exact(MathAssert.Components(new Vector4d(-2.5)), [-2.5, -2.5, -2.5, -2.5], "Vector4d(value)");
            MathAssert.Exact(MathAssert.Components(new Vector4d(1.0, -2.0, 3.5, -4.0)), [1.0, -2.0, 3.5, -4.0], "Vector4d(x, y, z, w)");
            MathAssert.Exact(MathAssert.Components(new Vector4d(new Vector2d(4.0, -5.0), 6.0, 7.0)), [4.0, -5.0, 6.0, 7.0],
                "Vector4d(xy, z, w)");
            MathAssert.Exact(MathAssert.Components(new Vector4d(new Vector3d(8.0, 9.0, -10.0), 11.0)), [8.0, 9.0, -10.0, 11.0],
                "Vector4d(xyz, w)");
        });
    }

    [Test]
    public void Constants_ZeroOneUnits_HaveExpectedComponents()
    {
        Assert.Multiple(() =>
        {
            MathAssert.Exact(MathAssert.Components(Vector4d.Zero), [0.0, 0.0, 0.0, 0.0], nameof(Vector4d.Zero));
            MathAssert.Exact(MathAssert.Components(Vector4d.One), [1.0, 1.0, 1.0, 1.0], nameof(Vector4d.One));
            MathAssert.Exact(MathAssert.Components(Vector4d.UnitX), [1.0, 0.0, 0.0, 0.0], nameof(Vector4d.UnitX));
            MathAssert.Exact(MathAssert.Components(Vector4d.UnitY), [0.0, 1.0, 0.0, 0.0], nameof(Vector4d.UnitY));
            MathAssert.Exact(MathAssert.Components(Vector4d.UnitZ), [0.0, 0.0, 1.0, 0.0], nameof(Vector4d.UnitZ));
            MathAssert.Exact(MathAssert.Components(Vector4d.UnitW), [0.0, 0.0, 0.0, 1.0], nameof(Vector4d.UnitW));
        });
    }

    [Test]
    public void Indexer_ValidIndex_ReadsAndWritesComponent()
    {
        var source = new Vector4d(1.0, 2.0, 3.0, 4.0);

        Assert.Multiple(() =>
        {
            MathAssert.Exact([source[0], source[1], source[2], source[3]], [1.0, 2.0, 3.0, 4.0], "get");

            for (var index = 0; index < 4; index++)
            {
                var vector = Vector4d.Zero;
                vector[index] = 7.0;

                var expected = new double[4];
                expected[index] = 7.0;
                MathAssert.Exact(MathAssert.Components(vector), expected, $"set [{index}]");
                MathAssert.Exact(vector[index], 7.0, $"get [{index}] after set");
            }
        });
    }

    [TestCase(-1)]
    [TestCase(4)]
    public void Indexer_OutOfRange_Throws(int index)
    {
        var vector = new Vector4d(1.0, 2.0, 3.0, 4.0);

        Assert.Multiple(() =>
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => _ = vector[index], "get");
            Assert.Throws<ArgumentOutOfRangeException>(() => vector[index] = 0.0, "set");
        });
    }

    [Test]
    public void Arithmetic_RandomInputs_EqualOpenTKExactly()
    {
        var random = Inputs.CreateRandom();

        Assert.Multiple(() =>
        {
            for (var i = 0; i < Inputs.Count; i++)
            {
                var a = random.NextVector4d();
                var b = random.NextVector4d();
                var s = random.NextComponentD();
                var oa = Oracle.ToOpenTK(a);
                var ob = Oracle.ToOpenTK(b);
                var label = Inputs.Label(i);

                MathAssert.Exact(a + b, Oracle.FromOpenTK(oa + ob), label + " a + b");
                MathAssert.Exact(a - b, Oracle.FromOpenTK(oa - ob), label + " a - b");
                MathAssert.Exact(-a, Oracle.FromOpenTK(-oa), label + " -a");
                MathAssert.Exact(a * b, Oracle.FromOpenTK(oa * ob), label + " a * b");
                MathAssert.Exact(a * s, Oracle.FromOpenTK(oa * s), label + " a * s");
                MathAssert.Exact(s * a, Oracle.FromOpenTK(s * oa), label + " s * a");
                MathAssert.Exact(a / b, Oracle.FromOpenTK(oa / ob), label + " a / b");
                MathAssert.Exact(a / s, Oracle.FromOpenTK(oa / s), label + " a / s");
            }
        });
    }

    [Test]
    public void Dot_RandomInputs_MatchesOpenTK()
    {
        var random = Inputs.CreateRandom();

        Assert.Multiple(() =>
        {
            for (var i = 0; i < Inputs.Count; i++)
            {
                var a = random.NextVector4d();
                var b = random.NextVector4d();

                MathAssert.Near(Vector4d.Dot(a, b), OpenTKMath.Vector4d.Dot(Oracle.ToOpenTK(a), Oracle.ToOpenTK(b)), MathAssert.SumsOfProductsD,
                    MathAssert.SumOfAbs(a.X * b.X, a.Y * b.Y, a.Z * b.Z, a.W * b.W), Inputs.Label(i));
            }
        });
    }

    [Test]
    public void Length_ThreeFour_IsFive()
    {
        MathAssert.Exact(new Vector4d(3.0, 4.0, 0.0, 0.0).Length(), 5.0);
    }

    [Test]
    public void Length_RandomInputs_MatchesOpenTK()
    {
        var random = Inputs.CreateRandom();

        Assert.Multiple(() =>
        {
            for (var i = 0; i < Inputs.Count; i++)
            {
                var a = random.NextVector4d();
                var oa = Oracle.ToOpenTK(a);
                var label = Inputs.Label(i);

                MathAssert.Near(a.Length(), oa.Length, MathAssert.SumsOfProductsD, MathAssert.AtLeastOne(oa.Length), label + " Length");
                MathAssert.Near(a.LengthSquared(), oa.LengthSquared, MathAssert.SumsOfProductsD,
                    MathAssert.SumOfAbs(a.X * a.X, a.Y * a.Y, a.Z * a.Z, a.W * a.W), label + " LengthSquared");
            }
        });
    }

    [Test]
    public void Normalize_RandomInputs_MatchesOpenTK()
    {
        var random = Inputs.CreateRandom();

        Assert.Multiple(() =>
        {
            for (var i = 0; i < Inputs.Count; i++)
            {
                var a = random.NextVector4d();

                var expected = Oracle.FromOpenTK(OpenTKMath.Vector4d.Normalize(Oracle.ToOpenTK(a)));

                MathAssert.Near(Vector4d.Normalize(a), expected, MathAssert.SumsOfProductsD,
                    MathAssert.AtLeastOne(MathAssert.Components(expected)), Inputs.Label(i));
            }
        });
    }

    [Test]
    public void Normalize_Zero_IsNaN()
    {
        MathAssert.Exact(Vector4d.Normalize(Vector4d.Zero), new Vector4d(double.NaN));
    }

    [Test]
    public void Distance_RandomInputs_MatchesOpenTK()
    {
        var random = Inputs.CreateRandom();

        Assert.Multiple(() =>
        {
            for (var i = 0; i < Inputs.Count; i++)
            {
                var a = random.NextVector4d();
                var b = random.NextVector4d();
                var d = a - b;
                var label = Inputs.Label(i);

                var distance = OpenTKMath.Vector4d.Distance(Oracle.ToOpenTK(a), Oracle.ToOpenTK(b));
                MathAssert.Near(Vector4d.Distance(a, b), distance, MathAssert.SumsOfProductsD, MathAssert.AtLeastOne(distance),
                    label + " Distance");
                MathAssert.Near(Vector4d.DistanceSquared(a, b), OpenTKMath.Vector4d.DistanceSquared(Oracle.ToOpenTK(a), Oracle.ToOpenTK(b)),
                    MathAssert.SumsOfProductsD, MathAssert.SumOfAbs(d.X * d.X, d.Y * d.Y, d.Z * d.Z, d.W * d.W), label + " DistanceSquared");
            }
        });
    }

    [Test]
    public void MinMax_RandomInputs_EqualOpenTKExactly()
    {
        var random = Inputs.CreateRandom();

        Assert.Multiple(() =>
        {
            for (var i = 0; i < Inputs.Count; i++)
            {
                var a = random.NextVector4d();
                var b = random.NextVector4d();
                var oa = Oracle.ToOpenTK(a);
                var ob = Oracle.ToOpenTK(b);
                var label = Inputs.Label(i);

                MathAssert.Exact(Vector4d.Min(a, b), Oracle.FromOpenTK(OpenTKMath.Vector4d.ComponentMin(oa, ob)), label + " Min");
                MathAssert.Exact(Vector4d.Max(a, b), Oracle.FromOpenTK(OpenTKMath.Vector4d.ComponentMax(oa, ob)), label + " Max");
            }
        });
    }

    [Test]
    public void AbsAndClamp_RandomInputs_EqualOpenTKExactly()
    {
        var random = Inputs.CreateRandom();

        Assert.Multiple(() =>
        {
            for (var i = 0; i < Inputs.Count; i++)
            {
                var value = random.NextVector4d();
                var p = random.NextVector4d();
                var q = random.NextVector4d();
                var min = Vector4d.Min(p, q);
                var max = Vector4d.Max(p, q);
                var label = Inputs.Label(i);

                MathAssert.Exact(Vector4d.Abs(value), Oracle.FromOpenTK(OpenTKMath.Vector4d.Abs(Oracle.ToOpenTK(value))), label + " Abs");

                // OpenTK 4.9.4's Vector4d.Clamp clamps Z and W wrongly (for example (-51.1, -29.3, 5.25, -66.1) between
                // (-30.7, -83, -21, -68.9) and (90, 38.3, 94.7, -2.4) gives Z = -21), so the oracle is OpenTK's
                // ComponentMin(ComponentMax(value, min), max), the same definition built from its correct members.
                var expectedClamp = OpenTKMath.Vector4d.ComponentMin(OpenTKMath.Vector4d.ComponentMax(Oracle.ToOpenTK(value), Oracle.ToOpenTK(min)),
                    Oracle.ToOpenTK(max));
                MathAssert.Exact(Vector4d.Clamp(value, min, max), Oracle.FromOpenTK(expectedClamp), label + " Clamp");
            }
        });
    }

    [Test]
    public void Lerp_RandomInputs_MatchesOpenTK()
    {
        var random = Inputs.CreateRandom();

        Assert.Multiple(() =>
        {
            for (var i = 0; i < Inputs.Count; i++)
            {
                var a = random.NextVector4d();
                var b = random.NextVector4d();
                var t = random.NextDouble();

                var expected = Oracle.FromOpenTK(OpenTKMath.Vector4d.Lerp(Oracle.ToOpenTK(a), Oracle.ToOpenTK(b), t));
                double[] scales =
                [
                    MathAssert.SumOfAbs(a.X, a.X * t, b.X * t),
                    MathAssert.SumOfAbs(a.Y, a.Y * t, b.Y * t),
                    MathAssert.SumOfAbs(a.Z, a.Z * t, b.Z * t),
                    MathAssert.SumOfAbs(a.W, a.W * t, b.W * t),
                ];

                MathAssert.Near(Vector4d.Lerp(a, b, t), expected, MathAssert.SumsOfProductsD, scales, Inputs.Label(i));
            }
        });
    }

    [Test]
    public void Lerp_TOutsideUnitRange_Extrapolates()
    {
        MathAssert.Near(Vector4d.Lerp(Vector4d.Zero, Vector4d.One * 10.0, 2.0), Vector4d.One * 20.0, MathAssert.SumsOfProductsD, 20.0);
    }

    [Test]
    public void Equality_SameAndDifferent_OperatorsAndEqualsAgree()
    {
        var a = new Vector4d(1.0, 2.0, 3.0, 4.0);
        var same = new Vector4d(1.0, 2.0, 3.0, 4.0);

        Assert.Multiple(() =>
        {
            Assert.That(a == same, Is.True, "== same");
            Assert.That(a != same, Is.False, "!= same");
            Assert.That(a.Equals(same), Is.True, "Equals same");
            Assert.That(a.Equals((object)same), Is.True, "Equals(object) same");

            for (var index = 0; index < 4; index++)
            {
                var different = a;
                different[index] += 1.0;

                Assert.That(a == different, Is.False, $"== differing in [{index}]");
                Assert.That(a != different, Is.True, $"!= differing in [{index}]");
                Assert.That(a.Equals(different), Is.False, $"Equals differing in [{index}]");
                Assert.That(a.Equals((object)different), Is.False, $"Equals(object) differing in [{index}]");
            }
        });
    }

    [Test]
    public void Equality_NaN_OperatorFalseEqualsTrue()
    {
        var a = new Vector4d(double.NaN, 2.0, 3.0, 4.0);
        var b = new Vector4d(double.NaN, 2.0, 3.0, 4.0);

        Assert.Multiple(() =>
        {
            Assert.That(a == b, Is.False, "==");
            Assert.That(a != b, Is.True, "!=");
            Assert.That(a.Equals(b), Is.True, "Equals");
        });
    }

    [Test]
    public void GetHashCode_PositiveAndNegativeZero_AreEqual()
    {
        var positive = new Vector4d(0.0, 2.0, 3.0, 4.0);
        var negative = new Vector4d(double.NegativeZero, 2.0, 3.0, 4.0);

        Assert.Multiple(() =>
        {
            Assert.That(negative.GetHashCode(), Is.EqualTo(positive.GetHashCode()), "hash");
            Assert.That(negative.Equals(positive), Is.True, "Equals");
        });
    }

    [Test]
    [SetCulture("de-DE")]
    public void ToString_CommaDecimalCulture_UsesInvariantFormat()
    {
        Assert.That(new Vector4d(1.5, 2.0, -3.0, 0.25).ToString(), Is.EqualTo("(1.5, 2, -3, 0.25)"));
    }

    [Test]
    public void FloatConversion_Widening_IsExact()
    {
        var random = Inputs.CreateRandom();

        Assert.Multiple(() =>
        {
            for (var i = 0; i < Inputs.Count; i++)
            {
                var single = random.NextVector4();
                var label = Inputs.Label(i);

                Vector4d widened = single;
                var back = (Numerics.Vector4)widened;

                MathAssert.Exact(MathAssert.Components(widened), [single.X, single.Y, single.Z, single.W], label + " widened");
                MathAssert.Exact(back, single, label + " float -> double -> float");
            }
        });
    }

    [Test]
    public void FloatConversion_Narrowing_RoundsToNearest()
    {
        var narrowed = (Numerics.Vector4)new Vector4d(1.0 + Math.Pow(2, -30), 0.1, -0.1, 3.0);

        MathAssert.Exact(narrowed, new Numerics.Vector4(1f, 0.1f, -0.1f, 3f));
    }

    [Test]
    public void Negation_SignedZeros_FlipSignBit()
    {
        MathAssert.Exact(-new Vector4d(0.0, double.NegativeZero, 1.0, -1.0), new Vector4d(double.NegativeZero, 0.0, -1.0, 1.0));
    }

    [Test]
    public void MinMaxClamp_NaNAndSignedZeros_MatchSystemNumerics()
    {
        // Hand values mixing NaN, +0, -0 and 1: the double results equal System.Numerics' float results.
        var a = new Vector4d(double.NaN, 0.0, double.NegativeZero, 1.0);
        var b = new Vector4d(1.0, double.NegativeZero, 0.0, double.NaN);
        var c = new Vector4d(0.0, 1.0, double.NaN, double.NegativeZero);
        var fa = (Numerics.Vector4)a;
        var fb = (Numerics.Vector4)b;
        var fc = (Numerics.Vector4)c;

        Assert.Multiple(() =>
        {
            MathAssert.Exact((Numerics.Vector4)Vector4d.Min(a, b), Numerics.Vector4.Min(fa, fb), "Min(a, b)");
            MathAssert.Exact((Numerics.Vector4)Vector4d.Min(b, a), Numerics.Vector4.Min(fb, fa), "Min(b, a)");
            MathAssert.Exact((Numerics.Vector4)Vector4d.Max(a, b), Numerics.Vector4.Max(fa, fb), "Max(a, b)");
            MathAssert.Exact((Numerics.Vector4)Vector4d.Max(b, a), Numerics.Vector4.Max(fb, fa), "Max(b, a)");
            MathAssert.Exact((Numerics.Vector4)Vector4d.Clamp(a, b, c), Numerics.Vector4.Clamp(fa, fb, fc), "Clamp(a, b, c)");
            MathAssert.Exact((Numerics.Vector4)Vector4d.Clamp(c, a, b), Numerics.Vector4.Clamp(fc, fa, fb), "Clamp(c, a, b)");
        });
    }
}
