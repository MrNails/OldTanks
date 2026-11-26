using OpenTK.Graphics.OpenGL4;

namespace CoolEngine.GraphicalEngine.Core;

/// <summary>
/// An offscreen framebuffer with an RGBA8 color renderbuffer and a 32-bit float depth renderbuffer
/// (<c>DEPTH_COMPONENT32F</c>): the 3D scene is drawn into it, then its color is copied to the window
/// with <see cref="BlitColorToWindow"/>.
/// </summary>
/// <remarks>
/// GL thread only: its methods make GL calls (OpenGL 4.5 direct state access, plus glBindFramebuffer and glViewport in
/// <see cref="Bind"/> and <see cref="BlitColorToWindow"/>) and must run on the thread that owns the context. The owner keeps the target at the size of the window's viewport (see <see cref="Resize"/>), because
/// <see cref="BlitColorToWindow"/> copies it 1:1 to the window's lower-left corner and leaves the viewport at the
/// target's size. There is no finalizer: a finalizer has no GL context (KI-017), so a target that is never disposed
/// keeps its GL objects until the context is destroyed.
/// </remarks>
public sealed class RenderTarget : IDisposable
{
    private readonly int m_framebuffer;
    private readonly int m_colorRenderbuffer;
    private readonly int m_depthRenderbuffer;

    private bool m_disposed;

    /// <summary>
    /// Creates the framebuffer and both renderbuffers at the given size. GL thread only.
    /// </summary>
    /// <param name="width">Width in pixels; at least 1.</param>
    /// <param name="height">Height in pixels; at least 1.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="width"/> or <paramref name="height"/> is less
    /// than 1; no GL object is created.</exception>
    /// <exception cref="InvalidOperationException">The framebuffer is incomplete; the message names the status, and
    /// the framebuffer and renderbuffers are deleted before the throw.</exception>
    public RenderTarget(int width, int height)
    {
        ValidateSize(width, height);

        GL.CreateFramebuffers(1, out m_framebuffer);
        GL.CreateRenderbuffers(1, out m_colorRenderbuffer);
        GL.CreateRenderbuffers(1, out m_depthRenderbuffer);

        AllocateStorage(width, height);

        GL.NamedFramebufferRenderbuffer(m_framebuffer, FramebufferAttachment.ColorAttachment0,
            RenderbufferTarget.Renderbuffer, m_colorRenderbuffer);
        GL.NamedFramebufferRenderbuffer(m_framebuffer, FramebufferAttachment.DepthAttachment,
            RenderbufferTarget.Renderbuffer, m_depthRenderbuffer);

        var status = GL.CheckNamedFramebufferStatus(m_framebuffer, FramebufferTarget.Framebuffer);

        if (status != FramebufferStatus.FramebufferComplete)
        {
            DeleteGLObjects();

            throw new InvalidOperationException($"The render target's framebuffer is incomplete: {status}.");
        }

        Width = width;
        Height = height;
    }

    /// <summary>Width of both renderbuffers in pixels.</summary>
    public int Width { get; private set; }

    /// <summary>Height of both renderbuffers in pixels.</summary>
    public int Height { get; private set; }

    /// <summary>
    /// Reallocates both renderbuffers at the new size; their contents become undefined. Does nothing when the size
    /// is unchanged. GL thread only.
    /// </summary>
    /// <param name="width">New width in pixels; at least 1.</param>
    /// <param name="height">New height in pixels; at least 1.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="width"/> or <paramref name="height"/> is less
    /// than 1; the target keeps its size.</exception>
    /// <exception cref="ObjectDisposedException">The target is disposed.</exception>
    public void Resize(int width, int height)
    {
        ObjectDisposedException.ThrowIf(m_disposed, this);
        ValidateSize(width, height);

        if (width == Width && height == Height)
            return;

        AllocateStorage(width, height);

        Width = width;
        Height = height;
    }

    /// <summary>
    /// Binds the target as the draw and the read framebuffer and sets the viewport to its size. GL thread only;
    /// runs every frame.
    /// </summary>
    /// <exception cref="ObjectDisposedException">The target is disposed.</exception>
    public void Bind()
    {
        ObjectDisposedException.ThrowIf(m_disposed, this);

        GL.BindFramebuffer(FramebufferTarget.Framebuffer, m_framebuffer);
        GL.Viewport(0, 0, Width, Height);
    }

    /// <summary>
    /// Copies the color buffer 1:1 (nearest) to the window's framebuffer (0), then binds framebuffer 0 as the draw
    /// and the read framebuffer and sets the viewport to the target's size, which is the window's viewport while the
    /// owner keeps the target at that size. GL thread only; runs every frame.
    /// </summary>
    /// <remarks>
    /// The blit is subject to the scissor test, so it must be off when this is called.
    /// </remarks>
    /// <exception cref="ObjectDisposedException">The target is disposed.</exception>
    public void BlitColorToWindow()
    {
        ObjectDisposedException.ThrowIf(m_disposed, this);

        GL.BlitNamedFramebuffer(m_framebuffer, 0,
            0, 0, Width, Height,
            0, 0, Width, Height,
            ClearBufferMask.ColorBufferBit, BlitFramebufferFilter.Nearest);

        GL.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
        GL.Viewport(0, 0, Width, Height);
    }

    /// <summary>
    /// Deletes the framebuffer and both renderbuffers; later calls do nothing. Must be called on the GL thread while
    /// the context exists.
    /// </summary>
    public void Dispose()
    {
        if (m_disposed)
            return;

        DeleteGLObjects();
        m_disposed = true;
    }

    private void AllocateStorage(int width, int height)
    {
        GL.NamedRenderbufferStorage(m_colorRenderbuffer, RenderbufferStorage.Rgba8, width, height);
        GL.NamedRenderbufferStorage(m_depthRenderbuffer, RenderbufferStorage.DepthComponent32f, width, height);
    }

    private void DeleteGLObjects()
    {
        GL.DeleteFramebuffer(m_framebuffer);
        GL.DeleteRenderbuffer(m_colorRenderbuffer);
        GL.DeleteRenderbuffer(m_depthRenderbuffer);
    }

    private static void ValidateSize(int width, int height)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(width, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(height, 1);
    }
}
