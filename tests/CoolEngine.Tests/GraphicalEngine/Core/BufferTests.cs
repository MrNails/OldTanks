using System.Reflection;
using System.Runtime.CompilerServices;
using CoolEngine.GraphicalEngine.Core;
using CoolEngine.GraphicalEngine.Services;
using OpenTK.Graphics.OpenGL4;

namespace CoolEngine.Tests.GraphicalEngine.Core;

// The finalizer of an undisposed Buffer<T> (KI-017). Headless: no GL context, so a GL call from the finalizer ends the test host.
[TestFixture]
[NonParallelizable]
public sealed class BufferTests
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
    public void Finalize_UndisposedBuffer_QueuesItsBufferWithoutBinding()
    {
        NewBuffer(900020);

        Collect();
        var recorded = new List<(GLObjectKind Kind, int Name)>();
        GLDeletionQueue.Drain((kind, name) => recorded.Add((kind, name)));

        Assert.That(recorded.Where(e => e.Name == 900020),
            Is.EqualTo(new[] { (GLObjectKind.Buffer, 900020) }));
    }

    [Test]
    public void Finalize_BufferAlreadyDisposed_QueuesNothing()
    {
        NewDisposedBuffer(900021);

        Collect();
        var recorded = new List<(GLObjectKind Kind, int Name)>();
        GLDeletionQueue.Drain((kind, name) => recorded.Add((kind, name)));

        Assert.That(recorded.Where(e => e.Name == 900021), Is.Empty);
    }

    private static void Collect()
    {
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
    }

    // Not inlined, so the buffer is unreachable once it returns. Its constructor calls OpenGL, so the object is
    // built uninitialized with only its id and target set; its finalizer is not suppressed.
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static int NewBuffer(int id)
    {
        var buffer = (Buffer<int>)RuntimeHelpers.GetUninitializedObject(typeof(Buffer<int>));
        SetBackingField(buffer, nameof(Buffer<int>.Id), id);
        SetBackingField(buffer, nameof(Buffer<int>.BufferTarget), BufferTarget.ArrayBuffer);

        return buffer.Id;
    }

    // As NewBuffer, with m_disposed set by reflection and the finalizer left registered.
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static int NewDisposedBuffer(int id)
    {
        var buffer = (Buffer<int>)RuntimeHelpers.GetUninitializedObject(typeof(Buffer<int>));
        SetBackingField(buffer, nameof(Buffer<int>.Id), id);
        SetBackingField(buffer, nameof(Buffer<int>.BufferTarget), BufferTarget.ArrayBuffer);
        typeof(Buffer<int>).GetField("m_disposed", BindingFlags.NonPublic | BindingFlags.Instance)!
            .SetValue(buffer, true);

        return buffer.Id;
    }

    private static void SetBackingField(Buffer<int> buffer, string property, object value)
    {
        typeof(Buffer<int>).GetField($"<{property}>k__BackingField", BindingFlags.NonPublic | BindingFlags.Instance)!
            .SetValue(buffer, value);
    }
}
