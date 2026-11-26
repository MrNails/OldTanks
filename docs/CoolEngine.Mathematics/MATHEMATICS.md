# Mathematics

| Field        | Value  |
|--------------|--------|
| Status       | Living |
| Last updated | 2026-11-26 |
| Verified against | 2026-11-26; feature/OT-5 at fb61096; whole page against the library sources and the test and benchmark projects (the benchmark figures are the pinned runs recorded on 2026-11-26, not re-run for this page) |

---

`CoolEngine.Mathematics` is the engine's own math library: column-major float matrices over `System.Numerics`' vectors and quaternion, and double vectors, matrices and a quaternion. Added in OT-5 (library part, 2026-11-25 and 2026-11-26) to replace `OpenTK.Mathematics` ([plan D7](../plans/ENGINES-AND-TANK-GAME.md#5-open-decisions)). **Nothing in the engine or the game uses it yet**; the move is the rest of OT-5 ([§9](#9-what-remains)). No package and no project reference; the guide is [CoolEngine.Mathematics/AGENTS.md](../../CoolEngine.Mathematics/AGENTS.md).

## 1. Conventions

All 2026-11-24 to 2026-11-26, Denys ([plan §6](../plans/ENGINES-AND-TANK-GAME.md#6-decisions-of-ot-3-and-the-planned-tasks), [session log](../sessions/SESSION-LOG.md)).

| Convention | Rule |
|------------|------|
| Vectors | Column vectors: `M * v`. `A * B` applied to `v` applies `B` first. There is no `v * M`, no `Row0`, no `M11` |
| Storage | Column-major: `Matrix4` is `Column0` to `Column3`, 64 bytes, memory `m00 m10 m20 m30 m01 …` |
| Construction | `FromColumns` and `FromRows`; no element constructor (so a ported row-vector constructor call fails to compile instead of transposing silently) |
| Handedness | Right-handed; view space looks down -Z |
| Clip depth | [0, 1] with reversed Z: near plane 1, far plane 0 (`glClipControl`) |
| Angles | Radians everywhere; `MathHelper` converts |
| Float vs double | Float for geometry, physics and rendering; double types for positions that need them (the plan: only if maps grow past about 10 km) |

## 2. The types

| Types | Namespace `CoolEngine.Mathematics` | What they are |
|-------|------------------------------------|---------------|
| `MathHelper` | static | `Pi`, `TwoPi`, `PiOver2`, `PiOver4` (float); `DegreesToRadians` and `RadiansToDegrees` for float and double. Clamp and lerp come from the BCL |
| `Matrix2`, `Matrix3`, `Matrix4` | structs | Float matrices; columns are `System.Numerics.Vector2`, `Vector3`, `Vector4`. `SizeInBytes` is 16, 36 and 64 |
| `Vector2d`, `Vector3d`, `Vector4d`, `Quaterniond` | structs | Double types in System.Numerics' API shape with OpenTK-style names; implicit and explicit conversions to and from the float System.Numerics types |
| `Matrix2d`, `Matrix3d`, `Matrix4d` | structs | The double matrices, the same conventions |

**The float vectors and quaternion are System.Numerics' own types** (Denys, 2026-11-26: "Option 1", after the review measured an own 12-byte `Vector3` at 2.38 ns per dependent-chain step against 0.41 for System.Numerics; the council then agreed, [plan](../plans/ENGINES-AND-TANK-GAME.md#6-decisions-of-ot-3-and-the-planned-tasks)). There is no `Vector3` or `Quaternion` type in this namespace.

| Operation | Done by |
|-----------|---------|
| `Matrix4` transpose, `CreateScale`, `CreateTranslation`, `CreateRotationX/Y/Z`, `CreateFromQuaternion`, `CreateFromAxisAngle`, `LookAt` | Delegated to `System.Numerics.Matrix4x4` on the same bytes (a row-vector matrix of the same transform has the same memory as the column-vector one) |
| `Matrix4` multiply, `Matrix4 * Vector4`, `TransformPosition`, `TransformDirection`, `Determinant`, `TryInvert`, `Inverted`, `ClearRotation` | Own code on `Vector128<float>`, no fused multiply-add, a fixed order of sums |
| `Matrix4` perspective (`CreatePerspectiveFieldOfView`, `CreatePerspectiveFieldOfViewInfinite`) and `CreateOrthographicOffCenter` | Own formulas (reversed Z); the perspective ones call `MathF.Tan` |
| `Matrix2`, `Matrix3` | Own code (`Matrix3.CreateFromAxisAngle` and `CreateFromQuaternion` have their own formulas) |
| The double types | Own scalar code; no `System.Numerics` call except the conversions |

The delegated members are fast and bit-for-bit the same on every CPU, except where the delegate uses sin, cos or tan ([§3](#3-the-same-bits-promise)). Multiply, `M * v` and inverse are own code because System.Numerics' versions give different bits with and without FMA hardware (the inverse three different results).

## 3. The same-bits promise

Denys, 2026-11-26: D6 "Same bits on x64 (Recommended)" (turned down: "Keep fastest"); after review B, "Exclude trig (Recommended)" (turned down: "Own trig too"). The 2026-11-25 answer "Fastest, no promise" is superseded.

| Part | Rule |
|------|------|
| Promise | Every member that uses only + − × ÷, sqrt and exact operations gives **the same bits on every x64 CPU** for **finite inputs**, whatever instruction set runs it |
| How | No fused multiply-add anywhere; every sum in a fixed order; portable `Vector128` and `Vector256` only (no `Sse`, `Avx`, `AdvSimd`, no `unsafe`) |
| Not covered: trig | Members that call `sin`, `cos`, `tan` or `acos` (the rotation factories, the axis-angle factories, perspective, `Quaterniond.CreateFromAxisAngle`, `Quaterniond.Slerp`) follow the OS math library. Windows' C runtime gives different results for them on CPUs with and without FMA3 (found by review B, 2026-11-26) |
| Not covered: NaN | A NaN's sign and payload are not covered |
| Not covered: other architectures | ARM is not tested; the promise is for x64 |

Every member's doc comment says when it calls trig. Proof: [§6](#6-how-it-is-tested).

## 4. Edge behaviour

| Case | Result |
|------|--------|
| `TryInvert` (all matrices) when the determinant, or `1 / det`, is not finite (zero, tiny subnormal, overflow, NaN element) | Returns `false`, the result is all NaN. Denys, 2026-11-26: "Return false (Recommended)" (turned down: keep `det == 0`) |
| `Inverted` in the same case | Throws `InvalidOperationException` |
| An inverse whose determinant and reciprocal are finite but whose elements overflow | Returns `true`; an element can be infinity |
| `Normalize` of a zero vector | NaN (System.Numerics' behaviour for the float vectors; the same in the double types) |
| `Matrix3`, `Matrix4`, `Matrix3d`, `Matrix4d` `CreateFromAxisAngle` | Divides the axis by its largest component, then normalizes: every finite nonzero axis rotates about its direction, however tiny or huge; an exactly zero axis gives `Identity`; a NaN or infinite component throws `ArgumentOutOfRangeException`. `Quaterniond.CreateFromAxisAngle` expects a unit axis and does not normalize, like System.Numerics' |
| Perspective and orthographic factories | Reject non-finite arguments and an invalid range by throwing (`ArgumentOutOfRangeException` for the perspective ones, `ArgumentException` for the orthographic one) |
| Perspective depth | Near maps to 1, far to 0; the infinite form (`CreatePerspectiveFieldOfViewInfinite`) maps far toward 0 |
| `ClearRotation` | Removes the rotation and keeps each axis's length, as OpenTK's does (the plan said unit axes; that was a plan error, the one caller passes scaled transforms) |
| `LookAt` | Unchecked: `eye == target`, or `up` parallel to the view direction, gives NaN |
| Double `Normalize` | Multiplies by the reciprocal length: 3.1× faster than a division per component, equal to OpenTK's, at most 1 ulp from a true division |

## 5. The OpenGL memory layout

System.Numerics vectors and the column-major matrices have exactly the bytes OpenGL takes **with `transpose = false`**, so no conversion type is needed (Denys asked on 2026-11-26 what this means for the OpenGL API; the answer is recorded in the [session log](../sessions/SESSION-LOG.md)). `Matrix4` uploads to a GLSL `mat4`, `Matrix2` to a `mat2` and `Matrix3` to a `mat3` through `glUniformMatrix*fv`; a std140 buffer pads each `mat3` column to 16 bytes, so a `Matrix3` does not copy into one as is (reasoned from the GL specification, not run). During the move, uniforms go through OpenTK's pointer or `ref float` overloads; own bindings (`CoolEngine.OpenGL`) come after OT-5 ([plan §6](../plans/ENGINES-AND-TANK-GAME.md#6-decisions-of-ot-3-and-the-planned-tasks)). `System.Numerics.Matrix4x4`, `Matrix3x2` and `Plane` are row-vector types: the analyzer in [BUILD §2](../BUILD.md#2-build) and [CODE-STYLE](../CODE-STYLE.md#math-types) keeps them out of engine and game code.

## 6. How it is tested

`tests/CoolEngine.Mathematics.Tests` (NUnit, `net10.0`, 433 tests, headless; OpenTK 4.9.4 is referenced there as an oracle only).

| Check | What it proves |
|-------|----------------|
| Oracles | Every member against System.Numerics (float) and OpenTK (float and double), on identical bytes: our column-major matrix is their row-major one, so their `A * B` is our `B * A` (`TestSupport/Oracle.cs`) |
| Inverse inputs | Seeded matrices with condition number κ1 ≤ 1e3, so a tolerance means something |
| `CoolEngine.Mathematics.Tests.CrossPathTests` | A fixed loop runs every trig-free member on seeded inputs and hashes the result bits; the hash must equal a golden. Run under the four paths: default, `DOTNET_EnableAVX2=0` (no FMA), `DOTNET_EnableAVX=0`, `DOTNET_EnableHWIntrinsic=0` (software), each in Debug and Release |
| `CoolEngine.Mathematics.Tests.AllocationTests` | 10,000 calls of the computing members allocate nothing |
| `CoolEngine.Mathematics.Tests.PublicApiTests` | No public signature mentions `Matrix4x4`, `Matrix3x2` or `Plane` |

**Goldens (2026-11-26, i7-14700K, .NET 10, Windows 11):** float `0xEDEDE1FB97659790`, double `0xFAB69D982403FC0E`. They also pass with the C runtime's FMA3 code switched off (`_set_FMA3_enable(0)`). **Re-record rule:** only after a .NET update (the delegated members can move), and only once the four paths agree on the new hash; a different hash on another CPU or OS is a defect to investigate, never a reason to re-record. Acceptance commands: [BUILD §2](../BUILD.md#2-build).

Found by the tests: OpenTK 4.9.4's `Vector4d.Clamp` clamps Z and W wrongly; the tests use its `ComponentMin` and `ComponentMax` instead.

## 7. Benchmarks

`tests/CoolEngine.Mathematics.Benchmarks` (BenchmarkDotNet 0.15.8) compares the library with System.Numerics (float) and OpenTK (float and double) on the same 1024 seeded inputs, one shared checksum for every library (per-library checksums compiled differently and distorted ratios up to 1.32×). Acceptance runs are pinned with `--affinity` to one performance core and use `--launchCount 3`, medians, because the i7-14700K mixes fast and slow cores (identical code measured 2.0 and 4.7 µs between unpinned runs). Command: [BUILD §2](../BUILD.md#2-build).

**Figures** (2026-11-26, i7-14700K, .NET 10.0.12, pinned, Release; µs per 1024 operations; 0 B allocated everywhere):

| Operation | CoolEngine.Mathematics | System.Numerics | OpenTK |
|-----------|------------------------|-----------------|--------|
| `Matrix4` multiply | 4.67 | 4.65 | 11.43 |
| `Matrix4 * Vector4` | 1.57 | 1.52 | 7.34 |
| `Matrix4` inverse | 16.2 | 17.8 | 8.7 |

Other ratios (ours divided by theirs; below 1 is faster): transpose 0.92, `LookAt` 0.99 and `CreateFromQuaternion` 0.99 of System.Numerics; `TransformPosition` 1.11 to 1.13 of System.Numerics (its baseline is bimodal); the double types 0.25 to 1.04 of OpenTK (`Matrix4d.LookAt` 0.25, perspective 0.59, inverse 0.85, `Quaterniond.Slerp` 0.80).

## 8. Known limits

| Limit | Detail |
|-------|--------|
| Inverse speed | `Matrix4.Inverted` is 0.91× System.Numerics but about 1.9× OpenTK's SSE inverse when that is inlined (about parity when not). Accepted (Claude, 2026-11-26): same bits ranks first and inversions are rare per frame; a faster same-bits version is in [BACKLOG](../BACKLOG.md#own-libraries-and-engine-direction). A rework of the `Matrix4d` inverse was dropped: it measured 0.85× OpenTK |
| Trig | Not identical across CPUs ([§3](#3-the-same-bits-promise)) |
| NaN | A NaN's sign and payload are not covered |
| The analyzer | RS0030 flags **uses** of the banned System.Numerics matrix APIs in `Common`, `CoolEngine`, `OldTanks.UI` and `OldTanks`, not declarations, and not the library or the test projects |
| Other CPUs | x64 only; ARM and WebAssembly are untested |
| OpenTK bug | `Vector4d.Clamp` in OpenTK 4.9.4 clamps Z and W wrongly; the library's own `Clamp` is correct |

## 9. What remains

OT-5's library part is done and nothing calls it. The rest of OT-5 waits for OT-4 to merge: move the 54 files that use `OpenTK.Mathematics` to this library, first with Denys's temporary port-check API (Denys, 2026-11-26: "Change API for first time to detect such issues easily, then make it as planned after fixes"), then the reversed-Z switch (a shader and depth-state change of its own). The OpenTK `Vector3` and `Matrix4` that existing code uses stay in place until then ([CODE-STYLE](../CODE-STYLE.md#math-types)). Work items: [BACKLOG](../BACKLOG.md#own-libraries-and-engine-direction).
