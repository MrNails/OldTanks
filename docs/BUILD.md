# Build and Run

| Field        | Value  |
|--------------|--------|
| Status       | Living |
| Last updated | 2026-11-24 |
| Verified against | 2026-11-24; develop at d32cbbf; §1-§2 built on Windows 11 with .NET SDK 10.0.401, §3 not run |

---

## 1. Requirements

| Need | Why |
|------|-----|
| Windows | The only platform the game has been developed on; the natives (GLFW through OpenTK, cimgui, FreeType) are not checked elsewhere |
| A GPU driver with **OpenGL 4.6** core | The window asks for a 4.6 forward-compatible debug context and every shader is `#version 460 core` |
| A .NET SDK that can target `net7.0` | Any SDK from 7 up; SDK 10 builds it with warning `NETSDK1138` (target out of support) |
| The **.NET 7 runtime** to run it | All four runtime projects target `net7.0`; see §3 when it is missing |
| The **.NET 10 SDK** for the tests | `tests/OldTanks.Docs.Tests` targets `net10.0` (§2) |

## 2. Build

From the repository root:

```powershell
dotnet build OldTanks.sln
```

**Baseline (2026-11-24, `--no-incremental`):** 0 errors, 165 warnings:

| Warnings | Count | Meaning |
|----------|-------|---------|
| `CS86xx` | 137 | Nullable reference warnings, mostly `CS8618` (non-nullable field not set in the constructor) |
| `NU1903`, `NU1902` | 12 + 12 | SixLabors.ImageSharp 3.1.3 has known high and moderate vulnerabilities ([BACKLOG](BACKLOG.md#platform-and-build)) |
| `NETSDK1138` | 2 | `net7.0` is out of support |
| `CS0162`, `CS0219` | 1 + 1 | Unreachable code (the disabled stress spawner) and an unused variable |

An incremental build that compiles nothing shows only the restore and SDK warnings. New code should not add to the baseline.

**Output:** `OldTanks/bin/Debug/net7.0/`, with `Assets/` copied next to `OldTanks.exe` on every build. The source generator runs inside this build; to read its output add `-p:EmitCompilerGeneratedFiles=true` ([BINDING-GENERATOR §1](OldTanks.UI.SourceGenerators/BINDING-GENERATOR.md#1-how-it-is-wired)).

**Tests:** `tests/OldTanks.Docs.Tests` (NUnit, `net10.0`) checks the documentation ([DOCUMENTATION-GUIDE §9](DOCUMENTATION-GUIDE.md#9-checks)); there are no tests of the game code yet ([BACKLOG](BACKLOG.md#platform-and-build)). Run them from the repository root; the project adds no warnings to the baseline:

```powershell
dotnet test OldTanks.sln
```

## 3. Run

The game resolves `Assets/` against the **working directory**, not against the executable ([ASSET-PIPELINE §1](CoolEngine/Services/Loaders/ASSET-PIPELINE.md#1-where-assets-are-found)). Start it from the build output:

```powershell
Set-Location OldTanks\bin\Debug\net7.0
.\OldTanks.exe
```

- **Not run on 2026-11-24:** the PC had the .NET 8, 9 and 10 runtimes and no .NET 7. To try a newer runtime without retargeting, set `$env:DOTNET_ROLL_FORWARD = 'Major'` in that shell first; this was not tested. The lasting fix is a retarget ([BACKLOG](BACKLOG.md#platform-and-build)).
- **Started from a folder without `Assets/`** (for example `dotnet run --project OldTanks` from the repository root, which keeps the shell's folder), the window opens and stays empty with no error ([KI-012](KNOWN-ISSUES.md#ki-012-startup-continues-after-a-failed-asset-load)). IDE launches use the output folder by default and work.
- A console window opens with the game and shows the log. The window title is "Test. OpenGL: <driver version>". Controls: [features §3](OldTanks/FEATURES-OVERVIEW.md#3-controls).

## 4. Files the game reads and writes

| File | Where | Content |
|------|-------|---------|
| `AppSettings.json` | `%USERPROFILE%\OldTanksProject\` (created on first run) | `OldTanks.DataModels.Settings`, read at start and written back every start |
| `log.txt` | The working directory | Serilog, level `Debug`; also written to the console ([KI-004](KNOWN-ISSUES.md#ki-004-loggerservice-ignores-the-log-file-name)) |

**Settings and defaults** (`OldTanks.Program.CreateDefaultSettings`):

| Setting | Default | Used for |
|---------|---------|----------|
| `AssetPath` | `Assets` | Root of every asset folder |
| `ShadersDirectory`, `TexturesDirectory`, `SkyBoxesDirectory`, `ModelsDirectory`, `FontsDirectory` | `Shaders`, `Textures`, `Textures\SkyBoxes`, `Models`, `Fonts` | Asset folders under `AssetPath` |
| `FullScreen` | `false` | Start in full screen |
| `Width`, `Height` | 800, 600 | Saved, not applied ([KI-013](KNOWN-ISSUES.md#ki-013-window-size-settings-are-ignored)) |
| `Sensitivity` | 0.1 (clamped 0-10) | Mouse look |

Delete the file to get the defaults back. Run from the build output, nothing lands in tracked folders (`bin/` and `obj/` are git-ignored); started from the repository root or the `OldTanks/` folder, the game leaves an untracked `log.txt` there, which `.gitignore` does not cover.
