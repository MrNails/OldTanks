using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using OpenTKMath = OpenTK.Mathematics;

namespace CoolEngine.Mathematics.Benchmarks;

/// <summary>
/// <see cref="Vector3d"/> against OpenTK's <c>Vector3d</c> (the baseline) on the same 1024 seeded vectors. Every method
/// returns a checksum of every component of every result. <c>DependentChain</c> integrates <c>vel += acc·dt;
/// pos += vel·dt</c> over 1024 steps, each step depending on the previous one: the latency a physics step sees, which a
/// throughput loop hides.
/// </summary>
[MemoryDiagnoser]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
[CategoriesColumn]
public class Vector3dBenchmarks
{
    private const int Count = 1024;
    private const double TimeStep = 1.0 / 60.0;

    private Vector3d[] m_a = [];
    private Vector3d[] m_b = [];
    private OpenTKMath.Vector3d[] m_oa = [];
    private OpenTKMath.Vector3d[] m_ob = [];

    [GlobalSetup]
    public void Setup()
    {
        var random = new Random(20261125);
        double Next() => random.NextDouble() * 200.0 - 100.0;

        m_a = new Vector3d[Count];
        m_b = new Vector3d[Count];
        m_oa = new OpenTKMath.Vector3d[Count];
        m_ob = new OpenTKMath.Vector3d[Count];
        for (var i = 0; i < Count; i++)
        {
            m_a[i] = new Vector3d(Next(), Next(), Next());
            m_b[i] = new Vector3d(Next(), Next(), Next());
            m_oa[i] = new OpenTKMath.Vector3d(m_a[i].X, m_a[i].Y, m_a[i].Z);
            m_ob[i] = new OpenTKMath.Vector3d(m_b[i].X, m_b[i].Y, m_b[i].Z);
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
            sum += Vector3d.Dot(m_a[i], m_b[i]);
        return sum;
    }

    [Benchmark(Baseline = true), BenchmarkCategory("Dot")]
    public double OpenTK_Dot()
    {
        var sum = 0.0;
        for (var i = 0; i < Count; i++)
            sum += OpenTKMath.Vector3d.Dot(m_oa[i], m_ob[i]);
        return sum;
    }

    [Benchmark, BenchmarkCategory("Cross")]
    public double Ours_Cross()
    {
        var sum = 0.0;
        for (var i = 0; i < Count; i++)
            sum += Sum(Vector3d.Cross(m_a[i], m_b[i]));
        return sum;
    }

    [Benchmark(Baseline = true), BenchmarkCategory("Cross")]
    public double OpenTK_Cross()
    {
        var sum = 0.0;
        for (var i = 0; i < Count; i++)
            sum += Sum(OpenTKMath.Vector3d.Cross(m_oa[i], m_ob[i]));
        return sum;
    }

    [Benchmark, BenchmarkCategory("Length")]
    public double Ours_Length()
    {
        var sum = 0.0;
        for (var i = 0; i < Count; i++)
            sum += m_a[i].Length();
        return sum;
    }

    [Benchmark(Baseline = true), BenchmarkCategory("Length")]
    public double OpenTK_Length()
    {
        var sum = 0.0;
        for (var i = 0; i < Count; i++)
            sum += m_oa[i].Length;
        return sum;
    }

    [Benchmark, BenchmarkCategory("Normalize")]
    public double Ours_Normalize()
    {
        var sum = 0.0;
        for (var i = 0; i < Count; i++)
            sum += Sum(Vector3d.Normalize(m_a[i]));
        return sum;
    }

    [Benchmark(Baseline = true), BenchmarkCategory("Normalize")]
    public double OpenTK_Normalize()
    {
        var sum = 0.0;
        for (var i = 0; i < Count; i++)
            sum += Sum(OpenTKMath.Vector3d.Normalize(m_oa[i]));
        return sum;
    }

    [Benchmark, BenchmarkCategory("DependentChain")]
    public double Ours_DependentChain()
    {
        var position = m_a[0];
        var velocity = m_b[0];
        for (var i = 0; i < Count; i++)
        {
            velocity += m_b[i] * TimeStep;
            position += velocity * TimeStep;
        }

        return Sum(position) + Sum(velocity);
    }

    [Benchmark(Baseline = true), BenchmarkCategory("DependentChain")]
    public double OpenTK_DependentChain()
    {
        var position = m_oa[0];
        var velocity = m_ob[0];
        for (var i = 0; i < Count; i++)
        {
            velocity += m_ob[i] * TimeStep;
            position += velocity * TimeStep;
        }

        return Sum(position) + Sum(velocity);
    }

    private static double Sum(Vector3d v) => v.X + v.Y + v.Z;

    private static double Sum(OpenTKMath.Vector3d v) => v.X + v.Y + v.Z;
}
