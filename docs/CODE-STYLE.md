# Code Style

| Field        | Value  |
|--------------|--------|
| Status       | Living |
| Last updated | 2026-11-25 |
| Verified against | 2026-11-25; bug/OT-8 rebased on master at 826e560 (OT-7); the renderer exception rule as Denys decided it on 2026-11-25; the language, events and package rules against the project files and `Directory.Packages.props`, Logging against the OT-8 code; the rest as of 2026-11-24 (develop at d32cbbf) |

---

The conventions the code already follows, written down so new code matches. Where the code is mixed, the rule names the majority form; don't rename old code only for style.

## Language and project settings

- **C# latest (`LangVersion latest`) on `net10.0`** with `ImplicitUsings` and `Nullable` enabled in every runtime project; the generator is `netstandard2.0` with `LangVersion latest`.
- **Package versions live only in `Directory.Packages.props`.** A `<PackageReference>` in a project file has no `Version`; a new package is added there first. Roslyn packages follow the SDK's compiler (BUILD, ARCHITECTURE §1).
- **Engine libraries take no packages** unless the plan says so: `CoolEngine.Imaging` has none, on purpose ([IMAGE-DECODERS](CoolEngine.Imaging/IMAGE-DECODERS.md)).
- **Nullable:** new code adds no nullable warnings to the [baseline](BUILD.md#2-build). Use `?` for "may be absent" and the `!` operator only where an invariant guarantees a value (a cache entry that startup has loaded).
- **Unsafe code** only in `CoolEngine` (`AllowUnsafeBlocks`), for `sizeof` and buffer uploads.
- **File-scoped namespaces** (`namespace X;`) matching the folder path. A few files still use block namespaces.

## Naming

| Element | Form | Example |
|---------|------|---------|
| Private instance field | `m_camelCase` | `m_currentCamera` |
| Private static field, including `static readonly` | `s_camelCase` | `s_instance`, `s_maxRenderAmount` |
| Private `const` | `PascalCase` (a few use `PascalCase_`) | `DefaultRenderCapacity` |
| Public static readonly field | `PascalCase` | `PhysicsConstants.FreeFallingAcceleration` |
| Types, methods, properties, events | `PascalCase` | `ApplyTransformation` |
| Event handler method | `<Source>On<Event>` (the IDE default) or `<Source><Event>` | `SpawnObjectOnClick`, `WorldObjectsCollectionChanged` |
| Generated members | `__` prefix | `__RigidBodyGeneratedBindingMethodToObject` (never write `__` names by hand) |

Exceptions exist: `RigidBody` (`_isStatic`) and the engine's `EngineSettings` (`_current`) use a bare `_`; some static fields use `m_` (`CollisionRenderer.m_drawCollisions`, `ObjectRendererOld.m_sceneBuffers`); some `static readonly` constants use `PascalCase_` (`MaxShaderAttributeSize_`). Leave them until the file is touched for other reasons.

## Patterns

- **Observable properties:** derive from `Common.Models.ObservableObject` and set through `SetField(ref m_field, value)`, which raises `PropertyChanged` only on change. A property that changes a part of another (`WorldObject.X` of `Position`) raises both names.
- **Events:** `System.EventHandler<TSender, TEventArgs>` (new in .NET 10) with a typed sender; value changes use `Common.Infrastructure.EventArgs.ValueChangedEventArgs<T>`. `Common`'s own delegate of the same shape was deleted in OT-3 (Denys: "Use the .NET 10 one").
- **Window classes are split in two partial files:** behaviour in `<Name>.cs`, controls (fields and the method that builds the tree) in `ControlsInitialization/<Name>.cs`; the game window's HUD is in `Windows/ControlWindows/MainWindow.cs`.
- **Caches by name:** shared assets are fetched from `CoolEngine.Services.GlobalCache<T>.Default` by name; keep names in a constants class (`OldTanks.Infrastructure.CollisionConstants`) rather than as scattered literals.
- **GL calls only on the GL thread** ([ARCHITECTURE §3](ARCHITECTURE.md#3-getting-work-onto-the-gl-thread)). A type that owns GL objects implements `IDisposable`.
- **No exception handling in the per-frame renderer path.** No `try`, `catch` or `finally`, and no `using` block or declaration (it compiles to `try`/`finally`), in a renderer's draw method or anything it calls each frame. Exception handling belongs only to the high-level methods that call the render pipeline, which are meant to catch and re-run the whole pipeline. No such handler exists yet: today an exception in the render path ends the game, unlogged ([KI-015](KNOWN-ISSUES.md#ki-015-unhandled-exceptions-are-never-logged), [BACKLOG](BACKLOG.md#game-and-engine-features-not-built)). A torn frame, a UI freeze or a lost pooled buffer (`ArrayPool` rental) on an exception is accepted. Denys, 2026-11-25, rejecting a `try`/`finally` around the rental in `ObjectRenderer<T>.RenderInstanced`: "Note, that any renderer pipeline (in loop) should not use try..catch..finaly. Only hight-level methods with calls. So, it is acceptable that we might face with some UI freeze and torn render pipeline on exception which will be handled on high-level caller which will re-run whole render pipeline." ([RENDERING §2](CoolEngine/Services/Renderers/RENDERING.md#2-objectrenderert-batching-and-instancing))
- **`#region`** is used to group overrides and object setup in long classes; optional.

## Math types

- **OpenTK.Mathematics** (`Vector3`, `Matrix4`, `Quaternion`) everywhere in the engine and the game.
- **System.Numerics** only in ImGui controls, because ImGui.NET takes those types.
- **Convert at the boundary** with `CoolEngine.Services.Extensions.VectorExtensions` (`ToGLVector3`, `ToSystemVector3`, and the `Vector2` pair), and with `OldTanks.Helpers.UIVectorHelper` where the UI shows degrees and the object stores radians.
- **Float comparison:** `Common.Extensions.PrimitiveExtensions.ApproximateEqual` **with an explicit epsilon**. Its default, `float.Epsilon` (about 1.4e-45), makes it an exact comparison, and the existing callers (`Sphere`, `Collision.SphereOBBCollisionCheck`) rely on that default.

## Logging

- **Serilog** `ILogger` from `OldTanks.Services.LoggerService.CreateLogger(source)`, passing the class name with `nameof` (`nameof(GameManager)`); the source shows as `[source]` on every line of the one `log.txt`. Engine code, which cannot reach the game's service, uses the static `Log.Logger`.
- **One `LoggerService` per process:** it builds the root logger and its file sink once; a second one would open a second sink on `log.txt`, which fails. `CreateLogger` never creates a sink ([ARCHITECTURE §4](ARCHITECTURE.md#4-startup-and-the-frame)).
- **Message templates with named properties** (`"Model {ModelName} loaded."`), never string interpolation in the template.
- **GL debug output** already reaches the log through `AddGLMessageHandling`; don't add `GL.GetError` polling.

## Comments and language

- Code, comments, logs and commit messages in **English**. (`RigidBody` has two Russian comments, "density" and "restitution"; translate them when the file is next edited.)
- XML doc comments on engine APIs whose behaviour is not obvious from the name (`Dispatcher.Invoke` vs `Post`, `EngineSettings.GetWindowY`).
- `TODO:` comments exist; an item worth doing goes to [BACKLOG](BACKLOG.md) too.

## Git rules

| Rule | Format |
|------|--------|
| Feature branch | `feature/OT-<n>`: new work, improvements, docs (e.g. `feature/OT-1`) |
| Bug branch | `bug/OT-<n>`: a fix for something that does not work as intended |
| Task number | One sequence across both prefixes, incrementing, never reused. OT-1 is the documentation start (2026-11-24); take the highest `OT-<n>` in `git log --all` plus one |
| Base and target | Branch from `master`; open the pull request into `master` (Denys, 2026-11-24). `develop` is no longer used: it stopped at PR #7 and lacks OT-1 |
| Commit message | The task id, then the change in the imperative, first line ≤ 72 characters (e.g. `OT-1 Add project documentation`) |

The branch naming is Denys's rule (2026-11-24). Before it, branches had descriptive names (`bug/CollisionFix`, `bug/FixDeterminingOnGroundState`) and commit subjects were past-tense sentences. The commit-message form is carried over from ArbBot with the documentation start; change it here if it does not fit.

- **No build output or IDE state:** `bin/`, `obj/`, `.vs/`, `.idea/` are git-ignored.
- **Build and test before committing:** `dotnet build OldTanks.slnx` with 0 errors and no new warnings, then `dotnet test OldTanks.slnx`.
- **Test projects** live under `tests/`, use NUnit (as ArbBot and AnalysisTools do) and are named `<Project>.Tests`, or `OldTanks.Docs.Tests` for the documentation checks. `CoolEngine.Imaging.Tests` is the first code test project; `CoolEngine.Tests` (OT-7; the dispatcher's tests since OT-8) holds the engine's headless unit tests, so a test there may not make an OpenGL call (no context exists); `GL`-calling types such as `Texture` and `Shader` are built by reflection with finalizers suppressed ([KI-017](KNOWN-ISSUES.md#ki-017-gl-objects-are-deleted-from-finalizers)). `OldTanks.UI.Tests` (OT-10) tests the UI layer; its tests share one `ControlHandler` per process, so each uses unique control names ([UI-CONTROLS §1](OldTanks.UI/UI-CONTROLS.md#1-controlhandler)).
- **Math changes** (once `CoolEngine.Mathematics` exists, OT-5) are reviewed at effort `max` by an `opus` or `fable` reviewer ([plan D7](plans/ENGINES-AND-TANK-GAME.md#5-open-decisions)).
