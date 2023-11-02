namespace CoolEngine.GraphicalEngine.Core.Font;

internal class FontLoadData
{
    public FontLoadData(IntPtr fontDataPtr, int width, int height)
    {
        FontDataPtr = fontDataPtr;
        Width = width;
        Height = height;
    }

    public IntPtr FontDataPtr { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }

    public byte[] LoadedImage { get; set; } = null!;
}