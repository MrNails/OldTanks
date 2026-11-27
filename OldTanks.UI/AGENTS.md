# OldTanks.UI

> **Project knowledge:** [../docs/PROJECT-KNOWLEDGE.md](../docs/PROJECT-KNOWLEDGE.md)

The UI layer: the `ControlHandler` singleton, engine-drawn screen text controls, retained-mode wrappers over Dear ImGui, and the ImGui renderer. The game's actual windows live in `OldTanks/UI/ImGuiUI/`.

## Related Docs

| Topic | Page |
|-------|------|
| Control handler, controls, events, the ImGui renderer, adding a window | [UI-CONTROLS](../docs/OldTanks.UI/UI-CONTROLS.md) |
| Generated bindings over these controls | [BINDING-GENERATOR](../docs/OldTanks.UI.SourceGenerators/BINDING-GENERATOR.md) |
| Known defects here | [KNOWN-ISSUES](../docs/KNOWN-ISSUES.md): KI-026 to KI-029 open; KI-010 and KI-018 closed |
| Tests of this project | `tests/OldTanks.UI.Tests` ([BUILD §2](../docs/BUILD.md#2-build)) |

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
| ImGui.NET 1.91.6.1 | Dear ImGui bindings |
| OpenTK 4.9.4 | GL calls in the ImGui renderer, window input |
| `Microsoft.CodeAnalysis.BannedApiAnalyzers` 5.6.0 (analyzer) | RS0030 error for the System.Numerics row-vector matrix APIs and OpenTK's math types in `BannedSymbols.txt` (OpenTK's `Vector2`, `Vector2i` and `Color4` stay allowed, for `ImGuiController`); `System.Numerics.Vector2/3/4` stay allowed ([BUILD §2](../docs/BUILD.md#2-build)) |

## Constraints

- **`ControlHandler` before any control**; one per process.
- **Control names are unique across the process** (HUD and ImGui controls, case-sensitive): a duplicate makes the control's constructor throw `InvalidOperationException` (KI-010). For ImGui controls the name is also the ImGui ID, so repeat a label only with an ID suffix (`Position##Tank`).
- **`Width` sizes only a leaf control** (`ImGui.SetNextItemWidth`); containers ignore it, and a new ImGui control must not push state it does not pop (KI-018).
- **ImGui input goes through ImGui's event API** (`AddKeyEvent`, `AddMousePosEvent`, ...) in `ImGuiController`; ImGui.NET 1.91 has no `KeyMap` or `KeysDown`.
- **Events use `System.EventHandler<TSender, TEventArgs>`** (.NET 10); `Common`'s own delegate was deleted.
- **ImGui types use `System.Numerics` vectors**; conversion to OpenTK happens in the caller.
- **No game types here:** controls are generic; game-specific windows belong in `OldTanks`.
- **Renaming a control property or event** used in a `[BindableElement]` string breaks the generated code at compile time; update the attributes in `OldTanks/UI/ImGuiUI/ControlsInitialization/MainWindow.cs`.
