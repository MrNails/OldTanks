# CoolEngine

> **Project knowledge:** [../docs/PROJECT-KNOWLEDGE.md](../docs/PROJECT-KNOWLEDGE.md)

The game engine: OpenGL rendering, asset loading, rigid-body physics and collision tests, the GL-thread dispatcher and the name-keyed global caches. It knows nothing about the game.

## Related Docs

| Topic | Page |
|-------|------|
| Threads, the dispatcher, singletons, frame order | [ARCHITECTURE](../docs/ARCHITECTURE.md) |
| Renderers, batching and instancing, shader contract, GPU types | [RENDERING](../docs/CoolEngine/Services/Renderers/RENDERING.md) |
| Loaders, asset folders, cache keys | [ASSET-PIPELINE](../docs/CoolEngine/Services/Loaders/ASSET-PIPELINE.md) |
| Image decoding (PNG, JPEG) | [IMAGE-DECODERS](../docs/CoolEngine.Imaging/IMAGE-DECODERS.md) |
| Rigid body, collision tests, shapes, the game's step loop | [PHYSICS-AND-COLLISIONS](../docs/CoolEngine/PhysicEngine/PHYSICS-AND-COLLISIONS.md) |
| Known defects here | [KNOWN-ISSUES](../docs/KNOWN-ISSUES.md): KI-006, KI-008, KI-011, KI-016, KI-017, KI-020, KI-024, KI-025 |

## Project Details

| Property | Value |
|----------|-------|
| Namespace root | `CoolEngine` |
| Target framework | `net10.0`, `LangVersion latest`, `AllowUnsafeBlocks` |
| Layer | Engine; referenced by `OldTanks.UI` and `OldTanks`; tested by `tests/CoolEngine.Tests` (`InternalsVisibleTo`; headless, so no test may call OpenGL) |

## Key Types

Representative, by folder.

| Folder | Types |
|--------|-------|
| `GraphicalEngine/Core/` | `Camera`, `Shader`, `Scene`, `Mesh`, `Face`, `SkyBox`, `Buffer<T>`, `DrawObjectInfo`; `Texture/` (`Texture`, `TextureData`, `TexturedObjectInfo`); `Font/` (`Font`, `FontInformation`); `Primitives/` (`Vertex`, `FontVertex`) |
| `GraphicalEngine/Services/` | `GLSettings` (GL state snapshot and restore) |
| `PhysicEngine/` | `PhysicsConstants`; `Core/RigidBody`, `Core/Face`; `Core/Collision/` (`Collision`, `CollisionData`) |
| `Models/` | `Ray`, `BoundingBox`, `Level` (unused) |
| `Services/` | `Application`, `EngineSettings`, `GlobalCache<T>`, `UI` (`UIInvoke`), `Colors` |
| `Services/Renderers/` | `ObjectRenderer<T>`, `ObjectRendererOld` (legacy: skybox, primitives), `CollisionRenderer`, `TextRenderer` |
| `Services/Loaders/` | `WaveFrontLoader`, `ShaderLoader`, `TextureLoader`, `SkyBoxTextureLoader` (the last two decode through `CoolEngine.Imaging.ImageDecoder`) |
| `Services/Threading/` | `Dispatcher`, `DispatcherOperation`, `DispatcherSynchronizationContext` |
| `Services/Interfaces/` | `IDrawable`, `IPhysicObject`, `ITransformable`, `IMovable`, `ICollisionable`, `IWatchable`, `IRenderer<T>`, `IAssetLoader` |

## Dependencies

| Dependency | Reason |
|------------|--------|
| `Common` (project) | `ObservableObject`, typed events, extensions |
| `CoolEngine.Imaging` (project) | PNG and JPEG decoding for the texture and skybox loaders |
| OpenTK 4.9.4 | OpenGL bindings, math types, windowing types |
| FreeTypeSharp 3.1.0 | Font rasterising, through its pointer API (`Font` wraps it in small `unsafe` helpers) |
| Serilog 4.4.0 | Logging and GL debug output (core API only) |
| `Serilog.Sinks.File` 7.0.0 | Not used by this project's code: the game's `LoggerService` uses the file sink through this reference |

Versions are set in `Directory.Packages.props`. MIConvexHull, `Serilog.Sinks.FastConsole` and SixLabors.ImageSharp were removed in OT-3.

## Constraints

- **No game knowledge:** nothing here may reference `OldTanks.*`.
- **GL calls only on the GL thread.** New loaders keep GL work after an `await` started on the main thread or inside `UI.UIInvoke`; renderers create GL objects lazily in their draw methods (`ObjectRenderer<T>.Render`, `DrawSkyBox`, `DrawPrimitives`, `DrawText2D`/`DrawText3D`, `DrawElementsCollision`).
- **Changing a uniform or attribute name** means changing the shader files in `OldTanks/Assets/Shaders/` in the same change; mismatches fail silently (KI-008).
- **The static renderers get their shaders from the game:** `CollisionRenderer.Shader` and `TextRenderer.Shader` when shaders load, `ObjectRendererOld`'s skybox shader through `RegisterScene`; `ObjectRenderer<T>` works without `InstancedShader` (everything is then drawn one by one) and accepts it before or after the drawables (KI-019, closed in OT-7). A new renderer is documented in RENDERING.
- **`EngineSettings.CollisionIterations`** must stay in 1-128 (the setter throws).
