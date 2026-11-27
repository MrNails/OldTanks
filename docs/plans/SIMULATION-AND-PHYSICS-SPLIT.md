# Simulation and Physics Split

| Field        | Value  |
|--------------|--------|
| Status       | Active plan - Denys's decisions of 2026-11-27 recorded (§6); no task started; sub-step count and backlog cap confirmed after WP1 |
| Last updated | 2026-11-27 |
| Verified against | 2026-11-27; master at 8c7e72e; §2 against the code and the open KNOWN-ISSUES entries (reasoned from code, not run) |

---

Denys asked for this plan on 2026-11-27 ("Yes, draft it"), after Claude's review of the architecture, and answered its questions the same day (§6). It is the first concrete slice of the "core and physics" phase of [ENGINES-AND-TANK-GAME §4](ENGINES-AND-TANK-GAME.md#4-proposed-shape-not-agreed). No mechanic, constant or formula is invented: where the code leaves a gap, §6 holds Denys's answer.

## 1. Goal

One owner for the simulation's state, so that the same simulation can run on a server without OpenGL ([D2](ENGINES-AND-TANK-GAME.md#5-open-decisions)).

| Outcome | Meaning |
|---------|---------|
| **Headless physics library** | Bodies, shapes, the step loop and ray casts in a project that references only `CoolEngine.Mathematics` |
| **Fixed tick** | The simulation advances 60 equal steps a second, not by the time a `Thread.Sleep(5)` loop happened to take |
| **Two channels between threads** | Commands go in, snapshots come out; no lock is shared and nothing else crosses |
| **The game object split** | A simulation entity (an id, a body handle, game data) and a GL-thread view of it |

**Not a goal:** new behaviour. Today's motion is reproduced and pinned by golden values recorded on today's code, the way OT-5 moved the math ([MATHEMATICS](../CoolEngine.Mathematics/MATHEMATICS.md)).

## 2. What is wrong today

| # | Finding | Consequence |
|---|---------|-------------|
| 1 | `OldTanks.Models.WorldObject` is the transform, the render instance, the physics body, the camera target and the binding source at once | Every subsystem reads and writes the same mutable fields |
| 2 | The physics thread (`OldTanks.Windows.MainWindow.WorldHandler`) steps by measured time, and also reads the keyboard and mouse, moves the free camera, writes HUD text and reads `ClientSize` | Results depend on timing; [KI-036](../KNOWN-ISSUES.md#ki-036-the-physics-thread-reads-the-windows-client-size-through-glfw) |
| 3 | The GL thread and the physics thread both change objects; `MainWindow.m_locker` and `EngineSettings.Current.GlobalLock` cover parts only ([ARCHITECTURE §6](../ARCHITECTURE.md#6-synchronization)) | [KI-003](../KNOWN-ISSUES.md#ki-003-worldobjects-is-changed-across-threads-without-a-shared-lock), [KI-031](../KNOWN-ISSUES.md#ki-031-the-collision-debug-view-transforms-objects-on-the-gl-thread-while-the-physics-thread-does) |
| 4 | `WorldObject` and `RigidBody` raise `PropertyChanged` on the physics thread in every sub-step; `OldTanks.UI.ImGuiUI.MainWindow` subscribes its generated bindings to both | The selected object's control values are written from the physics thread while the GL thread draws them |
| 5 | The step loop and the contact resolution are game code, `OldTanks.Models.World.CollideObjects` ([PHYSICS §3](../CoolEngine/PhysicEngine/PHYSICS-AND-COLLISIONS.md#3-the-step-loop-worldcollideobjects)) | The engine has detection tests and a property bag, no step |
| 6 | `CoolEngine.PhysicEngine.Core.RigidBody` carries game rules (`MaxSpeed`, `MaxBackSpeed`, `MaxSpeedMultiplier`, `DefaultJumpForce`, forward damping, velocity local to the object), derives from `Common.Models.ObservableObject` and reads the global `CoolEngine.Services.EngineSettings.Current.CollisionIterations` | Not project-agnostic ([requirements](ENGINES-AND-TANK-GAME.md#2-requirements)) |
| 7 | Physics sits in the `CoolEngine` assembly (OpenTK, Serilog and, since OT-4, `CoolEngine.Fonts`; FreeTypeSharp is gone from the engine); a `Collision` reads its owner's render transform through `CollisionData.PhysicObject`; shapes are registered in `GlobalCache<CollisionData>` by static constructors | It cannot run headless as D2 requires |

## 3. Target shape

| Part | Owns | Runs on | Knows nothing about |
|------|------|---------|---------------------|
| `CoolEngine.Physics` | Bodies, shapes, the step, ray casts | Whichever thread calls its step | OpenGL, the window, UI, threads, the game |
| `CoolEngine.Core` | The fixed-tick clock, the command queue, the snapshot buffer | — | Physics, the game |
| `OldTanks.Simulation`, headless | Entities (id, body handle, game data: drive limits, `Restitution`, `Density`, `CenterOfMass`, the name); applies commands, runs the drive step and game rules, steps physics, writes the snapshot | Its own thread on the desktop client (Q9); the server's loop later | OpenGL, the window, UI |
| Presentation (`OldTanks`) | The window, input sampling, the free camera and its pick ray, renderers, debug windows; `WorldObject` becomes the GL-thread view of one entity | The GL thread | Physics internals |

- **One writer:** only the simulation changes simulation state.
- **Commands in:** player input, debug-window edits, spawn, delete, pause, single step. Each carries a sequence number and is applied at the start of the next tick.
- **Snapshots out:** per tick, the tick number, the last command sequence applied, each entity's position, orientation, size, velocity and flags, and the step statistics the HUD shows. The GL thread draws the newest one as it is (Q6).
- **No thread assumption:** the simulation never asks which thread it is on, so a server or a browser host can call it inline ([plan §6](ENGINES-AND-TANK-GAME.md#6-decisions-of-ot-3-and-the-planned-tasks), "No worker threads").

## 4. Engineering choices

Proposed by Claude and accepted by Denys as amended (2026-11-27, §6). E3, E7, E8 and E9 are the amended ones; E10 became Q12.

| # | Choice |
|---|--------|
| E1 | Bodies in struct arrays (one array per field), addressed by a `BodyHandle` (slot and generation); slots are stable and reused through a free list, and the step iterates in slot order. **Warning:** after a delete and a spawn the new body takes the freed slot, so the pair order differs from today's appended list; deterministic, covered by no golden |
| E2 | A body holds position, a unit-quaternion orientation and half-extents; the game converts its Euler angles (today's transform order, [ARCHITECTURE §7](../ARCHITECTURE.md#7-spaces-and-units)). **Warning:** with Q4 this changes the arithmetic, the likeliest cause of a golden difference; hence WP2's two stages |
| E3 | Shapes are immutable objects shared by reference, safe to read from any thread; this replaces `GlobalCache<CollisionData>` and the static-constructor registration |
| E4 | The detection code of `CoolEngine.PhysicEngine.Core.Collision.Collision` moves over as static functions on spans; its 29 tests move with it ([PHYSICS §4](../CoolEngine/PhysicEngine/PHYSICS-AND-COLLISIONS.md#4-detection)) |
| E5 | The library has no statics, no `INotifyPropertyChanged`, no logger and no `Common` reference; gravity, terminal speed and the sub-step count come in a settings value, with today's numbers supplied by the game |
| E6 | The snapshot buffer is a triple buffer: the writer never waits and its one reader, the GL thread, always gets the newest complete snapshot. **Warning:** a `Thread.Sleep` on Windows can be as coarse as about 15.6 ms, so ticks would arrive in bursts and show as judder under Q6; the clock paces with a high-resolution wait and WP3 reports the measured tick jitter |
| E7 | `WorldObject` stays the drawable and the binding source, refreshed from the snapshot once per update frame on the GL thread; the refresh never sends a command, a setter called by the UI does. **An edit is acknowledged:** the view keeps an edited value until a snapshot reports that command's sequence as applied, so an older snapshot cannot overwrite what was just typed. The generator is untouched; the binding attributes that name `RigidBody` get the view's type name |
| E8 | The free camera moves on the GL thread per frame (it is not simulation state). Its pick ray is tested there against the newest snapshot with a pure function of the physics library; the simulation, and later the server, never hears of the debug camera |
| E9 | The collision debug view (key `0`) computes world-space vertices from the snapshot's transforms with a pure function of the physics library, and the renderer takes plain vertex and index arrays: `CoolEngine` gets no reference to `CoolEngine.Physics` |

## 5. Work packages

Two tasks (Q1): **`feature/OT-19`** holds WP1-WP3, where nothing in the game changes; **`feature/OT-20`** holds WP4-WP5. One commit per change, each building green. Tiers follow the [mentor pipeline](../../.claude/skills/mentor-pipeline/SKILL.md); the mentor's own plan for each package comes next.

| WP | Content | Tier | The game afterwards |
|----|---------|------|---------------------|
| **WP1 Goldens** | Tests only, on today's code: the default scene stepped through `World.CollideObjects` at 1/60 s with 5 sub-steps, recording positions, velocities and `OnGround` at fixed ticks; ray hits against cubes and spheres. **Also the sub-step comparison (Q3):** the default scene and a terminal-speed drop at 1, 2, 3, 5, 8 and 10 sub-steps, reporting penetration depth, resting jitter and cost per tick; Denys confirms the count and the backlog cap before the goldens are frozen | Standard | Unchanged |
| **WP2 Physics library** | `CoolEngine.Physics` with E1-E5, a benchmark project and KI entries for the three quirks of Q5. **Stage 1 (one commit):** the step ported from `World.CollideObjects`, `RigidBody.OnTick` and `WorldObject.Move` with today's exact arithmetic (the game's matrix, local velocity); WP1's goldens must be equal bit for bit. **Stage 2 (one commit):** quaternions (E2) and world-space velocity (Q4); the measured difference from the goldens goes to Denys | High | Unchanged; nothing uses the library yet |
| **WP3 Machinery** | `CoolEngine.Core`: the fixed-tick clock (pause, single step, the backlog cap, high-resolution pacing), the command queue with sequence numbers, the snapshot buffer (E6) | High | Unchanged |
| **WP4 The sandbox switches** | `OldTanks.Simulation`; `World` steps through it on its own thread (Q9); `WorldObject` as the view with acknowledged edits (E7); input sampled on the GL thread, the free camera, its pick ray and the edge-turn rule of Q11 there (E8); debug-window edits, Spawn and Delete as commands; the debug view from the snapshot (E9); the pause on an exception (Q12); `m_locker` and `GlobalLock` removed | High | Same scene and controls, on the new structure |
| **WP5 Removal** | Delete `CoolEngine/PhysicEngine/` and the interfaces only it used; move the physics page; update the docs of the update triggers | Mechanical | Unchanged |

**Tests the high-tier packages must name** (the skill's rule for threading and hot paths):

| Test | Checks |
|------|--------|
| Snapshot buffer, one writer against one reader | No torn snapshot (each carries a checksum of its tick), ticks never go backwards |
| Command queue, several producers against one consumer | Every command applied exactly once, in each producer's order |
| Stop while a tick runs | The simulation thread ends within a bound and nothing is left running |
| Pause and single step | No tick while paused; exactly one per step command; an exception pauses (Q12) |
| Same scene and commands run twice | Identical bits (Q7) |
| An edit against older snapshots | The view keeps the edited value until its command is acknowledged (E7) |
| Tick pacing | The measured spread of tick start times, reported (E6) |
| Benchmark of the step | The default scene and a 1 000-body stress scene; 0 bytes allocated per tick after warm-up; detection no slower than today's 0.42-0.44 µs per cube × cube test, on the same machine |

## 6. Decisions

Denys, 2026-11-27, each asked with a recommendation; quotes are his words where he wrote his own.

| # | Decision | Turned down |
|---|----------|-------------|
| Q1 | Two tasks: `feature/OT-19` for WP1-WP3, `feature/OT-20` for WP4-WP5 | One task with five packages; one task per package |
| Q2 | Three projects: `CoolEngine.Physics`, `CoolEngine.Core`, `OldTanks.Simulation` (this decides part of [D5](ENGINES-AND-TANK-GAME.md#5-open-decisions)) | Two projects, the machinery inside `OldTanks.Simulation` until a second user exists |
| Q3 | 60 ticks per second: "I think 60Hz it is "Golden Standard" for simulation frequency. But question is about sub-steps. Do 5 steps will be enough?" Claude's answer, computed from the code's constants and not measured: at the terminal fall speed (88.3 m/s) a body moves 0.29 m per sub-step of 1/300 s, about a third of what pushes a 1 m body through the 1 m floor the wrong way; 1.47 m with one sub-step. Then: 5 sub-steps, checked by WP1's comparison before the goldens are frozen. **Not answered separately:** the backlog cap, 0.25 s as proposed; confirmed with the sub-step count | 30 or 120 ticks; measuring before deciding; 5 without a comparison |
| Q4 | `CoolEngine.Physics` keeps world-space velocity; a drive step in `OldTanks.Simulation` reproduces today's forward damping, speed limits and "velocity turns with the heading" ([PHYSICS §2](../CoolEngine/PhysicEngine/PHYSICS-AND-COLLISIONS.md#2-the-rigid-body)) | Local-space velocity and the limits in the library, as `RigidBody` |
| Q5 | Port the three quirks exactly and record each as a `KI`: the terminal-speed clamp flips a fast upward speed to falling; the ground flag comes from the direction between centres; in one body the first face in list order wins a ray test | Fix them in the move |
| Q6 | The GL thread draws the newest snapshot: "Newest snapshot. Write Interpolate as finding with review later when actual issue of newest snapshot appears and I'll decide to fix" ([BACKLOG](../BACKLOG.md#game-and-engine-features-not-built)) | Interpolating between the two newest snapshots now (Claude's recommendation) |
| Q7 | The same scene and commands give the same bits on the same machine and build, tested; "every x64 CPU" is a later goal (it needs a trig-free step, [MATHEMATICS §3](../CoolEngine.Mathematics/MATHEMATICS.md#3-the-same-bits-promise)) | Every x64 CPU now; no promise |
| Q8 | The broad phase and a multi-threaded step are later tasks, each with a benchmark: both change the pair order or the boxes a pair is tested with | The broad phase in this plan; both in this plan |
| Q9 | The desktop client runs the simulation on its own thread; the same code stays callable inline | Inline in the update frame |
| Q10 | `Restitution`, `Density` and `CenterOfMass` are game data of the entity, `CenterOfMass` keeps its debug-window control; the library takes them when a response spec uses them | Carrying them into the library unused; dropping them |
| Q11 | The mouse-look edge zones keep today's speed. Today they turn by 3 units horizontally and 2 vertically (times the sensitivity) per physics loop, so the speed follows the loop rate. Before WP4 Denys reads the HUD's "Collision check per second" on his machine; the rule becomes 3 and 2 times that number per second | Per rendered frame; a rate of Denys's own |
| Q12 | After an exception the simulation loop logs it once at `Error` and pauses as key `U` does; `U` or `I` resumes | Log and continue, as today; log at `Fatal` and end the game |
| E1-E10 | "Accept all if you don't have any idea, warning, objection". Claude's second look gave four amendments (E3, E7, E8, E9), three warnings (E1, E2, E6) and the gaps Q11 and Q12; Denys then took the amended set (§4) | The ten as first written; discussing some first |
| WP2 | Two stages: an exact port first, then the representation changes with the measured difference | One stage compared within the golden tolerance |

## 7. Out of scope

Each is its own later task; the first three wait for Denys's specifications.

- **Collision response:** bounce, friction, rotation response, sleeping.
- **A player and a tank** ([KI-001](../KNOWN-ISSUES.md#ki-001-the-player-is-never-assigned)); the dead player controls move over unchanged and stay unreachable.
- **Networking and the server project.**
- **The broad phase and a parallel step** (Q8); **interpolation between snapshots** (Q6).
- **Rendering:** `ObjectRenderer<T>`, the static renderers and `GlobalCache<T>` for render assets stay as they are.
- **The rest of `OldTanks.Windows.MainWindow`:** only the simulation loop and the input sampling leave it.

## 8. Acceptance

- **Build:** `dotnet build OldTanks.slnx` with 0 errors and no more than 129 warnings.
- **Tests:** `dotnet test OldTanks.slnx`, the 857 of today plus every package's new tests, all passed. WP2's stage 1 equals WP1's goldens bit for bit; stage 2's difference is measured and goes to Denys. A difference in stage 1 stops the work; no check is loosened.
- **App check against `master`:** the default scene settles the same; keys `0`, `N`, `U`, `I`; debug-window edits of position, rotation, size and the body fields; Spawn and Delete pressed quickly with no exception and no `Error` line; the edge-zone turn of Q11.
- **Closed by WP4:** KI-003, KI-031, KI-036.
- **Docs:** the pages of the [update triggers](../DOCUMENTATION-GUIDE.md#8-update-triggers): ARCHITECTURE §1-§6, a guide and a feature page per new project, the physics page, BUILD, the glossary, the root guide's maps.

## 9. Risks

| Risk | Answer |
|------|--------|
| A resting contact amplifies a last-bit difference over hundreds of ticks | Stage 1 keeps today's order of operations and must be exact; stage 2's difference is measured, not assumed |
| Without interpolation, motion may step or judder when the frame rate and the tick rate differ or drift | Denys's choice (Q6); the finding is in BACKLOG for his review when it shows |
| A debug-window edit of a moving body is overwritten by the simulation one tick later | The same contest exists today under the lock; the acknowledgement of E7 covers the wait, not the contest |
| `feature/OT-4` is open and also edits `OldTanks.slnx` and `CoolEngine.csproj` | Rebase whichever merges second |
