using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using OpenTKMath = OpenTK.Mathematics;

namespace CoolEngine.Mathematics.Benchmarks;

/// <summary>
/// <see cref="Vector4d"/> against OpenTK's <c>Vector4d</c> (the baseline) on the same 1024 seeded vectors; every method
/// returns a checksum of every component of every result.
/// </summary>
[MemoryDiagnoser]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
[CategoriesColumn]
public class Vector4dBenchmarks
{
    private const int Count = 1024;

    private Vector4d[] m_a = [];
    private Vector4d[] m_b = [];
    private OpenTKMath.Vector4d[] m_oa = [];
    private OpenTKMath.Vector4d[] m_ob = [];

    [GlobalSetup]
    public void Setup()
    {
        var random = new Random(20261125);
        double Next() => random.NextDouble() * 200.0 - 100.0;

        m_a = new Vector4d[Count];
        m_b = new Vector4d[Count];
        m_oa = new OpenTKMath.Vector4d[Count];
        m_ob = new OpenTKMath.Vector4d[Count];
        for (var i = 0; i < Count; i++)
        {
            m_a[i] = new Vector4d(Next(), Next(), Next(), Next());
            m_b[i] = new Vector4d(Next(), Next(), Next(), Next());
            m_oa[i] = new OpenTKMath.Vector4d(m_a[i].X, m_a[i].Y, m_a[i].Z, m_a[i].W);
            m_ob[i] = new OpenTKMath.Vector4d(m_b[i].X, m_b[i].Y, m_b[i].Z, m_b[i].W);
        }
    }

    [Benchmark, BenchmarkCategory("Add")]
    public double Ours_Add()
    {
        var sum = 0.0;
        for (var i = 0; i < Count; i++)
            sum += Sum(m_a[i] + m_b[i]);
        return sum;
    }

    [Benchmark(Baseline = true), BenchmarkCategory("Add")]
    public double OpenTK_Add()
    {
        var sum = 0.0;
        for (var i = 0; i < Count; i++)
            sum += Sum(m_oa[i] + m_ob[i]);
        return sum;
    }

    [Benchmark, BenchmarkCategory("Dot")]
    public double Ours_Dot()
    {
        var sum = 0.0;
        for (var i = 0; i < Count; i++)
            sum += Vector4d.Dot(m_a[i], m_b[i]);
        return sum;
    }

    [Benchmark(Baseline = true), BenchmarkCategory("Dot")]
    public double OpenTK_Dot()
    {
        var sum = 0.0;
        for (var i = 0; i < Count; i++)
            sum += OpenTKMath.Vector4d.Dot(m_oa[i], m_ob[i]);
        return sum;
    }

    [Benchmark, BenchmarkCategory("Normalize")]
    public double Ours_Normalize()
    {
        var sum = 0.0;
        for (var i = 0; i < Count; i++)
            sum += Sum(Vector4d.Normalize(m_a[i]));
        return sum;
    }

    [Benchmark(Baseline = true), BenchmarkCategory("Normalize")]
    public double OpenTK_Normalize()
    {
        var sum = 0.0;
        for (var i = 0; i < Count; i++)
            sum += Sum(OpenTKMath.Vector4d.Normalize(m_oa[i]));
        return sum;
    }

    private static double Sum(Vector4d v) => v.X + v.Y + v.Z + v.W;

    private static double Sum(OpenTKMath.Vector4d v) => v.X + v.Y + v.Z + v.W;
}
