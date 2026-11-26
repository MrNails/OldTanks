using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using OpenTKMath = OpenTK.Mathematics;

namespace CoolEngine.Mathematics.Benchmarks;

/// <summary>
/// <see cref="Quaterniond"/> against OpenTK's <c>Quaterniond</c> (the baseline) on the same 1024 seeded unit quaternions;
/// every method returns a checksum of every component of every result.
/// </summary>
[MemoryDiagnoser]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
[CategoriesColumn]
public class QuaterniondBenchmarks
{
    private const int Count = 1024;

    private Quaterniond[] m_a = [];
    private Quaterniond[] m_b = [];
    private OpenTKMath.Quaterniond[] m_oa = [];
    private OpenTKMath.Quaterniond[] m_ob = [];
    private Quaterniond[] m_raw = [];
    private OpenTKMath.Quaterniond[] m_oraw = [];
    private Vector3d[] m_v = [];
    private OpenTKMath.Vector3d[] m_ov = [];
    private double[] m_t = [];

    [GlobalSetup]
    public void Setup()
    {
        var random = new Random(20261125);
        double Next() => random.NextDouble() * 2.0 - 1.0;

        m_a = new Quaterniond[Count];
        m_b = new Quaterniond[Count];
        m_oa = new OpenTKMath.Quaterniond[Count];
        m_ob = new OpenTKMath.Quaterniond[Count];
        m_raw = new Quaterniond[Count];
        m_oraw = new OpenTKMath.Quaterniond[Count];
        m_v = new Vector3d[Count];
        m_ov = new OpenTKMath.Vector3d[Count];
        m_t = new double[Count];

        for (var i = 0; i < Count; i++)
        {
            m_a[i] = Quaterniond.Normalize(new Quaterniond(Next(), Next(), Next(), Next()));
            m_b[i] = Quaterniond.Normalize(new Quaterniond(Next(), Next(), Next(), Next()));
            m_oa[i] = new OpenTKMath.Quaterniond(m_a[i].X, m_a[i].Y, m_a[i].Z, m_a[i].W);
            m_ob[i] = new OpenTKMath.Quaterniond(m_b[i].X, m_b[i].Y, m_b[i].Z, m_b[i].W);
            m_raw[i] = new Quaterniond(Next(), Next(), Next(), Next()) * 10.0;
            m_oraw[i] = new OpenTKMath.Quaterniond(m_raw[i].X, m_raw[i].Y, m_raw[i].Z, m_raw[i].W);
            m_v[i] = new Vector3d(Next(), Next(), Next()) * 100.0;
            m_ov[i] = new OpenTKMath.Vector3d(m_v[i].X, m_v[i].Y, m_v[i].Z);
            m_t[i] = random.NextDouble();
        }
    }

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
            sum += Sum(m_oa[i] * m_ob[i]);
        return sum;
    }

    [Benchmark, BenchmarkCategory("Transform")]
    public double Ours_Transform()
    {
        var sum = 0.0;
        for (var i = 0; i < Count; i++)
            sum += Sum(Vector3d.Transform(m_v[i], m_a[i]));
        return sum;
    }

    [Benchmark(Baseline = true), BenchmarkCategory("Transform")]
    public double OpenTK_Transform()
    {
        var sum = 0.0;
        for (var i = 0; i < Count; i++)
            sum += Sum(OpenTKMath.Vector3d.Transform(m_ov[i], m_oa[i]));
        return sum;
    }

    [Benchmark, BenchmarkCategory("Slerp")]
    public double Ours_Slerp()
    {
        var sum = 0.0;
        for (var i = 0; i < Count; i++)
            sum += Sum(Quaterniond.Slerp(m_a[i], m_b[i], m_t[i]));
        return sum;
    }

    [Benchmark(Baseline = true), BenchmarkCategory("Slerp")]
    public double OpenTK_Slerp()
    {
        var sum = 0.0;
        for (var i = 0; i < Count; i++)
            sum += Sum(OpenTKMath.Quaterniond.Slerp(m_oa[i], m_ob[i], m_t[i]));
        return sum;
    }

    [Benchmark, BenchmarkCategory("Normalize")]
    public double Ours_Normalize()
    {
        var sum = 0.0;
        for (var i = 0; i < Count; i++)
            sum += Sum(Quaterniond.Normalize(m_raw[i]));
        return sum;
    }

    [Benchmark(Baseline = true), BenchmarkCategory("Normalize")]
    public double OpenTK_Normalize()
    {
        var sum = 0.0;
        for (var i = 0; i < Count; i++)
            sum += Sum(OpenTKMath.Quaterniond.Normalize(m_oraw[i]));
        return sum;
    }

    private static double Sum(Quaterniond q) => q.X + q.Y + q.Z + q.W;

    private static double Sum(OpenTKMath.Quaterniond q) => q.X + q.Y + q.Z + q.W;

    private static double Sum(Vector3d v) => v.X + v.Y + v.Z;

    private static double Sum(OpenTKMath.Vector3d v) => v.X + v.Y + v.Z;
}
