# CoolEngine.Core

> **Project knowledge:** [../docs/PROJECT-KNOWLEDGE.md](../docs/PROJECT-KNOWLEDGE.md)

The engine's shared machinery between threads: a fixed-tick clock and the runner and thread host around it, a command queue (commands in) and a snapshot buffer (snapshots out). Added in OT-20 (2026-11-27 to 2026-11-28, `feature/OT-20`, unmerged); nothing in the game uses it yet (the switch is OT-21, [plan](../docs/plans/SIMULATION-AND-PHYSICS-SPLIT.md)).

## Related Docs

| Topic | Page |
|-------|------|
| The clock's exact arithmetic and cap, the queue, the snapshot buffer and its memory-model argument, the runner's pause, step and fault rules, the two waiters with the measured pacing, thread rules, tests, notes for the next task | [CORE](../docs/CoolEngine.Core/CORE.md) |
| Why the library exists, the decisions (E6, Q3, Q9, Q12, the high-resolution wait) and the work packages | [plan](../docs/plans/SIMULATION-AND-PHYSICS-SPLIT.md) |
| The library that is stepped from a tick handler | [PHYSICS-LIBRARY](../docs/CoolEngine.Physics/PHYSICS-LIBRARY.md) |
| Remaining work | [BACKLOG](../docs/BACKLOG.md#planned) |

## Project Details

| Property | Value |
|----------|-------|
| Namespace root | `CoolEngine.Core` (`Timing`, `Channels`, `Hosting`). It sits beside `CoolEngine.GraphicalEngine.Core` and `CoolEngine.PhysicEngine.Core`: inside a `CoolEngine.*` namespace a partly qualified `Core.X` resolves to the nearest enclosing `Core` |
| Target framework | `net10.0`, `LangVersion latest` |
| Layer | Engine library; referenced by no project yet; tested by `tests/CoolEngine.Core.Tests` (88 tests) |

## Key Types

| Type | Role |
|------|------|
| `CoolEngine.Core.Timing.FixedTickClock`, `TickAdvance` | The pure fixed-tick state machine over integer timestamps: due ticks, pause, single step, the backlog cap, paused frames |
| `CoolEngine.Core.Timing.ITimestampSource`, `StopwatchTimestampSource` | Monotonic integer timestamps |
| `CoolEngine.Core.Timing.ITickWaiter`, `SleepTickWaiter`, `HighResolutionTickWaiter` | Pacing: the portable event wait, and the Windows high-resolution waitable timer (`[SupportedOSPlatform("windows")]`) |
| `CoolEngine.Core.Channels.CommandQueue<T>`, `CommandBatch<T>` | Many producers, one consumer; the sequence number makes an acknowledgement exact |
| `CoolEngine.Core.Channels.SnapshotBuffer<T>` | A wait-free triple buffer, one writer and one reader |
| `CoolEngine.Core.Hosting.ITickHandler`, `FixedTickRunner` | The simulation side, and one inline iteration with the pause, step and fault rules |
| `CoolEngine.Core.Hosting.FixedTickThread`, `TickFaultEventArgs`, `TicksDroppedEventArgs` | The background loop thread host, and the two event payloads |

## Dependencies

| Dependency | Reason |
|------------|--------|
| none | No project and no package reference, on purpose, and no `Common`: a server or a browser host takes the library alone. No static field, no logger, no `unsafe` code |

## Constraints

- **No static state, no logger, no `INotifyPropertyChanged`.** The host logs: it subscribes to `FixedTickRunner.Faulted` and `TicksDropped` before the loop starts.
- **A `Faulted` or `TicksDropped` subscriber must not throw:** the exception leaves `FixedTickRunner.RunDue`, and on a `FixedTickThread` it ends the process ([CORE §5](../docs/CoolEngine.Core/CORE.md#5-the-runner-pause-step-and-faults)).
- **Commands after a faulting command in one batch are lost**, because the batch is consumed; the next snapshot acknowledges them ([CORE §3](../docs/CoolEngine.Core/CORE.md#3-the-command-queue)).
- **The cap and the tick rate come from the host:** nothing here hard-codes 60 or 15.
- **A change to the queue, the buffer or the stop sequence is reviewed at effort `xhigh` or above** and keeps the tests that name their threads (`CoolEngine.Core.Tests.Channels`, `Hosting`) green under repetition; those tests use no sleeps and no wall-clock assertions.
- **The Windows waiter is the one platform call:** `DllImport` of three `kernel32` functions with blittable signatures; a browser host never constructs it.
