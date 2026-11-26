using System.Numerics;
using System.Runtime.InteropServices;
using CoolEngine.Mathematics.Tests.TestSupport;

namespace CoolEngine.Mathematics.Tests;

/// <summary>
/// One result for every instruction set (Denys, D6: "Same bits on x64", scoped by review B: "Exclude trig"). A fixed loop
/// runs every trig-free computing member on seeded trig-free inputs and hashes the bits of every result. The expected hash
/// is a constant recorded on the default path; the acceptance runs this test under the default, <c>DOTNET_EnableAVX2=0</c>
/// (no FMA), <c>DOTNET_EnableAVX=0</c> and <c>DOTNET_EnableHWIntrinsic=0</c> (software), so a pass on all four proves the
/// same bits on every path.
/// <para>
/// Scope: only members that use + − × ÷ and sqrt, on finite inputs, which IEEE 754 rounds correctly and so the library
/// promises to give the same bits on every x64 CPU. Members that call sin, cos, tan or acos (the rotation, axis-angle and
/// perspective factories, <c>Quaterniond.CreateFromAxisAngle</c>, <c>Quaterniond.Slerp</c>) follow the OS math library,
/// which differs between CPUs with and without FMA3, so they stay out of the loops; their own tests compare them with
/// tolerances. Rotations come from four random components normalized with sqrt, not from Shoemake's trig formula.
/// </para>
/// <para>
/// Re-record rule: re-record a golden only after a .NET update (the members delegated to System.Numerics can move), and
/// only once the four paths agree on the new hash. A different hash on another CPU or OS is a defect to investigate, never
/// a reason to re-record.
/// </para>
/// </summary>
[TestFixture]
public sealed class CrossPathTests
{
    private const int Steps = 20_000;

    // Recorded 2026-11-26 on the default path (x64, i7-14700K, AVX2 and FMA available, .NET 10, Windows 11), after the
    // same hash was seen on all four paths: the test ran once with the previous value and the actual hash from its failure
    // message was copied here. Re-recorded in OT-5 review B (B1) when the loop dropped the trig members and trig inputs.
    private const ulong Golden = 0xEDEDE1FB97659790;

    [Test]
    public void HashedLoop_SameBitsOnEveryPath_MatchesGolden()
    {
        // Inputs come only from Random, casts and the trig-free rotation below (no System.Numerics or library arithmetic),
        // so they are the same on every path.
        var random = new Random(Inputs.Seed);
        var hash = new Fnv1a64();

        for (var step = 0; step < Steps; step++)
        {
            HashMatrix2(random, ref hash);
            HashMatrix3(random, ref hash);
            HashMatrix4(random, ref hash);
        }

        Assert.That(hash.Value, Is.EqualTo(Golden), $"hash 0x{hash.Value:X16}");
    }

    // Recorded 2026-11-26 on the default path (same machine) with OT-5 stage 4, after the same hash was seen on all four
    // paths: the test ran once with a placeholder and the actual hash from its failure message was copied here. Re-recorded
    // in OT-5 review B (B1) when the loop dropped the trig members and trig inputs.
    private const ulong DoubleGolden = 0xFAB69D982403FC0E;

    [Test]
    public void DoubleHashedLoop_SameBitsOnEveryPath_MatchesGolden()
    {
        var random = new Random(Inputs.Seed);
        var hash = new Fnv1a64();

        for (var step = 0; step < Steps; step++)
        {
            HashVectors(random, ref hash);
            HashQuaterniond(random, ref hash);
            HashMatrix2d(random, ref hash);
            HashMatrix3d(random, ref hash);
            HashMatrix4d(random, ref hash);
        }

        Assert.That(hash.Value, Is.EqualTo(DoubleGolden), $"hash 0x{hash.Value:X16}");
    }

    private static void HashVectors(Random random, ref Fnv1a64 hash)
    {
        var a2 = random.NextVector2d();
        var b2 = random.NextVector2d();
        var a3 = random.NextVector3d();
        var b3 = random.NextVector3d();
        var a4 = random.NextVector4d();
        var b4 = random.NextVector4d();
        var s = random.NextComponentD();
        var t = random.NextDouble();

        hash.Add(a2 + b2); hash.Add(a2 - b2); hash.Add(-a2); hash.Add(a2 * b2); hash.Add(a2 / b2); hash.Add(a2 * s); hash.Add(s * a2);
        hash.Add(a2 / s); hash.Add(a2.Length()); hash.Add(a2.LengthSquared()); hash.Add(Vector2d.Dot(a2, b2));
        hash.Add(Vector2d.Normalize(a2)); hash.Add(Vector2d.Distance(a2, b2)); hash.Add(Vector2d.DistanceSquared(a2, b2));
        hash.Add(Vector2d.Min(a2, b2)); hash.Add(Vector2d.Max(a2, b2)); hash.Add(Vector2d.Abs(a2)); hash.Add(Vector2d.Clamp(a2, b2, a2 + b2));
        hash.Add(Vector2d.Lerp(a2, b2, t));

        hash.Add(a3 + b3); hash.Add(a3 - b3); hash.Add(-a3); hash.Add(a3 * b3); hash.Add(a3 / b3); hash.Add(a3 * s); hash.Add(s * a3);
        hash.Add(a3 / s); hash.Add(a3.Length()); hash.Add(a3.LengthSquared()); hash.Add(Vector3d.Dot(a3, b3)); hash.Add(Vector3d.Cross(a3, b3));
        hash.Add(Vector3d.Normalize(a3)); hash.Add(Vector3d.Distance(a3, b3)); hash.Add(Vector3d.DistanceSquared(a3, b3));
        hash.Add(Vector3d.Min(a3, b3)); hash.Add(Vector3d.Max(a3, b3)); hash.Add(Vector3d.Abs(a3)); hash.Add(Vector3d.Clamp(a3, b3, a3 + b3));
        hash.Add(Vector3d.Lerp(a3, b3, t)); hash.Add(Vector3d.Transform(a3, NextTrigFreeRotationD(random)));

        hash.Add(a4 + b4); hash.Add(a4 - b4); hash.Add(-a4); hash.Add(a4 * b4); hash.Add(a4 / b4); hash.Add(a4 * s); hash.Add(s * a4);
        hash.Add(a4 / s); hash.Add(a4.Length()); hash.Add(a4.LengthSquared()); hash.Add(Vector4d.Dot(a4, b4));
        hash.Add(Vector4d.Normalize(a4)); hash.Add(Vector4d.Distance(a4, b4)); hash.Add(Vector4d.DistanceSquared(a4, b4));
        hash.Add(Vector4d.Min(a4, b4)); hash.Add(Vector4d.Max(a4, b4)); hash.Add(Vector4d.Abs(a4)); hash.Add(Vector4d.Clamp(a4, b4, a4 + b4));
        hash.Add(Vector4d.Lerp(a4, b4, t));
    }

    private static void HashQuaterniond(Random random, ref Fnv1a64 hash)
    {
        var a = random.NextQuaterniond();
        var b = random.NextQuaterniond();
        var s = random.NextComponentD();

        // CreateFromAxisAngle and Slerp call sin, cos or acos: out of scope.
        hash.Add(a + b); hash.Add(a - b); hash.Add(-a); hash.Add(a * b); hash.Add(a * s); hash.Add(a.Length()); hash.Add(a.LengthSquared());
        hash.Add(Quaterniond.Conjugate(a)); hash.Add(Quaterniond.Inverse(a)); hash.Add(Quaterniond.Normalize(a)); hash.Add(Quaterniond.Dot(a, b));
    }

    private static void HashMatrix2d(Random random, ref Fnv1a64 hash)
    {
        var a = random.NextMatrix2d();
        var b = random.NextMatrix2d();
        var v = random.NextVector2d();

        hash.Add(Matrix2d.FromRows(v, random.NextVector2d()));
        hash.Add(a * b);
        hash.Add(a * v);
        hash.Add(a.Determinant);
        hash.Add(a.Transposed());
        hash.Add(a.Inverted());
        hash.Add(Matrix2d.TryInvert(b, out var inverse));
        hash.Add(inverse);
        hash.Add(Matrix2d.CreateScale(random.NextComponentD()));
        hash.Add(Matrix2d.CreateScale(v));
        hash.Add(Matrix2d.CreateScale(v.X, v.Y));
        hash.Add((Matrix2)a);
    }

    private static void HashMatrix3d(Random random, ref Fnv1a64 hash)
    {
        var a = random.NextMatrix3d();
        var b = random.NextMatrix3d();
        var v = random.NextVector3d();

        hash.Add(Matrix3d.FromRows(v, random.NextVector3d(), random.NextVector3d()));
        hash.Add(new Matrix3d(random.NextMatrix4d()));
        hash.Add(a * b);
        hash.Add(a * v);
        hash.Add(a.Determinant);
        hash.Add(a.Transposed());
        hash.Add(a.Inverted());
        hash.Add(Matrix3d.TryInvert(b, out var inverse));
        hash.Add(inverse);
        hash.Add(Matrix3d.CreateScale(random.NextComponentD()));
        hash.Add(Matrix3d.CreateScale(v));
        hash.Add(Matrix3d.CreateScale(v.X, v.Y, v.Z));
        hash.Add(Matrix3d.CreateFromQuaternion(NextTrigFreeRotationD(random)));
        hash.Add((Matrix3)a);
    }

    private static void HashMatrix4d(Random random, ref Fnv1a64 hash)
    {
        var a = random.NextMatrix4d();
        var b = random.NextMatrix4d();
        var v = random.NextVector4d();
        var p = random.NextVector3d();

        hash.Add(Matrix4d.FromRows(v, random.NextVector4d(), random.NextVector4d(), random.NextVector4d()));
        hash.Add(new Matrix4d(random.NextMatrix3d()));
        hash.Add(a.Translation);
        hash.Add(a * b);
        hash.Add(a * v);
        hash.Add(a.TransformPosition(p));
        hash.Add(a.TransformDirection(p));
        hash.Add(a.Determinant);
        hash.Add(a.Transposed());
        hash.Add(a.Inverted());
        hash.Add(Matrix4d.TryInvert(b, out var inverse));
        hash.Add(inverse);
        hash.Add(a.ClearRotation());
        hash.Add(Matrix4d.CreateScale(random.NextComponentD()));
        hash.Add(Matrix4d.CreateScale(p));
        hash.Add(Matrix4d.CreateScale(p.X, p.Y, p.Z));
        hash.Add(Matrix4d.CreateTranslation(p));
        hash.Add(Matrix4d.CreateTranslation(p.X, p.Y, p.Z));
        hash.Add(Matrix4d.CreateFromQuaternion(NextTrigFreeRotationD(random)));
        hash.Add(Matrix4d.LookAt(p, random.NextVector3d(), random.NextVector3d()));
        hash.Add((Matrix4)a);

        // The perspective factories call tan: out of scope. Valid orthographic arguments: near in (0.01, 1), far beyond it.
        var depthNear = 0.01 + random.NextDouble();
        var depthFar = depthNear + 1.0 + 999.0 * random.NextDouble();
        var left = random.NextComponentD();
        var bottom = random.NextComponentD();
        hash.Add(Matrix4d.CreateOrthographicOffCenter(left, left + 1.0 + random.NextDouble() * 100.0, bottom,
            bottom + 1.0 + random.NextDouble() * 100.0, -depthNear, depthFar));
    }

    private static void HashMatrix2(Random random, ref Fnv1a64 hash)
    {
        var a = random.NextMatrix2();
        var b = random.NextMatrix2();
        var v = random.NextVector2();

        hash.Add(Matrix2.FromRows(v, random.NextVector2()));
        hash.Add(a * b);
        hash.Add(a * v);
        hash.Add(a.Determinant);
        hash.Add(a.Transposed());
        hash.Add(a.Inverted());
        hash.Add(Matrix2.TryInvert(b, out var inverse));
        hash.Add(inverse);
        hash.Add(Matrix2.CreateScale(random.NextComponent()));
        hash.Add(Matrix2.CreateScale(v));
        hash.Add(Matrix2.CreateScale(v.X, v.Y));
    }

    private static void HashMatrix3(Random random, ref Fnv1a64 hash)
    {
        var a = random.NextMatrix3();
        var b = random.NextMatrix3();
        var v = random.NextVector3();

        hash.Add(Matrix3.FromRows(v, random.NextVector3(), random.NextVector3()));
        hash.Add(new Matrix3(random.NextMatrix4()));
        hash.Add(a * b);
        hash.Add(a * v);
        hash.Add(a.Determinant);
        hash.Add(a.Transposed());
        hash.Add(a.Inverted());
        hash.Add(Matrix3.TryInvert(b, out var inverse));
        hash.Add(inverse);
        hash.Add(Matrix3.CreateScale(random.NextComponent()));
        hash.Add(Matrix3.CreateScale(v));
        hash.Add(Matrix3.CreateScale(v.X, v.Y, v.Z));
        hash.Add(Matrix3.CreateFromQuaternion(NextTrigFreeRotation(random)));
    }

    private static void HashMatrix4(Random random, ref Fnv1a64 hash)
    {
        var a = random.NextMatrix4();
        var b = random.NextMatrix4();
        var v = random.NextVector4();
        var p = random.NextVector3();

        hash.Add(Matrix4.FromRows(v, random.NextVector4(), random.NextVector4(), random.NextVector4()));
        hash.Add(new Matrix4(random.NextMatrix3()));
        hash.Add(a.Translation);
        hash.Add(a * b);
        hash.Add(a * v);
        hash.Add(a.TransformPosition(p));
        hash.Add(a.TransformDirection(p));
        hash.Add(a.Determinant);
        hash.Add(a.Transposed());
        hash.Add(a.Inverted());
        hash.Add(Matrix4.TryInvert(b, out var inverse));
        hash.Add(inverse);
        hash.Add(a.ClearRotation());
        hash.Add(Matrix4.CreateScale(random.NextComponent()));
        hash.Add(Matrix4.CreateScale(p));
        hash.Add(Matrix4.CreateScale(p.X, p.Y, p.Z));
        hash.Add(Matrix4.CreateTranslation(p));
        hash.Add(Matrix4.CreateTranslation(p.X, p.Y, p.Z));
        hash.Add(Matrix4.CreateFromQuaternion(NextTrigFreeRotation(random)));
        hash.Add(Matrix4.LookAt(p, random.NextVector3(), random.NextVector3()));

        // The perspective factories call tan: out of scope. Valid orthographic arguments: near in (0.01, 1), far beyond it.
        var depthNear = 0.01f + random.NextSingle();
        var depthFar = depthNear + 1f + 999f * random.NextSingle();
        var left = random.NextComponent();
        var bottom = random.NextComponent();
        hash.Add(Matrix4.CreateOrthographicOffCenter(left, left + 1f + random.NextSingle() * 100f, bottom,
            bottom + 1f + random.NextSingle() * 100f, -depthNear, depthFar));
    }

    /// <summary>
    /// A unit quaternion without trig: four components uniform in [-1, 1), divided by their length (<c>Math.Sqrt</c> of
    /// the sum of squares, left to right, in double). Every step is + − × ÷ or sqrt, correctly rounded, so the input is the
    /// same on every path. Not uniform over rotations, which a bit hash does not need.
    /// </summary>
    private static Quaterniond NextTrigFreeRotationD(Random random)
    {
        var x = 2.0 * random.NextDouble() - 1.0;
        var y = 2.0 * random.NextDouble() - 1.0;
        var z = 2.0 * random.NextDouble() - 1.0;
        var w = 2.0 * random.NextDouble() - 1.0;
        var length = Math.Sqrt(x * x + y * y + z * z + w * w);
        return new Quaterniond(x / length, y / length, z / length, w / length);
    }

    /// <summary><see cref="NextTrigFreeRotationD"/> rounded to float.</summary>
    private static Quaternion NextTrigFreeRotation(Random random)
    {
        var q = NextTrigFreeRotationD(random);
        return new Quaternion((float)q.X, (float)q.Y, (float)q.Z, (float)q.W);
    }

    /// <summary>FNV-1a, 64 bit, over the bytes of every value added.</summary>
    private struct Fnv1a64()
    {
        private const ulong OffsetBasis = 0xCBF29CE484222325;
        private const ulong Prime = 0x00000100000001B3;

        public ulong Value { get; private set; } = OffsetBasis;

        public void Add<T>(T value) where T : unmanaged
        {
            foreach (var b in MemoryMarshal.AsBytes(new ReadOnlySpan<T>(in value)))
                Value = (Value ^ b) * Prime;
        }
    }
}
