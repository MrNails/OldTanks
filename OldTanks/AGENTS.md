# OldTanks (game)

> **Project knowledge:** [../docs/PROJECT-KNOWLEDGE.md](../docs/PROJECT-KNOWLEDGE.md)

The game executable: entry point, the OpenTK game window, the world and its objects, the ImGui debug windows, user settings and the `Assets/` folder.

## Related Docs

| Topic | Page |
|-------|------|
| Controls, default scene, HUD, debug windows, adding an object type | [FEATURES-OVERVIEW](../docs/OldTanks/FEATURES-OVERVIEW.md) |
| Startup, threads, frame order | [ARCHITECTURE](../docs/ARCHITECTURE.md) |
| The physics step in `World.CollideObjects` | [PHYSICS-AND-COLLISIONS §3](../docs/CoolEngine/PhysicEngine/PHYSICS-AND-COLLISIONS.md#3-the-step-loop-worldcollideobjects) |
| What is in `Assets/` and how names map to caches | [ASSET-PIPELINE](../docs/CoolEngine/Services/Loaders/ASSET-PIPELINE.md) |
| The debug window's generated bindings | [BINDING-GENERATOR](../docs/OldTanks.UI.SourceGenerators/BINDING-GENERATOR.md) |
| Running, settings file, log file | [BUILD](../docs/BUILD.md) |
| Known defects here | [KNOWN-ISSUES](../docs/KNOWN-ISSUES.md): KI-001 to KI-003, KI-007, KI-012 to KI-014, KI-020, KI-022 |

## Project Details

| Property | Value |
|----------|-------|
| Namespace root | `OldTanks` (the ImGui windows are `OldTanks.UI.ImGuiUI`, though they live in this project) |
| Target framework | `net10.0`, `LangVersion latest`, `OutputType Exe` |
| Layer | Application; references `CoolEngine`, `OldTanks.UI` and the generator (`Common` comes through them) |

## Key Types

| Folder | Types |
|--------|-------|
| (root) | `Program` |
| `Windows/` | `MainWindow` (the `GameWindow`; HUD fields in `ControlWindows/MainWindow.cs`; its `NativeWindowSettings` use `ClientSize`) |
| `Models/` | `World`, `WorldObject`, `Cube`, `Sphere`, `Robot` |
| `Services/` | `GameManager` (asset loading), `LoggerService`, `EngineSettings` (game side), `Constants`, `Misc/TransformableAction` |
| `UI/ImGuiUI/` | `MainWindow` (debug window, `[BindableClass]`), `TextureWindow`; their controls in `ControlsInitialization/` |
| `DataModels/`, `Infrastructure/`, `Helpers/` | `Settings`, `CollisionConstants`, `UIVectorHelper` |
| `Assets/` | `Shaders/`, `Textures/` (+ `SkyBoxes/`), `Models/Default/`, `Fonts/` |

## Dependencies

| Dependency | Reason |
|------------|--------|
| `CoolEngine`, `OldTanks.UI` (projects) | Engine and UI |
| `OldTanks.UI.SourceGenerators` (project, `OutputItemType="Analyzer"`) | Generates `OldTanks.UI.ImGuiUI.MainWindow`'s bindings; also supplies the attribute types |
| `Serilog.Sinks.Console` 6.1.1 | Console log (version in `Directory.Packages.props`) |

## Constraints

- **Two `MainWindow` classes:** `OldTanks.Windows.MainWindow` (game) and `OldTanks.UI.ImGuiUI.MainWindow` (debug UI). Only the second may carry `[BindableClass]`, and the generator only serves a class with that name.
- **Asset names are literals** here (`"wall-texture"`, `"Cube_Cube.005"`, `"DefaultShader"`, `"SkyBox2"`, `"Arial"`); renaming an asset means changing them too.
- **World changes from UI handlers race the physics thread** (KI-003); don't add more until that is fixed, or take the fix with you.
- **Construct world objects only after assets are loaded** (in the startup continuation or later); constructors read the scene cache.
- **Keep `ObjectRendererOld` and `CollisionRenderer` registrations in step** with `World.WorldObjects` when adding or removing objects, as the debug window does.
