# Rendering

| Field        | Value  |
|--------------|--------|
| Status       | Living |
| Last updated | 2026-11-25 |
| Verified against | 2026-11-25; bug/OT-7 (committed, rebased on master at d2f8b8a); §2 against the code, the new tests and two runs of the game (the colour and the retexture); the other sections as of 2026-11-24 (develop at d32cbbf) |

---

How the engine draws a frame: the four renderers in `CoolEngine/Services/Renderers/`, the GPU-side types they use, and the contract each shader in `OldTanks/Assets/Shaders/` must meet. Frame order and threads: [ARCHITECTURE §4](../../../ARCHITECTURE.md#4-startup-and-the-frame).

## 1. Renderers

| Renderer | Kind | Draws | Shader (cache name) |
|----------|------|-------|---------------------|
| `CoolEngine.Services.Renderers.ObjectRenderer<T>` | Instance, `IRenderer<T>` | The world's drawables, batched per mesh and texture | `DefaultShader`; `DefaultShader2` for instanced batches |
| `CoolEngine.Services.Renderers.ObjectRendererOld` | Static, legacy | The skybox (`DrawSkyBox`) and debug lines and points (`DrawPrimitives`) | `SkyBoxShader`; the caller's shader for primitives (`PrimitivesShader`) |
| `CoolEngine.Services.Renderers.CollisionRenderer` | Static | Debug view: collision edges, vertex labels, face normals | `CollisionShader` |
| `CoolEngine.Services.Renderers.TextRenderer` | Static | Screen text (`DrawText2D`, the HUD) and world text (`DrawText3D`, object names and vertex labels, optionally billboarded) | `FontShader` |

`CoolEngine.Services.Renderers.SkyBoxRenderer` is an empty placeholder. `ObjectRendererOld` still keeps a per-CLR-type list of drawables (`AddDrawable`, `RemoveDrawable`), and the game keeps it in sync, but the call that drew them is commented out; the world is drawn only by `ObjectRenderer<T>`.

## 2. `ObjectRenderer<T>`: batching and instancing

**Feeding it:** set `Shader`, then `InstancedShader`, then `DrawableItems` (an `ObservableCollection<T>`). `InstancedShader` is optional and may be set before or after `DrawableItems` (since OT-7, [KI-019](../../../KNOWN-ISSUES.md#ki-019-objectrenderer-draws-nothing-without-an-instanced-shader)): drawables are always filed into per-instance groups, and without `InstancedShader` everything is drawn one by one with `Shader`. A drawable added with a texture new to a scene whose groups are all instanced is not filed ([KI-024](../../../KNOWN-ISSUES.md#ki-024-objectrenderert-drops-a-drawable-whose-texture-is-new-to-a-fully-instanced-scene)). The renderer subscribes to `CollectionChanged`, and to each `TexturedObjectInfo.TextureChanged`, and regroups on adds, removes and texture changes. It does **not** watch `IDrawable.TexturedObjectInfos` itself, so give a drawable its textures before adding it to the collection.

**Grouping:** scene → texture → drawables (`DrawSceneInfo.Drawables`), plus mesh → texture → per-drawable `TextureData`. Each (mesh, shader) pair gets one VAO and VBO, created on first draw.

**Per-instance path** (`DefaultShader`): one `DrawArrays` per visible drawable with uniforms `model`, `color`, `textureTransform` (a 2×2 scale-and-rotate from `TextureData.Scale` and `RotationAngle`) and `hasTexture`.

**Instanced path** (`DefaultShader2`): when a **17th** drawable joins a scene-and-texture group (the check is "more than 15" before the add; setting `InstancedShader` later moves groups of 16 or more), the group moves to the instanced set and is drawn with one `DrawArraysInstanced`. Per-instance data is a 100-byte record (model matrix, color, texture transform, has-texture flag) in a dynamic buffer whose capacity is the next power of two, at least 32. Groups never move back. Only **visible** drawables are packed: `ObjectRenderer<T>.FillInstanceRecords` writes one record per visible drawable contiguously from index 0, and the upload and the draw use exactly that count (since OT-7, [KI-009](../../../KNOWN-ISSUES.md#ki-009-instanced-rendering-skips-the-wrong-objects-when-some-are-hidden)); the rented `ArrayPool` array is returned on every path.

**Texture changes:** `DrawableTextureChanged` moves the drawable to the group of the new texture on the path where its old-texture group actually holds it (per-instance checked first, then instanced), and keeps the scene's other groups as they are. A texture can therefore have a per-instance and an instanced group in one scene ([KI-025](../../../KNOWN-ISSUES.md#ki-025-objectrenderert-can-keep-one-scene-texture-on-both-paths)). Before OT-7, retexturing an instanced drawable in a scene that also had per-instance groups threw `KeyNotFoundException` in the next frame (found and fixed in OT-7, no KI number).

**Vertices:** faces are expanded into a non-indexed triangle list of `CoolEngine.GraphicalEngine.Core.Primitives.Vertex` (position, normal, texture coordinate; 32 bytes). Only `iPos` and `iTextureCoord` are bound; normals are uploaded but no shader uses them, so there is no lighting.

**No texture:** a drawable whose `TextureData.Texture` is `Texture.Empty`, or whose mesh has no texture coordinates, is drawn in a flat rose, #AA556F as 0-1 channels, with `hasTexture` false, on both paths (fixed in OT-7, observed in the running game; before, it drew white: [KI-005](../../../KNOWN-ISSUES.md#ki-005-untextured-objects-use-a-0-255-color-in-a-0-1-color-space)). `TextureData.Color` is not read.

## 3. GPU-side types

| Type | Role |
|------|------|
| `CoolEngine.GraphicalEngine.Core.Shader` | Compiles and links a vertex and fragment pair (`Shader.Create`), caches active uniforms and attributes by name, sets uniforms, describes vertex attributes (`SetAttributeData` splits attributes wider than 4 floats over consecutive locations) |
| `CoolEngine.GraphicalEngine.Core.Texture.Texture` | A GL texture handle: 2D (`CreateTexture2D`) or cube map (`CreateCubeSkyBoxTexture`); `Texture.Empty` is the handle-0 placeholder |
| `CoolEngine.GraphicalEngine.Core.Texture.TextureData` | How one mesh of one drawable is textured: texture, offset, scale, rotation, color |
| `CoolEngine.GraphicalEngine.Core.Texture.TexturedObjectInfo` | Mesh → `TextureData` for one drawable; `ChangeTexture` raises `TextureChanged` |
| `CoolEngine.GraphicalEngine.Core.Buffer<T>` | A typed GL buffer with a fixed element capacity |
| `CoolEngine.GraphicalEngine.Core.DrawObjectInfo` | A VAO, VBO and EBO triple with vertex and index counts |
| `CoolEngine.GraphicalEngine.Core.Scene`, `Mesh`, `Face` | Loaded geometry: a scene is the meshes of one OBJ file ([ASSET-PIPELINE](../Loaders/ASSET-PIPELINE.md)) |
| `CoolEngine.GraphicalEngine.Core.Camera` | Position, look **direction** (`Rotation`, a unit vector, not angles), up vector, FOV, `LookAt` view matrix, free-fly `Move` and mouse `Rotate` |
| `CoolEngine.GraphicalEngine.Services.GLSettings` | Snapshot and restore of the GL state ImGui changes |

**Lookups by name fail silently:** a uniform or attribute name the shader does not declare (or that the GLSL compiler optimised away) resolves to location 0, and link errors are not checked ([KI-008](../../../KNOWN-ISSUES.md#ki-008-unknown-shader-names-resolve-to-location-0)). After changing a shader, check `log.txt` for compile errors and check every name the C# side sets.

**Disposal:** `Shader`, `Texture`, `Buffer<T>` and `DrawObjectInfo` free their GL objects in `Dispose` and, if never disposed, in a finalizer ([KI-017](../../../KNOWN-ISSUES.md#ki-017-gl-objects-are-deleted-from-finalizers)).

## 4. Shader contract

Each shader is a folder `Assets/Shaders/<Name>/` holding `<Name>.vert` and `<Name>.frag` (GLSL `#version 460 core`); the folder name is the cache name.

| Shader | Vertex inputs | Uniforms the C# side sets | Used by |
|--------|---------------|---------------------------|---------|
| `DefaultShader` | `iPos` (0), `iNormal` (1), `iTextureCoord` (2) | `projection`, `view`, `model`, `textureTransform`, `color`, `hasTexture` | `ObjectRenderer<T>` per-instance path |
| `DefaultShader2` | as above, plus per instance `iModel` (3-6), `iColor` (7), `iTextureTransform` (8), `iHasTexture` (9) | `projection`, `view` | `ObjectRenderer<T>` instanced path |
| `SkyBoxShader` | `iPos` | `projection`, `view` (a `mat3`, so the skybox ignores camera translation) | `ObjectRendererOld.DrawSkyBox` |
| `PrimitivesShader` | `iPos` | `projection`, `view`, `color` | Ray and normals in `OldTanks.Windows.MainWindow` |
| `CollisionShader` | `iPos` | `projection`, `view`, `color` | `CollisionRenderer` |
| `FontShader` | `iPos`, `iTexture` | `projection`, `model`, `color` | `TextRenderer` (2D and 3D) |
| `3DFontShader` | `iPos`, `iTexture` | — | Loaded, not used by any code |

**Not set from C#:** the samplers (`texture0`, and `texture` in `SkyBoxShader`) stay at unit 0, which is where every renderer binds its texture; the font shaders' `boldMultiplier` stays 0, which they treat as 1.

The ImGui renderer, `OldTanks.UI.Services.ImGUI.ImGuiController`, compiles its own shader from strings and saves and restores GL state around its draw ([UI-CONTROLS](../../../OldTanks.UI/UI-CONTROLS.md#5-the-imgui-renderer)).

## 5. Adding something to draw

1. Make the object an `IDrawable` (the game's `OldTanks.Models.WorldObject` already is) whose `Scene` comes from `GlobalCache<Scene>`.
2. Give it one `TexturedObjectInfo` with a `TextureData` for **every** mesh of its scene (a missing mesh throws in `ObjectRenderer<T>`; no `TexturedObjectInfo` at all makes it invisible).
3. Add it to `World.WorldObjects`; `ObjectRenderer<T>` picks it up (the game sets its `InstancedShader` at startup, though the renderer no longer needs it). For the debug view also call `CollisionRenderer.AddCollision`, and keep `ObjectRendererOld.AddDrawable` in step with what `OldTanks.UI.ImGuiUI.MainWindow` does until the legacy renderer is removed.
