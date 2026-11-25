# CoolEngine

> **Project knowledge:** [../docs/PROJECT-KNOWLEDGE.md](../docs/PROJECT-KNOWLEDGE.md)

The game engine: OpenGL rendering, asset loading, rigid-body physics and collision tests, the GL-thread dispatcher and the name-keyed global caches. It knows nothing about the game.

## Related Docs

| Topic | Page |
|-------|------|
| Threads, the dispatcher, singletons, frame order | [ARCHITECTURE](../docs/ARCHITECTURE.md) |
| Renderers, batching and instancing, shader contract, GPU types | [RENDERING](../docs/CoolEngine/Services/Renderers/RENDERING.md) |
| Loaders, asset folders, cache keys | [ASSET-PIPELINE](../docs/CoolEngine/Services/Loaders/ASSET-PIPELINE.md) |
| Rigid body, collision tests, shapes, the game's step loop | [PHYSICS-AND-COLLISIONS](../docs/CoolEngine/PhysicEngine/PHYSICS-AND-COLLISIONS.md) |
| Known defects here | [KNOWN-ISSUES](../docs/KNOWN-ISSUES.md): KI-005, KI-006, KI-008, KI-009, KI-011, KI-016, KI-017, KI-019 |

## Project Details

| Property | Value |
|----------|-------|
| Namespace root | `CoolEngine` |
| Target framework | `net10.0`, `LangVersion latest`, `AllowUnsafeBlocks` |
| Layer | Engine; referenced by `OldTanks.UI` and `OldTanks` |

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
| `Services/Loaders/` | `WaveFrontLoader`, `ShaderLoader`, `TextureLoader`, `SkyBoxTextureLoader` |
| `Services/Threading/` | `Dispatcher`, `DispatcherOperation`, `DispatcherSynchronizationContext` |
| `Services/Interfaces/` | `IDrawable`, `IPhysicObject`, `ITransformable`, `IMovable`, `ICollisionable`, `IWatchable`, `IRenderer<T>`, `IAssetLoader` |

## Dependencies

| Dependency | Reason |
|------------|--------|
| `Common` (project) | `ObservableObject`, typed events, extensions |
| OpenTK 4.8.1 | OpenGL bindings, math types, windowing types |
| SixLabors.ImageSharp 3.1.3 | Texture and skybox decoding (has known vulnerabilities, see BACKLOG) |
| FreeTypeSharp 1.1.3 | Font rasterising |
| Serilog 3.0.1 | Logging and GL debug output (core API only) |
| `Serilog.Sinks.File` 5.0.0, `Serilog.Sinks.FastConsole` 2.4.0 | Not used by this project's code: the game's `LoggerService` uses the file sink through this reference; FastConsole only in a commented-out line |
| MIConvexHull 1.1.19.1019 | Referenced, not used |

## Constraints

- **No game knowledge:** nothing here may reference `OldTanks.*`.
- **GL calls only on the GL thread.** New loaders keep GL work after an `await` started on the main thread or inside `UI.UIInvoke`; renderers create GL objects lazily in their draw methods (`ObjectRenderer<T>.Render`, `DrawSkyBox`, `DrawPrimitives`, `DrawText2D`/`DrawText3D`, `DrawElementsCollision`).
- **Changing a uniform or attribute name** means changing the shader files in `OldTanks/Assets/Shaders/` in the same change; mismatches fail silently (KI-008).
- **The static renderers get their shaders from the game:** `CollisionRenderer.Shader` and `TextRenderer.Shader` when shaders load, `ObjectRendererOld`'s skybox shader through `RegisterScene`; `ObjectRenderer<T>` needs `InstancedShader` set before drawables arrive (KI-019). A new renderer is documented in RENDERING.
- **`EngineSettings.CollisionIterations`** must stay in 1-128 (the setter throws).
