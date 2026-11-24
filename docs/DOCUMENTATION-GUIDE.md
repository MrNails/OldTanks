# Documentation Guide

| Field        | Value                                                              |
|--------------|--------------------------------------------------------------------|
| Status       | Living - conventions, adapted from ArbBot, the Crypto KB and AnalysisTools |
| Last updated | 2026-11-24                                                         |

---

Same philosophy as Denys's other repositories: **docs are Claude's memory.** If knowledge isn't written down, the next session starts without it. OldTanks takes the session bootstrap, session log and placement table from ArbBot and the Crypto KB, and the `AGENTS.md` guides, source-mirroring feature pages and `KI-nnn` register from AnalysisTools.

## 1. Freshness metadata (required)

Every page under `docs/` opens with its H1, then this block. `README.md` files and `AGENTS.md` guides are the only exceptions.

```markdown
| Field        | Value      |
|--------------|------------|
| Status       | Living     |
| Last updated | YYYY-MM-DD |
```

**Status vocabulary:** `Living` (kept current forever), `Snapshot` (point-in-time, never updated), `Historical` (superseded, kept for audit), and for plans `Draft` (not agreed yet) or `Active plan` (agreed, work open). A short qualifier may follow after ` - `. `Last updated` is a bare date: bump it on any substantive edit, never for a link, typo or formatting fix.

**Optional `Verified against` row**, after `Last updated`: the latest check of the page's claims against the code, in three parts separated by `; `: the date; the commit checked against; the scope. Add it only after a real check and never backfill it; replace it on the next check. Example: `2026-11-24; develop at d32cbbf; whole page`.

## 2. Placement

| Content | Goes to |
|---------|---------|
| Mission, current state, conventions, rules of thumb | `PROJECT-KNOWLEDGE.md` (about 10 KiB, read in full every session) |
| Projects, threads, frame loop, singletons, synchronization | `ARCHITECTURE.md` |
| Build, run, settings and log locations, the warning baseline | `BUILD.md` |
| C# conventions and Git rules | `CODE-STYLE.md` |
| A feature of one project | `docs/<Project>/<path of its source folder>/<TOPIC>.md` (§6) |
| A defect that is understood but not fixed | `KNOWN-ISSUES.md`, next `KI-nnn` (§7) |
| Open work, unwired features, technical debt | `BACKLOG.md` |
| Proposed or multi-step work | `plans/<NAME>.md` (create the folder on first need) |
| A decision worth keeping with its reason | `ARCHITECTURE-DECISIONS.md`, next `ADR-nnn` (create on first need; record the reason given, never an inferred one) |
| Term definitions | `knowledge/GLOSSARY.md` |
| Per-session journal | `sessions/SESSION-LOG.md` (§5) |
| How agents run a task: tiers, briefs, review checklist, calibration | `.claude/skills/mentor-pipeline/` (outside `docs/`; changed only with Denys's agreement, the calibration rows aside) |

Create a new page only for a genuinely new topic; otherwise extend the owning page. File names are SCREAMING-KEBAB-CASE.

## 3. Style rules

- **Tables over prose** for enumerable facts. Bold the key term at the start of a bullet; keep each section scannable in under 30 seconds.
- **No pasted source code.** Reference files by repository path (`CoolEngine/Services/Renderers/ObjectRenderer.cs`) and symbols by fully-qualified name (`CoolEngine.Services.Renderers.ObjectRenderer<T>.Render`), **never by line number**. Command lines meant to be run, and short identifiers or literals, are fine.
- **Relative Markdown links** between docs, with the target's exact case (GitHub is case-sensitive). Verify that each link and each path resolves when you write it.
- **Say what was checked.** A behaviour read from code but not observed in the running game says so ("reasoned from code").
- **Same name, two types.** OldTanks has several (two `EngineSettings`, two `MainWindow`, two `Face`); always write the namespace when the short name is ambiguous ([Glossary](knowledge/GLOSSARY.md#name-clashes)).
- **English** for docs, code, comments and commit messages.

## 4. `AGENTS.md` guides

An `AGENTS.md` is an orientation pointer, not documentation: it points at the `docs/` pages for its area and gives a small, accurate map. Each `AGENTS.md` has a `CLAUDE.md` adapter beside it holding only `@AGENTS.md` (the root one adds a title line), so Claude Code loads it; an adapter holds no prose.

| Section | Content |
|---------|---------|
| Title + project-knowledge link | `> **Project knowledge:** <relative link to docs/PROJECT-KNOWLEDGE.md>` |
| One-line description | What the project does |
| Related Docs | Topic → relative link into `docs/` |
| Project Details | Namespace root, target framework (as in the `.csproj`), layer |
| Key Types | Type → one-line purpose |
| Dependencies | Project and package references, matching the `.csproj` |
| Constraints | What not to do here, and why |

A guide exists for `CoolEngine`, `OldTanks`, `OldTanks.UI` and `OldTanks.UI.SourceGenerators`. `Common` is small enough for the root guide, and so is `tests/OldTanks.Docs.Tests`, whose rules are §9. A rename, move or delete in a project updates its guide in the same change.

**Budgets** (a prompt to review, not a hard cut): root `AGENTS.md` 6 KiB, other guides 8 KiB, `PROJECT-KNOWLEDGE.md` and `README.md` indexes 10 KiB, other pages 15 KiB.

## 5. Session log format

The log keeps what nothing else records: **what was decided, by whom, and why**. Add the entry at the top of `sessions/SESSION-LOG.md`, under the file's header:

```markdown
## YYYY-MM-DD — <one-line summary> (OT-<n>)
- **Done:** one line per piece of work: what, and where it is (branch, PR, commit)
- **Decisions:** in full: what, who decided, why, and the option turned down
- **Learned:** pointers only (KI-007, PHYSICS-AND-COLLISIONS §3), never the content again
- **Open:** only what BACKLOG and PROJECT-KNOWLEDGE §2 do not already hold
```

- **Size:** about 20 lines. Decisions get the room; the other sections stay short.
- **Retention:** the live file holds the newest 5 entries. When it holds more, move the oldest word for word to `sessions/archive/YYYY-MM.md` (newest first; a new month file opens with a `Historical` metadata block; relative links gain one `../`).

## 6. Feature pages mirror the source tree

A page about one project's feature lives at `docs/<Project>/<relative path of the feature's source folder>/`. Pages that span projects stay in the `docs/` root.

| Feature source | Page |
|----------------|------|
| `CoolEngine/Services/Renderers/` | [docs/CoolEngine/Services/Renderers/RENDERING.md](CoolEngine/Services/Renderers/RENDERING.md) |
| `CoolEngine/Services/Loaders/` | [docs/CoolEngine/Services/Loaders/ASSET-PIPELINE.md](CoolEngine/Services/Loaders/ASSET-PIPELINE.md) |
| `CoolEngine/PhysicEngine/` | [docs/CoolEngine/PhysicEngine/PHYSICS-AND-COLLISIONS.md](CoolEngine/PhysicEngine/PHYSICS-AND-COLLISIONS.md) |
| `OldTanks.UI/` | [docs/OldTanks.UI/UI-CONTROLS.md](OldTanks.UI/UI-CONTROLS.md) |
| `OldTanks.UI.SourceGenerators/` | [docs/OldTanks.UI.SourceGenerators/BINDING-GENERATOR.md](OldTanks.UI.SourceGenerators/BINDING-GENERATOR.md) |
| `OldTanks/` (the game application) | [docs/OldTanks/FEATURES-OVERVIEW.md](OldTanks/FEATURES-OVERVIEW.md) |

## 7. Known-issue entries

- **Numbering:** `KI-nnn`, assigned once, never renumbered or reused. Code comments and pages cite the number, so a published heading is never reworded.
- **Shape:** `Status`, `Scope`, `Root cause`, `Current state`, `Remaining check`, `Evidence`.
- **Status words:** `Open`, `Open by design` (with the reason), `Partially fixed`, `Closed` (with how). The index row repeats the status; update both together.
- **Closing:** a closed entry keeps its heading as a one-line stub with the fix's branch or PR, so its anchor keeps resolving.

## 8. Update triggers

| Event | Update |
|-------|--------|
| Project, package or reference added, removed or renamed | `ARCHITECTURE.md` §1, the project's `AGENTS.md`, the root folder map |
| New thread, lock or global singleton | `ARCHITECTURE.md` §2-§4 |
| Asset folder, file-name or cache-key rule changed | `ASSET-PIPELINE.md` and, for a new shader, the shader table in `RENDERING.md` |
| Key binding or HUD/ImGui control added or changed | [FEATURES-OVERVIEW](OldTanks/FEATURES-OVERVIEW.md) §3-§5 and the root README's controls table |
| Bug understood but not fixed | `KNOWN-ISSUES.md` new `KI-nnn` |
| Bug fixed | the `KI-nnn` stub, `BACKLOG.md` if listed, `PROJECT-KNOWLEDGE.md` §2 if the state changed |
| New term in code or docs | `knowledge/GLOSSARY.md` |
| Any session with substantial work | `SESSION-LOG.md` entry (always), then the retention move (§5) |

## 9. Checks

`tests/OldTanks.Docs.Tests` runs these over every Markdown file in the checkout (build output, `.git` and nested checkouts such as Claude Code's worktrees aside) with every `dotnet test OldTanks.sln`; adapted from ArbBot's documentation checks ([BUILD §2](BUILD.md#2-build)).

| Check | Fails on |
|-------|----------|
| Links | A relative link or image that doesn't resolve to a file or folder with the exact case |
| Anchors | A `#fragment` into a Markdown page that matches none of its headings (GitHub's slug: lower case, punctuation dropped, each space a `-`, repeats numbered `-1`, `-2`) or `<a id>` anchors |
| Metadata | A `docs/` page other than a `README.md` without the §1 block: an H1, a `Status` from the vocabulary, a bare-date `Last updated`, a `Verified against` in its three parts |
| Orphans | A `docs/` page no other page links to (the session archive aside) |
| Adapters | An `AGENTS.md` without a `CLAUDE.md` beside it holding only `@AGENTS.md` (§4) |
| Guide map | An `AGENTS.md` the root guide does not link |
| Target framework | A project guide whose `Target framework` row differs from its `.csproj` |
| Mirroring | A page under `docs/<Project>/` whose folder does not exist in the source tree (§6) |
| KI register | Entries not numbered `KI-001` upwards without gaps; an index row whose anchor, title or status differs from its entry; a status off the §7 vocabulary |
| Session log | More than 5 entries in the live log (§5) |
| Line numbers | A source line reference outside code fences (§3): a source file name followed by a colon and a number, or a GitHub `#L` line anchor |

Not checked, so still a reviewer's job: external URLs, whether a claim is true, whether a named symbol still exists, and whether `Last updated` moved when it should have.
