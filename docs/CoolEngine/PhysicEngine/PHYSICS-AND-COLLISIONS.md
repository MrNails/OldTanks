# Physics and Collisions

| Field        | Value  |
|--------------|--------|
| Status       | Living |
| Last updated | 2026-11-27 |
| Verified against | 2026-11-27; bug/OT-14 at da7db97; §2's `Move` bullet against the code and its behaviour test (reasoned from code and tested, not seen in the game); 2026-11-25; bug/OT-13 rebased on master at 8ded8f4; §4 and §5 against `Collision` and its 29 tests (`dotnet test`: 67 in `CoolEngine.Tests`), the thread notes reasoned from code; the other sections as of 2026-11-24 (develop at d32cbbf) |

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

- **Velocity is local to the object.** `OldTanks.Models.WorldObject.Move` rotates it about Y by the object's `Rotation.Y` (the heading, in radians, as the object is stored and drawn; [KI-002](../../KNOWN-ISSUES.md#ki-002-worldobjectrotation-is-read-as-radians-in-one-place-and-degrees-in-another), closed on `bug/OT-14`, 2026-11-27) before adding it to the position, so `X` is "forward" along the drawn forward axis. Only the heading turns the velocity, so gravity (in the local Y) stays world-down; with `Roll` not 0 the drawn forward axis's ground projection differs a little from the heading (measured 4.1° at heading 45°, roll 30°).
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

`Collision.CheckCollision(other, out normal, out depth)` runs an AABB test first, then the narrow test for the pair. Touching counts as separated in every polygon path (`>=` on projections, `dist >= radius` for the closest point):

| Pair | Test |
|------|------|
| Polygon × polygon | Separating-axis test. First the **face normals** of both shapes (unchanged), then the **edge × edge axes** ([KI-016](../../KNOWN-ISSUES.md#ki-016-polygon-tests-use-face-normals-only), closed in OT-13): the cross product of one edge direction of each shape, taken from the current vertices. Parallel pairs are skipped. The axis of least overlap gives normal and depth; a tie goes to the face axes (an edge axis wins only when strictly smaller). The normal is flipped to point from the first body to the second |
| Polygon × sphere, cuboid | **Closest point:** the sphere centre is clamped into the box along its three current axes. Contact when the distance is below the radius; normal from the box towards the sphere, depth = radius − distance. With the centre inside the box, the face of least penetration gives the normal and depth = radius + penetration. Only while the three current axes are perpendicular (guard below); exact for any cuboid |
| Polygon × sphere, any other convex shape (and a sheared or zero-size cuboid) | **Separating-axis fallback:** the face normals (sphere projected as centre ± radius; on a tie, prefer the face that contains the polygon vertex closest to the sphere), then one axis from every vertex to the centre and one from the closest point of every edge to the centre (skipped when the centre lies on the feature). Exact for convex shapes |
| Sphere × sphere | Centre distance ≤ sum of radii |

- **Edge directions:** one edge per parallel direction, found once per shape (a cube has 3, a tetrahedron 6), so a cube × cube pair tries 9 edge axes. Cost: about 0.42-0.44 µs per cube × cube `CheckCollision` in Release (one `Stopwatch` run on 2026-11-25, not a benchmark); `CheckCollision` and `UpdateCollision` allocate 0 bytes per call (asserted by a test).
- **Normal direction:** polygon × polygon returns a normal from the first body to the second. Of polygon × sphere only the closest-point path and the fallback's vertex and edge axes promise "polygon → sphere". The fallback's face axes use the face's outward normal, which can point away from the sphere (two coincident faces of a zero-width box); `World.CollideObjects` takes the push sign from the centres, so this does no harm.
- **What the face-only test got wrong (measured on 2026-11-25):** a sphere off a box corner reported contact across a gap of up to 0.73 × radius, off an edge up to 0.41 × radius; two unit-half-extent cubes at 45° edge to edge across a 0.1 gap reported depth 0.64.
- **Constants** (private, in `CoolEngine.PhysicEngine.Core.Collision.Collision`): `ParallelEpsilon` 1e-6 (squared cross length of two unit directions; written so that a NaN axis from an edge collapsed to a point is skipped too), `ZeroAxisEpsilon` 1e-12 (squared length of a sphere fallback axis), `CuboidTolerance` 1e-6 (cuboid detection), `CuboidOrthogonalityTolerance` 1e-4 (largest |dot| of two current box axes for the closest-point path; false on NaN).

`Collision.IntersectRay(ray, out point)`: for a polygon, each face's plane is hit-tested and the hit is accepted if its projections on the edges from face vertex 0 to vertices 1 and 2 fall within those edges (a correct inside test only for faces with perpendicular edges, such as the cube's); the **first** face in list order that passes wins, not the nearest. For a sphere, the quadratic is solved. The game casts a ray from 1 unit below the free camera to 100 units ahead each physics step and draws the nearest hit ([features §2](../../OldTanks/FEATURES-OVERVIEW.md#2-the-default-scene)).

## 5. Shapes

- **Polygon shapes are model-space** and span -1..1 on each axis; the object's transform scales them by `Size / 2`, rotates and translates them. `Collision.UpdateCollision` moves the face normals by the **inverse-transpose** of the transform's linear part (computed as the cofactor matrix, negated for a mirror), not by the rotation alone, so they stay perpendicular to their faces under uneven scale for any shape; the cube's normals are unchanged.
- **A polygon needs vertices:** the `Collision` constructor throws `ArgumentException` for a polygon original without any ([KI-022](../../KNOWN-ISSUES.md#ki-022-initcollision-prints-an-error-twice-at-startup), closed in OT-13; before, it printed a console line and became a ghost that never collided). A sphere has none by design and copies nothing, silently.
- **The shape must be convex and its faces must share vertex indices:** the constructor finds the shape's edges once from the face index lists. Two faces that share exactly two vertex indices and are not coplanar share an edge. A shape whose faces duplicate their vertices has no edges, so it gets no edge × edge axes.
- **Cuboid detection** (once, from the model-space shape): 8 vertices that are the 8 corners of an axis-aligned box with nonzero extent, and 6 faces whose normals are the six signed axes. `OldTanks.Models.Cube` is one; a cube split into triangles is not (it takes the fallback and gives the same answers). The closest-point path also needs the three current axes perpendicular: `WorldObject.ApplyTransformation` scales before it rotates (no shear), but `IPhysicObject.Transformation` forbids neither shear nor a zero size.
- **Zero size is not supported.** A box × box test against a box with a zero size component never reports contact: a NaN face normal separates in the face loop (as before OT-13). Sphere tests on such a box take the fallback and are exact.
- **Face vertex order matters** for the ray test: indices 0, 1 and 2 must be one corner and its two neighbours. `OldTanks.Models.Cube`'s static constructor shows the expected layout (8 vertices, 6 quads, outward normals).
- **Spheres have no vertices.** Their radius is `PhysicObject.Width / 2` and their box is position ± radius; `OldTanks.Models.Sphere` keeps width, height and length equal.
- **Shared originals:** each shape is registered once in `GlobalCache<CollisionData>` under `OldTanks.Infrastructure.CollisionConstants` names (`CubeCollision`, `SphereCollision`) by the owning class's **static constructor**, and every `Collision` copies it. Since OT-13 the original is only read (the constructor no longer recomputes its bounding box). Construct one instance of the class before fetching its shape.
- **Threads:** a `Collision` is constructed on a thread-pool thread (`OldTanks.Windows.MainWindow.InitDefaultObjects` runs in the startup continuation) or on the GL thread (the debug window's spawn). `CheckCollision` runs on the physics thread. `UpdateCollision` runs on the physics thread (`World.CollideObjects`, both branches) and, while the collision debug view is on, also on the GL thread ([KI-031](../../KNOWN-ISSUES.md#ki-031-the-collision-debug-view-transforms-objects-on-the-gl-thread-while-the-physics-thread-does); reasoned from code). The per-shape edge and cuboid data are readonly and set in the constructor.
- **Every world object needs a `Collision`:** `CollideObjects` dereferences it without a null check (the ray loop does check).
- **Tests:** `CoolEngine.Tests.PhysicEngine.Core.Collision.CollisionTests` (29 tests; helpers `CoolEngine.Tests.TestSupport.CollisionTestPhysicObject` and `CollisionTestShapes`: cube, triangulated cube, tetrahedron, sphere) cover face, edge-to-edge, corner, edge and inside contacts, the cuboid path against the fallback over 2000 spheres (unsheared and sheared), parallel equals serial over 192 pairs, and zero allocation per call.

## 6. Debug views

| Key | View |
|-----|------|
| `0` | Replace textured rendering with collision wireframes, vertex coordinates as 3D text and face normals (`CoolEngine.Services.Renderers.CollisionRenderer`); it also re-applies the objects' transformations on the GL thread ([KI-031](../../KNOWN-ISSUES.md#ki-031-the-collision-debug-view-transforms-objects-on-the-gl-thread-while-the-physics-thread-does)) |
| `N` | Draw mesh normals as red lines |
| `U`, `I` | Step mode and single step (§3) |

The HUD's "Collision check per second" is the physics thread's loop rate and "Collision subiteration amount" is `CollisionIterations`.
