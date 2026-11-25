using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using CoolEngine.Mathematics.Tests.TestSupport;
using Numerics = System.Numerics;

namespace CoolEngine.Mathematics.Tests;

[TestFixture]
public sealed class Vector2Tests
{
    [Test]
    public void Layout_SizeAndFieldOrder_MatchSizeInBytes()
    {
        var vector = new Vector2 { X = 1f, Y = 2f };

        var memory = MemoryMarshal.Cast<Vector2, float>(new ReadOnlySpan<Vector2>(in vector)).ToArray();

        Assert.Multiple(() =>
        {
            Assert.That(Unsafe.SizeOf<Vector2>(), Is.EqualTo(Vector2.SizeInBytes));
            Assert.That(Unsafe.SizeOf<Vector2>(), Is.EqualTo(8));
            Assert.That(memory, Is.EqualTo(new[] { 1f, 2f }));
        });
    }

    [Test]
    public void Constructor_Components_SetsFields()
    {
        Assert.Multiple(() =>
        {
            MathAssert.Exact(MathAssert.Components(new Vector2(-2.5f)), [-2.5f, -2.5f], "Vector2(value)");
            MathAssert.Exact(MathAssert.Components(new Vector2(1f, -2f)), [1f, -2f], "Vector2(x, y)");
        });
    }

    [Test]
    public void Constants_ZeroOneUnits_HaveExpectedComponents()
    {
        Assert.Multiple(() =>
        {
            MathAssert.Exact(MathAssert.Components(Vector2.Zero), [0f, 0f], nameof(Vector2.Zero));
            MathAssert.Exact(MathAssert.Components(Vector2.One), [1f, 1f], nameof(Vector2.One));
            MathAssert.Exact(MathAssert.Components(Vector2.UnitX), [1f, 0f], nameof(Vector2.UnitX));
            MathAssert.Exact(MathAssert.Components(Vector2.UnitY), [0f, 1f], nameof(Vector2.UnitY));
        });
    }

    [Test]
    public void Indexer_ValidIndex_ReadsAndWritesComponent()
    {
        var source = new Vector2(1f, 2f);

        Assert.Multiple(() =>
        {
            MathAssert.Exact([source[0], source[1]], [1f, 2f], "get");

            for (var index = 0; index < 2; index++)
            {
                var vector = Vector2.Zero;
                vector[index] = 7f;

                var expected = new float[2];
                expected[index] = 7f;
                MathAssert.Exact(MathAssert.Components(vector), expected, $"set [{index}]");
                MathAssert.Exact(vector[index], 7f, $"get [{index}] after set");
            }
        });
    }

    [TestCase(-1)]
    [TestCase(2)]
    public void Indexer_OutOfRange_Throws(int index)
    {
        var vector = new Vector2(1f, 2f);

        Assert.Multiple(() =>
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => _ = vector[index], "get");
            Assert.Throws<ArgumentOutOfRangeException>(() => vector[index] = 0f, "set");
        });
    }

    [Test]
    public void Arithmetic_RandomInputs_EqualSystemNumericsExactly()
    {
        var random = Inputs.CreateRandom();

        Assert.Multiple(() =>
        {
            for (var i = 0; i < Inputs.Count; i++)
            {
                var a = random.NextVector2();
                var b = random.NextVector2();
                var s = random.NextComponent();
                var na = Oracle.ToNumerics(a);
                var nb = Oracle.ToNumerics(b);
                var label = Inputs.Label(i);

                MathAssert.Exact(a + b, Oracle.FromNumerics(na + nb), label + " a + b");
                MathAssert.Exact(a - b, Oracle.FromNumerics(na - nb), label + " a - b");
                MathAssert.Exact(-a, Oracle.FromNumerics(-na), label + " -a");
                MathAssert.Exact(a * b, Oracle.FromNumerics(na * nb), label + " a * b");
                MathAssert.Exact(a * s, Oracle.FromNumerics(na * s), label + " a * s");
                MathAssert.Exact(s * a, Oracle.FromNumerics(s * na), label + " s * a");
                MathAssert.Exact(a / b, Oracle.FromNumerics(na / nb), label + " a / b");
                MathAssert.Exact(a / s, Oracle.FromNumerics(na / s), label + " a / s");
            }
        });
    }

    [Test]
    public void Dot_RandomInputs_MatchesSystemNumerics()
    {
        var random = Inputs.CreateRandom();

        Assert.Multiple(() =>
        {
            for (var i = 0; i < Inputs.Count; i++)
            {
                var a = random.NextVector2();
                var b = random.NextVector2();

                var expected = Numerics.Vector2.Dot(Oracle.ToNumerics(a), Oracle.ToNumerics(b));

                MathAssert.Near(Vector2.Dot(a, b), expected, MathAssert.SumsOfProducts,
                    MathAssert.SumOfAbs(a.X * b.X, a.Y * b.Y), Inputs.Label(i));
            }
        });
    }

    [Test]
    public void Length_ThreeFour_IsFive()
    {
        MathAssert.Exact(new Vector2(3f, 4f).Length, 5f);
    }

    [Test]
    public void Length_RandomInputs_MatchesSystemNumerics()
    {
        var random = Inputs.CreateRandom();

        Assert.Multiple(() =>
        {
            for (var i = 0; i < Inputs.Count; i++)
            {
                var a = random.NextVector2();
                var na = Oracle.ToNumerics(a);
                var label = Inputs.Label(i);

                var length = na.Length();
                MathAssert.Near(a.Length, length, MathAssert.SumsOfProducts, MathAssert.AtLeastOne(length), label + " Length");
                MathAssert.Near(a.LengthSquared, na.LengthSquared(), MathAssert.SumsOfProducts,
                    MathAssert.SumOfAbs(a.X * a.X, a.Y * a.Y), label + " LengthSquared");
            }
        });
    }

    [Test]
    public void Normalized_RandomInputs_MatchesSystemNumerics()
    {
        var random = Inputs.CreateRandom();

        Assert.Multiple(() =>
        {
            for (var i = 0; i < Inputs.Count; i++)
            {
                var a = random.NextVector2();

                var expected = Oracle.FromNumerics(Numerics.Vector2.Normalize(Oracle.ToNumerics(a)));

                MathAssert.Near(a.Normalized(), expected, MathAssert.SumsOfProducts,
                    MathAssert.AtLeastOne(MathAssert.Components(expected)), Inputs.Label(i));
            }
        });
    }

    [Test]
    public void Normalize_InPlaceAndStatic_EqualNormalized()
    {
        var random = Inputs.CreateRandom();

        Assert.Multiple(() =>
        {
            for (var i = 0; i < Inputs.Count; i++)
            {
                var a = random.NextVector2();
                var expected = a.Normalized();

                var inPlace = a;
                inPlace.Normalize();

                MathAssert.Exact(inPlace, expected, Inputs.Label(i) + " Normalize()");
                MathAssert.Exact(Vector2.Normalize(a), expected, Inputs.Label(i) + " Normalize(vector)");
            }
        });
    }

    [Test]
    public void Normalized_Zero_IsNaN()
    {
        MathAssert.Exact(Vector2.Zero.Normalized(), new Vector2(float.NaN));
    }

    [Test]
    public void Distance_RandomInputs_MatchesSystemNumerics()
    {
        var random = Inputs.CreateRandom();

        Assert.Multiple(() =>
        {
            for (var i = 0; i < Inputs.Count; i++)
            {
                var a = random.NextVector2();
                var b = random.NextVector2();
                var na = Oracle.ToNumerics(a);
                var nb = Oracle.ToNumerics(b);
                var d = na - nb;
                var label = Inputs.Label(i);

                var distance = Numerics.Vector2.Distance(na, nb);
                MathAssert.Near(Vector2.Distance(a, b), distance, MathAssert.SumsOfProducts, MathAssert.AtLeastOne(distance),
                    label + " Distance");
                MathAssert.Near(Vector2.DistanceSquared(a, b), Numerics.Vector2.DistanceSquared(na, nb), MathAssert.SumsOfProducts,
                    MathAssert.SumOfAbs(d.X * d.X, d.Y * d.Y), label + " DistanceSquared");
            }
        });
    }

    [Test]
    public void ComponentMinMax_RandomInputs_EqualSystemNumericsExactly()
    {
        var random = Inputs.CreateRandom();

        Assert.Multiple(() =>
        {
            for (var i = 0; i < Inputs.Count; i++)
            {
                var a = random.NextVector2();
                var b = random.NextVector2();
                var na = Oracle.ToNumerics(a);
                var nb = Oracle.ToNumerics(b);
                var label = Inputs.Label(i);

                MathAssert.Exact(Vector2.ComponentMin(a, b), Oracle.FromNumerics(Numerics.Vector2.Min(na, nb)), label + " ComponentMin");
                MathAssert.Exact(Vector2.ComponentMax(a, b), Oracle.FromNumerics(Numerics.Vector2.Max(na, nb)), label + " ComponentMax");
            }
        });
    }

    [Test]
    public void AbsAndClamp_RandomInputs_EqualSystemNumericsExactly()
    {
        var random = Inputs.CreateRandom();

        Assert.Multiple(() =>
        {
            for (var i = 0; i < Inputs.Count; i++)
            {
                var value = random.NextVector2();
                var p = Oracle.ToNumerics(random.NextVector2());
                var q = Oracle.ToNumerics(random.NextVector2());
                var min = Numerics.Vector2.Min(p, q);
                var max = Numerics.Vector2.Max(p, q);
                var nValue = Oracle.ToNumerics(value);
                var label = Inputs.Label(i);

                MathAssert.Exact(Vector2.Abs(value), Oracle.FromNumerics(Numerics.Vector2.Abs(nValue)), label + " Abs");
                MathAssert.Exact(Vector2.Clamp(value, Oracle.FromNumerics(min), Oracle.FromNumerics(max)),
                    Oracle.FromNumerics(Numerics.Vector2.Clamp(nValue, min, max)), label + " Clamp");
            }
        });
    }

    [Test]
    public void Lerp_RandomInputs_MatchesSystemNumerics()
    {
        var random = Inputs.CreateRandom();

        Assert.Multiple(() =>
        {
            for (var i = 0; i < Inputs.Count; i++)
            {
                var a = random.NextVector2();
                var b = random.NextVector2();
                var t = random.NextSingle();

                var expected = Oracle.FromNumerics(Numerics.Vector2.Lerp(Oracle.ToNumerics(a), Oracle.ToNumerics(b), t));
                float[] scales =
                [
                    MathAssert.SumOfAbs(a.X, a.X * t, b.X * t),
                    MathAssert.SumOfAbs(a.Y, a.Y * t, b.Y * t),
                ];

                MathAssert.Near(Vector2.Lerp(a, b, t), expected, MathAssert.SumsOfProducts, scales, Inputs.Label(i));
            }
        });
    }

    [Test]
    public void Lerp_TOutsideUnitRange_Extrapolates()
    {
        MathAssert.Near(Vector2.Lerp(Vector2.Zero, Vector2.One * 10f, 2f), Vector2.One * 20f, MathAssert.SumsOfProducts, 20f);
    }

    [Test]
    public void Equality_SameAndDifferent_OperatorsAndEqualsAgree()
    {
        var a = new Vector2(1f, 2f);
        var same = new Vector2(1f, 2f);

        Assert.Multiple(() =>
        {
            Assert.That(a == same, Is.True, "== same");
            Assert.That(a != same, Is.False, "!= same");
            Assert.That(a.Equals(same), Is.True, "Equals same");
            Assert.That(a.Equals((object)same), Is.True, "Equals(object) same");

            for (var index = 0; index < 2; index++)
            {
                var different = a;
                different[index] += 1f;

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
        var a = new Vector2(float.NaN, 2f);
        var b = new Vector2(float.NaN, 2f);

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
        var positive = new Vector2(0f, 2f);
        var negative = new Vector2(float.NegativeZero, 2f);

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
        Assert.That(new Vector2(1.5f, 2f).ToString(), Is.EqualTo("(1.5, 2)"));
    }

    [Test]
    public void SystemNumerics_RoundTrip_IsExact()
    {
        var random = Inputs.CreateRandom();

        Assert.Multiple(() =>
        {
            for (var i = 0; i < Inputs.Count; i++)
            {
                var ours = random.NextVector2();
                var theirs = Oracle.ToNumerics(random.NextVector2());
                var label = Inputs.Label(i);

                Numerics.Vector2 converted = ours;
                Vector2 back = converted;
                Vector2 fromTheirs = theirs;

                MathAssert.Exact([converted.X, converted.Y], MathAssert.Components(ours), label + " to System.Numerics");
                MathAssert.Exact(back, ours, label + " round trip");
                MathAssert.Exact(fromTheirs, Oracle.FromNumerics(theirs), label + " from System.Numerics");
            }
        });
    }
}
