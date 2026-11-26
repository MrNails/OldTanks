using System.Runtime.CompilerServices;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using Numerics = System.Numerics;
using OpenTKMath = OpenTK.Mathematics;

namespace CoolEngine.Mathematics.Benchmarks;

/// <summary>
/// The float <see cref="Matrix4"/> against System.Numerics' <c>Matrix4x4</c> (the baseline) and OpenTK's <c>Matrix4</c>,
/// on the same 1024 seeded matrices (identical bytes in all three). Every method runs its operation over all inputs and
/// returns a checksum of every component of every result, so nothing is optimized away. The <c>NoInline</c> categories
/// call the operation through a <c>[MethodImpl(NoInlining)]</c> wrapper: the call shape of code that does not inline it.
/// </summary>
[MemoryDiagnoser]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
[CategoriesColumn]
public class Matrix4Benchmarks
{
    private const int Count = 1024;

    private Matrix4[] m_a = [];
    private Matrix4[] m_b = [];
    private Numerics.Matrix4x4[] m_na = [];
    private Numerics.Matrix4x4[] m_nb = [];
    private OpenTKMath.Matrix4[] m_oa = [];
    private OpenTKMath.Matrix4[] m_ob = [];
    private Numerics.Vector4[] m_v = [];
    private OpenTKMath.Vector4[] m_ov = [];
    private Numerics.Vector3[] m_p = [];
    private OpenTKMath.Vector3[] m_op = [];
    private Numerics.Vector3[] m_target = [];
    private OpenTKMath.Vector3[] m_otarget = [];
    private Numerics.Vector3[] m_up = [];
    private OpenTKMath.Vector3[] m_oup = [];
    private Numerics.Quaternion[] m_q = [];
    private OpenTKMath.Quaternion[] m_oq = [];
    private float[] m_fov = [];
    private float[] m_aspect = [];

    [GlobalSetup]
    public void Setup()
    {
        var random = new Random(20261125);
        float Next() => (float)(random.NextDouble() * 2.0 - 1.0);

        m_a = new Matrix4[Count];
        m_b = new Matrix4[Count];
        m_na = new Numerics.Matrix4x4[Count];
        m_nb = new Numerics.Matrix4x4[Count];
        m_oa = new OpenTKMath.Matrix4[Count];
        m_ob = new OpenTKMath.Matrix4[Count];
        m_v = new Numerics.Vector4[Count];
        m_ov = new OpenTKMath.Vector4[Count];
        m_p = new Numerics.Vector3[Count];
        m_op = new OpenTKMath.Vector3[Count];
        m_target = new Numerics.Vector3[Count];
        m_otarget = new OpenTKMath.Vector3[Count];
        m_up = new Numerics.Vector3[Count];
        m_oup = new OpenTKMath.Vector3[Count];
        m_q = new Numerics.Quaternion[Count];
        m_oq = new OpenTKMath.Quaternion[Count];
        m_fov = new float[Count];
        m_aspect = new float[Count];

        for (var i = 0; i < Count; i++)
        {
            // A diagonal bias of 4 keeps the matrices well away from singular.
            Matrix4 NextMatrix() => Matrix4.FromColumns(
                new Numerics.Vector4(4f + Next(), Next(), Next(), Next()),
                new Numerics.Vector4(Next(), 4f + Next(), Next(), Next()),
                new Numerics.Vector4(Next(), Next(), 4f + Next(), Next()),
                new Numerics.Vector4(Next(), Next(), Next(), 4f + Next()));

            m_a[i] = NextMatrix();
            m_b[i] = NextMatrix();
            m_na[i] = Unsafe.BitCast<Matrix4, Numerics.Matrix4x4>(m_a[i]);
            m_nb[i] = Unsafe.BitCast<Matrix4, Numerics.Matrix4x4>(m_b[i]);
            m_oa[i] = Unsafe.BitCast<Matrix4, OpenTKMath.Matrix4>(m_a[i]);
            m_ob[i] = Unsafe.BitCast<Matrix4, OpenTKMath.Matrix4>(m_b[i]);
            m_v[i] = new Numerics.Vector4(Next(), Next(), Next(), Next());
            m_ov[i] = new OpenTKMath.Vector4(m_v[i].X, m_v[i].Y, m_v[i].Z, m_v[i].W);
            m_p[i] = new Numerics.Vector3(Next(), Next(), Next()) * 10f;
            m_op[i] = new OpenTKMath.Vector3(m_p[i].X, m_p[i].Y, m_p[i].Z);
            m_target[i] = new Numerics.Vector3(Next(), Next(), Next()) * 10f;
            m_otarget[i] = new OpenTKMath.Vector3(m_target[i].X, m_target[i].Y, m_target[i].Z);
            m_up[i] = Numerics.Vector3.UnitY + new Numerics.Vector3(Next(), Next(), Next()) * 0.1f;
            m_oup[i] = new OpenTKMath.Vector3(m_up[i].X, m_up[i].Y, m_up[i].Z);
            m_q[i] = Numerics.Quaternion.Normalize(new Numerics.Quaternion(Next(), Next(), Next(), Next()));
            m_oq[i] = new OpenTKMath.Quaternion(m_q[i].X, m_q[i].Y, m_q[i].Z, m_q[i].W);
            m_fov[i] = 0.5f + (Next() + 1f);
            m_aspect[i] = 1f + (Next() + 1f);
        }
    }

    // Multiply: our column-vector a * b has the bytes of their row-vector b * a.

    [Benchmark, BenchmarkCategory("Multiply")]
    public float Ours_Multiply()
    {
        var sum = 0f;
        for (var i = 0; i < Count; i++)
            sum += Sum(m_a[i] * m_b[i]);
        return sum;
    }

    [Benchmark(Baseline = true), BenchmarkCategory("Multiply")]
    public float Numerics_Multiply()
    {
        var sum = 0f;
        for (var i = 0; i < Count; i++)
            sum += Sum(m_nb[i] * m_na[i]);
        return sum;
    }

    [Benchmark, BenchmarkCategory("Multiply")]
    public float OpenTK_Multiply()
    {
        var sum = 0f;
        for (var i = 0; i < Count; i++)
            sum += Sum(m_ob[i] * m_oa[i]);
        return sum;
    }

    [Benchmark, BenchmarkCategory("Multiply_NoInline")]
    public float Ours_Multiply_NoInline()
    {
        var sum = 0f;
        for (var i = 0; i < Count; i++)
            sum += Sum(MultiplyNoInline(m_a[i], m_b[i]));
        return sum;
    }

    [Benchmark(Baseline = true), BenchmarkCategory("Multiply_NoInline")]
    public float Numerics_Multiply_NoInline()
    {
        var sum = 0f;
        for (var i = 0; i < Count; i++)
            sum += Sum(MultiplyNoInline(m_nb[i], m_na[i]));
        return sum;
    }

    [Benchmark, BenchmarkCategory("Multiply_NoInline")]
    public float OpenTK_Multiply_NoInline()
    {
        var sum = 0f;
        for (var i = 0; i < Count; i++)
            sum += Sum(MultiplyNoInline(m_ob[i], m_oa[i]));
        return sum;
    }

    [Benchmark, BenchmarkCategory("Inverted")]
    public float Ours_Inverted()
    {
        var sum = 0f;
        for (var i = 0; i < Count; i++)
            sum += Sum(m_a[i].Inverted());
        return sum;
    }

    [Benchmark(Baseline = true), BenchmarkCategory("Inverted")]
    public float Numerics_Inverted()
    {
        var sum = 0f;
        for (var i = 0; i < Count; i++)
        {
            Numerics.Matrix4x4.Invert(m_na[i], out var inverse);
            sum += Sum(inverse);
        }

        return sum;
    }

    [Benchmark, BenchmarkCategory("Inverted")]
    public float OpenTK_Inverted()
    {
        var sum = 0f;
        for (var i = 0; i < Count; i++)
            sum += Sum(OpenTKMath.Matrix4.Invert(m_oa[i]));
        return sum;
    }

    [Benchmark, BenchmarkCategory("Inverted_NoInline")]
    public float Ours_Inverted_NoInline()
    {
        var sum = 0f;
        for (var i = 0; i < Count; i++)
            sum += Sum(InvertNoInline(m_a[i]));
        return sum;
    }

    [Benchmark(Baseline = true), BenchmarkCategory("Inverted_NoInline")]
    public float Numerics_Inverted_NoInline()
    {
        var sum = 0f;
        for (var i = 0; i < Count; i++)
            sum += Sum(InvertNoInline(m_na[i]));
        return sum;
    }

    [Benchmark, BenchmarkCategory("Inverted_NoInline")]
    public float OpenTK_Inverted_NoInline()
    {
        var sum = 0f;
        for (var i = 0; i < Count; i++)
            sum += Sum(InvertNoInline(m_oa[i]));
        return sum;
    }

    [Benchmark, BenchmarkCategory("Transposed")]
    public float Ours_Transposed()
    {
        var sum = 0f;
        for (var i = 0; i < Count; i++)
            sum += Sum(m_a[i].Transposed());
        return sum;
    }

    [Benchmark(Baseline = true), BenchmarkCategory("Transposed")]
    public float Numerics_Transposed()
    {
        var sum = 0f;
        for (var i = 0; i < Count; i++)
            sum += Sum(Numerics.Matrix4x4.Transpose(m_na[i]));
        return sum;
    }

    [Benchmark, BenchmarkCategory("Transposed")]
    public float OpenTK_Transposed()
    {
        var sum = 0f;
        for (var i = 0; i < Count; i++)
            sum += Sum(OpenTKMath.Matrix4.Transpose(m_oa[i]));
        return sum;
    }

    // M * v: their row vector times the same bytes.

    [Benchmark, BenchmarkCategory("MultiplyVector4")]
    public float Ours_MultiplyVector4()
    {
        var sum = 0f;
        for (var i = 0; i < Count; i++)
            sum += Sum(m_a[i] * m_v[i]);
        return sum;
    }

    [Benchmark(Baseline = true), BenchmarkCategory("MultiplyVector4")]
    public float Numerics_MultiplyVector4()
    {
        var sum = 0f;
        for (var i = 0; i < Count; i++)
            sum += Sum(Numerics.Vector4.Transform(m_v[i], m_na[i]));
        return sum;
    }

    [Benchmark, BenchmarkCategory("MultiplyVector4")]
    public float OpenTK_MultiplyVector4()
    {
        var sum = 0f;
        for (var i = 0; i < Count; i++)
            sum += Sum(OpenTKMath.Vector4.TransformRow(m_ov[i], m_oa[i]));
        return sum;
    }

    [Benchmark, BenchmarkCategory("TransformPosition")]
    public float Ours_TransformPosition()
    {
        var sum = 0f;
        for (var i = 0; i < Count; i++)
            sum += Sum(m_a[i].TransformPosition(m_p[i]));
        return sum;
    }

    [Benchmark(Baseline = true), BenchmarkCategory("TransformPosition")]
    public float Numerics_TransformPosition()
    {
        var sum = 0f;
        for (var i = 0; i < Count; i++)
            sum += Sum(Numerics.Vector3.Transform(m_p[i], m_na[i]));
        return sum;
    }

    [Benchmark, BenchmarkCategory("TransformPosition")]
    public float OpenTK_TransformPosition()
    {
        var sum = 0f;
        for (var i = 0; i < Count; i++)
            sum += Sum(OpenTKMath.Vector3.TransformPosition(m_op[i], m_oa[i]));
        return sum;
    }

    [Benchmark, BenchmarkCategory("CreateFromQuaternion")]
    public float Ours_CreateFromQuaternion()
    {
        var sum = 0f;
        for (var i = 0; i < Count; i++)
            sum += Sum(Matrix4.CreateFromQuaternion(m_q[i]));
        return sum;
    }

    [Benchmark(Baseline = true), BenchmarkCategory("CreateFromQuaternion")]
    public float Numerics_CreateFromQuaternion()
    {
        var sum = 0f;
        for (var i = 0; i < Count; i++)
            sum += Sum(Numerics.Matrix4x4.CreateFromQuaternion(m_q[i]));
        return sum;
    }

    [Benchmark, BenchmarkCategory("CreateFromQuaternion")]
    public float OpenTK_CreateFromQuaternion()
    {
        var sum = 0f;
        for (var i = 0; i < Count; i++)
            sum += Sum(OpenTKMath.Matrix4.CreateFromQuaternion(m_oq[i]));
        return sum;
    }

    [Benchmark, BenchmarkCategory("LookAt")]
    public float Ours_LookAt()
    {
        var sum = 0f;
        for (var i = 0; i < Count; i++)
            sum += Sum(Matrix4.LookAt(m_p[i], m_target[i], m_up[i]));
        return sum;
    }

    [Benchmark(Baseline = true), BenchmarkCategory("LookAt")]
    public float Numerics_LookAt()
    {
        var sum = 0f;
        for (var i = 0; i < Count; i++)
            sum += Sum(Numerics.Matrix4x4.CreateLookAt(m_p[i], m_target[i], m_up[i]));
        return sum;
    }

    [Benchmark, BenchmarkCategory("LookAt")]
    public float OpenTK_LookAt()
    {
        var sum = 0f;
        for (var i = 0; i < Count; i++)
            sum += Sum(OpenTKMath.Matrix4.LookAt(m_op[i], m_otarget[i], m_oup[i]));
        return sum;
    }

    // Ours is reversed Z with depth [0, 1]; theirs are the conventional forms: the same amount of work.

    [Benchmark, BenchmarkCategory("CreatePerspectiveFieldOfView")]
    public float Ours_CreatePerspectiveFieldOfView()
    {
        var sum = 0f;
        for (var i = 0; i < Count; i++)
            sum += Sum(Matrix4.CreatePerspectiveFieldOfView(m_fov[i], m_aspect[i], 0.1f, 1000f));
        return sum;
    }

    [Benchmark(Baseline = true), BenchmarkCategory("CreatePerspectiveFieldOfView")]
    public float Numerics_CreatePerspectiveFieldOfView()
    {
        var sum = 0f;
        for (var i = 0; i < Count; i++)
            sum += Sum(Numerics.Matrix4x4.CreatePerspectiveFieldOfView(m_fov[i], m_aspect[i], 0.1f, 1000f));
        return sum;
    }

    [Benchmark, BenchmarkCategory("CreatePerspectiveFieldOfView")]
    public float OpenTK_CreatePerspectiveFieldOfView()
    {
        var sum = 0f;
        for (var i = 0; i < Count; i++)
            sum += Sum(OpenTKMath.Matrix4.CreatePerspectiveFieldOfView(m_fov[i], m_aspect[i], 0.1f, 1000f));
        return sum;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static Matrix4 MultiplyNoInline(Matrix4 left, Matrix4 right) => left * right;

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static Numerics.Matrix4x4 MultiplyNoInline(Numerics.Matrix4x4 left, Numerics.Matrix4x4 right) => left * right;

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static OpenTKMath.Matrix4 MultiplyNoInline(OpenTKMath.Matrix4 left, OpenTKMath.Matrix4 right) => left * right;

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static Matrix4 InvertNoInline(Matrix4 matrix) => matrix.Inverted();

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static Numerics.Matrix4x4 InvertNoInline(Numerics.Matrix4x4 matrix)
    {
        Numerics.Matrix4x4.Invert(matrix, out var inverse);
        return inverse;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static OpenTKMath.Matrix4 InvertNoInline(OpenTKMath.Matrix4 matrix) => OpenTKMath.Matrix4.Invert(matrix);

    // Every component of a result, so the whole result is consumed. The same 16 (or 3, 4) additions for every library.

    private static float Sum(Matrix4 m) => Sum(m.Column0) + Sum(m.Column1) + Sum(m.Column2) + Sum(m.Column3);

    private static float Sum(Numerics.Matrix4x4 m) =>
        m.M11 + m.M12 + m.M13 + m.M14 + m.M21 + m.M22 + m.M23 + m.M24 + m.M31 + m.M32 + m.M33 + m.M34 + m.M41 + m.M42 + m.M43 + m.M44;

    private static float Sum(OpenTKMath.Matrix4 m) => Sum(m.Row0) + Sum(m.Row1) + Sum(m.Row2) + Sum(m.Row3);

    private static float Sum(Numerics.Vector4 v) => v.X + v.Y + v.Z + v.W;

    private static float Sum(OpenTKMath.Vector4 v) => v.X + v.Y + v.Z + v.W;

    private static float Sum(Numerics.Vector3 v) => v.X + v.Y + v.Z;

    private static float Sum(OpenTKMath.Vector3 v) => v.X + v.Y + v.Z;
}
