# OldTanks.UI.SourceGenerators

> **Project knowledge:** [../docs/PROJECT-KNOWLEDGE.md](../docs/PROJECT-KNOWLEDGE.md)

A Roslyn source generator that writes the bindings between ImGui controls and the objects they edit, driven by attributes on the debug window's fields.

## Related Docs

| Topic | Page |
|-------|------|
| Attributes, generated members, limits, changing the generator | [BINDING-GENERATOR](../docs/OldTanks.UI.SourceGenerators/BINDING-GENERATOR.md) |
| The controls it binds | [UI-CONTROLS](../docs/OldTanks.UI/UI-CONTROLS.md) |

## Project Details

| Property | Value |
|----------|-------|
| Namespace root | `OldTanks.UI.SourceGenerators` |
| Target framework | `netstandard2.0`, `LangVersion latest`, `EnforceExtendedAnalyzerRules` |
| Layer | Build-time analyzer; referenced by `OldTanks` with `OutputItemType="Analyzer"` |

## Key Types

| Type | Role |
|------|------|
| `Attributes.BindableClassAttribute`, `BindableElementAttribute`, `BindingWay`, `TriggerUpdateOnAttribute` | The attributes the game puts on its debug window |
| `Generators.BindingGenerator` | The `ISourceGenerator`; emits `<namespace>.MainWindow.g.cs` |
| `Generators.BindableClassesSyntaxReceiver` | Finds the class named `MainWindow` with `[BindableClass]` and reads its field attributes |
| `Generators.Extensions.SyntaxNodeExtensions`, `TypeExtensions` | Syntax walking; maps attribute arguments to constructor parameter names |

## Dependencies

| Dependency | Reason |
|------------|--------|
| `Microsoft.CodeAnalysis.CSharp` 4.7.0 | Roslyn APIs |
| `Microsoft.CodeAnalysis.Analyzers` 3.3.4 (private) | Analyzer rules for generator authors |

## Constraints

- **`netstandard2.0` only**: the compiler loads it; no newer BCL APIs, no project references.
- **Attribute constructor parameter names are part of the contract**: the generator looks arguments up by those names.
- **Generated code is pasted from strings**; after a change, rebuild, read the generated file and run the game's debug window.
- **One class, named `MainWindow`**: widening that is a design change; update BINDING-GENERATOR §4 with it.
