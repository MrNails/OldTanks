namespace OldTanks.Docs.Tests;

/// <summary>
/// The link, anchor and metadata checks of DOCUMENTATION-GUIDE §9, over every Markdown file of the checkout.
/// Paths are compared with their exact case: GitHub and Linux CI are case-sensitive, Windows is not.
/// </summary>
[TestFixture]
public sealed class DocumentationIntegrityTests
{
    [Test]
    public void RelativeLinks_InEveryMarkdownFile_ResolveWithExactCase()
    {
        var checkout = Checkout.Current;
        var failures = checkout.Links
            .Where(link => checkout.Resolve(link) is null)
            .Select(link => $"{link.Where}: no such file or folder [{link.Target}]")
            .ToList();

        Assert.That(failures, Is.Empty, string.Join(Environment.NewLine, failures));
    }

    [Test]
    public void Fragments_IntoMarkdown_NameAHeadingOrAnAnchor()
    {
        var checkout = Checkout.Current;
        var failures = checkout.Links
            .Where(link => link.Fragment.Length > 0 &&
                           checkout.Resolve(link) is { } target &&
                           checkout.Anchors(target) is { } anchors &&
                           !anchors.Contains(link.Fragment))
            .Select(link => $"{link.Where}: no heading or anchor [{link.Target}]")
            .ToList();

        Assert.That(failures, Is.Empty, string.Join(Environment.NewLine, failures));
    }

    [Test]
    public void DocPages_UnderDocs_OpenWithTheMetadataBlock()
    {
        var failures = Checkout.Current.Markdown
            .Where(file => file.Path.StartsWith("docs/", StringComparison.Ordinal) &&
                           !file.Path.EndsWith("/README.md", StringComparison.Ordinal))
            .SelectMany(MarkdownRules.MetadataFailures)
            .ToList();

        Assert.That(failures, Is.Empty, string.Join(Environment.NewLine, failures));
    }

    [Test]
    public void DocPages_UnderDocs_AreLinkedFromAnotherPage()
    {
        var checkout = Checkout.Current;
        var linked = checkout.Links
            .Where(link => link.PathPart.Length > 0)
            .Select(link => (link.Source, Target: checkout.Resolve(link)))
            .Where(pair => pair.Target is not null && pair.Target != pair.Source)
            .Select(pair => pair.Target!)
            .ToHashSet(StringComparer.Ordinal);

        // Archived session entries are reached through the archive folder, not one by one
        var orphans = checkout.Markdown
            .Select(file => file.Path)
            .Where(path => path.StartsWith("docs/", StringComparison.Ordinal) &&
                           !path.StartsWith("docs/sessions/archive/", StringComparison.Ordinal) &&
                           !linked.Contains(path))
            .ToList();

        Assert.That(orphans, Is.Empty, "Pages no other page links to: " + string.Join(", ", orphans));
    }

    [TestCase("KI-005: Untextured objects use a 0-255 color in a 0-1 color space",
        "ki-005-untextured-objects-use-a-0-255-color-in-a-0-1-color-space")]
    [TestCase("3. The step loop (`World.CollideObjects`)", "3-the-step-loop-worldcollideobjects")]
    [TestCase("2. `ObjectRenderer<T>`: batching and instancing", "2-objectrenderert-batching-and-instancing")]
    [TestCase("2026-11-24 — Documentation start (OT-1)", "2026-11-24--documentation-start-ot-1")]
    [TestCase("[Deploy](workflows/DEPLOY.md) steps", "deploy-steps")]
    [TestCase("**Git** rules", "git-rules")]
    public void HeadingSlug_Heading_IsGitHubsAnchor(string heading, string expected)
        => Assert.That(MarkdownRules.HeadingSlug(heading), Is.EqualTo(expected));

    [Test]
    public void AnchorsOf_RepeatedHeadingAndFencedHash_NumbersRepeatsAndSkipsCode()
    {
        var file = MarkdownFile.Parse("docs/x.md",
            "# Notes\n## Setup\n## Setup\n```powershell\n# not a heading\n```\n<a id=\"pinned\"></a>\n");

        Assert.That(MarkdownRules.AnchorsOf(file), Is.EquivalentTo(new[] { "notes", "setup", "setup-1", "pinned" }));
    }

    [Test]
    public void LinksIn_CodeAndExternalUrls_AreSkipped()
    {
        var file = MarkdownFile.Parse("docs/x.md",
            "[a](a.md) `[b](b.md)` [c](https://example.org)\n```\n[d](d.md)\n```\n[e]: ../e.md\n");

        Assert.That(MarkdownRules.LinksIn(file).Select(link => link.Target), Is.EqualTo(new[] { "a.md", "../e.md" }));
    }

    [TestCase("docs/CoolEngine/PhysicEngine/X.md", "../../../ARCHITECTURE.md", "ARCHITECTURE.md")]
    [TestCase("docs/x.md", "../AGENTS.md", "AGENTS.md")]
    [TestCase("AGENTS.md", "docs/", "docs")]
    [TestCase("docs/x.md", "/OldTanks/Program.cs", "OldTanks/Program.cs")]
    [TestCase("docs/x.md", "../../outside.md", null)]
    public void ResolveRelative_LinkFromAFile_IsTheCheckoutPath(string source, string link, string? expected)
        => Assert.That(MarkdownRules.ResolveRelative(source, link), Is.EqualTo(expected));

    [Test]
    public void MetadataFailures_OffVocabularyStatusNoteInDateAndBareVerified_AreAllReported()
    {
        var page = MarkdownFile.Parse("docs/x.md",
            "# X\n\n| Field | Value |\n|---|---|\n| Status | Active |\n| Last updated | 2026-11-24 (notes added) |\n" +
            "| Verified against | 2026-11-24 |\n");

        Assert.That(MarkdownRules.MetadataFailures(page).Count(), Is.EqualTo(3));
    }

    [Test]
    public void MetadataFailures_CompleteBlock_ReportsNothing()
    {
        var page = MarkdownFile.Parse("docs/x.md",
            "# X\n\n| Field | Value |\n|---|---|\n| Status | Active plan - step 3 open |\n| Last updated | 2026-11-24 |\n" +
            "| Verified against | 2026-11-24; develop at d32cbbf; whole page |\n");

        Assert.That(MarkdownRules.MetadataFailures(page), Is.Empty);
    }

    [Test]
    public void FilesUnder_NestedCheckoutAndBuildOutput_AreSkipped()
    {
        var root = Directory.CreateTempSubdirectory("oldtanks-docs-tests").FullName;

        void Write(params string[] parts)
        {
            Directory.CreateDirectory(Path.Combine([root, .. parts[..^1]]));
            File.WriteAllText(Path.Combine([root, .. parts]), "");
        }

        try
        {
            Write("README.md");
            Write("OldTanks", "bin", "Debug", "NOTICE.md");
            Write(".claude", "worktrees", "other", ".git");
            Write(".claude", "worktrees", "other", "README.md");
            // A plain folder there (an agent run's working files) is no checkout, and is skipped all the same.
            Write(".claude", "worktrees", "run-notes", "plan.md");
            // The rest of .claude, and a folder that only shares the name, are still walked.
            Write(".claude", "skills", "x", "SKILL.md");
            Write("docs", "worktrees", "NOTES.md");

            Assert.That(Checkout.FilesUnder(root).Select(file => Path.GetRelativePath(root, file)),
                Is.EquivalentTo(new[]
                {
                    "README.md",
                    Path.Combine(".claude", "skills", "x", "SKILL.md"),
                    Path.Combine("docs", "worktrees", "NOTES.md"),
                }));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }
}
