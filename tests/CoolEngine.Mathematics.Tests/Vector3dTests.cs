using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using CoolEngine.Mathematics.Tests.TestSupport;
using Numerics = System.Numerics;
using OpenTKMath = OpenTK.Mathematics;

namespace CoolEngine.Mathematics.Tests;

[TestFixture]
public sealed class Vector3dTests
{
    [Test]
    public void Layout_SizeAndFieldOrder_MatchSizeInBytes()
    {
        var vector = new Vector3d { X = 1.0, Y = 2.0, Z = 3.0 };

        var memory = MemoryMarshal.Cast<Vector3d, double>(new ReadOnlySpan<Vector3d>(in vector)).ToArray();

        Assert.Multiple(() =>
        {
            Assert.That(Unsafe.SizeOf<Vector3d>(), Is.EqualTo(Vector3d.SizeInBytes));
            Assert.That(Unsafe.SizeOf<Vector3d>(), Is.EqualTo(24));
            Assert.That(memory, Is.EqualTo(new[] { 1.0, 2.0, 3.0 }));
        });
    }

    [Test]
    public void Constructor_Components_SetsFields()
    {
        Assert.Multiple(() =>
        {
            MathAssert.Exact(MathAssert.Components(new Vector3d(-2.5)), [-2.5, -2.5, -2.5], "Vector3d(value)");
            MathAssert.Exact(MathAssert.Components(new Vector3d(1.0, -2.0, 3.5)), [1.0, -2.0, 3.5], "Vector3d(x, y, z)");
            MathAssert.Exact(MathAssert.Components(new Vector3d(new Vector2d(4.0, -5.0), 6.0)), [4.0, -5.0, 6.0], "Vector3d(xy, z)");
        });
    }

    [Test]
    public void Constants_ZeroOneUnits_HaveExpectedComponents()
    {
        Assert.Multiple(() =>
        {
            MathAssert.Exact(MathAssert.Components(Vector3d.Zero), [0.0, 0.0, 0.0], nameof(Vector3d.Zero));
            MathAssert.Exact(MathAssert.Components(Vector3d.One), [1.0, 1.0, 1.0], nameof(Vector3d.One));
            MathAssert.Exact(MathAssert.Components(Vector3d.UnitX), [1.0, 0.0, 0.0], nameof(Vector3d.UnitX));
            MathAssert.Exact(MathAssert.Components(Vector3d.UnitY), [0.0, 1.0, 0.0], nameof(Vector3d.UnitY));
            MathAssert.Exact(MathAssert.Components(Vector3d.UnitZ), [0.0, 0.0, 1.0], nameof(Vector3d.UnitZ));
        });
    }

    [Test]
    public void Indexer_ValidIndex_ReadsAndWritesComponent()
    {
        var source = new Vector3d(1.0, 2.0, 3.0);

        Assert.Multiple(() =>
        {
            MathAssert.Exact([source[0], source[1], source[2]], [1.0, 2.0, 3.0], "get");

            for (var index = 0; index < 3; index++)
            {
                var vector = Vector3d.Zero;
                vector[index] = 7.0;

                var expected = new double[3];
                expected[index] = 7.0;
                MathAssert.Exact(MathAssert.Components(vector), expected, $"set [{index}]");
                MathAssert.Exact(vector[index], 7.0, $"get [{index}] after set");
            }
        });
    }

    [TestCase(-1)]
    [TestCase(3)]
    public void Indexer_OutOfRange_Throws(int index)
    {
        var vector = new Vector3d(1.0, 2.0, 3.0);

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
                var a = random.NextVector3d();
                var b = random.NextVector3d();
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
                var a = random.NextVector3d();
                var b = random.NextVector3d();

                MathAssert.Near(Vector3d.Dot(a, b), OpenTKMath.Vector3d.Dot(Oracle.ToOpenTK(a), Oracle.ToOpenTK(b)), MathAssert.SumsOfProductsD,
                    MathAssert.SumOfAbs(a.X * b.X, a.Y * b.Y, a.Z * b.Z), Inputs.Label(i));
            }
        });
    }

    [Test]
    public void Cross_RandomInputs_MatchesOpenTK()
    {
        var random = Inputs.CreateRandom();

        Assert.Multiple(() =>
        {
            for (var i = 0; i < Inputs.Count; i++)
            {
                var a = random.NextVector3d();
                var b = random.NextVector3d();

                var expected = Oracle.FromOpenTK(OpenTKMath.Vector3d.Cross(Oracle.ToOpenTK(a), Oracle.ToOpenTK(b)));
                double[] scales =
                [
                    MathAssert.SumOfAbs(a.Y * b.Z, a.Z * b.Y),
                    MathAssert.SumOfAbs(a.Z * b.X, a.X * b.Z),
                    MathAssert.SumOfAbs(a.X * b.Y, a.Y * b.X),
                ];

                MathAssert.Near(Vector3d.Cross(a, b), expected, MathAssert.SumsOfProductsD, scales, Inputs.Label(i));
            }
        });
    }

    [Test]
    public void Cross_UnitXUnitY_IsUnitZ()
    {
        MathAssert.Exact(Vector3d.Cross(Vector3d.UnitX, Vector3d.UnitY), Vector3d.UnitZ);
    }

    [Test]
    public void Length_ThreeFour_IsFive()
    {
        MathAssert.Exact(new Vector3d(3.0, 4.0, 0.0).Length(), 5.0);
    }

    [Test]
    public void Length_RandomInputs_MatchesOpenTK()
    {
        var random = Inputs.CreateRandom();

        Assert.Multiple(() =>
        {
            for (var i = 0; i < Inputs.Count; i++)
            {
                var a = random.NextVector3d();
                var oa = Oracle.ToOpenTK(a);
                var label = Inputs.Label(i);

                MathAssert.Near(a.Length(), oa.Length, MathAssert.SumsOfProductsD, MathAssert.AtLeastOne(oa.Length), label + " Length");
                MathAssert.Near(a.LengthSquared(), oa.LengthSquared, MathAssert.SumsOfProductsD,
                    MathAssert.SumOfAbs(a.X * a.X, a.Y * a.Y, a.Z * a.Z), label + " LengthSquared");
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
                var a = random.NextVector3d();

                var expected = Oracle.FromOpenTK(OpenTKMath.Vector3d.Normalize(Oracle.ToOpenTK(a)));

                MathAssert.Near(Vector3d.Normalize(a), expected, MathAssert.SumsOfProductsD,
                    MathAssert.AtLeastOne(MathAssert.Components(expected)), Inputs.Label(i));
            }
        });
    }

    [Test]
    public void Normalize_Zero_IsNaN()
    {
        MathAssert.Exact(Vector3d.Normalize(Vector3d.Zero), new Vector3d(double.NaN));
    }

    [Test]
    public void Distance_RandomInputs_MatchesOpenTK()
    {
        var random = Inputs.CreateRandom();

        Assert.Multiple(() =>
        {
            for (var i = 0; i < Inputs.Count; i++)
            {
                var a = random.NextVector3d();
                var b = random.NextVector3d();
                var d = a - b;
                var label = Inputs.Label(i);

                var distance = OpenTKMath.Vector3d.Distance(Oracle.ToOpenTK(a), Oracle.ToOpenTK(b));
                MathAssert.Near(Vector3d.Distance(a, b), distance, MathAssert.SumsOfProductsD, MathAssert.AtLeastOne(distance),
                    label + " Distance");
                MathAssert.Near(Vector3d.DistanceSquared(a, b), OpenTKMath.Vector3d.DistanceSquared(Oracle.ToOpenTK(a), Oracle.ToOpenTK(b)),
                    MathAssert.SumsOfProductsD, MathAssert.SumOfAbs(d.X * d.X, d.Y * d.Y, d.Z * d.Z), label + " DistanceSquared");
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
                var a = random.NextVector3d();
                var b = random.NextVector3d();
                var oa = Oracle.ToOpenTK(a);
                var ob = Oracle.ToOpenTK(b);
                var label = Inputs.Label(i);

                MathAssert.Exact(Vector3d.Min(a, b), Oracle.FromOpenTK(OpenTKMath.Vector3d.ComponentMin(oa, ob)), label + " Min");
                MathAssert.Exact(Vector3d.Max(a, b), Oracle.FromOpenTK(OpenTKMath.Vector3d.ComponentMax(oa, ob)), label + " Max");
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
                var value = random.NextVector3d();
                var p = random.NextVector3d();
                var q = random.NextVector3d();
                var min = Vector3d.Min(p, q);
                var max = Vector3d.Max(p, q);
                var label = Inputs.Label(i);

                MathAssert.Exact(Vector3d.Abs(value), Oracle.FromOpenTK(OpenTKMath.Vector3d.Abs(Oracle.ToOpenTK(value))), label + " Abs");
                MathAssert.Exact(Vector3d.Clamp(value, min, max),
                    Oracle.FromOpenTK(OpenTKMath.Vector3d.Clamp(Oracle.ToOpenTK(value), Oracle.ToOpenTK(min), Oracle.ToOpenTK(max))), label + " Clamp");
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
                var a = random.NextVector3d();
                var b = random.NextVector3d();
                var t = random.NextDouble();

                var expected = Oracle.FromOpenTK(OpenTKMath.Vector3d.Lerp(Oracle.ToOpenTK(a), Oracle.ToOpenTK(b), t));
                double[] scales =
                [
                    MathAssert.SumOfAbs(a.X, a.X * t, b.X * t),
                    MathAssert.SumOfAbs(a.Y, a.Y * t, b.Y * t),
                    MathAssert.SumOfAbs(a.Z, a.Z * t, b.Z * t),
                ];

                MathAssert.Near(Vector3d.Lerp(a, b, t), expected, MathAssert.SumsOfProductsD, scales, Inputs.Label(i));
            }
        });
    }

    [Test]
    public void Lerp_TOutsideUnitRange_Extrapolates()
    {
        MathAssert.Near(Vector3d.Lerp(Vector3d.Zero, Vector3d.One * 10.0, 2.0), Vector3d.One * 20.0, MathAssert.SumsOfProductsD, 20.0);
    }

    [Test]
    public void Equality_SameAndDifferent_OperatorsAndEqualsAgree()
    {
        var a = new Vector3d(1.0, 2.0, 3.0);
        var same = new Vector3d(1.0, 2.0, 3.0);

        Assert.Multiple(() =>
        {
            Assert.That(a == same, Is.True, "== same");
            Assert.That(a != same, Is.False, "!= same");
            Assert.That(a.Equals(same), Is.True, "Equals same");
            Assert.That(a.Equals((object)same), Is.True, "Equals(object) same");

            for (var index = 0; index < 3; index++)
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
        var a = new Vector3d(double.NaN, 2.0, 3.0);
        var b = new Vector3d(double.NaN, 2.0, 3.0);

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
        var positive = new Vector3d(0.0, 2.0, 3.0);
        var negative = new Vector3d(double.NegativeZero, 2.0, 3.0);

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
        Assert.That(new Vector3d(1.5, 2.0, -3.0).ToString(), Is.EqualTo("(1.5, 2, -3)"));
    }

    [Test]
    public void FloatConversion_Widening_IsExact()
    {
        var random = Inputs.CreateRandom();

        Assert.Multiple(() =>
        {
            for (var i = 0; i < Inputs.Count; i++)
            {
                var single = random.NextVector3();
                var label = Inputs.Label(i);

                Vector3d widened = single;
                var back = (Numerics.Vector3)widened;

                MathAssert.Exact(MathAssert.Components(widened), [single.X, single.Y, single.Z], label + " widened");
                MathAssert.Exact(back, single, label + " float -> double -> float");
            }
        });
    }

    [Test]
    public void FloatConversion_Narrowing_RoundsToNearest()
    {
        var narrowed = (Numerics.Vector3)new Vector3d(1.0 + Math.Pow(2, -30), 0.1, -0.1);

        MathAssert.Exact(narrowed, new Numerics.Vector3(1f, 0.1f, -0.1f));
    }

    [Test]
    public void Negation_SignedZeros_FlipSignBit()
    {
        MathAssert.Exact(-new Vector3d(0.0, double.NegativeZero, 1.0), new Vector3d(double.NegativeZero, 0.0, -1.0));
    }

    [Test]
    public void MinMaxClamp_NaNAndSignedZeros_MatchSystemNumerics()
    {
        // Hand values mixing NaN, +0, -0 and 1: the double results equal System.Numerics' float results.
        var a = new Vector3d(double.NaN, 0.0, double.NegativeZero);
        var b = new Vector3d(1.0, double.NegativeZero, 0.0);
        var c = new Vector3d(0.0, 1.0, double.NaN);
        var fa = (Numerics.Vector3)a;
        var fb = (Numerics.Vector3)b;
        var fc = (Numerics.Vector3)c;

        Assert.Multiple(() =>
        {
            MathAssert.Exact((Numerics.Vector3)Vector3d.Min(a, b), Numerics.Vector3.Min(fa, fb), "Min(a, b)");
            MathAssert.Exact((Numerics.Vector3)Vector3d.Min(b, a), Numerics.Vector3.Min(fb, fa), "Min(b, a)");
            MathAssert.Exact((Numerics.Vector3)Vector3d.Max(a, b), Numerics.Vector3.Max(fa, fb), "Max(a, b)");
            MathAssert.Exact((Numerics.Vector3)Vector3d.Max(b, a), Numerics.Vector3.Max(fb, fa), "Max(b, a)");
            MathAssert.Exact((Numerics.Vector3)Vector3d.Clamp(a, b, c), Numerics.Vector3.Clamp(fa, fb, fc), "Clamp(a, b, c)");
            MathAssert.Exact((Numerics.Vector3)Vector3d.Clamp(c, a, b), Numerics.Vector3.Clamp(fc, fa, fb), "Clamp(c, a, b)");
        });
    }
}
