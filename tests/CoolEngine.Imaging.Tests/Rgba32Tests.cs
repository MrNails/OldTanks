using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace CoolEngine.Imaging.Tests;

[TestFixture]
public sealed class Rgba32Tests
{
    [Test]
    public void SizeOf_IsFourBytesInRgbaOrder()
    {
        Rgba32[] pixels = [new Rgba32(1, 2, 3, 4)];

        var size = Unsafe.SizeOf<Rgba32>();
        var bytes = MemoryMarshal.AsBytes(pixels.AsSpan()).ToArray();

        Assert.Multiple(() =>
        {
            Assert.That(size, Is.EqualTo(4));
            Assert.That(bytes, Is.EqualTo(new byte[] { 1, 2, 3, 4 }));
        });
    }
}
