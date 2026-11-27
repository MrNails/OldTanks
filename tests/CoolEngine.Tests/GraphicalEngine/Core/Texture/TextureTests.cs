using System.Reflection;
using System.Runtime.CompilerServices;
using CoolEngine.GraphicalEngine.Services;
using CoolEngine.Tests.TestSupport;
using Serilog.Events;
using EngineTexture = CoolEngine.GraphicalEngine.Core.Texture.Texture;

namespace CoolEngine.Tests.GraphicalEngine.Core.Texture;

// The finalizer of an undisposed Texture (KI-017). Headless: no GL context, so a GL call from the finalizer ends the test host.
[TestFixture]
[NonParallelizable]
public sealed class TextureTests
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
    public void Finalize_UndisposedTexture_MakesNoGLCall()
    {
        NewTexture(900001, "LeakTexture");

        Collect();

        Assert.Pass();
    }

    [Test]
    public void Finalize_UndisposedTexture_QueuesItsHandleOnce()
    {
        NewTexture(900002, "LeakTexture");

        Collect();
        var recorded = new List<(GLObjectKind Kind, int Name)>();
        GLDeletionQueue.Drain((kind, name) => recorded.Add((kind, name)));

        Assert.That(recorded.Where(e => e.Name == 900002),
            Is.EqualTo(new[] { (GLObjectKind.Texture, 900002) }));
    }

    [Test]
    public void Finalize_UndisposedTexture_LogsOneLeakLineNamingIt()
    {
        var capture = new LogCapture();
        capture.Start();
        try
        {
            NewTexture(900003, "LeakTexture");

            Collect();

            var lines = capture.Sink.Events
                .Select(e => (e.Level, Text: e.RenderMessage()))
                .Where(e => e.Text.Contains("900003"))
                .ToArray();
            Assert.That(lines, Has.Length.EqualTo(1));
            Assert.Multiple(() =>
            {
                Assert.That(lines[0].Level, Is.EqualTo(LogEventLevel.Warning));
                Assert.That(lines[0].Text, Does.Contain("LeakTexture"));
                Assert.That(lines[0].Text, Does.Not.Contain("Shader"));
            });
        }
        finally
        {
            capture.Stop();
        }
    }

    [Test]
    public void Finalize_TextureAlreadyDisposed_QueuesNothing()
    {
        NewDisposedTexture(900004, "LeakTexture");

        Collect();
        var recorded = new List<(GLObjectKind Kind, int Name)>();
        GLDeletionQueue.Drain((kind, name) => recorded.Add((kind, name)));

        Assert.That(recorded.Where(e => e.Name == 900004), Is.Empty);
    }

    private static void Collect()
    {
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
    }

    // Not inlined, so the texture is unreachable once it returns. Its finalizer is not suppressed.
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static int NewTexture(int handle, string name)
    {
        var texture = CreateUnsuppressedTexture(handle, name);

        return texture.Handle;
    }

    // As NewTexture, with Disposed set through its private setter and the finalizer left registered.
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static int NewDisposedTexture(int handle, string name)
    {
        var texture = CreateUnsuppressedTexture(handle, name);
        typeof(EngineTexture).GetProperty(nameof(EngineTexture.Disposed))!
            .GetSetMethod(nonPublic: true)!
            .Invoke(texture, new object[] { true });

        return texture.Handle;
    }

    private static EngineTexture CreateUnsuppressedTexture(int handle, string name)
    {
        var constructor = typeof(EngineTexture).GetConstructor(BindingFlags.NonPublic | BindingFlags.Instance,
            new[] { typeof(int), typeof(float), typeof(float) })!;
        var texture = (EngineTexture)constructor.Invoke(new object[] { handle, 1f, 1f });
        texture.Name = name;

        return texture;
    }
}
