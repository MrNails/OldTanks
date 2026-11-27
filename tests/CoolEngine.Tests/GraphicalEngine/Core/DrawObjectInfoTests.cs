using System.Reflection;
using System.Runtime.CompilerServices;
using CoolEngine.GraphicalEngine.Core;
using CoolEngine.GraphicalEngine.Services;

namespace CoolEngine.Tests.GraphicalEngine.Core;

// The finalizer of an undisposed DrawObjectInfo (KI-017). Headless: no GL context, so a GL call from the finalizer
// ends the test host. The public constructor takes plain names and makes no GL call.
[TestFixture]
[NonParallelizable]
public sealed class DrawObjectInfoTests
{
    [SetUp]
    public void SetUp()
    {
        GLDeletionQueue.Drain(static (_, _) => { });
    }

    [TearDown]
    public void TearDown()
    {
        GLDeletionQueue.Drain(static (_, _) => { });
    }

    [Test]
    public void Finalize_UndisposedDrawObjectInfo_QueuesItsThreeNames()
    {
        NewDrawObjectInfo(900030, 900031, 900032);

        Collect();
        var recorded = new List<(GLObjectKind Kind, int Name)>();
        GLDeletionQueue.Drain((kind, name) => recorded.Add((kind, name)));

        Assert.That(recorded.Where(e => e.Name is >= 900030 and <= 900032), Is.EquivalentTo(new[]
        {
            (GLObjectKind.VertexArray, 900030),
            (GLObjectKind.Buffer, 900031),
            (GLObjectKind.Buffer, 900032)
        }));
    }

    [Test]
    public void Finalize_DrawObjectInfoWithoutElementBuffer_QueuesTwoNames()
    {
        NewDrawObjectInfo(900040, 900041, 0);

        Collect();
        var recorded = new List<(GLObjectKind Kind, int Name)>();
        GLDeletionQueue.Drain((kind, name) => recorded.Add((kind, name)));

        Assert.Multiple(() =>
        {
            Assert.That(recorded.Where(e => e.Name is >= 900040 and <= 900041), Is.EquivalentTo(new[]
            {
                (GLObjectKind.VertexArray, 900040),
                (GLObjectKind.Buffer, 900041)
            }));
            Assert.That(recorded.Where(e => e.Name == 0), Is.Empty);
        });
    }

    [Test]
    public void Finalize_DrawObjectInfoAlreadyDisposed_QueuesNothing()
    {
        NewDisposedDrawObjectInfo(900050, 900051, 900052);

        Collect();
        var recorded = new List<(GLObjectKind Kind, int Name)>();
        GLDeletionQueue.Drain((kind, name) => recorded.Add((kind, name)));

        Assert.That(recorded.Where(e => e.Name is >= 900050 and <= 900052), Is.Empty);
    }

    private static void Collect()
    {
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
    }

    // Not inlined, so the info is unreachable once it returns.
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static int NewDrawObjectInfo(int vertexArrayObject, int vertexBufferObject, int elementsBufferObject)
    {
        var info = new DrawObjectInfo(vertexArrayObject, vertexBufferObject, elementsBufferObject);

        return info.VertexArrayObject;
    }

    // As NewDrawObjectInfo, with m_disposed set by reflection and the names left non-zero, so only the disposed
    // guard keeps them out of the queue; the finalizer stays registered.
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static int NewDisposedDrawObjectInfo(int vertexArrayObject, int vertexBufferObject, int elementsBufferObject)
    {
        var info = new DrawObjectInfo(vertexArrayObject, vertexBufferObject, elementsBufferObject);
        typeof(DrawObjectInfo).GetField("m_disposed", BindingFlags.NonPublic | BindingFlags.Instance)!
            .SetValue(info, true);

        return info.VertexArrayObject;
    }
}
