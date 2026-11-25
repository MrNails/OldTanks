# UI Controls

| Field        | Value  |
|--------------|--------|
| Status       | Living |
| Last updated | 2026-11-25 |
| Verified against | 2026-11-25; feature/OT-3, uncommitted, and bug/OT-9 (the `ImGuiTextBlock` row, read from code); §3's event type and §5's input against the code; the ImGui input also ran in the smoke run (the window drew), but typing, dragging and scrolling were not tried; the rest as of 2026-11-24 |

---

`OldTanks.UI` gives the game two kinds of on-screen UI: engine-drawn screen text (the HUD) and retained-mode wrappers over Dear ImGui (the debug windows). Both are tracked by one `ControlHandler`. The game's own windows are in the `OldTanks` project ([features §5](../OldTanks/FEATURES-OVERVIEW.md#5-debug-windows)); their bindings are generated ([BINDING-GENERATOR](../OldTanks.UI.SourceGenerators/BINDING-GENERATOR.md)).

## 1. `ControlHandler`

`OldTanks.UI.Services.ControlHandler` is a process-wide singleton (`ControlHandler.Current`):

- **Create it first.** Every control's constructor registers itself through `ControlHandler.Current`, so constructing a control before the handler exists throws `NullReferenceException`; a second handler throws `InvalidOperationException`. The game creates it in `OldTanks.Windows.MainWindow`'s constructor before any window.
- **Windows:** an `ImGuiWindow` registers itself as a window; `HandleControls` draws every registered window, and the game calls it once per render frame.
- **Parent links:** adding a control to a container (`ControlCollection.Add`, `ImGuiControlContainer.Child`) links it to that parent. A control can have one parent; linking it twice throws `OldTanks.UI.Services.CustomExceptions.ControlLinkedException`.
- **Names:** the handler means to reject duplicate names but compares controls by reference ([KI-010](../KNOWN-ISSUES.md#ki-010-control-names-are-not-checked-for-uniqueness)). For ImGui controls the name is also the ImGui label and ID, so keep names unique within a window.

## 2. Screen text controls (`OldTanks.UI.Controls`)

| Type | Role |
|------|------|
| `Control` | Base: `Name`, `Position`, `Size`, `Rotation`, `IsVisible`, `Width` |
| `TextControl` | `Text`, `Font`, `Color` (white by default); estimates `Size` from text length and font size. The alignment properties exist but nothing reads them |
| `TextBlock` | Draws its text with `CoolEngine.Services.Renderers.TextRenderer.DrawText2D`; `Position` is in pixels from the window's top-left |

These are not drawn by the handler: the owner keeps them in a list and calls `Draw` itself (`OldTanks.Windows.MainWindow` does, every render frame).

## 3. ImGui controls (`OldTanks.UI.ImGuiControls`)

Each wraps one ImGui widget, keeps its state between frames and raises events with a typed sender (`System.EventHandler<TSender, TEventArgs>`, .NET 10's; `Common`'s own delegate of that shape was deleted in OT-3). Vector values use `System.Numerics` types; convert to OpenTK at the boundary ([CODE-STYLE](../CODE-STYLE.md#math-types)).

| Control | State and events |
|---------|------------------|
| `ImGuiWindow` | `Title`, `IsVisible` (starts hidden), `Show`, `Close`, one `Child`; the title-bar close button clears `IsVisible` |
| `ImGuiPanel` | `Children`, drawn in the order added (the collection is a `HashSet`, so the order is an implementation detail that holds while children are only added) |
| `ImGuiColumnsControl` | `Children`, `ColumnsAmount`, `SetColumn(control, column)`; only children given a column are drawn |
| `ImGuiTreeNode` | `Children`, `IsExpanded` |
| `ImGuiButton` | `Click` |
| `ImGuiCheckBox` | `IsChecked`, `Checked` |
| `ImGuiTextBlock` | `Text`, drawn verbatim with `ImGui.TextUnformatted` (since OT-9: `ImGui.Text` read its string as a printf format, so a `%` garbled it, `%n` ended the process and text over 3 KB was cut); a null `Text` draws an empty line |
| `ImGuiTextBox` | `Text`, `MaxLength`, password, read-only and input flags |
| `ImGuiFloatDragTextBox`, `ImGuiFloat2DragTextBox`, `ImGuiFloat3DragTextBox`, `ImGuiFloat4DragTextBox` | `Value` (`float`, `Vector2`, `Vector3`, `Vector4`), `DragDelta`, `ValueChanged`, raised for user drags and for changes set from code |
| `ImGuiListBox<T>` | `Items` (an `ObservableCollection<T>`, followed live), `BindingFunction` (item → label, `ToString` by default), `SelectedItem`, `SelectedIndex`, `SelectionChanged`, `ClearSelection`; label buffers come from the `ArrayPool<string>` passed to the constructor |
| `ImGuiImage` | `Texture` (a GL handle), `Size` |
| `ImGuiSameLine`, `ImGuiNewLine` | Layout only |

Setting `Width` pushes an ImGui item width that is never popped ([KI-018](../KNOWN-ISSUES.md#ki-018-imguicontrolwidth-is-pushed-and-never-popped)).

## 4. Other types

| Type | Status |
|------|--------|
| `OldTanks.UI.Services.Binding<TObject, TObjectData, TControlData>` | A hand-written property-change binding; not used (the generator replaced it) |
| `OldTanks.UI.GlobalSettings.FloatComparisonTolerance` | Not used |

## 5. The ImGui renderer

`OldTanks.UI.Services.ImGUI.ImGuiController` owns the ImGui context. It compiles its own GLSL 460 shader from strings, builds the font atlas from ImGui's default font, and draws ImGui's lists with a scissor per command, saving and restoring GL state through `CoolEngine.GraphicalEngine.Services.GLSettings`.

| Call | When the game makes it |
|------|------------------------|
| `Update(window, dt)` | Every update frame: ends the previous ImGui frame, feeds mouse, keyboard and typed characters, starts a new frame. Since OT-3 input goes through ImGui's event API: `AddMousePosEvent`, `AddMouseButtonEvent` (left, right, middle), `AddKeyEvent` for every OpenTK key with an `ImGuiKey` counterpart (`TranslateKey`) plus the `ModCtrl`, `ModShift`, `ModAlt` and `ModSuper` keys, and `AddInputCharacter` for typed text (ImGui.NET 1.91 removed `KeyMap` and `KeysDown`) |
| `Render()` | Last in every render frame |
| `PressChar`, `MouseScroll` | From the window's text-input and mouse-wheel events (`MouseScroll` calls `AddMouseWheelEvent`) |
| `WindowResized` | On resize |

The game never disposes the controller.

## 6. Adding a debug window

1. Create `OldTanks/UI/ImGuiUI/<Name>.cs` (behaviour) and `OldTanks/UI/ImGuiUI/ControlsInitialization/<Name>.cs` (fields and an `InitializeComponents` that builds the tree and sets `Child`), as `partial class <Name> : ImGuiWindow`; `TextureWindow` is the template.
2. Construct it after the `ControlHandler` exists, set `Title`, and call `Show()`.
3. Wire events by hand: the generator only serves a class named `MainWindow` ([BINDING-GENERATOR §4](../OldTanks.UI.SourceGenerators/BINDING-GENERATOR.md#4-limits)).
4. Handlers run on the GL thread inside the render frame; anything that touches `World.WorldObjects` races the physics thread ([KI-003](../KNOWN-ISSUES.md#ki-003-worldobjects-is-changed-across-threads-without-a-shared-lock)).
