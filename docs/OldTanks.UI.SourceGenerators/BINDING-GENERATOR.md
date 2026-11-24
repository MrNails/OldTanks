# Binding Generator

| Field        | Value  |
|--------------|--------|
| Status       | Living |
| Last updated | 2026-11-24 |
| Verified against | 2026-11-24; develop at d32cbbf; whole page |

---

`OldTanks.UI.SourceGenerators` is a Roslyn source generator that writes the glue between ImGui controls and the objects they edit, so the debug window has no hand-written binding code. Today it serves exactly one class, `OldTanks.UI.ImGuiUI.MainWindow` (the ImGui "Debug window"), whose attributed fields are in `OldTanks/UI/ImGuiUI/ControlsInitialization/MainWindow.cs`.

## 1. How it is wired

- **Project:** `netstandard2.0`, `LangVersion latest`, `Microsoft.CodeAnalysis.CSharp` 4.7.0. `OldTanks.csproj` references it with `OutputItemType="Analyzer"`; the attribute types in `OldTanks.UI.SourceGenerators.Attributes` come from the same assembly.
- **Generator:** `OldTanks.UI.SourceGenerators.Generators.BindingGenerator`, a v1 `ISourceGenerator` with a syntax receiver, `BindableClassesSyntaxReceiver`. An `IIncrementalGenerator` version is sketched in a comment at the end of the file.
- **Output:** one file, `<namespace>.MainWindow.g.cs`, with a `partial` part of the class. To read it, build with `EmitCompilerGeneratedFiles` set to `true` (it lands under `obj/`), or open the analyzer node in the IDE.

## 2. Attributes

All arguments are strings that the generator pastes into C# **as written**: they are not checked against the compilation, so a typo becomes a compile error in the generated file.

| Attribute | On | Arguments |
|-----------|----|-----------|
| `[BindableClass]` | The class (on the partial part that also declares the attributed fields) | — |
| `[BindableElement(...)]` (the attribute allows several per field, but only one `TwoWay` or `OneWayToSource` binding per field compiles, §4) | A control field | `fromProperty` (control property, e.g. `Value`); `toProperty` (object property, e.g. `Position`); `toObject` (an expression from the class to the object, e.g. `m_worldObjectsListBox.SelectedItem.RigidBody`); `castObjectAs` (the object's full type name); `sourceTrigger` (the control's event, e.g. `ValueChanged`); `sourceTriggerArgs` (that event's full parameter list in parentheses); optional `converterFrom` (control → object) and `converterTo` (object → control), as fully-qualified static method names; optional `bindingWay` |
| `[TriggerUpdateOn(...)]` | A control field | `triggeredFromEvent` (e.g. `SelectionChanged`); `eventParams` (the parameter list, no parentheses); optional `checkOnNullElement` (an expression; skip the refresh when it is null) |

`BindingWay` is `TwoWay` (default), `OneWay` (object → control) or `OneWayToSource` (control → object).

## 3. What it generates

| Generated member | For | Does |
|------------------|-----|------|
| `InitGeneratedData()` | The class | Subscribes every control-side handler to its `sourceTrigger`, subscribes each `TriggerUpdateOn` handler and runs it once. The class declares `partial void InitGeneratedData();` and calls it at the end of its `Initialize` |
| `__<field>GeneratedBindingMethod(<sourceTriggerArgs>)` | `TwoWay` and `OneWayToSource` | Control → object: returns if `toObject` is null, else sets `toObject.toProperty` from `field.fromProperty`, through `converterFrom` if given |
| `__<last segment of toObject>GeneratedBindingMethodToObject(sender, PropertyChangedEventArgs)` | `TwoWay` and `OneWay`, one method per distinct `toObject` | Object → control: if `sender` is the current `toObject`, switches on the property name and sets the control, through `converterTo` if given |
| `__<field>UpdateBindingsHandler(...)` and `__<field>UpdateBindings()` | Each `TriggerUpdateOn` | Re-reads **every** `OneWay` and `TwoWay` binding of the class from its object, after the null check |

**You subscribe the object side yourself.** The generator cannot know when the object behind `toObject` changes, so the class adds each `...GeneratedBindingMethodToObject` to the objects' `PropertyChanged`. `OldTanks.UI.ImGuiUI.MainWindow` does it in its constructor (current camera, every world object, every rigid body) and in `WorldObjectsCollectionChanged` for objects added or removed later. The `TriggerUpdateOn` on the world-objects list box is what refreshes the panel when the selection changes.

**Null safety:** a `toObject` with more than one `.` is emitted with `?.` between its parts for the null checks, so a deep path such as `m_worldObjectsListBox.SelectedItem.RigidBody` is safe while nothing is selected.

## 4. Limits

| Limit | Consequence |
|-------|-------------|
| The receiver only looks at classes **named `MainWindow`** that carry `[BindableClass]`, and keeps one | No other window can use generated bindings; `OldTanks.UI.ImGuiUI.TextureWindow` is wired by hand |
| Attributes are read from the syntax of the `[BindableClass]` part only | Attributed fields declared in another partial part are ignored |
| Arguments are read as text: a positional string literal as its value, a named argument as its whole source text (split at `:`), anything else as an empty string | **String arguments must be positional** (a named one keeps its quotes and breaks the generated code). **`bindingWay` must be named** (`bindingWay: BindingWay.OneWayToSource`); passed positionally it reads as empty and silently falls back to `TwoWay`. A string literal containing `:` is misread as a named argument |
| A field's control → object method is emitted once per attribute, each time covering all of the field's attributes | Two `TwoWay` or `OneWayToSource` bindings on one field generate duplicate members and fail to compile |
| Object-side method names use only the last segment of `toObject` | Two different paths ending in the same name generate the same method twice and fail to compile |
| `[TriggerUpdateOn]` refreshes all bindings, not just its field's | Fine at the current size; costly with many bindings |

## 5. Changing the generator

- Rebuild the solution after a change; an IDE may keep the old generator loaded until it restarts.
- After a rebuild, inspect the generated file (§1) before running the game.
- Keep the attribute constructor parameter **names** stable: the receiver maps positional arguments to the widest constructor's parameter names (`GetBiggestCtorParams`), and the generator reads them by those names (`toObject`, `sourceTrigger`, ...).
