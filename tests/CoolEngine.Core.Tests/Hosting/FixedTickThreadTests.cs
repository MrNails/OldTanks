using System.Runtime.ExceptionServices;
using CoolEngine.Core.Hosting;
using CoolEngine.Core.Tests.TestSupport;
using CoolEngine.Core.Timing;

namespace CoolEngine.Core.Tests.Hosting;

[TestFixture]
public sealed class FixedTickThreadTests
{
    private const long Frequency = 10_000_000;
    private const int Rate = 60;
    private const int Cap = 15;
    private const long Origin = 1_000;
    private const long P = 166_667;
    private const string ThreadName = "Test simulation";

    private static readonly TimeSpan s_hangDeadline = TimeSpan.FromSeconds(10);

    private ManualTimestampSource m_source = null!;
    private ManualTickWaiter m_waiter = null!;
    private RecordingTickHandler m_handler = null!;
    private FixedTickRunner m_runner = null!;
    private FixedTickThread m_thread = null!;

    [SetUp]
    public void SetUp()
    {
        m_source = new ManualTimestampSource(Frequency, Origin);
        m_waiter = new ManualTickWaiter(m_source);
        m_handler = new RecordingTickHandler();
        m_runner = new FixedTickRunner(new FixedTickClock(Rate, Frequency, Cap), m_handler);
        m_thread = new FixedTickThread(m_runner, m_source, m_waiter, ThreadName);
    }

    [TearDown]
    public void TearDown()
    {
        // Lets a failed test's loop go on (a gate it may block on, a park) so that Dispose can join it.
        m_handler.OpenGate();

        // The bounded Stop fails the test if the loop never ends; after it returns, Dispose only joins a thread that
        // has ended, so the direct call (which the NUnit analyzer requires here) cannot hang.
        StopWithinDeadline();
        m_thread.Dispose();
        m_waiter.Dispose();
    }

    [Test]
    public void Constructor_TimeSourceFrequencyDiffersFromTheClock_ThrowsArgumentException()
    {
        var otherSource = new ManualTimestampSource(Frequency + 1, Origin);

        var thrown = Assert.Throws<ArgumentException>(() => new FixedTickThread(m_runner, otherSource, m_waiter, ThreadName).Dispose());

        Assert.That(thrown!.ParamName, Is.EqualTo("time"));
    }

    [Test]
    public void Start_WaiterReleasedForTwoPeriods_RunsTwoTicksOnTheLoopThread()
    {
        m_thread.Start();
        m_waiter.WaitForParked();

        m_waiter.Release(Origin + P);
        m_waiter.WaitForParked();
        m_waiter.Release(Origin + 2 * P);
        m_waiter.WaitForParked();

        var calls = m_handler.Calls;
        Assert.That(calls.Select(c => (c.Kind, c.Number)),
            Is.EqualTo(new[] { (TickCallKind.Tick, 1L), (TickCallKind.Tick, 2L) }));
        var loopThread = calls[0].Thread;
        Assert.Multiple(() =>
        {
            Assert.That(calls[1].Thread, Is.SameAs(loopThread), "both ticks on one thread");
            Assert.That(loopThread, Is.Not.SameAs(Thread.CurrentThread), "not the test thread");
            Assert.That(loopThread.Name, Is.EqualTo(ThreadName));
            Assert.That(loopThread.IsBackground, Is.True);
        });
    }

    [Test]
    public void Stop_WhileATickRuns_ReturnsOnlyAfterThatTickAndRunsNoOther()
    {
        m_handler.BlockInTick = 1;
        m_thread.Start();
        m_waiter.WaitForParked();
        m_waiter.Release(Origin + 10 * P);
        m_handler.WaitForEntered();

        // Stop runs on its own thread here, joined with the hang deadline below, as StopWithinDeadline does.
        var stopper = new Thread(() => m_thread.Stop()) { IsBackground = true };
        stopper.Start();
        // Stop wakes the waiter after it has set the stop requests, so a wake proves they are set.
        m_waiter.WaitForWakeCount(1);
        var runningWhileBlocked = m_thread.IsRunning;
        var stopperReturnedEarly = stopper.Join(0);
        var loopThread = m_handler.FirstThread!;

        m_handler.OpenGate();

        Assert.That(stopper.Join(s_hangDeadline), Is.True, "stopper joined");
        Assert.Multiple(() =>
        {
            Assert.That(runningWhileBlocked, Is.True, "IsRunning while the tick blocks");
            Assert.That(stopperReturnedEarly, Is.False, "Stop returned before the tick did");
            Assert.That(m_handler.TickCount, Is.EqualTo(1), "ticks recorded");
            Assert.That(m_thread.IsRunning, Is.False, "IsRunning after Stop");
            Assert.That(loopThread.IsAlive, Is.False, "the loop thread is alive");
        });
    }

    [Test]
    public void Stop_WhilePaused_ReturnsWithoutATick()
    {
        m_thread.Start();
        m_waiter.WaitForParked();
        m_thread.Pause();
        m_waiter.WaitForParked();
        var ticksAfterPause = m_handler.TickCount;

        StopWithinDeadline();

        Assert.Multiple(() =>
        {
            Assert.That(m_handler.TickCount, Is.EqualTo(ticksAfterPause), "ticks after the pause");
            Assert.That(m_thread.IsRunning, Is.False);
        });
    }

    [Test]
    public void Dispose_AfterStart_TheLoopThreadHasEnded()
    {
        m_thread.Start();
        m_waiter.WaitForParked();
        m_waiter.Release(Origin + P);
        m_waiter.WaitForParked();
        var loopThread = m_handler.FirstThread!;

        DisposeWithinDeadline();

        Assert.That(loopThread.IsAlive, Is.False);
    }

    [Test]
    public void Stop_FromTheLoopThread_ThrowsInvalidOperation()
    {
        Exception? recorded = null;
        m_handler.OnTick = _ =>
        {
            // On the loop thread on purpose: this call is what the test checks.
            try
            {
                m_thread.Stop();
            }
            catch (Exception e)
            {
                recorded = e;
            }
        };
        m_thread.Start();
        m_waiter.WaitForParked();

        m_waiter.Release(Origin + P);
        m_waiter.WaitForParked();
        StopWithinDeadline();

        Assert.That(recorded, Is.InstanceOf<InvalidOperationException>());
    }

    [Test]
    public void Start_Twice_ThrowsInvalidOperation()
    {
        m_thread.Start();

        Assert.Throws<InvalidOperationException>(() => m_thread.Start());
    }

    [Test]
    public void Start_AfterDispose_ThrowsObjectDisposed()
    {
        DisposeWithinDeadline();

        Assert.Throws<ObjectDisposedException>(() => m_thread.Start());
    }

    [Test]
    public void Stop_BeforeStart_DoesNothing()
    {
        Assert.Multiple(() =>
        {
            Assert.DoesNotThrow(StopWithinDeadline);
            Assert.DoesNotThrow(DisposeWithinDeadline);
        });
    }

    [Test]
    public void Tick_Throws_RaisesFaultedOnceOnTheLoopThreadAndKeepsPausedFrames()
    {
        m_handler.ThrowInTick = tick => tick == 1 ? new InvalidOperationException("x") : null;
        var faultThreads = new List<Thread>();
        m_runner.Faulted += (_, _) =>
        {
            lock (faultThreads)
                faultThreads.Add(Thread.CurrentThread);
        };
        m_thread.Start();
        m_waiter.WaitForParked();

        m_waiter.Release(Origin + P);
        m_waiter.WaitForParked();
        m_waiter.Release(Origin + 2 * P);
        m_waiter.WaitForParked();

        Thread[] faults;
        lock (faultThreads)
            faults = faultThreads.ToArray();
        Assert.That(faults, Has.Length.EqualTo(1), "Faulted raised");
        Assert.Multiple(() =>
        {
            Assert.That(faults[0], Is.SameAs(m_handler.FirstThread), "raised on the loop thread");
            Assert.That(faults[0], Is.Not.SameAs(Thread.CurrentThread), "not on the test thread");
            Assert.That(m_thread.IsPaused, Is.True);
            Assert.That(m_handler.PausedFrameCount, Is.EqualTo(1), "paused frames");
        });
    }

    [Test]
    public void Step_WhileTheLoopWaits_WakesItAndRunsExactlyOneTick()
    {
        m_runner.Pause();
        m_thread.Start();
        m_waiter.WaitForParked();
        var ticksBefore = m_handler.TickCount;

        m_thread.Step();
        m_waiter.WaitForParked();

        Assert.Multiple(() =>
        {
            Assert.That(m_waiter.WakeCount, Is.EqualTo(1), "wakes");
            Assert.That(m_handler.TickCount - ticksBefore, Is.EqualTo(1), "ticks after the step");
            Assert.That(m_source.Now, Is.EqualTo(Origin), "the source is unchanged");
        });
    }

    private void StopWithinDeadline()
    {
        RunWithinDeadline(() => m_thread.Stop(), nameof(FixedTickThread.Stop));
    }

    private void DisposeWithinDeadline()
    {
        RunWithinDeadline(() => m_thread.Dispose(), nameof(FixedTickThread.Dispose));
    }

    // Stop and Dispose join the loop thread without a bound. Run on a background thread joined with the hang deadline,
    // a call that never returns fails the test instead of hanging the run; an exception it throws is rethrown here.
    private static void RunWithinDeadline(Action action, string name)
    {
        Exception? thrown = null;
        var thread = new Thread(() =>
        {
            try
            {
                action();
            }
            catch (Exception e)
            {
                thrown = e;
            }
        }) { IsBackground = true };
        thread.Start();

        if (!thread.Join(s_hangDeadline))
            Assert.Fail($"{name} did not return within {s_hangDeadline.TotalSeconds} s.");

        if (thrown is not null)
            ExceptionDispatchInfo.Capture(thrown).Throw();
    }
}
