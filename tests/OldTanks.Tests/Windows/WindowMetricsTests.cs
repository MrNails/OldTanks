using CoolEngine.Mathematics;
using OldTanks.Windows;
using Vector2i = OpenTK.Mathematics.Vector2i;
using Vector4 = System.Numerics.Vector4;

namespace OldTanks.Tests.Windows;

/// <summary>
/// <see cref="WindowMetrics"/>: GL drawing in framebuffer pixels, screen-text layout in client units. Sizes in the
/// test names and comments are written client / framebuffer.
/// </summary>
[TestFixture]
public sealed class WindowMetricsTests
{
    private static WindowMetrics Create(Vector2i client, Vector2i framebuffer)
    {
        Assert.That(WindowMetrics.TryCreate(client, framebuffer, out var metrics), Is.True);
        return metrics;
    }

    [Test]
    public void TryCreate_ClientAndFramebufferPositive_ReturnsTrueWithBothSizes()
    {
        var created = WindowMetrics.TryCreate(new Vector2i(800, 600), new Vector2i(800, 600), out var metrics);

        Assert.Multiple(() =>
        {
            Assert.That(created, Is.True);
            Assert.That(metrics.ClientSize, Is.EqualTo(new Vector2i(800, 600)));
            Assert.That(metrics.FramebufferSize, Is.EqualTo(new Vector2i(800, 600)));
        });
    }

    [Test]
    public void TryCreate_MinimisedWindow_ReturnsFalse()
    {
        var created = WindowMetrics.TryCreate(new Vector2i(0, 0), new Vector2i(0, 0), out var metrics);

        Assert.Multiple(() =>
        {
            Assert.That(created, Is.False);
            Assert.That(metrics, Is.EqualTo(default(WindowMetrics)));
        });
    }

    [Test]
    public void TryCreate_EmptyClientOnly_ReturnsFalse()
    {
        var created = WindowMetrics.TryCreate(new Vector2i(0, 0), new Vector2i(800, 600), out var metrics);

        Assert.Multiple(() =>
        {
            Assert.That(created, Is.False);
            Assert.That(metrics, Is.EqualTo(default(WindowMetrics)));
        });
    }

    [Test]
    public void TryCreate_ZeroHeightClientArea_ReturnsFalse()
    {
        var created = WindowMetrics.TryCreate(new Vector2i(800, 0), new Vector2i(800, 0), out _);

        Assert.That(created, Is.False);
    }

    [Test]
    public void TryCreate_EmptyFramebufferOnly_ReturnsFalse()
    {
        var created = WindowMetrics.TryCreate(new Vector2i(800, 600), new Vector2i(0, 0), out _);

        Assert.That(created, Is.False);
    }

    [Test]
    public void PerspectiveAspect_Framebuffer800x600_IsWidthOverHeight()
    {
        var metrics = Create(new Vector2i(800, 600), new Vector2i(800, 600));

        Assert.That(metrics.PerspectiveAspect, Is.EqualTo((float)800 / 600));
    }

    [Test]
    public void PerspectiveAspect_FramebufferLargerThanClient_UsesFramebuffer()
    {
        var metrics = Create(new Vector2i(800, 600), new Vector2i(1600, 900));

        Assert.That(metrics.PerspectiveAspect, Is.EqualTo((float)1600 / 900));
    }

    [Test]
    public void PerspectiveAspect_PortraitFramebuffer_IsClampedToOne()
    {
        var metrics = Create(new Vector2i(600, 800), new Vector2i(600, 800));

        Assert.That(metrics.PerspectiveAspect, Is.EqualTo(1f));
    }

    [Test]
    public void CreatePerspective_Fov45Far2000_EqualsLibraryFactoryWithFramebufferAspect()
    {
        var metrics = Create(new Vector2i(800, 600), new Vector2i(800, 600));

        var actual = metrics.CreatePerspective(45f, 2000f);

        var expected = Matrix4.CreatePerspectiveFieldOfView(MathHelper.DegreesToRadians(45f), (float)800 / 600, 0.1f,
            2000f);
        Assert.That(actual == expected, Is.True);
    }

    [Test]
    public void CreateScreenProjection_HudBaseline15PxBelowTop_MapsToClientRow15()
    {
        var metrics = Create(new Vector2i(800, 600), new Vector2i(800, 600));

        var ndc = metrics.CreateScreenProjection() * new Vector4(0, 600 - 15, 0, 1);

        Assert.Multiple(() =>
        {
            Assert.That(ndc.X, Is.EqualTo(-1f).Within(1e-6f));
            Assert.That(ndc.Y, Is.EqualTo(0.95f).Within(1e-6f));
            Assert.That((1 - ndc.Y) / 2 * 600, Is.EqualTo(15f).Within(1e-3f));
        });
    }

    [Test]
    public void CreateScreenProjection_FramebufferLargerThanClient_SpansClientUnits()
    {
        var metrics = Create(new Vector2i(800, 600), new Vector2i(1600, 1200));
        var projection = metrics.CreateScreenProjection();

        var lowerLeft = projection * new Vector4(0, 0, 0, 1);
        var upperRight = projection * new Vector4(800, 600, 0, 1);

        Assert.Multiple(() =>
        {
            Assert.That(lowerLeft.X, Is.EqualTo(-1f).Within(1e-6f));
            Assert.That(lowerLeft.Y, Is.EqualTo(-1f).Within(1e-6f));
            Assert.That(upperRight.X, Is.EqualTo(1f).Within(1e-6f));
            Assert.That(upperRight.Y, Is.EqualTo(1f).Within(1e-6f));
        });
    }
}
