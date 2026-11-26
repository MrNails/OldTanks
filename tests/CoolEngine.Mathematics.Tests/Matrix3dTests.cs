using System.Runtime.CompilerServices;
using CoolEngine.Mathematics.Tests.TestSupport;
using OpenTKMath = OpenTK.Mathematics;

namespace CoolEngine.Mathematics.Tests;

[TestFixture]
public sealed class Matrix3dTests
{
    private const int N = 3;

    [Test]
    public void Layout_FromRows_StoresColumnMajor()
    {
        var matrix = Matrix3d.FromRows(new Vector3d(1.0, 2.0, 3.0), new Vector3d(4.0, 5.0, 6.0), new Vector3d(7.0, 8.0, 9.0));

        var memory = MathAssert.Doubles(matrix);

        Assert.Multiple(() =>
        {
            Assert.That(Unsafe.SizeOf<Matrix3d>(), Is.EqualTo(Matrix3d.SizeInBytes));
            Assert.That(Unsafe.SizeOf<Matrix3d>(), Is.EqualTo(72));
            Assert.That(memory, Is.EqualTo(new[] { 1.0, 4.0, 7.0, 2.0, 5.0, 8.0, 3.0, 6.0, 9.0 }));
        });
    }

    [Test]
    public void FromColumns_Columns_AreTheFields()
    {
        var c0 = new Vector3d(1.0, 2.0, 3.0);
        var c1 = new Vector3d(4.0, 5.0, 6.0);
        var c2 = new Vector3d(7.0, 8.0, 9.0);

        var matrix = Matrix3d.FromColumns(c0, c1, c2);

        Assert.Multiple(() =>
        {
            MathAssert.Exact(matrix.Column0, c0, nameof(Matrix3d.Column0));
            MathAssert.Exact(matrix.Column1, c1, nameof(Matrix3d.Column1));
            MathAssert.Exact(matrix.Column2, c2, nameof(Matrix3d.Column2));
        });
    }

    [Test]
    public void Identity_Elements_AreDiagonalOnes()
    {
        var identity = Matrix3d.Identity;

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
                    var matrix = Matrix3d.Zero;
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
        var matrix = Matrix3d.Identity;

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
                var a = random.NextMatrix3d();
                var b = random.NextMatrix3d();

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
                var matrix = random.NextMatrix3d();
                var vector = random.NextVector3d();

                var expected = Oracle.FromOpenTK(OpenTKMath.Vector3d.TransformRow(Oracle.ToOpenTK(vector), Oracle.ToOpenTK(matrix)));

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
                var matrix = random.NextMatrix3d();

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
        MathAssert.Exact(Matrix3d.CreateScale(2.0).Determinant, 8.0);
    }

    [Test]
    public void Determinant_Random_MatchesOracle()
    {
        var random = Inputs.CreateRandom();

        Assert.Multiple(() =>
        {
            for (var i = 0; i < Inputs.Count; i++)
            {
                var matrix = random.NextMatrix3d();

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
                var matrix = random.NextRotationScale3d();

                MathAssert.Near(matrix * matrix.Inverted(), Matrix3d.Identity, MathAssert.InverseD, 1.0, Inputs.Label(i));
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
                var matrix = random.NextWellConditionedMatrix3d();

                var expected = Oracle.FromOpenTK(Oracle.ToOpenTK(matrix).Inverted());

                MathAssert.Near(matrix.Inverted(), expected, MathAssert.InverseD, MathAssert.AtLeastOne(MathAssert.Components(expected)),
                    Inputs.Label(i));
            }
        });
    }

    [Test]
    public void Inverted_Singular_Throws()
    {
        // Small integers with column 0 equal to column 2: every product is exact, so the determinant is exactly 0.
        var twoEqualColumns = Matrix3d.FromColumns(new Vector3d(1.0, 2.0, 3.0), new Vector3d(-2.0, 0.0, 5.0), new Vector3d(1.0, 2.0, 3.0));

        Assert.Multiple(() =>
        {
            Assert.Throws<InvalidOperationException>(() => Matrix3d.Zero.Inverted(), nameof(Matrix3d.Zero));
            Assert.Throws<InvalidOperationException>(() => twoEqualColumns.Inverted(), "two equal columns");
        });
    }

    [Test]
    public void TryInvert_SingularAndInvertible_ReportsAndFills()
    {
        var singular = Matrix3d.TryInvert(Matrix3d.Zero, out var singularResult);
        var invertible = Matrix3d.TryInvert(Matrix3d.CreateScale(2.0), out var inverse);

        Assert.Multiple(() =>
        {
            Assert.That(singular, Is.False, "singular returns");
            MathAssert.Exact(singularResult, Matrix3d.FromColumns(new Vector3d(double.NaN), new Vector3d(double.NaN), new Vector3d(double.NaN)),
                "singular result");
            Assert.That(invertible, Is.True, "invertible returns");
            MathAssert.Exact(inverse, Matrix3d.CreateScale(0.5), "invertible result");
        });
    }

    [Test]
    public void TryInvert_TinyButInvertible_ReturnsTrue()
    {
        // The determinant 1e-36 is tiny but not 0: the matrix is invertible.
        var invertible = Matrix3d.TryInvert(Matrix3d.CreateScale(1e-12), out var inverse);
        var expected = Matrix3d.CreateScale(1e12);

        Assert.Multiple(() =>
        {
            Assert.That(invertible, Is.True, "returns");
            MathAssert.Near(inverse, expected, MathAssert.SumsOfProductsD, MathAssert.AtLeastOne(MathAssert.Components(expected)), "result");
        });
    }

    // det 1e-315 is subnormal and 1 / det overflows to +∞; det 1e315 overflows to +∞; a NaN element makes det NaN.
    private static (string Name, Matrix3d Matrix)[] NonFiniteReciprocalCases()
    {
        var nanElement = Matrix3d.Identity;
        nanElement[0, 0] = double.NaN;
        return [("subnormal det", Matrix3d.CreateScale(1e-105)), ("overflowing det", Matrix3d.CreateScale(1e105)), ("NaN element", nanElement)];
    }

    [Test]
    public void TryInvert_NonFiniteReciprocal_ReturnsFalse()
    {
        var allNaN = Matrix3d.FromColumns(new Vector3d(double.NaN), new Vector3d(double.NaN), new Vector3d(double.NaN));

        Assert.Multiple(() =>
        {
            Assert.That(double.IsSubnormal(Matrix3d.CreateScale(1e-105).Determinant), Is.True, "premise: subnormal det");
            Assert.That(Matrix3d.CreateScale(1e105).Determinant, Is.EqualTo(double.PositiveInfinity), "premise: overflowing det");
            foreach (var (name, matrix) in NonFiniteReciprocalCases())
            {
                var returned = Matrix3d.TryInvert(matrix, out var result);
                Assert.That(returned, Is.False, name + " returns");
                MathAssert.Exact(result, allNaN, name + " result");
            }
        });
    }

    [Test]
    public void Inverted_NonFiniteReciprocal_Throws()
    {
        Assert.Multiple(() =>
        {
            foreach (var (name, matrix) in NonFiniteReciprocalCases())
                Assert.Throws<InvalidOperationException>(() => matrix.Inverted(), name);
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
                var m4 = random.NextMatrix4d();
                var m3 = random.NextMatrix3d();
                var label = Inputs.Label(i);

                var upperLeft = new Matrix3d(m4);
                var widened = new Matrix4d(m3);

                MathAssert.Exact(upperLeft, Matrix3d.FromColumns(Xyz(m4.Column0), Xyz(m4.Column1), Xyz(m4.Column2)), label + " Matrix3d(Matrix4d)");
                MathAssert.Exact(widened, Matrix4d.FromColumns(new Vector4d(m3.Column0, 0.0), new Vector4d(m3.Column1, 0.0),
                    new Vector4d(m3.Column2, 0.0), Vector4d.UnitW), label + " Matrix4d(Matrix3d)");
                MathAssert.Exact(new Matrix3d(widened), m3, label + " round trip");
            }
        });
    }

    [Test]
    public void CreateScale_Overloads_GiveDiagonal()
    {
        var diagonal = Matrix3d.FromRows(new Vector3d(2.0, 0.0, 0.0), new Vector3d(0.0, 3.0, 0.0), new Vector3d(0.0, 0.0, 4.0));
        var uniform = Matrix3d.FromRows(new Vector3d(5.0, 0.0, 0.0), new Vector3d(0.0, 5.0, 0.0), new Vector3d(0.0, 0.0, 5.0));

        Assert.Multiple(() =>
        {
            MathAssert.Exact(Matrix3d.CreateScale(5.0), uniform, "CreateScale(scale)");
            MathAssert.Exact(Matrix3d.CreateScale(new Vector3d(2.0, 3.0, 4.0)), diagonal, "CreateScale(Vector3d)");
            MathAssert.Exact(Matrix3d.CreateScale(2.0, 3.0, 4.0), diagonal, "CreateScale(x, y, z)");
        });
    }

    [Test]
    public void CreateRotationX_HalfPi_TurnsUnitYToUnitZ()
    {
        MathAssert.Near(Matrix3d.CreateRotationX(Math.PI / 2) * Vector3d.UnitY, Vector3d.UnitZ, MathAssert.TrigonometricD);
    }

    [Test]
    public void CreateRotationY_HalfPi_TurnsUnitZToUnitX()
    {
        MathAssert.Near(Matrix3d.CreateRotationY(Math.PI / 2) * Vector3d.UnitZ, Vector3d.UnitX, MathAssert.TrigonometricD);
    }

    [Test]
    public void CreateRotationZ_HalfPi_TurnsUnitXToUnitY()
    {
        MathAssert.Near(Matrix3d.CreateRotationZ(Math.PI / 2) * Vector3d.UnitX, Vector3d.UnitY, MathAssert.TrigonometricD);
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
                var label = Inputs.Label(i);

                MathAssert.Near(Matrix3d.CreateRotationX(angle), Oracle.FromOpenTK(OpenTKMath.Matrix3d.CreateRotationX(angle)),
                    MathAssert.TrigonometricD, 1.0, label + " X");
                MathAssert.Near(Matrix3d.CreateRotationY(angle), Oracle.FromOpenTK(OpenTKMath.Matrix3d.CreateRotationY(angle)),
                    MathAssert.TrigonometricD, 1.0, label + " Y");
                MathAssert.Near(Matrix3d.CreateRotationZ(angle), Oracle.FromOpenTK(OpenTKMath.Matrix3d.CreateRotationZ(angle)),
                    MathAssert.TrigonometricD, 1.0, label + " Z");
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
                var axis = random.NextVector3d();
                var angle = random.NextAngleD();

                var expected = Oracle.FromOpenTK(OpenTKMath.Matrix3d.CreateFromAxisAngle(Oracle.ToOpenTK(axis), angle));

                MathAssert.Near(Matrix3d.CreateFromAxisAngle(axis, angle), expected, MathAssert.TrigonometricD, 1.0, Inputs.Label(i));
            }
        });
    }

    [Test]
    public void CreateFromAxisAngle_ZeroAxis_IsIdentity()
    {
        MathAssert.Exact(Matrix3d.CreateFromAxisAngle(Vector3d.Zero, 1.0), Matrix3d.Identity);
    }

    // 2e154 squared overflows double to +∞; a NaN or infinite component makes the squared length non-finite too.
    [TestCase(2e154, 0.0, 0.0)]
    [TestCase(0.0, double.NaN, 0.0)]
    [TestCase(0.0, 0.0, double.NegativeInfinity)]
    public void CreateFromAxisAngle_NonFiniteAxis_Throws(double x, double y, double z)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Matrix3d.CreateFromAxisAngle(new Vector3d(x, y, z), 1.0));
    }

    [Test]
    public void CreateFromAxisAngle_UnnormalizedAxis_EqualsNormalized()
    {
        var random = Inputs.CreateRandom();

        Assert.Multiple(() =>
        {
            for (var i = 0; i < Inputs.Count; i++)
            {
                var axis = Vector3d.Normalize(random.NextVector3d());
                var angle = random.NextAngleD();

                MathAssert.Near(Matrix3d.CreateFromAxisAngle(axis * 5.0, angle), Matrix3d.CreateFromAxisAngle(axis, angle),
                    MathAssert.TrigonometricD, 1.0, Inputs.Label(i));
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
                var rotation = random.NextRotationD();
                var vector = random.NextVector3d();

                MathAssert.Near(Matrix3d.CreateFromQuaternion(rotation) * vector, Vector3d.Transform(vector, rotation), MathAssert.SumsOfProductsD,
                    MathAssert.RotationScales(rotation, vector), Inputs.Label(i));
            }
        });
    }

    [Test]
    public void Equality_SameAndDifferent_OperatorsAndEqualsAgree()
    {
        var random = Inputs.CreateRandom();
        var a = random.NextMatrix3d();
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
        var a = Matrix3d.Identity;
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
        var positive = Matrix3d.Identity;
        positive[1, 0] = 0.0;
        var negative = Matrix3d.Identity;
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
        var matrix = Matrix3d.FromRows(new Vector3d(1.5, 2.0, -3.0), new Vector3d(4.0, 5.0, 6.0), new Vector3d(7.0, 8.0, 0.25));

        Assert.That(matrix.ToString(), Is.EqualTo("(1.5, 2, -3)\n(4, 5, 6)\n(7, 8, 0.25)"));
    }

    [Test]
    public void FloatConversion_Widening_IsExact()
    {
        var random = Inputs.CreateRandom();

        Assert.Multiple(() =>
        {
            for (var i = 0; i < Inputs.Count; i++)
            {
                var single = random.NextMatrix3();
                var label = Inputs.Label(i);

                Matrix3d widened = single;
                var back = (Matrix3)widened;

                MathAssert.Exact(MathAssert.Components(widened), Array.ConvertAll(MathAssert.Components(single), x => (double)x), label + " widened");
                MathAssert.Exact(back, single, label + " float -> double -> float");
            }
        });
    }

    [Test]
    public void FloatConversion_Narrowing_RoundsToNearest()
    {
        var matrix = Matrix3d.Identity;
        matrix[0, 0] = 1.0 + Math.Pow(2, -30);
        matrix[1, 2] = 0.1;
        matrix[2, 1] = -0.1;

        var expected = Matrix3.Identity;
        expected[1, 2] = 0.1f;
        expected[2, 1] = -0.1f;

        MathAssert.Exact((Matrix3)matrix, expected);
    }

    private static Vector3d Xyz(Vector4d vector)
    {
        return new Vector3d(vector.X, vector.Y, vector.Z);
    }
}
