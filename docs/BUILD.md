# Build and Run

| Field        | Value  |
|--------------|--------|
| Status       | Living |
| Last updated | 2026-11-25 |
| Verified against | 2026-11-25; bug/OT-7 (committed, rebased on master at d2f8b8a with OT-11): §2's warning baseline and tests; §1-§2 built and tested on Windows 11 with .NET SDK 10.0.401, §3 run on 2026-11-25 (the smoke run, Denys's own run and the two OT-7 runs), §4 as of 2026-11-24 except the settings file |

---

## 1. Requirements

| Need | Why |
|------|-----|
| Windows | The only platform the game has been developed on; the natives (GLFW through OpenTK, cimgui, FreeType) are not checked elsewhere; `tests/CoolEngine.Imaging.Tests` targets `net10.0-windows` (GDI+) |
| A GPU driver with **OpenGL 4.6** core | The window asks for a 4.6 forward-compatible debug context and every shader is `#version 460 core` |
| The **.NET 10 SDK** | Every project targets `net10.0` (the generator `netstandard2.0`), with `LangVersion latest`; built with SDK 10.0.401. The generator's Roslyn packages are held at 5.9.0, the compiler of that SDK ([ARCHITECTURE §1](ARCHITECTURE.md#1-solution-layout)) |
| The **.NET 10 runtime** to run the game | See §3 |

## 2. Build

The solution file is `OldTanks.slnx` (the XML solution format; converted from `OldTanks.sln` at the end of OT-3 with `dotnet sln OldTanks.sln migrate`, SDK 10.0.401, at Denys's request; the same eight projects, the three test projects in the `tests` solution folder). The IDE needs to read `.slnx`; the .NET SDK used here builds and tests it. From the repository root:

```powershell
dotnet build OldTanks.slnx
```

**Baseline (2026-11-25, `--no-incremental`, `bug/OT-7` on `master` with OT-11; OT-7's `CoolEngine.Tests` adds no warning):** 0 errors, 136 warnings (139 after OT-3; `master` before OT-3 had 165):

| Warnings | Count | Meaning |
|----------|-------|---------|
| `CS86xx`, `CS8714`, `CS8767` | most of the 135 compiler warnings | The nullable set, mostly `CS8618` (non-nullable field not set in the constructor); `CS8714` is one in `Common/Extensions/DictionaryExtensions.cs`; `CS8767` is `OldTanks.Models.WorldObject.Collision` against `CoolEngine.Services.Interfaces.ICollisionable.Collision`. The generated binding file has none since OT-11 ([KI-021](KNOWN-ISSUES.md#ki-021-generated-binding-code-calls-equals-before-its-null-check)) |
| `CS0162`, `CS0219` | the rest of the 135 | Unreachable code (the disabled stress spawner) and an unused variable |
| Six Labors license notice | 1 | From `tests/CoolEngine.Imaging.Tests`, which references ImageSharp 4.1.4 (see the Release note below) |

Gone in OT-3: `NU1902`, `NU1903` (ImageSharp 3.1.3 advisories), `NETSDK1138` (`net7.0` out of support), `CS0618`, `CA2024`, `CS8669`, `RS1035`, `RS1042`. Gone in OT-11: the generated binding file's 3 × `CS8602`. `dotnet list package --vulnerable --include-transitive` reports no vulnerable package (2026-11-25). `CoolEngine.Imaging` and its tests add no compiler warnings.

An incremental build that compiles nothing shows only the restore and SDK warnings. New code should not add to the baseline.

**Output:** `OldTanks/bin/Debug/net10.0/`, with `Assets/` copied next to `OldTanks.exe` on every build. The source generator runs inside this build; to read its output add `--no-incremental -p:EmitCompilerGeneratedFiles=true` (a build with nothing to compile writes no file, [BINDING-GENERATOR §5](OldTanks.UI.SourceGenerators/BINDING-GENERATOR.md#5-changing-the-generator)).

**Release builds of the solution need a Six Labors license key.** `tests/CoolEngine.Imaging.Tests` references SixLabors.ImageSharp 4.1.4, whose unaffected versions need a key at build time: a Debug build warns, a Release build fails until a `sixlabors.lic` file or the `SixLaborsLicenseKey` property supplies one (the Debug build's warning says "Release builds require a valid license" and names `SixLaborsLicenseKey`, `SixLaborsLicenseFile` and `sixlabors.lic`; a Release build was not tried). Building only the runtime projects is not affected. A community key is a [BACKLOG](BACKLOG.md#platform-and-build) item.

**Tests** (2026-11-25): `tests/OldTanks.Docs.Tests` (NUnit, `net10.0`, 31 tests) checks the documentation ([DOCUMENTATION-GUIDE §9](DOCUMENTATION-GUIDE.md#9-checks)); `tests/CoolEngine.Imaging.Tests` (NUnit, `net10.0-windows`, 247 tests: 98 PNG side, 149 JPEG) checks the image decoders against Windows and ImageSharp ([IMAGE-DECODERS §6](CoolEngine.Imaging/IMAGE-DECODERS.md#6-how-it-is-tested)); `tests/CoolEngine.Tests` (NUnit, `net10.0`, 16 tests, since OT-7) checks `CoolEngine.Services.Renderers.ObjectRenderer<T>`'s grouping, instancing records and colour: 294 in all (31 + 16 + 247, 2026-11-25). `CoolEngine.Tests` is **headless**: no OpenGL context and no window exist, so it tests only code that makes no GL call, and it builds `Texture` (private constructor) and `Shader` (uninitialized) by reflection with their finalizers suppressed, because their constructors or finalizers call OpenGL ([KI-017](KNOWN-ISSUES.md#ki-017-gl-objects-are-deleted-from-finalizers)); its seams are `internal` members of `CoolEngine`, visible through `InternalsVisibleTo`. Before the OT-7 fix 10 of its 16 tests failed. There are no tests of the game project yet ([BACKLOG](BACKLOG.md#platform-and-build)). Run them from the repository root:

```powershell
dotnet test OldTanks.slnx
```

## 3. Run

The game resolves `Assets/` against the **working directory**, not against the executable ([ASSET-PIPELINE §1](CoolEngine/Services/Loaders/ASSET-PIPELINE.md#1-where-assets-are-found)). Start it from the build output:

```powershell
Set-Location OldTanks\bin\Debug\net10.0
.\OldTanks.exe
```

- **Run on 2026-11-25** (a smoke run Denys allowed, one more for a console capture and a screenshot, and Denys's own run): the game starts on .NET 10 with OpenTK 4.9.4/GLFW and an OpenGL 4.6 core context (NVIDIA 621.24). The walls and floor are textured the right way up by `CoolEngine.Imaging`; the ImGui debug window draws and shows text; the console showed no exception, texture or font-loading error. **Not working, all older than OT-3:** the font and skybox shaders fail to compile and are used anyway, so there is no skybox and no HUD text (a garbled strip appears near the scene's centre instead) and about 45,000 GL error lines a second reach the console and `log.txt` ([KI-020](KNOWN-ISSUES.md#ki-020-three-shaders-fail-to-compile-and-are-used-anyway)); two `InitCollision error` lines appear at startup ([KI-022](KNOWN-ISSUES.md#ki-022-initcollision-prints-an-error-twice-at-startup)). The run also confirmed [KI-004](KNOWN-ISSUES.md#ki-004-loggerservice-ignores-the-log-file-name) and [KI-008](KNOWN-ISSUES.md#ki-008-unknown-shader-names-resolve-to-location-0). Not checked in the run: the font port visually (blocked by KI-020), typing and dragging in the ImGui windows, the texture window.
- **Run on 2026-11-25 for OT-7** (Claude, with computer use, Denys allowed; NVIDIA 621.24, OpenGL 4.6 core). *Before the fix* (`master`'s code on `bug/OT-7`): a spawned cube drew pure white ([KI-005](KNOWN-ISSUES.md#ki-005-untextured-objects-use-a-0-255-color-in-a-0-1-color-space)); after 17 or more spawns the console logged `Buffer ArrayBuffer object with 11 created.` (the group went instanced); giving one spawned cube the Brick texture in the texture window ended the game with `KeyNotFoundException: The given key 'OldTanks.Models.Cube' was not present in the dictionary.` in `ObjectRenderer<T>.RenderInstanced` (the retexture crash, [RENDERING §2](CoolEngine/Services/Renderers/RENDERING.md#2-objectrenderert-batching-and-instancing)). *After the fix:* spawned cubes draw rose, also after 18 spawns on the instanced path; retexturing one to Brick kept the game running, and that cube, moved up in the debug window, drew with the brick texture while the others stayed rose; exit code 0, no exception. Hidden objects ([KI-009](KNOWN-ISSUES.md#ki-009-instanced-rendering-skips-the-wrong-objects-when-some-are-hidden)) cannot be shown in the game (nothing sets `Visible = false`); the unit tests cover them. **Run notes:** ImGui reads the mouse by polling, so a scripted click must hold the button across frames (a press and release within one frame is missed); Escape closes the game; `log.txt` grows by about 0.5 GB a minute while [KI-020](KNOWN-ISSUES.md#ki-020-three-shaders-fail-to-compile-and-are-used-anyway) is open, so delete it after a run.
- **Started from a folder without `Assets/`** (for example `dotnet run --project OldTanks` from the repository root, which keeps the shell's folder), the window opens and stays empty with no error ([KI-012](KNOWN-ISSUES.md#ki-012-startup-continues-after-a-failed-asset-load)). IDE launches use the output folder by default and work.
- A console window opens with the game and shows the log. The window title is "Test. OpenGL: <driver version>". Controls: [features §3](OldTanks/FEATURES-OVERVIEW.md#3-controls).

## 4. Files the game reads and writes

| File | Where | Content |
|------|-------|---------|
| `AppSettings.json` | `%USERPROFILE%\OldTanksProject\` (created on first run; the 2026-11-25 run created it on this PC) | `OldTanks.DataModels.Settings`, read at start and written back every start |
| `log.txt` | The working directory | Serilog, level `Debug`; also written to the console ([KI-004](KNOWN-ISSUES.md#ki-004-loggerservice-ignores-the-log-file-name)). Grows by tens of thousands of lines a second while [KI-020](KNOWN-ISSUES.md#ki-020-three-shaders-fail-to-compile-and-are-used-anyway) is open |

**Settings and defaults** (`OldTanks.Program.CreateDefaultSettings`):

| Setting | Default | Used for |
|---------|---------|----------|
| `AssetPath` | `Assets` | Root of every asset folder |
| `ShadersDirectory`, `TexturesDirectory`, `SkyBoxesDirectory`, `ModelsDirectory`, `FontsDirectory` | `Shaders`, `Textures`, `Textures\SkyBoxes`, `Models`, `Fonts` | Asset folders under `AssetPath` |
| `FullScreen` | `false` | Start in full screen |
| `Width`, `Height` | 800, 600 | Saved, not applied ([KI-013](KNOWN-ISSUES.md#ki-013-window-size-settings-are-ignored)) |
| `Sensitivity` | 0.1 (clamped 0-10) | Mouse look |

Delete the file to get the defaults back. Run from the build output, nothing lands in tracked folders (`bin/` and `obj/` are git-ignored); started from the repository root or the `OldTanks/` folder, the game leaves an untracked `log.txt` there, which `.gitignore` does not cover.
