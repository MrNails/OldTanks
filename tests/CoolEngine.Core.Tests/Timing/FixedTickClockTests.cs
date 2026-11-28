using CoolEngine.Core.Timing;

namespace CoolEngine.Core.Tests.Timing;

[TestFixture]
public sealed class FixedTickClockTests
{
    private const long Frequency = 10_000_000;
    private const int Rate = 60;
    private const int Cap = 15;
    private const long Origin = 1_000;

    private static FixedTickClock CreateStarted()
    {
        var clock = new FixedTickClock(Rate, Frequency, Cap);
        clock.Start(Origin);
        return clock;
    }

    [TestCase(0)]
    [TestCase(-60)]
    public void Constructor_TicksPerSecondNotPositive_ThrowsArgumentOutOfRange(int ticksPerSecond)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => _ = new FixedTickClock(ticksPerSecond, Frequency, Cap));
    }

    [TestCase(0L)]
    [TestCase(-1L)]
    public void Constructor_FrequencyNotPositive_ThrowsArgumentOutOfRange(long frequency)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => _ = new FixedTickClock(Rate, frequency, Cap));
    }

    [TestCase(0)]
    [TestCase(-1)]
    public void Constructor_MaxBacklogBelowOne_ThrowsArgumentOutOfRange(int cap)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => _ = new FixedTickClock(Rate, Frequency, cap));
    }

    [Test]
    public void Constructor_FrequencyTimesRateOverflows_ThrowsArgumentOutOfRange()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => _ = new FixedTickClock(Rate, long.MaxValue / 60, Cap));
    }

    [Test]
    public void Advance_BeforeStart_ThrowsInvalidOperation()
    {
        var clock = new FixedTickClock(Rate, Frequency, Cap);

        Assert.Throws<InvalidOperationException>(() => clock.Advance(0));
    }

    [Test]
    public void Advance_OneUnitBeforeFirstDue_ReturnsNoTick()
    {
        var clock = CreateStarted();

        var advance = clock.Advance(Origin + 166_666);

        Assert.That(advance, Is.EqualTo(new TickAdvance(0, 0, false)));
    }

    [Test]
    public void Advance_AtFirstDueTimestamp_ReturnsOneTick()
    {
        var clock = CreateStarted();

        var advance = clock.Advance(Origin + 166_667);

        Assert.Multiple(() =>
        {
            Assert.That(advance, Is.EqualTo(new TickAdvance(1, 0, false)));
            Assert.That(clock.TicksIssued, Is.EqualTo(1L));
        });
    }

    [TestCase(10_000_000L, 166_667L)]
    [TestCase(1_000_000_000L, 16_666_667L)]
    [TestCase(3_579_545L, 59_660L)]
    public void NextDueTimestamp_AfterStart_IsTheCeilingOfOnePeriod(long frequency, long expected)
    {
        var clock = new FixedTickClock(Rate, frequency, Cap);
        clock.Start(0);

        Assert.That(clock.NextDueTimestamp, Is.EqualTo(expected));
    }

    [Test]
    public void Advance_ThreeTicksAtTenMegahertz_ThirdIsDueAtExactly500000()
    {
        var clock = CreateStarted();

        var first = clock.Advance(Origin + 499_999);
        var second = clock.Advance(Origin + 500_000);

        Assert.Multiple(() =>
        {
            Assert.That(first.Ticks, Is.EqualTo(2));
            Assert.That(second.Ticks, Is.EqualTo(1));
            Assert.That(clock.TicksIssued, Is.EqualTo(3L));
        });
    }

    [TestCase(10_000_000L)]
    [TestCase(1_000_000_000L)]
    [TestCase(3_579_545L)]
    public void Advance_OneHourInUnevenSteps_IssuesExactly216000Ticks(long frequency)
    {
        var clock = new FixedTickClock(Rate, frequency, 1_000);
        clock.Start(0);
        var random = new Random(20261127);
        var end = 3_600 * frequency;
        var now = 0L;

        while (now < end)
        {
            now = Math.Min(end, now + random.NextInt64(1, frequency / 30 + 1));
            clock.Advance(now);
        }

        Assert.Multiple(() =>
        {
            Assert.That(clock.TicksIssued, Is.EqualTo(216_000L));
            Assert.That(clock.TicksDropped, Is.EqualTo(0L));
        });
    }

    [Test]
    public void Advance_TimestampGoesBackwards_ReturnsNoTickAndKeepsTheGrid()
    {
        var clock = CreateStarted();
        var arranged = clock.Advance(Origin + 200_000);

        var backwards = clock.Advance(Origin + 100_000);
        var nextDue = clock.NextDueTimestamp;
        var beforeTheGridPoint = clock.Advance(Origin + 333_333);
        var next = clock.Advance(Origin + 333_334);

        Assert.Multiple(() =>
        {
            Assert.That(arranged.Ticks, Is.EqualTo(1), "the arranged advance");
            Assert.That(backwards.Ticks, Is.EqualTo(0));
            Assert.That(nextDue, Is.EqualTo(Origin + 333_334), "NextDueTimestamp after the backwards call");
            Assert.That(beforeTheGridPoint.Ticks, Is.EqualTo(0), "one unit before the second grid point");
            Assert.That(next.Ticks, Is.EqualTo(1));
        });
    }

    [Test]
    public void Advance_BacklogAboveCap_RunsCapTicksAndReportsTheRestAsDropped()
    {
        var clock = CreateStarted();

        var advance = clock.Advance(Origin + 10_000_000);

        Assert.Multiple(() =>
        {
            Assert.That(advance, Is.EqualTo(new TickAdvance(15, 45, false)));
            Assert.That(clock.TicksIssued, Is.EqualTo(15L));
            Assert.That(clock.TicksDropped, Is.EqualTo(45L));
        });
    }

    [Test]
    public void Advance_BacklogExactlyAtCap_DropsNothing()
    {
        var clock = CreateStarted();

        var advance = clock.Advance(Origin + 2_500_000);

        Assert.That(advance, Is.EqualTo(new TickAdvance(15, 0, false)));
    }

    [Test]
    public void Advance_AfterADrop_KeepsTheGrid()
    {
        var clock = CreateStarted();

        // 1.01 s: 60 ticks due and a sub-tick phase of 0.6 of a period, which the drop must keep (a lost phase would
        // put the next tick at Origin + 10_266_667).
        var advance = clock.Advance(Origin + 10_100_000);

        Assert.Multiple(() =>
        {
            Assert.That(advance, Is.EqualTo(new TickAdvance(15, 45, false)));
            Assert.That(clock.NextDueTimestamp, Is.EqualTo(Origin + 10_166_667));
        });
    }

    [Test]
    public void Advance_PausedForOneSecond_ReturnsNoTickAndOnePausedFrame()
    {
        var clock = CreateStarted();
        clock.Pause();

        var advance = clock.Advance(Origin + 10_000_000);

        Assert.Multiple(() =>
        {
            Assert.That(advance, Is.EqualTo(new TickAdvance(0, 0, true)));
            Assert.That(clock.TicksIssued, Is.EqualTo(0L));
            Assert.That(clock.TicksDropped, Is.EqualTo(0L));
        });
    }

    [Test]
    public void Advance_PausedWithinOnePeriod_ReturnsNoPausedFrame()
    {
        var clock = CreateStarted();
        clock.Pause();

        var advance = clock.Advance(Origin + 100_000);

        Assert.That(advance, Is.EqualTo(new TickAdvance(0, 0, false)));
    }

    [Test]
    public void Resume_AfterALongPause_NextAdvanceRunsOneTickNotABurst()
    {
        var clock = CreateStarted();
        clock.Pause();
        clock.Advance(Origin + 100_000_000);
        clock.Resume();

        var advance = clock.Advance(Origin + 100_166_667);

        Assert.That(advance, Is.EqualTo(new TickAdvance(1, 0, false)));
    }

    [Test]
    public void Step_WhilePaused_NextAdvanceReturnsOneTickAtOnce()
    {
        var clock = CreateStarted();
        clock.Pause();
        clock.Advance(Origin + 10);

        var stepped = clock.Step();
        var first = clock.Advance(Origin + 11);
        var second = clock.Advance(Origin + 12);

        Assert.Multiple(() =>
        {
            Assert.That(stepped, Is.True);
            Assert.That(first, Is.EqualTo(new TickAdvance(1, 0, false)));
            Assert.That(second, Is.EqualTo(new TickAdvance(0, 0, false)));
        });
    }

    [Test]
    public void Step_ThreeTimesWhilePaused_NextAdvanceReturnsThreeTicks()
    {
        var clock = CreateStarted();
        clock.Pause();

        clock.Step();
        clock.Step();
        clock.Step();
        var advance = clock.Advance(Origin + 1);

        Assert.Multiple(() =>
        {
            Assert.That(advance.Ticks, Is.EqualTo(3));
            Assert.That(clock.PendingSteps, Is.EqualTo(0));
        });
    }

    [Test]
    public void Step_WhileRunning_ReturnsFalseAndAddsNoTick()
    {
        var clock = CreateStarted();

        var stepped = clock.Step();
        var advance = clock.Advance(Origin + 1);

        Assert.Multiple(() =>
        {
            Assert.That(stepped, Is.False);
            Assert.That(advance.Ticks, Is.EqualTo(0));
        });
    }

    [Test]
    public void NextDueTimestamp_StepPending_IsTheLastTimestamp()
    {
        var clock = CreateStarted();
        clock.Pause();
        clock.Advance(Origin + 50);
        clock.Step();

        Assert.That(clock.NextDueTimestamp, Is.EqualTo(1_050L));
    }

    [Test]
    public void Start_AgainWithABacklog_DiscardsItAndStartsANewGrid()
    {
        var clock = CreateStarted();

        clock.Start(5_000_000);
        var advance = clock.Advance(5_000_000 + 166_667);

        Assert.That(advance.Ticks, Is.EqualTo(1));
    }
}
