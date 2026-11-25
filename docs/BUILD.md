# Build and Run

| Field        | Value  |
|--------------|--------|
| Status       | Living |
| Last updated | 2026-11-25 |
| Verified against | 2026-11-25; bug/OT-9 on master at d749ec8 (§2 build and test counts: `--no-incremental` 0 errors, 131 warnings, 308 tests passed; §3's OT-9 run and §4's size setting and `imgui.ini` observed in the game on 2026-11-25); bug/OT-8 rebased on master at 826e560 (OT-11 and OT-7 merged); bug/OT-6 rebased on master at 6753e46 (OT-8 merged) for the OT-6 runs in §3; §1-§2 built and tested on Windows 11 with .NET SDK 10.0.401, §3 run on 2026-11-25 (the OT-3 smoke runs, Denys's own run, the two OT-7 runs, the OT-8 smoke run, the two OT-6 runs and the OT-9 app check), §4 as of 2026-11-24 except the settings file, `log.txt` and `imgui.ini` |

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

**Baseline (2026-11-25, `--no-incremental`, `bug/OT-9` on `master` at `d749ec8`, with OT-11, OT-7, OT-8 and OT-6; `CoolEngine.Tests` adds no warning):** 0 errors, 131 warnings (136 on `master`, 139 after OT-3; `master` before OT-3 had 165). OT-9 removed five `CS8604` ("possible null argument for parameter `texture`") at the `FillObject` calls, because `OldTanks.Windows.MainWindow.FillObject` now takes a `Texture?`:

| Warnings | Count | Meaning |
|----------|-------|---------|
| `CS86xx`, `CS8714`, `CS8767` | most of the 130 compiler warnings | The nullable set, mostly `CS8618` (non-nullable field not set in the constructor); `CS8714` is one in `Common/Extensions/DictionaryExtensions.cs`; `CS8767` is `OldTanks.Models.WorldObject.Collision` against `CoolEngine.Services.Interfaces.ICollisionable.Collision`. The generated binding file has none since OT-11 ([KI-021](KNOWN-ISSUES.md#ki-021-generated-binding-code-calls-equals-before-its-null-check)) |
| `CS0162`, `CS0219` | the rest of the 130 | Unreachable code (the disabled stress spawner) and an unused variable |
| Six Labors license notice | 1 | From `tests/CoolEngine.Imaging.Tests`, which references ImageSharp 4.1.4 (see the Release note below) |

Gone in OT-3: `NU1902`, `NU1903` (ImageSharp 3.1.3 advisories), `NETSDK1138` (`net7.0` out of support), `CS0618`, `CA2024`, `CS8669`, `RS1035`, `RS1042`. Gone in OT-11: the generated binding file's 3 × `CS8602`. `dotnet list package --vulnerable --include-transitive` reports no vulnerable package (2026-11-25). `CoolEngine.Imaging` and its tests add no compiler warnings.

An incremental build that compiles nothing shows only the restore and SDK warnings. New code should not add to the baseline.

**Output:** `OldTanks/bin/Debug/net10.0/`, with `Assets/` copied next to `OldTanks.exe` on every build. The source generator runs inside this build; to read its output add `--no-incremental -p:EmitCompilerGeneratedFiles=true` (a build with nothing to compile writes no file, [BINDING-GENERATOR §5](OldTanks.UI.SourceGenerators/BINDING-GENERATOR.md#5-changing-the-generator)).

**Release builds of the solution need a Six Labors license key.** `tests/CoolEngine.Imaging.Tests` references SixLabors.ImageSharp 4.1.4, whose unaffected versions need a key at build time: a Debug build warns, a Release build fails until a `sixlabors.lic` file or the `SixLaborsLicenseKey` property supplies one (the Debug build's warning says "Release builds require a valid license" and names `SixLaborsLicenseKey`, `SixLaborsLicenseFile` and `sixlabors.lic`; a Release build was not tried). Building only the runtime projects is not affected. A community key is a [BACKLOG](BACKLOG.md#platform-and-build) item.

**Tests** (2026-11-25): `tests/OldTanks.Docs.Tests` (NUnit, `net10.0`, 31 tests) checks the documentation ([DOCUMENTATION-GUIDE §9](DOCUMENTATION-GUIDE.md#9-checks)); `tests/CoolEngine.Imaging.Tests` (NUnit, `net10.0-windows`, 247 tests: 98 PNG side, 149 JPEG) checks the image decoders against Windows and ImageSharp ([IMAGE-DECODERS §6](CoolEngine.Imaging/IMAGE-DECODERS.md#6-how-it-is-tested)); `tests/CoolEngine.Tests` (NUnit, `net10.0`, 30 tests) checks `CoolEngine.Services.Renderers.ObjectRenderer<T>`'s grouping, instancing records and colour (16 tests, since OT-7) and the dispatcher (14 tests in `CoolEngine.Tests.Services.Threading.DispatcherTests` and `DispatcherSynchronizationContextTests`, since OT-8; `TestSupport/CollectingSink.cs` is a Serilog sink they swap into `Log.Logger`): 308 in all (31 + 30 + 247, 2026-11-25). `CoolEngine.Tests` is **headless**: no OpenGL context and no window exist, so it tests only code that makes no GL call, and it builds `Texture` (private constructor) and `Shader` (uninitialized) by reflection with their finalizers suppressed, because their constructors or finalizers call OpenGL ([KI-017](KNOWN-ISSUES.md#ki-017-gl-objects-are-deleted-from-finalizers)); its seams are `internal` members of `CoolEngine`, visible through `InternalsVisibleTo`. Before the OT-7 fix 10 of its 16 tests failed. There are no tests of the game project yet ([BACKLOG](BACKLOG.md#platform-and-build)). Run them from the repository root:

```powershell
dotnet test OldTanks.slnx
```

## 3. Run

The game resolves `Assets/` against the **working directory**, not against the executable ([ASSET-PIPELINE §1](CoolEngine/Services/Loaders/ASSET-PIPELINE.md#1-where-assets-are-found)). Start it from the build output:

```powershell
Set-Location OldTanks\bin\Debug\net10.0
.\OldTanks.exe
```

- **Run on 2026-11-25** (a smoke run Denys allowed, one more for a console capture and a screenshot, and Denys's own run): the game starts on .NET 10 with OpenTK 4.9.4/GLFW and an OpenGL 4.6 core context (NVIDIA 621.24). The walls and floor are textured the right way up by `CoolEngine.Imaging`; the ImGui debug window draws and shows text; the console showed no exception, texture or font-loading error. **Found then, older than OT-3, closed since on `bug/OT-6`:** the font and skybox shaders failed to compile and were used anyway, so there was no skybox and no HUD text (a garbled strip near the scene's centre instead) and about 45,000 GL error lines a second reached the console and `log.txt` ([KI-020](KNOWN-ISSUES.md#ki-020-three-shaders-fail-to-compile-and-are-used-anyway)). **Still present:** two `InitCollision error` lines appear at startup ([KI-022](KNOWN-ISSUES.md#ki-022-initcollision-prints-an-error-twice-at-startup)). The run also confirmed [KI-004](KNOWN-ISSUES.md#ki-004-loggerservice-ignores-the-log-file-name) (closed on `bug/OT-8`, below) and [KI-008](KNOWN-ISSUES.md#ki-008-unknown-shader-names-resolve-to-location-0) (closed on `bug/OT-6`). Not checked in the run: typing and dragging in the ImGui windows, the texture window.
- **Run on 2026-11-25 for OT-7** (Claude, with computer use, Denys allowed; NVIDIA 621.24, OpenGL 4.6 core). *Before the fix* (`master`'s code on `bug/OT-7`): a spawned cube drew pure white ([KI-005](KNOWN-ISSUES.md#ki-005-untextured-objects-use-a-0-255-color-in-a-0-1-color-space)); after 17 or more spawns the console logged `Buffer ArrayBuffer object with 11 created.` (the group went instanced); giving one spawned cube the Brick texture in the texture window ended the game with `KeyNotFoundException: The given key 'OldTanks.Models.Cube' was not present in the dictionary.` in `ObjectRenderer<T>.RenderInstanced` (the retexture crash, [RENDERING §2](CoolEngine/Services/Renderers/RENDERING.md#2-objectrenderert-batching-and-instancing)). *After the fix:* spawned cubes draw rose, also after 18 spawns on the instanced path; retexturing one to Brick kept the game running, and that cube, moved up in the debug window, drew with the brick texture while the others stayed rose; exit code 0, no exception. Hidden objects ([KI-009](KNOWN-ISSUES.md#ki-009-instanced-rendering-skips-the-wrong-objects-when-some-are-hidden)) cannot be shown in the game (nothing sets `Visible = false`); the unit tests cover them. **Run notes:** ImGui reads the mouse by polling, so a scripted click must hold the button across frames (a press and release within one frame is missed); Escape closes the game; `log.txt` grows by about 0.5 GB a minute while [KI-020](KNOWN-ISSUES.md#ki-020-three-shaders-fail-to-compile-and-are-used-anyway) is open, so delete it after a run.
- **Smoke run of `bug/OT-8`, 2026-11-25** (Denys allowed it; `OldTanks.exe` from `OldTanks\bin\Debug\net10.0` for 12 s): `log.txt` had 164,445 lines (37 MB, the KI-020 flood): 14 `[ShaderLoader]` lines (including the KI-020 compile errors, which never reached the file before), 4 `[GameManager]` lines ("Model … loaded."), the rest `[OldTanks]`; the console had the same counts; no `ShaderLog.txt`, `TextureLog.txt` or `WaveFontLog.txt` was created; no exception. The `ImGuiController`'s lines (including its ImGui shader compile messages) are tagged `[OldTanks]` because `OldTanks.Windows.MainWindow.OnLoad` calls `CreateLogger()` without a source; passing `nameof(ImGuiController)` there is a follow-up for after OT-9. The two `InitCollision` lines ([KI-022](KNOWN-ISSUES.md#ki-022-initcollision-prints-an-error-twice-at-startup)) still go to the console only (`Console.WriteLine`).
- **OT-6 runs on 2026-11-25** (`OldTanks.exe`, console captured): a red run at the KI-008 commit (the link check without the shader fix, from a temporary worktree) showed `3DFontShader` fail with `C1105` and "failed to link", no other shader loading, a blank window, a 2-line `log.txt` and no GL error flood ([RENDERING §3](CoolEngine/Services/Renderers/RENDERING.md#3-gpu-side-types)); the final run at the branch tip (14 s, screenshot) compiled and linked all 8 programs (ImGui and the 7 shader folders) with Information lines only and showed the skybox, the HUD text at the top left and the readable world label "Wall 2"; the console had 61 lines (the two KI-022 lines and NVIDIA buffer notifications, no errors) and `log.txt` 27 lines, where it had 552,473 lines in 12 s before the fix.
- **Run on 2026-11-25 for OT-9** (Claude, with computer use, at Denys's request "You run it"; NVIDIA 621.24, OpenGL 4.6 core, Windows at 100 % scaling; the code of `bug/OT-9`). *Window size* ([KI-013](KNOWN-ISSUES.md#ki-013-window-size-settings-are-ignored)): Width 1280 and Height 720 gave a client area of 1280 × 720 (`GetClientRect`); Width 0 gave 800 × 600 and the console line `[WRN] Window size 0x720 in the settings is not valid; using 800x600.`, and the file was left as it was. *ImGui input* ([KI-014](KNOWN-ISSUES.md#ki-014-mouse-look-ignores-imgui-focus)): dragging the debug window by its title bar, dragging a value (Push force X 0 to 107) and moving the texture window did not turn the camera; a drag in the 3D view did (CRotation 0, 0, 0 to 0.991, 0, 0.137). The wheel over the World objects list scrolled the list without zooming; over the 3D view it zoomed. With the texture window's Asset path box active, `W` held for 1 s and typing `asdru0` left CPosition at 0, 0, 0 and did not switch to the collision view; `Esc` ended the edit (the text reverted) and did not quit. With no field active, `W` moved the camera (CPosition X 3.071) and `Esc` quit. *Bad texture* ([KI-012](KNOWN-ISSUES.md#ki-012-startup-continues-after-a-failed-asset-load)): a non-image `bad.bmp` in the build output's `Assets\Textures` logged `[ERR] Error loading texture bad.` with `NotSupportedException: Unknown image format: only PNG and JPEG are supported.`, and the scene loaded with its textures. *Without `Assets/`:* see the next bullet. The check found that the error window sized itself to its longest line, 810 px wide in an 800 px client area, cutting off the line ends and the close button; fixed in the same branch (`OldTanks.UI.ImGuiUI.StartupErrorWindow`), after which the window sat at 10, 10 with 780 × 191 in `imgui.ini`, every line wrapped, the close button inside the client area, and a click on the collapse arrow left it expanded. Clicks shorter than a frame were lost, as in the OT-7 run notes above.
- **Started from a folder without `Assets/`** (for example `dotnet run --project OldTanks` from the repository root, which keeps the shell's folder; run on 2026-11-25 as `dotnet OldTanks\bin\Debug\net10.0\OldTanks.dll` from the worktree root), the window opens and shows an ImGui window "Startup failed" that lists one `<ExceptionType>: <Message>` line per failed load (five `DirectoryNotFoundException` lines: `Shaders`, `Fonts`, `Textures`, `Textures\SkyBoxes`, `Models`); the same exceptions are logged as `[ERR] Startup failed.` to the console and `log.txt`, and `Esc` quits (since OT-9, [KI-012](KNOWN-ISSUES.md#ki-012-startup-continues-after-a-failed-asset-load)). The window fits the client area: placed 10 px inside it, as wide as the client area minus 20 px, with wrapped text ([FEATURES-OVERVIEW §5](OldTanks/FEATURES-OVERVIEW.md#5-debug-windows)). ImGui's default font covers Latin-1 only, so a non-Latin character in a path shows as `?` in the window; the log has the full text. IDE launches use the output folder by default and work. A texture or skybox file that fails to decode is skipped with an `Error loading texture` or `Error loading skybox` line instead ([ASSET-PIPELINE](CoolEngine/Services/Loaders/ASSET-PIPELINE.md)).
- A console window opens with the game and shows the log. The window title is "Test. OpenGL: <driver version>". Controls: [features §3](OldTanks/FEATURES-OVERVIEW.md#3-controls).

## 4. Files the game reads and writes

| File | Where | Content |
|------|-------|---------|
| `AppSettings.json` | `%USERPROFILE%\OldTanksProject\` (created on first run; the 2026-11-25 run created it on this PC) | `OldTanks.DataModels.Settings`, read at start and written back every start |
| `imgui.ini` | The working directory | ImGui's saved window positions and sizes, written when the game runs (observed 2026-11-25); started from the repository root it is left there untracked, like `log.txt` |
| `log.txt` | The working directory | Serilog, level `Debug`; also written to the console. One file for every logger: each line carries its source in brackets after the level (`[OldTanks]`, `[GameManager]`, `[ShaderLoader]`, `[TextureLoader]`, `[WaveFrontLoader]`, `[SkyBoxTextureLoader]`); there are no per-loader files since OT-8 ([ARCHITECTURE §4](ARCHITECTURE.md#4-startup-and-the-frame)). 27 lines in a 14 s run on `bug/OT-6`; it grew by tens of thousands of lines a second while [KI-020](KNOWN-ISSUES.md#ki-020-three-shaders-fail-to-compile-and-are-used-anyway) was open |

**Settings and defaults** (`OldTanks.Program.CreateDefaultSettings`):

| Setting | Default | Used for |
|---------|---------|----------|
| `AssetPath` | `Assets` | Root of every asset folder |
| `ShadersDirectory`, `TexturesDirectory`, `SkyBoxesDirectory`, `ModelsDirectory`, `FontsDirectory` | `Shaders`, `Textures`, `Textures\SkyBoxes`, `Models`, `Fonts` | Asset folders under `AssetPath` |
| `FullScreen` | `false` | Start in full screen |
| `Width`, `Height` | 800, 600 | Client size of the window since OT-9 ([KI-013](KNOWN-ISSUES.md#ki-013-window-size-settings-are-ignored); observed 2026-11-25: 1280 × 720 gave a 1280 × 720 client area); a value below 1 gives 800 × 600 for that start with a logged `Warning` ("Window size ... is not valid"), and the file is not changed |
| `Sensitivity` | 0.1 (clamped 0-10) | Mouse look |

Delete the file to get the defaults back. Run from the build output, nothing lands in tracked folders (`bin/` and `obj/` are git-ignored); started from the repository root or the `OldTanks/` folder, the game leaves an untracked `log.txt` there, which `.gitignore` does not cover.
