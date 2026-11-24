# Engines and the Tank Game

| Field        | Value  |
|--------------|--------|
| Status       | Draft - requirements recorded, D1-D4 decided, D5-D7 open |
| Last updated | 2026-11-24 |

---

Denys's goal of 2026-11-24 for OldTanks, recorded so every later session starts from it. Agreed: the requirements in §1-§2 and decisions D1-D4 in §5. §4 is a proposal.

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
| `*.Core` (shared) | Math on `System.Numerics` (hardware-accelerated SIMD), transforms, bounding volumes, handles and IDs, frame snapshots and other sync primitives, a job scheduler | `Common`, parts of `CoolEngine.Services` |
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
| D3 | Target framework for the new projects | **Decided (Denys, 2026-11-24):** `net10.0`, and the existing projects are retargeted to it too |
| D4 | Repository strategy: new projects beside the old ones, rewrite in place, or a new repository | **Decided (Denys, 2026-11-24):** new engine projects beside `CoolEngine` in this repository; the sandbox moves to them piece by piece; `CoolEngine` is deleted once nothing uses it |
| D5 | Project and engine names | Open |
| D6 | Physics determinism (needed for lockstep or replay, optional for a server-authoritative model) | Open; follows D2 |
| D7 | Math library: `System.Numerics` everywhere, or OpenTK math in the render engine | Open |
