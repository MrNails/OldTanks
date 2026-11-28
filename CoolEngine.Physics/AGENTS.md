# CoolEngine.Physics

> **Project knowledge:** [../docs/PROJECT-KNOWLEDGE.md](../docs/PROJECT-KNOWLEDGE.md)

The engine's headless physics library: bodies in arrays, shapes, narrow-phase detection, the sub-stepped step and the ray cast, an exact port of today's step. **Stage 1 of two** (OT-20, 2026-11-28, `feature/OT-20`, unmerged): the members `Linear`, `Size` and `VelocityFrame` are stage-1 members that stage 2 (quaternions, world-space velocity) replaces. Nothing in the game uses it yet.

## Related Docs

| Topic | Page |
|-------|------|
| What the library owns, the types, the phase calls, handles, shapes, stage 1 and stage 2, the parity proof, benchmarks, limits | [PHYSICS-LIBRARY](../docs/CoolEngine.Physics/PHYSICS-LIBRARY.md) |
| Today's step, detection and shapes that it ports | [PHYSICS-AND-COLLISIONS](../docs/CoolEngine/PhysicEngine/PHYSICS-AND-COLLISIONS.md) |
| The ported quirks (KI-040 to KI-047) | [KNOWN-ISSUES](../docs/KNOWN-ISSUES.md#ki-040-the-terminal-speed-clamp-flips-a-fast-upward-speed-to-falling) |
| The plan, Denys's decisions and WP2's two stages | [plan](../docs/plans/SIMULATION-AND-PHYSICS-SPLIT.md) |
| The tick machinery that will call the step | [CORE](../docs/CoolEngine.Core/CORE.md) |

## Project Details

| Property | Value |
|----------|-------|
| Namespace root | `CoolEngine.Physics` (`Shapes`; the internal `Detection` and `Internal`) |
| Target framework | `net10.0`, `LangVersion latest` |
| Layer | Engine library; referenced by no game or engine project yet; tested by `tests/CoolEngine.Physics.Tests` (127), `tests/OldTanks.Tests/PhysicsParity` (16) and `tests/CoolEngine.Tests` (`DetectionParityTests`, 4); benchmarked by `tests/CoolEngine.Physics.Benchmarks` |

## Key Types

| Type | Role |
|------|------|
| `CoolEngine.Physics.PhysicsWorld` | The bodies and the step: `Add`, `Remove`, getters and setters, `Step` and the public phase calls `IntegrateVelocities`, `ClampTerminalSpeed`, `ClearForces`, `IntegratePositions`, `ResolveContacts`, `AbortStep`, `RayCast` |
| `CoolEngine.Physics.StepPhase` | Where the world is inside a sub-step; the order is enforced at run time |
| `CoolEngine.Physics.BodyHandle`, `BodyDescription`, `BodyState` | A body's slot and generation; what `Add` takes; one body's whole state |
| `CoolEngine.Physics.PhysicsSettings` | Gravity, terminal speed and sub-step count, immutable |
| `CoolEngine.Physics.Ray`, `RayHit`, `PhysicsQueries` | Ray types, and the pure functions for geometry and ray casts on caller-owned buffers |
| `CoolEngine.Physics.Shapes.Shape`, `PolygonShape`, `ShapeFace`, `SphereShape` | Immutable shapes shared by reference |

## Dependencies

| Dependency | Reason |
|------------|--------|
| `CoolEngine.Mathematics` | The `Matrix3` and `Matrix4` of today's arithmetic; the only reference |
| `Microsoft.CodeAnalysis.BannedApiAnalyzers` (`PrivateAssets=all`) | RS0030 is an error: `BannedSymbols.txt` |

## Constraints

- **No static field except `const`, no logger, no `INotifyPropertyChanged`, no `Common`, no OpenGL, no threads.** A `PhysicsWorld` is used by one thread at a time; it has no lock and does not detect concurrent use. Shapes and `PhysicsQueries` are safe from any thread.
- **The port is exact:** every expression keeps today's form and order, because a reassociated position update made 8 parity tests fail. A change to `PhysicsWorld`, `NarrowPhase` or `Geometry` must keep every parity test (bitwise, against today's code) green; a difference of one bit stops the work and no check is loosened. The quirks KI-040 to KI-047 are ported on purpose: do not fix one without Denys's specification.
- **A handle is valid only for the world that returned it**; using it with another world is not detected.
- **0 B allocated** by `Step`, the phase calls, `RayCast` and detection after warm-up (allocation tests).
- **Stage 2 is not done:** `Linear`, `Size`, `VelocityFrame` (and their getters and setters, and the `Matrix3` overloads of `PhysicsQueries`) are replaced there; do not build on them in new game code without saying so.
- **Reviewed at effort `max`** (step math, a port onto `CoolEngine.Mathematics`).
