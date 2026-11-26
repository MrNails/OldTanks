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
    // once with a placeholder and the actual hash from its failure message was copied here. Re-record it only for a
    // deliberate change of a result, never to make a path agree.
    private const ulong Golden = 0x3CD765809D9E75E7;

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
