using System.Runtime.CompilerServices;
using CoolEngine.Mathematics.Tests.TestSupport;
using Numerics = System.Numerics;
using OpenTKMath = OpenTK.Mathematics;

namespace CoolEngine.Mathematics.Tests;

[TestFixture]
public sealed class Matrix2dTests
{
    private const int N = 2;

    [Test]
    public void Layout_FromRows_StoresColumnMajor()
    {
        var matrix = Matrix2d.FromRows(new Vector2d(1.0, 2.0), new Vector2d(3.0, 4.0));

        var memory = MathAssert.Doubles(matrix);

        Assert.Multiple(() =>
        {
            Assert.That(Unsafe.SizeOf<Matrix2d>(), Is.EqualTo(Matrix2d.SizeInBytes));
            Assert.That(Unsafe.SizeOf<Matrix2d>(), Is.EqualTo(32));
            Assert.That(memory, Is.EqualTo(new[] { 1.0, 3.0, 2.0, 4.0 }));
        });
    }

    [Test]
    public void FromColumns_Columns_AreTheFields()
    {
        var c0 = new Vector2d(1.0, 2.0);
        var c1 = new Vector2d(3.0, 4.0);

        var matrix = Matrix2d.FromColumns(c0, c1);

        Assert.Multiple(() =>
        {
            MathAssert.Exact(matrix.Column0, c0, nameof(Matrix2d.Column0));
            MathAssert.Exact(matrix.Column1, c1, nameof(Matrix2d.Column1));
        });
    }

    [Test]
    public void Identity_Elements_AreDiagonalOnes()
    {
        var identity = Matrix2d.Identity;

        Assert.Multiple(() =>
        {
            for (var row = 0; row < N; row++)
            {
                for (var column = 0; column < N; column++)
                    MathAssert.Exact(identity[row, column], row == column ? 1.0 : 0.0, $"[{row}, {column}]");
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
                    var matrix = Matrix2d.Zero;
                    matrix[row, column] = 7.0;

                    var expected = new double[N * N];
                    expected[column * N + row] = 7.0;
                    MathAssert.Exact(MathAssert.Components(matrix), expected, $"set [{row}, {column}]");
                    MathAssert.Exact(matrix[row, column], 7.0, $"get [{row}, {column}]");
                }
            }
        });
    }

    [TestCase(-1, 0)]
    [TestCase(0, N)]
    [TestCase(N, 0)]
    public void Indexer_OutOfRange_Throws(int row, int column)
    {
        var matrix = Matrix2d.Identity;

        Assert.Multiple(() =>
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => _ = matrix[row, column], "get");
            Assert.Throws<ArgumentOutOfRangeException>(() => matrix[row, column] = 0.0, "set");
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
                var a = random.NextMatrix2d();
                var b = random.NextMatrix2d();

                var expected = Oracle.FromOpenTK(Oracle.ToOpenTK(b) * Oracle.ToOpenTK(a));

                MathAssert.Near(a * b, expected, MathAssert.SumsOfProductsD, MathAssert.ProductScales(a, b), Inputs.Label(i));
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
                var matrix = random.NextMatrix2d();
                var vector = random.NextVector2d();

                var expected = Oracle.FromOpenTK(OpenTKMath.Vector2d.TransformRow(Oracle.ToOpenTK(vector), Oracle.ToOpenTK(matrix)));

                MathAssert.Near(matrix * vector, expected, MathAssert.SumsOfProductsD, MathAssert.ProductScales(matrix, vector),
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
                var matrix = random.NextMatrix2d();

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
        MathAssert.Exact(Matrix2d.CreateScale(2.0).Determinant, 4.0);
    }

    [Test]
    public void Determinant_Random_MatchesOracle()
    {
        var random = Inputs.CreateRandom();

        Assert.Multiple(() =>
        {
            for (var i = 0; i < Inputs.Count; i++)
            {
                var matrix = random.NextMatrix2d();

                MathAssert.Near(matrix.Determinant, Oracle.ToOpenTK(matrix).Determinant, MathAssert.SumsOfProductsD,
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
                var matrix = random.NextRotationScale2d();

                MathAssert.Near(matrix * matrix.Inverted(), Matrix2d.Identity, MathAssert.InverseD, 1.0, Inputs.Label(i));
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
                var matrix = random.NextWellConditionedMatrix2d();

                var expected = Oracle.FromOpenTK(Oracle.ToOpenTK(matrix).Inverted());

                MathAssert.Near(matrix.Inverted(), expected, MathAssert.InverseD, MathAssert.AtLeastOne(MathAssert.Components(expected)),
                    Inputs.Label(i));
            }
        });
    }

    [Test]
    public void Inverted_Singular_Throws()
    {
        // Small integers with two equal columns: every product is exact, so the determinant is exactly 0.
        var twoEqualColumns = Matrix2d.FromColumns(new Vector2d(3.0, -2.0), new Vector2d(3.0, -2.0));

        Assert.Multiple(() =>
        {
            Assert.Throws<InvalidOperationException>(() => Matrix2d.Zero.Inverted(), nameof(Matrix2d.Zero));
            Assert.Throws<InvalidOperationException>(() => twoEqualColumns.Inverted(), "two equal columns");
        });
    }

    [Test]
    public void TryInvert_SingularAndInvertible_ReportsAndFills()
    {
        var singular = Matrix2d.TryInvert(Matrix2d.Zero, out var singularResult);
        var invertible = Matrix2d.TryInvert(Matrix2d.CreateScale(2.0), out var inverse);

        Assert.Multiple(() =>
        {
            Assert.That(singular, Is.False, "singular returns");
            MathAssert.Exact(singularResult, Matrix2d.FromColumns(new Vector2d(double.NaN), new Vector2d(double.NaN)), "singular result");
            Assert.That(invertible, Is.True, "invertible returns");
            MathAssert.Exact(inverse, Matrix2d.CreateScale(0.5), "invertible result");
        });
    }

    [Test]
    public void TryInvert_TinyButInvertible_ReturnsTrue()
    {
        // The determinant 1e-24 is tiny but not 0: the matrix is invertible.
        var invertible = Matrix2d.TryInvert(Matrix2d.CreateScale(1e-12), out var inverse);
        var expected = Matrix2d.CreateScale(1e12);

        Assert.Multiple(() =>
        {
            Assert.That(invertible, Is.True, "returns");
            MathAssert.Near(inverse, expected, MathAssert.SumsOfProductsD, MathAssert.AtLeastOne(MathAssert.Components(expected)), "result");
        });
    }

    [Test]
    public void CreateScale_Overloads_GiveDiagonal()
    {
        var diagonal = Matrix2d.FromRows(new Vector2d(2.0, 0.0), new Vector2d(0.0, 3.0));
        var uniform = Matrix2d.FromRows(new Vector2d(5.0, 0.0), new Vector2d(0.0, 5.0));

        Assert.Multiple(() =>
        {
            MathAssert.Exact(Matrix2d.CreateScale(5.0), uniform, "CreateScale(scale)");
            MathAssert.Exact(Matrix2d.CreateScale(new Vector2d(2.0, 3.0)), diagonal, "CreateScale(Vector2d)");
            MathAssert.Exact(Matrix2d.CreateScale(2.0, 3.0), diagonal, "CreateScale(x, y)");
        });
    }

    [Test]
    public void CreateRotation_HalfPi_TurnsUnitXToUnitY()
    {
        MathAssert.Near(Matrix2d.CreateRotation(Math.PI / 2) * Vector2d.UnitX, Vector2d.UnitY, MathAssert.TrigonometricD);
    }

    [Test]
    public void CreateRotations_RandomAngles_MatchOpenTK()
    {
        var random = Inputs.CreateRandom();

        Assert.Multiple(() =>
        {
            for (var i = 0; i < Inputs.Count; i++)
            {
                var angle = random.NextAngleD();

                MathAssert.Near(Matrix2d.CreateRotation(angle), Oracle.FromOpenTK(OpenTKMath.Matrix2d.CreateRotation(angle)),
                    MathAssert.TrigonometricD, 1.0, Inputs.Label(i));
            }
        });
    }

    [Test]
    public void Equality_SameAndDifferent_OperatorsAndEqualsAgree()
    {
        var random = Inputs.CreateRandom();
        var a = random.NextMatrix2d();
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
                    different[row, column] += 1.0;

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
        var a = Matrix2d.Identity;
        a[0, 0] = double.NaN;
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
        var positive = Matrix2d.Identity;
        positive[1, 0] = 0.0;
        var negative = Matrix2d.Identity;
        negative[1, 0] = double.NegativeZero;

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
        var matrix = Matrix2d.FromRows(new Vector2d(1.5, 2.0), new Vector2d(-3.0, 0.25));

        Assert.That(matrix.ToString(), Is.EqualTo("(1.5, 2)\n(-3, 0.25)"));
    }

    [Test]
    public void FloatConversion_Widening_IsExact()
    {
        var random = Inputs.CreateRandom();

        Assert.Multiple(() =>
        {
            for (var i = 0; i < Inputs.Count; i++)
            {
                var single = random.NextMatrix2();
                var label = Inputs.Label(i);

                Matrix2d widened = single;
                var back = (Matrix2)widened;

                MathAssert.Exact(MathAssert.Components(widened), Array.ConvertAll(MathAssert.Components(single), x => (double)x), label + " widened");
                MathAssert.Exact(back, single, label + " float -> double -> float");
            }
        });
    }

    [Test]
    public void FloatConversion_Narrowing_RoundsToNearest()
    {
        var matrix = Matrix2d.FromRows(new Vector2d(1.0 + Math.Pow(2, -30), 0.1), new Vector2d(-0.1, 3.0));

        var expected = Matrix2.FromRows(new Numerics.Vector2(1f, 0.1f), new Numerics.Vector2(-0.1f, 3f));

        MathAssert.Exact((Matrix2)matrix, expected);
    }
}
