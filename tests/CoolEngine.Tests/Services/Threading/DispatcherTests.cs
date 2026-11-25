using System.Collections.Concurrent;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.CompilerServices;
using CoolEngine.Services.Threading;
using CoolEngine.Tests.TestSupport;
using Serilog.Events;

namespace CoolEngine.Tests.Services.Threading;

[TestFixture]
[NonParallelizable]
public sealed class DispatcherTests
{
    private static readonly TimeSpan s_pumpDeadline = TimeSpan.FromSeconds(10);

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
    public void Post_ActionThrowsOnOtherThread_LogsErrorWithTheException()
    {
        var dispatcher = new Dispatcher();
        var x = new InvalidOperationException("boom");
        RunOnOtherThread(() => dispatcher.Post(() => throw x));

        Assert.DoesNotThrow(dispatcher.HandleQueue);

        AssertSingleErrorWith(x);
    }

    [Test]
    public void Post_ActionWithStateThrowsOnOtherThread_LogsErrorWithTheException()
    {
        var dispatcher = new Dispatcher();
        var x = new InvalidOperationException("boom");
        RunOnOtherThread(() => dispatcher.Post((object? s) => throw x, 42));

        Assert.DoesNotThrow(dispatcher.HandleQueue);

        AssertSingleErrorWith(x);
    }

    [Test]
    public void Post_DelegateThrowsOnOtherThread_LogsTheInnerException()
    {
        var dispatcher = new Dispatcher();
        var x = new InvalidOperationException("boom");
        RunOnOtherThread(() => dispatcher.Post((Delegate)new Action<int>(_ => throw x), 5));

        Assert.DoesNotThrow(dispatcher.HandleQueue);

        AssertSingleErrorWith(x);
    }

    [Test]
    public void Post_CallbackThrowsOnOtherThread_HandleQueueRunsTheNextCallback()
    {
        var dispatcher = new Dispatcher();
        var x = new InvalidOperationException("boom");
        var ran = false;
        RunOnOtherThread(() =>
        {
            dispatcher.Post(() => throw x);
            dispatcher.Post(() => ran = true);
        });

        Assert.DoesNotThrow(dispatcher.HandleQueue);

        Assert.That(ran, Is.True);
    }

    [Test]
    public void Post_CallbackThrowsOnOwnerThread_ThrowsToTheCallerAndLogsNothing()
    {
        var dispatcher = new Dispatcher();
        var x = new InvalidOperationException("boom");

        var thrown = Assert.Throws<InvalidOperationException>(() => dispatcher.Post(() => throw x));

        Assert.Multiple(() =>
        {
            Assert.That(thrown, Is.SameAs(x));
            Assert.That(m_capture.Sink.Events, Is.Empty);
        });
    }

    [Test]
    public void Invoke_CallbackThrowsOnOtherThread_RethrowsToTheCallerAndLogsNothing()
    {
        var dispatcher = new Dispatcher();
        var x = new InvalidOperationException("boom");
        Exception? caught = null;
        var thread = new Thread(() =>
        {
            try
            {
                dispatcher.Invoke(() => throw x);
            }
            catch (Exception e)
            {
                caught = e;
            }
        });
        thread.Start();

        PumpUntilJoined(dispatcher, thread);

        Assert.That(caught, Is.InstanceOf<AggregateException>());
        Assert.Multiple(() =>
        {
            Assert.That(caught!.InnerException, Is.SameAs(x));
            Assert.That(m_capture.Sink.Events, Is.Empty);
        });
    }

    [Test]
    public void Post_FromOneThread_RunsCallbacksInPostingOrder()
    {
        var dispatcher = new Dispatcher();
        var order = new List<int>();
        RunOnOtherThread(() =>
        {
            for (var i = 0; i < 100; i++)
            {
                var value = i;
                dispatcher.Post(() => order.Add(value));
            }
        });

        dispatcher.HandleQueue();

        Assert.That(order, Is.EqualTo(Enumerable.Range(0, 100)));
    }

    [Test]
    public void Post_ManyThreadsWhileOwnerDrains_RunsEveryCallbackOnceOnTheOwnerAndLogsEveryFailure()
    {
        const int posterCount = 8;
        const int postsPerThread = 1000;

        var dispatcher = new Dispatcher();
        var ownerId = Environment.CurrentManagedThreadId;
        var ran = 0;
        var offOwner = 0;
        using var barrier = new Barrier(posterCount);

        var posters = Enumerable.Range(0, posterCount)
            .Select(_ => new Thread(() =>
            {
                barrier.SignalAndWait();
                for (var j = 0; j < postsPerThread; j++)
                {
                    var index = j;
                    dispatcher.Post(() =>
                    {
                        if (Environment.CurrentManagedThreadId != ownerId)
                            Interlocked.Increment(ref offOwner);

                        if (index % 10 == 0)
                            throw new InvalidOperationException();

                        Interlocked.Increment(ref ran);
                    });
                }
            }))
            .ToArray();

        foreach (var poster in posters)
            poster.Start();

        PumpUntilJoined(dispatcher, posters);
        dispatcher.HandleQueue();

        var errors = m_capture.Sink.Events.Count(e => e.Level == LogEventLevel.Error);
        Assert.Multiple(() =>
        {
            Assert.That(ran, Is.EqualTo(7200));
            Assert.That(offOwner, Is.EqualTo(0));
            Assert.That(errors, Is.EqualTo(800));
        });
    }

    [Test]
    public void Post_CallbackThrowsOnOtherThread_IsNotReportedAgainAsUnobserved()
    {
        var x = new InvalidOperationException("boom");
        var y = new InvalidOperationException("control");
        var recorded = new ConcurrentQueue<Exception>();
        EventHandler<UnobservedTaskExceptionEventArgs> handler = (_, e) => recorded.Enqueue(e.Exception);
        TaskScheduler.UnobservedTaskException += handler;
        try
        {
            PostThrowingAndDrain(x, y);

            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            GC.WaitForPendingFinalizers();

            Assert.Multiple(() =>
            {
                // The control task faulted with y is never observed: seeing it proves the finalizer pass ran
                Assert.That(recorded, Has.Some.Matches<Exception>(e => ReferenceEquals(e?.InnerException, y)));
                Assert.That(recorded, Has.None.Matches<Exception>(e => ReferenceEquals(e?.InnerException, x)));
            });
        }
        finally
        {
            TaskScheduler.UnobservedTaskException -= handler;
        }
    }

    [Test]
    public void Post_ActionThrowsTargetInvocationExceptionOnOtherThread_LogsItAsIs()
    {
        var dispatcher = new Dispatcher();
        var x = new InvalidOperationException("boom");
        var tie = new TargetInvocationException(x);
        RunOnOtherThread(() => dispatcher.Post(() => throw tie));

        dispatcher.HandleQueue();

        AssertSingleErrorWith(tie);
    }

    // Not inlined, so the dispatcher, its failed operation and the control task are unreachable once it returns
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void PostThrowingAndDrain(Exception x, Exception y)
    {
        var dispatcher = new Dispatcher();
        RunOnOtherThread(() => dispatcher.Post(() => throw x));
        dispatcher.HandleQueue();

        var control = new TaskCompletionSource();
        control.SetException(y);
    }

    private void AssertSingleErrorWith(Exception expected)
    {
        var events = m_capture.Sink.Events.ToArray();

        Assert.That(events, Has.Length.EqualTo(1));
        Assert.Multiple(() =>
        {
            Assert.That(events[0].Level, Is.EqualTo(LogEventLevel.Error));
            Assert.That(events[0].Exception, Is.SameAs(expected));
        });
    }

    private static void RunOnOtherThread(Action action)
    {
        var thread = new Thread(() => action());
        thread.Start();
        thread.Join();
    }

    private static void PumpUntilJoined(Dispatcher dispatcher, params Thread[] threads)
    {
        var stopwatch = Stopwatch.StartNew();
        while (threads.Any(t => !t.Join(0)))
        {
            dispatcher.HandleQueue();

            if (stopwatch.Elapsed > s_pumpDeadline)
                Assert.Fail($"The threads did not end within {s_pumpDeadline.TotalSeconds} s.");
        }
    }
}
