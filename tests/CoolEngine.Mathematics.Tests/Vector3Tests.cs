using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using CoolEngine.Mathematics.Tests.TestSupport;
using Numerics = System.Numerics;

namespace CoolEngine.Mathematics.Tests;

[TestFixture]
public sealed class Vector3Tests
{
    [Test]
    public void Layout_SizeAndFieldOrder_MatchSizeInBytes()
    {
        var vector = new Vector3 { X = 1f, Y = 2f, Z = 3f };

        var memory = MemoryMarshal.Cast<Vector3, float>(new ReadOnlySpan<Vector3>(in vector)).ToArray();

        Assert.Multiple(() =>
        {
            Assert.That(Unsafe.SizeOf<Vector3>(), Is.EqualTo(Vector3.SizeInBytes));
            Assert.That(Unsafe.SizeOf<Vector3>(), Is.EqualTo(12));
            Assert.That(memory, Is.EqualTo(new[] { 1f, 2f, 3f }));
        });
    }

    [Test]
    public void Constructor_Components_SetsFields()
    {
        Assert.Multiple(() =>
        {
            MathAssert.Exact(MathAssert.Components(new Vector3(-2.5f)), [-2.5f, -2.5f, -2.5f], "Vector3(value)");
            MathAssert.Exact(MathAssert.Components(new Vector3(1f, -2f, 3.5f)), [1f, -2f, 3.5f], "Vector3(x, y, z)");
            MathAssert.Exact(MathAssert.Components(new Vector3(new Vector2(4f, -5f), 6f)), [4f, -5f, 6f], "Vector3(xy, z)");
        });
    }

    [Test]
    public void Constants_ZeroOneUnits_HaveExpectedComponents()
    {
        Assert.Multiple(() =>
        {
            MathAssert.Exact(MathAssert.Components(Vector3.Zero), [0f, 0f, 0f], nameof(Vector3.Zero));
            MathAssert.Exact(MathAssert.Components(Vector3.One), [1f, 1f, 1f], nameof(Vector3.One));
            MathAssert.Exact(MathAssert.Components(Vector3.UnitX), [1f, 0f, 0f], nameof(Vector3.UnitX));
            MathAssert.Exact(MathAssert.Components(Vector3.UnitY), [0f, 1f, 0f], nameof(Vector3.UnitY));
            MathAssert.Exact(MathAssert.Components(Vector3.UnitZ), [0f, 0f, 1f], nameof(Vector3.UnitZ));
        });
    }

    [Test]
    public void Indexer_ValidIndex_ReadsAndWritesComponent()
    {
        var source = new Vector3(1f, 2f, 3f);

        Assert.Multiple(() =>
        {
            MathAssert.Exact([source[0], source[1], source[2]], [1f, 2f, 3f], "get");

            for (var index = 0; index < 3; index++)
            {
                var vector = Vector3.Zero;
                vector[index] = 7f;

                var expected = new float[3];
                expected[index] = 7f;
                MathAssert.Exact(MathAssert.Components(vector), expected, $"set [{index}]");
                MathAssert.Exact(vector[index], 7f, $"get [{index}] after set");
            }
        });
    }

    [TestCase(-1)]
    [TestCase(3)]
    public void Indexer_OutOfRange_Throws(int index)
    {
        var vector = new Vector3(1f, 2f, 3f);

        Assert.Multiple(() =>
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => _ = vector[index], "get");
            Assert.Throws<ArgumentOutOfRangeException>(() => vector[index] = 0f, "set");
        });
    }

    [Test]
    public void Swizzles_GetAndSet_MapToComponents()
    {
        var vector = new Vector3(1f, 2f, 3f);

        var xy = vector.Xy;
        vector.Xy = new Vector2(-4f, -5f);

        Assert.Multiple(() =>
        {
            MathAssert.Exact(MathAssert.Components(xy), [1f, 2f], "get Xy");
            MathAssert.Exact(MathAssert.Components(vector), [-4f, -5f, 3f], "set Xy");
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
                var a = random.NextVector3();
                var b = random.NextVector3();
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
                var a = random.NextVector3();
                var b = random.NextVector3();

                var expected = Numerics.Vector3.Dot(Oracle.ToNumerics(a), Oracle.ToNumerics(b));

                MathAssert.Near(Vector3.Dot(a, b), expected, MathAssert.SumsOfProducts,
                    MathAssert.SumOfAbs(a.X * b.X, a.Y * b.Y, a.Z * b.Z), Inputs.Label(i));
            }
        });
    }

    [Test]
    public void Cross_RandomInputs_MatchesSystemNumerics()
    {
        var random = Inputs.CreateRandom();

        Assert.Multiple(() =>
        {
            for (var i = 0; i < Inputs.Count; i++)
            {
                var a = random.NextVector3();
                var b = random.NextVector3();

                var expected = Oracle.FromNumerics(Numerics.Vector3.Cross(Oracle.ToNumerics(a), Oracle.ToNumerics(b)));
                float[] scales =
                [
                    MathAssert.SumOfAbs(a.Y * b.Z, a.Z * b.Y),
                    MathAssert.SumOfAbs(a.Z * b.X, a.X * b.Z),
                    MathAssert.SumOfAbs(a.X * b.Y, a.Y * b.X),
                ];

                MathAssert.Near(Vector3.Cross(a, b), expected, MathAssert.SumsOfProducts, scales, Inputs.Label(i));
            }
        });
    }

    [Test]
    public void Cross_UnitXUnitY_IsUnitZ()
    {
        MathAssert.Exact(Vector3.Cross(Vector3.UnitX, Vector3.UnitY), Vector3.UnitZ);
    }

    [Test]
    public void Length_ThreeFour_IsFive()
    {
        MathAssert.Exact(new Vector3(3f, 4f, 0f).Length, 5f);
    }

    [Test]
    public void Length_RandomInputs_MatchesSystemNumerics()
    {
        var random = Inputs.CreateRandom();

        Assert.Multiple(() =>
        {
            for (var i = 0; i < Inputs.Count; i++)
            {
                var a = random.NextVector3();
                var na = Oracle.ToNumerics(a);
                var label = Inputs.Label(i);

                var length = na.Length();
                MathAssert.Near(a.Length, length, MathAssert.SumsOfProducts, MathAssert.AtLeastOne(length), label + " Length");
                MathAssert.Near(a.LengthSquared, na.LengthSquared(), MathAssert.SumsOfProducts,
                    MathAssert.SumOfAbs(a.X * a.X, a.Y * a.Y, a.Z * a.Z), label + " LengthSquared");
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
                var a = random.NextVector3();

                var expected = Oracle.FromNumerics(Numerics.Vector3.Normalize(Oracle.ToNumerics(a)));

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
                var a = random.NextVector3();
                var expected = a.Normalized();

                var inPlace = a;
                inPlace.Normalize();

                MathAssert.Exact(inPlace, expected, Inputs.Label(i) + " Normalize()");
                MathAssert.Exact(Vector3.Normalize(a), expected, Inputs.Label(i) + " Normalize(vector)");
            }
        });
    }

    [Test]
    public void Normalized_Zero_IsNaN()
    {
        MathAssert.Exact(Vector3.Zero.Normalized(), new Vector3(float.NaN));
    }

    [Test]
    public void Distance_RandomInputs_MatchesSystemNumerics()
    {
        var random = Inputs.CreateRandom();

        Assert.Multiple(() =>
        {
            for (var i = 0; i < Inputs.Count; i++)
            {
                var a = random.NextVector3();
                var b = random.NextVector3();
                var na = Oracle.ToNumerics(a);
                var nb = Oracle.ToNumerics(b);
                var d = na - nb;
                var label = Inputs.Label(i);

                var distance = Numerics.Vector3.Distance(na, nb);
                MathAssert.Near(Vector3.Distance(a, b), distance, MathAssert.SumsOfProducts, MathAssert.AtLeastOne(distance),
                    label + " Distance");
                MathAssert.Near(Vector3.DistanceSquared(a, b), Numerics.Vector3.DistanceSquared(na, nb), MathAssert.SumsOfProducts,
                    MathAssert.SumOfAbs(d.X * d.X, d.Y * d.Y, d.Z * d.Z), label + " DistanceSquared");
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
                var a = random.NextVector3();
                var b = random.NextVector3();
                var na = Oracle.ToNumerics(a);
                var nb = Oracle.ToNumerics(b);
                var label = Inputs.Label(i);

                MathAssert.Exact(Vector3.ComponentMin(a, b), Oracle.FromNumerics(Numerics.Vector3.Min(na, nb)), label + " ComponentMin");
                MathAssert.Exact(Vector3.ComponentMax(a, b), Oracle.FromNumerics(Numerics.Vector3.Max(na, nb)), label + " ComponentMax");
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
                var value = random.NextVector3();
                var p = Oracle.ToNumerics(random.NextVector3());
                var q = Oracle.ToNumerics(random.NextVector3());
                var min = Numerics.Vector3.Min(p, q);
                var max = Numerics.Vector3.Max(p, q);
                var nValue = Oracle.ToNumerics(value);
                var label = Inputs.Label(i);

                MathAssert.Exact(Vector3.Abs(value), Oracle.FromNumerics(Numerics.Vector3.Abs(nValue)), label + " Abs");
                MathAssert.Exact(Vector3.Clamp(value, Oracle.FromNumerics(min), Oracle.FromNumerics(max)),
                    Oracle.FromNumerics(Numerics.Vector3.Clamp(nValue, min, max)), label + " Clamp");
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
                var a = random.NextVector3();
                var b = random.NextVector3();
                var t = random.NextSingle();

                var expected = Oracle.FromNumerics(Numerics.Vector3.Lerp(Oracle.ToNumerics(a), Oracle.ToNumerics(b), t));
                float[] scales =
                [
                    MathAssert.SumOfAbs(a.X, a.X * t, b.X * t),
                    MathAssert.SumOfAbs(a.Y, a.Y * t, b.Y * t),
                    MathAssert.SumOfAbs(a.Z, a.Z * t, b.Z * t),
                ];

                MathAssert.Near(Vector3.Lerp(a, b, t), expected, MathAssert.SumsOfProducts, scales, Inputs.Label(i));
            }
        });
    }

    [Test]
    public void Lerp_TOutsideUnitRange_Extrapolates()
    {
        MathAssert.Near(Vector3.Lerp(Vector3.Zero, Vector3.One * 10f, 2f), Vector3.One * 20f, MathAssert.SumsOfProducts, 20f);
    }

    [Test]
    public void Equality_SameAndDifferent_OperatorsAndEqualsAgree()
    {
        var a = new Vector3(1f, 2f, 3f);
        var same = new Vector3(1f, 2f, 3f);

        Assert.Multiple(() =>
        {
            Assert.That(a == same, Is.True, "== same");
            Assert.That(a != same, Is.False, "!= same");
            Assert.That(a.Equals(same), Is.True, "Equals same");
            Assert.That(a.Equals((object)same), Is.True, "Equals(object) same");

            for (var index = 0; index < 3; index++)
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
        var a = new Vector3(float.NaN, 2f, 3f);
        var b = new Vector3(float.NaN, 2f, 3f);

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
        var positive = new Vector3(0f, 2f, 3f);
        var negative = new Vector3(float.NegativeZero, 2f, 3f);

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
        Assert.That(new Vector3(1.5f, 2f, -3f).ToString(), Is.EqualTo("(1.5, 2, -3)"));
    }

    [Test]
    public void TransformPosition_Translation_AddsOffset()
    {
        MathAssert.Exact(Vector3.TransformPosition(Matrix4.CreateTranslation(1f, 2f, 3f), new Vector3(1f, 1f, 1f)), new Vector3(2f, 3f, 4f));
    }

    [Test]
    public void TransformPosition_RandomTrs_MatchesSystemNumerics()
    {
        var random = Inputs.CreateRandom();

        Assert.Multiple(() =>
        {
            for (var i = 0; i < Inputs.Count; i++)
            {
                var trs = random.NextTrs();
                var position = random.NextVector3();

                var expected = Oracle.FromNumerics(Numerics.Vector3.Transform(Oracle.ToNumerics(position), Oracle.ToNumerics(trs)));
                var scales = MathAssert.ProductScales(trs, new Vector4(position, 1f));

                MathAssert.Near(Vector3.TransformPosition(trs, position), expected, MathAssert.SumsOfProducts, scales.AsSpan(0, 3),
                    Inputs.Label(i));
            }
        });
    }

    [Test]
    public void TransformDirection_Translation_IgnoresOffset()
    {
        MathAssert.Exact(Vector3.TransformDirection(Matrix4.CreateTranslation(1f, 2f, 3f), new Vector3(1f, 1f, 1f)), new Vector3(1f, 1f, 1f));
    }

    [Test]
    public void Transform_Quaternion_EqualsOperator()
    {
        var random = Inputs.CreateRandom();

        Assert.Multiple(() =>
        {
            for (var i = 0; i < Inputs.Count; i++)
            {
                var rotation = random.NextRotation();
                var vector = random.NextVector3();

                MathAssert.Exact(Vector3.Transform(rotation, vector), rotation * vector, Inputs.Label(i));
            }
        });
    }

    [Test]
    public void SystemNumerics_RoundTrip_IsExact()
    {
        var random = Inputs.CreateRandom();

        Assert.Multiple(() =>
        {
            for (var i = 0; i < Inputs.Count; i++)
            {
                var ours = random.NextVector3();
                var theirs = Oracle.ToNumerics(random.NextVector3());
                var label = Inputs.Label(i);

                Numerics.Vector3 converted = ours;
                Vector3 back = converted;
                Vector3 fromTheirs = theirs;

                MathAssert.Exact([converted.X, converted.Y, converted.Z], MathAssert.Components(ours), label + " to System.Numerics");
                MathAssert.Exact(back, ours, label + " round trip");
                MathAssert.Exact(fromTheirs, Oracle.FromNumerics(theirs), label + " from System.Numerics");
            }
        });
    }
}
