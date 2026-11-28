# Glossary

| Field        | Value  |
|--------------|--------|
| Status       | Living |
| Last updated | 2026-11-28 |

---

Terms used in the code and in these docs, with the type that implements each.

## Engine and game

| Term | Meaning | Type |
|------|---------|------|
| **26.6 fixed point** | A number with 6 fractional bits, so 1/64 pixel per unit: FreeType's format for outline points, advances and metrics, and the library's too | `CoolEngine.Fonts.OutlinePoint`, `GlyphMetrics`, `LineMetrics` |
| **AABB** | Axis-aligned bounding box; the cheap first collision test | `CoolEngine.Models.BoundingBox` |
| **Adam7** | PNG's seven-pass interlacing scheme | `CoolEngine.Imaging.Png.PngDecoder` |
| **Asset** | A file under `OldTanks/Assets/` loaded at startup: shader, texture, skybox, model, font | [ASSET-PIPELINE](../CoolEngine/Services/Loaders/ASSET-PIPELINE.md) |
| **Banned-API analyzer** | `Microsoft.CodeAnalysis.BannedApiAnalyzers` with `BannedSymbols.txt`: a use of System.Numerics' row-vector `Matrix4x4`, `Matrix3x2` or `Plane` (or a vector Transform overload taking them) or an OpenTK math type other than `Vector2`, `Vector2i` and `Color4` is error RS0030 in `Common`, `CoolEngine`, `OldTanks.UI`, `OldTanks` and the two engine test projects; declarations are not flagged | [BUILD §2](../BUILD.md#2-build) |
| **Blue zones** | CFF hinting: the horizontal bands (baseline, x-height, cap height, ascender, descender) a Private DICT declares (`BlueValues`, `OtherBlues`, `BlueScale`, `BlueShift`, `BlueFuzz`) that stems snap to at small sizes | `CoolEngine.Fonts.Hinting.Cff.CffBlues` |
| **Broad phase** | The cheap first pass of collision detection that picks the pairs worth an exact test; not built: today every pair is tried and the AABB test inside `CheckCollision` is the only filter | [BACKLOG](../BACKLOG.md#game-and-engine-features-not-built), [plan Q8](../plans/SIMULATION-AND-PHYSICS-SPLIT.md#6-decisions) |
| **Body handle** | A body of a `PhysicsWorld`: its slot and the slot's generation; a removed body's handle is rejected; valid only for the world that returned it | `CoolEngine.Physics.BodyHandle`; [PHYSICS-LIBRARY §4](../CoolEngine.Physics/PHYSICS-LIBRARY.md#4-storage-and-handles) |
| **Cache key** | The name an asset is stored and fetched under | `CoolEngine.Services.GlobalCache<T>` |
| **Central package management** | Every NuGet version set once in `Directory.Packages.props` (`ManagePackageVersionsCentrally`); project files list packages without a `Version` | [ARCHITECTURE §1](../ARCHITECTURE.md#1-solution-layout) |
| **CFF, CFF2** | Compact Font Format: the PostScript-style cubic outlines of an OpenType (`OTTO`) font; CFF2 is its variable-font version, with `blend` | `CoolEngine.Fonts.Cff.CffTable` |
| **Charstring** | The small Type 2 program that draws one CFF glyph (moves, lines, curves, hints, subroutine calls) | `CoolEngine.Fonts.Cff.Type2Interpreter` |
| **Closest point (sphere × box)** | The point of a box nearest the sphere centre, found by clamping the centre into the box along its axes; the exact sphere × cuboid test | `Collision.SphereCuboidCollisionCheck` |
| **cmap** | The font table mapping code points to glyph indices; formats 0, 4, 6, 10, 12, 13 and 14 are read (14: variation selectors) | `CoolEngine.Fonts.Sfnt.CmapTable` |
| **Command queue** | The queue through which any thread sends commands to the simulation thread; each gets a sequence number in the same critical section as its place, so the acknowledgement of a snapshot is exact | `CoolEngine.Core.Channels.CommandQueue<T>`; [CORE §3](../CoolEngine.Core/CORE.md#3-the-command-queue) |
| **Collision** | One object's live collision shape and its tests | `CoolEngine.PhysicEngine.Core.Collision.Collision` |
| **Collision data** | A shape: polygon (vertices and faces) or sphere | `CoolEngine.PhysicEngine.Core.Collision.CollisionData` |
| **Column-major** | Matrix storage that lists the first column, then the second, and so on; `Matrix4` is four `System.Numerics.Vector4` columns, 64 bytes, and uploads to GLSL with `transpose = false` | [MATHEMATICS §5](../CoolEngine.Mathematics/MATHEMATICS.md#5-the-opengl-memory-layout) |
| **Column vector** | The convention where a matrix multiplies a vector from the left (`M * v`) and `A * B` applies `B` first; the opposite of System.Numerics' row vectors (`v * M`) | [MATHEMATICS §1](../CoolEngine.Mathematics/MATHEMATICS.md#1-conventions) |
| **Cuboid** | A box whose edges meet at right angles: the cube, or any scaled and rotated cube. Detected from the model-space shape once | `Collision.FindCuboidAxisFaces` |
| **Debug view** | Key `0`: collision wireframes instead of textured objects | `CoolEngine.Services.Renderers.CollisionRenderer` |
| **DirectWrite** | Windows' font API; the test reference for glyph indices and WOFF2 unpacking, called through own COM interop | `tests/CoolEngine.Fonts.Tests/Reference/DirectWriteReference.cs` |
| **Dispatcher** | The queue that runs work on the GL thread | `CoolEngine.Services.Threading.Dispatcher` |
| **Drawable** | Anything the object renderer can draw: a scene, textures, a transform | `CoolEngine.Services.Interfaces.IDrawable` |
| **Edge × edge axis** | A separating axis perpendicular to one edge of each of two polygons (their cross product); needed besides the face normals to separate boxes that meet edge to edge | `Collision.EdgeAxesCollisionCheckLoop` |
| **Fixed tick** | A simulation step of constant length, run as often as real time requires; 60 per second (Denys, 2026-11-27). Built in `CoolEngine.Core` as an exact integer grid (tick `k` is due at `t0 + ceil(k · F / R)`, with a backlog cap), not yet used: today the physics thread steps by measured time | `CoolEngine.Core.Timing.FixedTickClock`; [CORE §2](../CoolEngine.Core/CORE.md#2-the-clock) |
| **Framebuffer, client and outer size** | The window's three sizes: the framebuffer (pixels GL draws into, `FramebufferSize`), the client area (the unit of the mouse position, `ClientSize`) and the outer size (`Size`: client plus the GLFW frame, used for nothing). GL uses the first, UI layout the second | `OldTanks.Windows.WindowMetrics`, [RENDERING §5](../CoolEngine/Services/Renderers/RENDERING.md#5-the-render-target-and-reversed-z) |
| **Free camera** | The fly-through camera driven by `WASD`, `Space`, `Shift` | `CoolEngine.GraphicalEngine.Core.Camera.Move` |
| **FreeType** | The C font library whose 2.13.2 version is the reference: `CoolEngine.Fonts` produces the same outlines, metrics and bitmaps; the engine no longer uses it, only the font tests do (FreeTypeSharp) | [FONTS](../CoolEngine.Fonts/FONTS.md) §6, §7 |
| **FTL** | The FreeType Project License: the files that follow FreeType closely name the source, and the docs carry its credit line | [FONTS](../CoolEngine.Fonts/FONTS.md) §7 |
| **Fused multiply-add (FMA)** | One instruction computing `a * b + c` with a single rounding; hardware-dependent, so results differ from separate multiply and add. The own math uses none | [MATHEMATICS §3](../CoolEngine.Mathematics/MATHEMATICS.md#3-the-same-bits-promise) |
| **fvar, avar, gvar, cvar, HVAR, VVAR, MVAR** | The variable-font tables: the axes and named instances (`fvar`), the axis value remap (`avar`), the per-glyph point deltas (`gvar`), the `cvt` deltas (`cvar`), the advance deltas (`HVAR`, `VVAR`) and the font-wide metric deltas (`MVAR`) | `CoolEngine.Fonts.Variations.*` |
| **GL thread** | The main thread, which owns the OpenGL context | [ARCHITECTURE §2](../ARCHITECTURE.md#2-threads) |
| **glyf, loca** | The TrueType outline table and the offset table that locates each glyph in it | `CoolEngine.Fonts.Sfnt.GlyfTable` |
| **Half-extents** | Half the size of a body along its own axes (a sphere's three are equal, its radius); the one size form of the physics library and, by Denys's rule of 2026-11-28, of every layer. Today's game stores the full size (`Size`) and the transform scales by `Size / 2` | `CoolEngine.Physics.BodyDescription.HalfExtents`, [UNITS-STANDARDIZATION](../plans/UNITS-STANDARDIZATION.md) |
| **Heading** | The rotation of a body about the up axis Y that decides which way "forward" points. Today it is `WorldObject.Pitch`, applied negated; Denys's rule of 2026-11-28 names it Yaw, positive turning left | [UNITS-STANDARDIZATION](../plans/UNITS-STANDARDIZATION.md) |
| **Hinting** | Moving an outline's points at one pixel size so that its stems and heights fall on the pixel grid; `HintingMode.Native` runs the font's own instructions (TrueType) or hints (CFF) | `CoolEngine.Fonts.HintingMode` |
| **Instanced rendering** | Drawing many copies of a mesh in one call with per-copy data in a buffer; used from the 17th copy with one texture | `ObjectRenderer<T>` |
| **Golden value** | A result recorded from the old code before a refactor and written into a test as literal numbers, so the new code must reproduce it within a tolerance (`1e-5 × max(1, abs(expected))` per element) | `CoolEngine.Tests.TestSupport.GoldenAssert`, `OldTanks.Tests.Models.WorldObjectTests` |
| **Golden hash** | The recorded hash of every result bit of the math library's fixed loop; `CrossPathTests` must reproduce it under four instruction-set paths, and it is re-recorded only after a .NET update once the four paths agree | `CoolEngine.Mathematics.Tests.CrossPathTests` |
| **Inverse-transpose** | The matrix that moves a normal so that it stays perpendicular to its face under uneven scale; the engine uses the cofactor matrix of the linear part (inverse-transpose times determinant) | `Collision.UpdateCollision` |
| **MCU** | Minimum coded unit: the group of 8×8 blocks (all components) that a JPEG scan codes together; sets the restart-interval step | `CoolEngine.Imaging.Jpeg.JpegDecoder` |
| **Mesh** | Vertices, texture coordinates, normals and faces of one part of a model | `CoolEngine.GraphicalEngine.Core.Mesh` |
| **OBB** | Oriented bounding box; the code's name for the polygon tests | `Collision.OBBCollisionCheck` |
| **OBJ** | Wavefront text model format | `CoolEngine.Services.Loaders.ModelsLoaders.WaveFrontLoader` |
| **OFL** | The SIL Open Font License 1.1, the licence of the Source Sans 3 test fonts | [FONTS](../CoolEngine.Fonts/FONTS.md) §7 |
| **On ground** | A body resting on something along gravity; allows a jump | `RigidBody.OnGround` |
| **OTS** | The OpenType Sanitiser that Chrome and Firefox run on every web font; the dividing line the strict rules were measured against (Denys's "Safety-strict") | [FONTS](../CoolEngine.Fonts/FONTS.md) §3 |
| **ppem** | Pixels per em: the size a font is scaled to; `ScaledFontOptions.PixelSize` | `CoolEngine.Fonts.ScaledFontOptions` |
| **Progressive JPEG** | A JPEG coded in several scans that refine the picture (DC first, then AC bands, then refinement bits); `Brick.jpg` is the one in the assets | `CoolEngine.Imaging.Jpeg.JpegDecoder` |
| **Paused frame** | While paused, the tick loop still calls the handler once per period to apply commands and publish, without stepping | `CoolEngine.Core.Hosting.ITickHandler.PausedFrame`; [CORE §5](../CoolEngine.Core/CORE.md#5-the-runner-pause-step-and-faults) |
| **Phase call** | One of the public calls of the physics step (`IntegrateVelocities`, `ClampTerminalSpeed`, `IntegratePositions`, `ResolveContacts`, with `ClearForces` and `AbortStep`) that a caller makes in order to run its own rules between them; the order is checked at run time | `CoolEngine.Physics.PhysicsWorld`; [PHYSICS-LIBRARY §3](../CoolEngine.Physics/PHYSICS-LIBRARY.md#3-the-step-public-phase-calls) |
| **Picking ray** | The ray cast from the free camera each physics step; its nearest hit is drawn | `CoolEngine.Models.Ray` |
| **Player** | The world object the player camera follows; never set today | `OldTanks.Models.World.Player` |
| **Render target** | An offscreen framebuffer (RGBA8 colour, 32-bit float depth) the 3D scene is drawn into before its colour is blitted to the window | `CoolEngine.GraphicalEngine.Core.RenderTarget` |
| **Restart interval** | A JPEG's `DRI` setting: after every n MCUs the bit stream resets and an `RSTm` marker follows | `CoolEngine.Imaging.Jpeg.JpegDecoder` |
| **Reversed Z** | Depth mapped so that the near plane is 1 and the far plane 0, for better float precision; built into the own math's projections (OT-5), not yet used by the renderer | [MATHEMATICS §1](../CoolEngine.Mathematics/MATHEMATICS.md#1-conventions), [plan D7](../plans/ENGINES-AND-TANK-GAME.md#5-open-decisions) |
| **Rigid body** | Velocity, forces, limits and flags of a physical object | `CoolEngine.PhysicEngine.Core.RigidBody` |
| **SAT / separating axis** | Separating-axis test: two convex shapes miss if their projections on some axis do not overlap | `Collision` |
| **Same-bits promise (path-stable)** | The own math's D6 guarantee: members that use only + − × ÷, sqrt and exact operations give the same bits on every x64 CPU for finite inputs; trig members follow the OS math library and a NaN's sign is not covered | [MATHEMATICS §3](../CoolEngine.Mathematics/MATHEMATICS.md#3-the-same-bits-promise) |
| **Scene** | All meshes of one loaded model | `CoolEngine.GraphicalEngine.Core.Scene` |
| **sfnt** | The container of a TrueType or OpenType file: an offset table, a table directory and the tables; a collection holds several faces in one file | `CoolEngine.Fonts.Sfnt.SfntReader` |
| **Side-by-side test** | A test that runs today's code and its replacement in one process on the same input and compares every result bit for bit; it proves an exact port | `OldTanks.Tests.PhysicsParity`; [PHYSICS-LIBRARY §7](../CoolEngine.Physics/PHYSICS-LIBRARY.md#7-how-it-is-proved) |
| **Skybox** | A cube-map texture drawn behind everything | `CoolEngine.GraphicalEngine.Core.SkyBox` |
| **Snapshot (simulation)** | A per-tick copy of what the GL thread needs from the simulation (transforms, flags, statistics), so that no live object is shared between threads; the GL thread draws the newest one. The buffer that carries it exists (`CoolEngine.Core.Channels.SnapshotBuffer<T>`); the snapshot content and its use are planned (OT-21) | [SIMULATION-AND-PHYSICS-SPLIT §3](../plans/SIMULATION-AND-PHYSICS-SPLIT.md#3-target-shape) |
| **Triple buffer** | Three instances passed between one writer and one reader by single atomic exchanges, so neither waits and the reader always gets the newest complete one | `CoolEngine.Core.Channels.SnapshotBuffer<T>`; [CORE §4](../CoolEngine.Core/CORE.md#4-the-snapshot-buffer) |
| **Stem hints** | CFF `hstem` and `vstem` operators that mark the edges of a stem so the hinter can snap them | `CoolEngine.Fonts.Hinting.Cff.CffHintMap` |
| **Strict decoding** | A decoder that throws `InvalidDataException` on any nonconforming or damaged data instead of concealing it | [IMAGE-DECODERS §3](../CoolEngine.Imaging/IMAGE-DECODERS.md#3-the-strict-rules) |
| **Static body** | A body that never moves (`IsStatic`); static-static pairs are not tested | `RigidBody.IsStatic` |
| **Step mode** | Physics paused (`U`), advanced one step at a time (`I`) | `World.IsActive`, `World.Stop` |
| **Sub-iteration** | One of the `CollisionIterations` slices a physics step is cut into | `CoolEngine.Services.EngineSettings.CollisionIterations` |
| **Textured object info** | For one drawable: which `TextureData` each mesh uses | `CoolEngine.GraphicalEngine.Core.Texture.TexturedObjectInfo` |
| **TrueType interpreter v35 and v40** | The two behaviours of FreeType's bytecode interpreter: v35, the classic one, and v40, which follows ClearType's backward-compatibility rules (the default here) | `CoolEngine.Fonts.TrueTypeInterpreterVersion` |
| **TTC, OTC** | A TrueType or OpenType font collection (`ttcf`): several faces sharing tables in one file | `CoolEngine.Fonts.FontFile.Faces` |
| **VAO, VBO, EBO** | OpenGL vertex array, vertex buffer and index buffer objects | `CoolEngine.GraphicalEngine.Core.DrawObjectInfo` |
| **Variable font** | A font with axes (weight, width, ...) and named instances; `ScaledFontOptions.Variation` picks coordinates | `CoolEngine.Fonts.FontFace.Axes` |
| **WOFF, WOFF2** | Web font containers: WOFF 1.0 compresses each table with zlib, WOFF2 compresses all with Brotli and transforms `glyf`, `loca` and `hmtx`; both decode to an sfnt image | `CoolEngine.Fonts.Woff.WoffDecoder`, `Woff2Decoder` |
| **World object** | The game's base object: drawable, physical, can carry a camera | `OldTanks.Models.WorldObject` |
| **World-space velocity** | A velocity (and force) expressed in world axes, in metres per second and newtons. The physics library holds only this; today's game keeps the velocity local to the body (X forward) and the drive converts at the boundary | `CoolEngine.Physics.PhysicsWorld.GetVelocity`, `OldTanks.Tests.PhysicsParity.WorldSpaceDrive` |
| **World-space velocity** | A velocity (and force) expressed in world axes, in metres per second and newtons. The physics library holds only this; today's game keeps the velocity local to the body (X forward) and the drive converts at the boundary | `CoolEngine.Physics.PhysicsWorld.GetVelocity`, `OldTanks.Tests.PhysicsParity.WorldSpaceDrive` |

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
| `MathHelper` | `OpenTK.Mathematics.MathHelper` (banned since OT-5), `Common.Helpers.MathHelper` (quadratic solver) and `CoolEngine.Mathematics.MathHelper` (the one the engine and game use) |
| `Matrix2`, `Matrix3`, `Matrix4` | `CoolEngine.Mathematics.*` (column vectors, column-major; used everywhere since OT-5) and the banned `OpenTK.Mathematics.*` (row vectors). `Vector3` and `Quaternion` are `System.Numerics`' everywhere in engine and game code; OpenTK's of the same names are banned |
| `ArrayExtensions`, `PrimitiveExtensions` | One each in `Common.Extensions` and `CoolEngine.Services.Extensions` |
| `Rotation` | Euler angles on `WorldObject`, in radians everywhere since OT-14 (the debug window shows degrees; [KI-002](../KNOWN-ISSUES.md#ki-002-worldobjectrotation-is-read-as-radians-in-one-place-and-degrees-in-another)), a unit look direction on `Camera` |
| "Yaw", "Pitch", "Roll" | Today, on `WorldObject`, rotation about X, Y and Z, in that order; not the usual aviation meaning. **Decided 2026-11-28 for new code and the later conversion (Denys, "Standard meaning"):** Yaw (heading) about the up axis Y, Pitch about the right axis Z, Roll about the forward axis X, positive turning left by the right-hand rule ([UNITS-STANDARDIZATION](../plans/UNITS-STANDARDIZATION.md)) |
