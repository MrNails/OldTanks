using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using CoolEngine.Mathematics.Tests.TestSupport;
using Numerics = System.Numerics;
using OpenTKMath = OpenTK.Mathematics;

namespace CoolEngine.Mathematics.Tests;

[TestFixture]
public sealed class Vector2dTests
{
    [Test]
    public void Layout_SizeAndFieldOrder_MatchSizeInBytes()
    {
        var vector = new Vector2d { X = 1.0, Y = 2.0 };

        var memory = MemoryMarshal.Cast<Vector2d, double>(new ReadOnlySpan<Vector2d>(in vector)).ToArray();

        Assert.Multiple(() =>
        {
            Assert.That(Unsafe.SizeOf<Vector2d>(), Is.EqualTo(Vector2d.SizeInBytes));
            Assert.That(Unsafe.SizeOf<Vector2d>(), Is.EqualTo(16));
            Assert.That(memory, Is.EqualTo(new[] { 1.0, 2.0 }));
        });
    }

    [Test]
    public void Constructor_Components_SetsFields()
    {
        Assert.Multiple(() =>
        {
            MathAssert.Exact(MathAssert.Components(new Vector2d(-2.5)), [-2.5, -2.5], "Vector2d(value)");
            MathAssert.Exact(MathAssert.Components(new Vector2d(1.0, -2.0)), [1.0, -2.0], "Vector2d(x, y)");
        });
    }

    [Test]
    public void Constants_ZeroOneUnits_HaveExpectedComponents()
    {
        Assert.Multiple(() =>
        {
            MathAssert.Exact(MathAssert.Components(Vector2d.Zero), [0.0, 0.0], nameof(Vector2d.Zero));
            MathAssert.Exact(MathAssert.Components(Vector2d.One), [1.0, 1.0], nameof(Vector2d.One));
            MathAssert.Exact(MathAssert.Components(Vector2d.UnitX), [1.0, 0.0], nameof(Vector2d.UnitX));
            MathAssert.Exact(MathAssert.Components(Vector2d.UnitY), [0.0, 1.0], nameof(Vector2d.UnitY));
        });
    }

    [Test]
    public void Indexer_ValidIndex_ReadsAndWritesComponent()
    {
        var source = new Vector2d(1.0, 2.0);

        Assert.Multiple(() =>
        {
            MathAssert.Exact([source[0], source[1]], [1.0, 2.0], "get");

            for (var index = 0; index < 2; index++)
            {
                var vector = Vector2d.Zero;
                vector[index] = 7.0;

                var expected = new double[2];
                expected[index] = 7.0;
                MathAssert.Exact(MathAssert.Components(vector), expected, $"set [{index}]");
                MathAssert.Exact(vector[index], 7.0, $"get [{index}] after set");
            }
        });
    }

    [TestCase(-1)]
    [TestCase(2)]
    public void Indexer_OutOfRange_Throws(int index)
    {
        var vector = new Vector2d(1.0, 2.0);

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
                var a = random.NextVector2d();
                var b = random.NextVector2d();
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
                var a = random.NextVector2d();
                var b = random.NextVector2d();

                MathAssert.Near(Vector2d.Dot(a, b), OpenTKMath.Vector2d.Dot(Oracle.ToOpenTK(a), Oracle.ToOpenTK(b)), MathAssert.SumsOfProductsD,
                    MathAssert.SumOfAbs(a.X * b.X, a.Y * b.Y), Inputs.Label(i));
            }
        });
    }

    [Test]
    public void Length_ThreeFour_IsFive()
    {
        MathAssert.Exact(new Vector2d(3.0, 4.0).Length(), 5.0);
    }

    [Test]
    public void Length_RandomInputs_MatchesOpenTK()
    {
        var random = Inputs.CreateRandom();

        Assert.Multiple(() =>
        {
            for (var i = 0; i < Inputs.Count; i++)
            {
                var a = random.NextVector2d();
                var oa = Oracle.ToOpenTK(a);
                var label = Inputs.Label(i);

                MathAssert.Near(a.Length(), oa.Length, MathAssert.SumsOfProductsD, MathAssert.AtLeastOne(oa.Length), label + " Length");
                MathAssert.Near(a.LengthSquared(), oa.LengthSquared, MathAssert.SumsOfProductsD, MathAssert.SumOfAbs(a.X * a.X, a.Y * a.Y),
                    label + " LengthSquared");
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
                var a = random.NextVector2d();

                var expected = Oracle.FromOpenTK(OpenTKMath.Vector2d.Normalize(Oracle.ToOpenTK(a)));

                MathAssert.Near(Vector2d.Normalize(a), expected, MathAssert.SumsOfProductsD,
                    MathAssert.AtLeastOne(MathAssert.Components(expected)), Inputs.Label(i));
            }
        });
    }

    [Test]
    public void Normalize_Zero_IsNaN()
    {
        MathAssert.Exact(Vector2d.Normalize(Vector2d.Zero), new Vector2d(double.NaN));
    }

    [Test]
    public void Distance_RandomInputs_MatchesOpenTK()
    {
        var random = Inputs.CreateRandom();

        Assert.Multiple(() =>
        {
            for (var i = 0; i < Inputs.Count; i++)
            {
                var a = random.NextVector2d();
                var b = random.NextVector2d();
                var d = a - b;
                var label = Inputs.Label(i);

                var distance = OpenTKMath.Vector2d.Distance(Oracle.ToOpenTK(a), Oracle.ToOpenTK(b));
                MathAssert.Near(Vector2d.Distance(a, b), distance, MathAssert.SumsOfProductsD, MathAssert.AtLeastOne(distance),
                    label + " Distance");
                MathAssert.Near(Vector2d.DistanceSquared(a, b), OpenTKMath.Vector2d.DistanceSquared(Oracle.ToOpenTK(a), Oracle.ToOpenTK(b)),
                    MathAssert.SumsOfProductsD, MathAssert.SumOfAbs(d.X * d.X, d.Y * d.Y), label + " DistanceSquared");
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
                var a = random.NextVector2d();
                var b = random.NextVector2d();
                var oa = Oracle.ToOpenTK(a);
                var ob = Oracle.ToOpenTK(b);
                var label = Inputs.Label(i);

                MathAssert.Exact(Vector2d.Min(a, b), Oracle.FromOpenTK(OpenTKMath.Vector2d.ComponentMin(oa, ob)), label + " Min");
                MathAssert.Exact(Vector2d.Max(a, b), Oracle.FromOpenTK(OpenTKMath.Vector2d.ComponentMax(oa, ob)), label + " Max");
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
                var value = random.NextVector2d();
                var p = random.NextVector2d();
                var q = random.NextVector2d();
                var min = Vector2d.Min(p, q);
                var max = Vector2d.Max(p, q);
                var label = Inputs.Label(i);

                MathAssert.Exact(Vector2d.Abs(value), Oracle.FromOpenTK(OpenTKMath.Vector2d.Abs(Oracle.ToOpenTK(value))), label + " Abs");
                MathAssert.Exact(Vector2d.Clamp(value, min, max),
                    Oracle.FromOpenTK(OpenTKMath.Vector2d.Clamp(Oracle.ToOpenTK(value), Oracle.ToOpenTK(min), Oracle.ToOpenTK(max))), label + " Clamp");
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
                var a = random.NextVector2d();
                var b = random.NextVector2d();
                var t = random.NextDouble();

                var expected = Oracle.FromOpenTK(OpenTKMath.Vector2d.Lerp(Oracle.ToOpenTK(a), Oracle.ToOpenTK(b), t));
                double[] scales = [MathAssert.SumOfAbs(a.X, a.X * t, b.X * t), MathAssert.SumOfAbs(a.Y, a.Y * t, b.Y * t)];

                MathAssert.Near(Vector2d.Lerp(a, b, t), expected, MathAssert.SumsOfProductsD, scales, Inputs.Label(i));
            }
        });
    }

    [Test]
    public void Lerp_TOutsideUnitRange_Extrapolates()
    {
        MathAssert.Near(Vector2d.Lerp(Vector2d.Zero, Vector2d.One * 10.0, 2.0), Vector2d.One * 20.0, MathAssert.SumsOfProductsD, 20.0);
    }

    [Test]
    public void Equality_SameAndDifferent_OperatorsAndEqualsAgree()
    {
        var a = new Vector2d(1.0, 2.0);
        var same = new Vector2d(1.0, 2.0);

        Assert.Multiple(() =>
        {
            Assert.That(a == same, Is.True, "== same");
            Assert.That(a != same, Is.False, "!= same");
            Assert.That(a.Equals(same), Is.True, "Equals same");
            Assert.That(a.Equals((object)same), Is.True, "Equals(object) same");

            for (var index = 0; index < 2; index++)
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
        var a = new Vector2d(double.NaN, 2.0);
        var b = new Vector2d(double.NaN, 2.0);

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
        var positive = new Vector2d(0.0, 2.0);
        var negative = new Vector2d(double.NegativeZero, 2.0);

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
        Assert.That(new Vector2d(1.5, 2.0).ToString(), Is.EqualTo("(1.5, 2)"));
    }

    [Test]
    public void FloatConversion_Widening_IsExact()
    {
        var random = Inputs.CreateRandom();

        Assert.Multiple(() =>
        {
            for (var i = 0; i < Inputs.Count; i++)
            {
                var single = random.NextVector2();
                var label = Inputs.Label(i);

                Vector2d widened = single;
                var back = (Numerics.Vector2)widened;

                MathAssert.Exact(MathAssert.Components(widened), [single.X, single.Y], label + " widened");
                MathAssert.Exact(back, single, label + " float -> double -> float");
            }
        });
    }

    [Test]
    public void FloatConversion_Narrowing_RoundsToNearest()
    {
        var narrowed = (Numerics.Vector2)new Vector2d(1.0 + Math.Pow(2, -30), 0.1);

        MathAssert.Exact(narrowed, new Numerics.Vector2(1f, 0.1f));
    }

    [Test]
    public void Negation_SignedZeros_FlipSignBit()
    {
        MathAssert.Exact(-new Vector2d(0.0, double.NegativeZero), new Vector2d(double.NegativeZero, 0.0));
    }

    [Test]
    public void MinMaxClamp_NaNAndSignedZeros_MatchSystemNumerics()
    {
        // Hand values mixing NaN, +0, -0 and 1: the double results equal System.Numerics' float results.
        var pairs = new (Vector2d A, Vector2d B, Vector2d C)[]
        {
            (new Vector2d(double.NaN, 0.0), new Vector2d(1.0, double.NegativeZero), new Vector2d(0.0, 1.0)),
            (new Vector2d(double.NegativeZero, 1.0), new Vector2d(0.0, double.NaN), new Vector2d(double.NaN, double.NegativeZero)),
        };

        Assert.Multiple(() =>
        {
            for (var i = 0; i < pairs.Length; i++)
            {
                var (a, b, c) = pairs[i];
                var fa = (Numerics.Vector2)a;
                var fb = (Numerics.Vector2)b;
                var fc = (Numerics.Vector2)c;

                MathAssert.Exact((Numerics.Vector2)Vector2d.Min(a, b), Numerics.Vector2.Min(fa, fb), $"case {i} Min(a, b)");
                MathAssert.Exact((Numerics.Vector2)Vector2d.Min(b, a), Numerics.Vector2.Min(fb, fa), $"case {i} Min(b, a)");
                MathAssert.Exact((Numerics.Vector2)Vector2d.Max(a, b), Numerics.Vector2.Max(fa, fb), $"case {i} Max(a, b)");
                MathAssert.Exact((Numerics.Vector2)Vector2d.Max(b, a), Numerics.Vector2.Max(fb, fa), $"case {i} Max(b, a)");
                MathAssert.Exact((Numerics.Vector2)Vector2d.Clamp(a, b, c), Numerics.Vector2.Clamp(fa, fb, fc), $"case {i} Clamp(a, b, c)");
                MathAssert.Exact((Numerics.Vector2)Vector2d.Clamp(c, a, b), Numerics.Vector2.Clamp(fc, fa, fb), $"case {i} Clamp(c, a, b)");
            }
        });
    }
}
