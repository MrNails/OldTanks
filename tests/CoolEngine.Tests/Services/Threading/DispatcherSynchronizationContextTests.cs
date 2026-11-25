using CoolEngine.Services.Threading;
using CoolEngine.Tests.TestSupport;
using Serilog.Events;

namespace CoolEngine.Tests.Services.Threading;

[TestFixture]
[NonParallelizable]
public sealed class DispatcherSynchronizationContextTests
{
    private readonly LogCapture m_capture = new();

    [SetUp]
    public void SetUp()
    {
        m_capture.Start();
    }

    [TearDown]
    public void TearDown()
    {
        m_capture.Stop();
    }

    [Test]
    public void Post_ObjectArrayStateFromOtherThread_PassesTheArrayAsOneArgument()
    {
        var dispatcher = new Dispatcher();
        var context = new DispatcherSynchronizationContext(dispatcher);
        object?[] state = { 1, 2 };
        object? received = null;
        RunOnOtherThread(() => context.Post(s => received = s, state));

        dispatcher.HandleQueue();

        Assert.Multiple(() =>
        {
            Assert.That(received, Is.SameAs(state));
            Assert.That(m_capture.Sink.Events, Is.Empty);
        });
    }

    [Test]
    public void Send_ObjectArrayStateOnOwnerThread_PassesTheArrayAsOneArgument()
    {
        var dispatcher = new Dispatcher();
        var context = new DispatcherSynchronizationContext(dispatcher);
        object?[] state = { 1, 2 };
        object? received = null;

        Assert.DoesNotThrow(() => context.Send(s => received = s, state));

        Assert.That(received, Is.SameAs(state));
    }

    [Test]
    public void Post_CallbackThrowsFromOtherThread_LogsTheOriginalException()
    {
        var dispatcher = new Dispatcher();
        var context = new DispatcherSynchronizationContext(dispatcher);
        var x = new InvalidOperationException("boom");
        RunOnOtherThread(() => context.Post(_ => throw x, null));

        dispatcher.HandleQueue();

        var events = m_capture.Sink.Events.ToArray();
        Assert.That(events, Has.Length.EqualTo(1));
        Assert.Multiple(() =>
        {
            Assert.That(events[0].Level, Is.EqualTo(LogEventLevel.Error));
            Assert.That(events[0].Exception, Is.SameAs(x));
        });
        Assert.That(events[0].Properties.TryGetValue("Callback", out var callback), Is.True);
        Assert.That(callback, Is.InstanceOf<ScalarValue>());
        Assert.That(((ScalarValue)callback!).Value, Is.InstanceOf<string>());
        Assert.That((string)((ScalarValue)callback).Value!, Does.Contain(nameof(DispatcherSynchronizationContextTests)));
    }

    [Test]
    public void Post_OnOwnerThread_RunsTheCallbackBeforeReturning()
    {
        var dispatcher = new Dispatcher();
        var context = new DispatcherSynchronizationContext(dispatcher);
        var ran = false;

        context.Post(_ => ran = true, null);

        Assert.That(ran, Is.True);
    }

    private static void RunOnOtherThread(Action action)
    {
        var thread = new Thread(() => action());
        thread.Start();
        thread.Join();
    }
}
