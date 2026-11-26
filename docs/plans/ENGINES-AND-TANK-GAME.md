# Engines and the Tank Game

| Field        | Value  |
|--------------|--------|
| Status       | Draft - requirements recorded, D1-D4 and D6-D13 decided, D5 open |
| Last updated | 2026-11-26 |
| Verified against | 2026-11-26; feature/OT-5 at fb61096; D6, D7 and the OT-5 row against the library and its docs |

---

Denys's goal of 2026-11-24 for OldTanks, recorded so every later session starts from it. Agreed: the requirements in §1-§2 and the decided rows of §5; the own-library tasks and the render, browser and backend direction are in §6. §4 is a proposal and §6 overrides it where they differ (the math library, D7).

## 1. Goal

A working online tank game, built on three separate engines (physics, render, UI) that are as optimized as .NET and OpenGL allow, with the game orchestrating them. A shared project may hold what the engines and the game have in common.

**Working rule (Denys):** anything unclear is asked; no improvisation, in physics or anywhere else.

## 2. Requirements

| Part | Requirements (Denys, 2026-11-24) |
|------|----------------------------------|
| **Physics engine** | Runs on several threads where possible, with all the machinery that needs. Project-agnostic models. Optimized as far as possible, using every available .NET optimization |
| **Render engine** | Optimized as far as possible with .NET optimizations and the available OpenGL functions and shaders. Project-agnostic models. All the machinery the OpenGL single-thread restriction requires |
| **UI engine** | Built on the render engine. ImGui for now, its own UI later. The default primitives: Button, TextBox, TextBlock/Label, Image, ListBox, ComboBox, ItemsControl; WPF's input and processing controls are the model (not all of them) |
| **Shared project** (if needed) | Models that fit both engines; synchronization primitives; common math; common models for 2D and 3D space; other shared needs |
| **Game** | A tank game with online play, using all of the above with proper orchestration. Primitives may live in a game project or the shared project |
| **Documentation** | A separate test project for the docs: done in OT-1 (`tests/OldTanks.Docs.Tests`) |

## 3. Scope of the reference client

Denys pointed at `C:\MyFiles\Programs\TankiOnline_2010-2012_resources` (the GTanks client's `battle.swf` and source) and asked for a 1:1 port of its logic. Claude declined to decompile or port it: by its name it is the Tanki Online client (AlternativaPlatform's game) as used by the GTanks private server, and a 1:1 port would recreate that game. Claude did not open the folder. Denys asked why it assumed the resources are not his; the answer is pending (2026-11-24). Until it changes, the engines are in scope, and the game is an original tank game whose mechanics come from Denys's own specifications (D1), not from the old client.

## 4. Proposed shape (not agreed)

Starting point, to be confirmed or changed through §5:

| Project (working names) | Role | Builds on what exists |
|-------------------------|------|-----------------------|
| `*.Core` (shared) | Math: the own library of D7 (§6), not plain `System.Numerics`; transforms, bounding volumes, handles and IDs, frame snapshots and other sync primitives, a job scheduler | `Common`, parts of `CoolEngine.Services` |
| `*.Physics` | Headless (no OpenGL), data-oriented: struct arrays, `Span<T>`, pooled memory; fixed time step; broad phase, narrow phase, solver; islands stepped in parallel on the job scheduler | `CoolEngine.PhysicEngine` (tests and lessons, [KI-016](../KNOWN-ISSUES.md#ki-016-polygon-tests-use-face-normals-only)) |
| `*.Rendering` | Owns the GL thread and context; other threads submit through queues; modern GL 4.5/4.6 (direct state access, persistent-mapped buffers, instancing and indirect draws, uniform and storage buffers) | `CoolEngine` renderers and the dispatcher ([RENDERING](../CoolEngine/Services/Renderers/RENDERING.md), [ARCHITECTURE §3](../ARCHITECTURE.md#3-getting-work-onto-the-gl-thread)) |
| `*.UI` | Retained control tree, layout, input routing, the listed primitives; an ImGui backend first, an own renderer later | `OldTanks.UI`, the binding generator |
| Game client, game server, network protocol | The game; the server runs physics headless | `OldTanks` (the sandbox becomes a test scene) |
| Tests and benchmarks | NUnit tests per project; BenchmarkDotNet for the hot paths | `tests/` |

**Phasing:** retarget and confirm the current game runs → core and physics (headless, tested, benchmarked) → rendering → UI → the game client on the new engines, replacing `CoolEngine` → networking and the server → game content. Each phase gets its own `OT-<n>` from Denys.

## 5. Open decisions

| # | Decision | Status |
|---|----------|--------|
| D1 | Where the tank game's mechanics come from (§3) | **Decided (Denys, 2026-11-24):** Denys writes the specifications (each mechanic and its numbers); Claude implements exactly that and asks about every gap |
| D2 | Online model: authoritative dedicated server or one player hosting | **Decided (Denys, 2026-11-24):** a dedicated authoritative server running physics headless; clients send input, predict locally and interpolate the others. The physics engine never depends on OpenGL or the window |
| D3 | Target framework for the new projects | **Decided (Denys, 2026-11-24):** `net10.0`, and the existing projects are retargeted to it too. **Done in OT-3** (2026-11-25), with `LangVersion latest` |
| D4 | Repository strategy: new projects beside the old ones, rewrite in place, or a new repository | **Decided (Denys, 2026-11-24):** new engine projects beside `CoolEngine` in this repository; the sandbox moves to them piece by piece; `CoolEngine` is deleted once nothing uses it |
| D5 | Project and engine names | Open |
| D6 | Physics determinism (needed for lockstep or replay, optional for a server-authoritative model) | **Decided (Denys, 2026-11-26):** "Same bits on x64 (Recommended)" (turned down: "Keep fastest"): every member of `CoolEngine.Mathematics` that uses only + − × ÷, sqrt and exact operations gives the same bits on every x64 CPU, for finite inputs. Scoped after review B: "Exclude trig (Recommended)" (turned down: "Own trig too"), because Windows' C runtime `Math.Sin/Cos/Tan/Acos` differ between CPUs with and without FMA3; members that call them follow the OS math library, and a NaN's sign and payload are not covered. It supersedes the 2026-11-25 answer "Fastest, no promise" (turned down then: "Same machine", "Bit-exact everywhere"). Scope and proof: [MATHEMATICS §3](../CoolEngine.Mathematics/MATHEMATICS.md#3-the-same-bits-promise). Physics determinism as a whole (lockstep, replay) is still the physics engine's own question |
| D7 | Math library: `System.Numerics` everywhere, or OpenTK math in the render engine | **Decided (Denys, 2026-11-24; scope and shape on 2026-11-25 and 2026-11-26, §6):** "Math agreed. We need our high-performance math." An own library, `CoolEngine.Mathematics` (options turned down: keep `OpenTK.Mathematics`, `System.Numerics` everywhere). Column vectors with column-major storage, clip-space depth [0, 1] (`glClipControl`) with reversed Z, `float` and `double` both supported; every math change is reviewed at effort `max` by an `opus` or `fable` reviewer (§6). **Built in OT-5 (library part, 2026-11-26):** the float vectors and quaternion are `System.Numerics`' own types, the matrices and the double types are own ([MATHEMATICS](../CoolEngine.Mathematics/MATHEMATICS.md)) |
| D8 | Render API | **Decided (Denys, 2026-11-24):** "I think we should be in the same pacing with modern world. But for our purpose OpenGL should be enough. We won't create AAA project." OpenGL 4.6 core in the modern explicit style behind an API-neutral render boundary (§6) |
| D9 | Browser client | **Decided as far-future direction (Denys, 2026-11-24):** "we will move to browser as well to allow cross-platform. But in the some far future I think. So WebGPU sounds right way, isn't it" (§6) |
| D10 | Backends | **Decided (Denys, 2026-11-24):** "Keep desktop on OpenGL, WebGPU only in browser." (option turned down: WebGPU natively on desktop through Dawn or wgpu-native, which brings back a native library) |
| D11 | Own libraries instead of dependencies | **Decided (Denys, 2026-11-24):** fonts (`CoolEngine.Fonts`, OT-4), math (`CoolEngine.Mathematics`, OT-5), OpenGL bindings (`CoolEngine.OpenGL`) and window and input (`CoolEngine.Platform`); the images project (`CoolEngine.Imaging`) is done (OT-3) (§6) |
| D12 | ImGui and logging | **Decided (Denys, 2026-11-24):** ImGui "Not needed. ImGUI will be as dependency until we either use AvalonUI to display complex UI or create our UI"; Serilog "Not needed." Both stay as dependencies |
| D13 | Order of the own-library tasks | **Decided (Denys, 2026-11-24):** "Plan math with doubles in OT-5 after fonts. But today we only complete OT-3" (§6) |

## 6. Decisions of OT-3 and the planned tasks

All 2026-11-24, Denys unless marked; the session record is in the [session log](../sessions/SESSION-LOG.md).

**Render API (D8).** Claude recommended, Denys: "Yes, record it". OpenGL 4.6 core written in the modern explicit style (direct state access, persistent mapped buffers, multi-draw indirect, few state changes; bindless textures only with a fallback) **behind an API-neutral render boundary** the rest of the engine uses (buffers, textures, pipeline-state objects, command lists recorded on any thread and submitted on the GL thread), so a second backend can be added later without rewriting the engine. OpenTK 4.9 keeps the GL calls and GLFW the window and input for now; own thin GL bindings remain an option, for control rather than speed. Options turned down: Vulkan or D3D12 now (several times the code for the same picture); Silk.NET (the same per-call speed as OpenTK; its value is the Vulkan and D3D12 coverage that is deferred). Open for the render-engine plan: the oldest GPU to support.

**Browser (D9, D10).** A far-future browser client with WebGPU as its backend (Claude agreed: WebGL 2 lacks compute, storage buffers and indirect draws); desktop stays on OpenGL. Design constraints Claude proposed to keep in mind from now on (guidance, not yet decided):

| Constraint | Meaning |
|------------|---------|
| Render boundary | Model it on WebGPU's pipelines, bind groups, command encoders and queue submit |
| Shaders | GLSL today; WGSL later, a translation path decided then |
| Managed-only libraries | `CoolEngine.Imaging`, `CoolEngine.Fonts` and the own math run in WebAssembly; native ones (GLFW, cimgui, OpenTK's natives) do not, so window, input and UI stay behind abstractions |
| No worker threads | The engine must work without them (browser threading in .NET WebAssembly is not settled) |
| Math | Built on 128-bit SIMD (WebAssembly's width), wider paths optional |
| Server transport | Browsers cannot open raw UDP or TCP, so the server must also speak WebSockets or WebTransport |
| Loaders | Read from an asset source, not file paths |

Browser support on 2026-11-24 (gpuweb implementation-status wiki, web.dev): Chrome and Edge 113+ on desktop, Chrome 121+ on newer Android devices, Firefox 141 on Windows and 145 on macOS ARM (Linux expected 2026), Safari 26 on Apple platforms.

**Math (D7), Denys's answers.** Denys asked which matrix convention is better and when `double` is needed; Claude recommended column vectors with column-major storage (matches GLSL, WGSL and most graphics literature; upload is a plain copy; the same cost as the row form), a [0, 1] depth range with reversed Z, and `float` for geometry, physics and rendering with time kept as integer ticks or `double`, `double` positions only if maps grow past roughly 10 km. Denys: "Yes, use column vectors with column-major storage. Two related choices: Depth range [0, 1] agreed. Reversed Z agreed. Use supporting for doubles. But such changes should be reviewed by agent with appropriate max effort and model (opus or fable)" (option turned down: `float` only with `double` added on demand). The review rule is recorded in the mentor-pipeline skill's tier table. Open question left then: determinism (D6), decided on 2026-11-26 (below).

**Math (D7), OT-5 decisions of 2026-11-25 and 2026-11-26 (Denys unless marked; the full record with turned-down options is in the [session log](../sessions/SESSION-LOG.md)).** 2026-11-25: scope "Library, then move in OT-5" (turned down: "Library only", Claude's recommendation; "Library + physics move"); names in OpenTK style, `Vector3d`, `Matrix4d`, `Quaterniond` (turned down: shader style `Float3`/`Double3`, System.Numerics style `Vector3D`); right-handed with radians in the whole API; BenchmarkDotNet 0.15.8; determinism "Fastest, no promise" (superseded). 2026-11-26: after the review measured an own 12-byte `Vector3` at 2.38 ns per dependent-chain step against 0.41 for System.Numerics and 0.77 for OpenTK, Denys asked "Why not use then System.Numerics before we dig dipper?" and chose to use `System.Numerics.Vector2/3/4` and `Quaternion` as the float vectors and quaternion (turned down: System.Numerics vectors with an own `Quaternion`; keeping own types). He then asked for a council: "Dispatch several fable agents to re-think whole idea of this ticket (to create coucil) which will analyze all pitfalls of such low-level instructions". Four `fable` agents (SIMD and JIT; engine and graphics architecture; physics numerics; cost and risk) all answered "B narrowed": System.Numerics float vectors and quaternion, own column-major matrices, the doubles deferred, the move and reversed Z as their own tickets; none picked own classes everywhere (A) or staying on OpenTK (C), and all called C workable. Denys chose "B as planned" (turned down: "B narrowed", "C: stay on OpenTK"): the double types and the move stay in OT-5. Also: D6 "Same bits on x64" (above); `Matrix4` kernels, "Which option will be faster? If System.Numerics internally - pick them" (so transpose and the factories delegate; multiply, `M * v` and inverse are own code, "Same bits: own code (Recommended)", because System.Numerics' versions differ with FMA hardware); the banned-API analyzer "In OT-5 now" (turned down: with the move ticket; no analyzer); `TryInvert` "Return false (Recommended)" when the determinant or its reciprocal is not finite; port safety, "Change API for first time to detect such issues easily, then make it as planned after fixes" (a temporary port-check API during the move). Open for the move ticket: nothing recorded.

**Answers to the tasks' open questions (Denys).** Bindings: "Use Roslyn source-generator. Generate all, but cut only unused (if possible)" (Claude: the .NET trimmer drops unused functions in trimmed or Native AOT publishes; function pointers load lazily on first call, so unused ones cost nothing at startup). Window and input: "Gamepad later. Only 1 window (I don't multi-window games either)". Fonts: see the table.

**Planned tasks.** Numbers are given when each branch is created ([CODE-STYLE](../CODE-STYLE.md#git-rules): highest `OT-<n>` plus one); the order is planned, not reserved, and only OT-4 and OT-5 are fixed (D13). Also in [BACKLOG](../BACKLOG.md#own-libraries-and-engine-direction).

| Order | Task | New project | Replaces | Scope | Open for its plan |
|-------|------|-------------|----------|-------|-------------------|
| 1 | Own fonts (OT-4) | `CoolEngine.Fonts` | FreeTypeSharp | Denys: formats "Everything excluding legacy and language specific fonts (arabic, indic, e.t.c)" (turned down: OpenType family only; TrueType only; everything incl. legacy Type 1 and bitmap fonts); hinting "Full hinting", the TrueType instruction interpreter (turned down: unhinted); name `CoolEngine.Fonts` (turned down: `CoolEngine.Text`); tests "FreeType and Windows", FreeTypeSharp in the test project only plus DirectWrite (turned down: FreeType only). Claude's proposal: dependency-free, OpenType with TrueType and CFF outlines, collections, WOFF/WOFF2. Denys: "Yes, OT-4 after OT-3" | Whether "everything" includes colour fonts (COLR/CPAL, CBDT, sbix, SVG) and variable-font axes beyond the default instance; whether CFF hinting (stem hints) is meant by "full hinting" |
| 2 | Own high-performance math (OT-5; **library done on `feature/OT-5`, 2026-11-26, the move open**) | `CoolEngine.Mathematics` | `OpenTK.Mathematics` (54 files use it, counted 2026-11-26) | Done: float `Matrix2/3/4` over System.Numerics vectors and quaternion, the double types, no FMA, same bits on x64 (D6), 433 tests and benchmarks against System.Numerics and OpenTK ([MATHEMATICS](../CoolEngine.Mathematics/MATHEMATICS.md)). Open, planned and not waiting for OT-4: move the 54 files with a temporary port-check API, then the reversed-Z switch; the physics engine then depends only on it | Nothing recorded |
| 3 | Own OpenGL bindings | `CoolEngine.OpenGL` | `OpenTK.Graphics` (18 files, 62 distinct `GL.*` functions) | A Roslyn source generator over Khronos `gl.xml` emits the OpenGL 4.6 core profile; unused functions cut where the toolchain can; unmanaged function pointers loaded through a loader interface (GLFW's until task 4, then WGL); typed handles and span-based signatures; the debug-output callback; no windowing dependency; generator output tested headless, rendering by an app check | Which extensions (for example `ARB_bindless_texture`) |
| 4 | Own window, context and input | `CoolEngine.Platform` | `OpenTK.Windowing` and the native GLFW (5 files; `OldTanks.Windows.MainWindow` derives from `GameWindow`, `ImGuiController` reads its input state) | Window and input interfaces (a browser host implements them later) and a Win32 implementation: message loop, WGL 4.6 core debug context, vsync, raw mouse input, keyboard and text input, DPI awareness, borderless and exclusive full screen, cursor capture; the game loop with today's update and render frequencies; keyboard, mouse and text now, gamepads later, exactly one window | How much of `GameWindow`'s loop semantics to keep |

Task 3 depends on nothing (it uses GLFW's loader until task 4); task 4 depends on task 3 (WGL function loading). The project names were proposed by Claude and Denys said "Yes, use them". The own-library tasks follow Denys's "Write tasks to rewrite bindings and windows to our code. Move this to appropriate projects."

**Already done in OT-3:** `CoolEngine.Imaging` ([IMAGE-DECODERS](../CoolEngine.Imaging/IMAGE-DECODERS.md)); the retarget to `net10.0`; central package versions in `Directory.Packages.props`.
