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
        var vector4 = random.NextVector4();
        var vector3 = random.NextVector3();
        var rotation = random.NextRotation();
        var otherRotation = random.NextRotation();

        // Warm up once, so the JIT compiles every path before the measurement.
        var checksum = RunHotPaths(1, matrix, other, vector4, vector3, rotation, otherRotation);

        var before = GC.GetAllocatedBytesForCurrentThread();
        checksum += RunHotPaths(Calls, matrix, other, vector4, vector3, rotation, otherRotation);
        var after = GC.GetAllocatedBytesForCurrentThread();

        Assert.Multiple(() =>
        {
            Assert.That(after - before, Is.EqualTo(0L), "allocated bytes");
            Assert.That(float.IsNaN(checksum), Is.False, "checksum (keeps the results alive)");
        });
    }

    // Every result feeds the checksum, so nothing is optimized away.
    private static float RunHotPaths(int calls, Matrix4 matrix, Matrix4 other, Vector4 vector4, Vector3 vector3, Quaternion rotation,
        Quaternion otherRotation)
    {
        var checksum = 0f;
        for (var i = 0; i < calls; i++)
        {
            checksum += (matrix * other).Column0.X;
            checksum += (matrix * vector4).X;
            checksum += Vector3.TransformPosition(matrix, vector3).Y;
            checksum += Vector3.Normalize(vector3).Z;
            checksum += (rotation * otherRotation).W;
            checksum += (rotation * vector3).X;
            checksum += matrix.Inverted().Column3.X;
        }

        return checksum;
    }
}
