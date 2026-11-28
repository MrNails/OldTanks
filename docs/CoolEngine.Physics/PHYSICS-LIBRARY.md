# CoolEngine.Physics

| Field        | Value  |
|--------------|--------|
| Status       | Living - both stages done |
| Last updated | 2026-11-28 |
| Verified against | 2026-11-28; feature/OT-20 at 3c1b624 (whole page against the library's code, its XML comments, its tests, the parity folder and the benchmark classes; the difference table, the reviewer's analysis and the benchmark figures are the mentor's and the reviewer's runs of 2026-11-28, not rerun by the documenter) |

---

`CoolEngine.Physics` (`CoolEngine.Physics/`) is the headless physics library of the [split plan](../plans/SIMULATION-AND-PHYSICS-SPLIT.md), WP2 of OT-20. **Both stages are done: stage 1 was an exact port of today's step (commit `9212d68`); stage 2 (`3c1b624`, 2026-11-28) gave a body a unit quaternion, half-extents and world-space velocity** ([§6](#6-the-two-stages)). **Nothing in the game uses the library yet; the game is unchanged.** Guide: [CoolEngine.Physics/AGENTS.md](../../CoolEngine.Physics/AGENTS.md). Today's behaviour that it reproduces: [PHYSICS-AND-COLLISIONS](../CoolEngine/PhysicEngine/PHYSICS-AND-COLLISIONS.md).

## 1. What the library owns

| Owns | Does not own |
|------|--------------|
| Bodies (position, orientation, half-extents, world-space velocity and force, mass, static flag, ground flag), shapes, detection, the sub-stepped step, the ray cast | OpenGL, the window, the UI, threads, the logger, the game |
| The terminal-speed rule and the contact resolution, with today's rules | The game's drive rules: forward damping and snap, `MaxSpeed`, `MaxBackSpeed`, `MaxSpeedMultiplier`, jump force, a velocity frame or a forward axis (Q4: they run between the phase calls, in the game), `Restitution`, `Density`, `CenterOfMass` (Q10, game data) |
| | The clock, pause, single step and the backlog cap ([CORE](../CoolEngine.Core/CORE.md)) |

References only `CoolEngine.Mathematics`; no static field except `const`. Worlds are independent: two worlds on two threads each equal their serial run (a test).

## 2. Public types

| Type | Role |
|------|------|
| `CoolEngine.Physics.PhysicsWorld` | The bodies and the step |
| `CoolEngine.Physics.StepPhase` | `Idle`, `VelocitiesIntegrated`, `TerminalSpeedClamped`, `PositionsIntegrated` |
| `CoolEngine.Physics.BodyHandle` | Slot and generation, a value type |
| `CoolEngine.Physics.BodyDescription`, `BodyState` | Input of `Add`: a shape, a position, a unit `System.Numerics.Quaternion` orientation and half-extents, then the optional world-space velocity and force, mass and static flag; one body's whole state (for a snapshot) |
| `CoolEngine.Physics.PhysicsSettings` | Gravity direction, gravity acceleration, terminal speed, sub-step count |
| `CoolEngine.Physics.Ray`, `RayHit` | A ray (start, end); the nearest hit (body, point, distance) |
| `CoolEngine.Physics.PhysicsQueries` | Pure functions on caller-owned buffers, taking a position, an orientation and half-extents: `ComputeWorldVertices`, `ComputeWorldNormals`, `IntersectRay` (polygon or sphere), `RayCastNearest` over arrays (the pick ray on the GL thread and the debug view, E8 and E9) |
| `CoolEngine.Physics.Shapes.Shape`, `PolygonShape`, `ShapeFace`, `SphereShape` | Shapes |

13 types plus the nested `PhysicsWorld.BodyEnumerator` (14 exported; a test pins the list). The detection (`CoolEngine.Physics.Detection.NarrowPhase`, `BodyGeometry`, `Geometry`) and `Internal.LegacyMath` are internal. A pose is a position, a unit quaternion and half-extents (half the body's size along its own axes; a sphere's three are equal, its radius). `PhysicsWorld.Add`, `SetOrientation` and `PhysicsQueries` take the orientation as given and do not normalize: `Add` rejects a quaternion with `|‖q‖² − 1| > 1e-5` (`ArgumentException`) and `SetOrientation` throws `ArgumentOutOfRangeException` for it (`PhysicsQueries` does not check). Velocity and force are world-space vectors; the library has no velocity frame and no forward axis. The game supplies `PhysicsSettings` as `(0, -1, 0)`, 9.80665, the float product 9.80665 × 9 (not a rounded literal) and 5 sub-steps.

## 3. The step: public phase calls

Denys chose "Public phase calls" (2026-11-27; turned down: two callbacks per sub-step, the mentor's recommendation; a per-body integrator; reading the plan first). The game's rules run between the calls. The mentor added a **run-time order check**: a call in the wrong phase throws `InvalidOperationException` naming the current phase and the call expected next, and `AbortStep` is the way out after an exception inside a sub-step.

| Call | Does | Needs | Leaves |
|------|------|-------|--------|
| `IntegrateVelocities(subStepTime)` | `v + (force / mass + gravityDirection × acceleration) × time` for every dynamic body in slot order | `Idle` | `VelocitiesIntegrated` |
| `ClampTerminalSpeed()` | Today's rule, including the flip of KI-040 | `VelocitiesIntegrated` | `TerminalSpeedClamped` |
| `ClearForces()` | Every dynamic body's force becomes zero; a static body keeps its force | any | unchanged |
| `IntegratePositions(subStepTime)` | `p + v × time` (v in world space), clears the ground flag, rebuilds the geometry | `TerminalSpeedClamped` | `PositionsIntegrated` |
| `ResolveContacts()` | Every pair `i < j` in slot order (static pairs skipped): bounds test, detection, push, ground flag, velocity cancel, geometry of the moved bodies before the next pair | `PositionsIntegrated` | `Idle` |
| `AbortStep()` | Back to `Idle`, nothing else changed | any | `Idle` |
| `Step(timeStep)` | The plain step with no game rule: `timeStep / SubStepCount` per sub-step, the five calls in order, `ClearForces` on the last sub-step | `Idle` | `Idle` |

One sub-step with the game's rules (the test drive `OldTanks.Tests.PhysicsParity.WorldSpaceDrive`, today's order): the forward damping and snap along the heading, `IntegrateVelocities`, the forward clamp, `ClampTerminalSpeed`, on the last sub-step `ClearForces`, `IntegratePositions`, `ResolveContacts`. Running each operation for all bodies before the next keeps today's order; the arithmetic differs by rounding, which is why the proof is a bar and not bits where a body is turned ([§7](#7-how-it-is-proved)). Which calls are allowed when: getters, `RayCast`, `GetState`, the enumerator, `ClearForces`, `AbortStep` in any phase; `SetVelocity`, `SetForce`, `AddForce` until the positions are integrated; `Add`, `Remove`, `SetPosition`, `SetOrientation`, `SetHalfExtents`, `SetMass`, `SetStatic` and the `Settings` setter in `Idle` only. A time argument must be finite and not negative. `SetOrientation` does not turn the velocity: a game that keeps a velocity relative to its heading turns it itself. `SetHalfExtents` allows zero and negative components, as today's sizes are (a negative one mirrors; a zero one gives NaN normals for the faces it collapses), and a sphere's three must stay equal. Setters throw where today's code ignores silently (a velocity or force on a static body), and the ground flag is read-only (mentor's decision, told to Denys).

## 4. Storage and handles

- **One array per field**, indexed by slot; the step walks slots `0 ... SlotCount - 1` and skips free ones; pairs are `i < j` over live slots.
- **Slots are reused last-freed-first** (a stack). After a removal and an add the new body takes the freed slot, so the pair order differs from today's appended list: deterministic, and covered by no golden.
- **`BodyHandle`** is a slot and a generation (1 on first use, plus one on `Remove`, wrapping from `int.MaxValue` to 1, never 0). `default` is never valid. A handle of a removed body is rejected (`ArgumentException`, parameter `handle`) by every member that takes one. **A handle is valid only for the world that returned it:** it does not name its world, so a handle used with another world addresses whatever body that world holds in the same slot with the same generation, and this is not detected (the mentor amended the contract; no code).
- **Growth** only in `Add` (arrays double); a slot keeps its geometry caches for reuse; the step never allocates. `Remove` never allocates.
- **Geometry** (vertices, normals, bounds) is rebuilt at once by every setter that changes it, so `RayCast` and `GetWorldVertices` never see stale data.

## 5. Shapes

Shapes are immutable and shared by reference, safe from any thread (E3: they replace `GlobalCache<CollisionData>` and the static constructors). `PolygonShape` copies its inputs, validates them (at least one vertex, finite data, at least 3 indices per face, indices in range) and finds the edges, edge directions and cuboid axis faces once, with today's loops and tolerances so that ties break as before. Model space spans -1 to 1; indices 0, 1, 2 of a face must be a corner and its two neighbours (the ray test). `SphereShape` holds no data and has a public constructor and no static instance: the radius is the body's X half-extent (a sphere's three half-extents are equal; `Add` and `SetHalfExtents` reject others).

## 6. The two stages

Denys chose two stages for WP2 (2026-11-27; turned down: one stage compared within the golden tolerance). The sections above describe the library as it is since stage 2; this one is the record of what each stage was and what stage 2 replaced.

| Stage | Commit | Body's pose and velocity | Proof |
|-------|--------|--------------------------|-------|
| 1, an exact port (2026-11-28) | `9212d68` | `BodyDescription.Linear` (today's rotation times scale, computed by the game with the `Matrix4` factories; the library formed `CreateTranslation(position) × new Matrix4(linear)`), `Size` (a sphere's width was `Size.X`), `VelocityFrame` (`Matrix3.CreateRotationY(heading).Transposed()`: velocity and force local to the body, as today) | Every parity scene equal to today's code bit for bit; kept in history at `9212d68` |
| 2, quaternions (2026-11-28) | `3c1b624` | A unit `System.Numerics.Quaternion` and half-extents (E2); velocity and force in world space (Q4); the library has no velocity frame | Bits where no body is turned, a bar otherwise ([§7](#7-how-it-is-proved)) |

| Stage 1 member | Stage 2 |
|----------------|---------|
| `BodyDescription.Linear`, `PhysicsWorld.GetLinear`/`SetLinear`, `BodyState.Linear` | `BodyDescription.Orientation` and `BodyState.Orientation`, `PhysicsWorld.GetOrientation`/`SetOrientation`; a non-unit quaternion is rejected |
| `Size`, `GetSize`/`SetSize` | `HalfExtents`, `GetHalfExtents`/`SetHalfExtents` |
| `VelocityFrame`, `SetVelocityFrame` | Gone. The game's drive turns the stored velocity when the heading changes and converts a local force or velocity to world space |
| `PhysicsQueries` `Matrix3` overloads | `ComputeWorldVertices`, `ComputeWorldNormals` and `RayCastNearest` take the orientation and half-extents |

The phase calls (`IntegrateVelocities`, `ClampTerminalSpeed`, `IntegratePositions`, `ResolveContacts`, `ClearForces`, `AbortStep`, `Step`) and `StepPhase` did not change.

**Why stage 2 cannot be equal by bits** (the mentor's reading, confirmed by the reviewer's analysis of 2026-11-28, [§7](#7-how-it-is-proved)): its rules equal today's in exact arithmetic, but a quaternion rotation and a world-space velocity round differently from today's matrices and local velocity, so a turned body differs in the last bits and a contact can amplify that.

**The game's drive rules for stage 2** are still test code until `OldTanks.Simulation` exists (OT-21): `OldTanks.Tests.PhysicsParity.WorldSpaceDrive` replaced the stage-1 `StageOneDrive` (deleted). It converts today's Euler angles to the orientation (`ToOrientation`), today's size to half-extents (`ToHalfExtents`) and a local velocity to world space with the forward clamp (`ToWorldVelocity`); it sums a local force and converts it at the **start of the tick** with the heading that tick runs under (`AddLocalForce`, `RunTick`); it turns the stored velocity when the heading changes (`SetHeading`); and it runs the damping and the forward clamp along the heading around the library's phase calls (`RunTick`). The members' XML comments hold the exact contracts.

## 7. How it is proved

By **side-by-side tests against today's code**: today's `OldTanks.Models.World` and the library run in one process, same machine and build (Q7). Denys chose that WP1's recorded goldens are compared "Within tolerance (Recommended)" (turned down: "Bit for bit"), so the port is proved here, not by WP1's literals.

**The bar of stage 2 (Denys, 2026-11-28, asked with the measured difference table):** "1 mm, 190 of 200 (Recommended)". It means:

| Scenes | Must hold |
|--------|-----------|
| The 10 scenes with no turned body (the default scene, negative zero and a zero time step, and eight of the ten golden scenes) | Equal to today's code by bits |
| The other named scenes (11) | Within 1 mm of today's positions on every tick (`OldTanks.Tests.PhysicsParity.SceneComparison.PositionBound`, 0.001 m) |
| The 200 seeded random scenes | At least 190 within 1 mm |
| Ground-flag differences | Printed, not asserted |

Turned down: 0.2 mm and 199 of 200; report only (no assertion); dropping stage 2. A bar exists at all because bit equality is impossible for a turned body.

| Where | What |
|-------|------|
| `tests/OldTanks.Tests/PhysicsParity` (27) | `PhysicsParityTests` (15): the default scene (1,200 ticks), terminal speed, forces, headings and jump, dynamic against dynamic, a body made static and dynamic again, remove and add, negative-zero coordinates and a zero time step, zero limits with a backward force on the ground, a body hit while static, a force and a heading change before the same tick, a velocity written after a heading change (and its direct test), the 200 seeded random scenes (at least 190 within 1 mm), the drive's domain check, seeded rays against today's nearest-hit loop (measured, not asserted). `GoldenSceneParityTests` (11): each stepped golden scenario of WP1 through today's `World` and the library, and a check that the scenes mirror the goldens. `Stage2DifferenceTests` (1): prints the whole difference table to the test output and asserts nothing. Helpers: `SceneComparison`, `SceneDescription`, `OldWorldRunner`, `LibraryRunner`, `WorldSpaceDrive` |
| `tests/CoolEngine.Tests` `DetectionParityTests` (3) | Seeded pairs of cube, triangulated cube, tetrahedron and sphere against today's `Collision.CheckCollision` (20,000 pairs); two spheres exactly touching on an axis; 20,000 seeded rays. Both sides test the same world geometry, the one today's `UpdateCollision` computes, so detection stays equal by bits; stage 1's fourth test, of the transforms against `UpdateCollision`, is gone with `Linear` |
| `tests/CoolEngine.Physics.Tests` (142) | The ported detection and geometry tests (geometry now from a quaternion and half-extents), shapes, handles, settings, the phase rules and their errors, the step (a drop onto a floor, the flip, the ground flag, forces, determinism: the same scene twice gives identical bits; two worlds on two threads), the orientation and half-extent rules, allocations (0 B), the exported types, no static field |

**The measured difference of stage 2 against today's code** (the table `Stage2DifferenceTests` prints; Debug and Release print the same lines; the implementer's run of 2026-11-28, reproduced by the reviewer; the test also passed in the mentor's combined run):

| Group | Result |
|-------|--------|
| Bit-equal | 10 scenes |
| Within the bound, named scenes | 11 scenes; largest 9.90e-5 m (dynamic against dynamic, with 28 ground-flag body-ticks differing); every other at most 3.95e-6 m |
| 200 seeded random scenes | Median 8.60e-6 m, 90th percentile 1.66e-5 m, maximum 0.358 m (seed 4010, a sphere resting on a tilted dynamic cube, 13 ground-flag body-ticks, first above 1 mm at tick 56); 199 of 200 within 1 mm (190 required) |

**The reviewer's analysis** (`opus` at `max`, 2026-11-28): the stage-2 rules equal today's in exact arithmetic; seed 4010 is rounding amplified by a contact; deliberate defects drop the random scenes within 1 mm to between 2 and 163 of 200; a pure rounding change moves the count from 199 to 195, so the bar of 190 sits about 5 to 10 scenes above noise. A defect that only swaps two limits or drops the multiplier is caught by the scene "velocity written after a heading change" (0.843 m and 1.68 m) and its direct test.

**The mentor's engineering notes** (told to Denys, not his decisions): a local force is converted at the start of the tick, not when it is given. The analyst's prototype converted it at the event, which was a bug in the prototype and the reason its preview (2026-11-27: 200 random scenes with a 90th percentile of 1.2 m and a maximum of 7.2 m) predicted about 30 of 200 scenes diverging. Measure the real code before asking for a bar.

**What stage 1's proof showed** (`9212d68`, `opus` at `max`, 2026-11-28): the port read side by side expression by expression, Debug and Release; 13 of the 15 mutants the mentor listed were caught at first, and the nine survivors of the later run were caught after. A reassociated position update made 8 parity tests fail, so the harness saw one bit. The exact domain of the drive: `MaxSpeed × MaxSpeedMultiplier ≥ -MaxBackSpeed` and finite inputs (today's setter clamps up to three times per sub-step, the drive once; `WorldSpaceDrive.Register` and `SetLimits` reject limits outside it). The position stores skip an equal value as today's `SetField` does (a zero displacement leaves a -0 coordinate); the library's internal stores do.

## 8. Benchmarks

`tests/CoolEngine.Physics.Benchmarks` (BenchmarkDotNet, `[MemoryDiagnoser]`; the old categories and the reference to `OldTanks` are transitional until the old code goes). Release, one pinned core, 3 launches, 2026-11-28; the implementer's run, its medians checked by the reviewer against the log; medians (the BenchmarkDotNet Ratio column is mean-based):

| Category | Today's median | Library median | Ratio |
|----------|----------------|----------------|-------|
| Cube against cube | 324.59 µs | 307.37 µs | 0.95 |
| Sphere against cube | 56.39 µs | 52.48 µs | 0.93 |
| Geometry (quaternion and half-extents against today's matrix path) | 60.88 µs | 48.41 µs | 0.80 |
| Step, with the drive | 15.81 µs | 4.08 µs | 0.26 |
| Step, plain | 15.81 µs | 3.61 µs | 0.23 |

- **Sphere against cube** is bimodal on both sides (launch modes near 37 µs and near 52 to 57 µs).
- **"1 B" in the Allocated column** is the harness's fixed 336 B per launch divided by the operation count, present on today's baselines too; the library allocates nothing per operation and its allocation tests assert 0 B.
- **Where the gain comes from** (the reviewer's reading, 2026-11-28, about half of the gap accounted for): today's tick rebuilds every transform about 20 times with sin and cos, calls `CreateRotationY` 10 times in `Move`, takes the `GlobalLock` 10 times and raises property-changed on every set, while the library takes the orientation and half-extents as inputs. **The game will see less than this gain**, because it still pays for the inputs and for the snapshot.
- The 1,000-body stress scene (`Step_Stress1000`) was not part of this run.

## 9. Known limits

- The ported quirks, recorded and not fixed (Denys, 2026-11-27: "Port exactly, record KIs"): [KI-040](../KNOWN-ISSUES.md#ki-040-the-terminal-speed-clamp-flips-a-fast-upward-speed-to-falling), [KI-041](../KNOWN-ISSUES.md#ki-041-the-ground-flag-comes-from-the-direction-between-the-bodies-centres), [KI-042](../KNOWN-ISSUES.md#ki-042-inside-one-body-the-first-face-in-list-order-wins-a-ray-test), [KI-043](../KNOWN-ISSUES.md#ki-043-a-sphere-deep-inside-another-sphere-gets-a-wrong-depth), [KI-044](../KNOWN-ISSUES.md#ki-044-a-ray-that-starts-inside-a-cube-reports-no-hit), [KI-045](../KNOWN-ISSUES.md#ki-045-a-body-under-a-static-ceiling-sticks-until-gravity-uses-up-its-upward-speed), [KI-046](../KNOWN-ISSUES.md#ki-046-stacked-dynamic-bodies-never-settle-and-the-lower-one-ends-each-tick-off-the-ground), [KI-047](../KNOWN-ISSUES.md#ki-047-two-spheres-with-exactly-the-same-centre-get-nan-positions).
- One thread at a time per world, not detected. A handle of another world is not detected.
- A velocity that overflows makes `SetVelocity` throw in the middle of a sub-step: the caller needs `AbortStep` (the notes for OT-21 in [BACKLOG](../BACKLOG.md#planned)).
- **A static body whose heading changes keeps its frozen world velocity unturned:** the library refuses velocity writes on a static body, while today's local velocity turns with the body. What happens when it becomes dynamic again needs a rule, and that rule is Denys's to give (open for OT-21). The library's tests cover such a body's geometry only.
- `PhysicsQueries` does not check that an orientation is a unit quaternion (`Add` and `SetOrientation` do). `PhysicsWorld.SetVelocity` stores exactly: a game that clamps velocities, as today's setter does, must clamp before it.
- No broad phase (the pair test is quadratic) and no multi-threaded step (Q8, later tasks); no collision response beyond the push and the vertical velocity cancel.
- The library gives the same bits for the same inputs only on the same machine and build (Q7), because `Vector3.Dot`, `Length` and `Normalize` may differ between instruction sets.
- Equality with today's code by bits holds only where no body is turned ([§7](#7-how-it-is-proved)).
- Units of the library's inputs: metres, seconds, kilograms, newtons and a quaternion, no angles; the rules for the new code around it are [UNITS-STANDARDIZATION](../plans/UNITS-STANDARDIZATION.md).
