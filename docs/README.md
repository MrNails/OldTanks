# OldTanks — Docs Index

Entry point for agents: [../AGENTS.md](../AGENTS.md) (Claude Code loads it through [../CLAUDE.md](../CLAUDE.md)). Read [PROJECT-KNOWLEDGE.md](PROJECT-KNOWLEDGE.md) first. This page routes a task to the page that owns it.

## Find the right page

| Task area | Start with | More |
|-----------|------------|------|
| Threads, startup, the frame, locks, global state | [ARCHITECTURE](ARCHITECTURE.md) | [BUILD](BUILD.md) for settings and log files |
| Drawing: renderers, batching and instancing, shaders, textures, the camera | [RENDERING](CoolEngine/Services/Renderers/RENDERING.md) | [ASSET-PIPELINE](CoolEngine/Services/Loaders/ASSET-PIPELINE.md) for shader files |
| Loading shaders, textures, skyboxes, models and fonts; cache names | [ASSET-PIPELINE](CoolEngine/Services/Loaders/ASSET-PIPELINE.md) | [features §6](OldTanks/FEATURES-OVERVIEW.md#6-adding-a-world-object-type) for a new object type |
| Decoding PNG and JPEG: the API, the strict rules, limits, tests | [IMAGE-DECODERS](CoolEngine.Imaging/IMAGE-DECODERS.md) | [ASSET-PIPELINE](CoolEngine/Services/Loaders/ASSET-PIPELINE.md) for the loaders |
| Math types, matrices, the conventions (column vectors, column-major, reversed Z), the same-bits promise, benchmarks | [MATHEMATICS](CoolEngine.Mathematics/MATHEMATICS.md) | [CODE-STYLE](CODE-STYLE.md#math-types), [plan D6 and D7](plans/ENGINES-AND-TANK-GAME.md#5-open-decisions) |
| Gravity, movement, collision tests and resolution, rays, step mode | [PHYSICS-AND-COLLISIONS](CoolEngine/PhysicEngine/PHYSICS-AND-COLLISIONS.md) | [KI-002](KNOWN-ISSUES.md#ki-002-worldobjectrotation-is-read-as-radians-in-one-place-and-degrees-in-another) (closed), [KI-031](KNOWN-ISSUES.md#ki-031-the-collision-debug-view-transforms-objects-on-the-gl-thread-while-the-physics-thread-does) |
| HUD text, ImGui controls, adding a debug window | [UI-CONTROLS](OldTanks.UI/UI-CONTROLS.md) | [BINDING-GENERATOR](OldTanks.UI.SourceGenerators/BINDING-GENERATOR.md) |
| Generated bindings in the debug window | [BINDING-GENERATOR](OldTanks.UI.SourceGenerators/BINDING-GENERATOR.md) | — |
| The game: controls, default scene, debug windows, new object types | [FEATURES-OVERVIEW](OldTanks/FEATURES-OVERVIEW.md) | — |
| Building and running | [BUILD](BUILD.md) | — |

Records that cut across areas:

- [PROJECT-KNOWLEDGE](PROJECT-KNOWLEDGE.md): mission, current state, conventions, rules of thumb; read first, about 10 KiB.
- [KNOWN-ISSUES](KNOWN-ISSUES.md): the defect register, `KI-001` to `KI-031`; stable numbers that code and pages cite.
- [BACKLOG](BACKLOG.md): open work, unwired features, debt.
- [CODE-STYLE](CODE-STYLE.md) and [DOCUMENTATION-GUIDE](DOCUMENTATION-GUIDE.md): how code, commits and docs are written.
- [knowledge/GLOSSARY](knowledge/GLOSSARY.md): terms, and the type names that clash.
- [sessions/SESSION-LOG](sessions/SESSION-LOG.md): what was decided in each session, newest first.

## Docs layout

- `docs/` root: pages that span projects.
- `docs/<Project>/<source path>/`: feature pages, mirroring the source folder they describe ([guide §6](DOCUMENTATION-GUIDE.md#6-feature-pages-mirror-the-source-tree)).
- `docs/knowledge/`: reference material. `docs/sessions/`: the journal and its monthly archive.
- `docs/plans/`: proposed and multi-step work. [ENGINES-AND-TANK-GAME](plans/ENGINES-AND-TANK-GAME.md) (draft): the separate physics, render and UI engines and the online tank game, with the own-library tasks, render API and browser direction. [SIMULATION-AND-PHYSICS-SPLIT](plans/SIMULATION-AND-PHYSICS-SPLIT.md) (agreed 2026-11-27, not started): a headless physics library, a fixed 60 Hz tick, commands in and snapshots out, with Denys's decisions and the two tasks, OT-19 and OT-20.
- `ARCHITECTURE-DECISIONS.md`: created when first needed.
