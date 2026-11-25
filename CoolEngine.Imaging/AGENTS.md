# CoolEngine.Imaging

> **Project knowledge:** [../docs/PROJECT-KNOWLEDGE.md](../docs/PROJECT-KNOWLEDGE.md)

Strict PNG and JPEG decoders into RGBA8 pixels, with no package dependencies. Added in OT-3 to replace SixLabors.ImageSharp in the engine.

## Related Docs

| Topic | Page |
|-------|------|
| Public API, formats, the strict rules and their two exceptions, limits, threading, how it is tested | [IMAGE-DECODERS](../docs/CoolEngine.Imaging/IMAGE-DECODERS.md) |
| Where the engine's texture loaders call it | [ASSET-PIPELINE](../docs/CoolEngine/Services/Loaders/ASSET-PIPELINE.md) |
| The tests against Windows and ImageSharp | [IMAGE-DECODERS §6](../docs/CoolEngine.Imaging/IMAGE-DECODERS.md#6-how-it-is-tested) |
| Known defects here | [KNOWN-ISSUES](../docs/KNOWN-ISSUES.md): KI-023 |

## Project Details

| Property | Value |
|----------|-------|
| Namespace root | `CoolEngine.Imaging` |
| Target framework | `net10.0`, `LangVersion latest` |
| Layer | Engine helper; referenced by `CoolEngine`; tested by `tests/CoolEngine.Imaging.Tests` (`InternalsVisibleTo`) |

## Key Types

| Type | Role |
|------|------|
| `ImageDecoder` | The public entry: `DetectFormat`, `Decode`, `Load`, `LoadAsync`, `MaxPixelCount` |
| `RgbaImage`, `Rgba32`, `ImageFormat` | The result (width, height, row-major pixels), one RGBA pixel, the detected format |
| `Png.PngDecoder`, `Png.Crc32` | Internal PNG decoder (zlib through `ZLibStream`) and its CRC |
| `Jpeg.JpegDecoder`, `Jpeg.HuffmanTable`, `Jpeg.Idct`, `Jpeg.JpegComponent` | Internal baseline and progressive Huffman JPEG decoder |

## Dependencies

| Dependency | Reason |
|------------|--------|
| none | No project and no package reference, on purpose: managed code only, so it can run in WebAssembly later ([plan](../docs/plans/ENGINES-AND-TANK-GAME.md#6-decisions-of-ot-3-and-the-planned-tasks)) |

## Constraints

- **No OpenGL, no native code, no packages.** The engine uploads the pixels; this project never touches the GPU.
- **Strict:** nonconforming or damaged data throws `InvalidDataException`, valid data beyond a limit or feature `NotSupportedException`; do not add leniency. The only accepted deviations are the two documented ones ([IMAGE-DECODERS §3](../docs/CoolEngine.Imaging/IMAGE-DECODERS.md#3-the-strict-rules)).
- **Valid files stay bit-identical to Windows**; a change that moves a pixel on an asset fails the tests. Never loosen a test tolerance: report the measured figures.
- **Decoders are stateless and thread-safe**; keep them so (the loaders decode on the thread pool).
- **Size check before allocating:** every decoder calls `ImageDecoder.ValidateSize` before allocating pixels.
