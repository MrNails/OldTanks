namespace CoolEngine.GraphicalEngine.Core.Font;

/// <summary>
/// The CPU result of building a font: the atlas (row-major, one byte per pixel, <see cref="Width"/> × <see cref="Height"/>
/// bytes at the start of <see cref="Pixels"/>) and the table of its characters.
/// </summary>
/// <remarks>
/// <see cref="Pixels"/> is rented from <see cref="System.Buffers.ArrayPool{T}.Shared"/> and may be longer than
/// <c>Width * Height</c>; whoever takes the atlas returns it to the pool.
/// </remarks>
internal sealed record FontAtlas(byte[] Pixels, int Width, int Height, Dictionary<char, CharacterInfo> Characters);
