using System.Runtime.CompilerServices;
using CoolEngine.Mathematics.Tests.TestSupport;
using Numerics = System.Numerics;
using OpenTKMath = OpenTK.Mathematics;

namespace CoolEngine.Mathematics.Tests;

[TestFixture]
public sealed class Matrix4dTests
{
    private const int N = 4;

    [Test]
    public void Layout_FromRows_StoresColumnMajor()
    {
        var matrix = Matrix4d.FromRows(
            new Vector4d(1.0, 2.0, 3.0, 4.0),
            new Vector4d(5.0, 6.0, 7.0, 8.0),
            new Vector4d(9.0, 10.0, 11.0, 12.0),
            new Vector4d(13.0, 14.0, 15.0, 16.0));

        var memory = MathAssert.Doubles(matrix);

        Assert.Multiple(() =>
        {
            Assert.That(Unsafe.SizeOf<Matrix4d>(), Is.EqualTo(Matrix4d.SizeInBytes));
            Assert.That(Unsafe.SizeOf<Matrix4d>(), Is.EqualTo(128));
            Assert.That(memory, Is.EqualTo(new[] { 1.0, 5.0, 9.0, 13.0, 2.0, 6.0, 10.0, 14.0, 3.0, 7.0, 11.0, 15.0, 4.0, 8.0, 12.0, 16.0 }));
        });
    }

    [Test]
    public void FromColumns_Columns_AreTheFields()
    {
        var c0 = new Vector4d(1.0, 2.0, 3.0, 4.0);
        var c1 = new Vector4d(5.0, 6.0, 7.0, 8.0);
        var c2 = new Vector4d(9.0, 10.0, 11.0, 12.0);
        var c3 = new Vector4d(13.0, 14.0, 15.0, 16.0);

        var matrix = Matrix4d.FromColumns(c0, c1, c2, c3);

        Assert.Multiple(() =>
        {
            MathAssert.Exact(matrix.Column0, c0, nameof(Matrix4d.Column0));
            MathAssert.Exact(matrix.Column1, c1, nameof(Matrix4d.Column1));
            MathAssert.Exact(matrix.Column2, c2, nameof(Matrix4d.Column2));
            MathAssert.Exact(matrix.Column3, c3, nameof(Matrix4d.Column3));
        });
    }

    [Test]
    public void Identity_Elements_AreDiagonalOnes()
    {
        var identity = Matrix4d.Identity;

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
                    var matrix = Matrix4d.Zero;
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
        var matrix = Matrix4d.Identity;

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
                var a = random.NextMatrix4d();
                var b = random.NextMatrix4d();

                var expected = Oracle.FromOpenTK(Oracle.ToOpenTK(b) * Oracle.ToOpenTK(a));

                MathAssert.Near(a * b, expected, MathAssert.SumsOfProductsD, MathAssert.ProductScales(a, b), Inputs.Label(i));
            }
        });
    }

    [Test]
    public void Multiply_TranslateTimesScale_ScalesFirst()
    {
        var matrix = Matrix4d.CreateTranslation(10.0, 0.0, 0.0) * Matrix4d.CreateScale(2.0);

        MathAssert.Exact(matrix * new Vector4d(1.0, 1.0, 1.0, 1.0), new Vector4d(12.0, 2.0, 2.0, 1.0));
    }

    [Test]
    public void MultiplyVector_RandomInputs_MatchesOracle()
    {
        var random = Inputs.CreateRandom();

        Assert.Multiple(() =>
        {
            for (var i = 0; i < Inputs.Count; i++)
            {
                var matrix = random.NextMatrix4d();
                var vector = random.NextVector4d();

                var expected = Oracle.FromOpenTK(OpenTKMath.Vector4d.TransformRow(Oracle.ToOpenTK(vector), Oracle.ToOpenTK(matrix)));

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
                var matrix = random.NextMatrix4d();

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
        MathAssert.Exact(Matrix4d.CreateScale(2.0).Determinant, 8.0);
    }

    [Test]
    public void Determinant_Random_MatchesOracle()
    {
        var random = Inputs.CreateRandom();

        Assert.Multiple(() =>
        {
            for (var i = 0; i < Inputs.Count; i++)
            {
                var matrix = random.NextMatrix4d();

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
                var matrix = random.NextTrsD();

                MathAssert.Near(matrix * matrix.Inverted(), Matrix4d.Identity, MathAssert.InverseD, 1.0, Inputs.Label(i));
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
                var matrix = random.NextWellConditionedMatrix4d();

                var expected = Oracle.FromOpenTK(Oracle.ToOpenTK(matrix).Inverted());

                MathAssert.Near(matrix.Inverted(), expected, MathAssert.InverseD, MathAssert.AtLeastOne(MathAssert.Components(expected)),
                    Inputs.Label(i));
            }
        });
    }

    [Test]
    public void Inverted_Singular_Throws()
    {
        // Small integers with column 1 equal to column 3: every product is exact, so the determinant is exactly 0.
        var twoEqualColumns = Matrix4d.FromColumns(
            new Vector4d(1.0, 2.0, 3.0, 4.0),
            new Vector4d(2.0, -1.0, 0.0, 3.0),
            new Vector4d(5.0, 1.0, -2.0, 1.0),
            new Vector4d(2.0, -1.0, 0.0, 3.0));

        Assert.Multiple(() =>
        {
            Assert.Throws<InvalidOperationException>(() => Matrix4d.Zero.Inverted(), nameof(Matrix4d.Zero));
            Assert.Throws<InvalidOperationException>(() => twoEqualColumns.Inverted(), "two equal columns");
        });
    }

    [Test]
    public void TryInvert_SingularAndInvertible_ReportsAndFills()
    {
        var singular = Matrix4d.TryInvert(Matrix4d.Zero, out var singularResult);
        var invertible = Matrix4d.TryInvert(Matrix4d.CreateScale(2.0), out var inverse);

        Assert.Multiple(() =>
        {
            Assert.That(singular, Is.False, "singular returns");
            MathAssert.Exact(singularResult, Matrix4d.FromColumns(new Vector4d(double.NaN), new Vector4d(double.NaN), new Vector4d(double.NaN),
                new Vector4d(double.NaN)), "singular result");
            Assert.That(invertible, Is.True, "invertible returns");
            MathAssert.Exact(inverse, Matrix4d.CreateScale(0.5), "invertible result");
        });
    }

    [Test]
    public void TryInvert_TinyButInvertible_ReturnsTrue()
    {
        // The determinant 1e-36 (w stays 1) is tiny but not 0: the matrix is invertible.
        var invertible = Matrix4d.TryInvert(Matrix4d.CreateScale(1e-12), out var inverse);
        var expected = Matrix4d.CreateScale(1e12);

        Assert.Multiple(() =>
        {
            Assert.That(invertible, Is.True, "returns");
            MathAssert.Near(inverse, expected, MathAssert.SumsOfProductsD, MathAssert.AtLeastOne(MathAssert.Components(expected)), "result");
        });
    }

    [Test]
    public void Translation_GetAndSet_MapToColumn3()
    {
        var matrix = Matrix4d.FromColumns(new Vector4d(1.0), new Vector4d(2.0), new Vector4d(3.0), new Vector4d(4.0, 5.0, 6.0, 7.0));

        var translation = matrix.Translation;
        var changed = matrix;
        changed.Translation = new Vector3d(-8.0, -9.0, -10.0);

        Assert.Multiple(() =>
        {
            MathAssert.Exact(translation, new Vector3d(4.0, 5.0, 6.0), "get");
            MathAssert.Exact(changed, Matrix4d.FromColumns(new Vector4d(1.0), new Vector4d(2.0), new Vector4d(3.0),
                new Vector4d(-8.0, -9.0, -10.0, 7.0)), "set");
        });
    }

    [Test]
    public void ClearRotation_Trs_KeepsScaleTranslationAndW()
    {
        var random = Inputs.CreateRandom();

        Assert.Multiple(() =>
        {
            for (var i = 0; i < Inputs.Count; i++)
            {
                var trs = random.NextTrsD(out var scale);
                trs.Column0.W = random.NextComponentD();
                trs.Column1.W = random.NextComponentD();
                trs.Column2.W = random.NextComponentD();
                var label = Inputs.Label(i);

                var cleared = trs.ClearRotation();

                double[] diagonal = [scale.X, scale.Y, scale.Z];
                MathAssert.Near([cleared[0, 0], cleared[1, 1], cleared[2, 2]], diagonal, MathAssert.SumsOfProductsD,
                    MathAssert.AtLeastOne(diagonal), label + " diagonal = scale");
                MathAssert.Exact([cleared[1, 0], cleared[2, 0], cleared[0, 1], cleared[2, 1], cleared[0, 2], cleared[1, 2]],
                    [0.0, 0.0, 0.0, 0.0, 0.0, 0.0], label + " off-diagonal");
                MathAssert.Exact([cleared.Column0.W, cleared.Column1.W, cleared.Column2.W],
                    [trs.Column0.W, trs.Column1.W, trs.Column2.W], label + " W");
                MathAssert.Exact(cleared.Column3, trs.Column3, label + " Column3");
            }
        });
    }

    [Test]
    public void ClearRotation_Random_MatchesOpenTK()
    {
        var random = Inputs.CreateRandom();

        Assert.Multiple(() =>
        {
            for (var i = 0; i < Inputs.Count; i++)
            {
                var matrix = random.NextMatrix4d();

                var expected = Oracle.FromOpenTK(Oracle.ToOpenTK(matrix).ClearRotation());

                MathAssert.Near(matrix.ClearRotation(), expected, MathAssert.SumsOfProductsD,
                    MathAssert.AtLeastOne(MathAssert.Components(expected)), Inputs.Label(i));
            }
        });
    }

    [Test]
    public void TransformPosition_Translation_AddsOffset()
    {
        MathAssert.Exact(Matrix4d.CreateTranslation(1.0, 2.0, 3.0).TransformPosition(new Vector3d(1.0, 1.0, 1.0)), new Vector3d(2.0, 3.0, 4.0));
    }

    [Test]
    public void TransformPosition_RandomTrs_MatchesOpenTK()
    {
        var random = Inputs.CreateRandom();

        Assert.Multiple(() =>
        {
            for (var i = 0; i < Inputs.Count; i++)
            {
                var trs = random.NextTrsD();
                var position = random.NextVector3d();

                var expected = Oracle.FromOpenTK(OpenTKMath.Vector3d.TransformPosition(Oracle.ToOpenTK(position), Oracle.ToOpenTK(trs)));
                var scales = MathAssert.ProductScales(trs, new Vector4d(position, 1.0));

                MathAssert.Near(trs.TransformPosition(position), expected, MathAssert.SumsOfProductsD, scales.AsSpan(0, 3), Inputs.Label(i));
            }
        });
    }

    [Test]
    public void TransformDirection_Translation_IgnoresOffset()
    {
        MathAssert.Exact(Matrix4d.CreateTranslation(1.0, 2.0, 3.0).TransformDirection(new Vector3d(1.0, 1.0, 1.0)), new Vector3d(1.0, 1.0, 1.0));
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
        var diagonal = Matrix4d.FromRows(
            new Vector4d(2.0, 0.0, 0.0, 0.0),
            new Vector4d(0.0, 3.0, 0.0, 0.0),
            new Vector4d(0.0, 0.0, 4.0, 0.0),
            new Vector4d(0.0, 0.0, 0.0, 1.0));
        var uniform = Matrix4d.FromRows(
            new Vector4d(5.0, 0.0, 0.0, 0.0),
            new Vector4d(0.0, 5.0, 0.0, 0.0),
            new Vector4d(0.0, 0.0, 5.0, 0.0),
            new Vector4d(0.0, 0.0, 0.0, 1.0));

        Assert.Multiple(() =>
        {
            MathAssert.Exact(Matrix4d.CreateScale(5.0), uniform, "CreateScale(scale)");
            MathAssert.Exact(Matrix4d.CreateScale(new Vector3d(2.0, 3.0, 4.0)), diagonal, "CreateScale(Vector3d)");
            MathAssert.Exact(Matrix4d.CreateScale(2.0, 3.0, 4.0), diagonal, "CreateScale(x, y, z)");
        });
    }

    [Test]
    public void CreateTranslation_Overloads_AreEqual()
    {
        var expected = Matrix4d.FromColumns(Vector4d.UnitX, Vector4d.UnitY, Vector4d.UnitZ, new Vector4d(1.0, -2.0, 3.0, 1.0));

        Assert.Multiple(() =>
        {
            MathAssert.Exact(Matrix4d.CreateTranslation(new Vector3d(1.0, -2.0, 3.0)), expected, "CreateTranslation(Vector3d)");
            MathAssert.Exact(Matrix4d.CreateTranslation(1.0, -2.0, 3.0), expected, "CreateTranslation(x, y, z)");
        });
    }

    [Test]
    public void CreateRotationX_HalfPi_TurnsUnitYToUnitZ()
    {
        MathAssert.Near(Matrix4d.CreateRotationX(Math.PI / 2) * Vector4d.UnitY, Vector4d.UnitZ, MathAssert.TrigonometricD);
    }

    [Test]
    public void CreateRotationY_HalfPi_TurnsUnitZToUnitX()
    {
        MathAssert.Near(Matrix4d.CreateRotationY(Math.PI / 2) * Vector4d.UnitZ, Vector4d.UnitX, MathAssert.TrigonometricD);
    }

    [Test]
    public void CreateRotationZ_HalfPi_TurnsUnitXToUnitY()
    {
        MathAssert.Near(Matrix4d.CreateRotationZ(Math.PI / 2) * Vector4d.UnitX, Vector4d.UnitY, MathAssert.TrigonometricD);
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

                MathAssert.Near(Matrix4d.CreateRotationX(angle), Oracle.FromOpenTK(OpenTKMath.Matrix4d.CreateRotationX(angle)),
                    MathAssert.TrigonometricD, 1.0, label + " X");
                MathAssert.Near(Matrix4d.CreateRotationY(angle), Oracle.FromOpenTK(OpenTKMath.Matrix4d.CreateRotationY(angle)),
                    MathAssert.TrigonometricD, 1.0, label + " Y");
                MathAssert.Near(Matrix4d.CreateRotationZ(angle), Oracle.FromOpenTK(OpenTKMath.Matrix4d.CreateRotationZ(angle)),
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

                var expected = Oracle.FromOpenTK(OpenTKMath.Matrix4d.CreateFromAxisAngle(Oracle.ToOpenTK(axis), angle));

                MathAssert.Near(Matrix4d.CreateFromAxisAngle(axis, angle), expected, MathAssert.TrigonometricD, 1.0, Inputs.Label(i));
            }
        });
    }

    [Test]
    public void CreateFromAxisAngle_ZeroAxis_IsIdentity()
    {
        MathAssert.Exact(Matrix4d.CreateFromAxisAngle(Vector3d.Zero, 1.0), Matrix4d.Identity);
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

                MathAssert.Near(Matrix4d.CreateFromAxisAngle(axis * 5.0, angle), Matrix4d.CreateFromAxisAngle(axis, angle),
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

                var rotated = Xyz(Matrix4d.CreateFromQuaternion(rotation) * new Vector4d(vector, 0.0));

                MathAssert.Near(rotated, Vector3d.Transform(vector, rotation), MathAssert.SumsOfProductsD,
                    MathAssert.RotationScales(rotation, vector), Inputs.Label(i));
            }
        });
    }

    [Test]
    public void CreateFromQuaternion_RandomUnitQuaternions_MatchesOpenTK()
    {
        var random = Inputs.CreateRandom();

        Assert.Multiple(() =>
        {
            for (var i = 0; i < Inputs.Count; i++)
            {
                var rotation = random.NextRotationD();

                var expected = Oracle.FromOpenTK(OpenTKMath.Matrix4d.CreateFromQuaternion(Oracle.ToOpenTK(rotation)));

                MathAssert.Near(Matrix4d.CreateFromQuaternion(rotation), expected, MathAssert.TrigonometricD, 1.0, Inputs.Label(i));
            }
        });
    }

    [Test]
    public void LookAt_FromOriginAlongNegativeZ_IsIdentity()
    {
        MathAssert.Near(Matrix4d.LookAt(Vector3d.Zero, new Vector3d(0.0, 0.0, -1.0), Vector3d.UnitY), Matrix4d.Identity,
            MathAssert.TrigonometricD);
    }

    [Test]
    public void LookAt_RandomInputs_MatchesOpenTK()
    {
        var random = Inputs.CreateRandom();

        Assert.Multiple(() =>
        {
            for (var i = 0; i < Inputs.Count; i++)
            {
                var eye = random.NextVector3d();
                var target = random.NextVector3d();
                var up = random.NextVector3d();

                var expected = Oracle.FromOpenTK(OpenTKMath.Matrix4d.LookAt(Oracle.ToOpenTK(eye), Oracle.ToOpenTK(target), Oracle.ToOpenTK(up)));

                MathAssert.Near(Matrix4d.LookAt(eye, target, up), expected, MathAssert.ProjectionD, LookAtScales(expected, eye), Inputs.Label(i));
            }
        });
    }

    [Test]
    public void CreatePerspectiveFieldOfView_NearAndFarPoints_MapToOneAndZero()
    {
        var projection = Matrix4d.CreatePerspectiveFieldOfView(Math.PI / 4, 16.0 / 9.0, 0.1, 1000.0);

        Assert.Multiple(() =>
        {
            MathAssert.Near(Project(projection, new Vector3d(0.0, 0.0, -0.1)).Z, 1.0, MathAssert.ProjectionD, 1.0, "near");
            MathAssert.Near(Project(projection, new Vector3d(0.0, 0.0, -1000.0)).Z, 0.0, MathAssert.ProjectionD, 1.0, "far");
        });
    }

    [Test]
    public void CreatePerspectiveFieldOfView_RandomPoints_MatchFloatXY()
    {
        var projection = Matrix4d.CreatePerspectiveFieldOfView(Math.PI / 4, 16.0 / 9.0, 0.1, 1000.0);
        var single = Matrix4.CreatePerspectiveFieldOfView(MathHelper.PiOver4, 16f / 9f, 0.1f, 1000f);
        var random = Inputs.CreateRandom();

        Assert.Multiple(() =>
        {
            for (var i = 0; i < Inputs.Count; i++)
            {
                var point = new Numerics.Vector3(random.NextComponent(), random.NextComponent(), -1f - 999f * random.NextSingle());

                AssertProjectionsAgree(projection, single, point, Inputs.Label(i));
            }
        });
    }

    [TestCase(0.0, 16.0 / 9.0, 0.1, 1000.0)]
    [TestCase(Math.PI, 16.0 / 9.0, 0.1, 1000.0)]
    [TestCase(Math.PI / 4, 0.0, 0.1, 1000.0)]
    [TestCase(Math.PI / 4, 16.0 / 9.0, 0.0, 1000.0)]
    [TestCase(Math.PI / 4, 16.0 / 9.0, 0.1, 0.1)]
    [TestCase(Math.PI / 4, 16.0 / 9.0, 0.1, double.PositiveInfinity)]
    public void CreatePerspectiveFieldOfView_InvalidArguments_Throw(double fovY, double aspectRatio, double depthNear, double depthFar)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Matrix4d.CreatePerspectiveFieldOfView(fovY, aspectRatio, depthNear, depthFar));
    }

    [Test]
    public void CreatePerspectiveFieldOfViewInfinite_DepthIsNearOverDistance()
    {
        var projection = Matrix4d.CreatePerspectiveFieldOfViewInfinite(Math.PI / 4, 16.0 / 9.0, 0.1);

        Assert.Multiple(() =>
        {
            foreach (var (distance, depth) in new[] { (0.1, 1.0), (1.0, 0.1), (1e6, 1e-7) })
            {
                MathAssert.Near(Project(projection, new Vector3d(0.0, 0.0, -distance)).Z, depth, MathAssert.ProjectionD, depth,
                    $"distance {distance}");
            }
        });
    }

    [Test]
    public void CreatePerspectiveFieldOfViewInfinite_RandomPoints_MatchFloatXY()
    {
        var projection = Matrix4d.CreatePerspectiveFieldOfViewInfinite(Math.PI / 4, 16.0 / 9.0, 0.1);
        var single = Matrix4.CreatePerspectiveFieldOfViewInfinite(MathHelper.PiOver4, 16f / 9f, 0.1f);
        var random = Inputs.CreateRandom();

        Assert.Multiple(() =>
        {
            for (var i = 0; i < Inputs.Count; i++)
            {
                var point = new Numerics.Vector3(random.NextComponent(), random.NextComponent(), -1f - 999f * random.NextSingle());

                AssertProjectionsAgree(projection, single, point, Inputs.Label(i));
            }
        });
    }

    [TestCase(0.0, 16.0 / 9.0, 0.1)]
    [TestCase(Math.PI / 4, 0.0, 0.1)]
    [TestCase(Math.PI / 4, 16.0 / 9.0, 0.0)]
    public void CreatePerspectiveFieldOfViewInfinite_InvalidArguments_Throw(double fovY, double aspectRatio, double depthNear)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Matrix4d.CreatePerspectiveFieldOfViewInfinite(fovY, aspectRatio, depthNear));
    }

    [Test]
    public void CreateOrthographicOffCenter_Corners_MapToClipBox()
    {
        var projection = Matrix4d.CreateOrthographicOffCenter(-2.0, 6.0, -1.0, 3.0, 0.5, 10.0);

        Assert.Multiple(() =>
        {
            MathAssert.Near(Project(projection, new Vector3d(-2.0, -1.0, -0.5)), new Vector3d(-1.0, -1.0, 1.0), MathAssert.ProjectionD, 1.0,
                "near corner");
            MathAssert.Near(Project(projection, new Vector3d(6.0, 3.0, -10.0)), new Vector3d(1.0, 1.0, 0.0), MathAssert.ProjectionD, 1.0,
                "far corner");
        });
    }

    [Test]
    public void CreateOrthographicOffCenter_ScreenSpace_PutsZeroDepthMidway()
    {
        var projection = Matrix4d.CreateOrthographicOffCenter(0.0, 800.0, 0.0, 600.0, -1.0, 1.0);

        MathAssert.Near(Project(projection, Vector3d.Zero), new Vector3d(-1.0, -1.0, 0.5), MathAssert.ProjectionD);
    }

    [Test]
    public void CreateOrthographicOffCenter_EmptyRange_Throws()
    {
        Assert.Multiple(() =>
        {
            Assert.Throws<ArgumentException>(() => Matrix4d.CreateOrthographicOffCenter(1.0, 1.0, 0.0, 1.0, 0.0, 1.0), "left == right");
            Assert.Throws<ArgumentException>(() => Matrix4d.CreateOrthographicOffCenter(0.0, 1.0, 2.0, 2.0, 0.0, 1.0), "bottom == top");
            Assert.Throws<ArgumentException>(() => Matrix4d.CreateOrthographicOffCenter(0.0, 1.0, 0.0, 1.0, 3.0, 3.0), "near == far");
        });
    }

    [Test]
    public void Equality_SameAndDifferent_OperatorsAndEqualsAgree()
    {
        var random = Inputs.CreateRandom();
        var a = random.NextMatrix4d();
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
        var a = Matrix4d.Identity;
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
        var positive = Matrix4d.Identity;
        positive[1, 0] = 0.0;
        var negative = Matrix4d.Identity;
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
        var matrix = Matrix4d.FromRows(
            new Vector4d(1.5, 2.0, -3.0, 0.25),
            new Vector4d(5.0, 6.0, 7.0, 8.0),
            new Vector4d(9.0, 10.0, 11.0, 12.0),
            new Vector4d(13.0, 14.0, 15.0, 16.0));

        Assert.That(matrix.ToString(), Is.EqualTo("(1.5, 2, -3, 0.25)\n(5, 6, 7, 8)\n(9, 10, 11, 12)\n(13, 14, 15, 16)"));
    }

    [Test]
    public void FloatConversion_Widening_IsExact()
    {
        var random = Inputs.CreateRandom();

        Assert.Multiple(() =>
        {
            for (var i = 0; i < Inputs.Count; i++)
            {
                var single = random.NextMatrix4();
                var label = Inputs.Label(i);

                Matrix4d widened = single;
                var back = (Matrix4)widened;

                MathAssert.Exact(MathAssert.Components(widened), Array.ConvertAll(MathAssert.Components(single), x => (double)x), label + " widened");
                MathAssert.Exact(back, single, label + " float -> double -> float");
            }
        });
    }

    [Test]
    public void FloatConversion_Narrowing_RoundsToNearest()
    {
        var matrix = Matrix4d.Identity;
        matrix[0, 0] = 1.0 + Math.Pow(2, -30);
        matrix[1, 2] = 0.1;
        matrix[3, 1] = -0.1;

        var expected = Matrix4.Identity;
        expected[1, 2] = 0.1f;
        expected[3, 1] = -0.1f;

        MathAssert.Exact((Matrix4)matrix, expected);
    }

    // LookAt's per-element scales for ProjectionD (1e-12): the rotation part and Column3.W max(1, |expected|); Column3's
    // X, Y, Z (-dot(axis, eye), which cancels terms of about 100) the sums-of-products tolerance (1e-14) times
    // |axis.X·eye.X| + |axis.Y·eye.Y| + |axis.Z·eye.Z| of that row's axis, with the floor 1e-12 × max(1, |expected|).
    private static double[] LookAtScales(Matrix4d expected, Vector3d eye)
    {
        var scales = MathAssert.AtLeastOne(MathAssert.Components(expected));
        for (var row = 0; row < 3; row++)
        {
            var sumOfProducts = MathAssert.SumOfAbs(expected[row, 0] * eye.X, expected[row, 1] * eye.Y, expected[row, 2] * eye.Z);
            var index = 3 * N + row;
            scales[index] = Math.Max(scales[index], MathAssert.SumsOfProductsD / MathAssert.ProjectionD * sumOfProducts);
        }

        return scales;
    }

    // The double projection's x/w and y/w within 1e-5 × max(1, |expected|) of the float projection's: no double oracle with
    // reversed Z exists, so the float library (itself tested against System.Numerics) is the reference.
    private static void AssertProjectionsAgree(Matrix4d projection, Matrix4 single, Numerics.Vector3 point, string label)
    {
        var clip = single * new Numerics.Vector4(point, 1f);
        double[] expected = [clip.X / clip.W, clip.Y / clip.W];
        var projected = Project(projection, new Vector3d(point.X, point.Y, point.Z));

        MathAssert.Near([projected.X, projected.Y], expected, MathAssert.Projection, MathAssert.AtLeastOne(expected), label);
    }

    // The point (x, y, z, 1) through the projection, divided by w.
    private static Vector3d Project(Matrix4d projection, Vector3d point)
    {
        var clip = projection * new Vector4d(point, 1.0);
        return Xyz(clip) / clip.W;
    }

    private static Vector3d Xyz(Vector4d vector)
    {
        return new Vector3d(vector.X, vector.Y, vector.Z);
    }
}
