using System.Diagnostics;
using CoolEngine.Core.Channels;

namespace CoolEngine.Core.Tests.Channels;

[TestFixture]
public sealed class SnapshotBufferTests
{
    private static readonly TimeSpan s_hangDeadline = TimeSpan.FromSeconds(10);

    private sealed class TestSnapshot
    {
        public long Tick;
        public readonly long[] Data = new long[64];
        public long Checksum;
    }

    [Test]
    public void Constructor_FactoryReturnsNull_ThrowsInvalidOperation()
    {
        Assert.Throws<InvalidOperationException>(() => _ = new SnapshotBuffer<TestSnapshot>(() => null!));
    }

    [Test]
    public void Constructor_FactoryReturnsOneInstanceTwice_ThrowsInvalidOperation()
    {
        var cached = new TestSnapshot();

        Assert.Throws<InvalidOperationException>(() => _ = new SnapshotBuffer<TestSnapshot>(() => cached));
    }

    [Test]
    public void Constructor_CallsTheFactoryExactlyThreeTimes()
    {
        var calls = 0;

        _ = new SnapshotBuffer<TestSnapshot>(() =>
        {
            calls++;
            return new TestSnapshot();
        });

        Assert.That(calls, Is.EqualTo(3));
    }

    [Test]
    public void Acquire_BeforeAnyPublish_ReturnsAnInstanceOtherThanTheWriteTarget()
    {
        var buffer = new SnapshotBuffer<TestSnapshot>(() => new TestSnapshot());

        var acquired = buffer.Acquire();

        Assert.That(acquired, Is.Not.SameAs(buffer.WriteTarget));
    }

    [Test]
    public void Publish_ThenAcquire_ReturnsThePublishedInstance()
    {
        var buffer = new SnapshotBuffer<TestSnapshot>(() => new TestSnapshot());
        var published = buffer.WriteTarget;
        published.Tick = 1;

        buffer.Publish();
        var acquired = buffer.Acquire();

        Assert.Multiple(() =>
        {
            Assert.That(acquired, Is.SameAs(published));
            Assert.That(acquired.Tick, Is.EqualTo(1L));
            Assert.That(buffer.WriteTarget, Is.Not.SameAs(published));
        });
    }

    [Test]
    public void Acquire_TwiceWithoutPublish_ReturnsTheSameInstance()
    {
        var buffer = new SnapshotBuffer<TestSnapshot>(() => new TestSnapshot());
        buffer.WriteTarget.Tick = 1;
        buffer.Publish();

        var first = buffer.Acquire();
        var second = buffer.Acquire();

        Assert.That(second, Is.SameAs(first));
    }

    [Test]
    public void Publish_ThreeTimesThenAcquire_ReturnsTheNewest()
    {
        var buffer = new SnapshotBuffer<TestSnapshot>(() => new TestSnapshot());
        for (var tick = 1; tick <= 3; tick++)
        {
            buffer.WriteTarget.Tick = tick;
            buffer.Publish();
        }

        var acquired = buffer.Acquire();

        Assert.That(acquired.Tick, Is.EqualTo(3L));
    }

    [Test]
    public void PublishAndAcquire_RandomInterleaving_WriteTargetIsNeverTheHeldInstance()
    {
        var instances = new List<TestSnapshot>();
        var buffer = new SnapshotBuffer<TestSnapshot>(() =>
        {
            var snapshot = new TestSnapshot();
            instances.Add(snapshot);
            return snapshot;
        });
        var random = new Random(20261127);
        var held = buffer.Acquire();
        var lastAcquiredTick = held.Tick;
        var nextTick = 1L;
        var heldIsWriteTarget = 0;
        var notDistinct = 0;
        var backwards = 0;

        for (var step = 0; step < 10_000; step++)
        {
            if (random.Next(2) == 0)
            {
                buffer.WriteTarget.Tick = nextTick++;
                buffer.Publish();
            }
            else
            {
                held = buffer.Acquire();
                if (held.Tick < lastAcquiredTick)
                    backwards++;
                lastAcquiredTick = held.Tick;
            }

            if (ReferenceEquals(buffer.WriteTarget, held))
                heldIsWriteTarget++;
            if (!instances.Contains(buffer.WriteTarget) || !instances.Contains(held))
                notDistinct++;
        }

        Assert.Multiple(() =>
        {
            Assert.That(heldIsWriteTarget, Is.EqualTo(0), "steps where WriteTarget was the held instance");
            Assert.That(notDistinct, Is.EqualTo(0), "steps where an instance was not one of the factory's");
            Assert.That(instances.Distinct().Count(), Is.EqualTo(3), "distinct factory instances");
            Assert.That(backwards, Is.EqualTo(0), "acquired ticks that went backwards");
        });
    }

    [Test]
    public void PublishAndAcquire_WriterRacingReader_NoTornSnapshotAndTicksNeverGoBackwards()
    {
        const long publishes = 1_000_000;

        var buffer = new SnapshotBuffer<TestSnapshot>(() => new TestSnapshot());
        var writer = new Thread(() =>
        {
            for (var tick = 1L; tick <= publishes; tick++)
            {
                var target = buffer.WriteTarget;
                var sum = 0L;
                for (var i = 0; i < target.Data.Length; i++)
                {
                    var value = tick * 31 + i;
                    target.Data[i] = value;
                    sum += value;
                }

                target.Tick = tick;
                target.Checksum = sum;
                buffer.Publish();
            }
        });
        writer.Start();

        var torn = 0;
        var backwards = 0;
        var lastTick = 0L;
        var stopwatch = Stopwatch.StartNew();
        while (lastTick < publishes)
        {
            var snapshot = buffer.Acquire();
            var sum = 0L;
            for (var i = 0; i < snapshot.Data.Length; i++)
                sum += snapshot.Data[i];

            if (sum != snapshot.Checksum || snapshot.Data[0] != snapshot.Tick * 31)
                torn++;
            if (snapshot.Tick < lastTick)
                backwards++;
            lastTick = snapshot.Tick;

            if (stopwatch.Elapsed > s_hangDeadline)
                Assert.Fail($"The reader did not see the last publish within {s_hangDeadline.TotalSeconds} s.");
        }

        Assert.Multiple(() =>
        {
            Assert.That(writer.Join(s_hangDeadline), Is.True, "writer joined");
            Assert.That(torn, Is.EqualTo(0), "torn snapshots");
            Assert.That(backwards, Is.EqualTo(0), "ticks that went backwards");
        });
    }

    [Test]
    public void PublishAndAcquire_SteadyState_AllocatesNothing()
    {
        const int rounds = 10_000;
        const int blocks = 3;

        var buffer = new SnapshotBuffer<TestSnapshot>(() => new TestSnapshot());
        var allocated = new long[blocks];

        // Warm up once, so the JIT compiles every path before the measurement.
        var checksum = RunRounds(buffer, 1, 0);

        // Three blocks of the same size: a per-call allocation shows in every block, a one-off runtime allocation on
        // this thread in one at most, so the smallest block is the measurement.
        for (var block = 0; block < blocks; block++)
        {
            var before = GC.GetAllocatedBytesForCurrentThread();
            checksum += RunRounds(buffer, rounds, 1 + block * rounds);
            allocated[block] = GC.GetAllocatedBytesForCurrentThread() - before;
        }

        Assert.Multiple(() =>
        {
            Assert.That(allocated.Min(), Is.EqualTo(0L), $"allocated bytes, smallest of the blocks {string.Join(", ", allocated)}");
            Assert.That(checksum, Is.EqualTo(Enumerable.Range(1, blocks * rounds).Sum(i => (long)i)), "ticks acquired (keeps the results alive)");
        });
    }

    private static long RunRounds(SnapshotBuffer<TestSnapshot> buffer, int rounds, long firstTick)
    {
        var checksum = 0L;
        for (var round = 0; round < rounds; round++)
        {
            buffer.WriteTarget.Tick = firstTick + round;
            buffer.Publish();
            checksum += buffer.Acquire().Tick;
        }

        return checksum;
    }
}
