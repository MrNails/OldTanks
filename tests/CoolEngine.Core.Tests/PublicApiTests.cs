using System.Reflection;
using System.Runtime.CompilerServices;
using CoolEngine.Core.Timing;

namespace CoolEngine.Core.Tests;

[TestFixture]
public sealed class PublicApiTests
{
    private static readonly Assembly s_assembly = typeof(FixedTickClock).Assembly;

    [Test]
    public void ExportedTypes_AreExactlyThePlannedOnes()
    {
        var expected = new[]
        {
            "CoolEngine.Core.Timing.FixedTickClock",
            "CoolEngine.Core.Timing.TickAdvance",
            "CoolEngine.Core.Timing.ITimestampSource",
            "CoolEngine.Core.Timing.StopwatchTimestampSource",
            "CoolEngine.Core.Timing.ITickWaiter",
            "CoolEngine.Core.Timing.SleepTickWaiter",
            "CoolEngine.Core.Timing.HighResolutionTickWaiter",
            "CoolEngine.Core.Channels.CommandQueue`1",
            "CoolEngine.Core.Channels.CommandBatch`1",
            "CoolEngine.Core.Channels.SnapshotBuffer`1",
            "CoolEngine.Core.Hosting.ITickHandler",
            "CoolEngine.Core.Hosting.FixedTickRunner",
            "CoolEngine.Core.Hosting.FixedTickThread",
            "CoolEngine.Core.Hosting.TickFaultEventArgs",
            "CoolEngine.Core.Hosting.TicksDroppedEventArgs",
        };

        var exported = s_assembly.GetExportedTypes().Select(t => t.FullName);

        Assert.That(exported, Is.EquivalentTo(expected));
    }

    [Test]
    public void Assembly_ReferencesOnlyTheRuntime()
    {
        var names = s_assembly.GetReferencedAssemblies().Select(a => a.Name ?? string.Empty).ToArray();
        string[] forbidden = ["CoolEngine", "Common", "OldTanks", "Serilog", "OpenTK"];

        var notRuntime = names
            .Where(n => !n.StartsWith("System", StringComparison.Ordinal) && n is not ("netstandard" or "Microsoft.Win32.Primitives"))
            .ToArray();
        var forbiddenFound = names.Where(n => forbidden.Any(f => n.StartsWith(f, StringComparison.Ordinal))).ToArray();

        Assert.Multiple(() =>
        {
            Assert.That(notRuntime, Is.Empty, "references outside the runtime");
            Assert.That(forbiddenFound, Is.Empty, "references to the solution's projects or packages");
        });
    }

    [Test]
    public void Types_DeclareNoStaticFields()
    {
        const BindingFlags flags = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;

        var staticFields = s_assembly.GetTypes()
            .Where(t => !IsCompilerGenerated(t))
            .SelectMany(t => t.GetFields(flags).Where(f => !f.IsLiteral).Select(f => $"{t.FullName}.{f.Name}"))
            .ToArray();

        Assert.That(staticFields, Is.Empty);
    }

    private static bool IsCompilerGenerated(Type type)
    {
        for (var current = type; current is not null; current = current.DeclaringType)
        {
            if (current.IsDefined(typeof(CompilerGeneratedAttribute), false))
                return true;
        }

        return false;
    }
}
