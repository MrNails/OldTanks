# Backlog

| Field        | Value  |
|--------------|--------|
| Status       | Living - open work, unwired features, debt |
| Last updated | 2026-11-25 |
| Verified against | 2026-11-25; feature/OT-3, uncommitted; the Platform and build section and the OT-3 items against the code; the other sections as of 2026-11-24 (develop at d32cbbf) |

---

Open work that is not a defect. Defects are in [KNOWN-ISSUES](KNOWN-ISSUES.md). One bullet per item; when an item gets a task number, add `(OT-<n>)`; when it is done, strike it through and add the branch or PR. Nothing here has been agreed or prioritised yet: the order inside each section is only a suggestion.

## Ticketed

- ~~**OT-1 Documentation start**~~ (`feature/OT-1`, 2026-11-24): this knowledge base, the `AGENTS.md` guides, the README and the docs test project. Merged into `master` by PR #8.
- ~~**OT-2 Mentor pipeline**~~ (`feature/OT-2`, 2026-11-24): the project's `.claude/skills/mentor-pipeline/`, and `master` as the working branch in the docs. Merged into `master` by PR #9.
- **OT-3 .NET 10, central packages, `CoolEngine.Imaging`** (`feature/OT-3`, 2026-11-24 to 2026-11-25): the retarget, the package upgrade, the unused packages, the incremental generator and the own image decoders ([session log](sessions/SESSION-LOG.md)). Open until merged into `master`.
- **OT-4 Own fonts** (`CoolEngine.Fonts`) and **OT-5 own math** (`CoolEngine.Mathematics`): fixed by Denys, see below.

## Planned

- **Engines and the tank game** ([plan](plans/ENGINES-AND-TANK-GAME.md), draft): separate physics, render and UI engines, a shared core, and the online game on them. Not ticketed as a whole; D1-D4 and D7-D13 decided, D5 and D6 (names, determinism) open.
- **Font and skybox shader bug task** ([KI-020](KNOWN-ISSUES.md#ki-020-three-shaders-fail-to-compile-and-are-used-anyway)): rename the `texture` input and uniform in `FontShader`, `3DFontShader` and `SkyBoxShader`, check the link status in `Shader.Create`. A `bug/OT-<n>` task after OT-3 (Denys: "Separate bug task").

## Platform and build

- ~~**Retarget from `net7.0`.**~~ Done in OT-3 (`net10.0`, `LangVersion latest`, every package at its latest stable version without advisories on 2026-11-24, [ARCHITECTURE §1](ARCHITECTURE.md#1-solution-layout)); the natives of OpenTK, ImGui.NET and FreeTypeSharp loaded in the 2026-11-25 smoke run.
- ~~**Upgrade SixLabors.ImageSharp 3.1.3.**~~ Done in OT-3 by removing it from the engine: `CoolEngine.Imaging` ([IMAGE-DECODERS](CoolEngine.Imaging/IMAGE-DECODERS.md)); ImageSharp 4.1.4 remains in the test project only.
- ~~**Remove unused packages:**~~ MIConvexHull and `Serilog.Sinks.FastConsole` removed in OT-3.
- **A Six Labors community key for the test project.** `tests/CoolEngine.Imaging.Tests` references ImageSharp 4.1.4, which needs a license key at build time: Debug builds warn, Release builds of the solution fail until a `sixlabors.lic` file or the `SixLaborsLicenseKey` property supplies one ([BUILD §2](BUILD.md#2-build)).
- **Clear the warning baseline** (135 compiler warnings on a full build since OT-11, mostly nullable, [BUILD §2](BUILD.md#2-build)), or at least stop adding to it.
- ~~**Move the generator to `IIncrementalGenerator`.**~~ Done in OT-3 ([BINDING-GENERATOR](OldTanks.UI.SourceGenerators/BINDING-GENERATOR.md)); its review finding, [KI-021](KNOWN-ISSUES.md#ki-021-generated-binding-code-calls-equals-before-its-null-check), was fixed on `bug/OT-11`.
- **Stop tracking `OldTanks.slnx.DotSettings.user`.** It is a per-user Rider file.
- ~~**Docs check.**~~ Done in OT-1: `tests/OldTanks.Docs.Tests` ([DOCUMENTATION-GUIDE §9](DOCUMENTATION-GUIDE.md#9-checks)).
- **Add code tests and CI.** No game or engine code is tested and there is no workflow. The first useful tests are pure logic: `Collision` tests, `RigidBody.OnTick`, the OBJ loader on the repository's models, `SettingsService`, and a snapshot of the binding generator's output (KI-021 showed only as compiler warnings). A CI workflow would run `dotnet build` and `dotnet test` on pull requests.

## Unwired features

Controls and settings that exist but do nothing yet:

- **`F`** toggles `CoolEngine.Services.EngineSettings.PhysicsEnable`, which nothing reads.
- **`O`** toggles `MainWindow.m_drawFaceNumber`, which nothing reads (face numbers are never drawn).
- **HUD** "Have collision" and "Speed" are never filled in.
- **Debug window** "Push force" and "Camera free mode" are not bound or handled.
- **`OldTanks.Services.GameManager`**: `IsDebugView`, `FreeCameraMode`, `DrawNormals`, `DrawFaceNumbers` duplicate `MainWindow` fields and are not used; `HandleCollisions` and `Draw` are empty.
- **`World.TransformableActions`** (a queue of `TransformableAction`) is never written or read.
- **`TextControl` alignment** properties are never read.
- **Stress spawner** (`MainWindow.SpawnObjects`, 10 000 random cubes) returns at its first line.

## Game and engine features not built

- **A player and a tank.** Assign `World.Player` and make the follow camera work ([KI-001](KNOWN-ISSUES.md#ki-001-the-player-is-never-assigned)); after that, a tank model, its controls and game rules.
- **Collision response:** use `RigidBody.Restitution` (bounce), friction between bodies, angular response; `Density` and `CenterOfMass` are stored but unused ([PHYSICS §2](CoolEngine/PhysicEngine/PHYSICS-AND-COLLISIONS.md#2-the-rigid-body)).
- **Broad phase:** the pair test is O(n²) per sub-step; a grid or sweep-and-prune is needed before many objects.
- **Lighting:** normals are uploaded but no shader uses them.
- **Materials:** the OBJ loader skips `mtllib` and `usemtl`.
- **Levels:** `CoolEngine.Models.Level` and `CoolEngine.PhysicEngine.Core.WorldSettings` are unused sketches.
- **Retire `ObjectRendererOld`:** move the skybox and debug primitives to their own renderers (`SkyBoxRenderer` is an empty placeholder) and drop the per-type drawable lists the game still keeps in sync.

## Dead code to remove or finish

- `CoolEngine.GraphicalEngine.Core.ObjectModel` (its constructor wraps its own still-null field and would throw), `DrawSceneInfoOld` (kept only for `ObjectRendererOld`), `CoolEngine/Services/Misc/RenderGroup.cs` and `MeshGroup.cs` (fully commented out).
- `OldTanks.UI.Services.Binding<TObject, TObjectData, TControlData>` and `OldTanks.UI.GlobalSettings` (unused).
- `OldTanks.Models.Robot` ([KI-007](KNOWN-ISSUES.md#ki-007-robot-cannot-be-constructed)), the unused `Monke.obj`, `SkyBox1.png`, `Fonts/Arial/` and `3DFontShader`.

## Own libraries and engine direction

Decided by Denys on 2026-11-24 ([plan §6](plans/ENGINES-AND-TANK-GAME.md#6-decisions-of-ot-3-and-the-planned-tasks) has scope, settled answers and open questions). Each task gets its `OT-<n>` when its branch is created; the order is planned, only OT-4 and OT-5 are fixed.

- **OT-4 `CoolEngine.Fonts`:** replaces FreeTypeSharp. Every font format except legacy and language-specific ones, full TrueType hinting, tests against FreeType and Windows (DirectWrite). Plan questions: colour fonts, variable-font axes, CFF stem hints. It also lets the FreeTypeSharp 3 port in `CoolEngine.GraphicalEngine.Core.Font.Font` go.
- **OT-5 `CoolEngine.Mathematics`:** replaces `OpenTK.Mathematics`. Column vectors, column-major storage, clip depth [0, 1], reversed Z, `float` and `double`, 128-bit SIMD; every math change reviewed at effort `max` by `opus` or `fable`.
- **`CoolEngine.OpenGL`:** a Roslyn source generator over `gl.xml` replaces `OpenTK.Graphics` (62 distinct `GL.*` functions in 18 files).
- **`CoolEngine.Platform`:** a Win32 window, WGL context and input replace `OpenTK.Windowing` and GLFW; one window; gamepads in a later task.
- **An own deflate decoder** for the PNG decoder, with full stream-end and Adler-32 checks (Denys: "Document, backlog it"; [KI-023](KNOWN-ISSUES.md#ki-023-png-decoding-cannot-see-a-zlib-streams-missing-end)).
- **The API-neutral render boundary** (D8) and the far-future WebGPU browser client (D9): guidance for the engine plans, no task yet.
