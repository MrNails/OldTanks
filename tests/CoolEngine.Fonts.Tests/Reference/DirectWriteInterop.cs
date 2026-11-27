using System.Runtime.InteropServices;
using System.Runtime.InteropServices.Marshalling;

namespace CoolEngine.Fonts.Tests.Reference;

// DirectWrite's COM interfaces, declared with every vtable slot in dwrite.h's order; the slots the tests do not call take raw
// pointers. Later OT-4 packages add IDWriteFactory1..5 (UnpackFontFile) and IDWriteFontFace1..5 as interfaces deriving from these.

/// <summary><c>DWRITE_GLYPH_METRICS</c>, in font design units.</summary>
[StructLayout(LayoutKind.Sequential)]
internal struct DWriteGlyphMetrics
{
    public int LeftSideBearing;
    public uint AdvanceWidth;
    public int RightSideBearing;
    public int TopSideBearing;
    public uint AdvanceHeight;
    public int BottomSideBearing;
    public int VerticalOriginY;
}

internal static partial class DirectWriteNative
{
    public const int FactoryTypeIsolated = 1;

    [LibraryImport("dwrite.dll")]
    public static partial int DWriteCreateFactory(int factoryType, in Guid iid, out nint factory);
}

[GeneratedComInterface]
[Guid("b859ee5a-d838-4b5b-a2e8-1adc7d93db48")]
internal partial interface IDWriteFactory
{
    void GetSystemFontCollection(nint fontCollection, int checkForUpdates);

    void CreateCustomFontCollection(nint collectionLoader, nint collectionKey, uint collectionKeySize, nint fontCollection);

    void RegisterFontCollectionLoader(nint fontCollectionLoader);

    void UnregisterFontCollectionLoader(nint fontCollectionLoader);

    void CreateFontFileReference([MarshalAs(UnmanagedType.LPWStr)] string filePath, nint lastWriteTime, out nint fontFile);

    void CreateCustomFontFileReference(nint fontFileReferenceKey, uint fontFileReferenceKeySize, nint fontFileLoader, nint fontFile);

    void CreateFontFace(int fontFaceType, uint numberOfFiles, in nint fontFiles, uint faceIndex, int fontFaceSimulationFlags,
        out IDWriteFontFace fontFace);

    void CreateRenderingParams(nint renderingParams);

    void CreateMonitorRenderingParams(nint monitor, nint renderingParams);

    void CreateCustomRenderingParams(float gamma, float enhancedContrast, float clearTypeLevel, int pixelGeometry, int renderingMode,
        nint renderingParams);

    void RegisterFontFileLoader(nint fontFileLoader);

    void UnregisterFontFileLoader(nint fontFileLoader);

    void CreateTextFormat(nint fontFamilyName, nint fontCollection, int fontWeight, int fontStyle, int fontStretch, float fontSize,
        nint localeName, nint textFormat);

    void CreateTypography(nint typography);

    void GetGdiInterop(nint gdiInterop);

    void CreateTextLayout(nint text, uint stringLength, nint textFormat, float maxWidth, float maxHeight, nint textLayout);

    void CreateGdiCompatibleTextLayout(nint text, uint stringLength, nint textFormat, float layoutWidth, float layoutHeight,
        float pixelsPerDip, nint transform, int useGdiNatural, nint textLayout);

    void CreateEllipsisTrimmingSign(nint textFormat, nint trimmingSign);

    void CreateTextAnalyzer(nint textAnalyzer);

    void CreateNumberSubstitution(int substitutionMethod, nint localeName, int ignoreUserOverride, nint numberSubstitution);

    void CreateGlyphRunAnalysis(nint glyphRun, float pixelsPerDip, nint transform, int renderingMode, int measuringMode,
        float baselineOriginX, float baselineOriginY, nint glyphRunAnalysis);
}

[GeneratedComInterface]
[Guid("739d886a-cef5-47dc-8769-1a8b41bebbb0")]
internal partial interface IDWriteFontFile
{
    void GetReferenceKey(nint fontFileReferenceKey, nint fontFileReferenceKeySize);

    void GetLoader(nint fontFileLoader);

    void Analyze(out int isSupportedFontType, out int fontFileType, out int fontFaceType, out uint numberOfFaces);
}

[GeneratedComInterface]
[Guid("5f49804d-7024-4d43-bfa9-d25984f53849")]
internal unsafe partial interface IDWriteFontFace
{
    [PreserveSig]
    int GetType();

    void GetFiles(nint numberOfFiles, nint fontFiles);

    [PreserveSig]
    uint GetIndex();

    [PreserveSig]
    int GetSimulations();

    [PreserveSig]
    int IsSymbolFont();

    [PreserveSig]
    void GetMetrics(nint fontFaceMetrics);

    [PreserveSig]
    ushort GetGlyphCount();

    void GetDesignGlyphMetrics(ushort* glyphIndices, uint glyphCount, DWriteGlyphMetrics* glyphMetrics, int isSideways);

    void GetGlyphIndices(uint* codePoints, uint codePointCount, ushort* glyphIndices);

    void TryGetFontTable(uint openTypeTableTag, nint tableData, nint tableSize, nint tableContext, nint exists);

    [PreserveSig]
    void ReleaseFontTable(nint tableContext);

    void GetGlyphRunOutline(float emSize, nint glyphIndices, nint glyphAdvances, nint glyphOffsets, uint glyphCount, int isSideways,
        int isRightToLeft, nint geometrySink);

    void GetRecommendedRenderingMode(float emSize, float pixelsPerDip, int measuringMode, nint renderingParams, nint renderingMode);

    void GetGdiCompatibleMetrics(float emSize, float pixelsPerDip, nint transform, nint fontFaceMetrics);

    void GetGdiCompatibleGlyphMetrics(float emSize, float pixelsPerDip, nint transform, int useGdiNatural, nint glyphIndices,
        uint glyphCount, nint glyphMetrics, int isSideways);
}

// IDWriteFactory1 to IDWriteFactory5 (dwrite_1.h, dwrite_2.h, dwrite_3.h, Windows SDK 10.0.26100.0): every method slot in header
// order, except overloads, which MSVC lays out in reverse order, each interface deriving from the one before. Only UnpackFontFile is called; the other slots take raw pointers, and
// methods that repeat a name in a later interface carry a numeric suffix (the names do not reach the vtable).

/// <summary><c>DWRITE_CONTAINER_TYPE</c>.</summary>
internal enum DWriteContainerType
{
    Unknown = 0,
    Woff = 1,
    Woff2 = 2,
}

[GeneratedComInterface]
[Guid("30572f99-dac6-41db-a16e-0486307e606a")]
internal partial interface IDWriteFactory1 : IDWriteFactory
{
    void GetEudcFontCollection(nint fontCollection, int checkForUpdates);

    void CreateCustomRenderingParams1(float gamma, float enhancedContrast, float enhancedContrastGrayscale, float clearTypeLevel,
        int pixelGeometry, int renderingMode, nint renderingParams);
}

[GeneratedComInterface]
[Guid("0439fc60-ca44-4994-8dee-3a9af7b732ec")]
internal partial interface IDWriteFactory2 : IDWriteFactory1
{
    void GetSystemFontFallback(nint fontFallback);

    void CreateFontFallbackBuilder(nint fontFallbackBuilder);

    void TranslateColorGlyphRun2(float baselineOriginX, float baselineOriginY, nint glyphRun, nint glyphRunDescription,
        int measuringMode, nint worldToDeviceTransform, uint colorPaletteIndex, nint colorLayers);

    void CreateCustomRenderingParams2(float gamma, float enhancedContrast, float grayscaleEnhancedContrast, float clearTypeLevel,
        int pixelGeometry, int renderingMode, int gridFitMode, nint renderingParams);

    void CreateGlyphRunAnalysis2(nint glyphRun, nint transform, int renderingMode, int measuringMode, int gridFitMode,
        int antialiasMode, float baselineOriginX, float baselineOriginY, nint glyphRunAnalysis);
}

[GeneratedComInterface]
[Guid("9A1B41C3-D3BB-466A-87FC-FE67556A3B65")]
internal partial interface IDWriteFactory3 : IDWriteFactory2
{
    void CreateGlyphRunAnalysis3(nint glyphRun, nint transform, int renderingMode, int measuringMode, int gridFitMode,
        int antialiasMode, float baselineOriginX, float baselineOriginY, nint glyphRunAnalysis);

    void CreateCustomRenderingParams3(float gamma, float enhancedContrast, float grayscaleEnhancedContrast, float clearTypeLevel,
        int pixelGeometry, int renderingMode, int gridFitMode, nint renderingParams);

    // dwrite_3.h declares CreateFontFaceReference(filePath, ...) and then CreateFontFaceReference(fontFile, ...); MSVC puts the
    // overloads of one interface in the vtable in reverse order of declaration, so the fontFile overload is the first slot.
    void CreateFontFaceReferenceFromFile(nint fontFile, uint faceIndex, int fontSimulations, nint fontFaceReference);

    void CreateFontFaceReferenceFromPath(nint filePath, nint lastWriteTime, uint faceIndex, int fontSimulations, nint fontFaceReference);

    void GetSystemFontSet(nint fontSet);

    void CreateFontSetBuilder3(nint fontSetBuilder);

    void CreateFontCollectionFromFontSet(nint fontSet, nint fontCollection);

    void GetSystemFontCollection3(int includeDownloadableFonts, nint fontCollection, int checkForUpdates);

    void GetFontDownloadQueue(nint fontDownloadQueue);
}

[GeneratedComInterface]
[Guid("4B0B5BD3-0797-4549-8AC5-FE915CC53856")]
internal partial interface IDWriteFactory4 : IDWriteFactory3
{
    void TranslateColorGlyphRun4(float baselineOriginX, float baselineOriginY, nint glyphRun, nint glyphRunDescription,
        int desiredGlyphImageFormats, int measuringMode, nint worldAndDpiTransform, uint colorPaletteIndex, nint colorLayers);

    // dwrite_3.h declares ComputeGlyphOrigins(glyphRun, measuringMode, ...) and then ComputeGlyphOrigins(glyphRun, baselineOrigin,
    // ...): reverse vtable order for the overloads of one interface (MSVC), so the shorter one is the first slot.
    void ComputeGlyphOriginsNoMode(nint glyphRun, float baselineOriginX, float baselineOriginY, nint glyphOrigins);

    void ComputeGlyphOrigins(nint glyphRun, int measuringMode, float baselineOriginX, float baselineOriginY,
        nint worldAndDpiTransform, nint glyphOrigins);
}

[GeneratedComInterface]
[Guid("958DB99A-BE2A-4F09-AF7D-65189803D1D3")]
internal unsafe partial interface IDWriteFactory5 : IDWriteFactory4
{
    void CreateFontSetBuilder5(nint fontSetBuilder);

    void CreateInMemoryFontFileLoader(nint newLoader);

    void CreateHttpFontFileLoader(nint referrerUrl, nint extraHeaders, nint newLoader);

    [PreserveSig]
    int AnalyzeContainerType(byte* fileData, uint fileDataSize);

    /// <summary>Unpacks a WOFF or WOFF2 container into an sfnt font file stream; a failed HRESULT throws.</summary>
    void UnpackFontFile(int containerType, byte* fileData, uint fileDataSize, out IDWriteFontFileStream unpackedFontStream);
}

[GeneratedComInterface]
[Guid("6d4865fe-0ab8-4d91-8f62-5dd6be34a3e0")]
internal unsafe partial interface IDWriteFontFileStream
{
    void ReadFileFragment(out byte* fragmentStart, ulong fileOffset, ulong fragmentSize, out nint fragmentContext);

    [PreserveSig]
    void ReleaseFileFragment(nint fragmentContext);

    void GetFileSize(out ulong fileSize);

    void GetLastWriteTime(out ulong lastWriteTime);
}
