# AGENTS.md — OldTanks

> **Project knowledge:** [docs/PROJECT-KNOWLEDGE.md](docs/PROJECT-KNOWLEDGE.md)

Denys's hobby 3D game in C# on its own OpenGL engine, CoolEngine: today an engine sandbox with physics, collisions, a free camera and ImGui debug windows; no tanks or player yet. `OldTanks.slnx`, ten projects plus nine test projects and two benchmark projects, `net10.0` (generator `netstandard2.0`), OpenTK 4.9.4, ImGui.NET, Serilog, versions central in `Directory.Packages.props`; GitHub `MrNails/OldTanks`; branches `feature/OT-<n>` and `bug/OT-<n>` from `master`, merged back by pull request.

## Session bootstrap

1. Read [docs/PROJECT-KNOWLEDGE.md](docs/PROJECT-KNOWLEDGE.md) in full: mission, current state, conventions, rules of thumb, routes.
2. Read the newest entry of [docs/sessions/SESSION-LOG.md](docs/sessions/SESSION-LOG.md).
3. Find the page that owns the task in [docs/README.md](docs/README.md) and read it before editing; check [docs/KNOWN-ISSUES.md](docs/KNOWN-ISSUES.md) for the area.

## Session close

- Add a session-log entry ([guide §5](docs/DOCUMENTATION-GUIDE.md#5-session-log-format)): decisions in full, everything else as pointers.
- Update the pages the change touched ([update triggers](docs/DOCUMENTATION-GUIDE.md#8-update-triggers)); a new defect gets the next `KI-nnn`, open work goes to [BACKLOG](docs/BACKLOG.md), a changed state to PROJECT-KNOWLEDGE §2. Bump `Last updated`.
- `dotnet build OldTanks.slnx` (0 errors, no new warnings) and `dotnet test OldTanks.slnx` (the docs checks) before any commit. Commit subjects start with the task id ([Git rules](docs/CODE-STYLE.md#git-rules)).

## AGENTS.md discovery

Before editing a file, read every `AGENTS.md` from the repository root down to the file's folder, root first. A project guide adds constraints and cancels none of the root's. Each guide has a `CLAUDE.md` adapter beside it that only imports it.

### Guide map

Exhaustive: every `AGENTS.md` in the repository.

| Area | Guide |
|------|-------|
| `CoolEngine` — rendering, loaders, physics, dispatcher | [CoolEngine/AGENTS.md](CoolEngine/AGENTS.md) |
| `CoolEngine.Fonts` — font loading, glyph outlines, metrics and hinted bitmaps identical to FreeType 2.13.2, no dependencies | [CoolEngine.Fonts/AGENTS.md](CoolEngine.Fonts/AGENTS.md) |
| `CoolEngine.Imaging` — strict PNG and JPEG decoders, no dependencies | [CoolEngine.Imaging/AGENTS.md](CoolEngine.Imaging/AGENTS.md) |
| `CoolEngine.Mathematics` — float matrices over System.Numerics vectors, double types, no dependencies | [CoolEngine.Mathematics/AGENTS.md](CoolEngine.Mathematics/AGENTS.md) |
| `CoolEngine.Core` — fixed-tick clock, tick runner and thread host, command queue, snapshot buffer; no dependencies; unused by the game yet | [CoolEngine.Core/AGENTS.md](CoolEngine.Core/AGENTS.md) |
| `CoolEngine.Physics` — headless bodies, shapes, detection, the sub-stepped step and ray casts, an exact port of today's step (stage 1 of two), references only `CoolEngine.Mathematics`; unused by the game yet | [CoolEngine.Physics/AGENTS.md](CoolEngine.Physics/AGENTS.md) |
| `OldTanks` — the game | [OldTanks/AGENTS.md](OldTanks/AGENTS.md) |
| `OldTanks.UI` — control handler, HUD text, ImGui controls | [OldTanks.UI/AGENTS.md](OldTanks.UI/AGENTS.md) |
| `OldTanks.UI.SourceGenerators` — binding generator | [OldTanks.UI.SourceGenerators/AGENTS.md](OldTanks.UI.SourceGenerators/AGENTS.md) |
| `Common` — `ObservableObject`, `SettingsService`, typed events, helpers; no dependencies | This root guide |
| `tests/OldTanks.Docs.Tests` — the documentation checks | This root guide; its rules are [DOCUMENTATION-GUIDE §9](docs/DOCUMENTATION-GUIDE.md#9-checks) |
| `tests/CoolEngine.Tests` — the engine's headless unit tests (today `ObjectRenderer<T>`, `ObjectRendererOld`, the dispatcher, `Scene`, `Collision`, `CollisionRenderGroup`, the GL types' finalizers with `GLDeletionQueue`, the golden values of the camera, the collision transforms and the texture transform, and the font-atlas goldens of Arial and Times New Roman) | This root guide and [BUILD §2](docs/BUILD.md#2-build) |
| `tests/OldTanks.Tests` — golden values and behaviour tests of `WorldObject`'s transform and movement and `WindowMetrics`'s sizes, headless | This root guide and [BUILD §2](docs/BUILD.md#2-build) |
| `tests/OldTanks.UI.Tests` — control names, item width and `ImGuiDisplayMetrics`, headless ImGui | This root guide and [UI-CONTROLS](docs/OldTanks.UI/UI-CONTROLS.md) |
| `tests/CoolEngine.Fonts.Tests` — the font library against FreeType 2.13.2 (FreeTypeSharp) and DirectWrite; `net10.0-windows`, `AllowUnsafeBlocks` for FreeTypeSharp's pointer API and the COM source generator | This root guide and [FONTS §6](docs/CoolEngine.Fonts/FONTS.md#6-how-it-is-tested) |
| `tests/CoolEngine.Imaging.Tests` — the decoders against Windows and ImageSharp | This root guide and [IMAGE-DECODERS §6](docs/CoolEngine.Imaging/IMAGE-DECODERS.md#6-how-it-is-tested) |
| `tests/CoolEngine.Mathematics.Tests` and `tests/CoolEngine.Mathematics.Benchmarks` — the math library against System.Numerics and OpenTK, and its benchmarks | The [Mathematics guide](CoolEngine.Mathematics/AGENTS.md) and [MATHEMATICS §6](docs/CoolEngine.Mathematics/MATHEMATICS.md#6-how-it-is-tested) |
| `tests/CoolEngine.Core.Tests` — the tick machinery through its public API, headless, no sleeps | The [Core guide](CoolEngine.Core/AGENTS.md) and [CORE §8](docs/CoolEngine.Core/CORE.md#8-how-it-is-tested) |
| `tests/CoolEngine.Physics.Tests` and `tests/CoolEngine.Physics.Benchmarks` — the physics library's tests and its benchmarks against today's step; the side-by-side tests against today's code are in `tests/OldTanks.Tests/PhysicsParity` and `tests/CoolEngine.Tests` | The [Physics guide](CoolEngine.Physics/AGENTS.md) and [PHYSICS-LIBRARY §7](docs/CoolEngine.Physics/PHYSICS-LIBRARY.md#7-how-the-exact-port-is-proved) |

## Folder map

Exhaustive for the repository root.

| Path | Purpose |
|------|---------|
| `Common/`, `CoolEngine/`, `CoolEngine.Fonts/`, `CoolEngine.Imaging/`, `CoolEngine.Mathematics/`, `CoolEngine.Core/`, `CoolEngine.Physics/`, `OldTanks.UI/`, `OldTanks.UI.SourceGenerators/`, `OldTanks/` | The ten projects ([architecture](docs/ARCHITECTURE.md#1-solution-layout)) |
| `Directory.Packages.props` | Every NuGet version, once (central package management); project files list packages without a `Version` |
| `BannedSymbols.txt` | The symbols the banned-API analyzer rejects (RS0030) in `Common`, `CoolEngine`, `CoolEngine.Physics`, `OldTanks.UI`, `OldTanks` and the three engine test projects (`CoolEngine.Tests`, `CoolEngine.Physics.Tests`, `OldTanks.Tests`): System.Numerics' row-vector `Matrix4x4`, `Matrix3x2`, `Plane` and the vector Transform overloads that take them, and OpenTK's math types except `Vector2`, `Vector2i` and `Color4` ([BUILD §2](docs/BUILD.md#2-build)) |
| `OldTanks/Assets/` | Shaders, textures, skyboxes, models, fonts; copied to the output on build ([asset pipeline](docs/CoolEngine/Services/Loaders/ASSET-PIPELINE.md)) |
| `tests/` | Nine NUnit test projects and two benchmark projects: `OldTanks.Docs.Tests` (`net10.0`), the documentation checks; `CoolEngine.Tests` (`net10.0`), the engine's unit tests, headless (no OpenGL context, so only code that makes no GL call): `ObjectRenderer<T>`, `ObjectRendererOld`, the dispatcher, `Scene`, `Collision` and `CollisionRenderGroup`, the finalizers of the GL types with `GLDeletionQueue`, the golden values of the camera, the collision transforms and the texture transform, and the font-atlas goldens of Arial and Times New Roman (`CoolEngine.GraphicalEngine.Core.Font.Font.BuildAtlasAsync`); `OldTanks.Tests` (`net10.0`), headless golden values and behaviour tests of `OldTanks.Models.WorldObject` and the size rules of `OldTanks.Windows.WindowMetrics`, since OT-20 the golden tests of today's step and rays (`Models`, `Windows`, `TestSupport`) and the side-by-side tests against `CoolEngine.Physics` (`PhysicsParity`); `OldTanks.UI.Tests` (`net10.0`), the UI layer's control handler, ImGui controls and `ImGuiDisplayMetrics`; `CoolEngine.Fonts.Tests` (`net10.0-windows`, unsafe allowed for FreeTypeSharp and the COM interop), the font library against FreeType and DirectWrite; `CoolEngine.Imaging.Tests` (`net10.0-windows`), the decoder tests; `CoolEngine.Mathematics.Tests` (`net10.0`), the math library's tests; `CoolEngine.Mathematics.Benchmarks` (`net10.0`, an exe, BenchmarkDotNet), its benchmarks; `CoolEngine.Core.Tests` (`net10.0`), the tick machinery's tests; `CoolEngine.Physics.Tests` (`net10.0`), the physics library's tests; `CoolEngine.Physics.Benchmarks` (`net10.0`, an exe, BenchmarkDotNet), the library against today's step |
| `docs/` | The knowledge base; index [docs/README.md](docs/README.md) |
| `.claude/skills/` | Project skills for Claude Code: [mentor-pipeline](.claude/skills/mentor-pipeline/SKILL.md) (agent tiers by risk, plan and brief templates, the review checklist, the calibration table). Personal settings and worktrees under `.claude/` stay git-ignored |
| `OldTanks.slnx` | The solution |
| `OldTanks.slnx.DotSettings.user` | A tracked per-user Rider file ([BACKLOG](docs/BACKLOG.md#platform-and-build)) |

## Build and run

`dotnet build OldTanks.slnx` (0 errors, 127 warnings baseline). Run `OldTanks.exe` from `OldTanks\bin\Debug\net10.0\`: assets resolve against the working directory, and a .NET 10 runtime is needed. `dotnet test OldTanks.slnx` runs the documentation checks, the `CoolEngine` tests, the UI tests, the decoder tests, the font tests (4,078 of them, about 55 s), the math tests, the `CoolEngine.Core` tests (88), the `CoolEngine.Physics` tests (127) and `OldTanks.Tests` (72: golden values, behaviour tests, `WindowMetrics`, the OT-20 golden tests of today's step and the 16 side-by-side tests against the physics library); 5,212 tests on `feature/OT-20` at `9212d68` (4,939 on `master`). Details: [docs/BUILD.md](docs/BUILD.md).

## Constraints

- **OpenGL only on the GL (main) thread**; marshal through the dispatcher ([architecture §3](docs/ARCHITECTURE.md#3-getting-work-onto-the-gl-thread)).
- **No second `Application`, `ControlHandler` or window**: they are process-wide singletons.
- **Asset names are cache keys** used as string literals in code; renaming a texture, shader folder or OBJ object breaks lookups silently (they return `null`).
- **No pasted source in docs**; reference symbols by fully-qualified name, never by line number ([guide §3](docs/DOCUMENTATION-GUIDE.md#3-style-rules)).
