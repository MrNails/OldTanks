using System.Globalization;
using System.Text.RegularExpressions;

namespace OldTanks.Docs.Tests;

/// <summary>
/// The Markdown rules the checks rely on: GitHub's heading anchors, relative link resolution and the metadata block
/// of DOCUMENTATION-GUIDE §1. Adapted from ArbBot's documentation checks.
/// </summary>
internal static partial class MarkdownRules
{
    public static readonly string[] StatusValues = ["Living", "Snapshot", "Historical", "Active plan", "Draft"];

    // The metadata table sits right under the H1 (DOCUMENTATION-GUIDE §1)
    private const int MetadataLines = 15;

    public static IEnumerable<MarkdownLink> LinksIn(MarkdownFile file)
        => file.Prose.SelectMany((line, index) => InlineLink().Matches(line)
            .Concat(ReferenceDefinition().Matches(line))
            .Select(match => match.Groups["target"].Value)
            .Where(target => !HasScheme().IsMatch(target))
            .Select(target => new MarkdownLink(file.Path, index + 1, target)));

    /// <summary>
    /// The checkout path a link points at: relative to the linking file's folder, or to the checkout root when it
    /// starts with '/'. Null when it climbs out of the checkout.
    /// </summary>
    public static string? ResolveRelative(string source, string link)
    {
        List<string> parts = link.StartsWith('/') ? [] : [.. source.Split('/').SkipLast(1)];

        foreach (var part in link.Split('/').Where(part => part is not ("" or ".")))
        {
            if (part != "..")
            {
                parts.Add(part);
                continue;
            }

            if (parts.Count == 0)
                return null;

            parts.RemoveAt(parts.Count - 1);
        }

        return string.Join('/', parts);
    }

    /// <summary>
    /// GitHub's rule: the heading's text in lower case, everything but letters, marks, numbers, '_', '-' and spaces
    /// dropped, each space a '-'. So an em dash between two spaces leaves "--".
    /// </summary>
    public static string HeadingSlug(string heading)
    {
        var text = LinkText().Replace(heading, "${text}").Replace("`", "").Replace("*", "");
        return SlugDropped().Replace(text.Trim().ToLowerInvariant(), "").Replace(' ', '-');
    }

    /// <summary>Heading anchors (a repeated heading is numbered: setup, setup-1) plus explicit &lt;a id&gt; anchors.</summary>
    public static HashSet<string> AnchorsOf(MarkdownFile file)
    {
        var anchors = new HashSet<string>(StringComparer.Ordinal);
        var slugs = file.Lines
            .Where((_, index) => !file.Fenced[index])
            .Select(line => HeadingLine().Match(line))
            .Where(heading => heading.Success)
            .Select(heading => HeadingSlug(heading.Groups["text"].Value));

        foreach (var slug in slugs)
        {
            var candidate = slug;
            for (var repeat = 1; !anchors.Add(candidate); repeat++)
                candidate = $"{slug}-{repeat}";
        }

        anchors.UnionWith(file.Prose.SelectMany(line => AnchorTag().Matches(line)).Select(tag => tag.Groups["id"].Value));
        return anchors;
    }

    public static IEnumerable<string> MetadataFailures(MarkdownFile file)
    {
        if (!file.Lines.FirstOrDefault(line => line.Length > 0, "").StartsWith("# ", StringComparison.Ordinal))
            yield return $"{file.Path}:1: the page doesn't open with an H1";

        var status = MetadataValue(file, "Status");
        if (status is null || !StatusValues.Any(value =>
                status == value || status.StartsWith(value + " - ", StringComparison.Ordinal)))
        {
            yield return $"{file.Path}: Status is one of {string.Join(", ", StatusValues)}, " +
                         $"optionally followed by \" - \" and a qualifier [{status}]";
        }

        var lastUpdated = MetadataValue(file, "Last updated");
        if (lastUpdated is null || !IsDate(lastUpdated))
            yield return $"{file.Path}: Last updated is a bare YYYY-MM-DD date [{lastUpdated}]";

        var verified = MetadataValue(file, "Verified against");
        if (verified is not null &&
            !(VerifiedAgainstValue().Match(verified) is { Success: true } match && IsDate(match.Groups["date"].Value)))
        {
            yield return $"{file.Path}: Verified against is \"date; what was checked against; the scope\" [{verified}]";
        }
    }

    public static string? MetadataValue(MarkdownFile file, string field)
        => file.Lines.Take(MetadataLines)
            .Select(line => MetadataRow().Match(line))
            .Where(row => row.Success && row.Groups["field"].Value == field)
            .Select(row => row.Groups["value"].Value)
            .FirstOrDefault();

    public static bool IsDate(string value)
        => DateOnly.TryParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out _);

    [GeneratedRegex(@"!?\[(?:[^\[\]]|\[[^\]]*\])*\]\(\s*<?(?<target>[^\s()<>]+)>?(?:\s+""[^""]*"")?\s*\)")]
    private static partial Regex InlineLink();

    [GeneratedRegex(@"^\s{0,3}\[[^\]]+\]:\s*<?(?<target>[^\s<>]+)>?")]
    private static partial Regex ReferenceDefinition();

    [GeneratedRegex(@"^[A-Za-z][A-Za-z0-9+.-]*:")]
    private static partial Regex HasScheme();

    [GeneratedRegex(@"`[^`]*`")]
    public static partial Regex InlineCode();

    [GeneratedRegex(@"^#{1,6}\s+(?<text>.*?)(?:\s+#+)?\s*$")]
    public static partial Regex HeadingLine();

    [GeneratedRegex(@"\[(?<text>[^\]]*)\]\([^)]*\)")]
    private static partial Regex LinkText();

    [GeneratedRegex(@"[^\p{L}\p{M}\p{N}\p{Pc}\- ]")]
    private static partial Regex SlugDropped();

    [GeneratedRegex(@"<a\s+(?:id|name)=""(?<id>[^""]+)""")]
    private static partial Regex AnchorTag();

    [GeneratedRegex(@"^\|\s*(?<field>[^|]+?)\s*\|\s*(?<value>[^|]*?)\s*\|\s*$")]
    private static partial Regex MetadataRow();

    [GeneratedRegex(@"^(?<date>\d{4}-\d{2}-\d{2}); [^;]+; .+$")]
    private static partial Regex VerifiedAgainstValue();
}
