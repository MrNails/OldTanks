# Project Knowledge — OldTanks

| Field        | Value  |
|--------------|--------|
| Status       | Living - read first every session |
| Last updated | 2026-11-25 |
| Verified against | 2026-11-25; bug/OT-13 rebased on master at 8ded8f4 (§2's build, test and documentation lines against the OT-13 build, test run and the KNOWN-ISSUES index; §6's collision-shapes rule against PHYSICS §5); bug/OT-12 rebased on master at 734d8f6 (§2's build, test and documentation lines against the build and test run and the KNOWN-ISSUES index); bug/OT-10 rebased on master at 483d63f (§2's build, test and documentation lines against the 2026-11-25 build and test run); bug/OT-9 on master at d749ec8 (§2 build, test and documentation lines, §6 run line); bug/OT-8 rebased on master at 826e560 (OT-7); §2 build, test and run lines against the 2026-11-25 build, test run, the two OT-7 runs of the game and the OT-8 smoke run, the shader lines against the two OT-6 runs (bug/OT-6, rebased on master at 6753e46); §3-§4 against the solution; §5-§6 as of 2026-11-24 |

---

## 1. Mission

OldTanks is Denys's hobby 3D game in C#, built on its own engine: **CoolEngine**, an OpenGL 4.6 renderer with simple rigid-body physics and collisions, plus a small UI layer over Dear ImGui with source-generated bindings. The name points at a tank game; what exists today is the engine and a sandbox scene to exercise it: walls, a floor, cubes and spheres falling and colliding, a free-fly camera, and debug windows to inspect and edit every object.

The value of the code is in the engine pieces (instanced rendering, collision tests, the GL-thread dispatcher, the binding generator), so most work is engine work tested in the sandbox. Explanations can assume a professional C# developer; graphics and physics terms are in the [Glossary](knowledge/GLOSSARY.md).

## 2. Current state

- **Repository:** https://github.com/MrNails/OldTanks. 75 commits from 2022-03-22 to 2024-06-01, then idle until the documentation start on 2026-11-24.
- **Branches:** `master` is the working branch (Denys, 2026-11-24): work branches from it as `feature/OT-<n>` or `bug/OT-<n>` and comes back by pull request ([CODE-STYLE](CODE-STYLE.md#git-rules)). OT-1 is in `master` through PR #8 (`55678e0`), on top of the earlier `develop` fixes (PR #5, spawned objects displaying; PR #7, on-ground detection). `develop` stopped at PR #7 and is no longer used.
- **Works (the game runs since OT-3; the smoke run of 2026-11-25 showed textured walls and floor and the ImGui debug window, the run of bug/OT-6 the skybox and the HUD text; the rest is read from code):** the sandbox scene with gravity and collisions; free camera; textured, instanced rendering (spawned untextured cubes draw rose, also on the instanced path, since OT-7); skybox; HUD; debug view of collision shapes; picking ray; the ImGui debug and texture windows with two-way editing of position, rotation, size and most body properties; spawning, deleting and retexturing objects ([features](OldTanks/FEATURES-OVERVIEW.md)). **Not working:** nothing known in rendering since `bug/OT-6` (2026-11-25, not merged yet: all 8 shader programs link, skybox and HUD text show; [KI-020](KNOWN-ISSUES.md#ki-020-three-shaders-fail-to-compile-and-are-used-anyway) closed).
- **Not built:** a player (`World.Player` is never set, [KI-001](KNOWN-ISSUES.md#ki-001-the-player-is-never-assigned)), tanks, game rules, levels, lighting, materials, sound, tests of the game code (the engine's only code tests besides the decoders are those of `ObjectRenderer<T>` , since OT-8, the dispatcher, since OT-12, `Scene` and, since OT-13, `Collision`; the UI layer has its own since OT-10), CI. Several controls are wired to nothing ([BACKLOG](BACKLOG.md#unwired-features)).
- **Build and tests (2026-11-25, `bug/OT-13`, nine code commits rebased on `master` at `8ded8f4`, which holds OT-3 (PR #10), OT-11 (PR #11), OT-7 (PR #12), OT-8 (PR #13), OT-6 (PR #14), OT-9 (PR #15), OT-10 (PR #16) and OT-12 (PR #17); no pull request yet):** every project targets `net10.0` with `LangVersion latest`; 0 errors and 131 warnings, the same as `master` (136 before OT-9, 139 after OT-3, 165 before it); 359 tests in four projects (31 documentation checks, 67 in the headless `tests/CoolEngine.Tests`: 16 `ObjectRenderer<T>`, 14 dispatcher, 8 `Scene` and 29 `Collision` tests, 14 UI tests in `tests/OldTanks.UI.Tests`, 247 decoder tests; 330 on `master`); no vulnerable package; versions central in `Directory.Packages.props` ([BUILD](BUILD.md), [ARCHITECTURE §1](ARCHITECTURE.md#1-solution-layout)). ImageSharp is gone from the engine, replaced by `CoolEngine.Imaging` ([IMAGE-DECODERS](CoolEngine.Imaging/IMAGE-DECODERS.md)).
- **OT-8 (`bug/OT-8`, 2026-11-25, 3 code commits, rebased on `master` at `826e560`, not pushed, not merged):** one root logger and one `log.txt` with `[source]` tags, logged unhandled and unobserved exceptions, logged exceptions of posted dispatcher callbacks; 14 dispatcher tests in the shared `tests/CoolEngine.Tests`; 308 tests in all, 0 errors, 136 warnings ([ARCHITECTURE §3](ARCHITECTURE.md#3-getting-work-onto-the-gl-thread), [§4](ARCHITECTURE.md#4-startup-and-the-frame)).
- **OT-13 (`bug/OT-13`, 2026-11-25, nine code commits rebased on `master` at `8ded8f4`, not pushed, not merged):** `Collision` tests edge × edge axes and an exact sphere × polygon contact (closest point for a cuboid, vertex and edge axes for other shapes), moves face normals by the inverse-transpose, and no longer prints a false `InitCollision error` for spheres; 29 new tests ([PHYSICS §4](CoolEngine/PhysicEngine/PHYSICS-AND-COLLISIONS.md#4-detection)). Closes KI-016 and KI-022, adds KI-031.
- **Documentation:** checked by `tests/OldTanks.Docs.Tests` on every `dotnet test`; 31 known defects recorded as `KI-001` to `KI-031`, 14 open (KI-016 and KI-022 closed by OT-13; KI-021 closed by OT-11; KI-005, KI-009 and KI-019 closed by OT-7; KI-004, KI-011 and KI-015 closed by OT-8; KI-008 and KI-020 closed by OT-6, checked in two runs; KI-012 to KI-014 closed by OT-9; KI-010 and KI-018 closed by OT-10; KI-006 closed by OT-12; KI-024 and KI-025 from OT-7; KI-026 to KI-029 from OT-10; KI-030 from OT-12, a provisional number until it merges; KI-031 from the OT-13 review); KI-004, KI-008 and KI-020 (each before its fix), KI-012 to KI-014 (after OT-9's fix) and KI-022 (before its fix) observed in the running game on 2026-11-25, KI-022's fix confirmed by a 12 s run of `bug/OT-13` on 2026-11-25, the rest reasoned from code.
- **Goal (Denys, 2026-11-24):** separate, maximally optimized physics, render and UI engines and an online tank game on them; requirements and open decisions in [plans/ENGINES-AND-TANK-GAME](plans/ENGINES-AND-TANK-GAME.md). Anything unclear is asked, never improvised.
- **Decided for it (Denys, 2026-11-24):** Denys writes the mechanics specs; dedicated authoritative server with headless physics; `net10.0` everywhere (done in OT-3); new engines beside `CoolEngine`, which goes once unused; own libraries for fonts, math, OpenGL bindings and the window, OpenGL 4.6 behind an API-neutral boundary, WebGPU only for a far-future browser client ([plan §6](plans/ENGINES-AND-TANK-GAME.md#6-decisions-of-ot-3-and-the-planned-tasks)).
- **Next:** merge `bug/OT-6` (the shader fixes, KI-008 and KI-020, done and not merged yet); **OT-4** own fonts (`CoolEngine.Fonts`), then **OT-5** own math (`CoolEngine.Mathematics`, `float` and `double`); names and determinism (D5, D6) still open.

## 3. Solution map

| Project | One line | Guide |
|---------|----------|-------|
| `Common` | Observable base, settings service, typed events, small helpers | [AGENTS.md](../AGENTS.md#guide-map) |
| `CoolEngine` | Rendering, asset loaders, physics, dispatcher, caches | [CoolEngine/AGENTS.md](../CoolEngine/AGENTS.md) |
| `CoolEngine.Imaging` | Strict PNG and JPEG decoders, no dependencies | [CoolEngine.Imaging/AGENTS.md](../CoolEngine.Imaging/AGENTS.md) |
| `OldTanks.UI` | Control handler, HUD text, ImGui wrappers and renderer | [OldTanks.UI/AGENTS.md](../OldTanks.UI/AGENTS.md) |
| `OldTanks.UI.SourceGenerators` | Generates the debug window's bindings | [OldTanks.UI.SourceGenerators/AGENTS.md](../OldTanks.UI.SourceGenerators/AGENTS.md) |
| `OldTanks` | The game window, world, objects, debug windows, assets | [OldTanks/AGENTS.md](../OldTanks/AGENTS.md) |

References, packages, threads and singletons: [ARCHITECTURE](ARCHITECTURE.md).

## 4. Routes

| When the task touches | Read |
|-----------------------|------|
| Threads, startup, the frame, locks | [ARCHITECTURE](ARCHITECTURE.md) |
| Drawing, shaders, textures | [RENDERING](CoolEngine/Services/Renderers/RENDERING.md) |
| Assets, cache names, loaders | [ASSET-PIPELINE](CoolEngine/Services/Loaders/ASSET-PIPELINE.md) |
| Decoding PNG and JPEG | [IMAGE-DECODERS](CoolEngine.Imaging/IMAGE-DECODERS.md) |
| Movement, gravity, collisions, rays | [PHYSICS-AND-COLLISIONS](CoolEngine/PhysicEngine/PHYSICS-AND-COLLISIONS.md) |
| HUD, ImGui controls, new debug windows | [UI-CONTROLS](OldTanks.UI/UI-CONTROLS.md) |
| Attributed bindings in the debug window | [BINDING-GENERATOR](OldTanks.UI.SourceGenerators/BINDING-GENERATOR.md) |
| Controls, the default scene, new object types | [FEATURES-OVERVIEW](OldTanks/FEATURES-OVERVIEW.md) |
| Building, running, settings, logs | [BUILD](BUILD.md) |
| Known defects, open work | [KNOWN-ISSUES](KNOWN-ISSUES.md), [BACKLOG](BACKLOG.md) |

## 5. Conventions

- **Docs:** [DOCUMENTATION-GUIDE](DOCUMENTATION-GUIDE.md). **Code and Git:** [CODE-STYLE](CODE-STYLE.md). **Agent work:** the [mentor pipeline](../.claude/skills/mentor-pipeline/SKILL.md) (adapted from ArbBot and AnalysisTools): the mentor plans, a tiered implementer codes, one opus reviewer checks, a documenter updates the docs.
- **Units and axes:** Y up, gravity -Y, metres and seconds by intent; the default camera looks along +X. Object rotation should be radians but is not read consistently ([KI-002](KNOWN-ISSUES.md#ki-002-worldobjectrotation-is-read-as-radians-in-one-place-and-degrees-in-another)); the debug window shows degrees.
- **Assets are fetched by name** from `GlobalCache<T>`; a model's name is its OBJ object name, not its file name.
- **Language:** docs, code and commits in English; conversation in whatever language Denys uses.
- **Dates** in docs are absolute (`2026-11-24`), never "today" or "last week".

## 6. Rules of thumb

- **OpenGL only on the GL thread.** Loaders get there by `await` from the main thread or by `UI.UIInvoke`; renderers create GL objects lazily inside `Render` ([ARCHITECTURE §3](ARCHITECTURE.md#3-getting-work-onto-the-gl-thread)).
- **Create the `ControlHandler` before any control**, and the `Application` before anything that uses the dispatcher.
- **A world object needs four things before it joins `World.WorldObjects`:** a scene from the cache, a `Collision`, a `TexturedObjectInfo` covering every mesh, and either `IsStatic` or speed limits (all speed limits default to 0, which pins forward speed at 0) ([features §6](OldTanks/FEATURES-OVERVIEW.md#6-adding-a-world-object-type)).
- **Collision shapes are model-space -1..1** and scaled by `Size / 2`; they must be convex with faces that share vertex indices (edges come from them), a polygon needs vertices, and face vertex order matters for rays ([PHYSICS §5](CoolEngine/PhysicEngine/PHYSICS-AND-COLLISIONS.md#5-shapes)).
- **Velocity is local:** `X` is the object's forward axis.
- **Objects sharing a model and texture are instanced from the 17th one added** (16 if they were present before `InstancedShader` was set; without `InstancedShader` they are all drawn one by one), and never go back; that path has its own shader, `DefaultShader2` ([RENDERING §2](CoolEngine/Services/Renderers/RENDERING.md#2-objectrenderert-batching-and-instancing)).
- **Shader problems are loud now (OT-6):** an unknown uniform, block or attribute name resolves to -1 with a Warning once per name, and a program that does not link throws `ShaderException` and stops startup ([RENDERING §3](CoolEngine/Services/Renderers/RENDERING.md#3-gpu-side-types)); check the console or `log.txt` after editing a shader, and never name a GLSL variable like a built-in (`texture`).
- **Run from a folder that contains `Assets/`**, or the game shows a "Startup failed" window and `Esc` quits ([BUILD §3](BUILD.md#3-run)).
- **The generator only serves a class named `MainWindow`** ([BINDING-GENERATOR §4](OldTanks.UI.SourceGenerators/BINDING-GENERATOR.md#4-limits)).

## 7. Open questions

- ~~Which .NET to retarget to?~~ Answered: `net10.0`, done in OT-3.
- The OT-4 plan questions: colour fonts, variable-font axes, CFF hinting ([plan §6](plans/ENGINES-AND-TANK-GAME.md#6-decisions-of-ot-3-and-the-planned-tasks)).
- ~~Is the face-normal-only collision test a deliberate simplification?~~ Answered (Denys, 2026-11-25): "Not deliberate: add axes"; done in OT-13 ([KI-016](KNOWN-ISSUES.md#ki-016-polygon-tests-use-face-normals-only), [PHYSICS §4](CoolEngine/PhysicEngine/PHYSICS-AND-COLLISIONS.md#4-detection)).
