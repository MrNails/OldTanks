using System.Diagnostics;
using System.Runtime.CompilerServices;
using CoolEngine.Core.Channels;

namespace CoolEngine.Core.Tests.Channels;

[TestFixture]
public sealed class CommandQueueTests
{
    private static readonly TimeSpan s_hangDeadline = TimeSpan.FromSeconds(10);

    private struct TestCommand
    {
        public int Producer;
        public int Index;
        public object? Payload;

        public TestCommand(int producer, int index, object? payload = null)
        {
            Producer = producer;
            Index = index;
            Payload = payload;
        }
    }

    [Test]
    public void Enqueue_First_ReturnsSequenceOne()
    {
        var queue = new CommandQueue<TestCommand>(4);

        var sequence = queue.Enqueue(new TestCommand(0, 0));

        Assert.That(sequence, Is.EqualTo(1L));
    }

    [Test]
    public void TakePending_NothingEnqueued_ReturnsAnEmptyBatchAfterSequenceZero()
    {
        var queue = new CommandQueue<TestCommand>(4);

        var batch = queue.TakePending();

        var (count, first, last) = (batch.Count, batch.FirstSequence, batch.LastSequence);
        Assert.Multiple(() =>
        {
            Assert.That(count, Is.EqualTo(0));
            Assert.That(first, Is.EqualTo(1L));
            Assert.That(last, Is.EqualTo(0L));
        });
    }

    [Test]
    public void TakePending_ThreeEnqueued_ReturnsThemInOrderWithSequencesOneToThree()
    {
        var queue = new CommandQueue<TestCommand>(4);
        for (var i = 0; i < 3; i++)
            queue.Enqueue(new TestCommand(0, i));

        var batch = queue.TakePending();

        var indices = Indices(batch);
        var (first, last) = (batch.FirstSequence, batch.LastSequence);
        Assert.Multiple(() =>
        {
            Assert.That(indices, Is.EqualTo(new[] { 0, 1, 2 }));
            Assert.That(first, Is.EqualTo(1L));
            Assert.That(last, Is.EqualTo(3L));
        });
    }

    [Test]
    public void TakePending_Twice_SecondBatchStartsAfterTheFirst()
    {
        var queue = new CommandQueue<TestCommand>(4);
        for (var i = 0; i < 3; i++)
            queue.Enqueue(new TestCommand(0, i));
        queue.TakePending();
        for (var i = 3; i < 5; i++)
            queue.Enqueue(new TestCommand(0, i));

        var batch = queue.TakePending();

        var (first, last) = (batch.FirstSequence, batch.LastSequence);
        Assert.Multiple(() =>
        {
            Assert.That(first, Is.EqualTo(4L));
            Assert.That(last, Is.EqualTo(5L));
            Assert.That(queue.LastTakenSequence, Is.EqualTo(5L));
        });
    }

    [Test]
    public void Enqueue_WhileABatchIsRead_LeavesTheBatchUnchanged()
    {
        var queue = new CommandQueue<TestCommand>(4);
        for (var i = 0; i < 3; i++)
            queue.Enqueue(new TestCommand(0, i));
        var firstBatch = queue.TakePending();

        for (var i = 3; i < 8; i++)
            queue.Enqueue(new TestCommand(0, i));

        var firstIndices = Indices(firstBatch);
        var nextIndices = Indices(queue.TakePending());
        Assert.Multiple(() =>
        {
            Assert.That(firstIndices, Is.EqualTo(new[] { 0, 1, 2 }));
            Assert.That(nextIndices, Is.EqualTo(new[] { 3, 4, 5, 6, 7 }));
        });
    }

    [Test]
    public void Enqueue_TenThousandBeforeOneTake_KeepsAllInOrder()
    {
        var queue = new CommandQueue<TestCommand>(4);
        for (var i = 0; i < 10_000; i++)
            queue.Enqueue(new TestCommand(0, i));

        var batch = queue.TakePending();

        var indices = Indices(batch);
        var last = batch.LastSequence;
        Assert.Multiple(() =>
        {
            Assert.That(indices, Is.EqualTo(Enumerable.Range(0, 10_000)));
            Assert.That(last, Is.EqualTo(10_000L));
        });
    }

    [Test]
    public void TakePending_FourProducersContending_EveryCommandOnceInEachProducersOrderWithContiguousSequences()
    {
        const int producerCount = 4;
        const int perProducer = 100_000;
        const int total = producerCount * perProducer;

        var queue = new CommandQueue<TestCommand>(4);
        var recordedByProducer = new long[producerCount][];
        var seenByConsumer = new long[producerCount][];
        for (var p = 0; p < producerCount; p++)
        {
            recordedByProducer[p] = new long[perProducer];
            seenByConsumer[p] = new long[perProducer];
        }

        using var barrier = new Barrier(producerCount);
        var producers = Enumerable.Range(0, producerCount)
            .Select(p => new Thread(() =>
            {
                barrier.SignalAndWait();
                var recorded = recordedByProducer[p];
                for (var i = 0; i < perProducer; i++)
                    recorded[i] = queue.Enqueue(new TestCommand(p, i));
            }))
            .ToArray();
        foreach (var producer in producers)
            producer.Start();

        var nextIndex = new int[producerCount];
        var arrived = 0;
        var previousLast = 0L;
        var orderErrors = 0;
        var rangeErrors = 0;
        var stopwatch = Stopwatch.StartNew();
        while (arrived < total)
        {
            var batch = queue.TakePending();

            if (batch.FirstSequence != previousLast + 1 || batch.LastSequence - batch.FirstSequence + 1 != batch.Count)
                rangeErrors++;
            previousLast = batch.LastSequence;

            var commands = batch.Commands;
            for (var i = 0; i < commands.Length; i++)
            {
                var command = commands[i];
                if (command.Index != nextIndex[command.Producer])
                    orderErrors++;
                nextIndex[command.Producer] = command.Index + 1;
                seenByConsumer[command.Producer][command.Index] = batch.FirstSequence + i;
            }

            arrived += commands.Length;

            if (stopwatch.Elapsed > s_hangDeadline)
                Assert.Fail($"The commands did not all arrive within {s_hangDeadline.TotalSeconds} s.");
        }

        foreach (var producer in producers)
            Assert.That(producer.Join(s_hangDeadline), Is.True, "producer joined");

        var sequenceMismatches = 0;
        for (var p = 0; p < producerCount; p++)
        for (var i = 0; i < perProducer; i++)
        {
            if (seenByConsumer[p][i] != recordedByProducer[p][i])
                sequenceMismatches++;
        }

        Assert.Multiple(() =>
        {
            Assert.That(arrived, Is.EqualTo(total), "arrived");
            Assert.That(orderErrors, Is.EqualTo(0), "per-producer order errors");
            Assert.That(rangeErrors, Is.EqualTo(0), "non-contiguous batches");
            Assert.That(sequenceMismatches, Is.EqualTo(0), "FirstSequence + i against the sequence Enqueue returned");
            Assert.That(nextIndex, Is.All.EqualTo(perProducer), "every producer's last index");
            Assert.That(queue.LastTakenSequence, Is.EqualTo((long)total), "last taken sequence");
        });
    }

    [Test]
    public void TakePending_CommandHoldsAReference_ReleasedByTheNextTake()
    {
        var queue = new CommandQueue<TestCommand>(4);
        var weak = EnqueueUnreachablePayload(queue);

        queue.TakePending();
        queue.TakePending();
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        Assert.That(weak.IsAlive, Is.False);
    }

    [Test]
    public void EnqueueAndTake_SteadyState_AllocatesNothing()
    {
        const int rounds = 10_000;
        const int blocks = 3;

        var queue = new CommandQueue<TestCommand>(64);
        var allocated = new long[blocks];

        // Warm up once, so the JIT compiles every path before the measurement.
        var checksum = RunRounds(queue, 1);

        // Three blocks of the same size: a per-call allocation shows in every block, a one-off runtime allocation on
        // this thread in one at most, so the smallest block is the measurement.
        for (var block = 0; block < blocks; block++)
        {
            var before = GC.GetAllocatedBytesForCurrentThread();
            checksum += RunRounds(queue, rounds);
            allocated[block] = GC.GetAllocatedBytesForCurrentThread() - before;
        }

        Assert.Multiple(() =>
        {
            Assert.That(allocated.Min(), Is.EqualTo(0L), $"allocated bytes, smallest of the blocks {string.Join(", ", allocated)}");
            Assert.That(checksum, Is.EqualTo((blocks * rounds + 1L) * 8), "commands taken (keeps the results alive)");
        });
    }

    [Test]
    public void LastTakenSequence_BeforeAnyTake_IsZero()
    {
        var queue = new CommandQueue<TestCommand>(4);
        queue.Enqueue(new TestCommand(0, 0));
        queue.Enqueue(new TestCommand(0, 1));

        Assert.That(queue.LastTakenSequence, Is.EqualTo(0L));
    }

    private static long RunRounds(CommandQueue<TestCommand> queue, int rounds)
    {
        var taken = 0L;
        for (var round = 0; round < rounds; round++)
        {
            for (var i = 0; i < 8; i++)
                queue.Enqueue(new TestCommand(0, i));

            taken += queue.TakePending().Count;
        }

        return taken;
    }

    // Not inlined, so the payload is reachable only through the queue once it returns.
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static WeakReference EnqueueUnreachablePayload(CommandQueue<TestCommand> queue)
    {
        var payload = new object();
        queue.Enqueue(new TestCommand(0, 0, payload));
        return new WeakReference(payload);
    }

    private static int[] Indices(CommandBatch<TestCommand> batch)
    {
        var indices = new int[batch.Count];
        for (var i = 0; i < indices.Length; i++)
            indices[i] = batch.Commands[i].Index;
        return indices;
    }
}
