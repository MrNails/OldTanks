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
