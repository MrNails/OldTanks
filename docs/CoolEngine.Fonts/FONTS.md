# Fonts

| Field        | Value  |
|--------------|--------|
| Status       | Living |
| Last updated | 2026-11-27 |
| Verified against | 2026-11-27; feature/OT-4 at 05e364f; whole page against the class remarks and constants of `CoolEngine.Fonts` and the figures recorded in the OT-4 plan (each work package's and review's measurements; the corpus sweeps were not re-run for this page) |

---

`CoolEngine.Fonts` loads font files, maps characters to glyphs and produces outlines, metrics and 8-bit coverage bitmaps, hinted the way FreeType 2.13.2 hints them. It is managed code with no dependencies and replaced FreeTypeSharp in the engine in OT-4 (2026-11-25 to 2026-11-27; Denys: formats "Everything excluding legacy and language specific fonts (arabic, indic, e.t.c)", hinting "Full hinting", 2026-11-24). Its output is **identical to FreeType 2.13.2's** wherever the two were compared (§6), and it is **strict** by Denys's rules (§3). The engine uses it in `CoolEngine.GraphicalEngine.Core.Font.Font` ([ASSET-PIPELINE §3](../CoolEngine/Services/Loaders/ASSET-PIPELINE.md#3-loader-details)); the guide is [CoolEngine.Fonts/AGENTS.md](../../CoolEngine.Fonts/AGENTS.md).

## 1. Public API

All in namespace `CoolEngine.Fonts`; everything under `Sfnt`, `Cff`, `Woff`, `Variations`, `Hinting` and `Rasterization` is internal.

| Type | Role |
|------|------|
| `FontLoader` | Static entry. `DetectFormat(ReadOnlySpan<byte>)` reads the signature. `Load(byte[])` and `Load(string)` parse a whole file; `LoadAsync(path, token)` reads asynchronously and parses in `Task.Run`. `MaxFileSize` is 128 MiB (also the limit for a decoded WOFF or WOFF2) |
| `FontFile` | `Format` (`FontFormat`: `TrueType`, `OpenTypeCff`, `Collection`, `Woff`, `Woff2`) and `Faces`, one `FontFace` per face (a collection gives several) |
| `FontFace` | One face, immutable. `FamilyName`, `StyleName`, `UnitsPerEm`, `GlyphCount`, `OutlineFormat` (`TrueType`, `Cff`, `Cff2`, `None` for a face with only bitmaps or SVG), `Ascender`, `Descender`, `Height` (font units, as FreeType's `FT_FaceRec`); `GetGlyphIndex(codePoint)` (0 when unmapped; FreeType's choice of Unicode map) and `GetGlyphIndex(codePoint, variationSelector)` (`cmap` format 14); `GetAdvanceWidth(glyphIndex)` (`hmtx`, font units, the default instance); `GetSvgDocument(glyphIndex)` (the glyph's SVG document, gzip removed, empty when none); `IsVariable`, `Axes` (`VariationAxis`), `NamedInstances` (`NamedInstance`); `WouldBeAutoHintedByFreeType` (a `glyf` face with no instructions at all that is not one of FreeType's tricky fonts: FreeType would hand it to its auto-hinter, this library renders it unhinted); `CreateScaledFont(ScaledFontOptions)` |
| `ScaledFontOptions` | `PixelSize` 1 to 4096 (`FT_Set_Pixel_Sizes(face, 0, PixelSize)`), `Hinting` (`HintingMode.Native` by default, or `None`), `Interpreter` (`TrueTypeInterpreterVersion.V40` by default, or `V35`), `Variation` (a list of `VariationCoordinate`, user-space values; axes not named stay at their default) |
| `ScaledFont` | A face at one size and one mode. `LineMetrics` (26.6, as `FT_Size_Metrics`), `GetMetrics(glyphIndex)` (`GlyphMetrics`, 26.6, the horizontal values of `FT_Glyph_Metrics`), `GetOutline(glyphIndex)` (`GlyphOutline`: `OutlinePoint` with an `OutlinePointKind` of on-curve, quadratic or cubic, plus contour end indices, 26.6), `Render(glyphIndex)` (`GlyphBitmap`: `Width`, `Height`, `Left`, `Top`, `Pixels` coverage with row 0 on top). `MaxGlyphPixelCount` is 16384² |

The default instance's `hmtx` advance is what `GetAdvanceWidth` returns: on a variable face DirectWrite's design advance follows a composite's `USE_MY_METRICS` component where `hmtx` does not, so DirectWrite is no reference for that value ([session log](../sessions/SESSION-LOG.md), OT-4). Metrics at a variation instance come from `ScaledFont`.

## 2. Formats and scope

| | |
|---|---|
| **Supported** | TrueType (`glyf`, sfnt version `0x00010000` or `true`), OpenType-CFF and CFF2 (`OTTO`), collections (`ttcf`, versions 1 and 2), WOFF 1.0 and WOFF2 (including transformed `glyf`, `loca` and `hmtx`, and collections); `cmap` formats 0, 4, 6, 10, 12, 13 and 14; the TrueType instruction interpreter, v35 and v40 (v40 the default); CFF and CFF2 hinting as FreeType's Adobe engine (blue zones and stem snapping, no stem darkening); variable fonts, all axes: `fvar`, `avar` version 1, `gvar`, `cvar`, `HVAR`, `VVAR`, `MVAR` and CFF2 `blend` |
| **Read but not drawn** | `COLR`/`CPAL` layers (a colour glyph draws its plain outline), `CBDT`/`CBLC`, `sbix`, `EBDT`/`EBLC` bitmaps (a glyph with only a bitmap is empty), `SVG ` documents (exposed by `GetSvgDocument` for the later SVG ticket). Denys (2026-11-25): "SVG renderer we needed, because we will load SVG files too. But move SVG renderer and loader to different ticket"; colour: "Color we will set from our settings. We read plain SVG without any color (only shape)"; the confirmation "Yes, as written (Recommended)": glyphs are coverage only, tinted by the game's colour |
| **Out of scope** | Shaping (kerning, ligatures, `kern`, `GPOS`, `GSUB`; Denys: "Glyph level only (Recommended)", a later task), colour rendering, vertical text, FreeType's auto-hinter, LCD and subpixel rendering, SDF, `avar` version 2 (`NotSupportedException`), `cmap` formats 2 and 8, fonts with only legacy or symbol `cmap`s (10 files of `C:\Windows\Fonts` are symbol-only and are refused with `NotSupportedException`), Multiple Master CFF, Type 1 (`typ1`), Apple bitmap-only (`bhed`) and `hvgl` faces |

## 3. The rules

Four kinds of outcome, by Denys's decisions. The authoritative lists are the class remarks of `CoolEngine.Fonts.Sfnt.SfntReader` (sfnt, `cmap`, `SVG `, load budgets), `CoolEngine.Fonts.Cff.CffTable`, `CoolEngine.Fonts.Woff.WoffDecoder` and `Woff2Decoder`, and the `Variations` classes; this page does not repeat them.

| Outcome | What it covers |
|---------|----------------|
| **Strict: `InvalidDataException` at load** | Whatever affects what the library reads or the bounds it reads within: offsets and lengths past the data; a table record overlapping another record without being the same one; a required table missing; `head`, `hhea`, `maxp`, `hmtx`, `name`, `post`, `loca` and `glyf` structure; a glyph's contours, flags, points, components (out of range, itself, a cycle); `cmap` subtable structure and a mapping to a glyph at or past `numGlyphs`; `SVG ` index and gzip document (header, CRC-32, ISIZE); CFF INDEX, DICT, charset, `FDSelect` and variation-store structure; the WOFF and WOFF2 "MUST" cases that OTS and DirectWrite reject; an `fvar` axis tag that is not printable or repeats; shared tables that would be read with different inputs. A hinting program error throws for that glyph only where FreeType's default mode fails (a failing `fpgm` or `prep` fails every hinted load of the size) |
| **Accepted (read as FreeType reads it)** | Table checksums are not verified; table records and `name` and `cmap` records in any order or repeated (the first record of a tag, FreeType's choice of name and `cmap`); a zero-length table counts as absent; `head` flags bit 15; the reserved bits of composite glyph flags; composites nested deeper than `maxp.maxComponentDepth` declares; an `OS/2` shorter than its version claims; the glyph names of `post`; a table version above the one known; a zlib stream that stops without its end marker (§4's known limits) |
| **Ours works where FreeType fails** | Each listed in the class remarks and tested to fail in FreeType and to work here: glyphs FreeType refuses with `Raster_Overflow` (105 real glyphs; overlapping glyphs wider than 8,191 pixels), outlines over 32,763 points, simple glyphs of 4,095 or more contours, a (0, 5) format-14 `cmap` listed last, a `glyf` face with a `CBDT` or `sbix` strike, a CFF2 `FDSelect` of format 4, FreeType's `SHZ` bug (clamped) |
| **`NotSupportedException`** | A format or feature out of scope (§2) and valid data past one of the library's limits (§4) |
| **Not checked** | `checkSumAdjustment`, the directory's search fields, table alignment, `cmap` subtables that can never be selected beyond their bounds, the tables this library does not read |

**Decisions behind them** (all Denys's, full record in the [session log](../sessions/SESSION-LOG.md)):

| Date | Decision |
|------|----------|
| 2026-11-25 | "Strict, like Imaging (Recommended)": a structural violation, a wrong table checksum or an out-of-range offset throws at load. A `cmap` mapping to a glyph id at or past `numGlyphs`: "Load error (Recommended)" (turned down: return 0 as FreeType does) |
| 2026-11-25 | Where FreeType itself fails: "Ours works, documented (Recommended)" (turned down: match FreeType's failures). Bit-identity forces function-by-function ports: "Accept for all of OT-4 (Recommended)": the files that follow FreeType closely say so in their headers (§7) |
| 2026-11-25 | Hinting errors: "Throw where FreeType throws (Recommended)" (turned down: pedantic mode, always throw) |
| 2026-11-26 | Composite reserved flag bits: "No rule" |
| 2026-11-26 | "Safety-strict (Recommended)": reject what affects what we read or the bounds; accept the rules stricter than FreeType and OTS that have no safety content. "Stop verifying (Recommended)": table checksums ignored (this reverses the first day's "Strict, like Imaging" for checksums only) |
| 2026-11-26 | Same-inputs rule for shared tables; strict overlap rule for table records, then for `cmap` subtables and SVG documents ("Rules + work budget"); `head` flags bit 15 "Accept it (Recommended)" |
| 2026-11-26 | W3C "MUST reject" cases of WOFF that OTS and DirectWrite reject and only FreeType accepts: "Reject, like the spec (Recommended)" |
| 2026-11-27 | `avar` anomalies "Apply as FreeType (Recommended)"; bad `fvar` tags "Reject at load (Recommended)"; a broken `HVAR`, `VVAR` or `MVAR` "Treat as absent, as FreeType (Recommended)"; repeated or unsorted `MVAR` tags "Follow FreeType (Recommended)" |

## 4. Limits and budgets

Every limit is `NotSupportedException` (the mentor's rule, 2026-11-25, as for `CoolEngine.Imaging`'s `MaxPixelCount`: a limit the library imposes on valid data is not nonconforming data), each with its measurement in the comment of its constant. A budget is a backstop set at about 100 times the largest figure measured on the Windows fonts and the test fonts, or at a floor, so no real font reaches it.

| Limit | Value | Measured maximum |
|-------|-------|------------------|
| `FontLoader.MaxFileSize` | 128 MiB, also for a decoded WOFF or WOFF2 | none |
| SVG inflate budget (`CoolEngine.Fonts.Sfnt.SvgTable.InflateBudget`) | 128 MiB per file, all faces and documents together | none (no local SVG font; real gzip documents compress at median 2.8:1, maximum 11.0:1) |
| Parsed memory (`ParsedTables.MaxParsedBytesPerFileByte`, `ParsedAllowance`) | 8 bytes per file byte plus 1 MiB | 0.889 per byte (`ROGFonts-Regular_0.otf`) over 198 files; the 1 MiB allowance was added in WP2's fix round for 1-2 KB CID subsets |
| Load work (`MaxWorkPerFileByte`, `WorkAllowance`) | 32 units per file byte plus 1 MiB (Denys: "Rules + work budget") | 1.70 per byte (`unispace bd.ttf`), median 1.12, over 200 files |
| `ScaledFont.MaxGlyphPixelCount` | 16384² pixels for one bitmap (Denys: "16384² + work cap (Recommended)") | none |
| Render work (`GlyphRenderer.WorkBudget`) | 1,000,000,000 units per glyph | 490,200,950 (`CascadiaCode.ttf`, glyph 1,387, 4096 px) over 1,767,308 glyphs |
| Composite glyphs | 255 levels, 65,535 components in a tree, 65,535 points | none |
| TrueType hinting (`TrueTypeHinter.GlyphWorkBudget`, `SizeWorkBudget`) | 2,000,000 per glyph load, 4,500,000 per `CreateScaledFont` (Denys: "Glyph and size budgets (Recommended)") | 19,897 (`arial.ttf` glyph 395); 44,511 (`cambria.ttc`) |
| CFF charstrings (`Type2Interpreter.GlyphWorkBudget`) | 1,000,000 per glyph load (a floor: the CFF corpus is small) | 2,849 hinted (`SourceSans3-Regular.otf` glyph 400) |
| Variations | 1,000,000 per unhinted glyph (`GlyfOutlineSource.VariationWorkBudget`), `cvar` on the size budget, `MVAR` set-up 1,000,000 (`VariationInstance.SetUpWorkBudget`) | 2,539 (`SitkaVF-Italic.ttf`); hinted 4,299; `cvar` 24,187; `MVAR` 5 |
| Pixel size | 1 to 4096 | `ArgumentOutOfRangeException` outside it |
| CFF | stack 48 (CFF) and 513 (CFF2), subroutine nesting 16, 96 hints, 256 Font DICTs, hinting refused above 2,000 ppem (the glyph loads unhinted, as FreeType does) | FreeType's own limits |

**Known limits** (not detected, documented): gzip `SVG ` documents with bytes between the deflate end and the trailer ([KI-037](../KNOWN-ISSUES.md#ki-037-svg-gzip-documents-with-bytes-between-the-deflate-end-and-the-trailer-are-not-detected)), and a WOFF 1.0 zlib stream missing its end marker and Adler-32 ([KI-038](../KNOWN-ISSUES.md#ki-038-a-woff-10-zlib-stream-missing-its-end-marker-and-adler-32-is-accepted)); both as [KI-023](../KNOWN-ISSUES.md#ki-023-png-decoding-cannot-see-a-zlib-streams-missing-end) for PNG, because `System.IO.Compression` reports them only through a process-wide switch. Hostile shapes inside every rule were refused in 0.01 to 0.05 s in the reviews; the load's work is linear in the file's size.

## 5. Threading

`FontFace`, `FontFile` and the value types are immutable and safe on any thread, and so are the loaders (`LoadAsync` parses on the thread pool, never on the calling thread). A `ScaledFont` holds hinting state: **one per thread**. Each of `GetOutline`, `GetMetrics` and `Render` loads the glyph again and, hinted, runs its programs again. A glyph's hinted output can depend on the glyphs loaded before it at the same size (the twilight zone and `WCVTF` writes to the `cvt` persist, as in FreeType), so two calls for one glyph can differ for a font whose programs write them. The engine builds its atlas on one `ScaledFont`, in a fixed order.

## 6. How it is tested

`tests/CoolEngine.Fonts.Tests` (NUnit, `net10.0-windows`, `AllowUnsafeBlocks` for FreeTypeSharp's pointer API and the COM source generator) compares the library with two references, per Denys's "FreeType and Windows" (2026-11-24): **FreeType 2.13.2** through FreeTypeSharp 3.1.0's native `freetype.dll` (the only project that references FreeTypeSharp; it also imports the variation and property functions the package does not bind, with a 64-bit `FT_Long`) and **DirectWrite** through its own COM interop (`[GeneratedComInterface]`, no package). 4,078 tests (2026-11-27; 4,939 in the whole solution after the rebase on `master` at `0f9cb48`), about 55 s on an idle machine; the engine's atlas goldens are in `tests/CoolEngine.Tests`.

| Check | Reference | Result |
|-------|-----------|--------|
| Glyph indices, properties, advances, face counts | FreeType and DirectWrite | Identical (DirectWrite advances only on non-variable faces) |
| Unhinted and hinted outlines, metrics, line metrics and bitmaps, v35 and v40 | FreeType, same load flags and interpreter version | Identical on every glyph; Windows corpus, WP1: 5,278,074 unhinted loads; WP4: 10,556,148 hinted loads at 9, 16 and 72 px and 28,149,728 at other sizes (1 to 400 px) over 214 faces, 0 differences; 20,592 fail in both (`monbaiti.ttf`) |
| Variations at every named instance and axis extreme, unhinted and hinted | FreeType (`FT_Set_Var_Design_Coordinates`); `Axes` and `NamedInstances` also against `FT_MM_Var` and DirectWrite | 1,764,444 loads of the six Windows variable faces, 0 differences; about 610,000 more in the review |
| Hinted CFF and CFF2 | FreeType | About 160,000 loads of Source Sans 3 and `ROGFonts-Regular_0.otf`, and about 352,000 more in the review, identical |
| WOFF and WOFF2 | DirectWrite's `UnpackFontFile`, FreeType | Every WOFF2 image byte-identical to DirectWrite's (transformed `glyf`, `loca`, `hmtx` included); loads equal FreeType's from the same bytes |
| The engine's atlas | The old FreeType path | The goldens of Arial and Times New Roman at 32 px equal the old output ([ASSET-PIPELINE](../CoolEngine/Services/Loaders/ASSET-PIPELINE.md#3-loader-details)) |
| Strict rules, limits, budgets | Crafted fonts | One test per rule and limit; hostile shapes are refused in milliseconds; tens of thousands of fuzzed fonts per review (up to 360,000 cases) produced only `InvalidDataException`, `NotSupportedException` or success |

**Test fonts:** the repository's `OldTanks/Assets/Fonts/Arial.ttf` and `TimesNewRoman.ttf`; Source Sans 3 (SIL OFL 1.1, Adobe), 13 files in `tests/CoolEngine.Fonts.Tests/Assets/SourceSans3` with its `LICENSE.md` (TrueType, CFF, the two variable fonts and their WOFF and WOFF2 copies; downloaded 2026-11-25 with Denys's approval); Windows fonts read in place from `C:\Windows\Fonts` (never copied; a missing one fails the test, the project is Windows-only); and fonts crafted in test code (a copy with one defect each). Denys (2026-11-26): no scan of other fonts on this machine; the two Myriad Pro files of the old client were counted read-only, never copied.

**Rules for the tests:** never loosen a check; if a case differs from FreeType after a reading of FreeType's code for that detail, report the figures (how many glyphs, sizes, maximum and mean difference) and Denys decides. **Never feed a mutated or fuzzed font to the native FreeType** (Denys's rule for the test projects). The sweeps over the whole Windows font folder are `[Explicit]` (minutes).

## 7. Credits

FreeType is the reference, and the files that follow its code closely carry a header naming the FreeType 2.13.2 source they follow (the rasteriser follows `ftgrays.c`; the sfnt, `cmap`, `name`, TrueType loader and interpreter, the CFF loader and the Adobe hinting engine, and the WOFF decoders follow the matching files). Denys accepted this for all of OT-4 ("Accept for all of OT-4 (Recommended)"; turned down: accept WP1 only and ask per package; rewrite from the specs with a tolerance) and "Specs first, source for details (Recommended)" (2026-11-25): the implementations start from the published specifications and read FreeType 2.13.2's code for a detail where the output differs. The notice the FreeType Project License requires:

> Portions of this software are copyright © 2023 The FreeType Project (www.freetype.org). All rights reserved.

The Source Sans 3 test fonts are Copyright 2010-2024 Adobe (http://www.adobe.com/), with Reserved Font Name 'Source', licensed under the SIL Open Font License, Version 1.1; the licence is `tests/CoolEngine.Fonts.Tests/Assets/SourceSans3/LICENSE.md`. They are used in the tests only.
