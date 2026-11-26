using System.Numerics;
using System.Runtime.InteropServices;
using CoolEngine.Mathematics.Tests.TestSupport;

namespace CoolEngine.Mathematics.Tests;

/// <summary>
/// One result for every instruction set on one machine (Denys, D6: "Same bits on x64"). A fixed loop runs every computing
/// member of the float matrices on seeded inputs and hashes the bits of every result. The expected hash is a constant
/// recorded on the default path; the acceptance runs this test under the default, <c>DOTNET_EnableAVX2=0</c> (no FMA),
/// <c>DOTNET_EnableAVX=0</c> and <c>DOTNET_EnableHWIntrinsic=0</c> (software), so a pass on all four proves the same bits
/// on every path. Trigonometry comes from .NET (<c>MathF.Sin</c> …), so the constant holds per machine and OS, not
/// across them.
/// </summary>
[TestFixture]
public sealed class CrossPathTests
{
    private const int Steps = 20_000;

    // Recorded 2026-11-26 on the default path (x64, i7-14700K, AVX2 and FMA available, .NET 10, Windows 11): the test ran
    // once with the previous value and the actual hash from its failure message was copied here, after the same hash was
    // seen on all four paths. Re-recorded in OT-5 step 3d for deliberate result changes (ClearRotation keeps the axis
    // lengths, the perspective constant n·(F/(F-n)), the inverse's determinant as row 0 · its cofactors). Re-record it
    // only for a deliberate change of a result, never to make a path agree.
    private const ulong Golden = 0xB1B5EACF1BFBD1DC;

    [Test]
    public void HashedLoop_SameBitsOnEveryPath_MatchesGolden()
    {
        // Inputs come only from Random and casts (no System.Numerics or library arithmetic), so they are the same on every path.
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

    // Recorded 2026-11-26 on the default path (same machine) with OT-5 stage 4: the test ran once with a placeholder and the
    // actual hash from its failure message was copied here, after the same hash was seen on all four paths. Re-recorded the
    // same way for the reciprocal Normalize of the double vectors and Quaterniond (one 1 / Length, then multiplies). Re-record
    // it only for a deliberate change of a result, never to make a path agree.
    private const ulong DoubleGolden = 0xADE032B4A613B567;

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
        hash.Add(Vector3d.Lerp(a3, b3, t)); hash.Add(Vector3d.Transform(a3, random.NextRotationD()));

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
        var ra = random.NextRotationD();
        var rb = random.NextRotationD();
        var s = random.NextComponentD();

        hash.Add(a + b); hash.Add(a - b); hash.Add(-a); hash.Add(a * b); hash.Add(a * s); hash.Add(a.Length()); hash.Add(a.LengthSquared());
        hash.Add(Quaterniond.Conjugate(a)); hash.Add(Quaterniond.Inverse(a)); hash.Add(Quaterniond.Normalize(a)); hash.Add(Quaterniond.Dot(a, b));
        hash.Add(Quaterniond.CreateFromAxisAngle(Vector3d.Normalize(random.NextVector3d()), random.NextAngleD()));
        hash.Add(Quaterniond.Slerp(ra, rb, random.NextDouble()));
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
        hash.Add(Matrix2d.CreateRotation(random.NextAngleD()));
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
        hash.Add(Matrix3d.CreateRotationX(random.NextAngleD()));
        hash.Add(Matrix3d.CreateRotationY(random.NextAngleD()));
        hash.Add(Matrix3d.CreateRotationZ(random.NextAngleD()));
        hash.Add(Matrix3d.CreateFromAxisAngle(random.NextVector3d(), random.NextAngleD()));
        hash.Add(Matrix3d.CreateFromQuaternion(random.NextRotationD()));
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
        hash.Add(Matrix4d.CreateRotationX(random.NextAngleD()));
        hash.Add(Matrix4d.CreateRotationY(random.NextAngleD()));
        hash.Add(Matrix4d.CreateRotationZ(random.NextAngleD()));
        hash.Add(Matrix4d.CreateFromAxisAngle(random.NextVector3d(), random.NextAngleD()));
        hash.Add(Matrix4d.CreateFromQuaternion(random.NextRotationD()));
        hash.Add(Matrix4d.LookAt(p, random.NextVector3d(), random.NextVector3d()));
        hash.Add((Matrix4)a);

        var fovY = 0.1 + 2.9 * random.NextDouble();
        var aspectRatio = 0.5 + 2.5 * random.NextDouble();
        var depthNear = 0.01 + random.NextDouble();
        var depthFar = depthNear + 1.0 + 999.0 * random.NextDouble();
        hash.Add(Matrix4d.CreatePerspectiveFieldOfView(fovY, aspectRatio, depthNear, depthFar));
        hash.Add(Matrix4d.CreatePerspectiveFieldOfViewInfinite(fovY, aspectRatio, depthNear));

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
        hash.Add(Matrix2.CreateRotation(random.NextAngle()));
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
        hash.Add(Matrix3.CreateRotationX(random.NextAngle()));
        hash.Add(Matrix3.CreateRotationY(random.NextAngle()));
        hash.Add(Matrix3.CreateRotationZ(random.NextAngle()));
        hash.Add(Matrix3.CreateFromAxisAngle(random.NextVector3(), random.NextAngle()));
        hash.Add(Matrix3.CreateFromQuaternion(random.NextRotation()));
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
        hash.Add(Matrix4.CreateRotationX(random.NextAngle()));
        hash.Add(Matrix4.CreateRotationY(random.NextAngle()));
        hash.Add(Matrix4.CreateRotationZ(random.NextAngle()));
        hash.Add(Matrix4.CreateFromAxisAngle(random.NextVector3(), random.NextAngle()));
        hash.Add(Matrix4.CreateFromQuaternion(random.NextRotation()));
        hash.Add(Matrix4.LookAt(p, random.NextVector3(), random.NextVector3()));

        // Valid projection arguments: fov in (0.1, 3), aspect in (0.5, 3), near in (0.01, 1), far beyond near.
        var fovY = 0.1f + 2.9f * random.NextSingle();
        var aspectRatio = 0.5f + 2.5f * random.NextSingle();
        var depthNear = 0.01f + random.NextSingle();
        var depthFar = depthNear + 1f + 999f * random.NextSingle();
        hash.Add(Matrix4.CreatePerspectiveFieldOfView(fovY, aspectRatio, depthNear, depthFar));
        hash.Add(Matrix4.CreatePerspectiveFieldOfViewInfinite(fovY, aspectRatio, depthNear));

        var left = random.NextComponent();
        var bottom = random.NextComponent();
        hash.Add(Matrix4.CreateOrthographicOffCenter(left, left + 1f + random.NextSingle() * 100f, bottom,
            bottom + 1f + random.NextSingle() * 100f, -depthNear, depthFar));
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
