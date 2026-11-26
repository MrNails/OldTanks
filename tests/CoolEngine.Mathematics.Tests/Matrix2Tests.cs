using System.Numerics;
using System.Runtime.CompilerServices;
using CoolEngine.Mathematics.Tests.TestSupport;
using OpenTKMath = OpenTK.Mathematics;

namespace CoolEngine.Mathematics.Tests;

[TestFixture]
public sealed class Matrix2Tests
{
    private const int N = 2;

    [Test]
    public void Layout_FromRows_StoresColumnMajor()
    {
        var matrix = Matrix2.FromRows(new Vector2(1f, 2f), new Vector2(3f, 4f));

        var memory = MathAssert.Floats(matrix);

        Assert.Multiple(() =>
        {
            Assert.That(Unsafe.SizeOf<Matrix2>(), Is.EqualTo(Matrix2.SizeInBytes));
            Assert.That(Unsafe.SizeOf<Matrix2>(), Is.EqualTo(16));
            Assert.That(memory, Is.EqualTo(new[] { 1f, 3f, 2f, 4f }));
        });
    }

    [Test]
    public void FromColumns_Columns_AreTheFields()
    {
        var c0 = new Vector2(1f, 2f);
        var c1 = new Vector2(3f, 4f);

        var matrix = Matrix2.FromColumns(c0, c1);

        Assert.Multiple(() =>
        {
            MathAssert.Exact(matrix.Column0, c0, nameof(Matrix2.Column0));
            MathAssert.Exact(matrix.Column1, c1, nameof(Matrix2.Column1));
        });
    }

    [Test]
    public void Identity_Elements_AreDiagonalOnes()
    {
        var identity = Matrix2.Identity;

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
                    var matrix = Matrix2.Zero;
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
        var matrix = Matrix2.Identity;

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
                var a = random.NextMatrix2();
                var b = random.NextMatrix2();

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
                var matrix = random.NextMatrix2();
                var vector = random.NextVector2();

                var expected = Oracle.FromOpenTK(OpenTKMath.Vector2.TransformRow(Oracle.ToOpenTK(vector), Oracle.ToOpenTK(matrix)));

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
                var matrix = random.NextMatrix2();

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
        MathAssert.Exact(Matrix2.CreateScale(2f).Determinant, 4f);
    }

    [Test]
    public void Determinant_Random_MatchesOracle()
    {
        var random = Inputs.CreateRandom();

        Assert.Multiple(() =>
        {
            for (var i = 0; i < Inputs.Count; i++)
            {
                var matrix = random.NextMatrix2();

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
                var matrix = random.NextRotationScale2();

                MathAssert.Near(matrix * matrix.Inverted(), Matrix2.Identity, MathAssert.Inverse, 1f, Inputs.Label(i));
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
                var matrix = random.NextMatrix2();

                var expected = Oracle.FromOpenTK(OpenTKMath.Matrix2.Invert(Oracle.ToOpenTK(matrix)));

                MathAssert.Near(matrix.Inverted(), expected, MathAssert.Inverse, MathAssert.AtLeastOne(MathAssert.Components(expected)),
                    Inputs.Label(i));
            }
        });
    }

    [Test]
    public void Inverted_Singular_Throws()
    {
        // Small integers with two equal columns: every product is exact, so the determinant is exactly 0.
        var twoEqualColumns = Matrix2.FromColumns(new Vector2(3f, -2f), new Vector2(3f, -2f));

        Assert.Multiple(() =>
        {
            Assert.Throws<InvalidOperationException>(() => Matrix2.Zero.Inverted(), nameof(Matrix2.Zero));
            Assert.Throws<InvalidOperationException>(() => twoEqualColumns.Inverted(), "two equal columns");
        });
    }

    [Test]
    public void TryInvert_SingularAndInvertible_ReportsAndFills()
    {
        var singular = Matrix2.TryInvert(Matrix2.Zero, out var singularResult);
        var invertible = Matrix2.TryInvert(Matrix2.CreateScale(2f), out var inverse);

        Assert.Multiple(() =>
        {
            Assert.That(singular, Is.False, "singular returns");
            MathAssert.Exact(singularResult, Matrix2.FromColumns(new Vector2(float.NaN), new Vector2(float.NaN)), "singular result");
            Assert.That(invertible, Is.True, "invertible returns");
            MathAssert.Exact(inverse, Matrix2.CreateScale(0.5f), "invertible result");
        });
    }

    [Test]
    public void CreateScale_Overloads_GiveDiagonal()
    {
        var diagonal = Matrix2.FromRows(new Vector2(2f, 0f), new Vector2(0f, 3f));
        var uniform = Matrix2.FromRows(new Vector2(5f, 0f), new Vector2(0f, 5f));

        Assert.Multiple(() =>
        {
            MathAssert.Exact(Matrix2.CreateScale(5f), uniform, "CreateScale(scale)");
            MathAssert.Exact(Matrix2.CreateScale(new Vector2(2f, 3f)), diagonal, "CreateScale(Vector2)");
            MathAssert.Exact(Matrix2.CreateScale(2f, 3f), diagonal, "CreateScale(x, y)");
        });
    }

    [Test]
    public void CreateRotation_HalfPi_TurnsUnitXToUnitY()
    {
        MathAssert.Near(Matrix2.CreateRotation(MathHelper.PiOver2) * Vector2.UnitX, Vector2.UnitY, 1e-6f);
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

                MathAssert.Near(Matrix2.CreateRotation(angle), Oracle.FromOpenTK(OpenTKMath.Matrix2.CreateRotation(angle)),
                    MathAssert.Trigonometric, 1f, Inputs.Label(i));
            }
        });
    }

    [Test]
    public void Equality_SameAndDifferent_OperatorsAndEqualsAgree()
    {
        var random = Inputs.CreateRandom();
        var a = random.NextMatrix2();
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
        var a = Matrix2.Identity;
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
        var positive = Matrix2.Identity;
        positive[1, 0] = 0f;
        var negative = Matrix2.Identity;
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
        var matrix = Matrix2.FromRows(new Vector2(1.5f, 2f), new Vector2(-3f, 0.25f));

        Assert.That(matrix.ToString(), Is.EqualTo("(1.5, 2)\n(-3, 0.25)"));
    }
}
