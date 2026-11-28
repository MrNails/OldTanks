# CoolEngine.Physics

> **Project knowledge:** [../docs/PROJECT-KNOWLEDGE.md](../docs/PROJECT-KNOWLEDGE.md)

The engine's headless physics library: bodies in arrays, shapes, narrow-phase detection, the sub-stepped step and the ray cast, today's step with a unit-quaternion orientation, half-extents and world-space velocity. **Both stages are done** (OT-20, 2026-11-28, `feature/OT-20`, unmerged): stage 1 was an exact port, stage 2 (`3c1b624`) replaced `Linear`, `Size` and `VelocityFrame` by `Orientation`, `HalfExtents` and world-space velocity. Nothing in the game uses it yet.

## Related Docs

| Topic | Page |
|-------|------|
| What the library owns, the types, the phase calls, handles, shapes, the two stages, the parity proof and its bar, benchmarks, limits | [PHYSICS-LIBRARY](../docs/CoolEngine.Physics/PHYSICS-LIBRARY.md) |
| Today's step, detection and shapes that it ports | [PHYSICS-AND-COLLISIONS](../docs/CoolEngine/PhysicEngine/PHYSICS-AND-COLLISIONS.md) |
| The ported quirks (KI-040 to KI-047) | [KNOWN-ISSUES](../docs/KNOWN-ISSUES.md#ki-040-the-terminal-speed-clamp-flips-a-fast-upward-speed-to-falling) |
| The plan, Denys's decisions and WP2's two stages | [plan](../docs/plans/SIMULATION-AND-PHYSICS-SPLIT.md) |
| The unit rules the library's inputs and the new game code follow (decided 2026-11-28) | [UNITS-STANDARDIZATION](../docs/plans/UNITS-STANDARDIZATION.md) |
| The tick machinery that will call the step | [CORE](../docs/CoolEngine.Core/CORE.md) |

## Project Details

| Property | Value |
|----------|-------|
| Namespace root | `CoolEngine.Physics` (`Shapes`; the internal `Detection` and `Internal`) |
| Target framework | `net10.0`, `LangVersion latest` |
| Layer | Engine library; referenced by no game or engine project yet; tested by `tests/CoolEngine.Physics.Tests` (142), `tests/OldTanks.Tests/PhysicsParity` (27) and `tests/CoolEngine.Tests` (`DetectionParityTests`, 3); benchmarked by `tests/CoolEngine.Physics.Benchmarks` |

## Key Types

| Type | Role |
|------|------|
| `CoolEngine.Physics.PhysicsWorld` | The bodies and the step: `Add`, `Remove`, getters and setters, `Step` and the public phase calls `IntegrateVelocities`, `ClampTerminalSpeed`, `ClearForces`, `IntegratePositions`, `ResolveContacts`, `AbortStep`, `RayCast` |
| `CoolEngine.Physics.StepPhase` | Where the world is inside a sub-step; the order is enforced at run time |
| `CoolEngine.Physics.BodyHandle`, `BodyDescription`, `BodyState` | A body's slot and generation; what `Add` takes (shape, position, unit quaternion, half-extents, world-space velocity and force); one body's whole state |
| `CoolEngine.Physics.PhysicsSettings` | Gravity, terminal speed and sub-step count, immutable |
| `CoolEngine.Physics.Ray`, `RayHit`, `PhysicsQueries` | Ray types, and the pure functions for geometry and ray casts on caller-owned buffers |
| `CoolEngine.Physics.Shapes.Shape`, `PolygonShape`, `ShapeFace`, `SphereShape` | Immutable shapes shared by reference |

## Dependencies

| Dependency | Reason |
|------------|--------|
| `CoolEngine.Mathematics` | The matrices of the geometry (`Matrix3.CreateFromQuaternion` and the rest); the only reference |
| `Microsoft.CodeAnalysis.BannedApiAnalyzers` (`PrivateAssets=all`) | RS0030 is an error: `BannedSymbols.txt` |

## Constraints

- **No static field except `const`, no logger, no `INotifyPropertyChanged`, no `Common`, no OpenGL, no threads.** A `PhysicsWorld` is used by one thread at a time; it has no lock and does not detect concurrent use. Shapes and `PhysicsQueries` are safe from any thread.
- **The rules are today's, in today's order:** every expression keeps today's form and order, because a reassociated position update made 8 parity tests fail in stage 1. A change to `PhysicsWorld`, `NarrowPhase` or `Geometry` must keep every parity test green: the 10 scenes with no turned body equal to today's code by bits, the other named scenes within 1 mm on every tick and at least 190 of the 200 random scenes within 1 mm (Denys's bar, 2026-11-28); no check is loosened. The quirks KI-040 to KI-047 are ported on purpose: do not fix one without Denys's specification.
- **Velocity and force are world-space vectors** in metres per second and newtons; the library has no velocity frame, forward axis or speed limit. A pose is a unit quaternion and half-extents; a non-unit quaternion is rejected by `Add` and `SetOrientation`. New code follows [UNITS-STANDARDIZATION](../docs/plans/UNITS-STANDARDIZATION.md).
- **A handle is valid only for the world that returned it**; using it with another world is not detected.
- **0 B allocated** by `Step`, the phase calls, `RayCast` and detection after warm-up (allocation tests).
- **The stage-1 members are gone** (`Linear`, `Size`, `VelocityFrame`, their getters and setters, the `Matrix3` overloads of `PhysicsQueries`); the game's drive rules live in test code (`OldTanks.Tests.PhysicsParity.WorldSpaceDrive`) until `OldTanks.Simulation` exists (OT-21).
- **Reviewed at effort `max`** (step math, a port onto `CoolEngine.Mathematics`).
