using OldTanks.UI.Services.ImGUI;
using Vector2 = System.Numerics.Vector2;
using Vector2i = OpenTK.Mathematics.Vector2i;
using Vector4 = System.Numerics.Vector4;

namespace OldTanks.UI.Tests.Services.ImGUI;

/// <summary>
/// <see cref="ImGuiDisplayMetrics"/>: ImGui's display in client units, its scissor box in framebuffer pixels. Sizes
/// are written client / framebuffer.
/// </summary>
[TestFixture]
public sealed class ImGuiDisplayMetricsTests
{
    private static ImGuiDisplayMetrics Create(Vector2i client, Vector2i framebuffer)
    {
        Assert.That(ImGuiDisplayMetrics.TryCreate(client, framebuffer, out var metrics), Is.True);
        return metrics;
    }

    [Test]
    public void TryCreate_EqualClientAndFramebuffer_DisplaySizeIsClientAndScaleIsOne()
    {
        var created = ImGuiDisplayMetrics.TryCreate(new Vector2i(800, 600), new Vector2i(800, 600), out var metrics);

        Assert.That(created, Is.True);
        Assert.That(metrics.DisplaySize, Is.EqualTo(new Vector2(800, 600)));
        Assert.That(metrics.FramebufferScale, Is.EqualTo(new Vector2(1, 1)));
        Assert.That(metrics.FramebufferHeight, Is.EqualTo(600));
    }

    [Test]
    public void TryCreate_FramebufferTwiceClient_ScaleIsTwo()
    {
        var created = ImGuiDisplayMetrics.TryCreate(new Vector2i(800, 600), new Vector2i(1600, 1200), out var metrics);

        Assert.That(created, Is.True);
        Assert.That(metrics.DisplaySize, Is.EqualTo(new Vector2(800, 600)));
        Assert.That(metrics.FramebufferScale, Is.EqualTo(new Vector2(2, 2)));
        Assert.That(metrics.FramebufferHeight, Is.EqualTo(1200));
    }

    [Test]
    public void TryCreate_MinimisedWindow_ReturnsFalse()
    {
        var created = ImGuiDisplayMetrics.TryCreate(new Vector2i(0, 0), new Vector2i(0, 0), out var metrics);

        Assert.That(created, Is.False);
        Assert.That(metrics, Is.EqualTo(default(ImGuiDisplayMetrics)));
    }

    [Test]
    public void CreateProjection_Display800x600_MapsCornersAndCentreToNdc()
    {
        var projection = Create(new Vector2i(800, 600), new Vector2i(800, 600)).CreateProjection();

        var topLeft = projection * new Vector4(0, 0, 0, 1);
        var bottomRight = projection * new Vector4(800, 600, 0, 1);
        var centre = projection * new Vector4(400, 300, 0, 1);

        Assert.That(topLeft.X, Is.EqualTo(-1f).Within(1e-6f));
        Assert.That(topLeft.Y, Is.EqualTo(1f).Within(1e-6f));
        Assert.That(bottomRight.X, Is.EqualTo(1f).Within(1e-6f));
        Assert.That(bottomRight.Y, Is.EqualTo(-1f).Within(1e-6f));
        Assert.That(centre.X, Is.EqualTo(0f).Within(1e-6f));
        Assert.That(centre.Y, Is.EqualTo(0f).Within(1e-6f));
    }

    [Test]
    public void GetScissorBox_ClipRect_FlipsToLowerLeftOriginOfFramebuffer()
    {
        var metrics = Create(new Vector2i(800, 600), new Vector2i(800, 600));

        var box = metrics.GetScissorBox(new Vector4(10, 20, 110, 70));

        Assert.That(box, Is.EqualTo((10, 530, 100, 50)));
    }

    [Test]
    public void GetScissorBox_FramebufferTwiceClient_UsesFramebufferHeight()
    {
        var metrics = Create(new Vector2i(800, 600), new Vector2i(1600, 1200));

        var box = metrics.GetScissorBox(new Vector4(20, 40, 220, 140));

        Assert.That(box, Is.EqualTo((20, 1060, 200, 100)));
    }
}
