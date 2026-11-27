# Code Style

| Field        | Value  |
|--------------|--------|
| Status       | Living |
| Last updated | 2026-11-27 |
| Verified against | 2026-11-27; feature/OT-4 rebased on master at 0f9cb48 (the test-projects line against the project files and the OT-4 plan record, as checked at 05e364f; after the rebase the merged text read against the OT-14 to OT-19 pages); bug/OT-14 at da7db97 ("UI units" and the test-projects line against the code and `tests/OldTanks.Tests`); 2026-11-26; feature/OT-5 at 98ab147 ("Math types" and the test-projects line against the code, `BannedSymbols.txt` and the project files); feature/OT-5 at fb61096 ("Math changes" and the engine-library line against the project files and the library); 2026-11-25; bug/OT-8 rebased on master at 826e560 (OT-7); the renderer exception rule as Denys decided it on 2026-11-25; the language, events and package rules against the project files and `Directory.Packages.props`, Logging against the OT-8 code; the rest as of 2026-11-24 (develop at d32cbbf) |

---

The conventions the code already follows, written down so new code matches. Where the code is mixed, the rule names the majority form; don't rename old code only for style.

## Language and project settings

- **C# latest (`LangVersion latest`) on `net10.0`** with `ImplicitUsings` and `Nullable` enabled in every runtime project; the generator is `netstandard2.0` with `LangVersion latest`.
- **Package versions live only in `Directory.Packages.props`.** A `<PackageReference>` in a project file has no `Version`; a new package is added there first. Roslyn packages follow the SDK's compiler (BUILD, ARCHITECTURE §1).
- **Engine libraries take no packages** unless the plan says so: `CoolEngine.Imaging` and `CoolEngine.Mathematics` have none, on purpose ([IMAGE-DECODERS](CoolEngine.Imaging/IMAGE-DECODERS.md), [MATHEMATICS](CoolEngine.Mathematics/MATHEMATICS.md)).
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
- **GL calls only on the GL thread** ([ARCHITECTURE §3](ARCHITECTURE.md#3-getting-work-onto-the-gl-thread)). A type that owns GL objects implements `IDisposable`. **A finalizer makes no GL call** (it runs on the finalizer thread, which has no context): it logs a Warning and queues the names with `GLDeletionQueue.Enqueue`, and the host drains the queue on the GL thread with `DeletePending` ([RENDERING §3](CoolEngine/Services/Renderers/RENDERING.md#3-gpu-side-types), OT-15).
- **No exception handling in the per-frame renderer path.** No `try`, `catch` or `finally`, and no `using` block or declaration (it compiles to `try`/`finally`), in a renderer's draw method or anything it calls each frame. Exception handling belongs only to the high-level methods that call the render pipeline, which are meant to catch and re-run the whole pipeline. No such handler exists yet: today an exception in the render path ends the game, unlogged ([KI-015](KNOWN-ISSUES.md#ki-015-unhandled-exceptions-are-never-logged), [BACKLOG](BACKLOG.md#game-and-engine-features-not-built)). A torn frame, a UI freeze or a lost pooled buffer (`ArrayPool` rental) on an exception is accepted. Denys, 2026-11-25, rejecting a `try`/`finally` around the rental in `ObjectRenderer<T>.RenderInstanced`: "Note, that any renderer pipeline (in loop) should not use try..catch..finaly. Only hight-level methods with calls. So, it is acceptable that we might face with some UI freeze and torn render pipeline on exception which will be handled on high-level caller which will re-run whole render pipeline." ([RENDERING §2](CoolEngine/Services/Renderers/RENDERING.md#2-objectrenderert-batching-and-instancing))
- **`#region`** is used to group overrides and object setup in long classes; optional.

## Math types

Since the move of OT-5 (2026-11-26, merged into `master` by PR #20) the engine, UI, game and their tests share one set of math types ([MATHEMATICS](CoolEngine.Mathematics/MATHEMATICS.md)).

- **Vectors and quaternions are `System.Numerics`:** `Vector2`, `Vector3`, `Vector4` and `Quaternion`, in the engine, the game and the ImGui controls alike (ImGui.NET takes those types too). An OpenTK-style normalizing axis-angle is `CoolEngine.Services.Misc.QuaternionHelper.FromAxisAngle` (System.Numerics' `Quaternion.CreateFromAxisAngle` expects a unit axis and does not special-case zero).
- **Matrices and `MathHelper` are `CoolEngine.Mathematics`:** `Matrix2`, `Matrix3`, `Matrix4` (column vectors, column-major) and `CoolEngine.Mathematics.MathHelper` (`DegreesToRadians`, `PiOver4`); clamp and lerp come from the BCL (`Math.Clamp`, `float.Lerp`). The double types are there where `double` is needed.
- **Column vectors, so a product reads right to left:** `translation * rotation * scale` applies the scale first. `OldTanks.Models.WorldObject` builds translation × rotation Z × rotation Y × rotation X × scale. A `v * M` of the old OpenTK code is `M * v` now (or `TransformPosition` and `TransformDirection`).
- **OpenTK's math is banned** (RS0030, an error in `Common`, `CoolEngine`, `OldTanks.UI`, `OldTanks`, `tests/CoolEngine.Tests` and `tests/OldTanks.Tests`): `Matrix2`, `Matrix3`, `Matrix4` and the non-square matrices, `Vector3`, `Vector4`, `Quaternion`, `MathHelper`, the `Box` types, the integer and half vectors and the double versions. `Vector2`, `Vector2i` and `Color4` stay allowed, for the window boundary (`OldTanks.Windows.MainWindow`, `OldTanks.UI.Services.ImGUI.ImGuiController`, and since OT-16 `OldTanks.Windows.WindowMetrics` and `OldTanks.UI.Services.ImGUI.ImGuiDisplayMetrics`, which take `Vector2i` sizes) and the GL clear colour; convert there. `Half` and the Bezier types are not banned yet ([BACKLOG](BACKLOG.md#own-libraries-and-engine-direction)).
- **Also banned:** `System.Numerics.Matrix4x4`, `Matrix3x2`, `Plane` and the vector Transform overloads that take them (row-vector types). The list is `BannedSymbols.txt` at the repository root; it flags uses, not declarations. The error message names the replacement.
- **Vector text:** `CoolEngine.Services.Extensions.VectorExtensions.ToDisplayString` (OpenTK's `(x, y, z)` with the list separator) for anything shown in the HUD, the debug view or a log line that the fonts must draw; System.Numerics' `ToString` writes `<x, y, z>` with a group separator the fonts may lack.
- **GL uploads:** uniforms as floats (`GL.UniformMatrix4(location, 1, false, ref m.Column0.X)`), generic buffer calls take the structs as they are ([MATHEMATICS §5](CoolEngine.Mathematics/MATHEMATICS.md#5-the-opengl-memory-layout)).
- **A refactor of math keeps its behaviour provable:** record golden values from the old code first, port, and compare within a tolerance (`CoolEngine.Tests.TestSupport.GoldenAssert`).
- **UI units:** `OldTanks.Helpers.UIVectorHelper` converts where the UI shows degrees and the object stores radians, and every reader of `WorldObject.Rotation` reads radians since OT-14, only `CameraOffsetAngle` stays in degrees ([KI-002](KNOWN-ISSUES.md#ki-002-worldobjectrotation-is-read-as-radians-in-one-place-and-degrees-in-another)). `CoolEngine.Services.Extensions.VectorExtensions.ToGLVector3` and its siblings are identities kept for the debug window's binding attributes.
- **Float comparison:** `Common.Extensions.PrimitiveExtensions.ApproximateEqual` **with an explicit epsilon**. Its default, `float.Epsilon` (about 1.4e-45), makes it an exact comparison, and the existing callers (`Sphere`, `Collision.SpherePolygonSatCollisionCheck`) rely on that default.

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
- **Test projects** live under `tests/`, use NUnit (as ArbBot and AnalysisTools do) and are named `<Project>.Tests`, or `OldTanks.Docs.Tests` for the documentation checks. `CoolEngine.Imaging.Tests` is the first code test project; `CoolEngine.Tests` (OT-7; the dispatcher's tests since OT-8) holds the engine's headless unit tests, so a test there may not make an OpenGL call (no context exists); `GL`-calling types such as `Texture` and `Shader` are built by reflection, and the helpers of `ObjectRendererTests` keep `GC.SuppressFinalize` on them so that their leak lines (each finalizer logs a Warning, [KI-017](KNOWN-ISSUES.md#ki-017-gl-objects-are-deleted-from-finalizers), closed) stay out of other fixtures' log captures; the finalizer tests of OT-15 run the finalizer on purpose. In `CoolEngine.Tests`, the namespace `CoolEngine.Tests.GraphicalEngine.Core.Texture` shadows the type name `Texture` for test code in `CoolEngine.Tests.GraphicalEngine.Core`: `TextureTests` uses the alias `EngineTexture`. `OldTanks.Tests` (OT-5; OT-14) holds the headless golden-value tests of `OldTanks.Models.WorldObject` and two behaviour tests of its `Move` and `SetCameraData`, built from fake scenes in the cache (no window, no GL), and since OT-16 the size rules of `OldTanks.Windows.WindowMetrics`. `CoolEngine.Fonts.Tests` (OT-4) is Windows-only (`net10.0-windows`: FreeType 2.13.2 through FreeTypeSharp and DirectWrite are its references, [FONTS §6](CoolEngine.Fonts/FONTS.md#6-how-it-is-tested)) and the one test project that allows unsafe code (`AllowUnsafeBlocks`, for FreeTypeSharp's pointer API and the COM source generator); the font library itself has none. `OldTanks.UI.Tests` (OT-10) tests the UI layer, `ImGuiDisplayMetrics` since OT-16; its tests share one `ControlHandler` per process, so each uses unique control names ([UI-CONTROLS §1](OldTanks.UI/UI-CONTROLS.md#1-controlhandler)).
- **Never feed a mutated or fuzzed font to the native FreeType** (the font tests' reference, `freetype.dll`): crafted and fuzzed fonts go to `CoolEngine.Fonts` only; the native library sees real fonts (the repository's, Source Sans 3 and the Windows fonts read in place, [FONTS §6](CoolEngine.Fonts/FONTS.md#6-how-it-is-tested)).
- **Math changes** (`CoolEngine.Mathematics`, since OT-5) are reviewed at effort `max` by an `opus` or `fable` reviewer ([plan D7](plans/ENGINES-AND-TANK-GAME.md#5-open-decisions)); a member that uses only + − × ÷ and sqrt keeps the same-bits promise and the four-path test passes, and the affected benchmark categories are re-run and reported against the reference figures, a median more than 5% slower being a review finding ([MATHEMATICS §7](CoolEngine.Mathematics/MATHEMATICS.md#7-benchmarks); [MATHEMATICS §3](CoolEngine.Mathematics/MATHEMATICS.md#3-the-same-bits-promise), [BUILD §2](BUILD.md#2-build)).
