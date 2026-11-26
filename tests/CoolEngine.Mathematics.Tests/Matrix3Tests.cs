using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;
using CoolEngine.Mathematics.Tests.TestSupport;
using OpenTKMath = OpenTK.Mathematics;

namespace CoolEngine.Mathematics.Tests;

[TestFixture]
public sealed class Matrix3Tests
{
    private const int N = 3;

    [Test]
    public void Layout_FromRows_StoresColumnMajor()
    {
        var matrix = Matrix3.FromRows(new Vector3(1f, 2f, 3f), new Vector3(4f, 5f, 6f), new Vector3(7f, 8f, 9f));

        var memory = MathAssert.Floats(matrix);

        Assert.Multiple(() =>
        {
            Assert.That(Unsafe.SizeOf<Matrix3>(), Is.EqualTo(Matrix3.SizeInBytes));
            Assert.That(Unsafe.SizeOf<Matrix3>(), Is.EqualTo(36));
            Assert.That(memory, Is.EqualTo(new[] { 1f, 4f, 7f, 2f, 5f, 8f, 3f, 6f, 9f }));
        });
    }

    [Test]
    public void FromColumns_Columns_AreTheFields()
    {
        var c0 = new Vector3(1f, 2f, 3f);
        var c1 = new Vector3(4f, 5f, 6f);
        var c2 = new Vector3(7f, 8f, 9f);

        var matrix = Matrix3.FromColumns(c0, c1, c2);

        Assert.Multiple(() =>
        {
            MathAssert.Exact(matrix.Column0, c0, nameof(Matrix3.Column0));
            MathAssert.Exact(matrix.Column1, c1, nameof(Matrix3.Column1));
            MathAssert.Exact(matrix.Column2, c2, nameof(Matrix3.Column2));
        });
    }

    [Test]
    public void Identity_Elements_AreDiagonalOnes()
    {
        var identity = Matrix3.Identity;

        Assert.Multiple(() =>
        {
            for (var row = 0; row < N; row++)
            {
                for (var column = 0; column < N; column++)
                    MathAssert.Exact(identity[row, column], row == column ? 1f : 0f, $"[{row}, {column}]");
            }
        });
    }

    [Test]
    public void Indexer_RowColumn_MapsToColumnFields()
    {
        Assert.Multiple(() =>
        {
            for (var row = 0; row < N; row++)
            {
                for (var column = 0; column < N; column++)
                {
                    var matrix = Matrix3.Zero;
                    matrix[row, column] = 7f;

                    var expected = new float[N * N];
                    expected[column * N + row] = 7f;
                    MathAssert.Exact(MathAssert.Components(matrix), expected, $"set [{row}, {column}]");
                    MathAssert.Exact(matrix[row, column], 7f, $"get [{row}, {column}]");
                }
            }
        });
    }

    [TestCase(-1, 0)]
    [TestCase(0, N)]
    [TestCase(N, 0)]
    public void Indexer_OutOfRange_Throws(int row, int column)
    {
        var matrix = Matrix3.Identity;

        Assert.Multiple(() =>
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => _ = matrix[row, column], "get");
            Assert.Throws<ArgumentOutOfRangeException>(() => matrix[row, column] = 0f, "set");
        });
    }

    [Test]
    public void Multiply_RandomMatrices_MatchesOracleWithSwappedOperands()
    {
        var random = Inputs.CreateRandom();

        Assert.Multiple(() =>
        {
            for (var i = 0; i < Inputs.Count; i++)
            {
                var a = random.NextMatrix3();
                var b = random.NextMatrix3();

                var expected = Oracle.FromOpenTK(Oracle.ToOpenTK(b) * Oracle.ToOpenTK(a));

                MathAssert.Near(a * b, expected, MathAssert.SumsOfProducts, MathAssert.ProductScales(a, b), Inputs.Label(i));
            }
        });
    }

    [Test]
    public void MultiplyVector_RandomInputs_MatchesOracle()
    {
        var random = Inputs.CreateRandom();

        Assert.Multiple(() =>
        {
            for (var i = 0; i < Inputs.Count; i++)
            {
                var matrix = random.NextMatrix3();
                var vector = random.NextVector3();

                var expected = Oracle.FromOpenTK(OpenTKMath.Vector3.TransformRow(Oracle.ToOpenTK(vector), Oracle.ToOpenTK(matrix)));

                MathAssert.Near(matrix * vector, expected, MathAssert.SumsOfProducts, MathAssert.ProductScales(matrix, vector),
                    Inputs.Label(i));
            }
        });
    }

    [Test]
    public void Transposed_Random_SwapsElements()
    {
        var random = Inputs.CreateRandom();

        Assert.Multiple(() =>
        {
            for (var i = 0; i < Inputs.Count; i++)
            {
                var matrix = random.NextMatrix3();

                var transposed = matrix.Transposed();

                for (var row = 0; row < N; row++)
                {
                    for (var column = 0; column < N; column++)
                        MathAssert.Exact(transposed[row, column], matrix[column, row], $"{Inputs.Label(i)} [{row}, {column}]");
                }
            }
        });
    }

    [Test]
    public void Determinant_Scale_IsProductOfDiagonal()
    {
        MathAssert.Exact(Matrix3.CreateScale(2f).Determinant, 8f);
    }

    [Test]
    public void Determinant_Random_MatchesOracle()
    {
        var random = Inputs.CreateRandom();

        Assert.Multiple(() =>
        {
            for (var i = 0; i < Inputs.Count; i++)
            {
                var matrix = random.NextMatrix3();

                MathAssert.Near(matrix.Determinant, Oracle.ToOpenTK(matrix).Determinant, MathAssert.SumsOfProducts,
                    MathAssert.DeterminantScale(matrix), Inputs.Label(i));
            }
        });
    }

    [Test]
    public void Inverted_RandomTrs_ProductIsIdentity()
    {
        var random = Inputs.CreateRandom();

        Assert.Multiple(() =>
        {
            for (var i = 0; i < Inputs.Count; i++)
            {
                var matrix = random.NextRotationScale3();

                MathAssert.Near(matrix * matrix.Inverted(), Matrix3.Identity, MathAssert.Inverse, 1f, Inputs.Label(i));
            }
        });
    }

    [Test]
    public void Inverted_Random_MatchesOracle()
    {
        var random = Inputs.CreateRandom();

        Assert.Multiple(() =>
        {
            for (var i = 0; i < Inputs.Count; i++)
            {
                var matrix = random.NextWellConditionedMatrix3();

                var expected = Oracle.FromOpenTK(OpenTKMath.Matrix3.Invert(Oracle.ToOpenTK(matrix)));

                MathAssert.Near(matrix.Inverted(), expected, MathAssert.Inverse, MathAssert.AtLeastOne(MathAssert.Components(expected)),
                    Inputs.Label(i));
            }
        });
    }

    [Test]
    public void Inverted_Singular_Throws()
    {
        // Small integers with column 0 equal to column 2: every product is exact, so the determinant is exactly 0.
        var twoEqualColumns = Matrix3.FromColumns(new Vector3(1f, 2f, 3f), new Vector3(-2f, 0f, 5f), new Vector3(1f, 2f, 3f));

        Assert.Multiple(() =>
        {
            Assert.Throws<InvalidOperationException>(() => Matrix3.Zero.Inverted(), nameof(Matrix3.Zero));
            Assert.Throws<InvalidOperationException>(() => twoEqualColumns.Inverted(), "two equal columns");
        });
    }

    [Test]
    public void TryInvert_SingularAndInvertible_ReportsAndFills()
    {
        var singular = Matrix3.TryInvert(Matrix3.Zero, out var singularResult);
        var invertible = Matrix3.TryInvert(Matrix3.CreateScale(2f), out var inverse);

        Assert.Multiple(() =>
        {
            Assert.That(singular, Is.False, "singular returns");
            MathAssert.Exact(singularResult, Matrix3.FromColumns(new Vector3(float.NaN), new Vector3(float.NaN), new Vector3(float.NaN)),
                "singular result");
            Assert.That(invertible, Is.True, "invertible returns");
            MathAssert.Exact(inverse, Matrix3.CreateScale(0.5f), "invertible result");
        });
    }

    [Test]
    public void TryInvert_TinyButInvertible_ReturnsTrue()
    {
        // The determinant 1e-36 is tiny but not 0: the matrix is invertible.
        var invertible = Matrix3.TryInvert(Matrix3.CreateScale(1e-12f), out var inverse);
        var expected = Matrix3.CreateScale(1e12f);

        Assert.Multiple(() =>
        {
            Assert.That(invertible, Is.True, "returns");
            MathAssert.Near(inverse, expected, 1e-6f, MathAssert.AtLeastOne(MathAssert.Components(expected)), "result");
        });
    }

    [Test]
    public void ConvertBetweenSizes_UpperLeft_RoundTrips()
    {
        var random = Inputs.CreateRandom();

        Assert.Multiple(() =>
        {
            for (var i = 0; i < Inputs.Count; i++)
            {
                var m4 = random.NextMatrix4();
                var m3 = random.NextMatrix3();
                var label = Inputs.Label(i);

                var upperLeft = new Matrix3(m4);
                var widened = new Matrix4(m3);

                MathAssert.Exact(upperLeft, Matrix3.FromColumns(m4.Column0.AsVector128().AsVector3(), m4.Column1.AsVector128().AsVector3(),
                    m4.Column2.AsVector128().AsVector3()), label + " Matrix3(Matrix4)");
                MathAssert.Exact(widened, Matrix4.FromColumns(new Vector4(m3.Column0, 0f), new Vector4(m3.Column1, 0f),
                    new Vector4(m3.Column2, 0f), Vector4.UnitW), label + " Matrix4(Matrix3)");
                MathAssert.Exact(new Matrix3(widened), m3, label + " round trip");
            }
        });
    }

    [Test]
    public void CreateScale_Overloads_GiveDiagonal()
    {
        var diagonal = Matrix3.FromRows(new Vector3(2f, 0f, 0f), new Vector3(0f, 3f, 0f), new Vector3(0f, 0f, 4f));
        var uniform = Matrix3.FromRows(new Vector3(5f, 0f, 0f), new Vector3(0f, 5f, 0f), new Vector3(0f, 0f, 5f));

        Assert.Multiple(() =>
        {
            MathAssert.Exact(Matrix3.CreateScale(5f), uniform, "CreateScale(scale)");
            MathAssert.Exact(Matrix3.CreateScale(new Vector3(2f, 3f, 4f)), diagonal, "CreateScale(Vector3)");
            MathAssert.Exact(Matrix3.CreateScale(2f, 3f, 4f), diagonal, "CreateScale(x, y, z)");
        });
    }

    [Test]
    public void CreateRotationX_HalfPi_TurnsUnitYToUnitZ()
    {
        MathAssert.Near(Matrix3.CreateRotationX(MathHelper.PiOver2) * Vector3.UnitY, Vector3.UnitZ, 1e-6f);
    }

    [Test]
    public void CreateRotationY_HalfPi_TurnsUnitZToUnitX()
    {
        MathAssert.Near(Matrix3.CreateRotationY(MathHelper.PiOver2) * Vector3.UnitZ, Vector3.UnitX, 1e-6f);
    }

    [Test]
    public void CreateRotationZ_HalfPi_TurnsUnitXToUnitY()
    {
        MathAssert.Near(Matrix3.CreateRotationZ(MathHelper.PiOver2) * Vector3.UnitX, Vector3.UnitY, 1e-6f);
    }

    [Test]
    public void CreateRotations_RandomAngles_MatchOpenTK()
    {
        var random = Inputs.CreateRandom();

        Assert.Multiple(() =>
        {
            for (var i = 0; i < Inputs.Count; i++)
            {
                var angle = random.NextAngle();
                var label = Inputs.Label(i);

                MathAssert.Near(Matrix3.CreateRotationX(angle), Oracle.FromOpenTK(OpenTKMath.Matrix3.CreateRotationX(angle)),
                    MathAssert.Trigonometric, 1f, label + " X");
                MathAssert.Near(Matrix3.CreateRotationY(angle), Oracle.FromOpenTK(OpenTKMath.Matrix3.CreateRotationY(angle)),
                    MathAssert.Trigonometric, 1f, label + " Y");
                MathAssert.Near(Matrix3.CreateRotationZ(angle), Oracle.FromOpenTK(OpenTKMath.Matrix3.CreateRotationZ(angle)),
                    MathAssert.Trigonometric, 1f, label + " Z");
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
                var axis = random.NextVector3();
                var angle = random.NextAngle();

                var expected = Oracle.FromOpenTK(OpenTKMath.Matrix3.CreateFromAxisAngle(Oracle.ToOpenTK(axis), angle));

                MathAssert.Near(Matrix3.CreateFromAxisAngle(axis, angle), expected, MathAssert.Trigonometric, 1f, Inputs.Label(i));
            }
        });
    }

    [Test]
    public void CreateFromAxisAngle_ZeroAxis_IsIdentity()
    {
        MathAssert.Exact(Matrix3.CreateFromAxisAngle(Vector3.Zero, 1f), Matrix3.Identity);
    }

    [Test]
    public void CreateFromAxisAngle_UnnormalizedAxis_EqualsNormalized()
    {
        var random = Inputs.CreateRandom();

        Assert.Multiple(() =>
        {
            for (var i = 0; i < Inputs.Count; i++)
            {
                var axis = Vector3.Normalize(random.NextVector3());
                var angle = random.NextAngle();

                MathAssert.Near(Matrix3.CreateFromAxisAngle(axis * 5f, angle), Matrix3.CreateFromAxisAngle(axis, angle),
                    MathAssert.Trigonometric, 1f, Inputs.Label(i));
            }
        });
    }

    [Test]
    public void CreateFromQuaternion_RandomUnitQuaternions_MatchesRotatingVectors()
    {
        var random = Inputs.CreateRandom();

        Assert.Multiple(() =>
        {
            for (var i = 0; i < Inputs.Count; i++)
            {
                var rotation = random.NextRotation();
                var vector = random.NextVector3();

                MathAssert.Near(Matrix3.CreateFromQuaternion(rotation) * vector, Vector3.Transform(vector, rotation), MathAssert.SumsOfProducts,
                    MathAssert.RotationScales(rotation, vector), Inputs.Label(i));
            }
        });
    }

    [Test]
    public void Equality_SameAndDifferent_OperatorsAndEqualsAgree()
    {
        var random = Inputs.CreateRandom();
        var a = random.NextMatrix3();
        var same = a;

        Assert.Multiple(() =>
        {
            Assert.That(a == same, Is.True, "== same");
            Assert.That(a != same, Is.False, "!= same");
            Assert.That(a.Equals(same), Is.True, "Equals same");
            Assert.That(a.Equals((object)same), Is.True, "Equals(object) same");

            for (var row = 0; row < N; row++)
            {
                for (var column = 0; column < N; column++)
                {
                    var different = a;
                    different[row, column] += 1f;

                    Assert.That(a == different, Is.False, $"== differing in [{row}, {column}]");
                    Assert.That(a != different, Is.True, $"!= differing in [{row}, {column}]");
                    Assert.That(a.Equals(different), Is.False, $"Equals differing in [{row}, {column}]");
                    Assert.That(a.Equals((object)different), Is.False, $"Equals(object) differing in [{row}, {column}]");
                }
            }
        });
    }

    [Test]
    public void Equality_NaN_OperatorFalseEqualsTrue()
    {
        var a = Matrix3.Identity;
        a[0, 0] = float.NaN;
        var b = a;

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
        var positive = Matrix3.Identity;
        positive[1, 0] = 0f;
        var negative = Matrix3.Identity;
        negative[1, 0] = float.NegativeZero;

        Assert.Multiple(() =>
        {
            Assert.That(negative.GetHashCode(), Is.EqualTo(positive.GetHashCode()), "hash");
            Assert.That(negative.Equals(positive), Is.True, "Equals");
        });
    }

    [Test]
    [SetCulture("de-DE")]
    public void ToString_CommaDecimalCulture_UsesInvariantRows()
    {
        var matrix = Matrix3.FromRows(new Vector3(1.5f, 2f, -3f), new Vector3(4f, 5f, 6f), new Vector3(7f, 8f, 0.25f));

        Assert.That(matrix.ToString(), Is.EqualTo("(1.5, 2, -3)\n(4, 5, 6)\n(7, 8, 0.25)"));
    }
}
