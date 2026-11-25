# Asset Pipeline

| Field        | Value  |
|--------------|--------|
| Status       | Living |
| Last updated | 2026-11-25 |
| Verified against | 2026-11-25; bug/OT-12 rebased on master at 734d8f6 (§3's OBJ line against the code); feature/OT-3, uncommitted, bug/OT-6 for the font line and bug/OT-9 on master at d749ec8 (§3's texture line, read from code); §3's texture and font lines against the code and the 2026-11-25 runs (textures, the ImGui font, the HUD text and a world label showed); the rest as of 2026-11-24 (develop at d32cbbf, against the code and OldTanks/Assets) |

---

How files under `OldTanks/Assets/` become shaders, textures, models and fonts in the engine's caches: the loaders in `CoolEngine/Services/Loaders/` and the font loader, driven at startup by `OldTanks.Services.GameManager`.

## 1. Where assets are found

- **Build:** `OldTanks.csproj` copies `Assets\**` to the output folder on every build (`CopyToOutputDirectory` `Always`).
- **Runtime:** every folder is `Environment.CurrentDirectory` + `Settings.AssetPath` (default `Assets`) + the per-kind folder from `OldTanks.DataModels.Settings` ([BUILD §4](../../../BUILD.md#4-files-the-game-reads-and-writes)). The path is relative to the **working directory**, not to the executable, so the game must be started from a folder that contains `Assets/` ([BUILD §3](../../../BUILD.md#3-run)).
- **Startup:** `MainWindow.OnLoad` starts all five `GameManager.Load*` methods without waiting; they are `async` and run on the main thread between their awaits, and each raises its `*Loaded` event when done. Nothing waits for a single kind, so code that needs an asset runs in the `Task.WhenAll` continuation or in a `*Loaded` handler.

## 2. Cache keys

Every loaded asset ends up in `CoolEngine.Services.GlobalCache<T>.Default` under a name (the four `IAssetLoader`s store it themselves; for fonts, `GameManager.LoadFonts` stores what `Font.CreateFont` returns), and every consumer fetches by that name with `GetItemOrDefault`, which returns `null` for a missing key.

| Kind | Settings folder (default) | What is scanned | Loader | Cache | Key |
|------|---------------------------|-----------------|--------|-------|-----|
| Shaders | `ShadersDirectory` (`Shaders`) | Each **subfolder** | `CoolEngine.Services.Loaders.ShaderLoaders.ShaderLoader` | `GlobalCache<Shader>` | Folder name; needs `<Name>.vert` and `<Name>.frag` inside |
| Textures | `TexturesDirectory` (`Textures`) | Each **file** at the top level only | `CoolEngine.Services.Loaders.TextureLoaders.TextureLoader` | `GlobalCache<Texture>` | File name without extension |
| Skyboxes | `SkyBoxesDirectory` (`Textures\SkyBoxes`) | Each **subfolder** | `CoolEngine.Services.Loaders.TextureLoaders.SkyBoxTextureLoader` | `GlobalCache<Texture>` | Folder name; needs `right`, `left`, `top`, `bottom`, `front`, `back` `.jpg` |
| Models | `ModelsDirectory` (`Models`) | Files at the top level and in each subfolder one level down | `CoolEngine.Services.Loaders.ModelsLoaders.WaveFrontLoader` | `GlobalCache<Scene>` | **The OBJ's object name** (the last `o` line), not the file name |
| Fonts | `FontsDirectory` (`Fonts`) | Each **file** at the top level only | `CoolEngine.GraphicalEngine.Core.Font.Font.CreateFont`, cached by `GameManager.LoadFonts` | `GlobalCache<FontInformation>` | `FontInformation.FontName`: the file name without extension |
| Collision shapes | — (built in code) | — | Static constructors of `OldTanks.Models.Cube` and `Sphere` | `GlobalCache<CollisionData>` | `OldTanks.Infrastructure.CollisionConstants` |

Textures and skyboxes share one cache, so a texture file and a skybox folder with the same name overwrite each other.

**What is in the repository today** (2026-11-24):

| Asset | Cache key | Used by |
|-------|-----------|---------|
| `Models/Default/Cube.obj` | `Cube_Cube.005` | `OldTanks.Models.Cube` |
| `Models/Default/Sphere.obj` | `Sphere` | `OldTanks.Models.Sphere` |
| `Models/Default/Monke.obj` | `Suzanne` | Nothing |
| `Models/Default/Robot.obj` (155 objects) | `Cube.004_low_Cube.075` | Nothing; `OldTanks.Models.Robot` asks for `Robot` ([KI-007](../../../KNOWN-ISSUES.md#ki-007-robot-cannot-be-constructed)) |
| `Textures/*.jpg`, `*.png` | `Brick`, `Container`, `FloorTile`, `awesomeface`, `wall-texture` | The default scene and the texture window |
| `Textures/SkyBoxes/SkyBox2/` | `SkyBox2` | `World.SkyBox` |
| `Textures/SkyBoxes/SkyBox1.png` | — | Not loaded (skyboxes are folders) |
| `Fonts/Arial.ttf`, `TimesNewRoman.ttf` | `Arial`, `TimesNewRoman` | `Arial` for the HUD and 3D labels |
| `Fonts/Arial/Arial.csv`, `Arial.png` | — | Not loaded (only top-level files are) |
| `Shaders/*` | The folder names | [RENDERING §4](../Renderers/RENDERING.md#4-shader-contract) |

## 3. Loader details

- **OBJ (`WaveFrontLoader`):** reads `v`, `vt`, `vn` and `f`; ignores `mtllib`, `usemtl` and `s` (no materials). Faces must be triangles or quads; quads are split into two triangles. A `v` line that follows other data starts a new `Mesh`, so a multi-object file becomes one `Scene` with several meshes (`Robot.obj` has 155 `o` blocks). A face that is not a triangle or quad, or a face index that does not parse, stops the load and nothing is cached; a number that does not parse in a `v`, `vt` or `vn` line is logged and the load goes on, except that when such a line is the file's last data line the last mesh is dropped ([KI-030](../../../KNOWN-ISSUES.md#ki-030-a-malformed-last-v-vt-or-vn-line-drops-the-last-mesh-of-an-obj-file)). `CoolEngine.GraphicalEngine.Core.Scene.Center` is the midpoint of the model-space axis-aligned bounds of every vertex of every mesh, `(0, 0, 0)` for a scene with no vertices (pinned by `CoolEngine.Tests.GraphicalEngine.Core.SceneTests`, not a decided rule; fixed in OT-12, [KI-006](../../../KNOWN-ISSUES.md#ki-006-scenecenter-is-computed-from-a-wrong-maximum)); nothing reads it yet.
- **Textures:** decoded by `CoolEngine.Imaging.ImageDecoder.LoadAsync` ([IMAGE-DECODERS](../../../CoolEngine.Imaging/IMAGE-DECODERS.md)) as RGBA, flipped vertically for GL, uploaded as `Rgba` with mipmaps. Skybox faces go through the same decoder and are not flipped. **PNG and JPEG only, and strict:** a file in another format (BMP, GIF, TGA and WebP were read by ImageSharp) or a damaged one throws `NotSupportedException` or `InvalidDataException` from the loader. Since OT-9 `OldTanks.Services.GameManager.LoadTextures` and `LoadSkyBoxes` catch it per file or skybox folder, log `Error loading texture {TextureName}.` or `Error loading skybox {SkyBoxName}.` and carry on with the rest, so the name is missing from the cache: an object that asks for it draws untextured (`Texture.Empty`) and a skybox without a texture is not drawn. A missing folder is not caught and still fails the startup, which shows the "Startup failed" window ([KI-012](../../../KNOWN-ISSUES.md#ki-012-startup-continues-after-a-failed-asset-load), [FEATURES-OVERVIEW §5](../../../OldTanks/FEATURES-OVERVIEW.md#5-debug-windows)). The decode runs on the thread pool; the GL upload after the `await` runs on the main thread.
- **Fonts:** FreeType (FreeTypeSharp 3, its pointer API behind small `unsafe` helpers in `Font`) rasterises each glyph at 32 px into one atlas texture; per-glyph size, bearing, advance and atlas offset go into `CharacterInfo`. The rasterising runs in `Task.Run`, and the atlas upload is marshalled to the GL thread with `UI.UIInvoke`. A `Font` is then a `FontInformation` plus a display size (the HUD uses 16). The FreeTypeSharp 3 port is confirmed visually: on 2026-11-25 the run of `bug/OT-6` showed the HUD text and the world label "Wall 2" readable, once the font shaders compiled ([KI-020](../../../KNOWN-ISSUES.md#ki-020-three-shaders-fail-to-compile-and-are-used-anyway), closed).
- **GL thread:** shader and texture creation happen after an `await`, which resumes on the main thread through the dispatcher ([ARCHITECTURE §3](../../../ARCHITECTURE.md#3-getting-work-onto-the-gl-thread)). A new loader must keep its GL calls on that path.

## 4. Adding assets

| To add | Do |
|--------|----|
| A texture | Drop the file in `Assets/Textures/`; it is selectable in the texture window by file name. At runtime the texture window's **Add texture** loads any file path |
| A skybox | Add a folder with the six named `.jpg` faces under `Assets/Textures/SkyBoxes/`; select it by setting `World.SkyBox.Texture` (`MainWindow.OnSkyBoxesLoaded` picks `SkyBox2`) |
| A model | Drop the `.obj` under `Assets/Models/` (or one subfolder down), note its last `o` name, and fetch `GlobalCache<Scene>` with that name ([features §6](../../../OldTanks/FEATURES-OVERVIEW.md#6-adding-a-world-object-type)) |
| A shader | Add `Assets/Shaders/<Name>/<Name>.vert` and `.frag`; meet the names the C# side uses ([RENDERING §4](../Renderers/RENDERING.md#4-shader-contract)) |
| A font | Drop the `.ttf` in `Assets/Fonts/`; build a `Font` from `GlobalCache<FontInformation>` by file name |
