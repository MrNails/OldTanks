using System.Runtime.InteropServices;
using System.Runtime.InteropServices.Marshalling;

namespace CoolEngine.Fonts.Tests.Reference;

// DirectWrite's font-resource and font-set interfaces for variable fonts (dwrite.h and dwrite_3.h, Windows SDK 10.0.26100.0),
// every vtable slot in header order; overloads of one interface are laid out by MSVC in reverse order of declaration (as the
// note in DirectWriteInterop.cs says), so their slots are reversed here. Only the methods the tests call take typed
// parameters; the other slots take raw pointers, and methods that repeat a name of a base interface carry a numeric suffix.

/// <summary><c>DWRITE_FONT_AXIS_VALUE</c>.</summary>
[StructLayout(LayoutKind.Sequential)]
internal struct DWriteFontAxisValue
{
    public uint Tag;
    public float Value;
}

/// <summary><c>DWRITE_FONT_AXIS_RANGE</c>.</summary>
[StructLayout(LayoutKind.Sequential)]
internal struct DWriteFontAxisRange
{
    public uint Tag;
    public float Minimum;
    public float Maximum;
}

[GeneratedComInterface]
[Guid("F3744D80-21F7-42EB-B35D-995BC72FC223")]
internal unsafe partial interface IDWriteFactory6 : IDWriteFactory5
{
    void CreateFontFaceReference6(nint fontFile, uint faceIndex, int fontSimulations, nint fontAxisValues, uint fontAxisValueCount,
        nint fontFaceReference);

    void CreateFontResource(nint fontFile, uint faceIndex, out IDWriteFontResource fontResource);

    void GetSystemFontSet6(int includeDownloadableFonts, nint fontSet);

    void GetSystemFontCollection6(int includeDownloadableFonts, int fontFamilyModel, nint fontCollection);

    void CreateFontCollectionFromFontSet6(nint fontSet, int fontFamilyModel, nint fontCollection);

    void CreateFontSetBuilder6(out nint fontSetBuilder);

    void CreateTextFormat6(nint fontFamilyName, nint fontCollection, nint fontAxisValues, uint fontAxisValueCount, float fontSize,
        nint localeName, nint textFormat);
}

[GeneratedComInterface]
[Guid("1F803A76-6871-48E8-987F-B975551C50F2")]
internal unsafe partial interface IDWriteFontResource
{
    void GetFontFile(nint fontFile);

    [PreserveSig]
    uint GetFontFaceIndex();

    [PreserveSig]
    uint GetFontAxisCount();

    void GetDefaultFontAxisValues(DWriteFontAxisValue* fontAxisValues, uint fontAxisValueCount);

    void GetFontAxisRanges(DWriteFontAxisRange* fontAxisRanges, uint fontAxisRangeCount);

    [PreserveSig]
    int GetFontAxisAttributes(uint axisIndex);

    void GetAxisNames(uint axisIndex, out IDWriteLocalizedStrings names);

    [PreserveSig]
    uint GetAxisValueNameCount(uint axisIndex);

    void GetAxisValueNames(uint axisIndex, uint axisValueIndex, nint fontAxisRange, nint names);

    [PreserveSig]
    int HasVariations();

    void CreateFontFace(int fontSimulations, nint fontAxisValues, uint fontAxisValueCount, nint fontFace);

    void CreateFontFaceReference(int fontSimulations, nint fontAxisValues, uint fontAxisValueCount, nint fontFaceReference);
}

[GeneratedComInterface]
[Guid("08256209-099a-4b34-b86d-c22b110e7771")]
internal unsafe partial interface IDWriteLocalizedStrings
{
    [PreserveSig]
    uint GetCount();

    void FindLocaleName([MarshalAs(UnmanagedType.LPWStr)] string localeName, out uint index, out int exists);

    void GetLocaleNameLength(uint index, out uint length);

    void GetLocaleName(uint index, char* localeName, uint size);

    void GetStringLength(uint index, out uint length);

    void GetString(uint index, char* stringBuffer, uint size);
}

[GeneratedComInterface]
[Guid("2F642AFE-9C68-4F40-B8BE-457401AFCB3D")]
internal partial interface IDWriteFontSetBuilder
{
    // dwrite_3.h declares AddFontFaceReference(reference) and then AddFontFaceReference(reference, properties, count): reversed.
    void AddFontFaceReferenceWithProperties(nint fontFaceReference, nint properties, uint propertyCount);

    void AddFontFaceReference(nint fontFaceReference);

    void AddFontSet(nint fontSet);

    void CreateFontSet(out IDWriteFontSet fontSet);
}

[GeneratedComInterface]
[Guid("3FF7715F-3CDC-4DC6-9B72-EC5621DCCAFD")]
internal partial interface IDWriteFontSetBuilder1 : IDWriteFontSetBuilder
{
    void AddFontFile(nint fontFile);
}

[GeneratedComInterface]
[Guid("53585141-D9F8-4095-8321-D73CF6BD116B")]
internal partial interface IDWriteFontSet
{
    [PreserveSig]
    uint GetFontCount();

    void GetFontFaceReference(uint listIndex, out IDWriteFontFaceReference1 fontFaceReference);

    void FindFontFaceReference(nint fontFaceReference, out uint listIndex, out int exists);

    void FindFontFace(nint fontFace, out uint listIndex, out int exists);

    // Three GetPropertyValues overloads, declared (index, id, exists, values), (id, locales, values), (id, values): reversed.
    void GetPropertyValuesList(int propertyId, nint values);

    void GetPropertyValuesListWithLocales(int propertyId, nint preferredLocaleNames, nint values);

    void GetPropertyValues(uint listIndex, int propertyId, out int exists, out IDWriteLocalizedStrings? values);

    void GetPropertyOccurrenceCount(nint property, nint propertyOccurrenceCount);

    // Two GetMatchingFonts overloads, declared (properties, count, set), (family, weight, stretch, style, set): reversed.
    void GetMatchingFontsByName(nint familyName, int fontWeight, int fontStretch, int fontStyle, nint filteredSet);

    void GetMatchingFonts(nint properties, uint propertyCount, nint filteredSet);
}

[GeneratedComInterface]
[Guid("C081FE77-2FD1-41AC-A5A3-34983C4BA61A")]
internal unsafe partial interface IDWriteFontFaceReference1
{
    // IDWriteFontFaceReference's slots.
    void CreateFontFace(nint fontFace);

    void CreateFontFaceWithSimulations(int fontFaceSimulationFlags, nint fontFace);

    [PreserveSig]
    int EqualsReference(nint fontFaceReference);

    [PreserveSig]
    uint GetFontFaceIndex();

    [PreserveSig]
    int GetSimulations();

    void GetFontFile(nint fontFile);

    [PreserveSig]
    ulong GetLocalFileSize();

    [PreserveSig]
    ulong GetFileSize();

    void GetFileTime(nint lastWriteTime);

    [PreserveSig]
    int GetLocality();

    void EnqueueFontDownloadRequest();

    void EnqueueCharacterDownloadRequest(nint characters, uint characterCount);

    void EnqueueGlyphDownloadRequest(nint glyphIndices, uint glyphCount);

    void EnqueueFileFragmentDownloadRequest(ulong fileOffset, ulong fragmentSize);

    // IDWriteFontFaceReference1's own.
    void CreateFontFace1(nint fontFace);

    [PreserveSig]
    uint GetFontAxisValueCount();

    void GetFontAxisValues(DWriteFontAxisValue* fontAxisValues, uint fontAxisValueCount);
}

/// <summary>
/// An axis as DirectWrite's font resource gives it; <paramref name="IsVariable"/> is false for an axis DirectWrite takes from
/// <c>STAT</c> alone (not in <c>fvar</c>).
/// </summary>
public sealed record DirectWriteAxis(string Tag, float Minimum, float Default, float Maximum, bool IsHidden, string Name, bool IsVariable);

/// <summary>A named instance as DirectWrite's font set gives it (one font per named instance of the file).</summary>
public sealed record DirectWriteInstance(float[] Coordinates, string Name);

/// <summary>DirectWrite as the reference for a variable face's axes (<c>IDWriteFontResource</c>) and named instances (a font set).</summary>
internal static unsafe class DirectWriteVariations
{
    private const int TypographicFaceName = 13;
    private const int AxisAttributeVariable = 0x0001;
    private const int AxisAttributeHidden = 0x0002;

    /// <summary>The axes of the face's font resource and the named instances of a font set built from its file.</summary>
    public static (DirectWriteAxis[] Axes, DirectWriteInstance[] Instances) Read(DirectWriteReference directWrite, string path, int faceIndex)
    {
        var factory = (IDWriteFactory6)directWrite.Factory;
        factory.CreateFontFileReference(path, 0, out var file);
        try
        {
            factory.CreateFontResource(file, (uint)faceIndex, out var resource);
            DirectWriteAxis[] axes;
            try
            {
                axes = ReadAxes(resource);
            }
            finally
            {
                DirectWriteReference.Release(resource);
            }

            factory.CreateFontSetBuilder6(out var builderPointer);
            var builder = DirectWriteReference.Wrap<IDWriteFontSetBuilder1>(builderPointer);
            try
            {
                builder.AddFontFile(file);
                builder.CreateFontSet(out var set);
                try
                {
                    // fvar's axes come first; DirectWrite appends STAT's other axes, which do not vary.
                    var variable = axes.TakeWhile(axis => axis.IsVariable).ToArray();
                    return (variable, ReadInstances(set, faceIndex, variable.Length));
                }
                finally
                {
                    DirectWriteReference.Release(set);
                }
            }
            finally
            {
                DirectWriteReference.Release(builder);
            }
        }
        finally
        {
            Marshal.Release(file);
        }
    }

    private static DirectWriteAxis[] ReadAxes(IDWriteFontResource resource)
    {
        var count = (int)resource.GetFontAxisCount();
        var defaults = new DWriteFontAxisValue[count];
        var ranges = new DWriteFontAxisRange[count];
        fixed (DWriteFontAxisValue* values = defaults)
            resource.GetDefaultFontAxisValues(values, (uint)count);

        fixed (DWriteFontAxisRange* values = ranges)
            resource.GetFontAxisRanges(values, (uint)count);

        var axes = new DirectWriteAxis[count];
        for (var i = 0; i < count; i++)
        {
            resource.GetAxisNames((uint)i, out var names);
            var name = English(names);
            DirectWriteReference.Release(names);
            var attributes = resource.GetFontAxisAttributes((uint)i);
            axes[i] = new DirectWriteAxis(TagText(ranges[i].Tag), ranges[i].Minimum, defaults[i].Value, ranges[i].Maximum,
                (attributes & AxisAttributeHidden) != 0, name, (attributes & AxisAttributeVariable) != 0);
        }

        return axes;
    }

    private static DirectWriteInstance[] ReadInstances(IDWriteFontSet set, int faceIndex, int axisCount)
    {
        var instances = new List<DirectWriteInstance>();
        var count = set.GetFontCount();
        for (uint i = 0; i < count; i++)
        {
            set.GetFontFaceReference(i, out var reference);
            try
            {
                if (reference.GetFontFaceIndex() != faceIndex)
                    continue;

                var values = new DWriteFontAxisValue[reference.GetFontAxisValueCount()];
                fixed (DWriteFontAxisValue* pointer = values)
                    reference.GetFontAxisValues(pointer, (uint)values.Length);

                set.GetPropertyValues(i, TypographicFaceName, out var exists, out var names);
                var name = exists != 0 && names is not null ? English(names) : "";
                if (names is not null)
                    DirectWriteReference.Release(names);

                instances.Add(new DirectWriteInstance(values.Take(axisCount).Select(value => value.Value).ToArray(), name));
            }
            finally
            {
                DirectWriteReference.Release(reference);
            }
        }

        return [.. instances];
    }

    /// <summary>The en-us string, else the first.</summary>
    private static string English(IDWriteLocalizedStrings strings)
    {
        if (strings.GetCount() == 0)
            return "";

        strings.FindLocaleName("en-us", out var index, out var exists);
        if (exists == 0)
            index = 0;

        strings.GetStringLength(index, out var length);
        var buffer = new char[length + 1];
        fixed (char* chars = buffer)
            strings.GetString(index, chars, length + 1);

        return new string(buffer, 0, (int)length);
    }

    private static string TagText(uint tag) =>
        new([(char)(tag & 0xFF), (char)((tag >> 8) & 0xFF), (char)((tag >> 16) & 0xFF), (char)(tag >> 24)]);
}
