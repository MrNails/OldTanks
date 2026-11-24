namespace OldTanks.Docs.Tests;

/// <summary>
/// One Markdown file of the checkout. <see cref="Prose"/> is the text a link or anchor can sit in: lines inside a
/// code fence are blank there and inline code is cut out. <see cref="Fenced"/> marks fence lines and fenced content.
/// </summary>
internal sealed record MarkdownFile(string Path, string[] Lines, string[] Prose, bool[] Fenced)
{
    public static MarkdownFile Parse(string path, string text)
    {
        var lines = text.ReplaceLineEndings("\n").Split('\n');
        var prose = new string[lines.Length];
        var fenced = new bool[lines.Length];
        var inFence = false;

        for (var index = 0; index < lines.Length; index++)
        {
            var trimmed = lines[index].TrimStart();
            var fence = trimmed.StartsWith("```", StringComparison.Ordinal) ||
                        trimmed.StartsWith("~~~", StringComparison.Ordinal);
            inFence ^= fence;
            fenced[index] = fence || inFence;
            prose[index] = fenced[index] ? "" : MarkdownRules.InlineCode().Replace(lines[index], "");
        }

        return new MarkdownFile(path, lines, prose, fenced);
    }
}

/// <summary>A relative link found in a Markdown file.</summary>
internal readonly record struct MarkdownLink(string Source, int Line, string Target)
{
    public string Where => $"{Source}:{Line}";

    public string Fragment => Target.IndexOf('#') is var hash and >= 0
        ? Uri.UnescapeDataString(Target[(hash + 1)..])
        : "";

    public string PathPart => Uri.UnescapeDataString(Target.Split('#')[0]);
}
