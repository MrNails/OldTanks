using System.Numerics;
using System.Runtime.CompilerServices;
using CoolEngine.GraphicalEngine.Core;
using CoolEngine.Services.Renderers;
using OpenTK.Graphics.OpenGL4;

namespace CoolEngine.Tests.Services.Renderers;

[TestFixture]
public sealed class ObjectRendererOldTests
{
    private static readonly int[] s_capacities = { 0, 128, 6900, 16384, 1048576 };

    private static readonly int[] s_requests =
        { 1, 2, 127, 128, 129, 5760, 6900, 6901, 11808, 1048576, 1048577, int.MaxValue };

    #region GetPrimitivesUpload

    [Test]
    public void GetPrimitivesUpload_RequestAboveCurrentCapacity_GrowsToHoldRequest()
    {
        var upload = ObjectRendererOld.GetPrimitivesUpload(6900, 11808);

        Assert.That(upload, Is.EqualTo((16384, 11808)));
    }

    [Test]
    public void GetPrimitivesUpload_NoBufferYet_ReturnsNextPowerOfTwoAboveRequest()
    {
        var upload = ObjectRendererOld.GetPrimitivesUpload(0, 11808);

        Assert.That(upload, Is.EqualTo((16384, 11808)));
    }

    [Test]
    public void GetPrimitivesUpload_SmallRequestWithoutBuffer_ReturnsMinimumCapacity()
    {
        var upload = ObjectRendererOld.GetPrimitivesUpload(0, 2);

        Assert.That(upload, Is.EqualTo((128, 2)));
    }

    [Test]
    public void GetPrimitivesUpload_RequestWithinCapacity_KeepsCapacityAndDrawsRequest()
    {
        var upload = ObjectRendererOld.GetPrimitivesUpload(16384, 2);

        Assert.That(upload, Is.EqualTo((16384, 2)));
    }

    [Test]
    public void GetPrimitivesUpload_RequestEqualToCapacity_KeepsCapacity()
    {
        var upload = ObjectRendererOld.GetPrimitivesUpload(128, 128);

        Assert.That(upload, Is.EqualTo((128, 128)));
    }

    [Test]
    public void GetPrimitivesUpload_RequestIsPowerOfTwo_ReturnsRequestAsCapacity()
    {
        var upload = ObjectRendererOld.GetPrimitivesUpload(128, 4096);

        Assert.That(upload, Is.EqualTo((4096, 4096)));
    }

    [Test]
    public void GetPrimitivesUpload_RequestOneAbovePowerOfTwo_ReturnsNextPowerOfTwo()
    {
        var upload = ObjectRendererOld.GetPrimitivesUpload(4096, 4097);

        Assert.That(upload, Is.EqualTo((8192, 4097)));
    }

    [Test]
    public void GetPrimitivesUpload_RequestAboveLimit_CutsCapacityAndDrawCountToLimit()
    {
        var upload = ObjectRendererOld.GetPrimitivesUpload(16384, 2000000);

        Assert.That(upload, Is.EqualTo((1048576, 1048576)));
    }

    [Test]
    public void GetPrimitivesUpload_CapacityAtLimitAndRequestAboveIt_KeepsLimit()
    {
        var upload = ObjectRendererOld.GetPrimitivesUpload(1048576, 1048577);

        Assert.That(upload, Is.EqualTo((1048576, 1048576)));
    }

    [Test]
    public void GetPrimitivesUpload_AnyRequest_DrawCountFitsCapacityAndCapacityNeverShrinks()
    {
        Assert.Multiple(() =>
        {
            foreach (var capacity in s_capacities)
            {
                foreach (var request in s_requests)
                {
                    var upload = ObjectRendererOld.GetPrimitivesUpload(capacity, request);
                    var pair = $"capacity {capacity}, request {request}";

                    Assert.That(upload.DrawCount, Is.LessThanOrEqualTo(upload.Capacity), pair);
                    Assert.That(upload.Capacity, Is.GreaterThanOrEqualTo(capacity), pair);
                    Assert.That(upload.Capacity, Is.LessThanOrEqualTo(Math.Max(capacity, 1048576)), pair);
                    Assert.That(upload.DrawCount, Is.EqualTo(Math.Min(request, upload.Capacity)), pair);

                    if (request <= capacity)
                        Assert.That(upload.Capacity, Is.EqualTo(capacity), pair);
                }
            }
        });
    }

    #endregion

    #region DrawPrimitives

    [Test]
    public void DrawPrimitives_DrawAmountAboveArrayLength_ThrowsArgumentOutOfRangeException()
    {
        var shader = CreateShaderWithoutGl();
        var primitives = new Vector3[2];

        Assert.That(() => ObjectRendererOld.DrawPrimitives(PrimitiveType.Lines, shader, primitives, 3),
            Throws.TypeOf<ArgumentOutOfRangeException>()
                .With.Property(nameof(ArgumentOutOfRangeException.ParamName)).EqualTo("drawAmount"));
    }

    #endregion

    // Shader's constructor and finalizer call OpenGL (KI-017); the guard must throw before the shader is used.
    private static Shader CreateShaderWithoutGl()
    {
        var shader = (Shader)RuntimeHelpers.GetUninitializedObject(typeof(Shader));
        GC.SuppressFinalize(shader);

        return shader;
    }
}
