using System.Runtime.CompilerServices;
using CoolEngine.Mathematics.Tests.TestSupport;
using Numerics = System.Numerics;

namespace CoolEngine.Mathematics.Tests;

[TestFixture]
public sealed class QuaternionTests
{
    private static readonly float[] s_slerpTs = [0f, 0.25f, 0.5f, 1f];

    [Test]
    public void Layout_SizeAndFieldOrder_MatchSizeInBytes()
    {
        var quaternion = new Quaternion(1f, 2f, 3f, 4f);

        var memory = MathAssert.Floats(quaternion);

        Assert.Multiple(() =>
        {
            Assert.That(Unsafe.SizeOf<Quaternion>(), Is.EqualTo(Quaternion.SizeInBytes));
            Assert.That(Unsafe.SizeOf<Quaternion>(), Is.EqualTo(16));
            Assert.That(memory, Is.EqualTo(new[] { 1f, 2f, 3f, 4f }));
            MathAssert.Exact(MathAssert.Components(quaternion), [1f, 2f, 3f, 4f], "fields");
        });
    }

    [Test]
    public void Identity_IsZeroVectorAndOneScalar()
    {
        MathAssert.Exact(MathAssert.Components(Quaternion.Identity), [0f, 0f, 0f, 1f]);
    }

    [Test]
    public void Xyz_GetAndSet_MapToVectorPart()
    {
        var quaternion = new Quaternion(new Vector3(1f, 2f, 3f), 4f);

        var xyz = quaternion.Xyz;
        quaternion.Xyz = new Vector3(-5f, -6f, -7f);

        Assert.Multiple(() =>
        {
            MathAssert.Exact(xyz, new Vector3(1f, 2f, 3f), "get");
            MathAssert.Exact(MathAssert.Components(quaternion), [-5f, -6f, -7f, 4f], "set");
        });
    }

    [Test]
    public void Length_RandomInputs_MatchesSystemNumerics()
    {
        var random = Inputs.CreateRandom();

        Assert.Multiple(() =>
        {
            for (var i = 0; i < Inputs.Count; i++)
            {
                var q = random.NextQuaternion();
                var nq = Oracle.ToNumerics(q);
                var label = Inputs.Label(i);

                var length = nq.Length();
                MathAssert.Near(q.Length, length, MathAssert.SumsOfProducts, MathAssert.AtLeastOne(length), label + " Length");
                MathAssert.Near(q.LengthSquared, nq.LengthSquared(), MathAssert.SumsOfProducts,
                    MathAssert.SumOfAbs(q.X * q.X, q.Y * q.Y, q.Z * q.Z, q.W * q.W), label + " LengthSquared");
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
                var q = random.NextQuaternion();
                var label = Inputs.Label(i);

                var expected = Oracle.FromNumerics(Numerics.Quaternion.Normalize(Oracle.ToNumerics(q)));
                var normalized = q.Normalized();
                var inPlace = q;
                inPlace.Normalize();

                MathAssert.Near(normalized, expected, MathAssert.SumsOfProducts, MathAssert.AtLeastOne(MathAssert.Components(expected)),
                    label + " Normalized");
                MathAssert.Exact(inPlace, normalized, label + " Normalize()");
            }
        });
    }

    [Test]
    public void Conjugated_Random_NegatesVectorPart()
    {
        var random = Inputs.CreateRandom();

        Assert.Multiple(() =>
        {
            for (var i = 0; i < Inputs.Count; i++)
            {
                var q = random.NextQuaternion();

                MathAssert.Exact(q.Conjugated(), new Quaternion(-q.X, -q.Y, -q.Z, q.W), Inputs.Label(i));
            }
        });
    }

    [Test]
    public void Inverted_RandomInputs_ProductIsIdentity()
    {
        var random = Inputs.CreateRandom();

        Assert.Multiple(() =>
        {
            for (var i = 0; i < Inputs.Count; i++)
            {
                var q = random.NextQuaternion();
                var inverse = q.Inverted();

                MathAssert.Near(q * inverse, Quaternion.Identity, MathAssert.SumsOfProducts, MathAssert.ProductScales(q, inverse),
                    Inputs.Label(i));
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
                var a = random.NextQuaternion();
                var b = random.NextQuaternion();

                var expected = Numerics.Quaternion.Dot(Oracle.ToNumerics(a), Oracle.ToNumerics(b));

                MathAssert.Near(Quaternion.Dot(a, b), expected, MathAssert.SumsOfProducts,
                    MathAssert.SumOfAbs(a.X * b.X, a.Y * b.Y, a.Z * b.Z, a.W * b.W), Inputs.Label(i));
            }
        });
    }

    [Test]
    public void FromAxisAngle_RandomInputs_MatchesSystemNumerics()
    {
        var random = Inputs.CreateRandom();

        Assert.Multiple(() =>
        {
            for (var i = 0; i < Inputs.Count; i++)
            {
                var axis = random.NextVector3();
                var angle = random.NextAngle();

                var expected = Oracle.FromNumerics(
                    Numerics.Quaternion.CreateFromAxisAngle(Numerics.Vector3.Normalize(Oracle.ToNumerics(axis)), angle));

                MathAssert.Near(Quaternion.FromAxisAngle(axis, angle), expected, MathAssert.Trigonometric, 1f, Inputs.Label(i));
            }
        });
    }

    [Test]
    public void FromAxisAngle_ZeroAxis_IsIdentity()
    {
        MathAssert.Exact(Quaternion.FromAxisAngle(Vector3.Zero, 1f), Quaternion.Identity);
    }

    [Test]
    public void Multiply_RandomInputs_MatchesSystemNumerics()
    {
        var random = Inputs.CreateRandom();

        Assert.Multiple(() =>
        {
            for (var i = 0; i < Inputs.Count; i++)
            {
                var a = random.NextQuaternion();
                var b = random.NextQuaternion();

                var expected = Oracle.FromNumerics(Oracle.ToNumerics(a) * Oracle.ToNumerics(b));

                MathAssert.Near(a * b, expected, MathAssert.SumsOfProducts, MathAssert.ProductScales(a, b), Inputs.Label(i));
            }
        });
    }

    [Test]
    public void Multiply_TwoRotations_AppliesRightOperandFirst()
    {
        var aboutZ = Quaternion.FromAxisAngle(Vector3.UnitZ, MathHelper.PiOver2);
        var aboutX = Quaternion.FromAxisAngle(Vector3.UnitX, MathHelper.PiOver2);

        // The X rotation sends Y to Z, then the Z rotation keeps Z; the other order would give -X.
        MathAssert.Near((aboutZ * aboutX) * Vector3.UnitY, Vector3.UnitZ, 1e-6f);
    }

    [Test]
    public void MultiplyVector_RandomInputs_MatchesSystemNumericsTransform()
    {
        var random = Inputs.CreateRandom();

        Assert.Multiple(() =>
        {
            for (var i = 0; i < Inputs.Count; i++)
            {
                var rotation = random.NextRotation();
                var vector = random.NextVector3();

                var expected = Oracle.FromNumerics(Numerics.Vector3.Transform(Oracle.ToNumerics(vector), Oracle.ToNumerics(rotation)));

                MathAssert.Near(rotation * vector, expected, MathAssert.SumsOfProducts, MathAssert.RotationScales(rotation, vector),
                    Inputs.Label(i));
            }
        });
    }

    [Test]
    public void MultiplyVector_HalfPiAboutY_TurnsUnitZToUnitX()
    {
        MathAssert.Near(Quaternion.FromAxisAngle(Vector3.UnitY, MathHelper.PiOver2) * Vector3.UnitZ, Vector3.UnitX, 1e-6f);
    }

    [Test]
    public void Slerp_RandomInputs_MatchesSystemNumerics()
    {
        var random = Inputs.CreateRandom();

        Assert.Multiple(() =>
        {
            for (var i = 0; i < Inputs.Count; i++)
            {
                var (a, b) = NextSlerpPair(random);

                foreach (var t in s_slerpTs)
                {
                    var expected = Oracle.FromNumerics(Numerics.Quaternion.Slerp(Oracle.ToNumerics(a), Oracle.ToNumerics(b), t));

                    MathAssert.Near(Quaternion.Slerp(a, b, t), expected, MathAssert.Trigonometric, 1f, $"{Inputs.Label(i)} t {t}");
                }
            }
        });
    }

    [Test]
    public void Slerp_NearlyEqual_ReturnsNormalizedLerp()
    {
        const float t = 0.3f;
        var nudge = Numerics.Quaternion.CreateFromAxisAngle(Numerics.Vector3.UnitX, 1e-4f);
        var random = Inputs.CreateRandom();

        Assert.Multiple(() =>
        {
            for (var i = 0; i < Inputs.Count; i++)
            {
                var a = random.NextRotation();
                var b = Oracle.FromNumerics(Oracle.ToNumerics(a) * nudge);
                var label = Inputs.Label(i);

                var va = new Vector4(a.X, a.Y, a.Z, a.W);
                var vb = new Vector4(b.X, b.Y, b.Z, b.W);
                var lerp = (va + (vb - va) * t).Normalized();

                Assert.That(Quaternion.Dot(a, b), Is.GreaterThan(1f - 1e-6f), label + " precondition: Dot > 1 - 1e-6");
                MathAssert.Near(Quaternion.Slerp(a, b, t), new Quaternion(lerp.X, lerp.Y, lerp.Z, lerp.W), 1e-6f, 1f, label);
            }
        });
    }

    [Test]
    public void Slerp_OppositeHemisphere_TakesShortestArc()
    {
        var random = Inputs.CreateRandom();

        Assert.Multiple(() =>
        {
            for (var i = 0; i < Inputs.Count; i++)
            {
                var (q, other) = NextSlerpPair(random);
                var negated = new Quaternion(-other.X, -other.Y, -other.Z, -other.W);

                MathAssert.Near(Quaternion.Slerp(q, negated, 0.5f), Quaternion.Slerp(q, other, 0.5f), MathAssert.Trigonometric, 1f,
                    Inputs.Label(i));
            }
        });
    }

    [Test]
    public void Equality_SameAndDifferent_OperatorsAndEqualsAgree()
    {
        var a = new Quaternion(1f, 2f, 3f, 4f);
        var same = new Quaternion(1f, 2f, 3f, 4f);

        Assert.Multiple(() =>
        {
            Assert.That(a == same, Is.True, "== same");
            Assert.That(a != same, Is.False, "!= same");
            Assert.That(a.Equals(same), Is.True, "Equals same");
            Assert.That(a.Equals((object)same), Is.True, "Equals(object) same");

            for (var index = 0; index < 4; index++)
            {
                var components = MathAssert.Components(a);
                components[index] += 1f;
                var different = new Quaternion(components[0], components[1], components[2], components[3]);

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
        var a = new Quaternion(float.NaN, 2f, 3f, 4f);
        var b = new Quaternion(float.NaN, 2f, 3f, 4f);

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
        var positive = new Quaternion(0f, 2f, 3f, 4f);
        var negative = new Quaternion(float.NegativeZero, 2f, 3f, 4f);

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
        Assert.That(new Quaternion(1.5f, 2f, -3f, 0.25f).ToString(), Is.EqualTo("(1.5, 2, -3, 0.25)"));
    }

    [Test]
    public void SystemNumerics_RoundTrip_IsExact()
    {
        var random = Inputs.CreateRandom();

        Assert.Multiple(() =>
        {
            for (var i = 0; i < Inputs.Count; i++)
            {
                var ours = random.NextQuaternion();
                var theirs = Oracle.ToNumerics(random.NextQuaternion());
                var label = Inputs.Label(i);

                Numerics.Quaternion converted = ours;
                Quaternion back = converted;
                Quaternion fromTheirs = theirs;

                MathAssert.Exact([converted.X, converted.Y, converted.Z, converted.W], MathAssert.Components(ours),
                    label + " to System.Numerics");
                MathAssert.Exact(back, ours, label + " round trip");
                MathAssert.Exact(fromTheirs, Oracle.FromNumerics(theirs), label + " from System.Numerics");
            }
        });
    }

    // Two random rotations with Dot(a, b) in [0, 0.99]: b is negated into a's hemisphere, and pairs closer than 0.99
    // are drawn again (Slerp's general branch, not its lerp fallback).
    private static (Quaternion A, Quaternion B) NextSlerpPair(Random random)
    {
        while (true)
        {
            var a = random.NextRotation();
            var b = random.NextRotation();
            var dot = Numerics.Quaternion.Dot(Oracle.ToNumerics(a), Oracle.ToNumerics(b));
            if (dot < 0f)
            {
                b = new Quaternion(-b.X, -b.Y, -b.Z, -b.W);
                dot = -dot;
            }

            if (dot <= 0.99f)
                return (a, b);
        }
    }
}
