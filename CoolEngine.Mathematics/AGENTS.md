# CoolEngine.Mathematics

> **Project knowledge:** [../docs/PROJECT-KNOWLEDGE.md](../docs/PROJECT-KNOWLEDGE.md)

The engine's own math: column-major float matrices over `System.Numerics`' vectors and quaternion, and double vectors, matrices and a quaternion, with no package dependencies. Added in OT-5; `CoolEngine` references it since the move (2026-11-26, unmerged).

## Related Docs

| Topic | Page |
|-------|------|
| Conventions, the types, what is delegated and what is own code, the same-bits promise, edge behaviour, tests, benchmarks, limits | [MATHEMATICS](../docs/CoolEngine.Mathematics/MATHEMATICS.md) |
| The decisions (D6, D7) and the move from `OpenTK.Mathematics` (done in OT-5) | [plan §6](../docs/plans/ENGINES-AND-TANK-GAME.md#6-decisions-of-ot-3-and-the-planned-tasks) |
| The upload layout (`transpose = false`) | [MATHEMATICS §5](../docs/CoolEngine.Mathematics/MATHEMATICS.md#5-the-opengl-memory-layout) |
| Remaining work (the move, a faster inverse, tests and benchmark pinning) | [BACKLOG](../docs/BACKLOG.md#own-libraries-and-engine-direction) |

## Project Details

| Property | Value |
|----------|-------|
| Namespace root | `CoolEngine.Mathematics` |
| Target framework | `net10.0`, `LangVersion latest` |
| Layer | Engine library; referenced by no project yet; tested by `tests/CoolEngine.Mathematics.Tests`, benchmarked by `tests/CoolEngine.Mathematics.Benchmarks` |

## Key Types

| Type | Role |
|------|------|
| `MathHelper` | `Pi`, `TwoPi`, `PiOver2`, `PiOver4` and the degree and radian conversions (float and double) |
| `Matrix2`, `Matrix3`, `Matrix4` | Float matrices for column vectors, column-major, built on `System.Numerics.Vector2/3/4`; `Matrix4` also takes `System.Numerics.Quaternion` |
| `Vector2d`, `Vector3d`, `Vector4d`, `Quaterniond` | Double vectors and quaternion with System.Numerics' API shape and OpenTK-style names |
| `Matrix2d`, `Matrix3d`, `Matrix4d` | Double matrices, the same conventions as the float ones |
| (no float vector or quaternion type) | The float vectors and quaternion **are** `System.Numerics.Vector2/3/4` and `System.Numerics.Quaternion` |

## Dependencies

| Dependency | Reason |
|------------|--------|
| none | No project and no package reference, on purpose: managed code only, portable `Vector128` and `Vector256` intrinsics (WebAssembly later, [plan §6](../docs/plans/ENGINES-AND-TANK-GAME.md#6-decisions-of-ot-3-and-the-planned-tasks)) |

## Constraints

- **The conventions are fixed (Denys, D7):** column vectors, column-major storage, right-handed, radians, clip depth [0, 1] with reversed Z (near is 1, far is 0). `A * B` applied to a vector applies `B` first.
- **No fused multiply-add anywhere**, in code or through a call into System.Numerics where its result depends on FMA hardware. Every member that uses only + − × ÷, sqrt and exact operations gives the same bits on every x64 CPU for finite inputs ([MATHEMATICS §3](../docs/CoolEngine.Mathematics/MATHEMATICS.md#3-the-same-bits-promise)). A change that breaks that fails `CoolEngine.Mathematics.Tests.CrossPathTests`.
- **The promise has limits:** members that call sin, cos, tan or acos follow the OS math library (not identical across CPUs), and a NaN's sign and payload are not covered. Say so in the member's doc comment.
- **Never put `System.Numerics.Matrix4x4`, `Matrix3x2` or `Plane` in a public signature.** They are row-vector types. `Matrix4` converts to and from `Matrix4x4` privately, on the same bytes; `tests/CoolEngine.Mathematics.Tests.PublicApiTests` guards it.
- **Every math change is reviewed at effort `max` by an `opus` or `fable` reviewer** ([CODE-STYLE](../docs/CODE-STYLE.md#git-rules), plan D7).
- **Every math change re-runs the affected benchmark categories** (Release, pinned, `--launchCount 3`, medians) and reports them against the reference figures; a median more than 5% slower is a review finding, and a hot member without a benchmark gets one ([MATHEMATICS §7](../docs/CoolEngine.Mathematics/MATHEMATICS.md#7-benchmarks)).
- **Re-record a golden hash only after a .NET update, and only once the four paths agree** (default, `DOTNET_EnableAVX2=0`, `DOTNET_EnableAVX=0`, `DOTNET_EnableHWIntrinsic=0`). A different hash on another CPU is a defect to find, never a reason to re-record ([MATHEMATICS §6](../docs/CoolEngine.Mathematics/MATHEMATICS.md#6-how-it-is-tested)).
- **Portable intrinsics only, no `unsafe`:** `Vector128` and `Vector256`, never `Sse`, `Avx` or `AdvSimd`.
- **0 allocations** in the computing members (`CoolEngine.Mathematics.Tests.AllocationTests`); `ToString` formats with the invariant culture.
