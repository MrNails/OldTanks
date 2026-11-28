# CoolEngine.Physics

| Field        | Value  |
|--------------|--------|
| Status       | Living - stage 1 of two |
| Last updated | 2026-11-28 |
| Verified against | 2026-11-28; feature/OT-20 at 9212d68 (whole page against the library's code, its tests and benchmark classes; the parity proof and the benchmark figures are the implementer's and the reviewer's runs of 2026-11-27 and 2026-11-28, not rerun by the documenter) |

---

`CoolEngine.Physics` (`CoolEngine.Physics/`) is the headless physics library of the [split plan](../plans/SIMULATION-AND-PHYSICS-SPLIT.md), WP2 of OT-20. **Stage 1 is done: an exact port of today's step. Stage 2 (quaternions, world-space velocity) is not done**, and the members `Linear`, `Size` and `VelocityFrame` are stage-1 members that stage 2 replaces. **Nothing in the game uses the library yet; the game is unchanged.** Guide: [CoolEngine.Physics/AGENTS.md](../../CoolEngine.Physics/AGENTS.md). Today's behaviour that it reproduces: [PHYSICS-AND-COLLISIONS](../CoolEngine/PhysicEngine/PHYSICS-AND-COLLISIONS.md).

## 1. What the library owns

| Owns | Does not own |
|------|--------------|
| Bodies (position, velocity, force, mass, static flag, ground flag), shapes, detection, the sub-stepped step, the ray cast | OpenGL, the window, the UI, threads, the logger, the game |
| The terminal-speed rule and the contact resolution, with today's arithmetic | The game's drive rules: forward damping and snap, `MaxSpeed`, `MaxBackSpeed`, `MaxSpeedMultiplier`, jump force (Q4: they run between the phase calls, in the game), `Restitution`, `Density`, `CenterOfMass` (Q10, game data) |
| | The clock, pause, single step and the backlog cap ([CORE](../CoolEngine.Core/CORE.md)) |

References only `CoolEngine.Mathematics`; no static field except `const`. Worlds are independent: two worlds on two threads each equal their serial run (a test).

## 2. Public types

| Type | Role |
|------|------|
| `CoolEngine.Physics.PhysicsWorld` | The bodies and the step |
| `CoolEngine.Physics.StepPhase` | `Idle`, `VelocitiesIntegrated`, `TerminalSpeedClamped`, `PositionsIntegrated` |
| `CoolEngine.Physics.BodyHandle` | Slot and generation, a value type |
| `CoolEngine.Physics.BodyDescription`, `BodyState` | Input of `Add`; one body's whole state (for a snapshot) |
| `CoolEngine.Physics.PhysicsSettings` | Gravity direction, gravity acceleration, terminal speed, sub-step count |
| `CoolEngine.Physics.Ray`, `RayHit` | A ray (start, end); the nearest hit (body, point, distance) |
| `CoolEngine.Physics.PhysicsQueries` | Pure functions on caller-owned buffers: world vertices and normals, ray against a polygon or sphere, nearest hit over arrays (the pick ray on the GL thread and the debug view, E8 and E9) |
| `CoolEngine.Physics.Shapes.Shape`, `PolygonShape`, `ShapeFace`, `SphereShape` | Shapes |

13 types plus the nested `PhysicsWorld.BodyEnumerator` (14 exported; a test pins the list). The detection (`CoolEngine.Physics.Detection.NarrowPhase`, `BodyGeometry`, `Geometry`) and `Internal.LegacyMath` are internal. The game supplies `PhysicsSettings` as `(0, -1, 0)`, 9.80665, the float product 9.80665 × 9 (not a rounded literal) and 5 sub-steps.

## 3. The step: public phase calls

Denys chose "Public phase calls" (2026-11-27; turned down: two callbacks per sub-step, the mentor's recommendation; a per-body integrator; reading the plan first). The game's rules run between the calls. The mentor added a **run-time order check**: a call in the wrong phase throws `InvalidOperationException` naming the current phase and the call expected next, and `AbortStep` is the way out after an exception inside a sub-step.

| Call | Does | Needs | Leaves |
|------|------|-------|--------|
| `IntegrateVelocities(subStepTime)` | `v + (force / mass + gravityDirection × acceleration) × time` for every dynamic body in slot order | `Idle` | `VelocitiesIntegrated` |
| `ClampTerminalSpeed()` | Today's rule, including the flip of KI-040 | `VelocitiesIntegrated` | `TerminalSpeedClamped` |
| `ClearForces()` | Every dynamic body's force becomes zero; a static body keeps its force | any | unchanged |
| `IntegratePositions(subStepTime)` | `p + ((velocityFrame × v) × time)`, clears the ground flag, rebuilds the geometry | `TerminalSpeedClamped` | `PositionsIntegrated` |
| `ResolveContacts()` | Every pair `i < j` in slot order (static pairs skipped): bounds test, detection, push, ground flag, velocity cancel, geometry of the moved bodies before the next pair | `PositionsIntegrated` | `Idle` |
| `AbortStep()` | Back to `Idle`, nothing else changed | any | `Idle` |
| `Step(timeStep)` | The plain step with no game rule: `timeStep / SubStepCount` per sub-step, the five calls in order, `ClearForces` on the last sub-step | `Idle` | `Idle` |

One sub-step with the game's rules (the stage-1 test drive, today's order): the forward damping and snap, `IntegrateVelocities`, the forward clamp, `ClampTerminalSpeed`, on the last sub-step `ClearForces`, `IntegratePositions`, `ResolveContacts`. Running each operation for all bodies before the next gives today's bits (measured by the analyst's prototype on 2026-11-27: every scene equal). Which calls are allowed when: getters, `RayCast`, `GetState`, the enumerator, `ClearForces`, `AbortStep` in any phase; `SetVelocity`, `SetForce`, `AddForce` until the positions are integrated; `Add`, `Remove`, `SetPosition`, `SetLinear`, `SetSize`, `SetVelocityFrame`, `SetMass`, `SetStatic` and the `Settings` setter in `Idle` only. A time argument must be finite and not negative. Setters throw where today's code ignores silently (a velocity or force on a static body), and the ground flag is read-only (mentor's decision, told to Denys).

## 4. Storage and handles

- **One array per field**, indexed by slot; the step walks slots `0 ... SlotCount - 1` and skips free ones; pairs are `i < j` over live slots.
- **Slots are reused last-freed-first** (a stack). After a removal and an add the new body takes the freed slot, so the pair order differs from today's appended list: deterministic, and covered by no golden.
- **`BodyHandle`** is a slot and a generation (1 on first use, plus one on `Remove`, wrapping from `int.MaxValue` to 1, never 0). `default` is never valid. A handle of a removed body is rejected (`ArgumentException`, parameter `handle`) by every member that takes one. **A handle is valid only for the world that returned it:** it does not name its world, so a handle used with another world addresses whatever body that world holds in the same slot with the same generation, and this is not detected (the mentor amended the contract; no code).
- **Growth** only in `Add` (arrays double); a slot keeps its geometry caches for reuse; the step never allocates. `Remove` never allocates.
- **Geometry** (vertices, normals, bounds) is rebuilt at once by every setter that changes it, so `RayCast` and `GetWorldVertices` never see stale data.

## 5. Shapes

Shapes are immutable and shared by reference, safe from any thread (E3: they replace `GlobalCache<CollisionData>` and the static constructors). `PolygonShape` copies its inputs, validates them (at least one vertex, finite data, at least 3 indices per face, indices in range) and finds the edges, edge directions and cuboid axis faces once, with today's loops and tolerances so that ties break as before. Model space spans -1 to 1; indices 0, 1, 2 of a face must be a corner and its two neighbours (the ray test). `SphereShape` holds no data and has a public constructor and no static instance: the radius is half the body's width (`Size.X / 2`).

## 6. Stage 1 and what stage 2 will replace

| Stage 1 member (transitional) | Why | Stage 2 |
|-------------------------------|-----|---------|
| `BodyDescription.Linear`, `PhysicsWorld.GetLinear`/`SetLinear`, `BodyState.Linear` | Today's rotation times scale, computed by the game with the `Matrix4` factories; the library forms `CreateTranslation(position) × new Matrix4(linear)`. A direct column assembly is equal in value but not in bits (a -0 translation turns into +0) | A unit quaternion and half-extents (E2); a non-unit quaternion rejected |
| `Size`, `GetSize`/`SetSize` | Today's `Size`; a sphere's width is `Size.X` | Half-extents; a sphere's three equal |
| `VelocityFrame` | `Matrix3.CreateRotationY(heading).Transposed()`: velocity and force are local to the body, as today | World-space velocity; the game's drive rotates it on a heading change (Q4) |
| `PhysicsQueries` `Matrix3` overloads | Same | Quaternion overloads |

Stage 2's difference from stage 1 is to be measured and goes to Denys, who decides its acceptance bar; the analyst's preview (2026-11-27, a prototype, not the stage) read: the default scene bit-equal over 1,200 ticks; 200 random scenes of 300 ticks with a median of the largest position difference of 1.97e-5 m, a 90th percentile of 1.2 m and a maximum of 7.2 m (chaotic amplification through contacts). Open in [BACKLOG](../BACKLOG.md#planned).

## 7. How the exact port is proved

By **side-by-side tests against today's code, bit for bit** (Denys chose that WP1's recorded goldens are compared "Within tolerance (Recommended)", turned down "Bit for bit"; the exact port is therefore proved here, not by WP1's literals). All run today's `OldTanks.Models.World` and the library in one process, same machine and build (Q7).

| Where | What |
|-------|------|
| `tests/OldTanks.Tests/PhysicsParity` (16) | The linear part against `WorldObject.ApplyTransformation`; the default scene (1,200 ticks), terminal speed, forces, headings and jump, dynamic against dynamic, a body made static and dynamic again, remove and add, negative-zero coordinates and a zero time step, zero limits with a backward force on the ground, a body hit while static, 4 seeded random-scene sets (50 scenes of 300 ticks each), the drive's domain check, seeded rays against today's nearest-hit loop |
| `tests/CoolEngine.Tests` `DetectionParityTests` (4) | 20,000 seeded pairs of cube, triangulated cube, tetrahedron and sphere against today's `Collision.CheckCollision`; two spheres exactly touching on an axis; 20,000 seeded rays; 20,000 seeded transforms against `UpdateCollision` |
| `tests/CoolEngine.Physics.Tests` (127) | The ported detection and geometry tests, shapes, handles, settings, the phase rules and their errors, the step (a drop onto a floor, the flip, the ground flag, forces, determinism: the same scene twice gives identical bits; two worlds on two threads), allocations (0 B), the exported types, no static field |

The reviewer's evidence (`opus`, `max`, 2026-11-28): the port read side by side expression by expression, Debug and Release; 13 of the 15 mutants he listed were caught at first, and the nine survivors of the later run are caught now. A reassociated position update made 8 parity tests fail, so the harness sees one bit. The exact domain: `MaxSpeed × MaxSpeedMultiplier ≥ -MaxBackSpeed` and finite inputs (today's setter clamps up to three times per sub-step, the drive once; the test drive rejects limits outside it). The reviewer also found that the position stores must skip an equal value as today's `SetField` does (a zero displacement leaves a -0 coordinate); the library's internal stores do. The test of WP1's scenes run through the library is a later item (BACKLOG).

## 8. Benchmarks

`tests/CoolEngine.Physics.Benchmarks` (BenchmarkDotNet, `[MemoryDiagnoser]`; the old categories and the reference to `OldTanks` are transitional until the old code goes). Reported by the mentor, 2026-11-28, one pinned core:

| Measure | Today | Library |
|---------|-------|---------|
| One tick (5 sub-steps), default scene | 15.7 µs | 3.9 µs with the game's rules, 3.6 µs plain; 0 B |
| Cube against cube detection | 1 | 0.91 of today's time |
| Sphere against cube | 1 | 0.92 |
| Geometry | 1 | 0.86 |

The implementer's first run (other agents running) gave 15.8, 4.0 and 3.6 µs. **The reviewer's reading of where the gain comes from** (about half of the gap accounted for): today's tick rebuilds every transform about 20 times with sin and cos, calls `CreateRotationY` 10 times in `Move`, takes the `GlobalLock` 10 times and raises property-changed on every set, while the library takes the linear part and velocity frame as inputs. **The game will see less than the fourfold gain**, because it still pays for those inputs and for the snapshot. No benchmark of the 1,000-body stress scene is quoted here: it exists (`Step_Stress1000`) but its figures were not given to the documenter.

## 9. Known limits

- Stage 2 is open (section 6); `Linear`, `Size` and `VelocityFrame` change then.
- The ported quirks, recorded and not fixed (Denys, 2026-11-27: "Port exactly, record KIs"): [KI-040](../KNOWN-ISSUES.md#ki-040-the-terminal-speed-clamp-flips-a-fast-upward-speed-to-falling), [KI-041](../KNOWN-ISSUES.md#ki-041-the-ground-flag-comes-from-the-direction-between-the-bodies-centres), [KI-042](../KNOWN-ISSUES.md#ki-042-inside-one-body-the-first-face-in-list-order-wins-a-ray-test), [KI-043](../KNOWN-ISSUES.md#ki-043-a-sphere-deep-inside-another-sphere-gets-a-wrong-depth), [KI-044](../KNOWN-ISSUES.md#ki-044-a-ray-that-starts-inside-a-cube-reports-no-hit), [KI-045](../KNOWN-ISSUES.md#ki-045-a-body-under-a-static-ceiling-sticks-until-gravity-uses-up-its-upward-speed), [KI-046](../KNOWN-ISSUES.md#ki-046-stacked-dynamic-bodies-never-settle-and-the-lower-one-ends-each-tick-off-the-ground), [KI-047](../KNOWN-ISSUES.md#ki-047-two-spheres-with-exactly-the-same-centre-get-nan-positions).
- One thread at a time per world, not detected. A handle of another world is not detected.
- A velocity that overflows makes `SetVelocity` throw in the middle of a sub-step: the caller needs `AbortStep` (the notes in [BACKLOG](../BACKLOG.md#planned)).
- No broad phase (the pair test is quadratic) and no multi-threaded step (Q8, later tasks); no collision response beyond the push and the vertical velocity cancel.
- Same bits on the same machine and build only (Q7); `Vector3.Dot`, `Length` and `Normalize` may differ between instruction sets.
