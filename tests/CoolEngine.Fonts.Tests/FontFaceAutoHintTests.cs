using System.Runtime.InteropServices;
using CoolEngine.Fonts.Tests.Reference;
using FreeTypeSharp;
using static FreeTypeSharp.FT;

namespace CoolEngine.Fonts.Tests;

/// <summary>
/// <see cref="FontFace.WouldBeAutoHintedByFreeType"/> against what FreeType 2.13.2 does: a glyph loaded with default flags
/// (<c>FT_LOAD_NO_BITMAP</c> only) differs from the same glyph loaded with <c>FT_LOAD_NO_AUTOHINT</c> exactly when FreeType
/// auto-hinted it.
/// </summary>
[TestFixture]
public sealed unsafe class FontFaceAutoHintTests
{
    private static IEnumerable<TestCaseData> Faces()
    {
        // Hinted TrueType, an unhinted variable TrueType, CFF, and Windows fonts without instructions.
        var faces = TestFonts.AllFaces().Concat(
        [
            new TestFace("BaiJamjuree-Regular.ttf", TestFonts.Windows("BaiJamjuree-Regular.ttf"), 0),
            new TestFace("SimsunExtG.ttf", TestFonts.Windows("SimsunExtG.ttf"), 0),
            new TestFace("SegoeIcons.ttf", TestFonts.Windows("SegoeIcons.ttf"), 0),
        ]);

        return faces.Select(face => new TestCaseData(face).SetArgDisplayNames(face.Name));
    }

    [TestCaseSource(nameof(Faces))]
    public void WouldBeAutoHintedByFreeType_Face_MatchesFreeTypesChoice(TestFace testFace)
    {
        var face = FontCache.Face(testFace);

        Assert.That(face.WouldBeAutoHintedByFreeType, Is.EqualTo(FreeTypeAutoHints(testFace)));
    }

    [Test]
    public void WouldBeAutoHintedByFreeType_ArialAndAnUnhintedFace_FalseAndTrue()
    {
        Assert.Multiple(() =>
        {
            Assert.That(FontCache.Face(new TestFace("Arial", TestFonts.RepositoryArial, 0)).WouldBeAutoHintedByFreeType, Is.False);
            Assert.That(FontCache.Face(new TestFace("SourceSans3VF-Upright.ttf", TestFonts.SourceSans("SourceSans3VF-Upright.ttf"), 0))
                .WouldBeAutoHintedByFreeType, Is.True);
        });
    }

    /// <summary>Whether any glyph of the first 400 at 16 and 32 px loads differently with and without <c>FT_LOAD_NO_AUTOHINT</c>.</summary>
    private static bool FreeTypeAutoHints(TestFace testFace)
    {
        var data = FontCache.Bytes(testFace.Path);
        var pin = GCHandle.Alloc(data, GCHandleType.Pinned);
        FT_LibraryRec_* library;
        FT_FaceRec_* face;

        FreeTypeReference.Check(FT_Init_FreeType(&library), "FT_Init_FreeType");

        try
        {
            FreeTypeReference.Check(
                FT_New_Memory_Face(library, (byte*)pin.AddrOfPinnedObject(), data.Length, testFace.FaceIndex, &face), "FT_New_Memory_Face");

            try
            {
                foreach (var size in new uint[] { 16, 32 })
                {
                    FreeTypeReference.Check(FT_Set_Pixel_Sizes(face, 0, size), "FT_Set_Pixel_Sizes");

                    for (uint glyph = 1; glyph < Math.Min((uint)face->num_glyphs, 400u); glyph++)
                    {
                        var native = Load(face, glyph, FT_LOAD.FT_LOAD_NO_BITMAP | FT_LOAD.FT_LOAD_NO_AUTOHINT);
                        var byDefault = Load(face, glyph, FT_LOAD.FT_LOAD_NO_BITMAP);

                        if (!native.SequenceEqual(byDefault))
                            return true;
                    }
                }

                return false;
            }
            finally
            {
                FT_Done_Face(face);
            }
        }
        finally
        {
            FT_Done_FreeType(library);
            pin.Free();
        }
    }

    private static long[] Load(FT_FaceRec_* face, uint glyph, FT_LOAD flags)
    {
        FreeTypeReference.Check(FT_Load_Glyph(face, glyph, flags), "FT_Load_Glyph");

        var outline = &face->glyph->outline;
        var values = new long[outline->n_points * 2];
        for (var i = 0; i < outline->n_points; i++)
        {
            values[2 * i] = (long)outline->points[i].x;
            values[2 * i + 1] = (long)outline->points[i].y;
        }

        return values;
    }
}
