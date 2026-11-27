# CoolEngine.Fonts

> **Project knowledge:** [../docs/PROJECT-KNOWLEDGE.md](../docs/PROJECT-KNOWLEDGE.md)

Font loading, character mapping, outlines, metrics and coverage bitmaps with FreeType-identical hinting, as managed code with no dependencies: TrueType, OpenType-CFF and CFF2, collections, WOFF and WOFF2, variable fonts. Added in OT-4 to replace FreeTypeSharp in the engine.

## Related Docs

| Topic | Page |
|-------|------|
| Public API, formats and scope, the rules (strict, accepted, ours works, not supported), limits and budgets, threading, how it is tested, credits | [FONTS](../docs/CoolEngine.Fonts/FONTS.md) |
| Where the engine builds its glyph atlas from it | [ASSET-PIPELINE](../docs/CoolEngine/Services/Loaders/ASSET-PIPELINE.md) |
| The tests against FreeType 2.13.2 and DirectWrite | [FONTS §6](../docs/CoolEngine.Fonts/FONTS.md#6-how-it-is-tested) |
| The FreeType credit and Source Sans 3 licence | [FONTS §7](../docs/CoolEngine.Fonts/FONTS.md#7-credits) |
| Known defects here | [KNOWN-ISSUES](../docs/KNOWN-ISSUES.md): KI-037, KI-038 |

## Project Details

| Property | Value |
|----------|-------|
| Namespace root | `CoolEngine.Fonts` |
| Target framework | `net10.0`, `LangVersion latest` |
| Layer | Engine helper; referenced by `CoolEngine`; tested by `tests/CoolEngine.Fonts.Tests` (`InternalsVisibleTo`, Windows-only: FreeType and DirectWrite references) |

## Key Types

| Type | Role |
|------|------|
| `FontLoader`, `FontFile`, `FontFormat` | The public entry: `DetectFormat`, `Load`, `LoadAsync`, `MaxFileSize`; the faces of a file |
| `FontFace` | One immutable face: names, metrics in font units, `GetGlyphIndex`, `GetAdvanceWidth`, `GetSvgDocument`, `Axes`, `NamedInstances`, `WouldBeAutoHintedByFreeType`, `CreateScaledFont` |
| `ScaledFont`, `ScaledFontOptions`, `HintingMode`, `TrueTypeInterpreterVersion` | A face at one pixel size: `GetOutline`, `GetMetrics`, `Render`; one per thread |
| `GlyphOutline`, `OutlinePoint`, `GlyphMetrics`, `LineMetrics`, `GlyphBitmap`, `VariationAxis`, `NamedInstance`, `VariationCoordinate` | The value types of the API |
| `Sfnt.SfntReader`, `Sfnt.ParsedTables` | Internal: the table directory, every table's checks and the per-file memory and work budgets |
| `Sfnt.CmapTable`, `Sfnt.GlyfTable`, `Sfnt.SvgTable` and the other table classes | Internal: one parsed, checked table each |
| `Cff.CffTable`, `Cff.Type2Interpreter` | Internal: CFF and CFF2 structure and the charstring interpreter |
| `Woff.WoffDecoder`, `Woff.Woff2Decoder` | Internal: WOFF 1.0 and WOFF2 to an sfnt image |
| `Variations.*` | Internal: `fvar`, `avar`, `gvar`, `cvar`, `HVAR`, `VVAR`, `MVAR`, the item variation store |
| `Hinting.TrueType.TrueTypeHinter` and `Interpreter`, `Hinting.Cff.CffHinter` | Internal: the TrueType bytecode interpreter (v35 and v40) and the Adobe-engine CFF hinting |
| `Rasterization.CoverageRasterizer`, `Rasterization.GlyphRenderer` | Internal: the coverage rasteriser (FreeType's `smooth` renderer) and the bitmap box |

## Dependencies

| Dependency | Reason |
|------------|--------|
| none | No project and no package reference, on purpose: managed code only. FreeTypeSharp is referenced by `tests/CoolEngine.Fonts.Tests` only |

## Constraints

- **Identical to FreeType 2.13.2** for glyph indices, outlines, metrics, bitmap position and every coverage byte, hinted and unhinted; a change that moves a value fails the tests. Never loosen a check: report the figures and Denys decides ([FONTS §6](../docs/CoolEngine.Fonts/FONTS.md#6-how-it-is-tested)).
- **Strict by Denys's rules** ([FONTS §3](../docs/CoolEngine.Fonts/FONTS.md#3-the-rules)): `InvalidDataException` for nonconforming data that affects what is read or its bounds, `NotSupportedException` for a feature out of scope or a limit; table checksums are not verified; every accepted deviation is listed in the class remarks of `Sfnt.SfntReader` and its siblings, and one test per rule bites.
- **Every limit and budget has its measurement in the comment of its constant** ([FONTS §4](../docs/CoolEngine.Fonts/FONTS.md#4-limits-and-budgets)); a new loop over data from the file is charged to a budget before it runs.
- **Managed only, no OpenGL, no unsafe code, no packages.** The engine uploads the bitmaps; this project never touches the GPU.
- **`FontFace` is immutable and thread-safe; `ScaledFont` is not** (hinting state): one per thread.
- **The files that follow a FreeType source say so in their header**, and the FreeType credit stays in [FONTS §7](../docs/CoolEngine.Fonts/FONTS.md#7-credits); a new file that follows FreeType closely gets the same header.
- **Never feed a mutated or fuzzed font to the native FreeType** in the tests.
