using OldTanks.UI.Controls;
using OldTanks.UI.ImGuiControls;

namespace OldTanks.UI.Tests.Services;

[TestFixture]
public sealed class ControlHandlerTests
{
    private static string UniqueName() => $"{TestContext.CurrentContext.Test.Name}-{Guid.NewGuid():N}";

    [Test]
    public void RegisterControl_DuplicateName_Throws()
    {
        var name = UniqueName();
        _ = new ImGuiButton(name);

        var exception = Assert.Throws<InvalidOperationException>(() => _ = new ImGuiButton(name));

        Assert.That(exception!.Message, Does.Contain(name));
    }

    [Test]
    public void RegisterControl_DuplicateNameOfHiddenControl_Throws()
    {
        var name = UniqueName();
        var first = new ImGuiButton(name);
        first.IsVisible = false;

        Assert.Throws<InvalidOperationException>(() => _ = new ImGuiButton(name));
    }

    [Test]
    public void RegisterControl_DuplicateNameAcrossControlKinds_Throws()
    {
        var name = UniqueName();
        _ = new TextBlock(name);

        Assert.Throws<InvalidOperationException>(() => _ = new ImGuiButton(name));
    }

    [Test]
    public void RegisterControl_UniqueNames_DoesNotThrow()
    {
        var name = UniqueName();
        _ = new ImGuiButton(name + "-a");

        Assert.DoesNotThrow(() => _ = new ImGuiButton(name + "-b"));
    }

    [Test]
    public void RegisterControl_NamesDifferingOnlyInCase_DoesNotThrow()
    {
        var guid = Guid.NewGuid().ToString("N");
        _ = new ImGuiButton("Case-" + guid);

        Assert.DoesNotThrow(() => _ = new ImGuiButton("case-" + guid));
    }
}
