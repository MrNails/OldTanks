using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;
using CoolEngine.Mathematics.Tests.TestSupport;
using Numerics = System.Numerics;
using OpenTKMath = OpenTK.Mathematics;

namespace CoolEngine.Mathematics.Tests;

[TestFixture]
public sealed class Matrix4Tests
{
    private const int N = 4;

    [Test]
    public void Layout_FromRows_StoresColumnMajor()
    {
        var matrix = Matrix4.FromRows(
            new Vector4(1f, 2f, 3f, 4f),
            new Vector4(5f, 6f, 7f, 8f),
            new Vector4(9f, 10f, 11f, 12f),
            new Vector4(13f, 14f, 15f, 16f));

        var memory = MathAssert.Floats(matrix);

        Assert.Multiple(() =>
        {
            Assert.That(Unsafe.SizeOf<Matrix4>(), Is.EqualTo(Matrix4.SizeInBytes));
            Assert.That(Unsafe.SizeOf<Matrix4>(), Is.EqualTo(64));
            Assert.That(memory, Is.EqualTo(new[] { 1f, 5f, 9f, 13f, 2f, 6f, 10f, 14f, 3f, 7f, 11f, 15f, 4f, 8f, 12f, 16f }));
        });
    }

    [Test]
    public void FromColumns_Columns_AreTheFields()
    {
        var c0 = new Vector4(1f, 2f, 3f, 4f);
        var c1 = new Vector4(5f, 6f, 7f, 8f);
        var c2 = new Vector4(9f, 10f, 11f, 12f);
        var c3 = new Vector4(13f, 14f, 15f, 16f);

        var matrix = Matrix4.FromColumns(c0, c1, c2, c3);

        Assert.Multiple(() =>
        {
            MathAssert.Exact(matrix.Column0, c0, nameof(Matrix4.Column0));
            MathAssert.Exact(matrix.Column1, c1, nameof(Matrix4.Column1));
            MathAssert.Exact(matrix.Column2, c2, nameof(Matrix4.Column2));
            MathAssert.Exact(matrix.Column3, c3, nameof(Matrix4.Column3));
        });
    }

    [Test]
    public void Identity_Elements_AreDiagonalOnes()
    {
        var identity = Matrix4.Identity;

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
                    var matrix = Matrix4.Zero;
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
        var matrix = Matrix4.Identity;

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
                var a = random.NextMatrix4();
                var b = random.NextMatrix4();

                var expected = Oracle.FromNumerics(Oracle.ToNumerics(b) * Oracle.ToNumerics(a));

                MathAssert.Near(a * b, expected, MathAssert.SumsOfProducts, MathAssert.ProductScales(a, b), Inputs.Label(i));
            }
        });
    }

    [Test]
    public void Multiply_TranslateTimesScale_ScalesFirst()
    {
        var matrix = Matrix4.CreateTranslation(10f, 0f, 0f) * Matrix4.CreateScale(2f);

        MathAssert.Exact(matrix * new Vector4(1f, 1f, 1f, 1f), new Vector4(12f, 2f, 2f, 1f));
    }

    [Test]
    public void MultiplyVector_RandomInputs_MatchesOracle()
    {
        var random = Inputs.CreateRandom();

        Assert.Multiple(() =>
        {
            for (var i = 0; i < Inputs.Count; i++)
            {
                var matrix = random.NextMatrix4();
                var vector = random.NextVector4();

                var expected = Vector4.Transform(vector, Oracle.ToNumerics(matrix));

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
                var matrix = random.NextMatrix4();

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
        MathAssert.Exact(Matrix4.CreateScale(2f).Determinant, 8f);
    }

    [Test]
    public void Determinant_Random_MatchesOracle()
    {
        var random = Inputs.CreateRandom();

        Assert.Multiple(() =>
        {
            for (var i = 0; i < Inputs.Count; i++)
            {
                var matrix = random.NextMatrix4();

                MathAssert.Near(matrix.Determinant, Oracle.ToNumerics(matrix).GetDeterminant(), MathAssert.SumsOfProducts,
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
                var matrix = random.NextTrs();

                MathAssert.Near(matrix * matrix.Inverted(), Matrix4.Identity, MathAssert.Inverse, 1f, Inputs.Label(i));
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
                var matrix = random.NextWellConditionedMatrix4();

                Numerics.Matrix4x4.Invert(Oracle.ToNumerics(matrix), out var inverse);
                var expected = Oracle.FromNumerics(inverse);

                MathAssert.Near(matrix.Inverted(), expected, MathAssert.Inverse, MathAssert.AtLeastOne(MathAssert.Components(expected)),
                    Inputs.Label(i));
            }
        });
    }

    [Test]
    public void Inverted_Singular_Throws()
    {
        // Small integers with column 1 equal to column 3: every product is exact, so the determinant is exactly 0.
        var twoEqualColumns = Matrix4.FromColumns(
            new Vector4(1f, 2f, 3f, 4f),
            new Vector4(2f, -1f, 0f, 3f),
            new Vector4(5f, 1f, -2f, 1f),
            new Vector4(2f, -1f, 0f, 3f));

        Assert.Multiple(() =>
        {
            Assert.Throws<InvalidOperationException>(() => Matrix4.Zero.Inverted(), nameof(Matrix4.Zero));
            Assert.Throws<InvalidOperationException>(() => twoEqualColumns.Inverted(), "two equal columns");
        });
    }

    [Test]
    public void TryInvert_SingularAndInvertible_ReportsAndFills()
    {
        var singular = Matrix4.TryInvert(Matrix4.Zero, out var singularResult);
        var invertible = Matrix4.TryInvert(Matrix4.CreateScale(2f), out var inverse);

        Assert.Multiple(() =>
        {
            Assert.That(singular, Is.False, "singular returns");
            MathAssert.Exact(singularResult, Matrix4.FromColumns(new Vector4(float.NaN), new Vector4(float.NaN), new Vector4(float.NaN),
                new Vector4(float.NaN)), "singular result");
            Assert.That(invertible, Is.True, "invertible returns");
            MathAssert.Exact(inverse, Matrix4.CreateScale(0.5f), "invertible result");
        });
    }

    [Test]
    public void TryInvert_TinyButInvertible_ReturnsTrue()
    {
        // The determinant 1e-36 (w stays 1) is tiny but not 0: the matrix is invertible.
        var invertible = Matrix4.TryInvert(Matrix4.CreateScale(1e-12f), out var inverse);
        var expected = Matrix4.CreateScale(1e12f);

        Assert.Multiple(() =>
        {
            Assert.That(invertible, Is.True, "returns");
            MathAssert.Near(inverse, expected, 1e-6f, MathAssert.AtLeastOne(MathAssert.Components(expected)), "result");
        });
    }

    [Test]
    public void Translation_GetAndSet_MapToColumn3()
    {
        var matrix = Matrix4.FromColumns(new Vector4(1f), new Vector4(2f), new Vector4(3f), new Vector4(4f, 5f, 6f, 7f));

        var translation = matrix.Translation;
        var changed = matrix;
        changed.Translation = new Vector3(-8f, -9f, -10f);

        Assert.Multiple(() =>
        {
            MathAssert.Exact(translation, new Vector3(4f, 5f, 6f), "get");
            MathAssert.Exact(changed, Matrix4.FromColumns(new Vector4(1f), new Vector4(2f), new Vector4(3f),
                new Vector4(-8f, -9f, -10f, 7f)), "set");
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
                var trs = random.NextTrs(out var scale);
                trs.Column0.W = random.NextComponent();
                trs.Column1.W = random.NextComponent();
                trs.Column2.W = random.NextComponent();
                var label = Inputs.Label(i);

                var cleared = trs.ClearRotation();

                float[] diagonal = [scale.X, scale.Y, scale.Z];
                MathAssert.Near([cleared[0, 0], cleared[1, 1], cleared[2, 2]], diagonal, 1e-6f, MathAssert.AtLeastOne(diagonal),
                    label + " diagonal = scale");
                MathAssert.Exact([cleared[1, 0], cleared[2, 0], cleared[0, 1], cleared[2, 1], cleared[0, 2], cleared[1, 2]],
                    [0f, 0f, 0f, 0f, 0f, 0f], label + " off-diagonal");
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
                var matrix = random.NextMatrix4();

                var expected = Oracle.FromOpenTK(Oracle.ToOpenTK(matrix).ClearRotation());

                MathAssert.Near(matrix.ClearRotation(), expected, MathAssert.SumsOfProducts,
                    MathAssert.AtLeastOne(MathAssert.Components(expected)), Inputs.Label(i));
            }
        });
    }

    [Test]
    public void TransformPosition_Translation_AddsOffset()
    {
        MathAssert.Exact(Matrix4.CreateTranslation(1f, 2f, 3f).TransformPosition(new Vector3(1f, 1f, 1f)), new Vector3(2f, 3f, 4f));
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

                var expected = Vector3.Transform(position, Oracle.ToNumerics(trs));
                var scales = MathAssert.ProductScales(trs, new Vector4(position, 1f));

                MathAssert.Near(trs.TransformPosition(position), expected, MathAssert.SumsOfProducts, scales.AsSpan(0, 3),
                    Inputs.Label(i));
            }
        });
    }

    [Test]
    public void TransformDirection_Translation_IgnoresOffset()
    {
        MathAssert.Exact(Matrix4.CreateTranslation(1f, 2f, 3f).TransformDirection(new Vector3(1f, 1f, 1f)), new Vector3(1f, 1f, 1f));
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
        var diagonal = Matrix4.FromRows(
            new Vector4(2f, 0f, 0f, 0f),
            new Vector4(0f, 3f, 0f, 0f),
            new Vector4(0f, 0f, 4f, 0f),
            new Vector4(0f, 0f, 0f, 1f));
        var uniform = Matrix4.FromRows(
            new Vector4(5f, 0f, 0f, 0f),
            new Vector4(0f, 5f, 0f, 0f),
            new Vector4(0f, 0f, 5f, 0f),
            new Vector4(0f, 0f, 0f, 1f));

        Assert.Multiple(() =>
        {
            MathAssert.Exact(Matrix4.CreateScale(5f), uniform, "CreateScale(scale)");
            MathAssert.Exact(Matrix4.CreateScale(new Vector3(2f, 3f, 4f)), diagonal, "CreateScale(Vector3)");
            MathAssert.Exact(Matrix4.CreateScale(2f, 3f, 4f), diagonal, "CreateScale(x, y, z)");
        });
    }

    [Test]
    public void CreateTranslation_Overloads_AreEqual()
    {
        var expected = Matrix4.FromColumns(Vector4.UnitX, Vector4.UnitY, Vector4.UnitZ, new Vector4(1f, -2f, 3f, 1f));

        Assert.Multiple(() =>
        {
            MathAssert.Exact(Matrix4.CreateTranslation(new Vector3(1f, -2f, 3f)), expected, "CreateTranslation(Vector3)");
            MathAssert.Exact(Matrix4.CreateTranslation(1f, -2f, 3f), expected, "CreateTranslation(x, y, z)");
        });
    }

    [Test]
    public void CreateRotationX_HalfPi_TurnsUnitYToUnitZ()
    {
        MathAssert.Near(Matrix4.CreateRotationX(MathHelper.PiOver2) * Vector4.UnitY, Vector4.UnitZ, 1e-6f);
    }

    [Test]
    public void CreateRotationY_HalfPi_TurnsUnitZToUnitX()
    {
        MathAssert.Near(Matrix4.CreateRotationY(MathHelper.PiOver2) * Vector4.UnitZ, Vector4.UnitX, 1e-6f);
    }

    [Test]
    public void CreateRotationZ_HalfPi_TurnsUnitXToUnitY()
    {
        MathAssert.Near(Matrix4.CreateRotationZ(MathHelper.PiOver2) * Vector4.UnitX, Vector4.UnitY, 1e-6f);
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

                MathAssert.Near(Matrix4.CreateRotationX(angle), Oracle.FromOpenTK(OpenTKMath.Matrix4.CreateRotationX(angle)),
                    MathAssert.Trigonometric, 1f, label + " X");
                MathAssert.Near(Matrix4.CreateRotationY(angle), Oracle.FromOpenTK(OpenTKMath.Matrix4.CreateRotationY(angle)),
                    MathAssert.Trigonometric, 1f, label + " Y");
                MathAssert.Near(Matrix4.CreateRotationZ(angle), Oracle.FromOpenTK(OpenTKMath.Matrix4.CreateRotationZ(angle)),
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

                var expected = Oracle.FromOpenTK(OpenTKMath.Matrix4.CreateFromAxisAngle(Oracle.ToOpenTK(axis), angle));

                MathAssert.Near(Matrix4.CreateFromAxisAngle(axis, angle), expected, MathAssert.Trigonometric, 1f, Inputs.Label(i));
            }
        });
    }

    [Test]
    public void CreateFromAxisAngle_ZeroAxis_IsIdentity()
    {
        MathAssert.Exact(Matrix4.CreateFromAxisAngle(Vector3.Zero, 1f), Matrix4.Identity);
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

                MathAssert.Near(Matrix4.CreateFromAxisAngle(axis * 5f, angle), Matrix4.CreateFromAxisAngle(axis, angle),
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

                var rotated = (Matrix4.CreateFromQuaternion(rotation) * new Vector4(vector, 0f)).AsVector128().AsVector3();

                MathAssert.Near(rotated, Vector3.Transform(vector, rotation), MathAssert.SumsOfProducts, MathAssert.RotationScales(rotation, vector),
                    Inputs.Label(i));
            }
        });
    }

    [Test]
    public void CreateFromQuaternion_RandomUnitQuaternions_MatchesSystemNumerics()
    {
        var random = Inputs.CreateRandom();

        Assert.Multiple(() =>
        {
            for (var i = 0; i < Inputs.Count; i++)
            {
                var rotation = random.NextRotation();

                var expected = Oracle.FromNumerics(Numerics.Matrix4x4.CreateFromQuaternion(rotation));

                MathAssert.Near(Matrix4.CreateFromQuaternion(rotation), expected, MathAssert.Trigonometric, 1f, Inputs.Label(i));
            }
        });
    }

    [Test]
    public void LookAt_FromOriginAlongNegativeZ_IsIdentity()
    {
        MathAssert.Near(Matrix4.LookAt(Vector3.Zero, new Vector3(0f, 0f, -1f), Vector3.UnitY), Matrix4.Identity, 1e-6f);
    }

    [Test]
    public void LookAt_RandomInputs_MatchesOpenTK()
    {
        var random = Inputs.CreateRandom();

        Assert.Multiple(() =>
        {
            for (var i = 0; i < Inputs.Count; i++)
            {
                var eye = random.NextVector3();
                var target = random.NextVector3();
                var up = random.NextVector3();

                var expected = Oracle.FromOpenTK(OpenTKMath.Matrix4.LookAt(Oracle.ToOpenTK(eye), Oracle.ToOpenTK(target), Oracle.ToOpenTK(up)));

                MathAssert.Near(Matrix4.LookAt(eye, target, up), expected, MathAssert.Projection, LookAtScales(expected, eye),
                    Inputs.Label(i));
            }
        });
    }

    [Test]
    public void CreatePerspectiveFieldOfView_NearAndFarPoints_MapToOneAndZero()
    {
        var projection = Matrix4.CreatePerspectiveFieldOfView(MathHelper.PiOver4, 16f / 9f, 0.1f, 1000f);

        Assert.Multiple(() =>
        {
            MathAssert.Near(Project(projection, new Vector3(0f, 0f, -0.1f)).Z, 1f, 1e-5f, 1f, "near");
            MathAssert.Near(Project(projection, new Vector3(0f, 0f, -1000f)).Z, 0f, 1e-5f, 1f, "far");
        });
    }

    [Test]
    public void CreatePerspectiveFieldOfView_RandomPoints_MatchSystemNumericsXY()
    {
        const float fovY = MathHelper.PiOver4;
        const float aspectRatio = 16f / 9f;
        const float depthNear = 0.1f;
        const float depthFar = 1000f;
        var projection = Matrix4.CreatePerspectiveFieldOfView(fovY, aspectRatio, depthNear, depthFar);
        var oracle = Numerics.Matrix4x4.CreatePerspectiveFieldOfView(fovY, aspectRatio, depthNear, depthFar);
        var random = Inputs.CreateRandom();

        Assert.Multiple(() =>
        {
            for (var i = 0; i < Inputs.Count; i++)
            {
                // In front of the camera: x, y in [-100, 100], z in [-1000, -1].
                var point = new Vector3(random.NextComponent(), random.NextComponent(), -1f - 999f * random.NextSingle());

                var clip = Numerics.Vector4.Transform(new Numerics.Vector4(point.X, point.Y, point.Z, 1f), oracle);
                var expected = new Vector2(clip.X / clip.W, clip.Y / clip.W);

                var projected = Project(projection, point);
                MathAssert.Near(new Vector2(projected.X, projected.Y), expected, MathAssert.Projection,
                    MathAssert.AtLeastOne(MathAssert.Components(expected)), Inputs.Label(i));
            }
        });
    }

    [TestCase(0f, 16f / 9f, 0.1f, 1000f)]
    [TestCase(MathHelper.Pi, 16f / 9f, 0.1f, 1000f)]
    [TestCase(MathHelper.PiOver4, 0f, 0.1f, 1000f)]
    [TestCase(MathHelper.PiOver4, 16f / 9f, 0f, 1000f)]
    [TestCase(MathHelper.PiOver4, 16f / 9f, 0.1f, 0.1f)]
    [TestCase(MathHelper.PiOver4, 16f / 9f, 0.1f, float.PositiveInfinity)]
    public void CreatePerspectiveFieldOfView_InvalidArguments_Throw(float fovY, float aspectRatio, float depthNear, float depthFar)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Matrix4.CreatePerspectiveFieldOfView(fovY, aspectRatio, depthNear, depthFar));
    }

    [Test]
    public void CreatePerspectiveFieldOfViewInfinite_DepthIsNearOverDistance()
    {
        var projection = Matrix4.CreatePerspectiveFieldOfViewInfinite(MathHelper.PiOver4, 16f / 9f, 0.1f);

        Assert.Multiple(() =>
        {
            foreach (var (distance, depth) in new[] { (0.1f, 1f), (1f, 0.1f), (1e6f, 1e-7f) })
            {
                MathAssert.Near(Project(projection, new Vector3(0f, 0f, -distance)).Z, depth, 1e-5f, depth,
                    $"distance {distance}");
            }
        });
    }

    [Test]
    public void CreatePerspectiveFieldOfViewInfinite_RandomPoints_MatchSystemNumericsXY()
    {
        const float fovY = MathHelper.PiOver4;
        const float aspectRatio = 16f / 9f;
        const float depthNear = 0.1f;
        var projection = Matrix4.CreatePerspectiveFieldOfViewInfinite(fovY, aspectRatio, depthNear);

        // x/w and y/w do not depend on the far plane, so any finite far plane gives the oracle.
        var oracle = Numerics.Matrix4x4.CreatePerspectiveFieldOfView(fovY, aspectRatio, depthNear, 1000f);
        var random = Inputs.CreateRandom();

        Assert.Multiple(() =>
        {
            for (var i = 0; i < Inputs.Count; i++)
            {
                // In front of the camera: x, y in [-100, 100], z in [-1000, -1].
                var point = new Vector3(random.NextComponent(), random.NextComponent(), -1f - 999f * random.NextSingle());

                var clip = Numerics.Vector4.Transform(new Numerics.Vector4(point.X, point.Y, point.Z, 1f), oracle);
                var expected = new Vector2(clip.X / clip.W, clip.Y / clip.W);

                var projected = Project(projection, point);
                MathAssert.Near(new Vector2(projected.X, projected.Y), expected, MathAssert.Projection,
                    MathAssert.AtLeastOne(MathAssert.Components(expected)), Inputs.Label(i));
            }
        });
    }

    [TestCase(0f, 16f / 9f, 0.1f)]
    [TestCase(MathHelper.PiOver4, 0f, 0.1f)]
    [TestCase(MathHelper.PiOver4, 16f / 9f, 0f)]
    public void CreatePerspectiveFieldOfViewInfinite_InvalidArguments_Throw(float fovY, float aspectRatio, float depthNear)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Matrix4.CreatePerspectiveFieldOfViewInfinite(fovY, aspectRatio, depthNear));
    }

    [Test]
    public void CreateOrthographicOffCenter_Corners_MapToClipBox()
    {
        var projection = Matrix4.CreateOrthographicOffCenter(-2f, 6f, -1f, 3f, 0.5f, 10f);

        Assert.Multiple(() =>
        {
            MathAssert.Near(Project(projection, new Vector3(-2f, -1f, -0.5f)), new Vector3(-1f, -1f, 1f), 1e-6f, 1f, "near corner");
            MathAssert.Near(Project(projection, new Vector3(6f, 3f, -10f)), new Vector3(1f, 1f, 0f), 1e-6f, 1f, "far corner");
        });
    }

    [Test]
    public void CreateOrthographicOffCenter_ScreenSpace_PutsZeroDepthMidway()
    {
        var projection = Matrix4.CreateOrthographicOffCenter(0f, 800f, 0f, 600f, -1f, 1f);

        MathAssert.Near(Project(projection, Vector3.Zero), new Vector3(-1f, -1f, 0.5f), 1e-6f);
    }

    [Test]
    public void CreateOrthographicOffCenter_EmptyRange_Throws()
    {
        Assert.Multiple(() =>
        {
            Assert.Throws<ArgumentException>(() => Matrix4.CreateOrthographicOffCenter(1f, 1f, 0f, 1f, 0f, 1f), "left == right");
            Assert.Throws<ArgumentException>(() => Matrix4.CreateOrthographicOffCenter(0f, 1f, 2f, 2f, 0f, 1f), "bottom == top");
            Assert.Throws<ArgumentException>(() => Matrix4.CreateOrthographicOffCenter(0f, 1f, 0f, 1f, 3f, 3f), "near == far");
        });
    }

    [Test]
    public void Equality_SameAndDifferent_OperatorsAndEqualsAgree()
    {
        var random = Inputs.CreateRandom();
        var a = random.NextMatrix4();
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
        var a = Matrix4.Identity;
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
        var positive = Matrix4.Identity;
        positive[1, 0] = 0f;
        var negative = Matrix4.Identity;
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
        var matrix = Matrix4.FromRows(
            new Vector4(1.5f, 2f, -3f, 0.25f),
            new Vector4(5f, 6f, 7f, 8f),
            new Vector4(9f, 10f, 11f, 12f),
            new Vector4(13f, 14f, 15f, 16f));

        Assert.That(matrix.ToString(), Is.EqualTo("(1.5, 2, -3, 0.25)\n(5, 6, 7, 8)\n(9, 10, 11, 12)\n(13, 14, 15, 16)"));
    }

    // LookAt's per-element scales for the Projection tolerance (1e-5). The rotation part and Column3.W: max(1, |expected|).
    // Column3's X, Y, Z are -dot(axis, eye), which cancels terms of about 100: there the bound is the sums-of-products
    // tolerance (1e-6) times |axis.X·eye.X| + |axis.Y·eye.Y| + |axis.Z·eye.Z| of that row's axis, with a floor of
    // 1e-5 × max(1, |expected|). Expressed as a scale for 1e-5, the sums-of-products part is 0.1 × that sum.
    private static float[] LookAtScales(Matrix4 expected, Vector3 eye)
    {
        var scales = MathAssert.AtLeastOne(MathAssert.Components(expected));
        for (var row = 0; row < 3; row++)
        {
            var axis = new Vector3(expected[row, 0], expected[row, 1], expected[row, 2]);
            var sumOfProducts = MathAssert.SumOfAbs(axis.X * eye.X, axis.Y * eye.Y, axis.Z * eye.Z);
            var index = 3 * N + row;
            scales[index] = MathF.Max(scales[index], MathAssert.SumsOfProducts / MathAssert.Projection * sumOfProducts);
        }

        return scales;
    }

    // The point (x, y, z, 1) through the projection, divided by w.
    private static Vector3 Project(Matrix4 projection, Vector3 point)
    {
        var clip = projection * new Vector4(point, 1f);
        return clip.AsVector128().AsVector3() / clip.W;
    }
}
