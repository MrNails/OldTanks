using CoolEngine.Core.Hosting;
using CoolEngine.Core.Tests.TestSupport;
using CoolEngine.Core.Timing;

namespace CoolEngine.Core.Tests.Hosting;

[TestFixture]
public sealed class FixedTickRunnerTests
{
    private const long Frequency = 10_000_000;
    private const int Rate = 60;
    private const int Cap = 15;
    private const long Origin = 1_000;
    private const long P = 166_667;

    private static readonly TimeSpan s_hangDeadline = TimeSpan.FromSeconds(10);

    private static FixedTickRunner CreateStarted(RecordingTickHandler handler)
    {
        var runner = new FixedTickRunner(new FixedTickClock(Rate, Frequency, Cap), handler);
        runner.Start(Origin);
        return runner;
    }

    [Test]
    public void RunDue_TwoPeriodsElapsed_CallsTickWithNumbersOneAndTwo()
    {
        var handler = new RecordingTickHandler();
        var runner = CreateStarted(handler);

        runner.RunDue(Origin + 2 * P);

        Assert.Multiple(() =>
        {
            Assert.That(handler.Calls.Select(c => (c.Kind, c.Number)),
                Is.EqualTo(new[] { (TickCallKind.Tick, 1L), (TickCallKind.Tick, 2L) }));
            Assert.That(runner.TickNumber, Is.EqualTo(2L));
        });
    }

    [Test]
    public void RunDue_HandlerThrowsOnTheSecondOfFiveTicks_PausesRaisesFaultedOnceAndRunsNoMoreTicks()
    {
        var x = new InvalidOperationException("x");
        var handler = new RecordingTickHandler { ThrowInTick = tick => tick == 2 ? x : null };
        var runner = CreateStarted(handler);
        var faults = RecordFaults(runner);

        runner.RunDue(Origin + 5 * P);

        Assert.That(faults, Has.Count.EqualTo(1));
        Assert.Multiple(() =>
        {
            Assert.That(handler.Calls.Select(c => (c.Kind, c.Number)),
                Is.EqualTo(new[] { (TickCallKind.Tick, 1L), (TickCallKind.Tick, 2L) }));
            Assert.That(runner.IsPaused, Is.True);
            Assert.That(faults[0].Exception, Is.SameAs(x));
            Assert.That(faults[0].TickNumber, Is.EqualTo(2L));
            Assert.That(faults[0].InPausedFrame, Is.False);
        });
    }

    [Test]
    public void RunDue_AfterAFaultAndOnePeriod_CallsPausedFrameNotTick()
    {
        var handler = new RecordingTickHandler { ThrowInTick = tick => tick == 2 ? new InvalidOperationException("x") : null };
        var runner = CreateStarted(handler);
        runner.RunDue(Origin + 5 * P);
        var before = handler.Calls.Length;

        runner.RunDue(Origin + 6 * P);

        Assert.That(handler.Calls.Skip(before).Select(c => (c.Kind, c.Number)),
            Is.EqualTo(new[] { (TickCallKind.PausedFrame, 2L) }));
    }

    [Test]
    public void RunDue_PausedFrameThrowsRepeatedly_RaisesFaultedOnlyOnce()
    {
        var handler = new RecordingTickHandler { ThrowInPausedFrame = new InvalidOperationException("x") };
        var runner = CreateStarted(handler);
        var faults = RecordFaults(runner);
        runner.Pause();

        for (var i = 1; i <= 4; i++)
            runner.RunDue(Origin + i * P);

        Assert.Multiple(() =>
        {
            Assert.That(handler.PausedFrameCount, Is.EqualTo(4), "paused frames");
            Assert.That(faults, Has.Count.EqualTo(1), "Faulted raised");
        });
    }

    [Test]
    public void Resume_AfterAFault_RunsTicksAndRaisesTheNextFault()
    {
        var handler = new RecordingTickHandler
        {
            ThrowInTick = tick => tick is 2 or 3 ? new InvalidOperationException($"x{tick}") : null
        };
        var runner = CreateStarted(handler);
        var faults = RecordFaults(runner);
        runner.RunDue(Origin + 5 * P);
        var ticksBefore = handler.TickCount;

        runner.Resume();
        runner.RunDue(Origin + 6 * P);

        Assert.Multiple(() =>
        {
            Assert.That(handler.TickCount - ticksBefore, Is.EqualTo(1), "ticks started after the resume");
            Assert.That(faults, Has.Count.EqualTo(2), "Faulted raised");
        });
    }

    [Test]
    public void Step_AfterAFault_RunsExactlyOneTick()
    {
        var handler = new RecordingTickHandler { ThrowInTick = tick => tick == 2 ? new InvalidOperationException("x") : null };
        var runner = CreateStarted(handler);
        runner.RunDue(Origin + 5 * P);
        var ticksBefore = handler.TickCount;

        runner.Step();
        runner.RunDue(Origin + 5 * P);

        Assert.Multiple(() =>
        {
            Assert.That(handler.TickCount - ticksBefore, Is.EqualTo(1), "ticks after the step");
            Assert.That(runner.IsPaused, Is.True);
        });
    }

    [Test]
    public void RunDue_ResumeDuringTheFaultingTick_StaysPausedAndRaisesFaultedOnce()
    {
        var handler = new RecordingTickHandler
        {
            ThrowInTick = tick => tick == 1 ? new InvalidOperationException("x") : null,
            ThrowInPausedFrame = new InvalidOperationException("y")
        };
        var runner = CreateStarted(handler);
        handler.OnTick = _ => runner.Resume();
        var faults = RecordFaults(runner);

        for (var i = 1; i <= 4; i++)
            runner.RunDue(Origin + i * P);

        Assert.Multiple(() =>
        {
            Assert.That(runner.IsPaused, Is.True, "paused");
            Assert.That(handler.TickCount, Is.EqualTo(1), "ticks");
            Assert.That(handler.PausedFrameCount, Is.EqualTo(3), "paused frames");
            Assert.That(faults, Has.Count.EqualTo(1), "Faulted raised");
        });
    }

    [Test]
    public void RunDue_StepWhileRunningDuringTheFaultingTick_RunsNoTickAfterTheFault()
    {
        var handler = new RecordingTickHandler { ThrowInTick = tick => tick == 1 ? new InvalidOperationException("x") : null };
        var runner = CreateStarted(handler);
        handler.OnTick = tick =>
        {
            if (tick == 1)
                runner.Step();
        };
        runner.RunDue(Origin + P);

        runner.RunDue(Origin + P);

        Assert.Multiple(() =>
        {
            Assert.That(handler.TickCount, Is.EqualTo(1), "ticks");
            Assert.That(runner.IsPaused, Is.True, "paused");
        });
    }

    [Test]
    public void RunDue_ResumeThenPauseBeforeTheNextRunDue_DoesNotRaiseFaultedAgain()
    {
        var handler = new RecordingTickHandler
        {
            ThrowInTick = tick => tick == 1 ? new InvalidOperationException("x") : null,
            ThrowInPausedFrame = new InvalidOperationException("y")
        };
        var runner = CreateStarted(handler);
        var faults = RecordFaults(runner);
        runner.RunDue(Origin + P);

        runner.Resume();
        runner.Pause();
        runner.RunDue(Origin + 2 * P);

        Assert.Multiple(() =>
        {
            Assert.That(handler.TickCount, Is.EqualTo(1), "ticks");
            Assert.That(handler.PausedFrameCount, Is.EqualTo(1), "paused frames");
            Assert.That(runner.IsPaused, Is.True, "paused");
            Assert.That(faults, Has.Count.EqualTo(1), "Faulted raised");
        });
    }

    [Test]
    public void RunDue_ResumeFromTheFaultedSubscriber_RunsTheNextTickAndRaisesItsFault()
    {
        var handler = new RecordingTickHandler { ThrowInTick = _ => new InvalidOperationException("x") };
        var runner = CreateStarted(handler);
        var faults = new List<TickFaultEventArgs>();
        runner.Faulted += (sender, e) =>
        {
            faults.Add(e);
            if (faults.Count == 1)
                sender.Resume();
        };
        runner.RunDue(Origin + P);

        runner.RunDue(Origin + 2 * P);

        Assert.Multiple(() =>
        {
            Assert.That(handler.TickCount, Is.EqualTo(2), "ticks");
            Assert.That(runner.IsPaused, Is.True, "paused by the second fault");
            Assert.That(faults, Has.Count.EqualTo(2), "Faulted raised");
            Assert.That(faults[1].TickNumber, Is.EqualTo(2L), "the second fault's tick");
        });
    }

    [Test]
    public void RunDue_PausedFrameSucceedsBetweenTwoFaults_RaisesFaultedTwice()
    {
        var handler = new RecordingTickHandler { ThrowInTick = tick => tick == 1 ? new InvalidOperationException("x") : null };
        var runner = CreateStarted(handler);
        var faults = RecordFaults(runner);
        runner.RunDue(Origin + P);
        runner.RunDue(Origin + 2 * P);

        handler.ThrowInPausedFrame = new InvalidOperationException("y");
        runner.RunDue(Origin + 3 * P);

        Assert.That(faults, Has.Count.EqualTo(2), "Faulted raised");
        Assert.Multiple(() =>
        {
            Assert.That(handler.PausedFrameCount, Is.EqualTo(2), "paused frames");
            Assert.That(faults[0].InPausedFrame, Is.False, "the first fault in a tick");
            Assert.That(faults[1].InPausedFrame, Is.True, "the second fault in a paused frame");
        });
    }

    [Test]
    public void Step_WhileRunning_IsDiscarded()
    {
        var handler = new RecordingTickHandler();
        var runner = CreateStarted(handler);

        runner.Step();
        runner.RunDue(Origin + P);
        runner.Pause();
        runner.RunDue(Origin + P);

        Assert.Multiple(() =>
        {
            Assert.That(handler.TickCount, Is.EqualTo(1), "ticks (the due one only)");
            Assert.That(handler.PausedFrameCount, Is.EqualTo(0), "paused frames");
        });
    }

    [Test]
    public void RunDue_PauseAndStepRequestedTogether_AppliesThePauseFirstAndRunsOneTick()
    {
        var handler = new RecordingTickHandler();
        var runner = CreateStarted(handler);

        runner.Pause();
        runner.Step();
        runner.RunDue(Origin + 2 * P);

        Assert.Multiple(() =>
        {
            Assert.That(handler.Calls.Select(c => (c.Kind, c.Number)), Is.EqualTo(new[] { (TickCallKind.Tick, 1L) }));
            Assert.That(runner.IsPaused, Is.True, "paused");
        });
    }

    [Test]
    public void NextDueTimestamp_RequestPending_IsTheLastTimestampGiven()
    {
        var handler = new RecordingTickHandler();
        var runner = CreateStarted(handler);
        runner.RunDue(Origin + 50);
        var withoutRequest = runner.NextDueTimestamp;

        runner.Pause();

        Assert.Multiple(() =>
        {
            Assert.That(withoutRequest, Is.EqualTo(Origin + P), "without a request: the next grid point");
            Assert.That(runner.NextDueTimestamp, Is.EqualTo(Origin + 50), "with a pause pending");
        });
    }

    [Test]
    public void Pause_BeforeRunDue_RunsNoTickAndOnePausedFrame()
    {
        var handler = new RecordingTickHandler();
        var runner = CreateStarted(handler);

        runner.Pause();
        runner.RunDue(Origin + 2 * P);

        Assert.Multiple(() =>
        {
            Assert.That(handler.TickCount, Is.EqualTo(0), "ticks");
            Assert.That(handler.PausedFrameCount, Is.EqualTo(1), "paused frames");
        });
    }

    [Test]
    public void Step_FromFourThreadsWhilePaused_RunsOneTickPerRequest()
    {
        const int threadCount = 4;
        const int stepsPerThread = 1_000;

        var handler = new RecordingTickHandler(threadCount * stepsPerThread);
        var runner = CreateStarted(handler);
        runner.Pause();
        runner.RunDue(Origin);

        using var barrier = new Barrier(threadCount);
        var threads = Enumerable.Range(0, threadCount)
            .Select(_ => new Thread(() =>
            {
                barrier.SignalAndWait();
                for (var i = 0; i < stepsPerThread; i++)
                    runner.Step();
            }))
            .ToArray();
        foreach (var thread in threads)
            thread.Start();
        foreach (var thread in threads)
            Assert.That(thread.Join(s_hangDeadline), Is.True, "stepping thread joined");

        runner.RunDue(Origin);

        var ticks = handler.Calls.Where(c => c.Kind == TickCallKind.Tick).Select(c => c.Number).ToArray();
        Assert.That(ticks, Is.EqualTo(Enumerable.Range(1, threadCount * stepsPerThread).Select(i => (long)i)));
    }

    [Test]
    public void RunDue_FaultedSubscriberThrows_TheExceptionReachesTheCallerAndTheRunnerStaysPaused()
    {
        var x = new InvalidOperationException("x");
        var y = new InvalidOperationException("y");
        var handler = new RecordingTickHandler { ThrowInTick = _ => x };
        var runner = CreateStarted(handler);
        runner.Faulted += (_, _) => throw y;

        var thrown = Assert.Throws<InvalidOperationException>(() => runner.RunDue(Origin + P));

        Assert.Multiple(() =>
        {
            Assert.That(thrown, Is.SameAs(y));
            Assert.That(runner.IsPaused, Is.True);
        });
    }

    [Test]
    public void RunDue_BacklogAboveTheCap_RaisesTicksDroppedWithTheCounts()
    {
        var handler = new RecordingTickHandler();
        var runner = CreateStarted(handler);
        var drops = new List<TicksDroppedEventArgs>();
        runner.TicksDropped += (_, e) => drops.Add(e);

        runner.RunDue(Origin + 10_000_000);

        Assert.That(drops, Has.Count.EqualTo(1));
        Assert.Multiple(() =>
        {
            Assert.That(handler.TickCount, Is.EqualTo(15));
            Assert.That(drops[0].DroppedTicks, Is.EqualTo(45));
            Assert.That(drops[0].TotalDroppedTicks, Is.EqualTo(45L));
        });
    }

    [Test]
    public void RunDue_SteadyState_AllocatesNothing()
    {
        const int calls = 10_000;
        const int blocks = 3;

        var handler = new RecordingTickHandler(blocks * calls + 16);
        var runner = CreateStarted(handler);
        var allocated = new long[blocks];

        // Warm up once, so the JIT compiles every path before the measurement.
        var ticks = RunPeriods(runner, 1, 1);

        // Three blocks of the same size: a per-call allocation shows in every block, a one-off runtime allocation on
        // this thread in one at most, so the smallest block is the measurement.
        for (var block = 0; block < blocks; block++)
        {
            var before = GC.GetAllocatedBytesForCurrentThread();
            ticks += RunPeriods(runner, 2 + block * calls, calls);
            allocated[block] = GC.GetAllocatedBytesForCurrentThread() - before;
        }

        Assert.Multiple(() =>
        {
            Assert.That(allocated.Min(), Is.EqualTo(0L), $"allocated bytes, smallest of the blocks {string.Join(", ", allocated)}");
            Assert.That(ticks, Is.EqualTo(blocks * calls + 1L), "ticks run (keeps the results alive)");
        });
    }

    private static long RunPeriods(FixedTickRunner runner, int firstPeriod, int count)
    {
        var ticks = 0L;
        for (var i = 0; i < count; i++)
            ticks += runner.RunDue(Origin + (firstPeriod + i) * P).Ticks;

        return ticks;
    }

    private static List<TickFaultEventArgs> RecordFaults(FixedTickRunner runner)
    {
        var faults = new List<TickFaultEventArgs>();
        runner.Faulted += (_, e) => faults.Add(e);
        return faults;
    }
}
