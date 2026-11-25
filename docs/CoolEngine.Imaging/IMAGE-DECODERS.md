# Image Decoders

| Field        | Value  |
|--------------|--------|
| Status       | Living |
| Last updated | 2026-11-25 |
| Verified against | 2026-11-25; feature/OT-3, uncommitted; whole page against the code in `CoolEngine.Imaging` and the figures measured on the integrated tree |

---

`CoolEngine.Imaging` decodes PNG and JPEG files into RGBA8 pixels for the engine's textures. It has no dependencies and replaced SixLabors.ImageSharp in OT-3 (Denys: "Write native .net10 analogue as separate project", 2026-11-24): every ImageSharp version without known advisories needs a Six Labors license key at build time, and 3.1.x has high-severity advisories ([session log](../sessions/SESSION-LOG.md)). Both decoders are **strict** (§3) and, on every valid texture asset, **bit-identical to Windows' decoder**.

## 1. Public API

All in namespace `CoolEngine.Imaging`; the decoders themselves (`Png.PngDecoder`, `Jpeg.JpegDecoder`) are internal.

| Type | Role |
|------|------|
| `ImageDecoder` | Static entry. `DetectFormat(ReadOnlySpan<byte>)` reads the signature, not the extension. `Decode(ReadOnlySpan<byte>, bool flipVertically = false)` decodes a whole file. `Load(path, flip)` reads and decodes. `LoadAsync(path, flip, token)` reads asynchronously, then decodes inside `Task.Run`. `MaxPixelCount` is 16384 × 16384 |
| `RgbaImage` | `Width`, `Height`, `Pixels` (`Rgba32[]`, row-major, exactly `Width * Height`), an indexer `this[x, y]` that checks bounds. Row 0 is the top unless decoded with `flipVertically`, which puts the bottom row first as OpenGL uploads expect |
| `Rgba32` | `readonly record struct (byte R, G, B, A)`, four bytes in that order, so an array uploads as `Rgba` / `UnsignedByte` |
| `ImageFormat` | `Unknown`, `Png`, `Jpeg` |

Pixels come out as stored: gamma, `sRGB`, ICC profiles and EXIF orientation are ignored. A file of another format throws `NotSupportedException` ("only PNG and JPEG are supported").

## 2. Formats

| Format | Supported | `NotSupportedException` |
|--------|-----------|-------------------------|
| PNG | Every colour type and bit depth of the specification, palettes, `tRNS` transparency, Adam7 interlacing; 16-bit samples rounded to 8 bits; the zlib stream through `System.IO.Compression.ZLibStream` | An unknown critical chunk; an image over `MaxPixelCount` |
| JPEG | Baseline, extended sequential and progressive Huffman, 8-bit, 1 or 3 components; libjpeg's `islow` inverse DCT, fancy upsampling and YCbCr tables (an Adobe marker, then JFIF, then the component ids decide RGB or YCbCr) | Lossless, hierarchical, arithmetic-coded, 12-bit, CMYK/YCCK, a DNL-sized (height 0) frame, more than 100 scans, an image over `MaxPixelCount` |

Not read at all: BMP, GIF, TGA, WebP (ImageSharp read them; a texture in one of these formats now throws from the loader, [KI-012](../KNOWN-ISSUES.md#ki-012-startup-continues-after-a-failed-asset-load)).

## 3. The strict rules

Nonconforming or damaged data throws `InvalidDataException`. Denys's decisions (2026-11-24 and 2026-11-25): PNG "I think we should strict. Lenients smells like possible vulnerability."; JPEG "Strict JPEG too" (libjpeg's leniency, which decodes a truncated scan as gray like Windows, turned down). The authoritative list is the class remarks of `CoolEngine.Imaging.Png.PngDecoder` and `CoolEngine.Imaging.Jpeg.JpegDecoder`; in short:

| Decoder | Rejected |
|---------|----------|
| PNG | A CRC mismatch on **any** chunk (ancillary too); IHDR not first or repeated, a bad size, bit depth for the colour type, compression, filter or interlace method; a bad `PLTE` (an error in grayscale, more than 2^depth entries for palette images, after `IDAT`, repeated, missing for a palette image); `tRNS` after `IDAT`, repeated, in a type with alpha, before `PLTE` or longer than the palette; non-consecutive `IDAT`; missing or non-empty `IEND`; a bad zlib header (CM 8, CINFO ≤ 7, no FDICT, FCHECK); image data too short for the header, checked before allocating; inflated data that does not end exactly after the last row; a filter type above 4; any inflater error, rethrown with our own message. `tRNS` samples are masked to the bit depth (PNG 3 §11.3.1.1). Unknown ancillary chunks with a valid CRC are skipped |
| JPEG | Anything but markers and `0xFF` fill between segments (an `RSTn` or `TEM` there included); a missing `EOI` (bytes after it are ignored); bad segment lengths; a second frame; a zero quantization step; an all-ones Huffman code; a scan component absent from the frame, repeated or out of frame order; more than 10 blocks per interleaved MCU; Huffman table ids above 1 in a baseline (SOF0) frame; a needed table undefined; sequential scan parameters other than 0/63/0/0; a component missing from, or in two of, the sequential scans; a progressive file that breaks T.81 G.1.1.1, lacks a DC first scan for a component, or **alters** a used quantization table mid-progression (an identical redefinition is conforming); in the data: an invalid Huffman code, a DC category above 11, an AC category above 10, an AC zero run (ZRL included) past the block's or band's last coefficient, a refinement symbol of size other than 0 or 1, EOBn run codes in a sequential scan, consuming a fill bit (a truncated or cut scan), a restart interval not used up or not followed by the expected `RSTm` |

Decisions behind individual rules, all Denys's: "Keep all five" header-consistency rules; "100 scans" (not constant-time EOB runs, not 1000); "CRC on every chunk"; "Exact zlib stream end"; "Reject both" for an AC zero run past the band and an AC size above 10 (libjpeg's acceptance turned down). Full list: [session log](../sessions/SESSION-LOG.md).

**Documented exceptions (accepted on purpose):**

| Exception | Why |
|-----------|-----|
| The padding bits before a marker are not checked for 1s | "Drop it" (Denys): a decoder never uses them, and checking only rejects encoders that pad with zeros |
| 16-bit quantization tables (Pq = 1) in 8-bit files are accepted | "Accept, documented" (Denys): libjpeg writes them |

**Known PNG limit** ([KI-023](../KNOWN-ISSUES.md#ki-023-png-decoding-cannot-see-a-zlib-streams-missing-end)): a zlib stream missing its final block or its Adler-32, and bytes after the stream inside `IDAT`, still decode, because `ZLibStream` cannot report them. A wrong Adler-32 that the inflater does detect is rejected. An own deflate decoder is in the [BACKLOG](../BACKLOG.md#own-libraries-and-engine-direction).

## 4. Limits

| Limit | Value | Behaviour |
|-------|-------|-----------|
| `ImageDecoder.MaxPixelCount` | 16384² (Denys: "Keep 16384²") | `NotSupportedException` before the pixel buffer is allocated; a zero or negative size is `InvalidDataException` |
| JPEG scans | 100 | `NotSupportedException` |
| Memory on a crafted header (measured 2026-11-25) | PNG 0 bytes; JPEG 1.50 GiB at 16384² × 3 components | PNG rejects image data too small for its header before allocating; JPEG allocates its coefficient grids before the first scan fails (a 150-byte header claiming that size threw after 7 ms) |
| Time | A valid 99-scan progressive file at 16384² (556 KiB) decodes in about 16 s; a file with 883 scans (908 KiB) is rejected after about 13 s, once its first 100 scans are decoded | Documented, not guarded further |

A fully valid file of about 1.5 MB (progressive DC data) at the size limit reaches about 3.25 GiB; this is documented, not prevented.

## 5. Threading

The decoders are stateless and safe on any thread. `ImageDecoder.LoadAsync` decodes on the thread pool, never on the calling thread; the engine's loaders `await` it on the GL thread and then upload the pixels there ([ASSET-PIPELINE §3](../CoolEngine/Services/Loaders/ASSET-PIPELINE.md#3-loader-details)). There is no SIMD and no parallel decode.

## 6. How it is tested

`tests/CoolEngine.Imaging.Tests` (NUnit, `net10.0-windows` because GDI+ is Windows-only) compares the decoders with two references, per Denys's "Tests both with Windows and ImageSharp": Windows' GDI+ through `System.Drawing.Common` 11.0.0 and SixLabors.ImageSharp 4.1.4, both in the test project only. 247 tests on 2026-11-25: 98 on the PNG side, 149 on the JPEG side.

| Check | Tolerance |
|-------|-----------|
| PNG assets and 30 encoded variants (every colour type, depth and Adam7) against ImageSharp; the variants of 8 bits or less also against Windows (GDI+ treats 16-bit PNG as linear light) | Identical |
| JPEG assets and the 42 encoded variants against Windows | No chroma subsampling: max ≤ 4, mean ≤ 1.0. Subsampled: mean ≤ 2.5, 99.9th percentile ≤ 24; alpha always 255 |
| JPEG against ImageSharp | The same tolerances (39 variants: ImageSharp 4.1.4 writes three malformed restart-interval files that it cannot read itself) |
| Crafted files | One test per strict rule, the two exceptions and the limits |

**Measured (2026-11-24 to 2026-11-25):** GDI+ and WIC decode every texture asset identically; ImageSharp equals them on PNG; on JPEG against Windows ImageSharp differs by max 2 (4:4:4) and max 32, mean ≤ 2.11 (`Brick.jpg`), 99.9th percentile ≤ 19 (4:2:0). `CoolEngine.Imaging`'s JPEG output is **bit-identical to Windows** on all 9 assets and all 42 encoded variants. A corpus check decoded 306 distinct system JPEGs bit-identically and 7,838 of 7,840 system PNGs (the 2 rejected have a genuine `IDAT` CRC mismatch that Windows shows anyway); 40,000 mutated files produced only `InvalidDataException`, `NotSupportedException` or success.

**Rule for the tests:** never loosen a tolerance; if a case fails, report the measured figures. `CoolEngine.Imaging.Rgba32` clashes with `SixLabors.ImageSharp.PixelFormats.Rgba32` in the test project; the tests alias both ([Glossary](../knowledge/GLOSSARY.md#name-clashes)).

**Release builds:** the test project references ImageSharp 4.1.4, which needs a Six Labors license key at build time. A Debug build only warns; a Release build of the solution fails until a key is supplied, as a `sixlabors.lic` file or the `SixLaborsLicenseKey` property (not tried; reasoned from the package's licensing notice and the OT-3 plan). A community key is a [BACKLOG](../BACKLOG.md#own-libraries-and-engine-direction) item.
