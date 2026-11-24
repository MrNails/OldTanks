# Backlog

| Field        | Value  |
|--------------|--------|
| Status       | Living - open work, unwired features, debt |
| Last updated | 2026-11-24 |
| Verified against | 2026-11-24; develop at d32cbbf; whole page |

---

Open work that is not a defect. Defects are in [KNOWN-ISSUES](KNOWN-ISSUES.md). One bullet per item; when an item gets a task number, add `(OT-<n>)`; when it is done, strike it through and add the branch or PR. Nothing here has been agreed or prioritised yet: the order inside each section is only a suggestion.

## Ticketed

- ~~**OT-1 Documentation start**~~ (`feature/OT-1`, 2026-11-24): this knowledge base, the `AGENTS.md` guides, the README and the docs test project. Merged into `master` by PR #8.
- **OT-2 Mentor pipeline** (`feature/OT-2`, 2026-11-24): the project's `.claude/skills/mentor-pipeline/`, and `master` as the working branch in the docs. Open until merged into `master`.

## Planned

- **Engines and the tank game** ([plan](plans/ENGINES-AND-TANK-GAME.md), draft): separate physics, render and UI engines, a shared core, and the online game on them. Not ticketed; D1-D4 decided, D5-D7 (names, determinism, math library) open.

## Platform and build

- **Retarget from `net7.0`.** .NET 7 has been out of support since May 2024, and the PC used on 2026-11-24 had only the .NET 8, 9 and 10 runtimes ([BUILD §3](BUILD.md#3-run)). `net10.0` matches the installed SDK; check OpenTK, ImGui.NET and FreeTypeSharp natives on the new target.
- **Upgrade SixLabors.ImageSharp 3.1.3.** The restore reports known high (NU1903) and moderate (NU1902) vulnerabilities. Images come only from `Assets/` and from paths typed into the texture window, so the exposure is low, but the fix is a package bump.
- **Remove unused packages:** MIConvexHull (no code uses it) and, unless the commented-out sink comes back, `Serilog.Sinks.FastConsole`.
- **Clear the warning baseline** (about 140 nullable warnings on a full build, [BUILD §2](BUILD.md#2-build)), or at least stop adding to it.
- **Move the generator to `IIncrementalGenerator`.** The v1 `ISourceGenerator` API is deprecated in current Roslyn; a draft is commented out at the end of `BindingGenerator.cs`.
- **Stop tracking `OldTanks.sln.DotSettings.user`.** It is a per-user Rider file.
- ~~**Docs check.**~~ Done in OT-1: `tests/OldTanks.Docs.Tests` ([DOCUMENTATION-GUIDE §9](DOCUMENTATION-GUIDE.md#9-checks)).
- **Add code tests and CI.** No game or engine code is tested and there is no workflow. The first useful tests are pure logic: `Collision` tests, `RigidBody.OnTick`, the OBJ loader on the repository's models, `SettingsService`. A CI workflow would run `dotnet build` and `dotnet test` on pull requests.

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
