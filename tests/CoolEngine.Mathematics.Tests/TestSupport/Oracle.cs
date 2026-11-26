using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Numerics = System.Numerics;
using OpenTKMath = OpenTK.Mathematics;

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
/// The library's float vectors and quaternion are System.Numerics' own types; the named conversions here cover the
/// matrices (to and from <c>Matrix4x4</c> and OpenTK's matrices, byte for byte) and the vectors to OpenTK.
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

    // Vectors convert to OpenTK field by field. The float vectors and the quaternion are System.Numerics' own types, so
    // they need no conversion to that oracle.

    public static OpenTKMath.Vector2 ToOpenTK(Vector2 value)
    {
        return new OpenTKMath.Vector2(value.X, value.Y);
    }

    public static OpenTKMath.Vector3 ToOpenTK(Vector3 value)
    {
        return new OpenTKMath.Vector3(value.X, value.Y, value.Z);
    }

    public static Vector2 FromOpenTK(OpenTKMath.Vector2 value)
    {
        return new Vector2(value.X, value.Y);
    }

    public static Vector3 FromOpenTK(OpenTKMath.Vector3 value)
    {
        return new Vector3(value.X, value.Y, value.Z);
    }

    // Matrices move byte for byte: the same memory is the same transform (see the class summary).

    public static Numerics.Matrix4x4 ToNumerics(Matrix4 value)
    {
        return Reinterpret<Matrix4, Numerics.Matrix4x4>(value);
    }

    public static Matrix4 FromNumerics(Numerics.Matrix4x4 value)
    {
        return Reinterpret<Numerics.Matrix4x4, Matrix4>(value);
    }

    public static OpenTKMath.Matrix2 ToOpenTK(Matrix2 value)
    {
        return Reinterpret<Matrix2, OpenTKMath.Matrix2>(value);
    }

    public static OpenTKMath.Matrix3 ToOpenTK(Matrix3 value)
    {
        return Reinterpret<Matrix3, OpenTKMath.Matrix3>(value);
    }

    public static OpenTKMath.Matrix4 ToOpenTK(Matrix4 value)
    {
        return Reinterpret<Matrix4, OpenTKMath.Matrix4>(value);
    }

    public static Matrix2 FromOpenTK(OpenTKMath.Matrix2 value)
    {
        return Reinterpret<OpenTKMath.Matrix2, Matrix2>(value);
    }

    public static Matrix3 FromOpenTK(OpenTKMath.Matrix3 value)
    {
        return Reinterpret<OpenTKMath.Matrix3, Matrix3>(value);
    }

    public static Matrix4 FromOpenTK(OpenTKMath.Matrix4 value)
    {
        return Reinterpret<OpenTKMath.Matrix4, Matrix4>(value);
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
