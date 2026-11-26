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

    // Double types: OpenTK's double types are the oracle. Vectors and quaternions convert field by field, matrices byte
    // for byte (OpenTK's row-vector, row-major double matrices have the bytes of our column-major ones).

    public static OpenTKMath.Vector2d ToOpenTK(Vector2d value) => new(value.X, value.Y);

    public static OpenTKMath.Vector3d ToOpenTK(Vector3d value) => new(value.X, value.Y, value.Z);

    public static OpenTKMath.Vector4d ToOpenTK(Vector4d value) => new(value.X, value.Y, value.Z, value.W);

    public static OpenTKMath.Quaterniond ToOpenTK(Quaterniond value) => new(value.X, value.Y, value.Z, value.W);

    public static Vector2d FromOpenTK(OpenTKMath.Vector2d value) => new(value.X, value.Y);

    public static Vector3d FromOpenTK(OpenTKMath.Vector3d value) => new(value.X, value.Y, value.Z);

    public static Vector4d FromOpenTK(OpenTKMath.Vector4d value) => new(value.X, value.Y, value.Z, value.W);

    public static Quaterniond FromOpenTK(OpenTKMath.Quaterniond value) => new(value.X, value.Y, value.Z, value.W);

    public static OpenTKMath.Matrix2d ToOpenTK(Matrix2d value) => Reinterpret<Matrix2d, OpenTKMath.Matrix2d>(value);

    public static OpenTKMath.Matrix3d ToOpenTK(Matrix3d value) => Reinterpret<Matrix3d, OpenTKMath.Matrix3d>(value);

    public static OpenTKMath.Matrix4d ToOpenTK(Matrix4d value) => Reinterpret<Matrix4d, OpenTKMath.Matrix4d>(value);

    public static Matrix2d FromOpenTK(OpenTKMath.Matrix2d value) => Reinterpret<OpenTKMath.Matrix2d, Matrix2d>(value);

    public static Matrix3d FromOpenTK(OpenTKMath.Matrix3d value) => Reinterpret<OpenTKMath.Matrix3d, Matrix3d>(value);

    public static Matrix4d FromOpenTK(OpenTKMath.Matrix4d value) => Reinterpret<OpenTKMath.Matrix4d, Matrix4d>(value);

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
