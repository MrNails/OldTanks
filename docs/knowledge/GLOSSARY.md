# Glossary

| Field        | Value  |
|--------------|--------|
| Status       | Living |
| Last updated | 2026-11-26 |

---

Terms used in the code and in these docs, with the type that implements each.

## Engine and game

| Term | Meaning | Type |
|------|---------|------|
| **AABB** | Axis-aligned bounding box; the cheap first collision test | `CoolEngine.Models.BoundingBox` |
| **Adam7** | PNG's seven-pass interlacing scheme | `CoolEngine.Imaging.Png.PngDecoder` |
| **Asset** | A file under `OldTanks/Assets/` loaded at startup: shader, texture, skybox, model, font | [ASSET-PIPELINE](../CoolEngine/Services/Loaders/ASSET-PIPELINE.md) |
| **Banned-API analyzer** | `Microsoft.CodeAnalysis.BannedApiAnalyzers` with `BannedSymbols.txt`: a use of System.Numerics' row-vector `Matrix4x4`, `Matrix3x2` or `Plane` (or a vector Transform overload taking them) is error RS0030 in `Common`, `CoolEngine`, `OldTanks.UI` and `OldTanks`; declarations are not flagged | [BUILD §2](../BUILD.md#2-build) |
| **Cache key** | The name an asset is stored and fetched under | `CoolEngine.Services.GlobalCache<T>` |
| **Central package management** | Every NuGet version set once in `Directory.Packages.props` (`ManagePackageVersionsCentrally`); project files list packages without a `Version` | [ARCHITECTURE §1](../ARCHITECTURE.md#1-solution-layout) |
| **Closest point (sphere × box)** | The point of a box nearest the sphere centre, found by clamping the centre into the box along its axes; the exact sphere × cuboid test | `Collision.SphereCuboidCollisionCheck` |
| **Collision** | One object's live collision shape and its tests | `CoolEngine.PhysicEngine.Core.Collision.Collision` |
| **Collision data** | A shape: polygon (vertices and faces) or sphere | `CoolEngine.PhysicEngine.Core.Collision.CollisionData` |
| **Column-major** | Matrix storage that lists the first column, then the second, and so on; `Matrix4` is four `System.Numerics.Vector4` columns, 64 bytes, and uploads to GLSL with `transpose = false` | [MATHEMATICS §5](../CoolEngine.Mathematics/MATHEMATICS.md#5-the-opengl-memory-layout) |
| **Column vector** | The convention where a matrix multiplies a vector from the left (`M * v`) and `A * B` applies `B` first; the opposite of System.Numerics' row vectors (`v * M`) | [MATHEMATICS §1](../CoolEngine.Mathematics/MATHEMATICS.md#1-conventions) |
| **Cuboid** | A box whose edges meet at right angles: the cube, or any scaled and rotated cube. Detected from the model-space shape once | `Collision.FindCuboidAxisFaces` |
| **Debug view** | Key `0`: collision wireframes instead of textured objects | `CoolEngine.Services.Renderers.CollisionRenderer` |
| **Dispatcher** | The queue that runs work on the GL thread | `CoolEngine.Services.Threading.Dispatcher` |
| **Drawable** | Anything the object renderer can draw: a scene, textures, a transform | `CoolEngine.Services.Interfaces.IDrawable` |
| **Edge × edge axis** | A separating axis perpendicular to one edge of each of two polygons (their cross product); needed besides the face normals to separate boxes that meet edge to edge | `Collision.EdgeAxesCollisionCheckLoop` |
| **Free camera** | The fly-through camera driven by `WASD`, `Space`, `Shift` | `CoolEngine.GraphicalEngine.Core.Camera.Move` |
| **Fused multiply-add (FMA)** | One instruction computing `a * b + c` with a single rounding; hardware-dependent, so results differ from separate multiply and add. The own math uses none | [MATHEMATICS §3](../CoolEngine.Mathematics/MATHEMATICS.md#3-the-same-bits-promise) |
| **GL thread** | The main thread, which owns the OpenGL context | [ARCHITECTURE §2](../ARCHITECTURE.md#2-threads) |
| **Instanced rendering** | Drawing many copies of a mesh in one call with per-copy data in a buffer; used from the 17th copy with one texture | `ObjectRenderer<T>` |
| **Golden hash** | The recorded hash of every result bit of the math library's fixed loop; `CrossPathTests` must reproduce it under four instruction-set paths, and it is re-recorded only after a .NET update once the four paths agree | `CoolEngine.Mathematics.Tests.CrossPathTests` |
| **Inverse-transpose** | The matrix that moves a normal so that it stays perpendicular to its face under uneven scale; the engine uses the cofactor matrix of the linear part (inverse-transpose times determinant) | `Collision.UpdateCollision` |
| **MCU** | Minimum coded unit: the group of 8×8 blocks (all components) that a JPEG scan codes together; sets the restart-interval step | `CoolEngine.Imaging.Jpeg.JpegDecoder` |
| **Mesh** | Vertices, texture coordinates, normals and faces of one part of a model | `CoolEngine.GraphicalEngine.Core.Mesh` |
| **OBB** | Oriented bounding box; the code's name for the polygon tests | `Collision.OBBCollisionCheck` |
| **OBJ** | Wavefront text model format | `CoolEngine.Services.Loaders.ModelsLoaders.WaveFrontLoader` |
| **On ground** | A body resting on something along gravity; allows a jump | `RigidBody.OnGround` |
| **Progressive JPEG** | A JPEG coded in several scans that refine the picture (DC first, then AC bands, then refinement bits); `Brick.jpg` is the one in the assets | `CoolEngine.Imaging.Jpeg.JpegDecoder` |
| **Picking ray** | The ray cast from the free camera each physics step; its nearest hit is drawn | `CoolEngine.Models.Ray` |
| **Player** | The world object the player camera follows; never set today | `OldTanks.Models.World.Player` |
| **Restart interval** | A JPEG's `DRI` setting: after every n MCUs the bit stream resets and an `RSTm` marker follows | `CoolEngine.Imaging.Jpeg.JpegDecoder` |
| **Reversed Z** | Depth mapped so that the near plane is 1 and the far plane 0, for better float precision; built into the own math's projections (OT-5), not yet used by the renderer | [MATHEMATICS §1](../CoolEngine.Mathematics/MATHEMATICS.md#1-conventions), [plan D7](../plans/ENGINES-AND-TANK-GAME.md#5-open-decisions) |
| **Rigid body** | Velocity, forces, limits and flags of a physical object | `CoolEngine.PhysicEngine.Core.RigidBody` |
| **SAT / separating axis** | Separating-axis test: two convex shapes miss if their projections on some axis do not overlap | `Collision` |
| **Same-bits promise (path-stable)** | The own math's D6 guarantee: members that use only + − × ÷, sqrt and exact operations give the same bits on every x64 CPU for finite inputs; trig members follow the OS math library and a NaN's sign is not covered | [MATHEMATICS §3](../CoolEngine.Mathematics/MATHEMATICS.md#3-the-same-bits-promise) |
| **Scene** | All meshes of one loaded model | `CoolEngine.GraphicalEngine.Core.Scene` |
| **Skybox** | A cube-map texture drawn behind everything | `CoolEngine.GraphicalEngine.Core.SkyBox` |
| **Strict decoding** | A decoder that throws `InvalidDataException` on any nonconforming or damaged data instead of concealing it | [IMAGE-DECODERS §3](../CoolEngine.Imaging/IMAGE-DECODERS.md#3-the-strict-rules) |
| **Static body** | A body that never moves (`IsStatic`); static-static pairs are not tested | `RigidBody.IsStatic` |
| **Step mode** | Physics paused (`U`), advanced one step at a time (`I`) | `World.IsActive`, `World.Stop` |
| **Sub-iteration** | One of the `CollisionIterations` slices a physics step is cut into | `CoolEngine.Services.EngineSettings.CollisionIterations` |
| **Textured object info** | For one drawable: which `TextureData` each mesh uses | `CoolEngine.GraphicalEngine.Core.Texture.TexturedObjectInfo` |
| **VAO, VBO, EBO** | OpenGL vertex array, vertex buffer and index buffer objects | `CoolEngine.GraphicalEngine.Core.DrawObjectInfo` |
| **World object** | The game's base object: drawable, physical, can carry a camera | `OldTanks.Models.WorldObject` |

## UI

| Term | Meaning | Type |
|------|---------|------|
| **Bindable element** | A control field whose value the generator binds to an object property | `OldTanks.UI.SourceGenerators.Attributes.BindableElementAttribute` |
| **Control handler** | The singleton that registers every control and draws the ImGui windows | `OldTanks.UI.Services.ControlHandler` |
| **Debug window** | The ImGui window for the camera, the object list and the selected object | `OldTanks.UI.ImGuiUI.MainWindow` |
| **HUD** | Screen text in the top-left corner | `OldTanks.UI.Controls.TextBlock` |
| **ImGui** | Dear ImGui, an immediate-mode UI library, used through ImGui.NET | `OldTanks.UI.ImGuiControls.*` |
| **Texture window** | The ImGui window that retextures a mesh of the selected object | `OldTanks.UI.ImGuiUI.TextureWindow` |

## Name clashes

Same short name, different types; write the namespace when it matters.

| Short name | Types |
|------------|-------|
| `EngineSettings` | `CoolEngine.Services.EngineSettings` (engine singleton: projections, collision iterations, global lock) and `OldTanks.Services.EngineSettings` (game: far plane, movement axis). `OldTanks.Windows.MainWindow` aliases the engine one |
| `MainWindow` | `OldTanks.Windows.MainWindow` (the OpenTK game window) and `OldTanks.UI.ImGuiUI.MainWindow` (the ImGui debug window) |
| `Face` | `CoolEngine.GraphicalEngine.Core.Face` (render face: vertex, texture and normal indices) and `CoolEngine.PhysicEngine.Core.Face` (collision face: indices and a normal) |
| `Rgba32` | `CoolEngine.Imaging.Rgba32` (the engine's pixel) and `SixLabors.ImageSharp.PixelFormats.Rgba32`; both are in scope in `tests/CoolEngine.Imaging.Tests`, which aliases them (`ReferenceDecoders` imports the second as `SixLaborsRgba32`) |
| `EventHandler<TSender, TEventArgs>` | `System.EventHandler<TSender, TEventArgs>` (.NET 10) replaced `Common.Infrastructure.Delegates.EventHandler<TSender, TArg>` in OT-3 |
| `MathHelper` | `OpenTK.Mathematics.MathHelper`, `Common.Helpers.MathHelper` (quadratic solver) and `CoolEngine.Mathematics.MathHelper` (OT-5; unused so far) |
| `Matrix2`, `Matrix3`, `Matrix4` | `OpenTK.Mathematics.*` (row vectors; used everywhere today) and `CoolEngine.Mathematics.*` (column vectors; OT-5, unused so far). `Vector3` and `Quaternion` are OpenTK's in engine and game code and `System.Numerics`' in the ImGui controls and in the new library |
| `ArrayExtensions`, `PrimitiveExtensions` | One each in `Common.Extensions` and `CoolEngine.Services.Extensions` |
| `Rotation` | Euler angles on `WorldObject` ([KI-002](../KNOWN-ISSUES.md#ki-002-worldobjectrotation-is-read-as-radians-in-one-place-and-degrees-in-another)), a unit look direction on `Camera` |
| "Yaw", "Pitch", "Roll" | On `WorldObject`, rotation about X, Y and Z, in that order; not the usual aviation meaning |
