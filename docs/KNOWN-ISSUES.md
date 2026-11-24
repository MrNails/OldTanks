# Known Issues

| Field        | Value  |
|--------------|--------|
| Status       | Living - known-issues register |
| Last updated | 2026-11-24 |
| Verified against | 2026-11-24; develop at d32cbbf; root causes of KI-001 to KI-019 (not observed at run time) |

---

Defects that are understood and not yet fixed, with their root cause and the check that would close them. Rules for entries: [DOCUMENTATION-GUIDE §7](DOCUMENTATION-GUIDE.md#7-known-issue-entries). Unwired features and debt that is not a defect are in [BACKLOG](BACKLOG.md).

All entries below were found by reading `develop` at `d32cbbf` on 2026-11-24 for the documentation start (OT-1). None was observed in the running game: the machine had no .NET 7 runtime ([BUILD §3](BUILD.md#3-run)). The remaining check of each says what would confirm it.

## Index

| KI | Title | Area | Status |
|----|-------|------|--------|
| [KI-001](#ki-001-the-player-is-never-assigned) | The player is never assigned | Game | Open |
| [KI-002](#ki-002-worldobjectrotation-is-read-as-radians-in-one-place-and-degrees-in-another) | `WorldObject.Rotation` is read as radians in one place and degrees in another | Game, physics | Open |
| [KI-003](#ki-003-worldobjects-is-changed-across-threads-without-a-shared-lock) | `WorldObjects` is changed across threads without a shared lock | Threads | Open |
| [KI-004](#ki-004-loggerservice-ignores-the-log-file-name) | `LoggerService` ignores the log file name | Logging | Open |
| [KI-005](#ki-005-untextured-objects-use-a-0-255-color-in-a-0-1-color-space) | Untextured objects use a 0-255 color in a 0-1 color space | Rendering | Open |
| [KI-006](#ki-006-scenecenter-is-computed-from-a-wrong-maximum) | `Scene.Center` is computed from a wrong maximum | Engine | Open |
| [KI-007](#ki-007-robot-cannot-be-constructed) | `Robot` cannot be constructed | Game, assets | Open |
| [KI-008](#ki-008-unknown-shader-names-resolve-to-location-0) | Unknown shader names resolve to location 0 | Rendering | Open |
| [KI-009](#ki-009-instanced-rendering-skips-the-wrong-objects-when-some-are-hidden) | Instanced rendering skips the wrong objects when some are hidden | Rendering | Open |
| [KI-010](#ki-010-control-names-are-not-checked-for-uniqueness) | Control names are not checked for uniqueness | UI | Open |
| [KI-011](#ki-011-dispatcherpost-swallows-callback-exceptions) | `Dispatcher.Post` swallows callback exceptions | Threads | Open |
| [KI-012](#ki-012-startup-continues-after-a-failed-asset-load) | Startup continues after a failed asset load | Startup | Open |
| [KI-013](#ki-013-window-size-settings-are-ignored) | Window size settings are ignored | Settings | Open |
| [KI-014](#ki-014-mouse-look-ignores-imgui-focus) | Mouse look ignores ImGui focus | Input | Open |
| [KI-015](#ki-015-unhandled-exceptions-are-never-logged) | Unhandled exceptions are never logged | Logging | Open |
| [KI-016](#ki-016-polygon-tests-use-face-normals-only) | Polygon tests use face normals only | Physics | Open |
| [KI-017](#ki-017-gl-objects-are-deleted-from-finalizers) | GL objects are deleted from finalizers | Rendering | Open |
| [KI-018](#ki-018-imguicontrolwidth-is-pushed-and-never-popped) | `ImGuiControl.Width` is pushed and never popped | UI | Open |
| [KI-019](#ki-019-objectrenderer-draws-nothing-without-an-instanced-shader) | `ObjectRenderer` draws nothing without an instanced shader | Rendering | Open |

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
| **Current state** | The per-loader log files are never created. Whether the later sinks can open the already-open file was not checked |
| **Remaining check** | Run the game, then look for per-loader entries in `log.txt` and for Serilog self-log errors; use the parameter, or one shared logger with context |
| **Evidence** | `LoggerService.CreateLogger`, `GameManager`'s constructor |

## KI-005: Untextured objects use a 0-255 color in a 0-1 color space

| Field | Value |
|---|---|
| **Status** | Open |
| **Scope** | `CoolEngine.Services.Renderers.ObjectRenderer<T>`, objects without a texture (every spawned cube) |
| **Root cause** | The file-local constant `DefaultEmptyTextureColor_` is `(0xAA, 0x55, 0x6F, 1)`: 0-255 channel values where GL colors are 0-1. `DefaultShader` outputs `color` as is when `hasTexture` is false |
| **Current state** | The channels saturate, so untextured objects should draw white rather than the intended rose |
| **Remaining check** | Spawn an object and look at it; divide the channels by 255 |
| **Evidence** | `ObjectRenderer.cs`, `Assets/Shaders/DefaultShader/DefaultShader.frag` |

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
| **Current state** | A misspelt or optimised-away uniform is written to whatever sits at location 0; a program that fails to link is used silently |
| **Remaining check** | Return -1 for unknown names (GL ignores -1), log once per unknown name, and check `LinkStatus` in `Shader.Create` |
| **Evidence** | The three lookup methods and `Shader.Create` |

## KI-009: Instanced rendering skips the wrong objects when some are hidden

| Field | Value |
|---|---|
| **Status** | Open |
| **Scope** | `ObjectRenderer<T>.RenderInstanced` |
| **Root cause** | Per-instance data is written at the drawable's index in the group, skipping hidden ones and leaving gaps, but the upload and the draw use the first `count - hidden` records |
| **Current state** | With any drawable hidden (`Visible = false`) in an instanced group, the last visible ones are not drawn and stale records fill the gaps. Nothing hides objects today |
| **Remaining check** | Write records with a separate running index |
| **Evidence** | `RenderInstanced` |

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
| **Current state** | Started from the wrong folder, the window stays empty with no error shown ([BUILD §3](BUILD.md#3-run)) |
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
| **Status** | Open |
| **Scope** | `CoolEngine.Services.Renderers.ObjectRenderer<T>` |
| **Root cause** | `AddDrawableToInstancedGroup`, the only method that files a drawable into a texture group for either path, returns at once while `InstancedShader` is null. Setting `InstancedShader` later re-sorts only drawables already filed, so the skipped ones stay out |
| **Current state** | Latent: the game sets `InstancedShader` before `DrawableItems`. A renderer without an instanced shader, or one fed drawables first, draws nothing, not even through the per-instance path |
| **Remaining check** | File drawables into the per-instance groups regardless of `InstancedShader`, and move them only when an instanced shader exists |
| **Evidence** | `ObjectRenderer<T>.AddDrawable`, `AddDrawableToInstancedGroup`, `SortInstancedAndPerInstanceObjects`; found by the documentation review on 2026-11-24 |
