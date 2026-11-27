using System.Reflection;
using System.Runtime.CompilerServices;
using CoolEngine.GraphicalEngine.Core;
using CoolEngine.GraphicalEngine.Services;

namespace CoolEngine.Tests.GraphicalEngine.Core;

// The finalizer of an undisposed Shader (KI-017). Headless: no GL context, so a GL call from the finalizer ends the test host.
[TestFixture]
[NonParallelizable]
public sealed class ShaderTests
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
    public void Finalize_UndisposedShader_QueuesItsProgram()
    {
        NewShader(900010, "LeakShader");

        Collect();
        var recorded = new List<(GLObjectKind Kind, int Name)>();
        GLDeletionQueue.Drain((kind, name) => recorded.Add((kind, name)));

        Assert.That(recorded.Where(e => e.Name == 900010),
            Is.EqualTo(new[] { (GLObjectKind.Program, 900010) }));
    }

    [Test]
    public void Finalize_ShaderAlreadyDisposed_QueuesNothing()
    {
        NewDisposedShader(900011, "LeakShader");

        Collect();
        var recorded = new List<(GLObjectKind Kind, int Name)>();
        GLDeletionQueue.Drain((kind, name) => recorded.Add((kind, name)));

        Assert.That(recorded.Where(e => e.Name == 900011), Is.Empty);
    }

    private static void Collect()
    {
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
    }

    // Not inlined, so the shader is unreachable once it returns. Its constructor calls OpenGL, so the object is
    // built uninitialized with only its handle and name set; its finalizer is not suppressed.
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static int NewShader(int handle, string name)
    {
        var shader = (Shader)RuntimeHelpers.GetUninitializedObject(typeof(Shader));
        SetBackingField(shader, nameof(Shader.Handle), handle);
        SetBackingField(shader, nameof(Shader.Name), name);

        return shader.Handle;
    }

    // As NewShader, with Disposed set through its private setter and the finalizer left registered.
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static int NewDisposedShader(int handle, string name)
    {
        var shader = (Shader)RuntimeHelpers.GetUninitializedObject(typeof(Shader));
        SetBackingField(shader, nameof(Shader.Handle), handle);
        SetBackingField(shader, nameof(Shader.Name), name);
        typeof(Shader).GetProperty(nameof(Shader.Disposed))!
            .GetSetMethod(nonPublic: true)!
            .Invoke(shader, new object[] { true });

        return shader.Handle;
    }

    private static void SetBackingField(Shader shader, string property, object value)
    {
        typeof(Shader).GetField($"<{property}>k__BackingField", BindingFlags.NonPublic | BindingFlags.Instance)!
            .SetValue(shader, value);
    }
}
