# OldTanks Features Overview

| Field        | Value  |
|--------------|--------|
| Status       | Living |
| Last updated | 2026-11-24 |
| Verified against | 2026-11-24; develop at d32cbbf; whole page (read from code, not played) |

---

What the game application (`OldTanks/`) does today: the window, the default scene, the controls, the HUD and the debug windows. It is an engine sandbox; there is no tank, player or game rule yet ([PROJECT-KNOWLEDGE §2](../PROJECT-KNOWLEDGE.md#2-current-state)). Threads and the frame: [ARCHITECTURE](../ARCHITECTURE.md).

## 1. Main pieces

| Type | File | Role |
|------|------|------|
| `OldTanks.Program` | `OldTanks/Program.cs` | Logger, `Application`, settings, window |
| `OldTanks.Windows.MainWindow` | `OldTanks/Windows/MainWindow.cs` (+ HUD in `Windows/ControlWindows/MainWindow.cs`) | The OpenTK `GameWindow`: loading, input, physics thread, frame |
| `OldTanks.Services.GameManager` | `OldTanks/Services/GameManager.cs` | Asset loading and the `Textures` name list ([ASSET-PIPELINE](../CoolEngine/Services/Loaders/ASSET-PIPELINE.md)) |
| `OldTanks.Models.World` | `OldTanks/Models/World.cs` | Objects, camera, skybox, `Player`, the physics step ([PHYSICS §3](../CoolEngine/PhysicEngine/PHYSICS-AND-COLLISIONS.md#3-the-step-loop-worldcollideobjects)) |
| `OldTanks.Models.WorldObject` | `OldTanks/Models/WorldObject.cs` | Base of every object: `IDrawable`, `IPhysicObject`, `IWatchable` |
| `Cube`, `Sphere`, `Robot` | `OldTanks/Models/` | Concrete objects; `Robot` cannot be built ([KI-007](../KNOWN-ISSUES.md#ki-007-robot-cannot-be-constructed)) |
| `OldTanks.UI.ImGuiUI.MainWindow`, `TextureWindow` | `OldTanks/UI/ImGuiUI/` | The two ImGui debug windows |
| `OldTanks.DataModels.Settings` | `OldTanks/DataModels/Settings.cs` | The user settings file's model ([BUILD §4](../BUILD.md#4-files-the-game-reads-and-writes)) |

## 2. The default scene

Built by `MainWindow.InitDefaultObjects` once all assets are loaded.

| Object | Size | Position | Body | Texture |
|--------|------|----------|------|---------|
| Wall 1 (cube) | 10 × 2 × 5 | (0, 0, -10) | Static | `wall-texture` |
| Wall 2 (cube) | 1 × 3 × 20 | (9, 0, 0) | Static | `wall-texture` |
| Floor 1 (cube) | 20 × 1 × 20 | (0, -2, 0) | Static | `FloorTile` |
| Sphere | 1 | (0, 5, 0) | Static | `Brick` |
| Dynamic Cube 1 | 1 | (0, 2, 1.5), tilted 45° about X | Dynamic | `wall-texture` |
| Sphere | 1 | (0, 5, 0) | Dynamic | `wall-texture` |

Dynamic bodies get max speed 2, back speed 2, multiplier 1, jump force -250; restitution is set (0.5 static, 0.4 dynamic) but unused. The skybox is `SkyBox2`, the camera FOV 45°, and named objects show their name in red above them. Each physics step casts a ray from 1 unit below the free camera to 100 units ahead; the red line and point show the nearest hit.

## 3. Controls

| Input | Effect |
|-------|--------|
| Hold any mouse button and move | Look around (sensitivity from settings); with a button held, a cursor near the window edge keeps turning the view ([KI-014](../KNOWN-ISSUES.md#ki-014-mouse-look-ignores-imgui-focus)) |
| Mouse wheel | Zoom (FOV 20-90°) |
| `W` `A` `S` `D` | Free camera: forward, left, back, right |
| `Space`, `Left Shift` | Free camera: up, down |
| `Left Ctrl` (hold) | Free camera 3× speed |
| `0` | Toggle the collision debug view |
| `N` | Toggle mesh normals |
| `U` | Toggle physics step mode (pause) |
| `I` | In step mode, advance one physics step |
| `R` | Move the camera back to the origin |
| `Alt` + `Enter` | Toggle full screen |
| `Esc` | Quit |
| `1` | Switch between free camera and player camera; does nothing while no player is set ([KI-001](../KNOWN-ISSUES.md#ki-001-the-player-is-never-assigned)) |
| `F`, `O` | Toggle flags nothing reads ([BACKLOG](../BACKLOG.md#unwired-features)) |

**Player controls** (written, unreachable until `World.Player` is set): `W`/`S` push along +X of the object, `A`/`D` turn, `Space` jumps when on the ground, `Q`/`E`/`Z`/`X` move the follow camera, `Left Ctrl` raises the speed multiplier to 3.

## 4. HUD

Top-left screen text (`OldTanks.UI.Controls.TextBlock`, Arial 16): FPS; collision sub-iteration count; collision checks per second (the physics loop rate); position and rotation of the current object (the camera, whose "rotation" is its look direction); "Default cube rotation" (a field only `R` resets). "Have collision" and "Speed" are never filled in.

## 5. Debug windows

**Debug window** (`OldTanks.UI.ImGuiUI.MainWindow`, open at start):

| Control | Effect |
|---------|--------|
| Show texture window | Opens the texture window for the selected object |
| Spawn object | Adds a static 1 × 1 × 1 cube with no texture at the origin |
| Delete selected object | Removes the selected object from the world and the renderers |
| Push force | Not wired |
| CPosition | Camera position, two-way |
| CRotation | Camera look direction, display only |
| Camera free mode | Not wired |
| World objects, Clear selection | Pick the object the panel edits |
| Position, Rotation (degrees), Size, Camera offset, Camera offset angle | Selected object, two-way |
| Rigid body: Is static, Center of mass, Velocity, Jump force, Max speed, Max back speed, Speed multiplier, Weight | Selected object's body, two-way; Max speed, Max back speed and Speed multiplier raise no change notification, so changes made by code show only after the selection changes |

**Texture window** (`OldTanks.UI.ImGuiUI.TextureWindow`): pick one of the object's textured-object infos and one of its meshes, see the current texture, and pick another from the loaded list to retexture that mesh (`TexturedObjectInfo.ChangeTexture`). **Add texture** loads an image from any file path into the cache and the list. The window takes the object selected when it is first opened; later selections reach it through a generated binding.

## 6. Adding a world object type

Follow `OldTanks.Models.Cube`:

1. Put the model under `Assets/Models/` and note its OBJ object name ([ASSET-PIPELINE §2](../CoolEngine/Services/Loaders/ASSET-PIPELINE.md#2-cache-keys)).
2. Derive from `WorldObject`; pass `GlobalCache<Scene>.Default.GetItemOrDefault("<object name>")` to the base constructor (a missing scene throws `CoolEngine.Services.Exceptions.ObjectException`).
3. In a static constructor, build the collision shape (model space -1..1, outward face normals, ray-friendly vertex order) and register it under a new `OldTanks.Infrastructure.CollisionConstants` name ([PHYSICS §5](../CoolEngine/PhysicEngine/PHYSICS-AND-COLLISIONS.md#5-shapes)).
4. When creating an instance (after assets have loaded): set `Size` and `Position`, assign `Collision`, add a `TexturedObjectInfo` covering every mesh, set the `RigidBody` (static, or speeds for a dynamic body), then add it to `World.WorldObjects` ([RENDERING §5](../CoolEngine/Services/Renderers/RENDERING.md#5-adding-something-to-draw)). After startup, also register it with `CollisionRenderer.AddCollision` and `ObjectRendererOld.AddDrawable`, as `OldTanks.UI.ImGuiUI.MainWindow.SpawnObjectOnClick` does; the startup continuation registers only the default scene.
