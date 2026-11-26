using System.Runtime.CompilerServices;
using CoolEngine.Mathematics.Tests.TestSupport;
using Numerics = System.Numerics;
using OpenTKMath = OpenTK.Mathematics;

namespace CoolEngine.Mathematics.Tests;

[TestFixture]
public sealed class QuaterniondTests
{
    private static readonly double[] s_slerpTs = [0.0, 0.25, 0.5, 1.0];

    [Test]
    public void Layout_SizeAndFieldOrder_MatchSizeInBytes()
    {
        var quaternion = new Quaterniond(1.0, 2.0, 3.0, 4.0);

        var memory = MathAssert.Doubles(quaternion);

        Assert.Multiple(() =>
        {
            Assert.That(Unsafe.SizeOf<Quaterniond>(), Is.EqualTo(Quaterniond.SizeInBytes));
            Assert.That(Unsafe.SizeOf<Quaterniond>(), Is.EqualTo(32));
            Assert.That(memory, Is.EqualTo(new[] { 1.0, 2.0, 3.0, 4.0 }));
        });
    }

    [Test]
    public void Constructor_Components_SetsFields()
    {
        Assert.Multiple(() =>
        {
            MathAssert.Exact(MathAssert.Components(new Quaterniond(1.0, -2.0, 3.5, -4.0)), [1.0, -2.0, 3.5, -4.0], "Quaterniond(x, y, z, w)");
            MathAssert.Exact(MathAssert.Components(new Quaterniond(new Vector3d(5.0, 6.0, -7.0), 8.0)), [5.0, 6.0, -7.0, 8.0],
                "Quaterniond(vectorPart, scalarPart)");
        });
    }

    [Test]
    public void Constants_IdentityAndZero_HaveExpectedComponents()
    {
        Assert.Multiple(() =>
        {
            MathAssert.Exact(MathAssert.Components(Quaterniond.Identity), [0.0, 0.0, 0.0, 1.0], nameof(Quaterniond.Identity));
            MathAssert.Exact(MathAssert.Components(Quaterniond.Zero), [0.0, 0.0, 0.0, 0.0], nameof(Quaterniond.Zero));
        });
    }

    [Test]
    public void Length_RandomInputs_MatchesOpenTK()
    {
        var random = Inputs.CreateRandom();

        Assert.Multiple(() =>
        {
            for (var i = 0; i < Inputs.Count; i++)
            {
                var q = random.NextQuaterniond();
                var oq = Oracle.ToOpenTK(q);
                var label = Inputs.Label(i);

                MathAssert.Near(q.Length(), oq.Length, MathAssert.SumsOfProductsD, MathAssert.AtLeastOne(oq.Length), label + " Length");
                MathAssert.Near(q.LengthSquared(), oq.LengthSquared, MathAssert.SumsOfProductsD,
                    MathAssert.SumOfAbs(q.X * q.X, q.Y * q.Y, q.Z * q.Z, q.W * q.W), label + " LengthSquared");
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
                var q = random.NextQuaterniond();

                var expected = Oracle.FromOpenTK(OpenTKMath.Quaterniond.Normalize(Oracle.ToOpenTK(q)));

                MathAssert.Near(Quaterniond.Normalize(q), expected, MathAssert.SumsOfProductsD,
                    MathAssert.AtLeastOne(MathAssert.Components(expected)), Inputs.Label(i));
            }
        });
    }

    [Test]
    public void Conjugate_Random_NegatesVectorPart()
    {
        var random = Inputs.CreateRandom();

        Assert.Multiple(() =>
        {
            for (var i = 0; i < Inputs.Count; i++)
            {
                var q = random.NextQuaterniond();

                MathAssert.Exact(Quaterniond.Conjugate(q), new Quaterniond(-q.X, -q.Y, -q.Z, q.W), Inputs.Label(i));
            }
        });
    }

    [Test]
    public void Inverse_RandomInputs_ProductIsIdentity()
    {
        var random = Inputs.CreateRandom();

        Assert.Multiple(() =>
        {
            for (var i = 0; i < Inputs.Count; i++)
            {
                var q = random.NextQuaterniond();
                var inverse = Quaterniond.Inverse(q);

                MathAssert.Near(q * inverse, Quaterniond.Identity, MathAssert.SumsOfProductsD, MathAssert.ProductScales(q, inverse),
                    Inputs.Label(i));
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
                var a = random.NextQuaterniond();
                var b = random.NextQuaterniond();

                // OpenTK has no quaternion dot product; its Vector4d.Dot over the same components is the oracle.
                var expected = OpenTKMath.Vector4d.Dot(new OpenTKMath.Vector4d(a.X, a.Y, a.Z, a.W), new OpenTKMath.Vector4d(b.X, b.Y, b.Z, b.W));

                MathAssert.Near(Quaterniond.Dot(a, b), expected, MathAssert.SumsOfProductsD,
                    MathAssert.SumOfAbs(a.X * b.X, a.Y * b.Y, a.Z * b.Z, a.W * b.W), Inputs.Label(i));
            }
        });
    }

    [Test]
    public void CreateFromAxisAngle_RandomInputs_MatchesOpenTK()
    {
        var random = Inputs.CreateRandom();

        Assert.Multiple(() =>
        {
            for (var i = 0; i < Inputs.Count; i++)
            {
                var axis = Vector3d.Normalize(random.NextVector3d());
                var angle = random.NextAngleD();

                var expected = Oracle.FromOpenTK(OpenTKMath.Quaterniond.FromAxisAngle(Oracle.ToOpenTK(axis), angle));

                MathAssert.Near(Quaterniond.CreateFromAxisAngle(axis, angle), expected, MathAssert.TrigonometricD, 1.0, Inputs.Label(i));
            }
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
                var a = random.NextQuaterniond();
                var b = random.NextQuaterniond();
                var s = random.NextComponentD();
                var oa = Oracle.ToOpenTK(a);
                var ob = Oracle.ToOpenTK(b);
                var label = Inputs.Label(i);

                MathAssert.Exact(a + b, Oracle.FromOpenTK(oa + ob), label + " a + b");
                MathAssert.Exact(a - b, Oracle.FromOpenTK(oa - ob), label + " a - b");
                MathAssert.Exact(a * s, Oracle.FromOpenTK(oa * s), label + " a * s");

                // OpenTK's Quaterniond has no unary minus: the hand negation of every component is the reference.
                MathAssert.Exact(-a, new Quaterniond(-a.X, -a.Y, -a.Z, -a.W), label + " -a");
            }
        });
    }

    [Test]
    public void Multiply_RandomInputs_MatchesOpenTK()
    {
        var random = Inputs.CreateRandom();

        Assert.Multiple(() =>
        {
            for (var i = 0; i < Inputs.Count; i++)
            {
                var a = random.NextQuaterniond();
                var b = random.NextQuaterniond();

                var expected = Oracle.FromOpenTK(Oracle.ToOpenTK(a) * Oracle.ToOpenTK(b));

                MathAssert.Near(a * b, expected, MathAssert.SumsOfProductsD, MathAssert.ProductScales(a, b), Inputs.Label(i));
            }
        });
    }

    [Test]
    public void Multiply_TwoRotations_AppliesRightOperandFirst()
    {
        var aboutZ = Quaterniond.CreateFromAxisAngle(Vector3d.UnitZ, Math.PI / 2);
        var aboutX = Quaterniond.CreateFromAxisAngle(Vector3d.UnitX, Math.PI / 2);

        // The X rotation sends Y to Z, then the Z rotation keeps Z; the other order would give -X.
        MathAssert.Near(Vector3d.Transform(Vector3d.UnitY, aboutZ * aboutX), Vector3d.UnitZ, MathAssert.TrigonometricD);
    }

    [Test]
    public void Transform_RandomInputs_MatchesOpenTK()
    {
        var random = Inputs.CreateRandom();

        Assert.Multiple(() =>
        {
            for (var i = 0; i < Inputs.Count; i++)
            {
                var rotation = random.NextRotationD();
                var vector = random.NextVector3d();

                var expected = Oracle.FromOpenTK(OpenTKMath.Vector3d.Transform(Oracle.ToOpenTK(vector), Oracle.ToOpenTK(rotation)));

                MathAssert.Near(Vector3d.Transform(vector, rotation), expected, MathAssert.SumsOfProductsD,
                    MathAssert.RotationScales(rotation, vector), Inputs.Label(i));
            }
        });
    }

    [Test]
    public void Transform_HalfPiAboutY_TurnsUnitZToUnitX()
    {
        MathAssert.Near(Vector3d.Transform(Vector3d.UnitZ, Quaterniond.CreateFromAxisAngle(Vector3d.UnitY, Math.PI / 2)), Vector3d.UnitX,
            MathAssert.TrigonometricD);
    }

    [Test]
    public void Slerp_RandomInputs_MatchesOpenTK()
    {
        var random = Inputs.CreateRandom();

        Assert.Multiple(() =>
        {
            for (var i = 0; i < Inputs.Count; i++)
            {
                var (a, b) = NextSlerpPair(random);

                foreach (var t in s_slerpTs)
                {
                    var expected = Oracle.FromOpenTK(OpenTKMath.Quaterniond.Slerp(Oracle.ToOpenTK(a), Oracle.ToOpenTK(b), t));

                    MathAssert.Near(Quaterniond.Slerp(a, b, t), expected, MathAssert.TrigonometricD, 1.0, $"{Inputs.Label(i)} t {t}");
                }
            }
        });
    }

    [Test]
    public void Slerp_NearlyEqual_ReturnsNormalizedLerp()
    {
        const double t = 0.3;
        var nudge = Quaterniond.CreateFromAxisAngle(Vector3d.UnitX, 1e-7);
        var random = Inputs.CreateRandom();

        Assert.Multiple(() =>
        {
            for (var i = 0; i < Inputs.Count; i++)
            {
                var a = random.NextRotationD();
                var b = Oracle.FromOpenTK(Oracle.ToOpenTK(a) * Oracle.ToOpenTK(nudge));
                var label = Inputs.Label(i);

                var va = new Vector4d(a.X, a.Y, a.Z, a.W);
                var vb = new Vector4d(b.X, b.Y, b.Z, b.W);
                var lerp = Vector4d.Normalize(va + (vb - va) * t);

                Assert.That(Quaterniond.Dot(a, b), Is.GreaterThan(1.0 - 1e-12), label + " precondition: Dot > 1 - 1e-12");
                MathAssert.Near(Quaterniond.Slerp(a, b, t), new Quaterniond(lerp.X, lerp.Y, lerp.Z, lerp.W), MathAssert.TrigonometricD, 1.0,
                    label);
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

                MathAssert.Near(Quaterniond.Slerp(q, -other, 0.5), Quaterniond.Slerp(q, other, 0.5), MathAssert.TrigonometricD, 1.0,
                    Inputs.Label(i));
            }
        });
    }

    [Test]
    public void Slerp_TOutsideUnitRange_Extrapolates()
    {
        var target = Quaterniond.CreateFromAxisAngle(Vector3d.UnitY, 0.6);

        Assert.Multiple(() =>
        {
            MathAssert.Near(Quaterniond.Slerp(Quaterniond.Identity, target, 1.5), Quaterniond.CreateFromAxisAngle(Vector3d.UnitY, 0.9),
                MathAssert.TrigonometricD, 1.0, "t = 1.5");
            MathAssert.Near(Quaterniond.Slerp(Quaterniond.Identity, target, -0.5), Quaterniond.CreateFromAxisAngle(Vector3d.UnitY, -0.3),
                MathAssert.TrigonometricD, 1.0, "t = -0.5");
        });
    }

    [Test]
    public void Equality_SameAndDifferent_OperatorsAndEqualsAgree()
    {
        var a = new Quaterniond(1.0, 2.0, 3.0, 4.0);
        var same = new Quaterniond(1.0, 2.0, 3.0, 4.0);

        Assert.Multiple(() =>
        {
            Assert.That(a == same, Is.True, "== same");
            Assert.That(a != same, Is.False, "!= same");
            Assert.That(a.Equals(same), Is.True, "Equals same");
            Assert.That(a.Equals((object)same), Is.True, "Equals(object) same");

            for (var index = 0; index < 4; index++)
            {
                var components = MathAssert.Components(a);
                components[index] += 1.0;
                var different = new Quaterniond(components[0], components[1], components[2], components[3]);

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
        var a = new Quaterniond(double.NaN, 2.0, 3.0, 4.0);
        var b = new Quaterniond(double.NaN, 2.0, 3.0, 4.0);

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
        var positive = new Quaterniond(0.0, 2.0, 3.0, 4.0);
        var negative = new Quaterniond(double.NegativeZero, 2.0, 3.0, 4.0);

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
        Assert.That(new Quaterniond(1.5, 2.0, -3.0, 0.25).ToString(), Is.EqualTo("(1.5, 2, -3, 0.25)"));
    }

    [Test]
    public void FloatConversion_Widening_IsExact()
    {
        var random = Inputs.CreateRandom();

        Assert.Multiple(() =>
        {
            for (var i = 0; i < Inputs.Count; i++)
            {
                var single = random.NextRotation();
                var label = Inputs.Label(i);

                Quaterniond widened = single;
                var back = (Numerics.Quaternion)widened;

                MathAssert.Exact(MathAssert.Components(widened), [single.X, single.Y, single.Z, single.W], label + " widened");
                MathAssert.Exact([back.X, back.Y, back.Z, back.W], [single.X, single.Y, single.Z, single.W], label + " float -> double -> float");
            }
        });
    }

    [Test]
    public void FloatConversion_Narrowing_RoundsToNearest()
    {
        var narrowed = (Numerics.Quaternion)new Quaterniond(1.0 + Math.Pow(2, -30), 0.1, -0.1, 3.0);

        MathAssert.Exact([narrowed.X, narrowed.Y, narrowed.Z, narrowed.W], [1f, 0.1f, -0.1f, 3f]);
    }

    // Two random rotations with Dot(a, b) in [0, 0.99]: b is negated into a's hemisphere, and pairs closer than 0.99
    // are drawn again (Slerp's general branch, not its lerp fallback).
    private static (Quaterniond A, Quaterniond B) NextSlerpPair(Random random)
    {
        while (true)
        {
            var a = random.NextRotationD();
            var b = random.NextRotationD();
            var dot = a.X * b.X + a.Y * b.Y + a.Z * b.Z + a.W * b.W;
            if (dot < 0.0)
            {
                b = new Quaterniond(-b.X, -b.Y, -b.Z, -b.W);
                dot = -dot;
            }

            if (dot <= 0.99)
                return (a, b);
        }
    }
}
