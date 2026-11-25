using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using CoolEngine.Mathematics.Tests.TestSupport;
using Numerics = System.Numerics;

namespace CoolEngine.Mathematics.Tests;

[TestFixture]
public sealed class Vector4Tests
{
    [Test]
    public void Layout_SizeAndFieldOrder_MatchSizeInBytes()
    {
        var vector = new Vector4 { X = 1f, Y = 2f, Z = 3f, W = 4f };

        var memory = MemoryMarshal.Cast<Vector4, float>(new ReadOnlySpan<Vector4>(in vector)).ToArray();

        Assert.Multiple(() =>
        {
            Assert.That(Unsafe.SizeOf<Vector4>(), Is.EqualTo(Vector4.SizeInBytes));
            Assert.That(Unsafe.SizeOf<Vector4>(), Is.EqualTo(16));
            Assert.That(memory, Is.EqualTo(new[] { 1f, 2f, 3f, 4f }));
        });
    }

    [Test]
    public void Constructor_Components_SetsFields()
    {
        Assert.Multiple(() =>
        {
            MathAssert.Exact(MathAssert.Components(new Vector4(-2.5f)), [-2.5f, -2.5f, -2.5f, -2.5f], "Vector4(value)");
            MathAssert.Exact(MathAssert.Components(new Vector4(1f, -2f, 3.5f, -4f)), [1f, -2f, 3.5f, -4f], "Vector4(x, y, z, w)");
            MathAssert.Exact(MathAssert.Components(new Vector4(new Vector2(4f, -5f), 6f, 7f)), [4f, -5f, 6f, 7f], "Vector4(xy, z, w)");
            MathAssert.Exact(MathAssert.Components(new Vector4(new Vector3(8f, 9f, -10f), 11f)), [8f, 9f, -10f, 11f], "Vector4(xyz, w)");
        });
    }

    [Test]
    public void Constants_ZeroOneUnits_HaveExpectedComponents()
    {
        Assert.Multiple(() =>
        {
            MathAssert.Exact(MathAssert.Components(Vector4.Zero), [0f, 0f, 0f, 0f], nameof(Vector4.Zero));
            MathAssert.Exact(MathAssert.Components(Vector4.One), [1f, 1f, 1f, 1f], nameof(Vector4.One));
            MathAssert.Exact(MathAssert.Components(Vector4.UnitX), [1f, 0f, 0f, 0f], nameof(Vector4.UnitX));
            MathAssert.Exact(MathAssert.Components(Vector4.UnitY), [0f, 1f, 0f, 0f], nameof(Vector4.UnitY));
            MathAssert.Exact(MathAssert.Components(Vector4.UnitZ), [0f, 0f, 1f, 0f], nameof(Vector4.UnitZ));
            MathAssert.Exact(MathAssert.Components(Vector4.UnitW), [0f, 0f, 0f, 1f], nameof(Vector4.UnitW));
        });
    }

    [Test]
    public void Indexer_ValidIndex_ReadsAndWritesComponent()
    {
        var source = new Vector4(1f, 2f, 3f, 4f);

        Assert.Multiple(() =>
        {
            MathAssert.Exact([source[0], source[1], source[2], source[3]], [1f, 2f, 3f, 4f], "get");

            for (var index = 0; index < 4; index++)
            {
                var vector = Vector4.Zero;
                vector[index] = 7f;

                var expected = new float[4];
                expected[index] = 7f;
                MathAssert.Exact(MathAssert.Components(vector), expected, $"set [{index}]");
                MathAssert.Exact(vector[index], 7f, $"get [{index}] after set");
            }
        });
    }

    [TestCase(-1)]
    [TestCase(4)]
    public void Indexer_OutOfRange_Throws(int index)
    {
        var vector = new Vector4(1f, 2f, 3f, 4f);

        Assert.Multiple(() =>
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => _ = vector[index], "get");
            Assert.Throws<ArgumentOutOfRangeException>(() => vector[index] = 0f, "set");
        });
    }

    [Test]
    public void Swizzles_GetAndSet_MapToComponents()
    {
        var vector = new Vector4(1f, 2f, 3f, 4f);

        var xy = vector.Xy;
        var xyz = vector.Xyz;
        var xySet = vector;
        xySet.Xy = new Vector2(-5f, -6f);
        var xyzSet = vector;
        xyzSet.Xyz = new Vector3(-7f, -8f, -9f);

        Assert.Multiple(() =>
        {
            MathAssert.Exact(MathAssert.Components(xy), [1f, 2f], "get Xy");
            MathAssert.Exact(MathAssert.Components(xyz), [1f, 2f, 3f], "get Xyz");
            MathAssert.Exact(MathAssert.Components(xySet), [-5f, -6f, 3f, 4f], "set Xy");
            MathAssert.Exact(MathAssert.Components(xyzSet), [-7f, -8f, -9f, 4f], "set Xyz");
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
                var a = random.NextVector4();
                var b = random.NextVector4();
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
                var a = random.NextVector4();
                var b = random.NextVector4();

                var expected = Numerics.Vector4.Dot(Oracle.ToNumerics(a), Oracle.ToNumerics(b));

                MathAssert.Near(Vector4.Dot(a, b), expected, MathAssert.SumsOfProducts,
                    MathAssert.SumOfAbs(a.X * b.X, a.Y * b.Y, a.Z * b.Z, a.W * b.W), Inputs.Label(i));
            }
        });
    }

    [Test]
    public void Length_ThreeFour_IsFive()
    {
        MathAssert.Exact(new Vector4(3f, 4f, 0f, 0f).Length, 5f);
    }

    [Test]
    public void Length_RandomInputs_MatchesSystemNumerics()
    {
        var random = Inputs.CreateRandom();

        Assert.Multiple(() =>
        {
            for (var i = 0; i < Inputs.Count; i++)
            {
                var a = random.NextVector4();
                var na = Oracle.ToNumerics(a);
                var label = Inputs.Label(i);

                var length = na.Length();
                MathAssert.Near(a.Length, length, MathAssert.SumsOfProducts, MathAssert.AtLeastOne(length), label + " Length");
                MathAssert.Near(a.LengthSquared, na.LengthSquared(), MathAssert.SumsOfProducts,
                    MathAssert.SumOfAbs(a.X * a.X, a.Y * a.Y, a.Z * a.Z, a.W * a.W), label + " LengthSquared");
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
                var a = random.NextVector4();

                var expected = Oracle.FromNumerics(Numerics.Vector4.Normalize(Oracle.ToNumerics(a)));

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
                var a = random.NextVector4();
                var expected = a.Normalized();

                var inPlace = a;
                inPlace.Normalize();

                MathAssert.Exact(inPlace, expected, Inputs.Label(i) + " Normalize()");
                MathAssert.Exact(Vector4.Normalize(a), expected, Inputs.Label(i) + " Normalize(vector)");
            }
        });
    }

    [Test]
    public void Normalized_Zero_IsNaN()
    {
        MathAssert.Exact(Vector4.Zero.Normalized(), new Vector4(float.NaN));
    }

    [Test]
    public void Distance_RandomInputs_MatchesSystemNumerics()
    {
        var random = Inputs.CreateRandom();

        Assert.Multiple(() =>
        {
            for (var i = 0; i < Inputs.Count; i++)
            {
                var a = random.NextVector4();
                var b = random.NextVector4();
                var na = Oracle.ToNumerics(a);
                var nb = Oracle.ToNumerics(b);
                var d = na - nb;
                var label = Inputs.Label(i);

                var distance = Numerics.Vector4.Distance(na, nb);
                MathAssert.Near(Vector4.Distance(a, b), distance, MathAssert.SumsOfProducts, MathAssert.AtLeastOne(distance),
                    label + " Distance");
                MathAssert.Near(Vector4.DistanceSquared(a, b), Numerics.Vector4.DistanceSquared(na, nb), MathAssert.SumsOfProducts,
                    MathAssert.SumOfAbs(d.X * d.X, d.Y * d.Y, d.Z * d.Z, d.W * d.W), label + " DistanceSquared");
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
                var a = random.NextVector4();
                var b = random.NextVector4();
                var na = Oracle.ToNumerics(a);
                var nb = Oracle.ToNumerics(b);
                var label = Inputs.Label(i);

                MathAssert.Exact(Vector4.ComponentMin(a, b), Oracle.FromNumerics(Numerics.Vector4.Min(na, nb)), label + " ComponentMin");
                MathAssert.Exact(Vector4.ComponentMax(a, b), Oracle.FromNumerics(Numerics.Vector4.Max(na, nb)), label + " ComponentMax");
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
                var value = random.NextVector4();
                var p = Oracle.ToNumerics(random.NextVector4());
                var q = Oracle.ToNumerics(random.NextVector4());
                var min = Numerics.Vector4.Min(p, q);
                var max = Numerics.Vector4.Max(p, q);
                var nValue = Oracle.ToNumerics(value);
                var label = Inputs.Label(i);

                MathAssert.Exact(Vector4.Abs(value), Oracle.FromNumerics(Numerics.Vector4.Abs(nValue)), label + " Abs");
                MathAssert.Exact(Vector4.Clamp(value, Oracle.FromNumerics(min), Oracle.FromNumerics(max)),
                    Oracle.FromNumerics(Numerics.Vector4.Clamp(nValue, min, max)), label + " Clamp");
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
                var a = random.NextVector4();
                var b = random.NextVector4();
                var t = random.NextSingle();

                var expected = Oracle.FromNumerics(Numerics.Vector4.Lerp(Oracle.ToNumerics(a), Oracle.ToNumerics(b), t));
                float[] scales =
                [
                    MathAssert.SumOfAbs(a.X, a.X * t, b.X * t),
                    MathAssert.SumOfAbs(a.Y, a.Y * t, b.Y * t),
                    MathAssert.SumOfAbs(a.Z, a.Z * t, b.Z * t),
                    MathAssert.SumOfAbs(a.W, a.W * t, b.W * t),
                ];

                MathAssert.Near(Vector4.Lerp(a, b, t), expected, MathAssert.SumsOfProducts, scales, Inputs.Label(i));
            }
        });
    }

    [Test]
    public void Lerp_TOutsideUnitRange_Extrapolates()
    {
        MathAssert.Near(Vector4.Lerp(Vector4.Zero, Vector4.One * 10f, 2f), Vector4.One * 20f, MathAssert.SumsOfProducts, 20f);
    }

    [Test]
    public void Equality_SameAndDifferent_OperatorsAndEqualsAgree()
    {
        var a = new Vector4(1f, 2f, 3f, 4f);
        var same = new Vector4(1f, 2f, 3f, 4f);

        Assert.Multiple(() =>
        {
            Assert.That(a == same, Is.True, "== same");
            Assert.That(a != same, Is.False, "!= same");
            Assert.That(a.Equals(same), Is.True, "Equals same");
            Assert.That(a.Equals((object)same), Is.True, "Equals(object) same");

            for (var index = 0; index < 4; index++)
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
        var a = new Vector4(float.NaN, 2f, 3f, 4f);
        var b = new Vector4(float.NaN, 2f, 3f, 4f);

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
        var positive = new Vector4(0f, 2f, 3f, 4f);
        var negative = new Vector4(float.NegativeZero, 2f, 3f, 4f);

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
        Assert.That(new Vector4(1.5f, 2f, -3f, 0.25f).ToString(), Is.EqualTo("(1.5, 2, -3, 0.25)"));
    }

    [Test]
    public void SystemNumerics_RoundTrip_IsExact()
    {
        var random = Inputs.CreateRandom();

        Assert.Multiple(() =>
        {
            for (var i = 0; i < Inputs.Count; i++)
            {
                var ours = random.NextVector4();
                var theirs = Oracle.ToNumerics(random.NextVector4());
                var label = Inputs.Label(i);

                Numerics.Vector4 converted = ours;
                Vector4 back = converted;
                Vector4 fromTheirs = theirs;

                MathAssert.Exact([converted.X, converted.Y, converted.Z, converted.W], MathAssert.Components(ours),
                    label + " to System.Numerics");
                MathAssert.Exact(back, ours, label + " round trip");
                MathAssert.Exact(fromTheirs, Oracle.FromNumerics(theirs), label + " from System.Numerics");
            }
        });
    }
}
