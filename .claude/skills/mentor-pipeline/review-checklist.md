# Review Checklist (OldTanks)

You review one change against its plan. You are read-only; building, running the tests and running benchmarks is fine; starting the game is not. Go through the sections in order and spend your effort where spec fidelity, threads and hot paths are at stake. List a section with nothing to report under "Checked, nothing found", so that silence means checked, not skipped.

`KI-nnn` refers to [KNOWN-ISSUES](../../../docs/KNOWN-ISSUES.md); "ARCHITECTURE §n" to [ARCHITECTURE](../../../docs/ARCHITECTURE.md); the engines' requirements and decisions are in the [plan](../../../docs/plans/ENGINES-AND-TANK-GAME.md).

## 1. Verify, don't trust

- **Run** `dotnet build OldTanks.slnx --no-incremental` and `dotnet test OldTanks.slnx`, and quote the warning, error and test lines. Warnings must not exceed the baseline; Total must equal the baseline plus the plan's new tests, less any test the plan removes. A test that went from passed to skipped is a blocker. The test run includes the documentation checks.
- **Weakened tests are blockers:** in `git diff tests/`, an existing assertion changed, a test deleted, skipped or `[Ignore]`d without the plan saying so, or an expected value that differs from the plan.
- **Does the new test bite?** For a bug fix, read the new test and say whether it fails on the old code. A test that passes either way proves nothing. For a guard that hangs on one line, undo that line in a scratch copy of the checkout, never in the shared one, and confirm a test turns red.
- **Claims:** check every row of the implementer's claims table against the code, including the thread each method runs on. A claim that doesn't hold is a finding.

## 2. The plan

- **Conformance:** every planned change is there, with the signatures as written, and nothing outside the plan's files changed.
- **Challenge:** if the plan itself is wrong (a missed entry point, a wrong root cause, a missing test, a known issue it ignores, a boundary it crosses), say so under "Plan challenges". The mentor wrote it and can be wrong.

## 3. Spec fidelity (no improvisation)

- **Every mechanic number and formula** in the touched code traces to the spec section the plan cites. A value with no source, or a "reasonable default", is a **blocker**: Denys's rule is that nothing is improvised.
- **Units and axes** match the spec and the engine: radians or degrees stated and converted once (KI-002), Y up and gravity along -Y, seconds, the fixed time step where there is one.
- **Nothing comes from the old client:** no name, constant, formula, asset or structure traceable to the Tanki Online or GTanks resources ([plan §3](../../../docs/plans/ENGINES-AND-TANK-GAME.md#3-scope-of-the-reference-client)). A suspected copy is a blocker, reported with what it resembles.

## 4. Engine boundaries

- The physics engine references no OpenGL, no windowing and no game project; it runs headless, as the dedicated server needs (plan D2).
- Engine projects reference no `OldTanks.*` game type; their public models are project-agnostic.
- The shared core references no engine. UI builds on the render engine, not on the game.
- A new project reference or a type moved across a boundary needs the plan's say-so.

## 5. Threads and the GL thread

- **OpenGL only on the GL thread** (ARCHITECTURE §3): no GL call in a constructor that can run on another thread, in a finalizer (KI-017) or in an `await` continuation that may resume elsewhere.
- **Dispatcher:** `Invoke` from the GL thread runs inline and from another thread blocks until the next update frame; a wait on the GL thread for work queued to itself deadlocks. Exceptions in queued `Post` work are lost (KI-011).
- **Shared state:** collections read by one thread and changed by another (KI-003), locks that guard more or less than they say, lock order, `ReaderWriterLockSlim` used as a plain mutex, torn reads of `Vector3` or `Matrix4` fields written by another thread.
- **Parallel physics:** work split across threads writes disjoint data; the result of a parallel step equals the serial step's for the plan's test scene; shutdown and exceptions on a worker are handled; no `Parallel` loop on a per-frame path without a measured benefit.
- No `async void` outside event handlers, no `.Result` or `.Wait()` on a task that needs the GL thread.

## 6. Physics correctness

- Integration and contact resolution follow the plan step by step; sub-iterations divide the time step once.
- NaN and infinity cannot enter positions or velocities silently (zero-length normalisation, division by a zero mass or a zero time step).
- Static bodies stay static; a body with no collision is handled where the code iterates (the current step dereferences it, PHYSICS §5).
- Collision tests: axes used (KI-016), normal direction, depth sign, contact at a shape's corner, an object inside another.
- For server-authoritative state (plan D2): no dependence on frame rate, on wall-clock time or on the order of a `Dictionary` or `HashSet`.

## 7. Performance

- **Allocation-free hot paths:** per frame, per physics step and per draw, no LINQ, closures, boxing, `params` arrays, string formatting, iterator allocations or new collections. `ArrayPool` rentals are returned on the normal paths; `stackalloc` stays small.
- **No exception handling in the render loop (Denys, 2026-11-25):** reject any `try`/`catch`/`finally`, and any `using` block or declaration (it compiles to `try`/`finally`), in per-frame renderer code: a renderer's draw method and everything it calls each frame. Exceptions propagate to the high-level caller, which is meant to re-run the whole render pipeline (no such handler exists yet, so today a render-path exception ends the game; [BACKLOG](../../../docs/BACKLOG.md)); a torn frame, a UI freeze or a lost pooled buffer on an exception is accepted. Denys: "any renderer pipeline (in loop) should not use try..catch..finaly. Only hight-level methods with calls" ([CODE-STYLE](../../../docs/CODE-STYLE.md#patterns)).
- **Data layout:** structs passed by `in` or `ref readonly` when large; arrays of structs or `Span<T>` over lists of classes in the inner loops; no virtual or interface dispatch per element where a concrete type is known.
- **SIMD and math:** `System.Numerics` vector types where the plan asks; `MathF` for `float` math; no `double` round-trips in `float` loops.
- **OpenGL:** state changes and buffer uploads batched; no per-object uniform upload where an instanced or uniform-buffer path exists; buffers resized geometrically, not per frame.
- **Evidence:** a performance claim needs a benchmark run (BenchmarkDotNet with the memory diagnoser) against the baseline numbers in the plan. Without one the finding or the claim is PLAUSIBLE at best.

## 8. Rendering and shaders

- Every uniform and attribute name the C# side sets exists in the shader files, which change in the same diff (KI-008).
- GL objects are created and disposed on the GL thread; no handle is used after `Dispose`; state changed for a draw is restored where the next draw relies on it (`GLSettings` around ImGui).
- Every mesh of a drawable has its `TextureData` before the drawable reaches `ObjectRenderer<T>` (since OT-7 `InstancedShader` may be set before or after drawables arrive; without it everything is drawn per instance).

## 9. UI and the generator

- `ControlHandler` exists before any control; control names are unique in a window (KI-010); `Width` push and pop balance (KI-018).
- Binding attribute strings name real members and events; the generated file compiles and was read (BINDING-GENERATOR §1); `bindingWay` is a named argument.
- UI handlers that change the world respect the physics thread (KI-003).

## 10. Tests

- A bug fix has a test that fails before it and one for the unaffected case; on the high tier, the plan's concurrency tests exist and run in the normal test run.
- `MethodName_Condition_ExpectedBehavior` in the mirrored fixture, NUnit, AAA, `Assert.Multiple` for several assertions, `Assert.ThrowsAsync` awaited.
- Headless and deterministic: no OpenGL context, no window, no sleeps, no wall-clock timing; float comparisons with an explicit tolerance.

## 11. Style the build doesn't enforce (CODE-STYLE)

- `m_camelCase` instance fields, `s_camelCase` static fields, `PascalCase` constants; sealed classes where nothing derives; `ObservableObject.SetField` for observable properties.
- OpenTK types in engine and game code, `System.Numerics` at the ImGui boundary (plan D7 may change this); conversions in one place.
- Serilog message templates with named properties; English comments that say why, not what; no commented-out code; a comment that contradicts the code is a bug.
- A null-forgiving `!` needs a reason; no new nullable warnings.

## 12. Simpler with the platform

In the **touched lines only**, look for code that .NET or C# already provides: `Math.Clamp`, `MathF`, `ArgumentNullException.ThrowIfNull`, `CollectionsMarshal.AsSpan`, pattern matching, collection expressions, `TryGetValue`. Report one only when behaviour stays identical **for the actual types and inputs**, a test covers the line, and the result reads better.

Rewrites that look equivalent and aren't:

| Rewrite | The difference |
|---|---|
| `a > b ? a : b` → `Math.Max(a, b)` on `float` | The ternary returns `b` when `a` is NaN; `Math.Max` returns NaN |
| A scalar loop → `Vector<T>` or `Vector128` | Different summation order and possible fused multiply-add: results differ in the last bits, which breaks parallel-equals-serial and replay comparisons |
| A sequential loop → `Parallel.For` | Order-dependent results (contact resolution, accumulation) change, and the per-call overhead can exceed the work |
| A loop → LINQ on a per-frame path | Allocations every frame (§7) |
| `foreach` over a `List<T>` → `CollectionsMarshal.AsSpan` | No "collection was modified" check: a concurrent change corrupts silently instead of throwing |
| `ApproximateEqual(a, b)` with no epsilon | Its default is `float.Epsilon`, so it is exact equality (CODE-STYLE "Math types") |
| `Dictionary` iteration as an order | Not an order: two runs or two machines may differ |

These findings are nit or minor unless the rewrite removes a real bug. Untouched code gets no findings here; a sweep is its own OT task.

## 13. Packages and docs

- No `PackageReference` added or version changed without the plan saying so.
- A doc claim the change touched still matches the code; a known issue the change fixes or worsens is named.

## Report shape

```
Verified
- Build: <warnings line> / <errors line>
- Tests: <line> (baseline <Total> → <Total>)
- Benchmarks: <lines, if any>

Findings, most severe first
| # | file:line | Category | Severity | Claim | Failure scenario | Minimal fix | CONFIRMED / PLAUSIBLE |

Plan challenges
- …

Spec gaps (questions for Denys, not fixes)
- …

Claims that don't hold
- <symbol>: <claim>, but <what the code does>

Checked, nothing found
- <section numbers>
```

- **Severity:** **blocker** (an improvised or copied mechanic, a GL call off the GL thread, a data race on world state, a crossed engine boundary, a weakened test, exception handling in per-frame renderer code), **major** (wrong behaviour in a realistic case, an allocation or a regression on a hot path), **minor** (wrong in an edge case, or a style rule broken), **nit**.
- **CONFIRMED** means you traced it in the code or reproduced it. **PLAUSIBLE** means you couldn't, and says what would settle it.
