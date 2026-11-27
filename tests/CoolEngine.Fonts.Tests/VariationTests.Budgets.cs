using CoolEngine.Fonts.Cff;
using CoolEngine.Fonts.Hinting.TrueType;
using CoolEngine.Fonts.Sfnt;
using CoolEngine.Fonts.Tests.Reference;
using CoolEngine.Fonts.Variations;

namespace CoolEngine.Fonts.Tests;

/// <summary>The variation work budgets (Denys's "Glyph and size budgets"): the measurement behind them, and each biting.</summary>
public sealed partial class VariationTests
{
    /// <summary>
    /// The measurement behind <see cref="GlyfGlyphLoader.VariationWorkBudget"/>, <see cref="VariationInstance.SetUpWorkBudget"/>
    /// and the shared hinting and CFF budgets: every variable face of <c>C:\Windows\Fonts</c> and of the test fonts, at every
    /// named instance and each axis's extremes, every glyph at 16 px, unhinted and hinted (v40). Prints the maxima. Also checks
    /// that every font file of <c>C:\Windows\Fonts</c> that loaded before variations still loads.
    /// </summary>
    [Test]
    [Explicit("A measurement over the whole Windows font folder (minutes).")]
    public void Measure_VariationWork_EveryVariableFace()
    {
        var files = Directory.EnumerateFiles(TestFonts.WindowsFonts)
            .Where(path => Path.GetExtension(path).ToLowerInvariant() is ".ttf" or ".otf" or ".ttc")
            .Concat(TestFonts.AllFiles()).Distinct(StringComparer.OrdinalIgnoreCase).Order().ToList();
        var failures = new List<string>();
        var variable = new List<string>();
        (long Work, string Where) unhinted = (0, ""), hinted = (0, ""), cff = (0, ""), setUp = (0, ""), size = (0, "");

        foreach (var path in files)
        {
            FontFile file;
            try
            {
                file = FontLoader.Load(path);
            }
            catch (Exception exception) when (exception is InvalidDataException or NotSupportedException)
            {
                failures.Add($"{Path.GetFileName(path)}: {exception.GetType().Name}: {exception.Message}");
                continue;
            }

            foreach (var face in file.Faces.Where(face => face.IsVariable))
            {
                variable.Add($"{Path.GetFileName(path)} #{face.FaceIndex} ({string.Join(", ", face.Axes.Select(axis => axis.Tag))}; " +
                              $"{face.NamedInstances.Count} instances; {face.OutlineFormat})");
                foreach (var setting in MeasureSettings(face))
                {
                    var where = $"{Path.GetFileName(path)} #{face.FaceIndex} {setting.Label}";
                    var work = new WorkCounter(long.MaxValue, "measuring");
                    var instance = VariationInstance.Create(face, face.Data.Variations!, Coordinates(face, setting.Values));
                    _ = face.Data.Variations!.Mvar?.LineDeltas(instance.Normalized, work);
                    Max(ref setUp, work.Work, where);

                    var scale = FontScale.FromPixelSize(16, face.UnitsPerEm);
                    if (face.OutlineFormat == OutlineFormat.TrueType)
                    {
                        var loader = new GlyfGlyphLoader(face.Data, scale, null, instance, long.MaxValue);
                        var hinter = new TrueTypeHinter(face.Data, scale, TrueTypeInterpreterVersion.V40, long.MaxValue, long.MaxValue, instance);
                        Max(ref size, hinter.SizeWork, where);
                        var hintedLoader = new GlyfGlyphLoader(face.Data, scale, hinter, instance);
                        for (var glyph = 0; glyph < face.GlyphCount; glyph++)
                        {
                            loader.Load(glyph);
                            Max(ref unhinted, loader.VariationWork, $"{where} glyph {glyph}");
                            try
                            {
                                hintedLoader.Load(glyph);
                                Max(ref hinted, hinter.GlyphWork, $"{where} glyph {glyph}");
                            }
                            catch (InvalidDataException)
                            {
                            }
                        }
                    }
                    else if (face.OutlineFormat == OutlineFormat.Cff2)
                    {
                        var interpreter = new Type2Interpreter(face.Data.Cff!, new CffOutlineBuilder(), instance.Normalized)
                        {
                            WorkBudget = long.MaxValue,
                        };
                        for (var glyph = 0; glyph < face.GlyphCount; glyph++)
                        {
                            interpreter.Run(glyph);
                            Max(ref cff, interpreter.Work, $"{where} glyph {glyph}");
                        }
                    }
                }
            }
        }

        TestContext.Out.WriteLine($"{files.Count} files; {failures.Count} fail to load:");
        failures.ForEach(failure => TestContext.Out.WriteLine("  " + failure));
        TestContext.Out.WriteLine($"{variable.Count} variable faces:");
        variable.ForEach(face => TestContext.Out.WriteLine("  " + face));
        TestContext.Out.WriteLine($"unhinted variation work max {unhinted.Work} ({unhinted.Where})");
        TestContext.Out.WriteLine($"hinted (v40) glyph work max {hinted.Work} ({hinted.Where})");
        TestContext.Out.WriteLine($"size set-up (v40, with cvar) max {size.Work} ({size.Where})");
        TestContext.Out.WriteLine($"CFF2 glyph work max {cff.Work} ({cff.Where})");
        TestContext.Out.WriteLine($"coordinate set-up (MVAR) max {setUp.Work} ({setUp.Where})");
        Assert.Pass();

        static void Max(ref (long Work, string Where) max, long work, string where)
        {
            if (work > max.Work)
                max = (work, where);
        }
    }

    /// <summary>
    /// A sweep beyond the test faces: every variable face of <c>C:\Windows\Fonts</c> (Cascadia Code and Mono, Segoe UI
    /// Variable and Sitka Italic among them), every glyph at 16 and 32 px, at every named instance and each axis's extremes,
    /// unhinted and hinted in v35 and v40, against FreeType. Prints the differences.
    /// </summary>
    [Test]
    [Explicit("A sweep over the Windows variable fonts (minutes).")]
    public void Compare_EveryWindowsVariableFace_EveryGlyph()
    {
        var reports = new List<string>();
        long compared = 0, failedBoth = 0;
        var files = Directory.EnumerateFiles(TestFonts.WindowsFonts)
            .Where(path => Path.GetExtension(path).ToLowerInvariant() is ".ttf" or ".otf" or ".ttc").Order();
        foreach (var path in files)
        {
            FontFile file;
            try
            {
                file = FontLoader.Load(path);
            }
            catch (Exception exception) when (exception is InvalidDataException or NotSupportedException)
            {
                continue;
            }

            foreach (var face in file.Faces.Where(face => face.IsVariable))
            {
                var data = FontCache.Bytes(path);
                var glyphs = Enumerable.Range(0, face.GlyphCount).ToArray();
                var modes = face.OutlineFormat == OutlineFormat.TrueType ? new[] { Mode.Unhinted, Mode.V35, Mode.V40 } : [Mode.Unhinted];
                Parallel.ForEach(MeasureSettings(face).SelectMany(setting => modes.Select(mode => (setting, mode))), item =>
                {
                    var result = Compare(face, data, face.FaceIndex, item.setting.Values, glyphs, item.mode);
                    lock (reports)
                    {
                        compared += result.Compared;
                        failedBoth += result.BothFailed;
                        if (result.Differences > 0 || result.LineMetricsDifferences.Count > 0)
                            reports.Add($"{Path.GetFileName(path)} {item.setting.Label} {item.mode}: {result} {string.Join("; ", result.LineMetricsDifferences)}");
                    }
                });
            }
        }

        TestContext.Out.WriteLine($"{compared} glyph loads compared, {failedBoth} fail in both, {reports.Count} settings differ.");
        reports.ForEach(report => TestContext.Out.WriteLine(report));
        Assert.That(reports, Is.Empty);
    }

    /// <summary>Every named instance and each axis's extremes.</summary>
    private static IEnumerable<Setting> MeasureSettings(FontFace face)
    {
        var defaults = face.Axes.Select(axis => axis.Default).ToArray();
        for (var i = 0; i < face.NamedInstances.Count; i++)
            yield return new Setting($"instance {i}", face.NamedInstances[i].Coordinates.ToArray());

        for (var a = 0; a < face.Axes.Count; a++)
        {
            foreach (var value in new[] { face.Axes[a].Minimum, face.Axes[a].Maximum })
            {
                var values = (float[])defaults.Clone();
                values[a] = value;
                yield return new Setting($"{face.Axes[a].Tag} {value}", values);
            }
        }
    }
}
