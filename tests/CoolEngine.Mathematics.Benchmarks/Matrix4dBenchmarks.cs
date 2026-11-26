using System.Runtime.CompilerServices;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using OpenTKMath = OpenTK.Mathematics;

namespace CoolEngine.Mathematics.Benchmarks;

/// <summary>
/// <see cref="Matrix4d"/> against OpenTK's <c>Matrix4d</c> (the baseline) on the same 1024 seeded matrices (identical
/// bytes); every method returns a checksum of every component of every result.
/// </summary>
[MemoryDiagnoser]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
[CategoriesColumn]
public class Matrix4dBenchmarks
{
    private const int Count = 1024;

    private Matrix4d[] m_a = [];
    private Matrix4d[] m_b = [];
    private OpenTKMath.Matrix4d[] m_oa = [];
    private OpenTKMath.Matrix4d[] m_ob = [];
    private Vector4d[] m_v = [];
    private OpenTKMath.Vector4d[] m_ov = [];
    private Vector3d[] m_eye = [];
    private OpenTKMath.Vector3d[] m_oeye = [];
    private Vector3d[] m_target = [];
    private OpenTKMath.Vector3d[] m_otarget = [];
    private Vector3d[] m_up = [];
    private OpenTKMath.Vector3d[] m_oup = [];
    private Quaterniond[] m_q = [];
    private OpenTKMath.Quaterniond[] m_oq = [];

    [GlobalSetup]
    public void Setup()
    {
        var random = new Random(20261125);
        double Next() => random.NextDouble() * 2.0 - 1.0;

        m_a = new Matrix4d[Count];
        m_b = new Matrix4d[Count];
        m_oa = new OpenTKMath.Matrix4d[Count];
        m_ob = new OpenTKMath.Matrix4d[Count];
        m_v = new Vector4d[Count];
        m_ov = new OpenTKMath.Vector4d[Count];
        m_eye = new Vector3d[Count];
        m_oeye = new OpenTKMath.Vector3d[Count];
        m_target = new Vector3d[Count];
        m_otarget = new OpenTKMath.Vector3d[Count];
        m_up = new Vector3d[Count];
        m_oup = new OpenTKMath.Vector3d[Count];
        m_q = new Quaterniond[Count];
        m_oq = new OpenTKMath.Quaterniond[Count];

        for (var i = 0; i < Count; i++)
        {
            // A diagonal bias of 4 keeps the matrices well away from singular.
            Matrix4d NextMatrix() => Matrix4d.FromColumns(
                new Vector4d(4.0 + Next(), Next(), Next(), Next()),
                new Vector4d(Next(), 4.0 + Next(), Next(), Next()),
                new Vector4d(Next(), Next(), 4.0 + Next(), Next()),
                new Vector4d(Next(), Next(), Next(), 4.0 + Next()));

            m_a[i] = NextMatrix();
            m_b[i] = NextMatrix();
            m_oa[i] = Unsafe.BitCast<Matrix4d, OpenTKMath.Matrix4d>(m_a[i]);
            m_ob[i] = Unsafe.BitCast<Matrix4d, OpenTKMath.Matrix4d>(m_b[i]);
            m_v[i] = new Vector4d(Next(), Next(), Next(), Next());
            m_ov[i] = new OpenTKMath.Vector4d(m_v[i].X, m_v[i].Y, m_v[i].Z, m_v[i].W);
            m_eye[i] = new Vector3d(Next(), Next(), Next()) * 10.0;
            m_oeye[i] = new OpenTKMath.Vector3d(m_eye[i].X, m_eye[i].Y, m_eye[i].Z);
            m_target[i] = new Vector3d(Next(), Next(), Next()) * 10.0;
            m_otarget[i] = new OpenTKMath.Vector3d(m_target[i].X, m_target[i].Y, m_target[i].Z);
            m_up[i] = Vector3d.UnitY + new Vector3d(Next(), Next(), Next()) * 0.1;
            m_oup[i] = new OpenTKMath.Vector3d(m_up[i].X, m_up[i].Y, m_up[i].Z);
            m_q[i] = Quaterniond.Normalize(new Quaterniond(Next(), Next(), Next(), Next()));
            m_oq[i] = new OpenTKMath.Quaterniond(m_q[i].X, m_q[i].Y, m_q[i].Z, m_q[i].W);
        }
    }

    // Multiply: our column-vector a * b has the bytes of their row-vector b * a.

    [Benchmark, BenchmarkCategory("Multiply")]
    public double Ours_Multiply()
    {
        var sum = 0.0;
        for (var i = 0; i < Count; i++)
            sum += Sum(m_a[i] * m_b[i]);
        return sum;
    }

    [Benchmark(Baseline = true), BenchmarkCategory("Multiply")]
    public double OpenTK_Multiply()
    {
        var sum = 0.0;
        for (var i = 0; i < Count; i++)
            sum += Sum(m_ob[i] * m_oa[i]);
        return sum;
    }

    [Benchmark, BenchmarkCategory("MultiplyVector4d")]
    public double Ours_MultiplyVector4d()
    {
        var sum = 0.0;
        for (var i = 0; i < Count; i++)
            sum += Sum(m_a[i] * m_v[i]);
        return sum;
    }

    [Benchmark(Baseline = true), BenchmarkCategory("MultiplyVector4d")]
    public double OpenTK_MultiplyVector4d()
    {
        var sum = 0.0;
        for (var i = 0; i < Count; i++)
            sum += Sum(OpenTKMath.Vector4d.TransformRow(m_ov[i], m_oa[i]));
        return sum;
    }

    [Benchmark, BenchmarkCategory("Inverted")]
    public double Ours_Inverted()
    {
        var sum = 0.0;
        for (var i = 0; i < Count; i++)
            sum += Sum(m_a[i].Inverted());
        return sum;
    }

    [Benchmark(Baseline = true), BenchmarkCategory("Inverted")]
    public double OpenTK_Inverted()
    {
        var sum = 0.0;
        for (var i = 0; i < Count; i++)
            sum += Sum(m_oa[i].Inverted());
        return sum;
    }

    [Benchmark, BenchmarkCategory("Transposed")]
    public double Ours_Transposed()
    {
        var sum = 0.0;
        for (var i = 0; i < Count; i++)
            sum += Sum(m_a[i].Transposed());
        return sum;
    }

    [Benchmark(Baseline = true), BenchmarkCategory("Transposed")]
    public double OpenTK_Transposed()
    {
        var sum = 0.0;
        for (var i = 0; i < Count; i++)
            sum += Sum(OpenTKMath.Matrix4d.Transpose(m_oa[i]));
        return sum;
    }

    [Benchmark, BenchmarkCategory("CreateFromQuaternion")]
    public double Ours_CreateFromQuaternion()
    {
        var sum = 0.0;
        for (var i = 0; i < Count; i++)
            sum += Sum(Matrix4d.CreateFromQuaternion(m_q[i]));
        return sum;
    }

    [Benchmark(Baseline = true), BenchmarkCategory("CreateFromQuaternion")]
    public double OpenTK_CreateFromQuaternion()
    {
        var sum = 0.0;
        for (var i = 0; i < Count; i++)
            sum += Sum(OpenTKMath.Matrix4d.CreateFromQuaternion(m_oq[i]));
        return sum;
    }

    [Benchmark, BenchmarkCategory("LookAt")]
    public double Ours_LookAt()
    {
        var sum = 0.0;
        for (var i = 0; i < Count; i++)
            sum += Sum(Matrix4d.LookAt(m_eye[i], m_target[i], m_up[i]));
        return sum;
    }

    [Benchmark(Baseline = true), BenchmarkCategory("LookAt")]
    public double OpenTK_LookAt()
    {
        var sum = 0.0;
        for (var i = 0; i < Count; i++)
            sum += Sum(OpenTKMath.Matrix4d.LookAt(m_oeye[i], m_otarget[i], m_oup[i]));
        return sum;
    }

    private static double Sum(Matrix4d m) => Sum(m.Column0) + Sum(m.Column1) + Sum(m.Column2) + Sum(m.Column3);

    private static double Sum(OpenTKMath.Matrix4d m) => Sum(m.Row0) + Sum(m.Row1) + Sum(m.Row2) + Sum(m.Row3);

    private static double Sum(Vector4d v) => v.X + v.Y + v.Z + v.W;

    private static double Sum(OpenTKMath.Vector4d v) => v.X + v.Y + v.Z + v.W;
}
