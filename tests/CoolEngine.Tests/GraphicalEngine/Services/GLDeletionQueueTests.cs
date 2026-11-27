using System.Runtime.CompilerServices;
using CoolEngine.GraphicalEngine.Core;
using CoolEngine.GraphicalEngine.Services;

namespace CoolEngine.Tests.GraphicalEngine.Services;

// The queue that carries GL names from finalizers to the GL thread (KI-017). Headless: the deleter is injected
// through Drain, so no test makes a GL call; the test thread stands in for the GL thread.
[TestFixture]
[NonParallelizable]
public sealed class GLDeletionQueueTests
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
    public void Enqueue_NameZero_QueuesNothing()
    {
        var calls = 0;

        GLDeletionQueue.Enqueue(GLObjectKind.Texture, 0);
        var drained = GLDeletionQueue.Drain((_, _) => calls++);

        Assert.Multiple(() =>
        {
            Assert.That(drained, Is.EqualTo(0));
            Assert.That(calls, Is.EqualTo(0));
            Assert.That(GLDeletionQueue.PendingCount, Is.EqualTo(0));
        });
    }

    [Test]
    public void Drain_QueuedNames_HandsEachToTheDeleterOnceInOrder()
    {
        GLDeletionQueue.Enqueue(GLObjectKind.Texture, 900101);
        GLDeletionQueue.Enqueue(GLObjectKind.Buffer, 900102);
        GLDeletionQueue.Enqueue(GLObjectKind.VertexArray, 900103);
        GLDeletionQueue.Enqueue(GLObjectKind.Program, 900104);
        var first = new List<(GLObjectKind Kind, int Name)>();
        var second = new List<(GLObjectKind Kind, int Name)>();

        var firstCount = GLDeletionQueue.Drain((kind, name) => first.Add((kind, name)));
        var secondCount = GLDeletionQueue.Drain((kind, name) => second.Add((kind, name)));

        Assert.Multiple(() =>
        {
            Assert.That(firstCount, Is.EqualTo(4));
            Assert.That(first, Is.EqualTo(new[]
            {
                (GLObjectKind.Texture, 900101),
                (GLObjectKind.Buffer, 900102),
                (GLObjectKind.VertexArray, 900103),
                (GLObjectKind.Program, 900104)
            }));
            Assert.That(secondCount, Is.EqualTo(0));
            Assert.That(second, Is.Empty);
        });
    }

    [Test]
    public void Drain_EmptyQueue_ReturnsZeroAndAllocatesNothing()
    {
        Action<GLObjectKind, int> noop = static (_, _) => { };
        GLDeletionQueue.Drain(noop);
        var nonZeroResults = 0;

        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < 1000; i++)
        {
            if (GLDeletionQueue.Drain(noop) != 0)
                nonZeroResults++;
        }
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        Assert.Multiple(() =>
        {
            Assert.That(nonZeroResults, Is.EqualTo(0));
            Assert.That(allocated, Is.EqualTo(0));
        });
    }

    [Test]
    public void Enqueue_EightThreadsConcurrently_DrainReturnsEveryNameExactlyOnce()
    {
        const int threadCount = 8;
        const int namesPerThread = 10_000;
        var barrier = new Barrier(threadCount);
        var producers = new Thread[threadCount];
        for (var t = 0; t < threadCount; t++)
        {
            var first = 1_000_000 + t * 10_000;
            producers[t] = new Thread(() =>
            {
                barrier.SignalAndWait();
                for (var i = 1; i <= namesPerThread; i++)
                    GLDeletionQueue.Enqueue(GLObjectKind.Buffer, first + i);
            });
        }

        var recorded = new List<(GLObjectKind Kind, int Name)>();
        Action<GLObjectKind, int> record = (kind, name) => recorded.Add((kind, name));
        foreach (var producer in producers)
            producer.Start();
        while (producers.Any(p => p.IsAlive))
            GLDeletionQueue.Drain(record);
        foreach (var producer in producers)
            producer.Join();
        while (GLDeletionQueue.Drain(record) != 0)
        {
        }

        var expected = Enumerable.Range(0, threadCount)
            .SelectMany(t => Enumerable.Range(1_000_000 + t * 10_000 + 1, namesPerThread))
            .Select(name => (GLObjectKind.Buffer, name));
        Assert.Multiple(() =>
        {
            Assert.That(recorded, Has.Count.EqualTo(threadCount * namesPerThread));
            Assert.That(recorded, Is.Unique);
            Assert.That(recorded.ToHashSet().SetEquals(expected), Is.True);
        });
    }

    [Test]
    public void Drain_WhileTheFinalizerThreadQueues_LosesNothing()
    {
        const int infoCount = 1000;
        var producer = new Thread(() =>
        {
            for (var k = 0; k < infoCount; k++)
            {
                NewGarbageDrawObjectInfo(k);
                if ((k + 1) % 100 == 0)
                    Collect();
            }
        });

        var recorded = new List<(GLObjectKind Kind, int Name)>();
        Action<GLObjectKind, int> record = (kind, name) => recorded.Add((kind, name));
        producer.Start();
        while (producer.IsAlive)
            GLDeletionQueue.Drain(record);
        producer.Join();
        Collect();
        GLDeletionQueue.Drain(record);

        var inRange = recorded.Where(e => e.Name is >= 2_000_000 and < 2_000_000 + 2 * infoCount).ToList();
        Assert.Multiple(() =>
        {
            Assert.That(inRange, Has.Count.EqualTo(2 * infoCount));
            Assert.That(inRange.Select(e => e.Name), Is.Unique);
            Assert.That(inRange.Count(e => e.Kind == GLObjectKind.VertexArray), Is.EqualTo(infoCount));
            Assert.That(inRange.Count(e => e.Kind == GLObjectKind.Buffer), Is.EqualTo(infoCount));
            Assert.That(inRange.Where(e => e.Kind == GLObjectKind.VertexArray).All(e => e.Name % 2 == 0), Is.True);
            Assert.That(recorded, Has.Count.EqualTo(2 * infoCount));
        });
    }

    [Test]
    public void Drain_DeleterQueuesAnotherName_ItIsDeletedOnceInThisOrTheNextDrain()
    {
        GLDeletionQueue.Enqueue(GLObjectKind.Texture, 900201);
        var recorded = new List<(GLObjectKind Kind, int Name)>();
        var queuedAnother = false;
        Action<GLObjectKind, int> deleter = (kind, name) =>
        {
            recorded.Add((kind, name));
            if (queuedAnother)
                return;

            queuedAnother = true;
            GLDeletionQueue.Enqueue(GLObjectKind.Texture, 900202);
        };

        GLDeletionQueue.Drain(deleter);
        GLDeletionQueue.Drain(deleter);

        Assert.Multiple(() =>
        {
            Assert.That(recorded.Count(e => e == (GLObjectKind.Texture, 900201)), Is.EqualTo(1));
            Assert.That(recorded.Count(e => e == (GLObjectKind.Texture, 900202)), Is.EqualTo(1));
            Assert.That(GLDeletionQueue.PendingCount, Is.EqualTo(0));
        });
    }

    [Test]
    public void Drain_AfterTheFinalDrain_NamesQueuedLaterStayQueued()
    {
        GLDeletionQueue.Enqueue(GLObjectKind.Texture, 900301);
        var recorded = new List<(GLObjectKind Kind, int Name)>();
        GLDeletionQueue.Drain((kind, name) => recorded.Add((kind, name)));

        GLDeletionQueue.Enqueue(GLObjectKind.Texture, 900302);

        Assert.Multiple(() =>
        {
            Assert.That(recorded, Is.EqualTo(new[] { (GLObjectKind.Texture, 900301) }));
            Assert.That(GLDeletionQueue.PendingCount, Is.EqualTo(1));
        });
    }

    private static void Collect()
    {
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
    }

    // Not inlined, so the info is unreachable once it returns: a VAO and a VBO, no element buffer.
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static int NewGarbageDrawObjectInfo(int k)
    {
        var info = new DrawObjectInfo(2_000_000 + 2 * k, 2_000_000 + 2 * k + 1, 0);

        return info.VertexArrayObject;
    }
}
