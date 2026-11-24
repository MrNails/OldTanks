using System.Text.RegularExpressions;

namespace OldTanks.Docs.Tests;

/// <summary>
/// The OldTanks-specific rules of DOCUMENTATION-GUIDE: AGENTS.md guides and their CLAUDE.md adapters (§4),
/// source-mirroring feature pages (§6), the KI register (§7), the session-log retention (§5) and
/// "symbols, never line numbers" (§3).
/// </summary>
[TestFixture]
public sealed partial class RepositoryConventionTests
{
    private const string RootGuide = "AGENTS.md";
    private const string KnownIssuesPage = "docs/KNOWN-ISSUES.md";
    private const string SessionLogPage = "docs/sessions/SESSION-LOG.md";
    private const int SessionLogEntries = 5;

    private static readonly string[] s_knownIssueStatuses = ["Open", "Open by design", "Partially fixed", "Closed"];

    [Test]
    public void AgentsGuides_EachHasAClaudeAdapterThatOnlyImportsIt()
    {
        var checkout = Checkout.Current;
        var failures = new List<string>();

        foreach (var guide in AgentsGuides(checkout))
        {
            var adapter = guide[..^"AGENTS.md".Length] + "CLAUDE.md";
            if (!checkout.Exists(adapter))
            {
                failures.Add($"{guide}: no CLAUDE.md adapter beside it");
                continue;
            }

            var lines = checkout.ReadText(adapter).ReplaceLineEndings("\n").Split('\n')
                .Select(line => line.Trim())
                .Where(line => line.Length > 0)
                .ToList();

            // The root adapter may carry a title line; every adapter holds the import and nothing else
            if (guide == RootGuide && lines.Count == 2 && lines[0].StartsWith("# ", StringComparison.Ordinal))
                lines.RemoveAt(0);

            if (lines is not ["@AGENTS.md"])
                failures.Add($"{adapter}: an adapter holds only the line @AGENTS.md");
        }

        Assert.That(failures, Is.Empty, string.Join(Environment.NewLine, failures));
    }

    [Test]
    public void RootGuideMap_LinksEveryAgentsGuide()
    {
        var checkout = Checkout.Current;
        var root = checkout.MarkdownAt(RootGuide);
        Assert.That(root, Is.Not.Null, "The root AGENTS.md is missing");

        var linked = MarkdownRules.LinksIn(root!)
            .Select(checkout.Resolve)
            .OfType<string>()
            .ToHashSet(StringComparer.Ordinal);
        var missing = AgentsGuides(checkout).Where(guide => guide != RootGuide && !linked.Contains(guide)).ToList();

        Assert.That(missing, Is.Empty, "Guides missing from the root guide map: " + string.Join(", ", missing));
    }

    [Test]
    public void ProjectGuides_TargetFramework_MatchesTheProjectFile()
    {
        var checkout = Checkout.Current;
        var failures = new List<string>();

        foreach (var guide in AgentsGuides(checkout).Where(guide => guide != RootGuide))
        {
            var folder = guide[..guide.LastIndexOf('/')];
            var project = $"{folder}/{folder[(folder.LastIndexOf('/') + 1)..]}.csproj";
            if (!checkout.Exists(project))
                continue;

            var expected = TargetFramework().Match(checkout.ReadText(project)).Groups["tfm"].Value;
            var documented = checkout.MarkdownAt(guide)!.Lines
                .Select(line => TargetFrameworkRow().Match(line))
                .FirstOrDefault(row => row.Success)?.Groups["tfm"].Value;

            if (documented != expected)
                failures.Add($"{guide}: Target framework says [{documented}], {project} has [{expected}]");
        }

        Assert.That(failures, Is.Empty, string.Join(Environment.NewLine, failures));
    }

    [Test]
    public void FeaturePages_UnderAProjectFolder_MirrorAnExistingSourceFolder()
    {
        var checkout = Checkout.Current;
        var failures = checkout.Markdown
            .Select(file => file.Path)
            .Where(path => path.StartsWith("docs/", StringComparison.Ordinal) && path.Count(c => c == '/') >= 2)
            .Select(path => (Page: path, Folder: path["docs/".Length..path.LastIndexOf('/')]))
            .Where(page => IsProjectFolder(checkout, page.Folder.Split('/')[0]) && !checkout.Exists(page.Folder))
            .Select(page => $"{page.Page}: no source folder {page.Folder}")
            .ToList();

        Assert.That(failures, Is.Empty, string.Join(Environment.NewLine, failures));
    }

    [Test]
    public void KnownIssues_IndexAndEntries_Agree()
    {
        var page = Checkout.Current.MarkdownAt(KnownIssuesPage);
        Assert.That(page, Is.Not.Null, $"{KnownIssuesPage} is missing");

        var index = page!.Lines.Select(line => KnownIssueIndexRow().Match(line)).Where(row => row.Success).ToList();
        var entries = page.Lines
            .Select((line, number) => (Match: KnownIssueHeading().Match(line), Number: number))
            .Where(heading => heading.Match.Success)
            .Select(heading => (
                Id: heading.Match.Groups["id"].Value,
                Title: heading.Match.Groups["title"].Value,
                Anchor: MarkdownRules.HeadingSlug(heading.Match.Groups["text"].Value),
                Status: page.Lines.Skip(heading.Number + 1)
                    .Select(line => KnownIssueStatusRow().Match(line))
                    .FirstOrDefault(row => row.Success)?.Groups["status"].Value))
            .ToList();
        var failures = new List<string>();

        var ids = entries.Select(entry => entry.Id).ToList();
        var expectedIds = Enumerable.Range(1, entries.Count).Select(n => $"KI-{n:000}").ToList();
        if (!ids.SequenceEqual(expectedIds))
            failures.Add($"Entries are numbered KI-001 upwards with no gap or repeat: [{string.Join(", ", ids)}]");

        if (!index.Select(row => row.Groups["id"].Value).SequenceEqual(ids))
            failures.Add("The index lists every entry once, in order");

        foreach (var (row, entry) in index.Zip(entries))
        {
            var indexStatus = row.Groups["status"].Value;
            if (row.Groups["anchor"].Value != entry.Anchor)
                failures.Add($"{entry.Id}: the index links #{row.Groups["anchor"].Value}, the heading's anchor is #{entry.Anchor}");
            if (row.Groups["title"].Value != entry.Title)
                failures.Add($"{entry.Id}: the index title differs from the heading");
            if (!s_knownIssueStatuses.Contains(indexStatus))
                failures.Add($"{entry.Id}: index status [{indexStatus}] is not one of {string.Join(", ", s_knownIssueStatuses)}");
            if (entry.Status is null || !entry.Status.StartsWith(indexStatus, StringComparison.Ordinal))
                failures.Add($"{entry.Id}: the entry's Status [{entry.Status}] does not repeat the index's [{indexStatus}]");
        }

        Assert.That(failures, Is.Empty, string.Join(Environment.NewLine, failures));
    }

    [Test]
    public void SessionLog_HoldsTheNewestEntriesOnly()
    {
        var page = Checkout.Current.MarkdownAt(SessionLogPage);
        Assert.That(page, Is.Not.Null, $"{SessionLogPage} is missing");

        var entries = page!.Lines.Count(line => SessionEntryHeading().IsMatch(line));

        Assert.That(entries, Is.InRange(1, SessionLogEntries),
            $"The live log holds 1 to {SessionLogEntries} entries; move older ones to docs/sessions/archive/YYYY-MM.md");
    }

    [Test]
    public void Markdown_NeverCitesSourceLineNumbers()
    {
        var failures = Checkout.Current.Markdown
            .SelectMany(file => file.Lines
                .Select((line, index) => (Line: line, Number: index + 1, Fenced: file.Fenced[index]))
                .Where(line => !line.Fenced && LineNumberReference().IsMatch(line.Line))
                .Select(line => $"{file.Path}:{line.Number}: cites a line number; reference the symbol instead"))
            .ToList();

        Assert.That(failures, Is.Empty, string.Join(Environment.NewLine, failures));
    }

    [TestCase("See `World.cs:146` for the reset", true)]
    [TestCase("Shader.cs#L84", true)]
    [TestCase("`World.CollideObjects` resets `Stop`", false)]
    [TestCase("2026-11-24 12:30 UTC", false)]
    public void LineNumberReference_Text_IsDetected(string text, bool expected)
        => Assert.That(LineNumberReference().IsMatch(text), Is.EqualTo(expected));

    private static IEnumerable<string> AgentsGuides(Checkout checkout)
        => checkout.Files.Where(file => file == RootGuide || file.EndsWith("/" + RootGuide, StringComparison.Ordinal));

    private static bool IsProjectFolder(Checkout checkout, string folder)
        => checkout.Exists($"{folder}/{folder}.csproj");

    [GeneratedRegex(@"<TargetFramework>\s*(?<tfm>[^<\s]+)\s*</TargetFramework>")]
    private static partial Regex TargetFramework();

    [GeneratedRegex(@"^\|\s*Target framework\s*\|\s*`(?<tfm>[^`]+)`")]
    private static partial Regex TargetFrameworkRow();

    [GeneratedRegex(@"^\|\s*\[(?<id>KI-\d{3})\]\(#(?<anchor>[^)]+)\)\s*\|\s*(?<title>.+?)\s*\|\s*[^|]+?\s*\|\s*(?<status>[^|]+?)\s*\|\s*$")]
    private static partial Regex KnownIssueIndexRow();

    [GeneratedRegex(@"^##\s+(?<text>(?<id>KI-\d{3}): (?<title>.+?))\s*$")]
    private static partial Regex KnownIssueHeading();

    [GeneratedRegex(@"^\|\s*\*\*Status\*\*\s*\|\s*(?<status>[^|]+?)\s*\|")]
    private static partial Regex KnownIssueStatusRow();

    [GeneratedRegex(@"^## \d{4}-\d{2}-\d{2} ")]
    private static partial Regex SessionEntryHeading();

    [GeneratedRegex(@"\.(?:cs|csproj|sln|props|vert|frag|obj|json):\d+|\.\w+#L\d+")]
    private static partial Regex LineNumberReference();
}
