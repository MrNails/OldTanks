# OldTanks.UI

> **Project knowledge:** [../docs/PROJECT-KNOWLEDGE.md](../docs/PROJECT-KNOWLEDGE.md)

The UI layer: the `ControlHandler` singleton, engine-drawn screen text controls, retained-mode wrappers over Dear ImGui, and the ImGui renderer. The game's actual windows live in `OldTanks/UI/ImGuiUI/`.

## Related Docs

| Topic | Page |
|-------|------|
| Control handler, controls, events, the ImGui renderer, adding a window | [UI-CONTROLS](../docs/OldTanks.UI/UI-CONTROLS.md) |
| Generated bindings over these controls | [BINDING-GENERATOR](../docs/OldTanks.UI.SourceGenerators/BINDING-GENERATOR.md) |
| Known defects here | [KNOWN-ISSUES](../docs/KNOWN-ISSUES.md): KI-010, KI-018 |

## Project Details

| Property | Value |
|----------|-------|
| Namespace root | `OldTanks.UI` |
| Target framework | `net10.0`, `LangVersion latest` |
| Layer | UI library over the engine; referenced by `OldTanks` |

## Key Types

| Folder | Types |
|--------|-------|
| `Services/` | `ControlHandler`, `ControlCollection`, `Binding<,,>` (unused); `ImGUI/ImGuiController`; `Interfaces/` (`IControl`, `IControlContainer`, `IControlsContainer`) |
| `Controls/` | `Control`, `TextControl`, `TextBlock` (screen text through `TextRenderer`) |
| `ImGuiControls/` | `ImGuiControl`, `ImGuiControlContainer`, `ImGuiWindow`, `ImGuiPanel`, `ImGuiColumnsControl`, `ImGuiTreeNode`, `ImGuiButton`, `ImGuiCheckBox`, `ImGuiTextBlock`, `ImGuiTextBox`, the `ImGuiFloat*DragTextBox` family, `ImGuiListBox<T>`, `ImGuiImage`, `ImGuiSameLine`, `ImGuiNewLine` |

## Dependencies

| Dependency | Reason |
|------------|--------|
| `Common`, `CoolEngine` (projects) | Observable base, typed events; `TextRenderer`, `Font`, `GLSettings`, `Texture` |
| ImGui.NET 1.89.9.3 | Dear ImGui bindings |
| OpenTK 4.8.1 | GL calls in the ImGui renderer, window input |

## Constraints

- **`ControlHandler` before any control**; one per process.
- **Control names are ImGui IDs** for ImGui controls: unique within a window (uniqueness is not enforced, KI-010).
- **ImGui types use `System.Numerics` vectors**; conversion to OpenTK happens in the caller.
- **No game types here:** controls are generic; game-specific windows belong in `OldTanks`.
- **Renaming a control property or event** used in a `[BindableElement]` string breaks the generated code at compile time; update the attributes in `OldTanks/UI/ImGuiUI/ControlsInitialization/MainWindow.cs`.
