# OldTanks

A hobby 3D game in C# on its own OpenGL engine, **CoolEngine**. There are some simple physics and collisions: today the game is an engine sandbox, a small scene of walls, a floor, cubes and spheres that fall and collide, a free-fly camera, and ImGui debug windows to inspect and edit every object. Tanks are the goal; they are not built yet.

**Engine features:** OpenGL 4.6 rendering with per-texture batching and instanced drawing, skybox, screen and world-space text (FreeType), Wavefront OBJ models, rigid bodies with gravity, separating-axis collision tests for boxes and spheres, ray picking, a dispatcher that keeps OpenGL work on the window thread, and a Roslyn source generator for two-way UI bindings.

## Requirements

- Windows, and a GPU driver with OpenGL 4.6
- The .NET 10 SDK to build and the .NET 10 runtime to run it

## Build and run

```powershell
dotnet build OldTanks.slnx
```

```powershell
Set-Location OldTanks\bin\Debug\net10.0
.\OldTanks.exe
```

Start the game from a folder that contains `Assets/` (the build output does); otherwise the window stays empty. Settings are kept in `%USERPROFILE%\OldTanksProject\AppSettings.json` and the log in `log.txt` next to where the game was started. More in [docs/BUILD.md](docs/BUILD.md).

## Controls

| Input | Effect |
|-------|--------|
| Mouse button held + move | Look around |
| Mouse wheel | Zoom |
| `W` `A` `S` `D`, `Space`, `Left Shift` | Fly: forward, left, back, right, up, down |
| `Left Ctrl` (hold) | Fly faster |
| `0` | Collision debug view |
| `N` | Show normals |
| `U` / `I` | Pause physics / advance one step |
| `R` | Camera back to the origin |
| `Alt` + `Enter` | Full screen |
| `Esc` | Quit |

The **Debug window** spawns, deletes and edits objects; its **Show texture window** button opens the texture picker. Full list: [docs/OldTanks/FEATURES-OVERVIEW.md](docs/OldTanks/FEATURES-OVERVIEW.md).

## Projects

| Project | Role |
|---------|------|
| `OldTanks` | The game: window, world, objects, debug windows, `Assets/` |
| `CoolEngine` | Rendering, asset loading, physics, threading |
| `CoolEngine.Imaging` | PNG and JPEG decoders, no dependencies |
| `OldTanks.UI` | HUD text, ImGui control wrappers, ImGui renderer |
| `OldTanks.UI.SourceGenerators` | Source generator for the debug window's bindings |
| `Common` | Shared helpers |
| `tests/OldTanks.Docs.Tests` | Documentation checks (`dotnet test OldTanks.slnx`) |
| `tests/CoolEngine.Tests` | Headless unit tests of the engine (today the `ObjectRenderer<T>` grouping and instancing) |
| `tests/CoolEngine.Imaging.Tests` | The image decoders against Windows and ImageSharp (Windows only; a Release build needs a Six Labors key, see [docs/BUILD.md](docs/BUILD.md#2-build)) |

## Documentation

The knowledge base is in [docs/](docs/README.md): architecture, rendering, assets, physics, UI, the known-issues register and the backlog. Coding agents start at [AGENTS.md](AGENTS.md).

## Contributing

Branch from `master` as `feature/OT-<n>` or `bug/OT-<n>` and open the pull request into `master`. Conventions: [docs/CODE-STYLE.md](docs/CODE-STYLE.md).
