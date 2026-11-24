namespace OldTanks.Docs.Tests;

/// <summary>
/// Every file of the checkout (paths relative to the root, '/'-separated, exact case) and its parsed Markdown.
/// Loaded once per test run.
/// </summary>
internal sealed class Checkout
{
    public const string SolutionFileName = "OldTanks.sln";

    // Build output, tool state and test results hold no docs; skipping .git keeps its objects out of the walk
    private static readonly HashSet<string> s_skippedFolders =
        [".git", ".vs", ".idea", "bin", "obj", "publish", "TestResults", "node_modules"];

    private static readonly Lazy<Checkout> s_current = new(() => Load(FindRoot()));

    private readonly HashSet<string> m_paths;
    private readonly Dictionary<string, MarkdownFile> m_markdown;
    private readonly Dictionary<string, HashSet<string>> m_anchors;

    private Checkout(string root, IReadOnlyList<string> files, IReadOnlyList<MarkdownFile> markdown)
    {
        Root = root;
        Files = files;
        Markdown = markdown;
        m_paths = [.. files, .. files.SelectMany(ParentFolders)];
        m_markdown = markdown.ToDictionary(file => file.Path, StringComparer.Ordinal);
        m_anchors = markdown.ToDictionary(file => file.Path, MarkdownRules.AnchorsOf, StringComparer.Ordinal);
        Links = [.. markdown.SelectMany(MarkdownRules.LinksIn)];
    }

    public static Checkout Current => s_current.Value;

    public string Root { get; }

    public IReadOnlyList<string> Files { get; }

    public IReadOnlyList<MarkdownFile> Markdown { get; }

    public IReadOnlyList<MarkdownLink> Links { get; }

    public static Checkout Load(string root)
    {
        var files = FilesUnder(root)
            .Select(file => Path.GetRelativePath(root, file).Replace('\\', '/'))
            .Order(StringComparer.Ordinal)
            .ToList();
        var markdown = files
            .Where(file => file.EndsWith(".md", StringComparison.Ordinal))
            .Select(file => MarkdownFile.Parse(file, File.ReadAllText(Path.Combine(root, file))))
            .ToList();

        return new Checkout(root, files, markdown);
    }

    public bool Exists(string path) => path.Length == 0 || m_paths.Contains(path);

    public MarkdownFile? MarkdownAt(string path) => m_markdown.GetValueOrDefault(path);

    public string ReadText(string path) => File.ReadAllText(Path.Combine(Root, path));

    /// <summary>The checkout path a link points at; the linking file itself for a bare #fragment; null when nothing is there.</summary>
    public string? Resolve(MarkdownLink link)
    {
        if (link.PathPart.Length == 0)
            return link.Source;

        var resolved = MarkdownRules.ResolveRelative(link.Source, link.PathPart);
        return resolved is not null && Exists(resolved) ? resolved : null;
    }

    public HashSet<string>? Anchors(string path) => m_anchors.GetValueOrDefault(path);

    /// <summary>
    /// Files below a folder, skipping build output and tool state. A folder with its own .git is another checkout
    /// (a Claude Code worktree under .claude/worktrees, a clone): its docs are checked from there.
    /// </summary>
    public static IEnumerable<string> FilesUnder(string folder)
        => Directory.EnumerateFiles(folder).Concat(Directory.EnumerateDirectories(folder)
            .Where(sub => !s_skippedFolders.Contains(Path.GetFileName(sub)) && !Path.Exists(Path.Combine(sub, ".git")))
            .SelectMany(FilesUnder));

    private static IEnumerable<string> ParentFolders(string path)
    {
        for (var slash = path.LastIndexOf('/'); slash > 0; slash = path.LastIndexOf('/', slash - 1))
            yield return path[..slash];
    }

    private static string FindRoot()
    {
        for (var folder = new DirectoryInfo(TestContext.CurrentContext.TestDirectory); folder is not null; folder = folder.Parent)
        {
            if (File.Exists(Path.Combine(folder.FullName, SolutionFileName)))
                return folder.FullName;
        }

        throw new InvalidOperationException(
            $"{SolutionFileName} not found above the test directory: the documentation checks need the whole checkout");
    }
}
