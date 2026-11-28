using System.Runtime.Versioning;
using CoolEngine.Core.Tests.TestSupport;
using CoolEngine.Core.Timing;

namespace CoolEngine.Core.Tests.Timing;

[TestFixture]
public sealed class TickWaiterTests
{
    private static readonly TimeSpan s_hangDeadline = TimeSpan.FromSeconds(10);

    [Test]
    public void SleepTickWaiter_TimestampAlreadyReached_ReturnsAtOnce()
    {
        using var waiter = new SleepTickWaiter(new ManualTimestampSource(10_000_000, 100));

        AssertWaitReturns(waiter, 50, wake: false);
    }

    [Test]
    public void SleepTickWaiter_WakeWhileWaiting_Returns()
    {
        using var waiter = new SleepTickWaiter(new ManualTimestampSource(10_000_000));

        AssertWaitReturns(waiter, long.MaxValue, wake: true);
    }

    [Test]
    public void SleepTickWaiter_FrequencyBelowOne_ThrowsArgumentOutOfRange()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new SleepTickWaiter(new ManualTimestampSource(0)).Dispose());
    }

    [Test]
    [Platform("Win")]
    [SupportedOSPlatform("windows")]
    public void HighResolutionTickWaiter_FrequencyBelowOne_ThrowsArgumentOutOfRange()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new HighResolutionTickWaiter(new ManualTimestampSource(0)).Dispose());
    }

    [Test]
    [Platform("Win")]
    [SupportedOSPlatform("windows")]
    public void HighResolutionTickWaiter_TimestampAlreadyReached_ReturnsAtOnce()
    {
        using var waiter = new HighResolutionTickWaiter(new ManualTimestampSource(10_000_000, 100));

        AssertWaitReturns(waiter, 50, wake: false);
    }

    [Test]
    [Platform("Win")]
    [SupportedOSPlatform("windows")]
    public void HighResolutionTickWaiter_WakeWhileWaiting_Returns()
    {
        using var waiter = new HighResolutionTickWaiter(new ManualTimestampSource(10_000_000));

        AssertWaitReturns(waiter, long.MaxValue, wake: true);
    }

    // Runs WaitUntil on its own thread and joins it with the hang guard. A wake that comes before the thread waits is
    // remembered by the waiter's contract, so the order of the two threads does not matter.
    private static void AssertWaitReturns(ITickWaiter waiter, long timestamp, bool wake)
    {
        var thread = new Thread(() => waiter.WaitUntil(timestamp)) { IsBackground = true };
        thread.Start();

        if (wake)
            waiter.Wake();

        Assert.That(thread.Join(s_hangDeadline), Is.True, "the waiting thread returned");
    }
}
