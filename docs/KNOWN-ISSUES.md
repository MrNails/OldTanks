# Known Issues

| Field        | Value  |
|--------------|--------|
| Status       | Living - known-issues register |
| Last updated | 2026-11-27 |
| Verified against | 2026-11-27; bug/OT-18 rebased on master at 65b186e (KI-034 closed against the code, `CollisionRenderGroupTests` and a headless fake-OpenGL probe, and the mentor's app check of key `0` on the branch and on master 5e04730; the other entries not rechecked); bug/OT-17 rebased on master at 88f6d1c (KI-033 closed against the code, the 11 new `CoolEngine.Tests.Services.Renderers.ObjectRendererOldTests` and the mentor's app checks of 2026-11-27: on `master` at 5e04730 the overlay logged 1 791 `GL_INVALID_VALUE` lines in 10 s, on the branch none; the other entries not rechecked); bug/OT-16 rebased on master at 4b425be (KI-032's closure and KI-036 against the code and `WindowMetricsTests` and `ImGuiDisplayMetricsTests`, and the mentor's app check of 2026-11-27 against `master` at 5e04730: windowed 800 by 600, fullscreen, a resize to 1200 by 700, a zero-height client, minimise and restore, `Alt` + `Enter` and `FullScreen` true at start, no error, exception or GL error in the log; a display scale other than 100% and the mouse-look edge zones not run; KI-036 reasoned from the code and GLFW's documentation); bug/OT-15 rebased on master at 477b26b (KI-017 closed against the four finalizers, `GLDeletionQueue`, `OldTanks.Windows.MainWindow` and the 18 new tests; the crash before the fix observed by the analyst's harness on NVIDIA 621.24, the fix by the reviewer's GL harness and the mentor's app check of 2026-11-27; KI-035 reasoned from the code); bug/OT-14 at da7db97 (KI-002 closed against the code and `tests/OldTanks.Tests`: movement and follow camera measured within 0.00004° of the drawn forward axis by the review, `DrawNormals` equal to the transform within 2.4e-7, a regression run of the game on 2026-11-27 with exit 0 and no Error or Warning line; the visible checks in the game are open for Denys; KI-001's `A`/`D` note read from code); 2026-11-26; feature/OT-5 at 98ab147 (KI-002's note on the move; KI-032 to KI-034 reasoned from the code and the mentor's app checks and review D's triage of 2026-11-26, the debug-view numbers not re-measured here); 2026-11-25; merge-state wording brought up to date after PRs #11 to #17 (OT-13); bug/OT-13 rebased on master at 8ded8f4 (KI-016 and KI-022 closed there; KI-022's fix confirmed by a 12 s run of the game on 2026-11-25 with no `InitCollision error` line on the console or in `log.txt`, KI-016's by tests; KI-031 read from code); bug/OT-12 rebased on master at 734d8f6 (KI-006 closed against the code and `SceneTests`, KI-030 read from code); bug/OT-10 rebased on master at 483d63f (KI-010 and KI-018 closed against the code and two runs of the game, KI-026 to KI-029 read from code); bug/OT-9 on master at d749ec8 (KI-012 to KI-014 closed and observed in the game on 2026-11-25); bug/OT-8 rebased on master at 826e560 (KI-004, KI-011 and KI-015 closed there) and bug/OT-6 rebased on master at 6753e46 (KI-008 and KI-020 closed there and checked in two runs); KI-021 and KI-022 (before its fix) observed in the running game on 2026-11-25 (KI-008 and KI-020 too, before their fix), KI-005's fix confirmed by a run and KI-009's and KI-019's by tests, KI-023 to KI-025 read from code, the rest as of 2026-11-24 (reasoned from code, not observed) |

---

Defects that are understood and not yet fixed, with their root cause and the check that would close them. Rules for entries: [DOCUMENTATION-GUIDE §7](DOCUMENTATION-GUIDE.md#7-known-issue-entries). Unwired features and debt that is not a defect are in [BACKLOG](BACKLOG.md).

KI-001 to KI-019 were found by reading `develop` at `d32cbbf` on 2026-11-24 for the documentation start (OT-1); the machine had no .NET 7 runtime then. The game runs since OT-3 (.NET 10, 2026-11-25, [BUILD §3](BUILD.md#3-run)), and one smoke run confirmed KI-004 (closed on `bug/OT-8`, 2026-11-25) and KI-008 and found KI-020 and KI-022 (KI-008 and KI-020 are closed on `bug/OT-6`, KI-022 on `bug/OT-13`, 2026-11-25); the other entries of KI-001 to KI-019 are still reasoned from code, not observed. KI-020 to KI-023 come from OT-3 (KI-021 from its review, KI-023 from its decoder work); KI-024 and KI-025 come from OT-7 (KI-024 from its planning, KI-025 from its review). KI-026 to KI-029 come from reading the UI controls for OT-10 (2026-11-25, reasoned from code, not observed), KI-030 from the OT-12 review (2026-11-25, reasoned from code, not observed); KI-024 to KI-031 are numbered in the merge order of the parallel bug branches and are final. KI-005, KI-009 and KI-019 were fixed in OT-7 (`bug/OT-7`), KI-004, KI-011 and KI-015 in OT-8 (`bug/OT-8`) and KI-010 and KI-018 in OT-10 (`bug/OT-10`) and KI-016 and KI-022 in OT-13 (`bug/OT-13`) and KI-002 in OT-14 (`bug/OT-14`, merged as PR #21) and KI-017 in OT-15 (`bug/OT-15`, merged as PR #22) and KI-032 in OT-16 (`bug/OT-16`, merged as PR #23) and KI-033 in OT-17 (`bug/OT-17`, merged as PR #24) and KI-034 in OT-18 (`bug/OT-18`, 2026-11-27); they are closed stubs. KI-035 comes from the OT-15 plan (2026-11-27, reasoned from code, not observed). KI-031 comes from the OT-13 review (2026-11-25, reasoned from code, not observed). KI-036 comes from OT-16 (2026-11-27, reasoned from code, not observed). The remaining check of each entry says what would confirm it.

## Index

| KI | Title | Area | Status |
|----|-------|------|--------|
| [KI-001](#ki-001-the-player-is-never-assigned) | The player is never assigned | Game | Open |
| [KI-002](#ki-002-worldobjectrotation-is-read-as-radians-in-one-place-and-degrees-in-another) | `WorldObject.Rotation` is read as radians in one place and degrees in another | Game, physics | Closed |
| [KI-003](#ki-003-worldobjects-is-changed-across-threads-without-a-shared-lock) | `WorldObjects` is changed across threads without a shared lock | Threads | Open |
| [KI-004](#ki-004-loggerservice-ignores-the-log-file-name) | `LoggerService` ignores the log file name | Logging | Closed |
| [KI-005](#ki-005-untextured-objects-use-a-0-255-color-in-a-0-1-color-space) | Untextured objects use a 0-255 color in a 0-1 color space | Rendering | Closed |
| [KI-006](#ki-006-scenecenter-is-computed-from-a-wrong-maximum) | `Scene.Center` is computed from a wrong maximum | Engine | Closed |
| [KI-007](#ki-007-robot-cannot-be-constructed) | `Robot` cannot be constructed | Game, assets | Open |
| [KI-008](#ki-008-unknown-shader-names-resolve-to-location-0) | Unknown shader names resolve to location 0 | Rendering | Closed |
| [KI-009](#ki-009-instanced-rendering-skips-the-wrong-objects-when-some-are-hidden) | Instanced rendering skips the wrong objects when some are hidden | Rendering | Closed |
| [KI-010](#ki-010-control-names-are-not-checked-for-uniqueness) | Control names are not checked for uniqueness | UI | Closed |
| [KI-011](#ki-011-dispatcherpost-swallows-callback-exceptions) | `Dispatcher.Post` swallows callback exceptions | Threads | Closed |
| [KI-012](#ki-012-startup-continues-after-a-failed-asset-load) | Startup continues after a failed asset load | Startup | Closed |
| [KI-013](#ki-013-window-size-settings-are-ignored) | Window size settings are ignored | Settings | Closed |
| [KI-014](#ki-014-mouse-look-ignores-imgui-focus) | Mouse look ignores ImGui focus | Input | Closed |
| [KI-015](#ki-015-unhandled-exceptions-are-never-logged) | Unhandled exceptions are never logged | Logging | Closed |
| [KI-016](#ki-016-polygon-tests-use-face-normals-only) | Polygon tests use face normals only | Physics | Closed |
| [KI-017](#ki-017-gl-objects-are-deleted-from-finalizers) | GL objects are deleted from finalizers | Rendering | Closed |
| [KI-018](#ki-018-imguicontrolwidth-is-pushed-and-never-popped) | `ImGuiControl.Width` is pushed and never popped | UI | Closed |
| [KI-019](#ki-019-objectrenderer-draws-nothing-without-an-instanced-shader) | `ObjectRenderer` draws nothing without an instanced shader | Rendering | Closed |
| [KI-020](#ki-020-three-shaders-fail-to-compile-and-are-used-anyway) | Three shaders fail to compile and are used anyway | Rendering | Closed |
| [KI-021](#ki-021-generated-binding-code-calls-equals-before-its-null-check) | Generated binding code calls Equals before its null check | Generator | Closed |
| [KI-022](#ki-022-initcollision-prints-an-error-twice-at-startup) | InitCollision prints an error twice at startup | Physics | Closed |
| [KI-023](#ki-023-png-decoding-cannot-see-a-zlib-streams-missing-end) | PNG decoding cannot see a zlib stream's missing end | Assets | Open |
| [KI-024](#ki-024-objectrenderert-drops-a-drawable-whose-texture-is-new-to-a-fully-instanced-scene) | `ObjectRenderer<T>` drops a drawable whose texture is new to a fully instanced scene | Rendering | Open |
| [KI-025](#ki-025-objectrenderert-can-keep-one-scene-texture-on-both-paths) | `ObjectRenderer<T>` can keep one scene texture on both paths | Rendering | Open |
| [KI-026](#ki-026-imguitreenode-skips-treepop-when-it-is-expanded-but-hidden) | `ImGuiTreeNode` skips `TreePop` when it is expanded but hidden | UI | Open |
| [KI-027](#ki-027-imguitextbox-flag-setters-test-the-password-flag-and-clear-every-other-flag) | `ImGuiTextBox` flag setters test the Password flag and clear every other flag | UI | Open |
| [KI-028](#ki-028-imguilistboxtselectedindex-accepts-itemscount) | `ImGuiListBox<T>.SelectedIndex` accepts `Items.Count` | UI | Open |
| [KI-029](#ki-029-a-control-renamed-after-construction-bypasses-the-name-check) | A control renamed after construction bypasses the name check | UI | Open |
| [KI-030](#ki-030-a-malformed-last-v-vt-or-vn-line-drops-the-last-mesh-of-an-obj-file) | A malformed last v, vt or vn line drops the last mesh of an OBJ file | Assets | Open |
| [KI-031](#ki-031-the-collision-debug-view-transforms-objects-on-the-gl-thread-while-the-physics-thread-does) | The collision debug view transforms objects on the GL thread while the physics thread does | Threads, physics | Open |
| [KI-032](#ki-032-the-viewport-projections-imgui-and-render-target-use-the-windows-outer-size) | The viewport, projections, ImGui and render target use the window's outer size | Rendering, UI | Closed |
| [KI-033](#ki-033-the-normals-overlay-overflows-the-primitives-buffer) | The normals overlay overflows the primitives buffer | Rendering | Closed |
| [KI-034](#ki-034-the-sphere-collision-render-group-has-no-vertices-and-re-creates-its-buffers-every-frame) | The sphere collision render group has no vertices and re-creates its buffers every frame | Rendering | Closed |
| [KI-035](#ki-035-objectrenderert-drops-per-instance-draw-infos-without-disposing-them) | `ObjectRenderer<T>` drops per-instance draw infos without disposing them | Rendering | Open |
| [KI-036](#ki-036-the-physics-thread-reads-the-windows-client-size-through-glfw) | The physics thread reads the window's client size through GLFW | Threads, input | Open |

---

## KI-001: The player is never assigned

| Field | Value |
|---|---|
| **Status** | Open |
| **Scope** | Player mode: key `1`, `MainWindow.HandleObjectMove`, `HandleKeyboardInputs`, `ChangeCameraMode` |
| **Root cause** | Nothing sets `OldTanks.Models.World.Player`. Key `1` computes the free-camera flag as "no player, or toggle", so it stays in free-camera mode, and the player branches never run |
| **Current state** | Only the free camera can be driven; the player movement, jump and follow camera are dead code. The player's `A`/`D` add 1 per physics loop to `Pitch`, which is 1 radian (about 57°) since [KI-002](#ki-002-worldobjectrotation-is-read-as-radians-in-one-place-and-degrees-in-another) fixed the unit; unreachable while no player is set, so left for the player task (Denys, 2026-11-27, OT-14 Q2: "All recommended"; turned down: 1° per loop; a turn rate × time from his spec) |
| **Remaining check** | Assign a `WorldObject` (for example Dynamic Cube 1) to `World.Player` in `InitDefaultObjects`, press `1`, drive it, fix the `A`/`D` turn step (above), and fix what the first run shows |
| **Evidence** | `World.Player` has a setter and no caller |

## KI-002: `WorldObject.Rotation` is read as radians in one place and degrees in another

| Field | Value |
|---|---|
| **Status** | Closed: fixed on `bug/OT-14` (2026-11-27): `Rotation` is radians everywhere; `OldTanks.Models.WorldObject.Move`, `OldTanks.Models.WorldObject.SetCameraData` and `OldTanks.Windows.MainWindow.DrawNormals` read it as radians ([PHYSICS-AND-COLLISIONS §2](CoolEngine/PhysicEngine/PHYSICS-AND-COLLISIONS.md#2-the-rigid-body)); the player's `A`/`D` step is left to [KI-001](#ki-001-the-player-is-never-assigned) |

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
| **Status** | Closed: fixed on `bug/OT-8` (2026-11-25): one root logger and one `log.txt`, every line tagged `[source]` ([BUILD §4](BUILD.md#4-files-the-game-reads-and-writes), [CODE-STYLE](CODE-STYLE.md#logging)) |

## KI-005: Untextured objects use a 0-255 color in a 0-1 color space

| Field | Value |
|---|---|
| **Status** | Closed: fixed in OT-7 (`bug/OT-7`, 2026-11-25). The untextured colour in `CoolEngine.Services.Renderers.ObjectRenderer<T>` is rose #AA556F in 0-1 channels (each byte divided by 255), on the per-instance and the instanced path (Denys chose "Rose #AA556F (Recommended)"). Confirmed in the running game: a spawned cube drew pure white before the fix and rose after it, on both paths ([BUILD §3](BUILD.md#3-run)); also asserted by `CoolEngine.Tests` |
| **Scope** | `CoolEngine.Services.Renderers.ObjectRenderer<T>`, objects without a texture (every spawned cube) |

## KI-006: `Scene.Center` is computed from a wrong maximum

| Field | Value |
|---|---|
| **Status** | Closed: fixed on `bug/OT-12` (2026-11-25); `Scene.Center` is now the midpoint of the model-space axis-aligned bounds of every vertex of every mesh, `(0, 0, 0)` for a scene with no vertices (today's result, not a decided rule); covered by `CoolEngine.Tests.GraphicalEngine.Core.SceneTests` |

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
| **Status** | Closed on `bug/OT-6` (2026-11-25, the commit "OT-6 Check shader link status and return -1 for unknown names"): the lookups return -1 and warn once, and `Shader.Create` checks compile and link status and throws `ShaderException` on a link failure ([RENDERING §3](CoolEngine/Services/Renderers/RENDERING.md#3-gpu-side-types)) |

## KI-009: Instanced rendering skips the wrong objects when some are hidden

| Field | Value |
|---|---|
| **Status** | Closed: fixed in OT-7 (`bug/OT-7`, 2026-11-25). `ObjectRenderer<T>.FillInstanceRecords` packs one record per visible drawable from index 0 and returns the count, which `RenderInstanced` uploads and draws; the same change makes `RenderInstanced` return its `ArrayPool` rental on its normal paths (the all-hidden return and after the draw; on an exception the frame is torn and the rental lost, by Denys's rule in [CODE-STYLE](CODE-STYLE.md#patterns)), which before was skipped when all drawables were hidden and on every frame for an instanced group emptied by deletes (spawn 17 or more cubes, delete them all). Confirmed by `CoolEngine.Tests` only: no object can be hidden in the game, nothing sets `Visible = false` |
| **Scope** | `ObjectRenderer<T>.RenderInstanced` |

## KI-010: Control names are not checked for uniqueness

| Field | Value |
|---|---|
| **Status** | Closed - fixed on `bug/OT-10` (2026-11-25): `ControlHandler` keys controls by name and a duplicate throws; the HUD's second `TB_FPS` is now `TB_CollidingPS`. Rules: [UI-CONTROLS §1](OldTanks.UI/UI-CONTROLS.md#1-controlhandler) |

## KI-011: `Dispatcher.Post` swallows callback exceptions

| Field | Value |
|---|---|
| **Status** | Closed: fixed on `bug/OT-8` (2026-11-25): a throwing posted callback is logged at `Error` and the queue goes on ([ARCHITECTURE §3](ARCHITECTURE.md#3-getting-work-onto-the-gl-thread)) |

## KI-012: Startup continues after a failed asset load

| Field | Value |
|---|---|
| **Status** | Closed on `bug/OT-9` (2026-11-25): a failed asset load now shows a "Startup failed" window and is logged ([BUILD §3](BUILD.md#3-run), [ARCHITECTURE §4](ARCHITECTURE.md#4-startup-and-the-frame)); a texture or skybox file that fails to decode is logged and skipped ([ASSET-PIPELINE](CoolEngine/Services/Loaders/ASSET-PIPELINE.md)); confirmed in the running game on 2026-11-25 ([BUILD §3](BUILD.md#3-run)). |

## KI-013: Window size settings are ignored

| Field | Value |
|---|---|
| **Status** | Closed on `bug/OT-9` (2026-11-25): `Settings.Width` and `Height` size the window, a value below 1 falls back to 800 × 600 with a warning, the constructor parameters are width-first and `EngineSettings` is registered ([BUILD §4](BUILD.md#4-files-the-game-reads-and-writes)); confirmed in the running game on 2026-11-25. |

## KI-014: Mouse look ignores ImGui focus

| Field | Value |
|---|---|
| **Status** | Closed on `bug/OT-9` (2026-11-25): mouse look, wheel zoom and game keys (including the physics thread's camera movement) are skipped while ImGui wants the input ([FEATURES-OVERVIEW §3](OldTanks/FEATURES-OVERVIEW.md#3-controls)); confirmed in the running game on 2026-11-25. |

## KI-015: Unhandled exceptions are never logged

| Field | Value |
|---|---|
| **Status** | Closed: fixed on `bug/OT-8` (2026-11-25): `Program.Main` subscribes the unhandled and the unobserved-task handlers and flushes the log on exit ([ARCHITECTURE §4](ARCHITECTURE.md#4-startup-and-the-frame)) |

## KI-016: Polygon tests use face normals only

| Field | Value |
|---|---|
| **Status** | Closed: fixed on `bug/OT-13` (2026-11-25): edge × edge axes for polygon × polygon, an exact closest-point test for cuboid × sphere and a vertex and edge axis fallback for other shapes ([PHYSICS-AND-COLLISIONS §4](CoolEngine/PhysicEngine/PHYSICS-AND-COLLISIONS.md#4-detection)) |

## KI-017: GL objects are deleted from finalizers

| Field | Value |
|---|---|
| **Status** | Closed: fixed on `bug/OT-15` (2026-11-27): the finalizers of `Texture`, `Shader`, `Buffer<T>` and `DrawObjectInfo` make no GL call; each logs one Warning and, unless the object was disposed, puts its GL names on `CoolEngine.GraphicalEngine.Services.GLDeletionQueue`, which `OldTanks.Windows.MainWindow` drains on the GL thread every update frame and in `OnUnload` ([RENDERING §3](CoolEngine/Services/Renderers/RENDERING.md#3-gpu-side-types)). It was a crash, not the silent leak this entry first described: each of the four finalizers ended the process (an access violation, `0xC0000005`, on NVIDIA 621.24 in the analyst's harness; an `InvalidOperationException` on the finalizer thread in a headless test host), and the crash was latent because no path of today's game drops a GL object undisposed (reasoned from code). Where `ObjectRenderer<T>` does drop some: [KI-035](#ki-035-objectrenderert-drops-per-instance-draw-infos-without-disposing-them) |

## KI-018: `ImGuiControl.Width` is pushed and never popped

| Field | Value |
|---|---|
| **Status** | Closed - fixed on `bug/OT-10` (2026-11-25): `Draw` calls `ImGui.SetNextItemWidth`, so `Width` sizes only a leaf control's own widget; containers ignore it. Rules: [UI-CONTROLS §3](OldTanks.UI/UI-CONTROLS.md#3-imgui-controls-oldtanksuiimguicontrols) |

## KI-019: `ObjectRenderer` draws nothing without an instanced shader

| Field | Value |
|---|---|
| **Status** | Closed: fixed in OT-7 (`bug/OT-7`, 2026-11-25). `ObjectRenderer<T>.AddDrawableToInstancedGroup` no longer returns while `InstancedShader` is null: drawables are always filed into the per-instance groups, and a group moves to the instanced set only once `InstancedShader` is set, before or after `DrawableItems` ([RENDERING §2](CoolEngine/Services/Renderers/RENDERING.md#2-objectrenderert-batching-and-instancing)). Confirmed by `CoolEngine.Tests` only: the game sets `InstancedShader` first, so a run could not show it |
| **Scope** | `CoolEngine.Services.Renderers.ObjectRenderer<T>` |

## KI-020: Three shaders fail to compile and are used anyway

| Field | Value |
|---|---|
| **Status** | Closed on `bug/OT-6` (2026-11-25, the commit "OT-6 Rename shader names that hide GLSL texture()"): the font varying is `textureCoord` and the skybox sampler `texture0`; skybox and HUD text confirmed in the running game ([RENDERING §4](CoolEngine/Services/Renderers/RENDERING.md#4-shader-contract)) |

## KI-021: Generated binding code calls Equals before its null check

| Field | Value |
|---|---|
| **Status** | Closed on `bug/OT-11` (2026-11-25): `OldTanks.UI.SourceGenerators.Generators.BindingGenerator.CreateMembersBinding` now emits the `tmpObject is null` test before `tmpObject.Equals(...)`, so a sender of another type returns instead of throwing; the generated file changed in the three guard lines only and the 3 × CS8602 are gone (139 → 136 warnings, [BINDING-GENERATOR §3](OldTanks.UI.SourceGenerators/BINDING-GENERATOR.md#3-what-it-generates)) |

## KI-022: InitCollision prints an error twice at startup

| Field | Value |
|---|---|
| **Status** | Closed: fixed on `bug/OT-13` (2026-11-25): a sphere copies nothing silently, a polygon without vertices throws `ArgumentException` from the `Collision` constructor ([PHYSICS-AND-COLLISIONS §5](CoolEngine/PhysicEngine/PHYSICS-AND-COLLISIONS.md#5-shapes)) |

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

## KI-026: `ImGuiTreeNode` skips `TreePop` when it is expanded but hidden

| Field | Value |
|---|---|
| **Status** | Open |
| **Scope** | `OldTanks.UI.ImGuiControls.ImGuiTreeNode.Draw` |
| **Root cause** | `ImGui.TreeNodeEx` runs before the visibility check; when it returns true and `IsVisible` is false, `Draw` returns without `ImGui.TreePop`. A hidden node also still draws its header |
| **Current state** | Latent (reasoned from code, not observed): nothing hides a tree node (the debug window's "Rigid body" node is always visible). Hiding an open node would leave ImGui's ID and tree stacks unbalanced for the rest of the window |
| **Remaining check** | Check `IsVisible` before `TreeNodeEx`, and pop whenever `TreeNodeEx` returned true |
| **Evidence** | `ImGuiTreeNode.Draw` |

## KI-027: `ImGuiTextBox` flag setters test the Password flag and clear every other flag

| Field | Value |
|---|---|
| **Status** | Open |
| **Scope** | `OldTanks.UI.ImGuiControls.ImGuiTextBox.SetFlagAndNotifyIfChanged`, behind `IsPassword`, `IsReadOnly`, `DisableUndoRedo`, `UseUpperCase`, `AllowNewLine`, `AllowTab` and `ClearAllOnEscape`; the unused `SetFlagsFromCharsDisplayForm` |
| **Root cause** | The "already set" test reads `ImGuiInputTextFlags.Password` instead of the flag passed in, and clearing does `m_flags &= flag` (keeps only that flag) instead of `&= ~flag`; `CharsDisplayForm` never reaches the flags |
| **Current state** | Latent (reasoned from code, not observed): no code sets these properties (the texture window's "Asset path" box uses the defaults) |
| **Remaining check** | Test the passed flag, clear with `&= ~flag`, and wire or drop `CharsDisplayForm` |
| **Evidence** | The method and the seven properties |

## KI-028: `ImGuiListBox<T>.SelectedIndex` accepts `Items.Count`

| Field | Value |
|---|---|
| **Status** | Open |
| **Scope** | `OldTanks.UI.ImGuiControls.ImGuiListBox<T>.SelectedIndex` |
| **Root cause** | The range check rejects `value > Count` where `>= Count` is meant |
| **Current state** | Latent (reasoned from code, not observed): setting `Count` selects nothing (`SelectedItem` returns default) and raises `SelectionChanged` with index `Count` |
| **Remaining check** | Reject `Count` |
| **Evidence** | The setter |

## KI-029: A control renamed after construction bypasses the name check

| Field | Value |
|---|---|
| **Status** | Open |
| **Scope** | `OldTanks.UI.Services.Interfaces.IControl.Name` (public setter), `OldTanks.UI.Services.ControlHandler` |
| **Root cause** | The handler registers the name once, from the constructor; a later rename keeps the old dictionary key, so the new name is unchecked and the old one stays reserved |
| **Current state** | Latent (reasoned from code, not observed): nothing renames a control |
| **Remaining check** | Make the name init-only, or re-key in the handler on rename |
| **Evidence** | `IControl`, `ControlHandler.RegisterControl` |

## KI-030: A malformed last v, vt or vn line drops the last mesh of an OBJ file

| Field | Value |
|---|---|
| **Status** | Open |
| **Scope** | `CoolEngine.Services.Loaders.ModelsLoaders.WaveFrontLoader.LoadAsset` |
| **Root cause** | The loader adds the file's last mesh only when `parseError` is false, but `parseError` holds only the result of the last `v`, `vt` or `vn` line parsed (a failed `f` line returns before caching) |
| **Current state** | When the last data line of the file is a `v`, `vt` or `vn` line whose number does not parse, the last mesh is dropped; a one-object file then caches a `Scene` with no meshes. Latent: the shipped assets parse. Reasoned from code, not observed. Whether to keep the mesh, drop the model or throw is Denys's decision |
| **Remaining check** | Write an OBJ whose last line is a malformed `v` line, load it and look at the cached scene's meshes |
| **Evidence** | `WaveFrontLoader.LoadAsset`; found by the OT-12 review on 2026-11-25 |

## KI-031: The collision debug view transforms objects on the GL thread while the physics thread does

| Field | Value |
|---|---|
| **Status** | Open |
| **Scope** | `CoolEngine.Services.Renderers.CollisionRenderer`, `CoolEngine.Services.Misc.CollisionRenderGroup.UpdateCollisionablesData`, `OldTanks.Models.WorldObject.ApplyTransformation`, `CoolEngine.PhysicEngine.Core.Collision.Collision.UpdateCollision` |
| **Root cause** | While the collision debug view (key `0`) is on, `CollisionRenderGroup.UpdateCollisionablesData` calls `WorldObject.ApplyTransformation` on the GL thread. It rewrites `m_transform`, `m_haveTransformation` and the collision's current vertices and normals, which the physics thread (`World.CollideObjects`) reads and writes at the same time, without a lock. Pre-existing, not caused by OT-13; found by the OT-13 review |
| **Current state** | Reasoned from code, not observed. The physics thread can read a half-updated transform or collision (for example new vertices with old normals) for one pair test, and both threads can write the same arrays |
| **Remaining check** | Let only the physics thread apply transformations and have the renderer read a snapshot, or guard both with one lock; then watch the debug view with many moving objects |
| **Evidence** | The call chain `CollisionRenderer` → `CollisionRenderGroup.UpdateCollisionablesData` → `WorldObject.ApplyTransformation` → `Collision.UpdateCollision`, against `World.CollideObjects` (OT-13 review, 2026-11-25) |

## KI-032: The viewport, projections, ImGui and render target use the window's outer size

| Field | Value |
|---|---|
| **Status** | Closed: fixed on `bug/OT-16` (2026-11-27): the viewport, the `CoolEngine.GraphicalEngine.Core.RenderTarget` and the perspective aspect follow `FramebufferSize`; `EngineSettings` window size, the HUD's screen projection and ImGui follow `ClientSize`, through the pure `OldTanks.Windows.WindowMetrics` and `OldTanks.UI.Services.ImGUI.ImGuiDisplayMetrics`, and the 39-pixel ImGui offset is gone ([RENDERING §5](CoolEngine/Services/Renderers/RENDERING.md#5-the-render-target-and-reversed-z), [UI-CONTROLS §5](OldTanks.UI/UI-CONTROLS.md#5-the-imgui-renderer)). The same task fixed a crash it found, with no KI number: with `FullScreen` true in the settings the game ended at start with a `NullReferenceException`, because the constructor's fullscreen switch raises the resize callbacks before `OnLoad` creates the render target; `OldTanks.Windows.MainWindow.ApplyWindowSize` now returns while the target is null. Checked in the running game on 2026-11-27 (windowed, fullscreen, resize, minimise, `FullScreen` true at start); not checked at a display scale other than 100% |

## KI-033: The normals overlay overflows the primitives buffer

| Field | Value |
|---|---|
| **Status** | Closed: fixed on `bug/OT-17` (2026-11-27): `CoolEngine.Services.Renderers.ObjectRendererOld.DrawPrimitives` keeps one buffer per primitive type, grown to the next power of two of the draw (128 to 1 048 576 vertices) and never shrunk; a larger draw is cut with one Warning per type ([RENDERING §1](CoolEngine/Services/Renderers/RENDERING.md#1-renderers)) |

## KI-034: The sphere collision render group has no vertices and re-creates its buffers every frame

| Field | Value |
|---|---|
| **Status** | Closed: fixed on `bug/OT-18` (2026-11-27): `CoolEngine.Services.Misc.CollisionRenderGroup` keeps a private `m_isInitialized` flag and creates its GL objects only when the first member's shape has vertices, so the sphere group creates and deletes nothing per frame; spheres stay undrawn in the debug view (Denys's answer, 2026-11-27; drawing them is in [BACKLOG](BACKLOG.md#game-and-engine-features-not-built)) ([PHYSICS-AND-COLLISIONS §6](CoolEngine/PhysicEngine/PHYSICS-AND-COLLISIONS.md#6-debug-views)) |

## KI-035: `ObjectRenderer<T>` drops per-instance draw infos without disposing them

| Field | Value |
|---|---|
| **Status** | Open |
| **Scope** | `CoolEngine.Services.Renderers.ObjectRenderer<T>`: `SortInstancedAndPerInstanceObjects` and `AddDrawableToInstancedGroup` |
| **Root cause** | Both methods drop a per-instance `DrawSceneInfo` together with its `MeshDrawInfos` (each holds `CoolEngine.GraphicalEngine.Core.DrawObjectInfo` GL objects) without disposing them |
| **Current state** | Unreachable in today's game (reasoned from code; the mentor's app check of 2026-11-27 logged no "never disposed" line). Since OT-15 a dropped `DrawObjectInfo` is no longer a crash: its finalizer logs a Warning and `CoolEngine.GraphicalEngine.Services.GLDeletionQueue` frees its names on the GL thread ([KI-017](#ki-017-gl-objects-are-deleted-from-finalizers), closed). Left out of OT-15 on purpose (Denys, 2026-11-27: a new KI, turned down: fix it there through the queue, a separate bug task) |
| **Remaining check** | Release the draw infos through `GLDeletionQueue` at both drop sites, not by `Dispose`: both can run on the startup thread-pool thread, which has no GL context; then check that dropping a per-instance group logs no "never disposed" Warning |
| **Evidence** | The two methods (OT-15 plan and review, 2026-11-27) |

## KI-036: The physics thread reads the window's client size through GLFW

| Field | Value |
|---|---|
| **Status** | Open |
| **Scope** | `OldTanks.Windows.MainWindow.HandleMouseMove` (the mouse-look edge zones, run by the physics thread) |
| **Root cause** | It reads `ClientSize` once per physics step, which is one `glfwGetWindowSize` call, and GLFW documents that function as main-thread only. The size is not cached anywhere that the physics thread could read instead |
| **Current state** | Safe in practice on Win32, where GLFW answers with `GetClientRect` (reasoned from the code and GLFW's documentation; no fault seen). Denys chose it (2026-11-27, OT-16 question 4: "All recommended"; turned down: a snapshot written on the GL thread, a high-tier change) |
| **Remaining check** | Write the client size to a field on the GL thread in `MainWindow.ApplyWindowSize` and read that field in `HandleMouseMove`; then check mouse look at the four window edges |
| **Evidence** | `MainWindow.HandleMouseMove` (OT-16, 2026-11-27) |
