# Rendering

| Field        | Value  |
|--------------|--------|
| Status       | Living |
| Last updated | 2026-11-27 |
| Verified against | 2026-11-27; bug/OT-17 rebased on master at 88f6d1c (§1's primitives-buffers paragraph against `ObjectRendererOld` and the checks it names; the other sections not rechecked); bug/OT-16 rebased on master at 4b425be (§5's window-size paragraphs against the code and `WindowMetricsTests`; the mentor's app check of 2026-11-27: windowed, fullscreen, resize, zero-height client, minimise and restore, `Alt` + `Enter`, `FullScreen` true at start, no GL error; a display scale other than 100% not run; the other sections not rechecked); bug/OT-15 at c208744 (§3's Disposal paragraph and §5's `RenderTarget` paragraph against the code, the 18 tests, the reviewer's GL harness and the mentor's app check of 2026-11-27; the other sections' claims not rechecked); 2026-11-26; feature/OT-5 at 98ab147 (§5, §3's `RenderTarget` row, §4's `SkyBoxShader` row and the skybox upload fix against the code, review D's GL harness on the real `RenderTarget` and shaders, and the app checks of 2026-11-26; the other sections' claims not rechecked); 2026-11-25; bug/OT-7 (committed, rebased on master at d2f8b8a); §2 against the code, the new tests and two runs of the game (the colour and the retexture); bug/OT-6 (rebased on master at 6753e46): §3's Shader row, the name-lookup paragraph (its fail-fast consequences against OT-8's exception logging) and §4's sampler note against the code and the two OT-6 runs; the other sections as of 2026-11-24 (develop at d32cbbf) |

---

How the engine draws a frame: the four renderers in `CoolEngine/Services/Renderers/`, the GPU-side types they use, and the contract each shader in `OldTanks/Assets/Shaders/` must meet. Frame order and threads: [ARCHITECTURE §4](../../../ARCHITECTURE.md#4-startup-and-the-frame). Math types (since OT-5): `CoolEngine.Mathematics` matrices and `System.Numerics` vectors, uploaded to uniforms as floats ([MATHEMATICS §5](../../../CoolEngine.Mathematics/MATHEMATICS.md#5-the-opengl-memory-layout)).

## 1. Renderers

| Renderer | Kind | Draws | Shader (cache name) |
|----------|------|-------|---------------------|
| `CoolEngine.Services.Renderers.ObjectRenderer<T>` | Instance, `IRenderer<T>` | The world's drawables, batched per mesh and texture | `DefaultShader`; `DefaultShader2` for instanced batches |
| `CoolEngine.Services.Renderers.ObjectRendererOld` | Static, legacy | The skybox (`DrawSkyBox`) and debug lines and points (`DrawPrimitives`) | `SkyBoxShader`; the caller's shader for primitives (`PrimitivesShader`) |
| `CoolEngine.Services.Renderers.CollisionRenderer` | Static | Debug view: collision edges, vertex labels, face normals | `CollisionShader` |
| `CoolEngine.Services.Renderers.TextRenderer` | Static | Screen text (`DrawText2D`, the HUD) and world text (`DrawText3D`, object names and vertex labels, optionally billboarded) | `FontShader` |

`CoolEngine.Services.Renderers.SkyBoxRenderer` is an empty placeholder. `ObjectRendererOld` still keeps a per-CLR-type list of drawables (`AddDrawable`, `RemoveDrawable`), and the game keeps it in sync, but the call that drew them is commented out; the world is drawn only by `ObjectRenderer<T>`.

**Primitives buffers (`ObjectRendererOld.DrawPrimitives`, since OT-17, 2026-11-27):** one VAO and VBO per `PrimitiveType` (lines and points are separate), created on the first draw of that type, each with its own capacity in its `DrawObjectInfo.VerticesLength`. The pure `ObjectRendererOld.GetPrimitivesUpload` decides the capacity ([KI-033](../../../KNOWN-ISSUES.md#ki-033-the-normals-overlay-overflows-the-primitives-buffer), closed):

| Rule | Value |
|------|-------|
| Capacity | The next power of two at or above the draw, at least 128 (`MinPrimitivesCapacity`), at most 1 048 576 (`MaxPrimitivesCapacity`, 12 MiB of `Vector3`); it never shrinks |
| Growth | The same VBO is re-specified (`glBufferData`) and the VAO keeps pointing at that buffer object, so the VAO is created once per type |
| A draw above the cap | Cut to its first 1 048 576 vertices, with one Warning per type (Denys, 2026-11-27) |
| `drawAmount` larger than the array | `ArgumentOutOfRangeException` before any GL call |
| Log lines (Information) | "Primitives buffer for {PrimitiveType} created for {Capacity} vertices." and "Primitives buffer for {PrimitiveType} grown from {OldCapacity} to {Capacity} vertices." |

Checked 2026-11-27: the 11 tests in `CoolEngine.Tests.Services.Renderers.ObjectRendererOldTests` (the capacity rules and the guard; headless, no GL call); the mentor's app check (normals overlay on, toggled four times, then the collision debug view on: no `GL_INVALID` line, one "created for 128 vertices" line each for Lines and Points, exactly one "Lines grown from 128 to 16384 vertices"); the reviewer's GL harness (VAO valid across growth, Lines 128 to 16 384 to 1 048 576, no GL error, no managed allocation over 1 000 frames, one Warning at the cut).

## 2. `ObjectRenderer<T>`: batching and instancing

**Feeding it:** set `Shader`, then `InstancedShader`, then `DrawableItems` (an `ObservableCollection<T>`). `InstancedShader` is optional and may be set before or after `DrawableItems` (since OT-7, [KI-019](../../../KNOWN-ISSUES.md#ki-019-objectrenderer-draws-nothing-without-an-instanced-shader)): drawables are always filed into per-instance groups, and without `InstancedShader` everything is drawn one by one with `Shader`. A drawable added with a texture new to a scene whose groups are all instanced is not filed ([KI-024](../../../KNOWN-ISSUES.md#ki-024-objectrenderert-drops-a-drawable-whose-texture-is-new-to-a-fully-instanced-scene)). The renderer subscribes to `CollectionChanged`, and to each `TexturedObjectInfo.TextureChanged`, and regroups on adds, removes and texture changes. It does **not** watch `IDrawable.TexturedObjectInfos` itself, so give a drawable its textures before adding it to the collection.

**Grouping:** scene → texture → drawables (`DrawSceneInfo.Drawables`), plus mesh → texture → per-drawable `TextureData`. Each (mesh, shader) pair gets one VAO and VBO, created on first draw.

**Per-instance path** (`DefaultShader`): one `DrawArrays` per visible drawable with uniforms `model`, `color`, `textureTransform` (a 2×2 scale-and-rotate from `TextureData.Scale` and `RotationAngle`) and `hasTexture`.

**Instanced path** (`DefaultShader2`): when a **17th** drawable joins a scene-and-texture group (the check is "more than 15" before the add; setting `InstancedShader` later moves groups of 16 or more), the group moves to the instanced set and is drawn with one `DrawArraysInstanced`. Per-instance data is a 100-byte record (model matrix, color, texture transform, has-texture flag) in a dynamic buffer whose capacity is the next power of two, at least 32. Groups never move back. Only **visible** drawables are packed: `ObjectRenderer<T>.FillInstanceRecords` writes one record per visible drawable contiguously from index 0, and the upload and the draw use exactly that count (since OT-7, [KI-009](../../../KNOWN-ISSUES.md#ki-009-instanced-rendering-skips-the-wrong-objects-when-some-are-hidden)); the rented `ArrayPool` array is returned on the normal paths (the all-hidden return and after the draw), including for a group emptied by deletes. **Exceptions:** the draw path catches none ([CODE-STYLE rule](../../../CODE-STYLE.md#patterns), Denys, 2026-11-25); an exception propagates to the caller of the render pipeline (up the call chain from `OldTanks.Windows.MainWindow.OnRenderFrame`), the frame is torn, and a rental in flight is lost. The high-level caller is meant to catch it and re-run the pipeline, but no such handler exists yet: today the exception ends the game, unlogged ([KI-015](../../../KNOWN-ISSUES.md#ki-015-unhandled-exceptions-are-never-logged), [BACKLOG](../../../BACKLOG.md#game-and-engine-features-not-built)).

**Texture changes:** `DrawableTextureChanged` moves the drawable to the group of the new texture on the path where its old-texture group actually holds it (per-instance checked first, then instanced), and keeps the scene's other groups as they are. A texture can therefore have a per-instance and an instanced group in one scene ([KI-025](../../../KNOWN-ISSUES.md#ki-025-objectrenderert-can-keep-one-scene-texture-on-both-paths)). Before OT-7, retexturing an instanced drawable in a scene that also had per-instance groups threw `KeyNotFoundException` in the next frame (found and fixed in OT-7, no KI number).

**Vertices:** faces are expanded into a non-indexed triangle list of `CoolEngine.GraphicalEngine.Core.Primitives.Vertex` (position, normal, texture coordinate; 32 bytes). Only `iPos` and `iTextureCoord` are bound; normals are uploaded but no shader uses them, so there is no lighting.

**No texture:** a drawable whose `TextureData.Texture` is `Texture.Empty`, or whose mesh has no texture coordinates, is drawn in a flat rose, #AA556F as 0-1 channels, with `hasTexture` false, on both paths (fixed in OT-7, observed in the running game; before, it drew white: [KI-005](../../../KNOWN-ISSUES.md#ki-005-untextured-objects-use-a-0-255-color-in-a-0-1-color-space)). `TextureData.Color` is not read.

## 3. GPU-side types

| Type | Role |
|------|------|
| `CoolEngine.GraphicalEngine.Core.Shader` | Compiles and links a vertex and fragment pair (`Shader.Create`, which checks both stages and the link and throws `CoolEngine.Services.Exceptions.ShaderException` when the program does not link), caches active uniforms, uniform blocks and attributes by name, sets uniforms, describes vertex attributes (`SetAttributeData` splits attributes wider than 4 floats over consecutive locations) |
| `CoolEngine.GraphicalEngine.Core.Texture.Texture` | A GL texture handle: 2D (`CreateTexture2D`) or cube map (`CreateCubeSkyBoxTexture`); `Texture.Empty` is the handle-0 placeholder |
| `CoolEngine.GraphicalEngine.Core.Texture.TextureData` | How one mesh of one drawable is textured: texture, offset, scale, rotation, color |
| `CoolEngine.GraphicalEngine.Core.Texture.TexturedObjectInfo` | Mesh → `TextureData` for one drawable; `ChangeTexture` raises `TextureChanged` |
| `CoolEngine.GraphicalEngine.Core.Buffer<T>` | A typed GL buffer with a fixed element capacity |
| `CoolEngine.GraphicalEngine.Core.DrawObjectInfo` | A VAO, VBO and EBO triple with vertex and index counts |
| `CoolEngine.GraphicalEngine.Core.Scene`, `Mesh`, `Face` | Loaded geometry: a scene is the meshes of one OBJ file ([ASSET-PIPELINE](../Loaders/ASSET-PIPELINE.md)) |
| `CoolEngine.GraphicalEngine.Core.Camera` | Position, look **direction** (`Rotation`, a unit vector, not angles), up vector, FOV, `LookAt` view matrix, free-fly `Move` and mouse `Rotate` |
| `CoolEngine.GraphicalEngine.Core.RenderTarget` | The offscreen framebuffer the 3D scene is drawn into: RGBA8 colour and 32-bit float depth ([§5](#5-the-render-target-and-reversed-z)) |
| `CoolEngine.GraphicalEngine.Services.GLSettings` | Snapshot and restore of the GL state ImGui changes |

**Lookups by name (since OT-6, 2026-11-25):** `Shader.GetUniformLocation`, `GetUniformBlockIndex` and `GetAttribLocation` return -1 for a name the linked program has no active entry for (GL ignores -1 in uniform calls), and log a Warning once per kind and name. Members of a uniform block have no location and are reported like unknown names (no shader declares a block today). `SetAttributeData` and `BindUniformBlock` make no GL call for -1. After changing a shader, check the console or `log.txt` for compile, link and "has no active" lines, and check every name the C# side sets.

| Case | What happens now | Seen |
|------|------------------|------|
| Stage compiles with a log, program links with a log | Warning lines, program used | Checked on the driver by the reviewer's harness (NVIDIA 621.24): a stage with `#extension ...: warn` gave Warning lines and no Error; NVIDIA repeats a stage warning in the link log, so one driver warning gives two Warning lines |
| Stage fails to compile or program fails to link | Error lines with the driver's log; `Shader.Create` deletes the stage shaders and the program and throws `ShaderException` ("fail fast") | Red run 2026-11-25 at the KI-008 commit of `bug/OT-6` (`3DFontShader`, C1105) |
| `GetAttribLocation` result passed straight to `GL.VertexAttribPointer` (`TextRenderer`, `ObjectRendererOld`, `CollisionRenderer`, `CollisionRenderGroup`) for an unknown name | -1 raises `GL_INVALID_VALUE` once; every current name exists and is active | Checked on the driver by the reviewer's harness (NVIDIA 621.24) |

**Consequences of a link failure (fail fast):** `OldTanks.Services.GameManager.LoadShaders` loads the shader folders alphabetically (`3DFontShader` first) and stops at the first that throws, so `ShadersLoaded` never fires and nobody awaits the faulted task ([KI-012](../../../KNOWN-ISSUES.md#ki-012-startup-continues-after-a-failed-asset-load)); since OT-8 the unobserved-task handler may log it late, after a garbage collection, or never ([ARCHITECTURE §4](../../../ARCHITECTURE.md#4-startup-and-the-frame)). The `Task.WhenAll` continuation in `MainWindow.OnLoad` then fails on the missing `SkyBoxShader` (an `ArgumentNullException` in `ObjectRendererOld.RegisterScene`) before `m_readyToHandle` is set: the window stays blank and still closes with Esc; the dependable trace is the two Error lines (observed in the red run of 2026-11-25, before OT-8). If the ImGui shader (`OldTanks.UI.Services.ImGUI.ImGuiController.CreateDeviceResources`) failed to link, the exception would leave the window constructor and end the process; since OT-8 `Program.CurrentDomainOnUnhandledException` logs it at `Fatal` first ([KI-015](../../../KNOWN-ISSUES.md#ki-015-unhandled-exceptions-are-never-logged), closed; reasoned from code). All 8 programs link on the 2026-11-25 run of bug/OT-6; earlier failures: [KI-008](../../../KNOWN-ISSUES.md#ki-008-unknown-shader-names-resolve-to-location-0) and [KI-020](../../../KNOWN-ISSUES.md#ki-020-three-shaders-fail-to-compile-and-are-used-anyway) (closed). A name that hides a GLSL built-in (such as `texture`) fails to compile on NVIDIA drivers, so sampler and varying names must not reuse built-in names.

**Disposal (since OT-15, `bug/OT-15`, 2026-11-27, [KI-017](../../../KNOWN-ISSUES.md#ki-017-gl-objects-are-deleted-from-finalizers) closed):** `Shader`, `Texture`, `Buffer<T>` and `DrawObjectInfo` free their GL objects in `Dispose` (immediate, GL thread only; unchanged). Their finalizers run on the .NET finalizer thread, which has no GL context, so they make **no GL call**: a finalizer logs one Warning (`DrawObjectInfo` logged nothing before; `Texture`'s old text wrongly said "Abandoned Shader") and, unless the object was disposed, puts its GL names on `CoolEngine.GraphicalEngine.Services.GLDeletionQueue`. `RenderTarget` has no finalizer by design ([§5](#5-the-render-target-and-reversed-z)).

| Part | Behaviour |
|------|-----------|
| `GLDeletionQueue.Enqueue(GLObjectKind, int)` | Any thread, the finalizer thread included; ignores name 0; no GL call, no lock of its own (the `ConcurrentQueue<T>` inside locks briefly when it grows); never throws. `GLObjectKind`: `Texture`, `Buffer`, `VertexArray`, `Program`, each picking the delete call |
| `GLDeletionQueue.DeletePending()` | **GL thread only**, with a context: deletes every queued name; a name queued while it runs is deleted in that call or the next, never twice. An empty call costs about 4 to 7 ns and allocates 0 B (the reviewer's measurement) |
| Who calls it | `OldTanks.Windows.MainWindow.OnUpdateFrame` every frame right after the dispatcher's queue, and `OnUnload` first thing, while the context still exists; a name queued after that is freed with the context ([ARCHITECTURE §4](../../../ARCHITECTURE.md#4-startup-and-the-frame)) |
| Names queued | `Texture`: its texture; `Shader`: its program; `Buffer<T>`: its buffer; `DrawObjectInfo`: its vertex array, vertex buffer and, if it has one, element buffer |

**The host must drain the queue.** The engine relies on its host to call `DeletePending`: a second host (the planned render engine, a tool) that forgets it leaks silently, while each finalizer still logs "queued". Today no path of the game drops a GL object undisposed (reasoned from code), so the queue is a safety net; the known droppers are [KI-035](../../../KNOWN-ISSUES.md#ki-035-objectrenderert-drops-per-instance-draw-infos-without-disposing-them) and the `ImGuiController` leaks in the [BACKLOG](../../../BACKLOG.md#platform-and-build).

| Check | Result |
|-------|--------|
| Before, the analyst's harness (NVIDIA 621.24, RTX 5070) | Each of the four finalizers ended the process with an access violation `0xC0000005`, no managed handler seeing it; in a headless test host OpenTK threw `InvalidOperationException` ("You need to initialize the OpenGL binding first ...") on the finalizer thread, which also ends the process |
| After, the reviewer's GL harness (hidden 4.6 debug-context window) | A real `Texture`, `Shader`, `Buffer<float>` and `DrawObjectInfo` dropped undisposed: no crash, one Warning each on the finalizer thread, every name still allocated while queued; `DeletePending` on the GL thread deleted all 5 names with no GL error or debug message; a reused name is never touched; with no bindings the process survives |
| Tests, `tests/CoolEngine.Tests` (headless, 18 tests) | `TextureTests` 4, `ShaderTests` 2, `BufferTests` 2, `DrawObjectInfoTests` 3 and `GLDeletionQueueTests` 7, four of them concurrency tests: 8 producers against a draining thread; the finalizer thread against the draining thread; a name queued during a drain; names queued after the last drain stay queued ([BUILD §2](../../../BUILD.md#2-build)) |
| App check by the mentor, 2026-11-27 | Start, 20 spawn-button clicks, the debug view, quit: exit 0, no Error, Warning, "never disposed" or "Abandoned" line |

**Re-run the GL harness whenever `GLDeletionQueue`'s kind-to-delete mapping changes:** no headless test can see that mapping (the tests hand names to a fake deleter).

## 4. Shader contract

Each shader is a folder `Assets/Shaders/<Name>/` holding `<Name>.vert` and `<Name>.frag` (GLSL `#version 460 core`); the folder name is the cache name.

| Shader | Vertex inputs | Uniforms the C# side sets | Used by |
|--------|---------------|---------------------------|---------|
| `DefaultShader` | `iPos` (0), `iNormal` (1), `iTextureCoord` (2) | `projection`, `view`, `model`, `textureTransform`, `color`, `hasTexture` | `ObjectRenderer<T>` per-instance path |
| `DefaultShader2` | as above, plus per instance `iModel` (3-6), `iColor` (7), `iTextureTransform` (8), `iHasTexture` (9) | `projection`, `view` | `ObjectRenderer<T>` instanced path |
| `SkyBoxShader` | `iPos` | `projection`, `view` (a `mat3`, so the skybox ignores camera translation) | `ObjectRendererOld.DrawSkyBox`; the vertex shader writes clip depth 0 (the far plane in reversed Z, [§5](#5-the-render-target-and-reversed-z)) |
| `PrimitivesShader` | `iPos` | `projection`, `view`, `color` | Ray and normals in `OldTanks.Windows.MainWindow` |
| `CollisionShader` | `iPos` | `projection`, `view`, `color` | `CollisionRenderer` |
| `FontShader` | `iPos`, `iTexture` | `projection`, `model`, `color` | `TextRenderer` (2D and 3D) |
| `3DFontShader` | `iPos`, `iTexture` | — | Loaded, not used by any code |

**Not set from C#:** the samplers (`texture0` in every shader, `SkyBoxShader` included since OT-6) stay at unit 0, which is where every renderer binds its texture; the font shaders' `boldMultiplier` stays 0, which they treat as 1.

The ImGui renderer, `OldTanks.UI.Services.ImGUI.ImGuiController`, compiles its own shader from strings and saves and restores GL state around its draw ([UI-CONTROLS](../../../OldTanks.UI/UI-CONTROLS.md#5-the-imgui-renderer)).

## 5. Adding something to draw

1. Make the object an `IDrawable` (the game's `OldTanks.Models.WorldObject` already is) whose `Scene` comes from `GlobalCache<Scene>`.
2. Give it one `TexturedObjectInfo` with a `TextureData` for **every** mesh of its scene (a missing mesh throws in `ObjectRenderer<T>`; no `TexturedObjectInfo` at all makes it invisible).
3. Add it to `World.WorldObjects`; `ObjectRenderer<T>` picks it up (the game sets its `InstancedShader` at startup, though the renderer no longer needs it). For the debug view also call `CollisionRenderer.AddCollision`, and keep `ObjectRendererOld.AddDrawable` in step with what `OldTanks.UI.ImGuiUI.MainWindow` does until the legacy renderer is removed.

## 5. The render target and reversed Z

Since OT-5 (2026-11-26, merged into `master` by PR #20 at `5e04730`; the window-size rules since OT-16, 2026-11-27, `bug/OT-16`; reasoned from the code and measured by the review's GL harness and the app checks, as stated per row).

**`CoolEngine.GraphicalEngine.Core.RenderTarget`** (sealed, `IDisposable`): a framebuffer with an RGBA8 colour renderbuffer and a `DEPTH_COMPONENT32F` depth renderbuffer.

| Member | Behaviour |
|--------|-----------|
| `RenderTarget(int width, int height)` | Creates the framebuffer and both renderbuffers, checks completeness; throws `ArgumentOutOfRangeException` for a size below 1 (no GL object made) and `InvalidOperationException` naming the status for an incomplete framebuffer (its GL objects are deleted first) |
| `Width`, `Height` | The size of both renderbuffers |
| `Resize(int width, int height)` | Reallocates both renderbuffers (contents undefined); does nothing when the size is unchanged; throws below 1 or after disposal |
| `Bind()` | Binds the target as draw and read framebuffer and sets the viewport to its size; every frame |
| `BlitColorToWindow()` | Copies the colour 1:1 (nearest) to framebuffer 0, then binds framebuffer 0 and sets the viewport to the target's size; every frame. The blit is subject to the scissor test, so that must be off. On NVIDIA the blit ignores the colour mask (review D) |
| `Dispose()` | Deletes the three GL objects; repeated calls do nothing |

**GL thread only.** Creation, `Resize`, `Bind`, the blit and `Dispose` make GL calls (OpenGL 4.5 direct state access, plus `glBindFramebuffer` and `glViewport` in `Bind` and the blit). **No finalizer** (Denys, 2026-11-27, OT-15: keep it without one; turned down: the same queued finalizer as the other four types, [§3](#3-gpu-side-types)): a finalizer has no GL context, so a target that is never disposed keeps its objects until the context is destroyed. The owner, `OldTanks.Windows.MainWindow`, creates it in `OnLoad` at the window's `FramebufferSize`, resizes it in `ApplyWindowSize` after the viewport and disposes it in `OnUnload` (the sizes: below). A render frame: `Bind`, clear colour and depth, draw the skybox, objects or collision view and primitives, `BlitColorToWindow`, then HUD, labels and ImGui on the window with the depth test off ([ARCHITECTURE §4](../../../ARCHITECTURE.md#4-startup-and-the-frame)). The window requests no depth or stencil bits (`NativeWindowSettings.DepthBits` and `StencilBits` 0), so a driver setting that forces multisampling on the default framebuffer cannot break the blit. The error screen draws on the window only. No unit test is possible (GL); review D's harness checked depth values, lifetime and a failed construction, and the blit costs about 9 µs of GPU time (measured by that harness, 2026-11-26).

**Reversed Z.**

| Part | Rule |
|------|------|
| Clip control | `GL.ClipControl(LowerLeft, ZeroToOne)` once in `OldTanks.Windows.MainWindow`'s `ApplyGLSettings`: clip depth [0, 1] |
| Clear | `GL.ClearDepth(0)` once at load: depth 0 is the far plane, so every depth clear is 0 |
| Depth functions | `Greater` for objects, the collision view and primitives; `Gequal` for the skybox |
| Projections | `CoolEngine.Mathematics.Matrix4.CreatePerspectiveFieldOfView` and `CreateOrthographicOffCenter` (near maps to 1, far to 0) |
| Skybox | `SkyBoxShader.vert` writes `vec4(pos.xy, 0.0, pos.w)`: depth 0 passes `Gequal` against the clear and stays behind everything drawn with `Greater` |
| Why | A 32-bit float depth with reversed Z keeps precision far away. Review D's harness resolved 1000 from 1000.1 and 1990 from 1990.5, which the old pipeline (standard Z, `LEQUAL`) z-fought |

**Window sizes (since OT-16, `bug/OT-16`, 2026-11-27; closes [KI-032](../../../KNOWN-ISSUES.md#ki-032-the-viewport-projections-imgui-and-render-target-use-the-windows-outer-size)).** OpenTK 4.9.4's `NativeWindow.Size` is the **outer** size: the client area plus the GLFW frame (816 by 639 for an 800 by 600 client area at 100%). Nothing draws from it now. `OldTanks.Windows.WindowMetrics` holds the two sizes that do, and `OldTanks.Windows.MainWindow.ApplyWindowSize` applies them, from `OnResize` and from the `OnFramebufferResize` override:

| Size | Source | Used for |
|------|--------|----------|
| Framebuffer | `FramebufferSize` | The viewport, the `RenderTarget`, the perspective aspect (`WindowMetrics.PerspectiveAspect` keeps the clamp to at least 1; `CreatePerspective`) |
| Client | `ClientSize` | `EngineSettings.Current.WindowWidth` and `WindowHeight`, the screen projection (`CreateScreenProjection`, in client units: the HUD's `TextRenderer` text), ImGui ([UI-CONTROLS §5](../../../OldTanks.UI/UI-CONTROLS.md#5-the-imgui-renderer)) |
| Outer | `Size` | Nothing |

On Windows the client and framebuffer sizes are equal at every display scale (GLFW makes the process per-monitor DPI aware); only the frame grows: 16 by 39 at 100%, 18 by 47 at 125%, 22 by 56 at 150%, 24 by 64 at 175%, 26 by 71 at 200%, none in fullscreen (the analyst's probe of a hidden 800 by 600 window, 2026-11-27). `WindowMetrics.TryCreate` rejects any empty size, so a minimised window, or one with a zero-height client area, **keeps the last sizes** (the orthographic factory rejects an empty range too), and `OldTanks.Windows.MainWindow.OnRenderFrame` draws nothing while the framebuffer is empty but still swaps buffers: with VSync on the swap is the loop's only pacing (without it the minimised loop used 97-98% of one core; with it 5.9%, against `master`'s 10.9%, the mentor's measurement of 2026-11-27). `ApplyWindowSize` also returns while the render target is null, before `OnLoad` and after `OnUnload`: the constructor's fullscreen switch raises the resize callbacks synchronously, which crashed the game at start when the settings had `FullScreen` true. `OnMouseWheel` rebuilds the perspective from the stored metrics. Checked in the running game on 2026-11-27 (against `master` at `5e04730`): at 800 by 600 the HUD shows all eight lines (`master` showed none of the FPS and sub-iteration lines and clipped the third), in fullscreen ImGui sits where it does in the window (`master` drew it 39 px lower), a resize to 1200 by 700, a zero-height client, minimise and restore and `Alt` + `Enter` raised no error, exception or GL error. A display scale other than 100% was not run (a system setting).

**Skybox upload (fixed in OT-5, no KI number):** `ObjectRendererOld.CreateSkyBoxDrawMeshInfo` uploaded three times the skybox array, so the driver read 864 bytes past the managed array (it crashed review D's GL harness with an access violation; the game survived by luck). It now uploads exactly the array: the 36 vertices `DrawSkyBox` draws.
