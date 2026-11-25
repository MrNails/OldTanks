# AGENTS.md — OldTanks

> **Project knowledge:** [docs/PROJECT-KNOWLEDGE.md](docs/PROJECT-KNOWLEDGE.md)

Denys's hobby 3D game in C# on its own OpenGL engine, CoolEngine: today an engine sandbox with physics, collisions, a free camera and ImGui debug windows; no tanks or player yet. `OldTanks.slnx`, six projects plus four test projects, `net10.0` (generator `netstandard2.0`), OpenTK 4.9.4, ImGui.NET, Serilog, versions central in `Directory.Packages.props`; GitHub `MrNails/OldTanks`; branches `feature/OT-<n>` and `bug/OT-<n>` from `master`, merged back by pull request.

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
| `CoolEngine.Imaging` — strict PNG and JPEG decoders, no dependencies | [CoolEngine.Imaging/AGENTS.md](CoolEngine.Imaging/AGENTS.md) |
| `OldTanks` — the game | [OldTanks/AGENTS.md](OldTanks/AGENTS.md) |
| `OldTanks.UI` — control handler, HUD text, ImGui controls | [OldTanks.UI/AGENTS.md](OldTanks.UI/AGENTS.md) |
| `OldTanks.UI.SourceGenerators` — binding generator | [OldTanks.UI.SourceGenerators/AGENTS.md](OldTanks.UI.SourceGenerators/AGENTS.md) |
| `Common` — `ObservableObject`, `SettingsService`, typed events, helpers; no dependencies | This root guide |
| `tests/OldTanks.Docs.Tests` — the documentation checks | This root guide; its rules are [DOCUMENTATION-GUIDE §9](docs/DOCUMENTATION-GUIDE.md#9-checks) |
| `tests/CoolEngine.Tests` — the engine's headless unit tests (today `ObjectRenderer<T>`, the dispatcher and `Scene`) | This root guide and [BUILD §2](docs/BUILD.md#2-build) |
| `tests/OldTanks.UI.Tests` — control names and item width, headless ImGui | This root guide and [UI-CONTROLS](docs/OldTanks.UI/UI-CONTROLS.md) |
| `tests/CoolEngine.Imaging.Tests` — the decoders against Windows and ImageSharp | This root guide and [IMAGE-DECODERS §6](docs/CoolEngine.Imaging/IMAGE-DECODERS.md#6-how-it-is-tested) |

## Folder map

Exhaustive for the repository root.

| Path | Purpose |
|------|---------|
| `Common/`, `CoolEngine/`, `CoolEngine.Imaging/`, `OldTanks.UI/`, `OldTanks.UI.SourceGenerators/`, `OldTanks/` | The six projects ([architecture](docs/ARCHITECTURE.md#1-solution-layout)) |
| `Directory.Packages.props` | Every NuGet version, once (central package management); project files list packages without a `Version` |
| `OldTanks/Assets/` | Shaders, textures, skyboxes, models, fonts; copied to the output on build ([asset pipeline](docs/CoolEngine/Services/Loaders/ASSET-PIPELINE.md)) |
| `tests/` | Four NUnit test projects: `OldTanks.Docs.Tests` (`net10.0`), the documentation checks; `CoolEngine.Tests` (`net10.0`), the engine's unit tests, headless (no OpenGL context, so only code that makes no GL call): `ObjectRenderer<T>` and the dispatcher; `OldTanks.UI.Tests` (`net10.0`), the UI layer's control handler and ImGui controls; `CoolEngine.Imaging.Tests` (`net10.0-windows`), the decoder tests |
| `docs/` | The knowledge base; index [docs/README.md](docs/README.md) |
| `.claude/skills/` | Project skills for Claude Code: [mentor-pipeline](.claude/skills/mentor-pipeline/SKILL.md) (agent tiers by risk, plan and brief templates, the review checklist, the calibration table). Personal settings and worktrees under `.claude/` stay git-ignored |
| `OldTanks.slnx` | The solution |
| `OldTanks.slnx.DotSettings.user` | A tracked per-user Rider file ([BACKLOG](docs/BACKLOG.md#platform-and-build)) |

## Build and run

`dotnet build OldTanks.slnx` (0 errors, 131 warnings baseline). Run `OldTanks.exe` from `OldTanks\bin\Debug\net10.0\`: assets resolve against the working directory, and a .NET 10 runtime is needed. `dotnet test OldTanks.slnx` runs the documentation checks, the `CoolEngine` tests, the UI tests and the decoder tests (no tests of the game project yet). Details: [docs/BUILD.md](docs/BUILD.md).

## Constraints

- **OpenGL only on the GL (main) thread**; marshal through the dispatcher ([architecture §3](docs/ARCHITECTURE.md#3-getting-work-onto-the-gl-thread)).
- **No second `Application`, `ControlHandler` or window**: they are process-wide singletons.
- **Asset names are cache keys** used as string literals in code; renaming a texture, shader folder or OBJ object breaks lookups silently (they return `null`).
- **No pasted source in docs**; reference symbols by fully-qualified name, never by line number ([guide §3](docs/DOCUMENTATION-GUIDE.md#3-style-rules)).
