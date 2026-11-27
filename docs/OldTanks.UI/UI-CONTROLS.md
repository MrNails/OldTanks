# UI Controls

| Field        | Value  |
|--------------|--------|
| Status       | Living |
| Last updated | 2026-11-27 |
| Verified against | 2026-11-27; bug/OT-16 rebased on master at 4b425be (§5's `ImGuiController` constructor, `WindowResized` and `ImGuiDisplayMetrics` rows against the code and `ImGuiDisplayMetricsTests`; the mentor's app check of 2026-11-27; the other sections not rechecked); 2026-11-25; bug/OT-10 rebased on master at 483d63f, and bug/OT-9 (the `ImGuiTextBlock` row, read from code); §1's names and §3's width against the code, `tests/OldTanks.UI.Tests` and two runs of the game (the debug window; the texture window opened, closed, reopened, collapsed and expanded; its list-box widths again after the "Textures" width); §3's event type and §5's input against the code; the ImGui input also ran in the smoke run (the window drew), but typing, dragging and scrolling were not tried; the rest as of 2026-11-24 |

---

`OldTanks.UI` gives the game two kinds of on-screen UI: engine-drawn screen text (the HUD) and retained-mode wrappers over Dear ImGui (the debug windows). Both are tracked by one `ControlHandler`. The game's own windows are in the `OldTanks` project ([features §5](../OldTanks/FEATURES-OVERVIEW.md#5-debug-windows)); their bindings are generated ([BINDING-GENERATOR](../OldTanks.UI.SourceGenerators/BINDING-GENERATOR.md)).

## 1. `ControlHandler`

`OldTanks.UI.Services.ControlHandler` is a process-wide singleton (`ControlHandler.Current`):

- **Create it first.** Every control's constructor registers itself through `ControlHandler.Current`, so constructing a control before the handler exists throws `NullReferenceException`; a second handler throws `InvalidOperationException`. The game creates it in `OldTanks.Windows.MainWindow`'s constructor before any window.
- **Windows:** an `ImGuiWindow` registers itself as a window; `HandleControls` draws every registered window, and the game calls it once per render frame.
- **Parent links:** adding a control to a container (`ControlCollection.Add`, `ImGuiControlContainer.Child`) links it to that parent. A control can have one parent; linking it twice throws `OldTanks.UI.Services.CustomExceptions.ControlLinkedException`.
- **Names:** the handler keeps controls in a dictionary keyed by `Name` (`StringComparer.Ordinal`, so case-sensitive, as ImGui's label hashes are). Names are unique across the whole process, HUD text and ImGui controls alike: `RegisterControl` throws `InvalidOperationException` ("Control with name ... already exists.") from the constructor of any control whose name is taken ([KI-010](../KNOWN-ISSUES.md#ki-010-control-names-are-not-checked-for-uniqueness), closed on `bug/OT-10`). `UnRegisterControl` (internal, no caller) removes an entry only when the control under that name is the same instance.
- **Names and ImGui IDs:** for ImGui controls the name is also the ImGui label and ID. The process-wide rule is stricter than ImGui's per-window IDs, so a later window that repeats a label such as "Position" must give it an ID suffix (for example `Position##Tank`). The check compares whole names, so it does not see ImGui's `###` ID override (`A###x` and `B###x` pass it but collide in ImGui; nothing uses `###` today). `IControl.Name` has a public setter, which lets a registered control be renamed past the check ([KI-029](../KNOWN-ISSUES.md#ki-029-a-control-renamed-after-construction-bypasses-the-name-check)).
- **Where a duplicate throws:** where the control is built. Debug-window controls: in `OldTanks.Windows.MainWindow`'s constructor (a crash with no log line, [KI-015](../KNOWN-ISSUES.md#ki-015-unhandled-exceptions-are-never-logged)). Texture-window controls: inside the render frame when the window first opens. HUD text blocks: in `OldTanks.Windows.MainWindow.InitControls`, which runs from `OnFontsLoaded` inside `GameManager.LoadFonts`' task, so the task faults and startup carries on ([KI-012](../KNOWN-ISSUES.md#ki-012-startup-continues-after-a-failed-asset-load)). The HUD's second `TB_FPS` (the colliding-objects-per-second block) is `TB_CollidingPS` since OT-10; no other name repeats.

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

**Width** ([KI-018](../KNOWN-ISSUES.md#ki-018-imguicontrolwidth-is-pushed-and-never-popped), closed on `bug/OT-10`): `ImGuiControl.Draw` calls `ImGui.SetNextItemWidth` when `Width` is set, so it sizes only a leaf control's own widget and nothing is pushed; `ImGuiCheckBox` applies it after its visibility check. The containers `ImGuiPanel`, `ImGuiTreeNode`, `ImGuiColumnsControl` and `ImGuiWindow` do not call the base `Draw`, so they ignore `Width`. `ImGuiImage`, `ImGuiSameLine` and `ImGuiNewLine` never applied it. `ImGuiWindow` draws its `Child` only when the window is open and `ImGui.Begin` returned true (a collapsed window draws nothing inside; `End` is always called). In the texture window "Object textures", "Meshes" and "Textures" are 200 px wide, each by its own `Width`; the "Asset path" text box takes the window's default item width (before OT-10 it inherited 200 px from the list boxes). "Textures" got its `Width` after the first app check (Denys, 2026-11-25): at the default width it started beside the 256 × 256 texture image and ran past the window's right edge, which clipped it and hid its label. Seen in the running game on 2026-11-25 (`bug/OT-10`, 800 × 600 window): the three list boxes at 200 px with their labels inside the window, "Asset path" at about 375 px.

**Tests:** `tests/OldTanks.UI.Tests` ([BUILD §2](../BUILD.md#2-build)) covers names (`OldTanks.UI.Tests.Services.ControlHandlerTests`) and width (`OldTanks.UI.Tests.ImGuiControls.ImGuiControlTests`, a headless ImGui context, no GL). The one `ControlHandler` per test process cannot be reset, so every test uses unique names.

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
| `Render()` | Last in every render frame that draws; while the framebuffer is empty (a minimised window) the game draws no frame at all, this call included, and only swaps buffers |
| `PressChar`, `MouseScroll` | From the window's text-input and mouse-wheel events (`MouseScroll` calls `AddMouseWheelEvent`) |
| `WindowResized(Vector2i clientSize, Vector2i framebufferSize)` | On a window or framebuffer resize, from `OldTanks.Windows.MainWindow.ApplyWindowSize` |

**Sizes (since OT-16, [KI-032](../KNOWN-ISSUES.md#ki-032-the-viewport-projections-imgui-and-render-target-use-the-windows-outer-size) closed).** The constructor and `WindowResized` take `(Vector2i clientSize, Vector2i framebufferSize)` and keep an `OldTanks.UI.Services.ImGUI.ImGuiDisplayMetrics`, a pure type (no GL, no ImGui context; `TryCreate` rejects any empty size):

| Member | Value |
|--------|-------|
| `DisplaySize` | The client size, the unit of the mouse position |
| `FramebufferScale` | Framebuffer size divided by client size (1 on Windows, where the two are equal) |
| `FramebufferHeight` | The framebuffer's height, for the scissor box |
| `CreateProjection` | The top-left orthographic projection of the draw data |
| `GetScissorBox` | The scissor rectangle of a draw command, flipped with the framebuffer height |

Before OT-16 the controller was given the outer window size (`NativeWindow.Size`) and subtracted a fixed 39-pixel frame, which did not follow the display scale or the window theme; the offset and the scale-factor field are gone. UI pixel sizes are unchanged above 100% display scale: a DPI-aware UI is in [BACKLOG](../BACKLOG.md#game-and-engine-features-not-built) (Denys, 2026-11-27).

The game never disposes the controller.

## 6. Adding a debug window

1. Create `OldTanks/UI/ImGuiUI/<Name>.cs` (behaviour) and `OldTanks/UI/ImGuiUI/ControlsInitialization/<Name>.cs` (fields and an `InitializeComponents` that builds the tree and sets `Child`), as `partial class <Name> : ImGuiWindow`; `TextureWindow` is the template.
2. Construct it after the `ControlHandler` exists, set `Title`, and call `Show()`.
3. Wire events by hand: the generator only serves a class named `MainWindow` ([BINDING-GENERATOR §4](../OldTanks.UI.SourceGenerators/BINDING-GENERATOR.md#4-limits)).
4. Handlers run on the GL thread inside the render frame; anything that touches `World.WorldObjects` races the physics thread ([KI-003](../KNOWN-ISSUES.md#ki-003-worldobjects-is-changed-across-threads-without-a-shared-lock)).
