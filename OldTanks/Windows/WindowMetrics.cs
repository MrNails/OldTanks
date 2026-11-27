using CoolEngine.Mathematics;
using Vector2i = OpenTK.Mathematics.Vector2i;

namespace OldTanks.Windows;

/// <summary>
/// The two sizes of the game window that drawing needs: GL drawing (the viewport, the render target, the perspective
/// aspect) uses the framebuffer size in pixels, and screen-text layout (the HUD's screen projection) uses the client
/// size, the unit of the mouse position. A <c>default</c> instance is not valid: build one with
/// <see cref="TryCreate"/>.
/// </summary>
public readonly record struct WindowMetrics
{
    private WindowMetrics(Vector2i clientSize, Vector2i framebufferSize)
    {
        ClientSize = clientSize;
        FramebufferSize = framebufferSize;
    }

    /// <summary>The client area in GLFW screen coordinates, the unit of the mouse position.</summary>
    public Vector2i ClientSize { get; }

    /// <summary>The default framebuffer in pixels, the unit of the GL viewport.</summary>
    public Vector2i FramebufferSize { get; }

    /// <summary>
    /// The perspective's aspect ratio: framebuffer width over height, at least 1 (a portrait window keeps the
    /// aspect 1).
    /// </summary>
    public float PerspectiveAspect
    {
        get
        {
            var aspect = (float)FramebufferSize.X / FramebufferSize.Y;
            return aspect >= 1 ? aspect : 1;
        }
    }

    /// <summary>
    /// Creates the metrics. Returns <see langword="false"/>, with <paramref name="metrics"/> = <c>default</c>, when any
    /// component of either size is below 1 (a minimised window, or a client area dragged to zero height).
    /// </summary>
    public static bool TryCreate(Vector2i clientSize, Vector2i framebufferSize, out WindowMetrics metrics)
    {
        if (clientSize.X < 1 || clientSize.Y < 1 || framebufferSize.X < 1 || framebufferSize.Y < 1)
        {
            metrics = default;
            return false;
        }

        metrics = new WindowMetrics(clientSize, framebufferSize);
        return true;
    }

    /// <summary>The perspective projection with the framebuffer's aspect and the near plane 0.1.</summary>
    public Matrix4 CreatePerspective(float fovDegrees, float depthFar)
    {
        return Matrix4.CreatePerspectiveFieldOfView(MathHelper.DegreesToRadians(fovDegrees), PerspectiveAspect, 0.1f,
            depthFar);
    }

    /// <summary>The orthographic projection of the client area, origin at the lower left, for the HUD's text.</summary>
    public Matrix4 CreateScreenProjection()
    {
        return Matrix4.CreateOrthographicOffCenter(0, ClientSize.X, 0, ClientSize.Y, -1, 1);
    }
}
