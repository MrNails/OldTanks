using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace CoolEngine.Mathematics.Tests.TestSupport;

/// <summary>
/// The bridge to the oracles, System.Numerics (float) and OpenTK (float and double).
/// <para>
/// Matrices are compared on identical bytes: our matrices are column vectors with column-major storage, theirs are
/// row vectors with row-major storage, so the same 16 floats in memory are the same transform. Read through the other
/// convention, their <c>A * B</c> is our <c>B * A</c> and their <c>Vector4.Transform(v, M)</c> is our <c>M * v</c>.
/// <see cref="Reinterpret{TFrom, TTo}"/> moves a value between the two worlds byte for byte and
/// <see cref="SameBytes{TOurs, TTheirs}"/> asserts two values have the same memory.
/// </para>
/// The named conversions of each library type (to and from System.Numerics and OpenTK) are added in the stage that adds
/// the type, built on <see cref="Reinterpret{TFrom, TTo}"/> where the layouts match.
/// </summary>
internal static class Oracle
{
    /// <summary>
    /// The same bytes read as another type of the same size; throws <see cref="ArgumentException"/> when the sizes differ.
    /// </summary>
    public static TTo Reinterpret<TFrom, TTo>(TFrom value) where TFrom : unmanaged where TTo : unmanaged
    {
        if (Unsafe.SizeOf<TFrom>() != Unsafe.SizeOf<TTo>())
        {
            throw new ArgumentException(
                $"{typeof(TFrom).Name} ({Unsafe.SizeOf<TFrom>()} B) and {typeof(TTo).Name} ({Unsafe.SizeOf<TTo>()} B) differ in size.");
        }

        return Unsafe.BitCast<TFrom, TTo>(value);
    }

    /// <summary>The bytes of <paramref name="value"/> in memory order.</summary>
    public static byte[] Bytes<T>(T value) where T : unmanaged
    {
        return MemoryMarshal.AsBytes(new ReadOnlySpan<T>(in value)).ToArray();
    }

    /// <summary>Asserts that our value and the oracle's have the same size and the same bytes.</summary>
    public static void SameBytes<TOurs, TTheirs>(TOurs ours, TTheirs theirs, string? context = null)
        where TOurs : unmanaged where TTheirs : unmanaged
    {
        var prefix = context is null ? "" : context + ": ";
        Assert.That(Bytes(ours), Is.EqualTo(Bytes(theirs)), $"{prefix}{typeof(TOurs).Name} against {typeof(TTheirs).Name} byte for byte");
    }
}
