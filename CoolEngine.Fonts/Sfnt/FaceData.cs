namespace CoolEngine.Fonts.Sfnt;

/// <summary>The checked tables of one face, shared by the face, its outline source and (from WP4) its hinter. Immutable.</summary>
internal sealed class FaceData
{
    public required int FaceIndex { get; init; }

    public required TableDirectory Tables { get; init; }

    public required HeadTable Head { get; init; }

    public required HheaTable Hhea { get; init; }

    public required MaxpTable Maxp { get; init; }

    public required HmtxTable Hmtx { get; init; }

    /// <summary>Null when the face has no <c>OS/2</c> table.</summary>
    public required Os2Table? Os2 { get; init; }

    public required NameTable Name { get; init; }

    public required PostTable Post { get; init; }

    public required CmapTable Cmap { get; init; }

    /// <summary>Null when the face has no <c>SVG </c> table.</summary>
    public required SvgTable? Svg { get; init; }

    /// <summary>Null unless <see cref="OutlineFormat"/> is <see cref="Fonts.OutlineFormat.TrueType"/>.</summary>
    public required GlyfTable? Glyf { get; init; }

    /// <summary>The checked <c>CFF </c> or <c>CFF2</c> table; null unless <see cref="OutlineFormat"/> is CFF or CFF2.</summary>
    public required Cff.CffTable? Cff { get; init; }

    public required OutlineFormat OutlineFormat { get; init; }

    public int GlyphCount => Maxp.NumGlyphs;
}
