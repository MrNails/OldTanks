# Units Standardization

| Field        | Value  |
|--------------|--------|
| Status       | Active plan - the rules decided 2026-11-28; OT-21's new code follows them, converting the old places is a task of its own after OT-21, no number yet |
| Last updated | 2026-11-28 |
| Verified against | 2026-11-28; feature/OT-20 at 3c1b624 (the inventory of old conventions against the code of `master` at 7f33680, read from the files by the documenter and the mentor: reasoned from code, not run; Denys's answers are the mentor's record of the chat) |

---

## 1. Goal

One unit rule for rotations, velocity and the rest, so that new code in the simulation split ([SIMULATION-AND-PHYSICS-SPLIT](SIMULATION-AND-PHYSICS-SPLIT.md)) does not add a third convention, and the old places can later be converted one by one.

Denys, 2026-11-28, while OT-20 was running: "After this improvements, we need to standartize our units (rotations, velocity, e.t.c.) Currently, if I understand correctly, we have rotation in two units (radians and degrees in some plcaes)". To "Should the unit rules be decided before OT-21 starts, so its new code follows them?" he answered "Yes, rules first" (turned down: after OT-21).

**Facts under the questions:** forward is +X, up is +Y, right is +Z, right-handed.

## 2. The eight rules (Denys, 2026-11-28)

Each was asked with a recommendation. The answers are his words as recorded.

| Rule | His answer | What it means | Turned down |
|------|------------|---------------|-------------|
| Angles | "Radians in code (Recommended)" | Every angle in code, snapshots and commands in radians; degrees only where a person reads or types a value (debug window, settings file), converted once at that boundary | Degrees in game code; keep both, documented |
| Angle names | "Standard meaning (Recommended)" | Yaw (heading) about the up axis Y, Pitch about the right axis Z, Roll about the forward axis X; members and debug-window labels renamed, stored values keep their effect | Axis names only; keep today's names |
| Turn sign | "Right-hand rule (Recommended)" | Positive turns left, as the math and physics libraries, no hidden negation; stored values negated once in the conversion so nothing moves differently; the debug window's heading changes sign | Positive turns right, as today |
| Orientation in game objects | "Quaternion (Recommended)" | The same value the physics body has; the debug window shows and edits angles derived from it | Three angles |
| Velocity and force | "World space (Recommended)" | World-space vectors everywhere, metres per second and newtons; the drive offers the forward speed as a derived number, shown beside the world velocity | Local in the game |
| Size | "Half-extents everywhere" (not the recommendation) | One form in every layer; the debug window shows half sizes and every scene definition is halved | Full size in the game with half-extents only inside the library (the recommendation) |
| Units and rates | "SI, per second (Recommended)" | Metres, seconds, kilograms, newtons, radians; every rate per second; mass named Mass; rates that need a number (the A/D turn rate) come from his specification | SI with rates left as they are |
| Unit marking | "Standard unmarked, rest suffixed (Recommended)" | A value in the standard unit carries no mark and its XML comment states the unit; any other unit exists only at a boundary and is named, such as `fovDegrees`; no new types | A typed Angle; a suffix on everything |

## 3. Scope

| Part | Decision |
|------|----------|
| OT-21's new code (commands, snapshot, drive, the camera's GL-thread side) | Follows the rules from the start |
| Converting the old places | A task of its own after OT-21, **no task number yet** ([BACKLOG](../BACKLOG.md#planned)); to be proved by the golden and side-by-side tests as OT-14 was |
| `CoolEngine.Physics` | Already follows most rules: a unit quaternion, half-extents, world-space velocity and force, `Mass` ([PHYSICS-LIBRARY](../CoolEngine.Physics/PHYSICS-LIBRARY.md)) |

The one-line form of the rules for new code is in [CODE-STYLE](../CODE-STYLE.md#units).

## 4. Where the old conventions remain (read from the code on `master` at `7f33680`, 2026-11-28)

| Kind | Where |
|------|-------|
| Radians | `OldTanks.Models.WorldObject.Rotation` (since OT-14); `CoolEngine.Physics` stores a quaternion |
| Degrees | `CoolEngine.GraphicalEngine.Core.Camera.Rotate` (mouse deltas and its internal pitch, clamped to 80) and `Camera.FOV` (converted in `OldTanks.Windows.WindowMetrics.CreatePerspective`); `CoolEngine.GraphicalEngine.Core.Texture.TextureData.RotationAngle` (converted in `ObjectRenderer<T>`); the text rotations of `TextRenderer` and `CoolEngine.Services.Misc.TextDrawInformation` (`SelfRotation`, `OriginRotation`); `WorldObject.CameraOffsetAngle`; the debug window shows degrees and converts in `OldTanks.Helpers.UIVectorHelper` |
| Misleading names | `Camera`'s default constructor passes `DegreesToRadians(90)` into a "rotation" that is a direction vector and gets normalized; `Camera.Rotation` is a direction, not angles; `WorldObject.Yaw` is the rotation about X, `Pitch` about Y (the heading, applied negated) and `Roll` about Z |
| Other mixed conventions | Velocity local to the body in today's game (X forward) against world space in the library; the free camera's `Velocity` is three per-axis speeds; `Size` is the full size in the game against half-extents in the library; mass is called `Weight` on `RigidBody`; some rates are per physics loop, not per second (the mouse-look edge zones, the player's A/D turn, [KI-001](../KNOWN-ISSUES.md#ki-001-the-player-is-never-assigned)) |

## 5. Open

- The task number and plan of the conversion (after OT-21); the A/D turn rate and the mouse-look rates per second come from Denys's specification, the latter tied to Q11 of the split plan.
- Denys's wording "Standard meaning" for yaw, pitch and roll differs from today's names; the [glossary](../knowledge/GLOSSARY.md#name-clashes) keeps both, dated.
