using System.Text;
using FreeTypeSharp;
using static FreeTypeSharp.FT;

namespace CoolEngine.Fonts.Tests.Reference;

/// <summary>
/// A FreeType library whose cff driver has its "random-seed" property set, so the CFF <c>random</c> operator is
/// deterministic: with 0, FreeType seeds each subfont from its Private DICT's initialRandomSeed (<c>cff_subfont_load</c>).
/// </summary>
internal sealed unsafe class SeededFreeType : IDisposable
{
    private FT_LibraryRec_* m_library;

    public SeededFreeType(int randomSeed)
    {
        FT_LibraryRec_* library;
        FreeTypeReference.Check(FT_Init_FreeType(&library), "FT_Init_FreeType");
        m_library = library;

        var module = Encoding.ASCII.GetBytes("cff\0");
        var property = Encoding.ASCII.GetBytes("random-seed\0");
        var value = randomSeed;
        fixed (byte* modulePointer = module)
        fixed (byte* propertyPointer = property)
            FreeTypeReference.Check(FT_Property_Set(m_library, modulePointer, propertyPointer, &value), "FT_Property_Set");
    }

    /// <summary>Opens face 0 of the bytes (pinned for the face's lifetime).</summary>
    public FreeTypeFace Open(byte[] data) => new(m_library, data, 0);

    public void Dispose()
    {
        if (m_library is null)
            return;

        FT_Done_FreeType(m_library);
        m_library = null;
    }
}
