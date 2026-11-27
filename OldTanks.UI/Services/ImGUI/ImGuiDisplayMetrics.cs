using CoolEngine.Mathematics;
using Vector2i = OpenTK.Mathematics.Vector2i;

namespace OldTanks.UI.Services.ImGUI;

/// <summary>
/// What ImGui needs to know about the window: its display in client units (the unit of the mouse position), the
/// framebuffer scale, and the framebuffer height for the scissor box. A <c>default</c> instance is not valid: build
/// one with <see cref="TryCreate"/>.
/// </summary>
public readonly record struct ImGuiDisplayMetrics
{
    private ImGuiDisplayMetrics(System.Numerics.Vector2 displaySize, System.Numerics.Vector2 framebufferScale,
        int framebufferHeight)
    {
        DisplaySize = displaySize;
        FramebufferScale = framebufferScale;
        FramebufferHeight = framebufferHeight;
    }

    /// <summary>The client size: ImGui's <c>DisplaySize</c>.</summary>
    public System.Numerics.Vector2 DisplaySize { get; }

    /// <summary>The framebuffer size divided by the client size: ImGui's <c>DisplayFramebufferScale</c>.</summary>
    public System.Numerics.Vector2 FramebufferScale { get; }

    /// <summary>The framebuffer height in pixels.</summary>
    public int FramebufferHeight { get; }

    /// <summary>
    /// Creates the metrics. Returns <see langword="false"/>, with <paramref name="metrics"/> = <c>default</c>, when any
    /// component of either size is below 1 (a minimised window).
    /// </summary>
    public static bool TryCreate(Vector2i clientSize, Vector2i framebufferSize, out ImGuiDisplayMetrics metrics)
    {
        if (clientSize.X < 1 || clientSize.Y < 1 || framebufferSize.X < 1 || framebufferSize.Y < 1)
        {
            metrics = default;
            return false;
        }

        metrics = new ImGuiDisplayMetrics(
            new System.Numerics.Vector2(clientSize.X, clientSize.Y),
            new System.Numerics.Vector2(framebufferSize.X / (float)clientSize.X, framebufferSize.Y / (float)clientSize.Y),
            framebufferSize.Y);
        return true;
    }

    /// <summary>The orthographic projection of the display, origin at the top left.</summary>
    public Matrix4 CreateProjection()
    {
        return Matrix4.CreateOrthographicOffCenter(0f, DisplaySize.X, DisplaySize.Y, 0f, -1f, 1f);
    }

    /// <summary>
    /// The scissor box (lower-left origin) of a clip rectangle (x1, y1, x2, y2) that is already in framebuffer pixels
    /// (after <c>ImDrawData.ScaleClipRects</c>).
    /// </summary>
    public (int X, int Y, int Width, int Height) GetScissorBox(System.Numerics.Vector4 clipRect)
    {
        return ((int)clipRect.X, FramebufferHeight - (int)clipRect.W, (int)(clipRect.Z - clipRect.X),
            (int)(clipRect.W - clipRect.Y));
    }
}
