# Build and Run

| Field        | Value  |
|--------------|--------|
| Status       | Living |
| Last updated | 2026-11-25 |
| Verified against | 2026-11-25; feature/OT-3, uncommitted; §1-§2 built and tested on Windows 11 with .NET SDK 10.0.401, §3 run on 2026-11-25 (the smoke run and Denys's own run), §4 as of 2026-11-24 except the settings file |

---

## 1. Requirements

| Need | Why |
|------|-----|
| Windows | The only platform the game has been developed on; the natives (GLFW through OpenTK, cimgui, FreeType) are not checked elsewhere; `tests/CoolEngine.Imaging.Tests` targets `net10.0-windows` (GDI+) |
| A GPU driver with **OpenGL 4.6** core | The window asks for a 4.6 forward-compatible debug context and every shader is `#version 460 core` |
| The **.NET 10 SDK** | Every project targets `net10.0` (the generator `netstandard2.0`), with `LangVersion latest`; built with SDK 10.0.401. The generator's Roslyn packages are held at 5.9.0, the compiler of that SDK ([ARCHITECTURE §1](ARCHITECTURE.md#1-solution-layout)) |
| The **.NET 10 runtime** to run the game | See §3 |

## 2. Build

The solution file is `OldTanks.slnx` (the XML solution format; converted from `OldTanks.sln` at the end of OT-3 with `dotnet sln OldTanks.sln migrate`, SDK 10.0.401, at Denys's request; the same eight projects, the two test projects in the `tests` solution folder). The IDE needs to read `.slnx`; the .NET SDK used here builds and tests it. From the repository root:

```powershell
dotnet build OldTanks.slnx
```

**Baseline (2026-11-25, `--no-incremental`, on the integrated OT-3 tree):** 0 errors, 139 warnings (`master` had 165):

| Warnings | Count | Meaning |
|----------|-------|---------|
| `CS86xx`, `CS8767` | most of the 138 compiler warnings | The nullable set, mostly `CS8618` (non-nullable field not set in the constructor); `CS8767` is `OldTanks.Models.WorldObject.Collision` against `CoolEngine.Services.Interfaces.ICollisionable.Collision`, and 3 × `CS8602` are in the generated binding file ([KI-021](KNOWN-ISSUES.md#ki-021-generated-binding-code-calls-equals-before-its-null-check)) |
| `CS0162`, `CS0219` | the rest of the 138 | Unreachable code (the disabled stress spawner) and an unused variable |
| Six Labors license notice | 1 | From `tests/CoolEngine.Imaging.Tests`, which references ImageSharp 4.1.4 (see the Release note below) |

Gone since `master`: `NU1902`, `NU1903` (ImageSharp 3.1.3 advisories), `NETSDK1138` (`net7.0` out of support), `CS0618`, `CA2024`, `CS8669`, `RS1035`, `RS1042`. `dotnet list package --vulnerable --include-transitive` reports no vulnerable package (2026-11-25). `CoolEngine.Imaging` and its tests add no compiler warnings.

An incremental build that compiles nothing shows only the restore and SDK warnings. New code should not add to the baseline.

**Output:** `OldTanks/bin/Debug/net10.0/`, with `Assets/` copied next to `OldTanks.exe` on every build. The source generator runs inside this build; to read its output add `-p:EmitCompilerGeneratedFiles=true` ([BINDING-GENERATOR §1](OldTanks.UI.SourceGenerators/BINDING-GENERATOR.md#1-how-it-is-wired)).

**Release builds of the solution need a Six Labors license key.** `tests/CoolEngine.Imaging.Tests` references SixLabors.ImageSharp 4.1.4, whose unaffected versions need a key at build time: a Debug build warns, a Release build fails until a `sixlabors.lic` file or the `SixLaborsLicenseKey` property supplies one (the Debug build's warning says "Release builds require a valid license" and names `SixLaborsLicenseKey`, `SixLaborsLicenseFile` and `sixlabors.lic`; a Release build was not tried). Building only the runtime projects is not affected. A community key is a [BACKLOG](BACKLOG.md#platform-and-build) item.

**Tests** (2026-11-25): `tests/OldTanks.Docs.Tests` (NUnit, `net10.0`, 31 tests) checks the documentation ([DOCUMENTATION-GUIDE §9](DOCUMENTATION-GUIDE.md#9-checks)); `tests/CoolEngine.Imaging.Tests` (NUnit, `net10.0-windows`, 247 tests: 98 PNG side, 149 JPEG) checks the image decoders against Windows and ImageSharp ([IMAGE-DECODERS §6](CoolEngine.Imaging/IMAGE-DECODERS.md#6-how-it-is-tested)): 278 in all. There are no tests of the game code yet ([BACKLOG](BACKLOG.md#platform-and-build)). Run them from the repository root:

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
