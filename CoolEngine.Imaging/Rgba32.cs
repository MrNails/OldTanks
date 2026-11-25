using System.Runtime.InteropServices;

namespace CoolEngine.Imaging;

/// <summary>
/// One pixel with 8-bit red, green, blue and alpha channels, laid out as four bytes in that order,
/// so an array of them uploads to OpenGL as <c>Rgba</c> / <c>UnsignedByte</c>.
/// </summary>
[StructLayout(LayoutKind.Sequential, Size = 4)]
public readonly record struct Rgba32(byte R, byte G, byte B, byte A)
{
    public override string ToString() => $"Rgba32({R}, {G}, {B}, {A})";
}
