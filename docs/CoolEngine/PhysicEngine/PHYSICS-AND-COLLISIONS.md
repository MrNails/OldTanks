# Physics and Collisions

| Field        | Value  |
|--------------|--------|
| Status       | Living |
| Last updated | 2026-11-24 |
| Verified against | 2026-11-24; develop at d32cbbf; whole page |

---

The engine side lives in `CoolEngine/PhysicEngine/`: the rigid body, the collision shapes and the detection tests. The step loop that moves objects and resolves contacts is game code, `OldTanks.Models.World.CollideObjects`, and is described here too because nothing else owns it. Threads and locks: [ARCHITECTURE §2](../../ARCHITECTURE.md#2-threads).

## 1. Types

| Type | Role |
|------|------|
| `CoolEngine.PhysicEngine.Core.RigidBody` | Velocity, force, weight, speed limits, `OnGround`, `IsStatic`; integrates one tick in `OnTick` |
| `CoolEngine.PhysicEngine.Core.Collision.CollisionData` | A shape: `CollisionType` (`Polygon` or `Sphere`), vertices, faces with normals, the axis-aligned `BoundingBox`, and the owning `IPhysicObject` |
| `CoolEngine.PhysicEngine.Core.Collision.Collision` | One object's live collision: a model-space original `CollisionData` plus a transformed copy; runs the tests |
| `CoolEngine.PhysicEngine.Core.Face` | A collision face: vertex indices and a normal. Not the render `CoolEngine.GraphicalEngine.Core.Face` |
| `CoolEngine.PhysicEngine.PhysicsConstants` | Gravity 9.80665 along -Y, terminal fall speed 9 × g, `G` |
| `CoolEngine.Models.Ray` | Start and end points for picking |
| `CoolEngine.Services.Interfaces.IPhysicObject` | What a body exposes: position, rotation, size, `RigidBody`, `Collision`, `ApplyTransformation` |

Unused today: `CoolEngine.PhysicEngine.Core.WorldSettings` and `CoolEngine.PhysicEngine.Physics.GetFreeFallingAcceleration` (planet-based gravity), and `CoolEngine.Models.Dto.CollisionIntersectionResult`.

## 2. The rigid body

- **Velocity is local to the object.** `OldTanks.Models.WorldObject.Move` rotates it about Y by the object's `Rotation.Y` before adding it to the position, so `X` is "forward".
- **Forward speed is clamped:** `Velocity.X` stays within `-MaxBackSpeed .. MaxSpeed × MaxSpeedMultiplier`. All three default to 0, so a dynamic body that never sets them cannot move forward or back (the default scene sets 2, 2 and 1). Y and Z are not clamped, except the fall speed (below).
- **One tick** (`OnTick`): damp forward speed at a rate of 0.6 × speed per second (about 45 % lost per second; snap to 0 below 0.01); add `(Force / Weight + gravity) × dt`; if the speed along the gravity axis exceeds 9 × g in either direction, set it to falling at 9 × g (so a very fast upward speed is flipped); clear `Force` on the last collision sub-iteration, or every tick when called with `-1`.
- **Static bodies** ignore velocity and force writes and are skipped by `Move`.
- **Stored but not used by the solver:** `Restitution`, `Density` (clamped 0.5-21.4) and `CenterOfMass`. `DefaultJumpForce` is used only by the unreachable player controls ([KI-001](../../KNOWN-ISSUES.md#ki-001-the-player-is-never-assigned)).

## 3. The step loop (`World.CollideObjects`)

Called by the physics thread about every 5 ms with the elapsed time.

1. **Paused?** If `World.IsActive` and `World.Stop` are both true, only re-apply transforms and return. Key `U` toggles `IsActive` (step mode); key `I` clears `Stop` for one step, after which `Stop` is set again once `AmountOfFrames` (1) steps have run.
2. **Split** the elapsed time into `EngineSettings.Current.CollisionIterations` sub-steps (the game sets 5).
3. **Each sub-step:** for every object, `Move` (tick and position update), clear `OnGround` on dynamic bodies, `ApplyTransformation` (rebuild the matrix, transform the collision, refresh its bounding box). Then test **every pair** once (O(n²)), skipping static-static pairs.
4. **On contact:** push the bodies apart along the contact normal by the depth: all of it to the dynamic body, or half to each when both are dynamic. Set `OnGround` from the sign of (direction between the bodies · gravity). For a body on the ground, cancel the vertical part of the pair's relative velocity (`ResolveColliding`). Re-apply both transforms.

There is no bounce, friction between bodies, rotation response or sleeping; resolution is position correction plus vertical velocity cancelling.

## 4. Detection

`Collision.CheckCollision(other, out normal, out depth)` runs an AABB test first, then the narrow test for the pair:

| Pair | Test |
|------|------|
| Polygon × polygon | Separating-axis test over **each shape's face normals** (no edge × edge axes, [KI-016](../../KNOWN-ISSUES.md#ki-016-polygon-tests-use-face-normals-only)). The axis of least overlap gives normal and depth; the normal is flipped to point from the first body to the second |
| Polygon × sphere | Separating-axis test over the polygon's face normals with the sphere projected as centre ± radius; on a tie, prefer the face that contains the polygon vertex closest to the sphere |
| Sphere × sphere | Centre distance ≤ sum of radii |

`Collision.IntersectRay(ray, out point)`: for a polygon, each face's plane is hit-tested and the hit is accepted if its projections on the edges from face vertex 0 to vertices 1 and 2 fall within those edges (a correct inside test only for faces with perpendicular edges, such as the cube's); the **first** face in list order that passes wins, not the nearest. For a sphere, the quadratic is solved. The game casts a ray from 1 unit below the free camera to 100 units ahead each physics step and draws the nearest hit ([features §2](../../OldTanks/FEATURES-OVERVIEW.md#2-the-default-scene)).

## 5. Shapes

- **Polygon shapes are model-space** and span -1..1 on each axis; the object's transform scales them by `Size / 2`, rotates and translates them, and `Collision.UpdateCollision` rotates the face normals (scale and translation cleared).
- **Face vertex order matters** for the ray test: indices 0, 1 and 2 must be one corner and its two neighbours. `OldTanks.Models.Cube`'s static constructor shows the expected layout (8 vertices, 6 quads, outward normals).
- **Spheres have no vertices.** Their radius is `PhysicObject.Width / 2` and their box is position ± radius; `OldTanks.Models.Sphere` keeps width, height and length equal.
- **Shared originals:** each shape is registered once in `GlobalCache<CollisionData>` under `OldTanks.Infrastructure.CollisionConstants` names (`CubeCollision`, `SphereCollision`) by the owning class's **static constructor**, and every `Collision` copies it. Construct one instance of the class before fetching its shape.
- **Every world object needs a `Collision`:** `CollideObjects` dereferences it without a null check (the ray loop does check).

## 6. Debug views

| Key | View |
|-----|------|
| `0` | Replace textured rendering with collision wireframes, vertex coordinates as 3D text and face normals (`CoolEngine.Services.Renderers.CollisionRenderer`) |
| `N` | Draw mesh normals as red lines |
| `U`, `I` | Step mode and single step (§3) |

The HUD's "Collision check per second" is the physics thread's loop rate and "Collision subiteration amount" is `CollisionIterations`.
