namespace CoolEngine.Fonts.Woff;

/// <summary>Decodes WOFF2 into the sfnt or collection image it wraps. A stub until OT-4 WP3.</summary>
internal static class Woff2Decoder
{
    /// <param name="data">The whole WOFF2 file, signature included.</param>
    /// <returns>A complete sfnt or collection image, which <see cref="FontLoader"/> then parses.</returns>
    /// <exception cref="NotSupportedException">Always, until WP3.</exception>
    public static byte[] Decode(ReadOnlySpan<byte> data) => throw new NotSupportedException("WOFF2: OT-4 WP3");
}
