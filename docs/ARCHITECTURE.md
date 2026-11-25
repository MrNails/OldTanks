# Architecture

| Field        | Value  |
|--------------|--------|
| Status       | Living |
| Last updated | 2026-11-25 |
| Verified against | 2026-11-25; bug/OT-10 rebased on master at 483d63f (§1's test projects and §5's `ControlHandler` row against the project files and the code); bug/OT-9 on master at d749ec8 (§2 physics row and §4 startup and frame, checked in the game on 2026-11-25); bug/OT-8 rebased on master at 826e560 (OT-7); §1 against the project files and Directory.Packages.props, §3 (posted-callback errors) and §4 (logging and crashes) against the OT-8 code, its tests and a smoke run, the other sections as of 2026-11-24 (develop at d32cbbf, reasoned from code) |

---

How the six projects fit together, which thread does what, and the process-wide state everything leans on. Feature detail lives on the feature pages linked from each section.

## 1. Solution layout

`OldTanks.slnx`, six SDK-style projects, all `net10.0` with `LangVersion latest` except the generator (`netstandard2.0`), plus four test projects under `tests/`: `OldTanks.Docs.Tests` (`net10.0`, no project references; it checks the docs, [DOCUMENTATION-GUIDE §9](DOCUMENTATION-GUIDE.md#9-checks)), `CoolEngine.Tests` (`net10.0`, headless; it references `CoolEngine`, which grants it `InternalsVisibleTo`; `ObjectRenderer<T>` tests since OT-7, dispatcher tests with a Serilog sink they swap into `Log.Logger` since OT-8; [BUILD §2](BUILD.md#2-build)), `OldTanks.UI.Tests` (`net10.0`; it references `OldTanks.UI`; it tests the control handler and the ImGui controls on a headless ImGui context, [UI-CONTROLS](OldTanks.UI/UI-CONTROLS.md#1-controlhandler); OT-10) and `CoolEngine.Imaging.Tests` (`net10.0-windows`; it references `CoolEngine.Imaging`; [IMAGE-DECODERS §6](CoolEngine.Imaging/IMAGE-DECODERS.md#6-how-it-is-tested)).

| Project | Type | Target | Role |
|---------|------|--------|------|
| `Common` | Library | `net10.0` | Dependency-free helpers: `Common.Models.ObservableObject`, `Common.Services.SettingsService` (JSON settings), `Common.Infrastructure.EventArgs.ValueChangedEventArgs<T>`, math and collection extensions. Its own typed-sender `EventHandler<TSender, TArg>` was deleted in OT-3 in favour of .NET 10's `System.EventHandler<TSender, TEventArgs>` |
| `CoolEngine.Imaging` | Library | `net10.0` | Strict PNG and JPEG decoders into RGBA8; no dependencies ([IMAGE-DECODERS](CoolEngine.Imaging/IMAGE-DECODERS.md)) |
| `CoolEngine` | Library | `net10.0`, unsafe allowed, C# latest | The engine: OpenGL rendering, asset loaders, physics and collision detection, the dispatcher, global caches |
| `OldTanks.UI` | Library | `net10.0` | UI layer over the engine: the `ControlHandler`, screen-text controls, ImGui control wrappers, the ImGui renderer |
| `OldTanks.UI.SourceGenerators` | Roslyn source generator | `netstandard2.0` | Generates two-way bindings for the ImGui debug window from attributes ([BINDING-GENERATOR](OldTanks.UI.SourceGenerators/BINDING-GENERATOR.md)) |
| `OldTanks` | Console exe | `net10.0`, C# latest | The game: `Program`, the OpenTK window, the world and its objects, the ImGui debug windows, the `Assets/` folder |

**References** (exactly as in the `.csproj` files):

| Project | References |
|---------|-----------|
| `Common` | none |
| `CoolEngine.Imaging` | none (no packages either) |
| `CoolEngine` | `Common`, `CoolEngine.Imaging` |
| `OldTanks.UI` | `Common`, `CoolEngine` |
| `OldTanks.UI.SourceGenerators` | none (Roslyn packages only) |
| `OldTanks` | `CoolEngine`, `OldTanks.UI`, and `OldTanks.UI.SourceGenerators` with `OutputItemType="Analyzer"` (it runs as a generator and its attribute types are also referenced) |

**Packages** (versions as of 2026-11-25): `CoolEngine` has OpenTK 4.9.4, FreeTypeSharp 3.1.0, Serilog 4.4.0 and `Serilog.Sinks.File` 7.0.0. `OldTanks.UI` has ImGui.NET 1.91.6.1 and OpenTK 4.9.4. `OldTanks` adds `Serilog.Sinks.Console` 6.1.1. The generator uses `Microsoft.CodeAnalysis.CSharp` and `Microsoft.CodeAnalysis.Analyzers` 5.9.0, held at the Roslyn of SDK 10.0.401 (a generator built against a newer one does not load, CS9057). The test projects use Microsoft.NET.Test.Sdk 18.12.0, NUnit 5.0.0, NUnit.Analyzers 4.15.0 and NUnit3TestAdapter 6.3.1; the imaging tests also SixLabors.ImageSharp 4.1.4 and System.Drawing.Common 11.0.0 (reference decoders, never referenced by a runtime project). **Central package management:** every version is set once in `Directory.Packages.props` at the repository root; project files list `<PackageReference>` without a `Version`. MIConvexHull, `Serilog.Sinks.FastConsole` and SixLabors.ImageSharp (in the engine) were removed in OT-3.

## 2. Threads

| Thread | Started by | Runs | Must not |
|--------|-----------|------|----------|
| **Main / GL thread** | `OldTanks.Program.Main` | The OpenTK `GameWindow` loop (`OldTanks.Windows.MainWindow.OnUpdateFrame`, `OnRenderFrame`), every OpenGL call, ImGui, and the dispatcher queue | Block on work queued to itself |
| **Physics thread** (`WorldHandler`, background) | `OldTanks.Windows.MainWindow.InitDefaultObjects` | Every ~5 ms: free-camera movement (skipped while ImGui wants the keyboard: it reads `m_imGuiWantsKeyboard` once per step), mouse look, `OldTanks.Models.World.CollideObjects`, the picking-ray test | Call OpenGL |
| **Thread pool** | Awaited I/O inside `OldTanks.Services.GameManager.Load*`, and `Task.Run` in `Font.CreateFont` | File reads, image decoding, font rasterising. The loads themselves are `async` methods started on the main thread, so OBJ parsing, pixel copying and every GL upload run there, interleaved with frames (§3) | Call OpenGL directly |
| **Spawner thread** (`SpawnObjects`, background) | Startup continuation | A 10 000-cube stress test, disabled by an early `return` | — |

The window is created with an OpenGL **4.6** forward-compatible **debug** context; `CoolEngine.Services.Extensions.LoggerExtensions.AddGLMessageHandling` routes GL debug messages to Serilog (errors and performance as `Error`, portability and undefined behaviour as `Warning`).

## 3. Getting work onto the GL thread

`CoolEngine.Services.Application` is created once in `Main`. Its constructor builds a `CoolEngine.Services.Threading.Dispatcher` bound to the main thread and installs a `DispatcherSynchronizationContext` there. `MainWindow.OnUpdateFrame` drains the queue every frame with `Dispatcher.HandleQueue`.

| Mechanism | Used by | Effect |
|-----------|---------|--------|
| `await` inside an async method started on the main thread | Shader, texture and skybox loaders | The continuation after `await` (where `Shader.Create` / `Texture.CreateTexture2D` run) is posted to the dispatcher and runs on the main thread |
| `CoolEngine.Services.UI.UIInvoke` (`Dispatcher.Invoke`) | `CoolEngine.GraphicalEngine.Core.Font.Font.CreateFont` inside `Task.Run` | Blocks the worker until the main thread has run the callback |
| Lazy GL object creation inside `Render` | `ObjectRenderer<T>`, `CollisionRenderer`, `TextRenderer` | Objects can be registered from any thread; VAOs and buffers are created on first draw |

**Rules that follow:** `Invoke` and `Post` called on the main thread run the callback inline; from another thread they queue it until the next update frame (`Invoke` then blocks until it has run).

**Exceptions in posted callbacks** (since OT-8, KI-011 closed on `bug/OT-8`, 2026-11-25): when a callback queued by `Post` from another thread throws, `DispatcherOperation.Execute` stores the exception as before, marks it observed (so `TaskScheduler.UnobservedTaskException` does not report it a second time), logs it at `Error` through `Log.Logger` ("A callback posted to the dispatcher threw an exception: {Callback}") and the queue goes on. `Invoke` is unchanged: its caller gets the exception as an `AggregateException` and nothing is logged. `Post` run inline on the main thread gives the exception to its caller. `DispatcherSynchronizationContext` is unchanged.

| Case | Result |
|------|--------|
| `Dispatcher.Post` called directly from another thread | `Error` line |
| An exception escaping an `async void` method off the GL thread | `Error` line |
| Await continuation of an `async Task` method | The exception stays in its task; nothing is logged |
| An exception escaping an `async void` method **on** the GL thread | The inline `Post` rethrows and .NET rethrows it on the thread pool: the process ends, now with a `Fatal` line from the handler in §4. Open question for Denys: whether to log and survive it too |

`{Callback}` names the delegate's method; on those runtime paths that is a .NET lambda (for example `System.Threading.Tasks.Task+<>c.<ThrowAsync>…`), so the exception's stack trace, not `{Callback}`, names the failing method. The one `async void` handler today, `TextureWindow.AddNewTextureButtonOnClick`, catches everything. The `Post` rows are checked by the tests of `CoolEngine.Tests`; the `async void` and continuation rows by the OT-8 review's probes (2026-11-25).

## 4. Startup and the frame

**Startup** (`OldTanks.Program.Main`): create the one `OldTanks.Services.LoggerService` and set `Log.Logger` to its root logger, subscribe the two exception handlers (below), then `Application`, load or create `%USERPROFILE%\OldTanksProject\AppSettings.json` ([BUILD §4](BUILD.md#4-files-the-game-reads-and-writes)), construct `MainWindow`, `Run()`. The rest of `Main` runs in `try`/`finally` with `Log.CloseAndFlush()`.

**Logging and crashes** (since OT-8, KI-004 and KI-015 closed on `bug/OT-8`, 2026-11-25):

| Part | Behaviour |
|------|-----------|
| Root logger | Built once in the `LoggerService` constructor: level `Debug`, console and `log.txt` in the working directory, enriched with `SourceContext` `OldTanks`. `CreateLogger(source)` returns the root tagged with `source`; null or blank gives the root; it never creates a sink |
| `Program.CurrentDomainOnUnhandledException` | Subscribed to `AppDomain.CurrentDomain.UnhandledException`; the terminating branch logs `Fatal` and calls `Log.CloseAndFlush()`. On .NET 10 `IsTerminating` is always true, so its `Error` branch is effectively unreachable (the OT-8 review's crash probes, 2026-11-25) |
| `Program.TaskSchedulerOnUnobservedTaskException` | Subscribed to `TaskScheduler.UnobservedTaskException`; logs `Error`, does not call `SetObserved`. Raised from a faulted task's finalizer: late (after a GC collects the task) or never if the process exits first, so it is not a dependable report of [KI-012](KNOWN-ISSUES.md#ki-012-startup-continues-after-a-failed-asset-load) |
| Crash order on .NET 10 | On the main thread the handler runs first, then the `finally` blocks (so lines logged while the stack unwinds reach the console only); on another thread `Main`'s `finally` never runs, so the handler's own flush is the one that matters |
| After `Log.CloseAndFlush()` | `Log.Logger` is a silent logger; loggers handed out earlier still write to the console and their file writes fail silently (Serilog reports them only to its `SelfLog`). Finalizer lines after exit ([KI-017](KNOWN-ISSUES.md#ki-017-gl-objects-are-deleted-from-finalizers)) are lost |

`MainWindow.OnLoad` turns on VSync, creates the `ImGuiController`, starts the five `GameManager` loads (shaders, fonts, textures, skyboxes, models) without waiting for any, and attaches a `Task.WhenAll(...).ContinueWith` that, on a thread-pool thread: registers the skybox shader with `ObjectRendererOld`, builds the default scene (`InitDefaultObjects`, which also starts the physics thread), hands `World.WorldObjects` to the renderers, and sets `m_readyToHandle`. Until then `OnRenderFrame` returns at once and `OnUpdateFrame` only drains the dispatcher (which is what lets the loads finish) and checks `Esc`, so the window stays empty while assets load.

**Startup failure** (since OT-9, [KI-012](KNOWN-ISSUES.md#ki-012-startup-continues-after-a-failed-asset-load)): the continuation checks the antecedent task and wraps its own body in a `try`. On a faulted load or a throw, `MainWindow.FailStartup` (still on the thread-pool thread) logs each exception with `Log.Logger.Error` (console and `log.txt`) and publishes the message with one write to the `volatile` field `m_startupError`; it makes no GL call and creates no control. `m_readyToHandle` stays false. The GL thread picks the message up in `OnUpdateFrame` and builds the "Startup failed" ImGui window there (`OldTanks.UI.ImGuiUI.StartupErrorWindow`, created by `OldTanks.Windows.MainWindow.ShowStartupError`, [FEATURES-OVERVIEW §5](OldTanks/FEATURES-OVERVIEW.md#5-debug-windows)). If the throw comes after `InitDefaultObjects`, the physics thread already runs behind the error screen (harmless; it is a background thread and is joined on exit). A single texture or skybox file that fails to decode never gets here: `OldTanks.Services.GameManager.LoadTextures` and `LoadSkyBoxes` log it and skip it ([ASSET-PIPELINE §3](CoolEngine/Services/Loaders/ASSET-PIPELINE.md)); a missing folder still faults the load.

**Update frame (GL thread):** drain the dispatcher; `Esc` closes on the key's press edge (`KeyboardState.IsKeyPressed`, one frame) and not while ImGui wants the keyboard; if `m_startupError` is set, show the error window on the first such frame (and `Show` it again if it was closed), run `ImGuiController.Update` and return; otherwise, once ready, feed input to ImGui with `ImGuiController.Update` and then copy ImGui's `WantCaptureMouse` and `WantCaptureKeyboard` into the `volatile` fields `m_imGuiWantsMouse` and `m_imGuiWantsKeyboard` (`UpdateImGuiInputClaims`, which ImGui computes in `NewFrame` inside that update); refresh HUD text. The input handlers (`OnMouseDown`, `OnMouseWheel`, `OnKeyDown`, `OnKeyUp`) and the physics thread read those fields: the claims lag the input by up to one frame ([FEATURES-OVERVIEW §3](OldTanks/FEATURES-OVERVIEW.md#3-controls)).

**Render frame:** on the error screen it clears, runs `ControlHandler.HandleControls` (inside the `GlobalLock` write lock), `ImGuiController.Render` and swaps buffers. Otherwise (under `m_locker`): clear; skybox with depth test `LEQUAL` (not drawn when `World.SkyBox.Texture` is null); the world through `ObjectRenderer<WorldObject>` or, in debug view, through `CollisionRenderer`; red primitives (normals if on, the picking ray); HUD text blocks; object names as 3D text; ImGui windows via `ControlHandler.HandleControls` (inside `EngineSettings.Current.GlobalLock`'s write lock); `ImGuiController.Render`; swap buffers.

## 5. Process-wide state

Everything below is a static or a singleton, so there is one window, one world and one UI tree per process, and a test would have to reset them. The UI tests do not: they share the one `ControlHandler` of the test process and use unique control names.

| Holder | What | Notes |
|--------|------|-------|
| `CoolEngine.Services.Application.Current` | Dispatcher and synchronization context | A second instance throws |
| `CoolEngine.Services.EngineSettings.Current` | Projection matrices, window size, `CollisionIterations` (1-128, the game sets 5), `GlobalLock`, `PhysicsEnable` | Lazily created |
| `CoolEngine.Services.GlobalCache<T>.Default` | Name-keyed registries for `Shader`, `Texture`, `Scene`, `FontInformation`, `CollisionData` | One per `T`; key rules in [ASSET-PIPELINE](CoolEngine/Services/Loaders/ASSET-PIPELINE.md#2-cache-keys) |
| `OldTanks.Services.LoggerService` (one per process) and `Serilog.Log.Logger` | The root logger and its console and `log.txt` sinks | A second `LoggerService` would open a second file sink on `log.txt`, which fails (its XML doc says so) |
| `OldTanks.UI.Services.ControlHandler.Current` | Every UI control and window | Must exist before any control is constructed; a second instance throws; control names are unique or the control's constructor throws ([UI-CONTROLS §1](OldTanks.UI/UI-CONTROLS.md#1-controlhandler)) |
| Static renderers | `ObjectRendererOld`, `CollisionRenderer`, `TextRenderer` | `CollisionRenderer.Shader` and `TextRenderer.Shader` are set in `MainWindow.OnShadersLoaded`; `ObjectRendererOld` gets the skybox shader through `RegisterScene` in the startup continuation |
| `OldTanks.Services.EngineSettings` (game side) | `MaxDepthLength` 2000 (far plane), `MovementDirectionUnit` (+X) | Not the same class as the engine's `EngineSettings` |

## 6. Synchronization

| Lock | Taken by | Protects |
|------|----------|----------|
| `MainWindow.m_locker` | The render frame; the (disabled) spawner | Render against the spawner adding cubes |
| `EngineSettings.Current.GlobalLock` (`ReaderWriterLockSlim`, only its write lock is used) | `OldTanks.Models.WorldObject.Move` around each position update; the render frame around ImGui handling | Position writes against UI edits |

Nothing locks `World.WorldObjects` as a whole: the physics thread iterates it by index while ImGui's Spawn and Delete buttons change it on the main thread ([KI-003](KNOWN-ISSUES.md#ki-003-worldobjects-is-changed-across-threads-without-a-shared-lock)).

## 7. Spaces and units

- **Axes:** Y up, gravity along -Y (`CoolEngine.PhysicEngine.PhysicsConstants.GravityDirection`). The default camera sits at the origin looking along +X.
- **Matrices:** OpenTK row-vector convention. A world object's transform is scale (`Size / 2`) × rotation X (`Yaw`) × rotation Y (`-Pitch`) × rotation Z (`Roll`) × translation; model-space meshes and collision shapes span -1..1.
- **Projection:** perspective, FOV 45° (mouse wheel clamps it to 20-90°), near 0.1, far 2000; the screen projection is orthographic with the origin at the bottom-left, and `EngineSettings.GetWindowY` flips a top-left y.
- **Rotation units are mixed:** see [KI-002](KNOWN-ISSUES.md#ki-002-worldobjectrotation-is-read-as-radians-in-one-place-and-degrees-in-another).
