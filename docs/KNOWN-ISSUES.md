# Known Issues

| Field        | Value  |
|--------------|--------|
| Status       | Living - known-issues register |
| Last updated | 2026-11-25 |
| Verified against | 2026-11-25; bug/OT-7 (committed, rebased on master at d2f8b8a); KI-004, KI-008 and KI-020 to KI-022 observed in the running game on 2026-11-25, KI-005's fix confirmed by a run and KI-009's and KI-019's by tests, KI-012's new case and KI-023 to KI-025 read from code, the rest as of 2026-11-24 (reasoned from code, not observed) |

---

Defects that are understood and not yet fixed, with their root cause and the check that would close them. Rules for entries: [DOCUMENTATION-GUIDE §7](DOCUMENTATION-GUIDE.md#7-known-issue-entries). Unwired features and debt that is not a defect are in [BACKLOG](BACKLOG.md).

KI-001 to KI-019 were found by reading `develop` at `d32cbbf` on 2026-11-24 for the documentation start (OT-1); the machine had no .NET 7 runtime then. The game runs since OT-3 (.NET 10, 2026-11-25, [BUILD §3](BUILD.md#3-run)), and one smoke run confirmed KI-004 and KI-008 and found KI-020 and KI-022; the other entries of KI-001 to KI-019 are still reasoned from code, not observed. KI-020 to KI-023 come from OT-3 (KI-021 from its review, KI-023 from its decoder work); KI-024 and KI-025 come from OT-7 (KI-024 from its planning, KI-025 from its review) and are numbered provisionally after KI-023: they shift at the rebase if other branches' entries reach `master` first. KI-005, KI-009 and KI-019 were fixed in OT-7 (`bug/OT-7`) and are closed stubs. The remaining check of each entry says what would confirm it.

## Index

| KI | Title | Area | Status |
|----|-------|------|--------|
| [KI-001](#ki-001-the-player-is-never-assigned) | The player is never assigned | Game | Open |
| [KI-002](#ki-002-worldobjectrotation-is-read-as-radians-in-one-place-and-degrees-in-another) | `WorldObject.Rotation` is read as radians in one place and degrees in another | Game, physics | Open |
| [KI-003](#ki-003-worldobjects-is-changed-across-threads-without-a-shared-lock) | `WorldObjects` is changed across threads without a shared lock | Threads | Open |
| [KI-004](#ki-004-loggerservice-ignores-the-log-file-name) | `LoggerService` ignores the log file name | Logging | Open |
| [KI-005](#ki-005-untextured-objects-use-a-0-255-color-in-a-0-1-color-space) | Untextured objects use a 0-255 color in a 0-1 color space | Rendering | Closed |
| [KI-006](#ki-006-scenecenter-is-computed-from-a-wrong-maximum) | `Scene.Center` is computed from a wrong maximum | Engine | Open |
| [KI-007](#ki-007-robot-cannot-be-constructed) | `Robot` cannot be constructed | Game, assets | Open |
| [KI-008](#ki-008-unknown-shader-names-resolve-to-location-0) | Unknown shader names resolve to location 0 | Rendering | Open |
| [KI-009](#ki-009-instanced-rendering-skips-the-wrong-objects-when-some-are-hidden) | Instanced rendering skips the wrong objects when some are hidden | Rendering | Closed |
| [KI-010](#ki-010-control-names-are-not-checked-for-uniqueness) | Control names are not checked for uniqueness | UI | Open |
| [KI-011](#ki-011-dispatcherpost-swallows-callback-exceptions) | `Dispatcher.Post` swallows callback exceptions | Threads | Open |
| [KI-012](#ki-012-startup-continues-after-a-failed-asset-load) | Startup continues after a failed asset load | Startup | Open |
| [KI-013](#ki-013-window-size-settings-are-ignored) | Window size settings are ignored | Settings | Open |
| [KI-014](#ki-014-mouse-look-ignores-imgui-focus) | Mouse look ignores ImGui focus | Input | Open |
| [KI-015](#ki-015-unhandled-exceptions-are-never-logged) | Unhandled exceptions are never logged | Logging | Open |
| [KI-016](#ki-016-polygon-tests-use-face-normals-only) | Polygon tests use face normals only | Physics | Open |
| [KI-017](#ki-017-gl-objects-are-deleted-from-finalizers) | GL objects are deleted from finalizers | Rendering | Open |
| [KI-018](#ki-018-imguicontrolwidth-is-pushed-and-never-popped) | `ImGuiControl.Width` is pushed and never popped | UI | Open |
| [KI-019](#ki-019-objectrenderer-draws-nothing-without-an-instanced-shader) | `ObjectRenderer` draws nothing without an instanced shader | Rendering | Closed |
| [KI-020](#ki-020-three-shaders-fail-to-compile-and-are-used-anyway) | Three shaders fail to compile and are used anyway | Rendering | Open |
| [KI-021](#ki-021-generated-binding-code-calls-equals-before-its-null-check) | Generated binding code calls Equals before its null check | Generator | Closed |
| [KI-022](#ki-022-initcollision-prints-an-error-twice-at-startup) | InitCollision prints an error twice at startup | Physics | Open |
| [KI-023](#ki-023-png-decoding-cannot-see-a-zlib-streams-missing-end) | PNG decoding cannot see a zlib stream's missing end | Assets | Open |
| [KI-024](#ki-024-objectrenderert-drops-a-drawable-whose-texture-is-new-to-a-fully-instanced-scene) | `ObjectRenderer<T>` drops a drawable whose texture is new to a fully instanced scene | Rendering | Open |
| [KI-025](#ki-025-objectrenderert-can-keep-one-scene-texture-on-both-paths) | `ObjectRenderer<T>` can keep one scene texture on both paths | Rendering | Open |

---

## KI-001: The player is never assigned

| Field | Value |
|---|---|
| **Status** | Open |
| **Scope** | Player mode: key `1`, `MainWindow.HandleObjectMove`, `HandleKeyboardInputs`, `ChangeCameraMode` |
| **Root cause** | Nothing sets `OldTanks.Models.World.Player`. Key `1` computes the free-camera flag as "no player, or toggle", so it stays in free-camera mode, and the player branches never run |
| **Current state** | Only the free camera can be driven; the player movement, jump and follow camera are dead code |
| **Remaining check** | Assign a `WorldObject` (for example Dynamic Cube 1) to `World.Player` in `InitDefaultObjects`, press `1`, drive it, and fix what the first run shows (see KI-002) |
| **Evidence** | `World.Player` has a setter and no caller |

## KI-002: `WorldObject.Rotation` is read as radians in one place and degrees in another

| Field | Value |
|---|---|
| **Status** | Open |
| **Scope** | `OldTanks.Models.WorldObject`, the debug window's Rotation field, normals drawing, player turning |
| **Root cause** | `WorldObject.ApplyTransformation` passes `Yaw`, `Pitch` and `Roll` straight to `Matrix4.CreateRotation*` (radians), and the debug window converts degrees to radians (`OldTanks.Helpers.UIVectorHelper`). But `WorldObject.Move` and `WorldObject.SetCameraData` turn `Rotation.Y` into radians with `DegreesToRadians`, and so does `MainWindow.DrawNormals` for all three axes. The player's `A`/`D` add 1 per tick to `Pitch` |
| **Current state** | An object turned in the debug window moves and shows normals at a different angle from the one it is drawn at; the player would turn about 57° per tick |
| **Remaining check** | Pick radians as the stored unit, change the three degree readers, and check that a cube rotated 90° in the debug window draws its normals on its faces |
| **Evidence** | The symbols above |

## KI-003: `WorldObjects` is changed across threads without a shared lock

| Field | Value |
|---|---|
| **Status** | Open |
| **Scope** | `OldTanks.Models.World.WorldObjects`, the physics thread, the debug window's Spawn and Delete |
| **Root cause** | The physics thread (`MainWindow.WorldHandler`) loops over the collection by index in `World.CollideObjects` and in the ray test. The Spawn and Delete buttons add and remove items on the GL thread during the render frame. The render frame's `m_locker` and the write lock taken in the UI handling are not taken by the physics loop around its iteration |
| **Current state** | A removal during a physics step can throw `ArgumentOutOfRangeException`; `WorldHandler` catches it, prints it to the console and continues. A cube spawned after a sub-step's move loop is pair-tested before its first `ApplyTransformation`, while its collision copy is still in model space at the origin with an empty bounding box |
| **Remaining check** | Spawn and delete objects quickly while the scene runs and watch the console; then make the step and the UI changes share one lock, or queue world changes to the physics thread |
| **Evidence** | `MainWindow.WorldHandler`, `World.CollideObjects`, `OldTanks.UI.ImGuiUI.MainWindow.SpawnObjectOnClick` and `DeleteObjectOnClick` |

## KI-004: `LoggerService` ignores the log file name

| Field | Value |
|---|---|
| **Status** | Open |
| **Scope** | `OldTanks.Services.LoggerService.CreateLogger` and every logger it creates |
| **Root cause** | The `fileName` parameter (`ShaderLog.txt`, `TextureLog.txt`, `WaveFontLog.txt` from `GameManager`) is never used; every logger writes to `log.txt` in the working directory, each through its own file sink on that one path |
| **Current state** | The per-loader log files are never created. Confirmed in the running game on 2026-11-25: the loader messages reach the console only and never `log.txt`, so the later sinks do not write to the already-open file (or the file lacks their entries) |
| **Remaining check** | Look for Serilog self-log errors to learn why; use the parameter, or one shared logger with context |
| **Evidence** | `LoggerService.CreateLogger`, `GameManager`'s constructor |

## KI-005: Untextured objects use a 0-255 color in a 0-1 color space

| Field | Value |
|---|---|
| **Status** | Closed: fixed in OT-7 (`bug/OT-7`, 2026-11-25). The untextured colour in `CoolEngine.Services.Renderers.ObjectRenderer<T>` is rose #AA556F in 0-1 channels (each byte divided by 255), on the per-instance and the instanced path (Denys chose "Rose #AA556F (Recommended)"). Confirmed in the running game: a spawned cube drew pure white before the fix and rose after it, on both paths ([BUILD §3](BUILD.md#3-run)); also asserted by `CoolEngine.Tests` |
| **Scope** | `CoolEngine.Services.Renderers.ObjectRenderer<T>`, objects without a texture (every spawned cube) |

## KI-006: `Scene.Center` is computed from a wrong maximum

| Field | Value |
|---|---|
| **Status** | Open |
| **Scope** | `CoolEngine.GraphicalEngine.Core.Scene.EvaluateSceneCenter` |
| **Root cause** | The branch that should raise `max` writes into `min`, so `max` stays at `float.MinValue` |
| **Current state** | `Scene.Center` is wrong for every scene; nothing reads it yet |
| **Remaining check** | Fix the three assignments before the first use of `Center` |
| **Evidence** | `Scene.EvaluateSceneCenter`; no reader of `Scene.Center` in the solution |

## KI-007: `Robot` cannot be constructed

| Field | Value |
|---|---|
| **Status** | Open |
| **Scope** | `OldTanks.Models.Robot`, `Assets/Models/Default/Robot.obj` |
| **Root cause** | `Robot` asks `GlobalCache<Scene>` for `Robot`, but scenes are keyed by the OBJ's last object name, which in `Robot.obj` is `Cube.004_low_Cube.075` ([ASSET-PIPELINE §2](CoolEngine/Services/Loaders/ASSET-PIPELINE.md#2-cache-keys)) |
| **Current state** | `new Robot()` throws `ObjectException`; nothing constructs one |
| **Remaining check** | Key models by file name, or rename the object in the OBJ, then construct a `Robot` |
| **Evidence** | `grep "^o " Robot.obj` lists 155 objects, the last being `Cube.004_low_Cube.075` |

## KI-008: Unknown shader names resolve to location 0

| Field | Value |
|---|---|
| **Status** | Open |
| **Scope** | `CoolEngine.GraphicalEngine.Core.Shader` |
| **Root cause** | `GetUniformLocation`, `GetUniformBlockIndex` and `GetAttribLocation` return the default of a failed dictionary lookup, 0, instead of -1. `Shader.Create` logs compile errors but never checks the link status |
| **Current state** | A misspelt or optimised-away uniform is written to whatever sits at location 0; a program that fails to link is used silently. Confirmed in the running game on 2026-11-25: the three programs of [KI-020](#ki-020-three-shaders-fail-to-compile-and-are-used-anyway) never link and are used every frame, and their uniform uploads land on location 0 of the program that happens to be current |
| **Remaining check** | Return -1 for unknown names (GL ignores -1), log once per unknown name, and check `LinkStatus` in `Shader.Create` |
| **Evidence** | The three lookup methods and `Shader.Create` |

## KI-009: Instanced rendering skips the wrong objects when some are hidden

| Field | Value |
|---|---|
| **Status** | Closed: fixed in OT-7 (`bug/OT-7`, 2026-11-25). `ObjectRenderer<T>.FillInstanceRecords` packs one record per visible drawable from index 0 and returns the count, which `RenderInstanced` uploads and draws; the same change makes `RenderInstanced` return its `ArrayPool` rental on every path, which before was skipped when all drawables were hidden and on every frame for an instanced group emptied by deletes (spawn 17 or more cubes, delete them all). Confirmed by `CoolEngine.Tests` only: no object can be hidden in the game, nothing sets `Visible = false` |
| **Scope** | `ObjectRenderer<T>.RenderInstanced` |

## KI-010: Control names are not checked for uniqueness

| Field | Value |
|---|---|
| **Status** | Open |
| **Scope** | `OldTanks.UI.Services.ControlHandler.RegisterControl` |
| **Root cause** | The handler stores controls in a `HashSet<IControl>` built without a comparer, so it compares references; the `Equals` the controls implement as `IEqualityComparer<IControl>` (by name in `Control`, by name and `IsVisible` in `ImGuiControl`) is never used, and the "already exists" exception cannot fire |
| **Current state** | The HUD has two text blocks named `TB_FPS`. For ImGui controls the name is the ImGui ID, so duplicates in one window would share state |
| **Remaining check** | Pass a name comparer to the set, then fix the duplicates it reports |
| **Evidence** | `ControlHandler`'s constructor, `OldTanks.Windows.MainWindow.InitControls` |

## KI-011: `Dispatcher.Post` swallows callback exceptions

| Field | Value |
|---|---|
| **Status** | Open |
| **Scope** | `CoolEngine.Services.Threading.Dispatcher`, `DispatcherOperation`, the synchronization context |
| **Root cause** | `DispatcherOperation.Execute` stores any exception in its completion source. `Invoke` waits on it and rethrows, but nobody waits on a `Post` from another thread, so the exception is lost. (On the GL thread itself `Post` runs the callback inline, so nothing is queued) |
| **Current state** | Latent: no current code posts a throwing callback from another thread (await continuations of `async Task` methods keep their exceptions in their tasks). Related: an exception escaping an `async void` method on the GL thread is re-thrown through the inline `Post`, ends up on the thread pool and ends the process without a log line (KI-015); the one `async void` handler today, `TextureWindow.AddNewTextureButtonOnClick`, catches everything |
| **Remaining check** | Log the exception in `Execute` before storing it |
| **Evidence** | `DispatcherOperation.Execute`, `Dispatcher.Post(Delegate, object?)`, `DispatcherSynchronizationContext.Post` |

## KI-012: Startup continues after a failed asset load

| Field | Value |
|---|---|
| **Status** | Open |
| **Scope** | `OldTanks.Windows.MainWindow.OnLoad` |
| **Root cause** | The startup work is attached with `Task.WhenAll(...).ContinueWith(...)` without checking the antecedent, so it runs even when a load faulted (for example `DirectoryNotFoundException` when the working directory has no `Assets/`). It then fetches missing cache entries, throws on a thread-pool thread, and nobody observes the exception |
| **Current state** | Started from the wrong folder, the window stays empty with no error shown ([BUILD §3](BUILD.md#3-run)). Since OT-3 a texture file that is not PNG or JPEG also faults: `CoolEngine.Imaging.ImageDecoder` throws `NotSupportedException` (ImageSharp read BMP, GIF, TGA and WebP too) from `TextureLoader` or `SkyBoxTextureLoader`, and `OldTanks.Services.GameManager.LoadTextures` has no try/catch around it (reasoned from code) |
| **Remaining check** | Start the game from the repository root and confirm; then check the antecedent and log or show the failure |
| **Evidence** | `MainWindow.OnLoad`, `GameManager.Load*` |

## KI-013: Window size settings are ignored

| Field | Value |
|---|---|
| **Status** | Open |
| **Scope** | `OldTanks.Windows.MainWindow` constructors, `OldTanks.DataModels.Settings` |
| **Root cause** | `Program` uses the `(caption, settings, logger)` constructor, which always passes 800 × 600; `Settings.Width` and `Height` are saved but never read. The next constructor names its parameters `(height, width)` but uses them as (x, y). The `(GameWindowSettings, NativeWindowSettings, …)` constructor also registers the `SettingsService` itself, not the engine settings, under the name `EngineSettings` |
| **Current state** | The window always opens at 800 × 600 (`FullScreen` is honoured) |
| **Remaining check** | Build the window size from `Settings`, fix the parameter names, register `m_engineSettings` |
| **Evidence** | The three `MainWindow` constructors |

## KI-014: Mouse look ignores ImGui focus

| Field | Value |
|---|---|
| **Status** | Open |
| **Scope** | `MainWindow.OnMouseDown`, `HandleMouseMove` |
| **Root cause** | Any mouse button sets the look flag, without asking ImGui whether it wants the mouse (`WantCaptureMouse`) |
| **Current state** | Dragging a slider or a window in the debug UI also turns the camera |
| **Remaining check** | Skip mouse look while ImGui captures the mouse |
| **Evidence** | `OnMouseDown`, `HandleMouseMove` |

## KI-015: Unhandled exceptions are never logged

| Field | Value |
|---|---|
| **Status** | Open |
| **Scope** | `OldTanks.Program` |
| **Root cause** | `Program.CurrentDomainOnUnhandledException` exists but is never subscribed to `AppDomain.CurrentDomain.UnhandledException` |
| **Current state** | A crash leaves no `Fatal` entry in `log.txt` |
| **Remaining check** | Subscribe it at the start of `Main` |
| **Evidence** | `Program` |

## KI-016: Polygon tests use face normals only

| Field | Value |
|---|---|
| **Status** | Open |
| **Scope** | `CoolEngine.PhysicEngine.Core.Collision.Collision`: polygon × polygon and polygon × sphere |
| **Root cause** | The separating-axis tests try only the shapes' face normals. Boxes also need the cross products of their edge directions, and a sphere near a box corner needs the corner-to-centre axis |
| **Current state** | Rotated boxes meeting edge to edge, and spheres at box corners, can report contact where there is a gap |
| **Remaining check** | Place two cubes rotated 45° about different axes edge to edge and watch the debug view; add the missing axes |
| **Evidence** | `OBBCollisionCheckLoop`, `SphereOBBCollisionCheck` |

## KI-017: GL objects are deleted from finalizers

| Field | Value |
|---|---|
| **Status** | Open |
| **Scope** | `Buffer<T>`, `DrawObjectInfo`, `Shader`, `Texture` |
| **Root cause** | Their finalizers call `GL.Delete*`, but finalizers run on the finalizer thread, which has no GL context |
| **Current state** | A leaked object's delete call is invalid and the GL name is never freed. `Buffer<T>`, `Shader` and `Texture` log an "Abandoned" line at `Fatal` first; the `Texture` one says "Abandoned Shader" (copied text) |
| **Remaining check** | Queue the delete to the GL thread through the dispatcher from the finalizer, or only log there |
| **Evidence** | The four finalizers |

## KI-018: `ImGuiControl.Width` is pushed and never popped

| Field | Value |
|---|---|
| **Status** | Open |
| **Scope** | `OldTanks.UI.ImGuiControls.ImGuiControl.Draw` |
| **Root cause** | When `Width` is set, `Draw` calls `ImGui.PushItemWidth` and no code calls `PopItemWidth` |
| **Current state** | The width also applies to the controls drawn after it in the same window (the texture window's 200-pixel list boxes) |
| **Remaining check** | Pop after the derived control has drawn its widget |
| **Evidence** | `ImGuiControl.Draw`; no `PopItemWidth` in the solution |

## KI-019: `ObjectRenderer` draws nothing without an instanced shader

| Field | Value |
|---|---|
| **Status** | Closed: fixed in OT-7 (`bug/OT-7`, 2026-11-25). `ObjectRenderer<T>.AddDrawableToInstancedGroup` no longer returns while `InstancedShader` is null: drawables are always filed into the per-instance groups, and a group moves to the instanced set only once `InstancedShader` is set, before or after `DrawableItems` ([RENDERING §2](CoolEngine/Services/Renderers/RENDERING.md#2-objectrenderert-batching-and-instancing)). Confirmed by `CoolEngine.Tests` only: the game sets `InstancedShader` first, so a run could not show it |
| **Scope** | `CoolEngine.Services.Renderers.ObjectRenderer<T>` |

## KI-020: Three shaders fail to compile and are used anyway

| Field | Value |
|---|---|
| **Status** | Open |
| **Scope** | `FontShader`, `3DFontShader` (an input named `texture`) and `SkyBoxShader` (a uniform named `texture`) in `OldTanks/Assets/Shaders/`; `CoolEngine.GraphicalEngine.Core.Shader.Create`; HUD text, 3D object labels and the skybox |
| **Root cause** | The name `texture` hides GLSL's built-in `texture()`; the NVIDIA driver (621.24) reports `error C1105: cannot call a non-function`. The shader files and `Shader.Create` are unchanged from `master`, so this predates OT-3. `Shader.Create` logs the compile errors but never checks the link status ([KI-008](#ki-008-unknown-shader-names-resolve-to-location-0)), so the unlinked programs are cached and used every frame. `glUseProgram` on an unlinked program fails with `GL_INVALID_OPERATION`, so the HUD quads are drawn by whichever program was current before, with that program's 3D camera matrices; the uniform uploads go to location 0 of that program (KI-008), which produces the "Uniform must be a matrix type in call to UniformMatrix*" errors; and the single-channel glyph atlas bound to texture unit 0 is sampled as a colour texture. The chain is from the code and the log, not yet verified by a fix |
| **Current state** | Observed on 2026-11-25 (smoke run and Denys's own run, .NET 10, OpenGL 4.6 core on NVIDIA 621.24): no skybox (a plain clear-colour sky) and no HUD text; instead a garbled red-and-white vertical strip near the middle of the scene (Denys: the HUD "rendered at center with corrupted appearance"). About 45,000 GL error lines a second go to the console and `log.txt` (552,473 lines in 12 s on the first run). The ImGui debug window draws normally. The FreeTypeSharp 3 port in `CoolEngine.GraphicalEngine.Core.Font.Font` can only be confirmed visually once this is fixed; the console showed no font-loading error |
| **Remaining check** | Rename the input and the uniform in the three shader files; this needs no C# change ([RENDERING §4](CoolEngine/Services/Renderers/RENDERING.md#4-shader-contract): samplers are not set from C#); also check the link status in `Shader.Create` (KI-008). Then confirm the skybox and the HUD text in the running game and that the error flood is gone. Denys's decision (2026-11-25): "Separate bug task", a `bug/OT-<n>` branch after OT-3 (option turned down: fix it inside OT-3) |
| **Evidence** | The driver's compile log in the console on 2026-11-25; the three shader files; `Shader.Create` |

## KI-021: Generated binding code calls Equals before its null check

| Field | Value |
|---|---|
| **Status** | Closed on `bug/OT-11` (2026-11-25): `OldTanks.UI.SourceGenerators.Generators.BindingGenerator.CreateMembersBinding` now emits the `tmpObject is null` test before `tmpObject.Equals(...)`, so a sender of another type returns instead of throwing; the generated file changed in the three guard lines only and the 3 × CS8602 are gone (139 → 136 warnings, [BINDING-GENERATOR §3](OldTanks.UI.SourceGenerators/BINDING-GENERATOR.md#3-what-it-generates)) |

## KI-022: InitCollision prints an error twice at startup

| Field | Value |
|---|---|
| **Status** | Open |
| **Scope** | `CoolEngine.PhysicEngine.Core.Collision.Collision.InitCollision` |
| **Root cause** | Not analysed. The message is `InitCollision error. Cannot copy mesh from original collision because there are no vertices.` |
| **Current state** | Printed twice at startup with `Console.WriteLine` (not through Serilog), seen in the smoke run of 2026-11-25; the scene still builds. Whether it predates OT-3 is not established, though the physics code is unchanged from `master` |
| **Remaining check** | Break on the message at startup, find which collisions have no vertices, and decide whether the message or the data is wrong |
| **Evidence** | The console capture of 2026-11-25 |

## KI-023: PNG decoding cannot see a zlib stream's missing end

| Field | Value |
|---|---|
| **Status** | Open |
| **Scope** | `CoolEngine.Imaging.Png.PngDecoder` ([IMAGE-DECODERS §3](CoolEngine.Imaging/IMAGE-DECODERS.md#3-the-strict-rules)) |
| **Root cause** | The decoder inflates through `System.IO.Compression.ZLibStream`, which does not report a stream that lacks its final block or its Adler-32, or bytes after the stream inside the `IDAT` data |
| **Current state** | Such files still decode, although the decoder is strict everywhere else. Denys's decision (2026-11-25): "Document, backlog it" (option turned down: an own inflater inside OT-3) |
| **Remaining check** | An own deflate decoder with full stream-end and checksum checks ([BACKLOG](BACKLOG.md#own-libraries-and-engine-direction)); then add tests for the three cases |
| **Evidence** | The decoder's class remarks; the OT-3 review |

## KI-024: `ObjectRenderer<T>` drops a drawable whose texture is new to a fully instanced scene

| Field | Value |
|---|---|
| **Status** | Open |
| **Scope** | `CoolEngine.Services.Renderers.ObjectRenderer<T>`: `AddDrawable`, `AddDrawableToInstancedGroup`, `SortInstancedAndPerInstanceObjects` |
| **Root cause** | `AddDrawable` creates a scene's per-instance `DrawSceneInfo` only when the scene is in neither dictionary. Once every group of a scene has moved to the instanced set, the per-instance entry is gone (removed by the promotion in `AddDrawableToInstancedGroup` or by `SortInstancedAndPerInstanceObjects`), so a drawable whose texture has no group yet falls through every branch of `AddDrawableToInstancedGroup` and is filed nowhere |
| **Current state** | Latent: not reachable from the game today (only untextured cubes can be spawned, and the Cube scene's wall and floor groups are never promoted); it would show once textured objects are spawned in numbers. Found while planning OT-7, by reading. Denys's decision: "Firstly run, then fix if reproduced" (option turned down: fix it in OT-7 without a run); the run could not reach it (reasoned from code) |
| **Remaining check** | Create the per-instance `DrawSceneInfo` on demand in that branch. A unit test in `CoolEngine.Tests`: 17 drawables of one texture with an instanced shader set, then one of the same scene with a new texture, which must be in a group |
| **Evidence** | OT-7 planning (reading), 2026-11-25 |

## KI-025: `ObjectRenderer<T>` can keep one scene texture on both paths

| Field | Value |
|---|---|
| **Status** | Open |
| **Scope** | `CoolEngine.Services.Renderers.ObjectRenderer<T>`: `DrawableTextureChanged`, `AddDrawableToInstancedGroup`, `SortInstancedAndPerInstanceObjects` |
| **Root cause** | The per-instance and the instanced group sets of a scene can both hold a group for the same texture: a per-instance drawable retextured to a texture whose group is instanced (older than OT-7), and since OT-7 an instanced drawable retextured to a texture that has a per-instance group (`DrawableTextureChanged` keeps the drawable on the path it was on) |
| **Current state** | Drawing stays correct, since each path draws its own group. But `AddDrawableToInstancedGroup` never promotes that texture's per-instance group again (the move needs no instanced group for the texture; the OT-7 reviewer's probe added 20 more drawables and the per-instance group reached 21, unpromoted), and setting `InstancedShader` a second time throws `ArgumentException` ("An item with the same key has already been added") in `SortInstancedAndPerInstanceObjects`. Reachable in the game (retexture a spawned instanced cube to the wall texture or FloorTile) but harmless today: nothing joins that group afterwards and the game sets `InstancedShader` once. Not fixed under Denys's rule "Firstly run, then fix if reproduced": it is not a visible failure in today's game (Claude's application of the rule) |
| **Remaining check** | Choose the scene info by where the new texture's group already lives, for both directions, and make `SortInstancedAndPerInstanceObjects` merge into an existing instanced group |
| **Evidence** | The OT-7 review (a scratch probe, not kept), 2026-11-25 |
