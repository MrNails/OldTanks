namespace CoolEngine.Imaging.Jpeg;

/// <summary>One colour component of a JPEG frame: its sampling, its coefficients and, after the inverse DCT, its samples.</summary>
internal sealed class JpegComponent
{
    public JpegComponent(byte id, int horizontalFactor, int verticalFactor, int quantizationTableId,
        int width, int height, int blocksPerLine, int blocksPerColumn)
    {
        Id = id;
        HorizontalFactor = horizontalFactor;
        VerticalFactor = verticalFactor;
        QuantizationTableId = quantizationTableId;
        Width = width;
        Height = height;
        BlocksPerLine = blocksPerLine;
        BlocksPerColumn = blocksPerColumn;
        Coefficients = new short[blocksPerLine * blocksPerColumn * 64];
    }

    public byte Id { get; }

    /// <summary>H, 1..4.</summary>
    public int HorizontalFactor { get; }

    /// <summary>V, 1..4.</summary>
    public int VerticalFactor { get; }

    public int QuantizationTableId { get; }

    /// <summary>The component's own width in samples: ceil(image width × H / Hmax).</summary>
    public int Width { get; }

    /// <summary>The component's own height in samples: ceil(image height × V / Vmax).</summary>
    public int Height { get; }

    /// <summary>Blocks per row of the coefficient grid: MCUs across × H.</summary>
    public int BlocksPerLine { get; }

    /// <summary>Block rows of the coefficient grid: MCUs down × V.</summary>
    public int BlocksPerColumn { get; }

    /// <summary>Blocks across in a non-interleaved scan: ceil(<see cref="Width"/> / 8).</summary>
    public int OwnBlocksPerLine => (Width + 7) / 8;

    /// <summary>Block rows in a non-interleaved scan: ceil(<see cref="Height"/> / 8).</summary>
    public int OwnBlocksPerColumn => (Height + 7) / 8;

    /// <summary>64 coefficients per block in natural order, blocks row-major over the coefficient grid.</summary>
    public short[] Coefficients { get; }

    /// <summary>The quantization table latched at the first scan containing this component, in natural order.</summary>
    public ushort[]? Quantization { get; set; }

    public int DcTableId { get; set; }

    public int AcTableId { get; set; }

    public int DcPredictor { get; set; }

    /// <summary>How many scans of the file have coded this component so far.</summary>
    public int ScanCount { get; set; }

    /// <summary>
    /// Progressive successive-approximation state per coefficient (zig-zag index 0..63, as Ss and Se count): the Al of the last scan
    /// that coded it, or -1 before its first scan (T.81 G.1.1.1.2).
    /// </summary>
    public int[] CoefficientBits { get; } = Enumerable.Repeat(-1, 64).ToArray();

    /// <summary>The decoded samples, <see cref="PlaneStride"/> per row; set by the inverse DCT after the last scan.</summary>
    public byte[] Plane { get; set; } = [];

    public int PlaneStride => OwnBlocksPerLine * 8;

    public Span<short> Block(int blockRow, int blockColumn) =>
        Coefficients.AsSpan((blockRow * BlocksPerLine + blockColumn) * 64, 64);
}
