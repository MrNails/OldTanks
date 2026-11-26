using System.Collections.Concurrent;

namespace CoolEngine.Fonts.Tests.Reference;

/// <summary>Test font bytes and their parsed files, read once per test run.</summary>
internal static class FontCache
{
    private static readonly ConcurrentDictionary<string, byte[]> s_bytes = new(StringComparer.OrdinalIgnoreCase);
    private static readonly ConcurrentDictionary<string, FontFile> s_files = new(StringComparer.OrdinalIgnoreCase);

    public static byte[] Bytes(string path) => s_bytes.GetOrAdd(path, System.IO.File.ReadAllBytes);

    public static FontFile File(string path) => s_files.GetOrAdd(path, key => FontLoader.Load(Bytes(key)));

    public static FontFace Face(TestFace face) => File(face.Path).Faces[face.FaceIndex];

    /// <summary>The repository's Arial: the base of most crafted copies. Callers get their own copy.</summary>
    public static byte[] Arial() => (byte[])Bytes(TestFonts.RepositoryArial).Clone();
}
