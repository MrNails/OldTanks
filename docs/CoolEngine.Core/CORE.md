# CoolEngine.Core

| Field        | Value  |
|--------------|--------|
| Status       | Living |
| Last updated | 2026-11-28 |
| Verified against | 2026-11-28; feature/OT-20 at 9212d68 (whole page against the library's code and its 88 tests; the pacing table is the analyst's probe of 2026-11-27, not rerun; the memory-model arguments are reasoned from code and the reviewer's reading, tested by repeated runs, not proved by a tool) |

---

`CoolEngine.Core` (`CoolEngine.Core/`) is the shared machinery between the simulation thread and the GL thread: a fixed-tick clock, a runner and a thread host around it, a command queue (commands in) and a snapshot buffer (snapshots out). Added by OT-20 as WP3 of the [split plan](../plans/SIMULATION-AND-PHYSICS-SPLIT.md); **nothing in the game uses it yet** (the switch is OT-21). No project reference, no package, no static state, no logger. Guide: [CoolEngine.Core/AGENTS.md](../../CoolEngine.Core/AGENTS.md).

## 1. Parts

| Namespace | Types | Thread rule |
|-----------|-------|-------------|
| `CoolEngine.Core.Timing` | `FixedTickClock`, `TickAdvance`, `ITimestampSource`, `StopwatchTimestampSource`, `ITickWaiter`, `SleepTickWaiter`, `HighResolutionTickWaiter` | The clock: one thread. Sources: any thread. Waiter: `WaitUntil` on the loop thread, `Wake` from any |
| `CoolEngine.Core.Channels` | `CommandQueue<T>`, `CommandBatch<T>`, `SnapshotBuffer<T>` | Queue: any producer, one consumer. Buffer: one writer, one reader |
| `CoolEngine.Core.Hosting` | `ITickHandler`, `FixedTickRunner`, `FixedTickThread`, `TickFaultEventArgs`, `TicksDroppedEventArgs` | Runner: `Start`, `RunDue` on the loop thread; `Pause`, `Resume`, `Step`, `IsPaused` from any. Host: every member from any thread, `Stop` and `Dispose` not from the loop thread |

15 public types. The clock's rate and cap come from the host as constructor arguments (`ticksPerSecond`, `timestampFrequency`, `maxBacklogTicks`); nothing here knows 60 or 15 (Denys's 60 Hz and 0.25 s cap, 15 ticks, are the game's choice).

## 2. The clock

`FixedTickClock` is pure: no thread, no sleep, no time source, integer timestamps only. It answers "how many ticks are due now".

| Rule | Detail |
|------|--------|
| **Exact grid** | Tick `k` after `Start` at `t0` is due at exactly `t0 + ceil(k · F / R)` (`F` the timestamp frequency, `R` ticks per second). The sub-tick phase is kept as an integer accumulator in units of timestamp × ticks, always in `[0, F)`, so rounding never drifts |
| **Overflow guard** | The constructor rejects `F · R > long.MaxValue / 2`; the elapsed time is split into whole seconds and a remainder, and a huge gap saturates at `long.MaxValue` ticks instead of wrapping |
| **Backlog cap** | `Advance` returns at most `MaxBacklogTicks` ticks; the rest are reported in `TickAdvance.DroppedTicks` and gone for good. The phase is kept, so the grid does not move after a drop (a lost phase at the cap was a reviewer's mutant that the first tests missed; now tested with a 10,100,000-unit advance) |
| **Pause** | Paused, the elapsed time is discarded. `Advance` returns the pending steps, or one **paused frame** (`PausedFrame` true, 0 ticks) when none is pending and at least one period has passed |
| **Step** | While paused, each `Step()` adds one pending tick, uncapped; requests add up. While running it returns false and changes nothing |
| **Resume** | Clears pending steps; the first tick comes at the next grid point (no burst), provided the host kept calling `RunDue` while paused: an inline host that stops calling gets a capped burst |
| **Backwards time** | A timestamp that goes backwards is ignored and the grid stays put |
| **`NextDueTimestamp`** | The last timestamp while a step is pending, else the next grid point: what a waiter sleeps to |

## 3. The command queue

`CommandQueue<T>` (`T : struct`) is a locked double buffer. `Enqueue` copies the command into the pending array and returns its sequence (1, 2, ...) inside one critical section; `TakePending` swaps the two arrays inside the lock and the consumer reads its batch without it.

- **Why the acknowledgement is exact:** the sequence and the position are assigned in the same critical section, so queue order equals sequence order and every batch is a contiguous range starting after the previous one. A consumer that reports `LastSequence = N` as applied has applied every command with a sequence of at most N. The sequence is not stored in the command: command `i` of a batch has `FirstSequence + i`.
- **Why a lock and not a lock-free queue:** measured on 2026-11-27 (analyst's probe, 24-byte command, 4 producers): with a `ConcurrentQueue` and an interlocked sequence, 43,838 and 56,068 of 1,000,000 commands arrived after a higher sequence had been acknowledged; the locked buffer had 0 and was the cheaper under contention (46 to 89 ns per command against 137 to 139 ns). One enqueue and take is about 26 ns, 0 B.
- **Unbounded:** the pending array doubles on a burst and is kept; no producer ever waits for space.
- **References:** a command holding references is cleared after use, so the queue keeps none alive.
- **Lost commands:** if the handler throws on one command of a batch, the rest of that batch is lost (the batch is consumed); the next snapshot's last applied sequence acknowledges them anyway, and the fault's `Error` line is the record.

## 4. The snapshot buffer

`SnapshotBuffer<T>` (`T : class`) is a triple buffer of three instances made by a factory: the writer fills `WriteTarget` and calls `Publish`; the reader calls `Acquire` and gets the newest published instance. Both are one atomic exchange, wait-free, 0 B.

- **Permutation:** {back, middle, front} is always a permutation of {0, 1, 2}; each side changes only its own index and swaps it with the middle atomically, so the writer never writes the instance the reader holds.
- **No torn snapshot (memory-model argument, reasoned from code):** the writer's stores into its instance precede its `Interlocked.Exchange`, a full fence; the reader reaches that instance only through its own exchange, which read the writer's value; the writer touches the instance again only after the reader handed it back through another exchange.
- **Never older than seen:** the "fresh" bit is set only by a publish and cleared only by the reader's exchange, so the instances the reader gets are ordered by publish time.
- **Stale contents:** `WriteTarget` holds what was published two publishes earlier; the writer must rewrite every field it publishes.
- **Measured 2026-11-27 (prototype of this scheme, analyst):** 2,000,000 publishes of 64 values against a spinning reader, two runs: 0 torn, 0 backwards; publish plus acquire on one thread about 11 ns.

## 5. The runner, pause, step and faults

`FixedTickRunner` joins a clock and an `ITickHandler` (`Tick(tickNumber)` from 1; `PausedFrame(lastTickNumber)`). `RunDue(timestamp)` is one inline iteration, so a server loop or a browser host calls it directly; `FixedTickThread` calls it on its own thread.

| Rule | Detail |
|------|--------|
| **Requests, not commands** | Pause, resume and step are requests to the runner, not commands in the queue (the plan's §3 listed them as commands; while paused no tick runs, so a queued resume would never be applied). A volatile flag and an interlocked step counter, applied at the start of the next `RunDue` |
| **Paused frames** | While paused the handler still gets `PausedFrame` once per period: it applies commands and publishes without stepping, so an edit made while paused takes effect at once, as today's `World.CollideObjects` re-applies transforms while paused |
| **Step** | Each `Step` is exactly one 1/60 s tick (one clock period); requests add up; discarded while running |
| **Fault** | A handler call that throws pauses the runner, skips the rest of the batch, and raises `Faulted` once per run of consecutive faults. A run ends when a handler call succeeds, when the loop finds the runner running again (a `Resume` stored after the fault's pause) or when it applies a `Step`. After a fault the resume key resumes and the step key steps once |
| **The fault's pause wins** | A `Resume` that arrives while the faulting call runs is dropped, and so is a `Step` issued while the runner was running: no tick runs after a fault. A `Step` that lands between the step clear and the pause store in `OnFault` is kept and runs one tick while paused (named in the code's comment; accepted) |
| **Drops** | When the cap drops ticks, `TicksDropped` is raised before the iteration's ticks run (the host logs at most once per run of drops) |
| **Stop** | `FixedTickThread.Stop` sets the flags, wakes the waiter and joins: it returns after the handler call in progress; a call that never returns blocks it, as today's `Join` does (.NET has no thread abort). `Dispose` marks the host disposed first, so a racing `Start` throws or its loop is stopped |

The runner catches because it is the high-level caller of the handler: the handler's per-tick code has no exception handling (the rule on exception handling in per-frame code, [CODE-STYLE](../CODE-STYLE.md)).

## 6. The waiters and the measured pacing

`ITickWaiter.WaitUntil(timestamp)` blocks the loop thread until the time source reaches the timestamp or `Wake` is called (one pending wake is remembered; early returns are allowed, callers re-check). There is no spin tail (mentor's decision, told to Denys).

- **`SleepTickWaiter`** (portable): an `AutoResetEvent` wait in whole milliseconds, rounded up. Accuracy is the OS timer resolution.
- **`HighResolutionTickWaiter`** (`[SupportedOSPlatform("windows")]`): a `CREATE_WAITABLE_TIMER_HIGH_RESOLUTION` timer and a wake event waited together; the due time is rounded up to 100 ns so it never fires early. Denys decided it lives in `CoolEngine.Core` ("In CoolEngine.Core (Recommended)"), so pacing does not depend on what any process asked of the timer resolution.

Measured on 2026-11-27 on this machine (i7-14700K, Windows 11, .NET 10.0.12, Release, plain console, no window), 1,200 ticks at 60 Hz per row; error = start minus scheduled, in microseconds; the global timer resolution read 1 ms throughout because another process held it:

| Wait | Mean | p99 | Max | Ticks over 2 ms late | CPU of the waiting thread |
|------|------|-----|-----|----------------------|----------------------------|
| `Thread.Sleep(1)` loop, no resolution request | 7,974 | 15,614 | 16,367 | 1,055 | 0.0 % |
| `AutoResetEvent.WaitOne` loop, no request | 8,180 and 8,294 | about 16,000 | about 16,500 | about 1,080 | 0.0 to 0.1 % |
| `timeBeginPeriod(8)` (OpenTK's request) plus `AutoResetEvent.WaitOne` (the `SleepTickWaiter` method) | 815 | 1,652 | 2,180 | 3 | 0.0 % |
| High-resolution waitable timer (the `HighResolutionTickWaiter` method), two runs | 299 and 307 | 674 and 710 | 2,464 and 922 | 1 and 0 | 0.2 to 0.4 % |
| High-resolution timer to 0.5 ms before, then spin (not built) | 8 | 152 | 381 | 0 | 5.0 % |
| Spin only | 1 | 1 | 87 | 0 | 99.7 % |

Reading: without a resolution request any sleep or event wait is about 8 ms late on average and up to a whole tick, so ticks arrive in pairs and gaps (the warning of the plan's E6); sleep-then-spin helps only when the spin margin exceeds the timer granularity (a 9 ms margin cost 45 % of a core). The waitable timer gives about 0.3 ms mean and 0.7 ms p99 at almost no CPU whatever any process requested. **The premise of E6 does not hold inside the game process:** `OpenTK.Windowing.Desktop` 4.9.4's `GameWindow.Run` calls `timeBeginPeriod(8)` (found in the assembly's IL, not by running a window), so today's physics loop sleeps about 5 to 6 ms here, not 15.6 ms; on a machine where no other process holds a finer resolution it would get 8 ms. This also bears on Q11's "per physics loop" rate. A tick pacing test through the new host in the game is not run (nothing uses it yet).

## 7. Thread rules

| Member | Thread |
|--------|--------|
| `FixedTickClock` | The thread that owns the runner; the runner owns the clock once given |
| `FixedTickRunner.Start`, `RunDue`, `TickNumber`, `NextDueTimestamp` | The loop thread |
| `FixedTickRunner.Pause`, `Resume`, `Step`, `IsPaused`; `FixedTickThread` members | Any thread (the host also wakes the loop) |
| `Faulted`, `TicksDropped` handlers | The loop thread |
| `CommandQueue<T>.Enqueue` | Any thread; `TakePending` and `LastTakenSequence`: the one consumer |
| `SnapshotBuffer<T>.WriteTarget`, `Publish` / `Acquire` | The writer / the reader |

The host does not own its waiter: dispose it after `Stop`. `FixedTickThread.Start` throws `PlatformNotSupportedException` on single-threaded WebAssembly; such a host calls `RunDue` inline. The constructor rejects a time source whose frequency differs from the clock's.

## 8. How it is tested

`tests/CoolEngine.Core.Tests` (NUnit, `net10.0`, 88 tests as of 2026-11-28, headless, through the public API, no sleeps and no wall-clock assertions): the clock's arithmetic and cap, the queue (several producers against one consumer: each command applied exactly once, in each producer's order, batches contiguous), the snapshot buffer (one writer against one reader: no torn snapshot, ticks never backwards), the runner (pause, step, faults, drops), the thread host (stop while a tick runs, dispose racing start), the waiters, the public surface (exported types, no reference but the runtime, no static field). Allocation tests assert 0 B in steady state, taking the minimum of three blocks since a first run flaked under load. Test doubles: `ManualTimestampSource`, `ManualTickWaiter`, `RecordingTickHandler`.

The first review (`opus`, `xhigh`, 2026-11-27) found the primitives correct under the .NET memory model: 0 blocker, 0 major, 7 minor, 3 nit; two fix rounds followed (the second removed the runner's resume signal so that `Resume` is one store) and the re-review of 2026-11-28 passed (30 of 30 sequential runs). Mutations no first test caught (a lost phase at the cap, a backwards timestamp moving the grid, a successful paused frame not ending a fault run, `Wake` removed from `Stop`) have tests now. No benchmark project (mentor's decision): the costs are tens of nanoseconds per command and the four allocation tests are the budget check.

## 9. Notes for the next task (OT-21)

- **A `Faulted` or `TicksDropped` subscriber must not throw.** The exception leaves `RunDue`; on `FixedTickThread` it ends the process.
- **Commands after a faulting command in a batch are lost** (section 3); the `Error` line of the fault is the only record.
- The simulation never asks which thread it is on, so the same handler can run inline.
- `Resume` after a fault: the next fault raises `Faulted` again; a `Step` after a fault steps once.

## 10. Limits

- A tick that never returns blocks `Stop`.
- The clock assumes the host keeps calling `RunDue`; a long stall is capped (15 ticks at the game's setting) and the rest dropped and reported.
- Pause and step are not commands: they cannot be sequenced with edits.
