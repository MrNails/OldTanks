namespace CoolEngine.Fonts.Woff;

/// <summary>Decodes WOFF 1.0 into the sfnt image it wraps. A stub until OT-4 WP3.</summary>
internal static class WoffDecoder
{
    /// <param name="data">The whole WOFF file, signature included.</param>
    /// <returns>A complete sfnt or collection image, which <see cref="FontLoader"/> then parses.</returns>
    /// <exception cref="NotSupportedException">Always, until WP3.</exception>
    public static byte[] Decode(ReadOnlySpan<byte> data) => throw new NotSupportedException("WOFF: OT-4 WP3");
}
