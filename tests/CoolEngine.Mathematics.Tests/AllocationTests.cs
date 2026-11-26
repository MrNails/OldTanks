using System.Numerics;
using CoolEngine.Mathematics.Tests.TestSupport;

namespace CoolEngine.Mathematics.Tests;

[TestFixture]
public sealed class AllocationTests
{
    private const int Calls = 10_000;

    [Test]
    public void HotPaths_TenThousandCalls_AllocateNothing()
    {
        var random = Inputs.CreateRandom();
        var matrix = random.NextTrs();
        var other = random.NextTrs();
        var matrix3 = random.NextRotationScale3();
        var vector4 = random.NextVector4();
        var vector3 = random.NextVector3();
        var rotation = random.NextRotation();

        // Warm up once, so the JIT compiles every path before the measurement.
        var checksum = RunHotPaths(1, matrix, other, matrix3, vector4, vector3, rotation);

        var before = GC.GetAllocatedBytesForCurrentThread();
        checksum += RunHotPaths(Calls, matrix, other, matrix3, vector4, vector3, rotation);
        var after = GC.GetAllocatedBytesForCurrentThread();

        Assert.Multiple(() =>
        {
            Assert.That(after - before, Is.EqualTo(0L), "allocated bytes");
            Assert.That(float.IsNaN(checksum), Is.False, "checksum (keeps the results alive)");
        });
    }

    // The library's own members only (the vectors and the quaternion are System.Numerics'); every result feeds the
    // checksum, so nothing is optimized away.
    private static float RunHotPaths(int calls, Matrix4 matrix, Matrix4 other, Matrix3 matrix3, Vector4 vector4, Vector3 vector3,
        Quaternion rotation)
    {
        var checksum = 0f;
        for (var i = 0; i < calls; i++)
        {
            checksum += (matrix * other).Column0.X;
            checksum += (matrix * vector4).X;
            checksum += matrix.TransformPosition(vector3).Y;
            checksum += matrix.Inverted().Column3.X;
            checksum += Matrix4.CreateFromQuaternion(rotation).Column1.Y;
            checksum += (matrix3 * vector3).Z;
        }

        return checksum;
    }
}
