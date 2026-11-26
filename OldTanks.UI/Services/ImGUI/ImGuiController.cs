using System.Drawing;
using System.Runtime.CompilerServices;
using CoolEngine.GraphicalEngine.Core;
using CoolEngine.GraphicalEngine.Core.Texture;
using CoolEngine.GraphicalEngine.Services;
using CoolEngine.Services.Extensions;
using ImGuiNET;
using OpenTK.Graphics.OpenGL4;
// Window boundary: OpenTK's window and input types (Vector2i, the scroll offset's Vector2).
using OpenTK.Mathematics;
using OpenTK.Windowing.Desktop;
using OpenTK.Windowing.GraphicsLibraryFramework;
using Serilog;
using Matrix4 = CoolEngine.Mathematics.Matrix4;

namespace OldTanks.UI.Services.ImGUI;

//TODO: Refactor this class to more optimized rendering (maybe) and e.t.c
//Main reason: struggling with Image rendering because it is render only one texture for every time
/// <summary>
/// A modified version of Veldrid.ImGui's ImGuiRenderer.
/// Manages input for ImGui and handles rendering ImGui's DrawLists with Veldrid.
/// </summary>
public class ImGuiController : IDisposable
{
    private const string VertexSource = @"#version 460 core
uniform mat4 projection_matrix;
layout(location = 0) in vec2 in_position;
layout(location = 1) in vec2 in_texCoord;
layout(location = 2) in vec4 in_color;
out vec4 color;
out vec2 texCoord;

void main()
{
    gl_Position = projection_matrix * vec4(in_position, 0, 1);
    color = in_color;
    texCoord = in_texCoord;
}";

    private const string FragmentSource = @"#version 460 core
uniform sampler2D in_fontTexture;
in vec4 color;
in vec2 texCoord;
out vec4 outputColor;
void main()
{
    outputColor = color * texture(in_fontTexture, texCoord);
}";

    private readonly ILogger m_logger;
    private readonly Queue<char> m_pressedChars = new Queue<char>();
    private readonly System.Numerics.Vector2 m_scaleFactor = System.Numerics.Vector2.One;
    
    private bool m_frameBegun;

    private IntPtr m_imGuiContext;

    private DrawObjectInfo m_drawObjectInfo;
    
    private int m_vertexBufferSize;
    private int m_indexBufferSize;

    private Texture? m_fontTexture;
    private Shader m_shader;

    private int m_windowWidth;
    private int m_windowHeight;
    
    private Matrix4 m_imGuiProjection;

    /// <summary>
    /// Constructs a new ImGuiController.
    /// </summary>
    public ImGuiController(int width, int height, ILogger logger)
    {
        m_logger = logger;
        WindowResized(width, height);

        m_imGuiContext = ImGui.CreateContext();
        ImGui.SetCurrentContext(m_imGuiContext);
        var io = ImGui.GetIO();
        io.Fonts.AddFontDefault();

        io.BackendFlags |= ImGuiBackendFlags.RendererHasVtxOffset;

        CreateDeviceResources();

        SetPerFrameImGuiData(1f / 60f);

        ImGui.NewFrame();
        m_frameBegun = true;
    }

    public Rectangle WindowClip { get; set; }
    
    public void WindowResized(int width, int height)
    {
        m_windowWidth = width;
        m_windowHeight = height - 39;
        
        m_imGuiProjection = LegacyProjections.CreateOrthographicOffCenter(
            0.0f,
            width,
            height - 39,
            -39f,
            -1.0f,
            1.0f);
    }

    public void CreateDeviceResources()
    {
        RecreateFontDeviceTexture();
        int vao = 0, vbo = 0, ebo = 0;
        
        m_shader = Shader.Create(VertexSource, FragmentSource, "ImGui", m_logger);
        m_shader.Use();

        m_vertexBufferSize = 10000;
        m_indexBufferSize = 2000;

        vao = GL.GenVertexArray();
        GL.BindVertexArray(vao);
        
        vbo = GL.GenBuffer();
        GL.BindBuffer(BufferTarget.ArrayBuffer, vbo);
        GL.BufferData(BufferTarget.ArrayBuffer, m_vertexBufferSize,IntPtr.Zero, BufferUsageHint.StreamDraw);
        
        GL.EnableVertexAttribArray(0);
        GL.VertexAttribPointer(0, 2, VertexAttribPointerType.Float, false, Unsafe.SizeOf<ImDrawVert>(), 0);

        GL.EnableVertexAttribArray(1);
        GL.VertexAttribPointer(1, 2, VertexAttribPointerType.Float, false, Unsafe.SizeOf<ImDrawVert>(), Unsafe.SizeOf<Vector2>());

        GL.EnableVertexAttribArray(2);
        GL.VertexAttribPointer(2, 4, VertexAttribPointerType.UnsignedByte, true, 
            Unsafe.SizeOf<ImDrawVert>(), Unsafe.SizeOf<Vector2>() * 2);

        ebo = GL.GenBuffer();
        GL.BindBuffer(BufferTarget.ElementArrayBuffer, ebo);
        GL.BufferData(BufferTarget.ElementArrayBuffer, m_indexBufferSize, IntPtr.Zero, BufferUsageHint.StaticDraw);

        m_drawObjectInfo = new DrawObjectInfo(vao, vbo, ebo);
    }

    /// <summary>
    /// Recreates the device texture used to render text.
    /// </summary>
    public void RecreateFontDeviceTexture()
    {
        var io = ImGui.GetIO();
        io.Fonts.GetTexDataAsRGBA32(out IntPtr pixels, out int width, out int height, out int bytesPerPixel);

        var pixelDto = new Texture.PixelDto(PixelInternalFormat.Rgba, PixelFormat.Rgba, PixelType.UnsignedByte);
        
        m_fontTexture = Texture.CreateTexture2D(pixels, (width, height), ref pixelDto, TextureWrapMode.ClampToEdge);
        m_fontTexture.Name = "ImGuiTexture";

        io.Fonts.SetTexID(m_fontTexture.Handle);
    }

    /// <summary>
    /// Invokes ImGui.Render and retrieves draw data.
    /// </summary>
    public void Render()
    {
        if (!m_frameBegun) return;
        
        m_frameBegun = false;
        ImGui.Render();
        RenderImDrawData(ImGui.GetDrawData());
    }

    /// <summary>
    /// Updates ImGui input and IO configuration state.
    /// </summary>
    public void Update(GameWindow wnd, float deltaSeconds)
    {
        if (m_frameBegun)
            ImGui.Render();

        SetPerFrameImGuiData(deltaSeconds);
        UpdateImGuiInput(wnd);

        m_frameBegun = true;
        ImGui.NewFrame();
    }

    public void PressChar(char keyChar) => m_pressedChars.Enqueue(keyChar);

    public void MouseScroll(in  Vector2 offset)
    {
        var io = ImGui.GetIO();

        io.AddMouseWheelEvent(offset.X, offset.Y);
    }

    private void SetupDrawSettings()
    {
        GL.Enable(EnableCap.Blend);
        GL.Enable(EnableCap.ScissorTest);
        GL.BlendEquation(BlendEquationMode.FuncAdd);
        GL.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);
        GL.Disable(EnableCap.CullFace);
        GL.Disable(EnableCap.DepthTest);
    }

    /// <summary>
    /// Sets per-frame data based on the associated window.
    /// This is called by Update(float).
    /// </summary>
    private void SetPerFrameImGuiData(float deltaSeconds)
    {
        var io = ImGui.GetIO();
        io.DisplaySize = new System.Numerics.Vector2(
            m_windowWidth / m_scaleFactor.X,
            m_windowHeight / m_scaleFactor.Y);
        io.DisplayFramebufferScale = m_scaleFactor;
        io.DeltaTime = deltaSeconds; // DeltaTime is in seconds.
    }

    private void UpdateImGuiInput(GameWindow wnd)
    {
        var io = ImGui.GetIO();

        var mouseState = wnd.MouseState;
        var keyboardState = wnd.KeyboardState;

        io.AddMouseButtonEvent(0, mouseState[MouseButton.Left]);
        io.AddMouseButtonEvent(1, mouseState[MouseButton.Right]);
        io.AddMouseButtonEvent(2, mouseState[MouseButton.Middle]);

        var screenPoint = new Vector2i((int)mouseState.X, (int)mouseState.Y);
        var point = screenPoint; //wnd.PointToClient(screenPoint);
        io.AddMousePosEvent(point.X, point.Y);

        foreach (Keys key in Enum.GetValues(typeof(Keys)))
        {
            if (key == Keys.Unknown)
                continue;

            var imGuiKey = TranslateKey(key);

            if (imGuiKey == ImGuiKey.None)
                continue;

            io.AddKeyEvent(imGuiKey, keyboardState.IsKeyDown(key));
        }

        while (m_pressedChars.Count != 0)
            io.AddInputCharacter(m_pressedChars.Dequeue());

        io.AddKeyEvent(ImGuiKey.ModCtrl, keyboardState.IsKeyDown(Keys.LeftControl) || keyboardState.IsKeyDown(Keys.RightControl));
        io.AddKeyEvent(ImGuiKey.ModAlt, keyboardState.IsKeyDown(Keys.LeftAlt) || keyboardState.IsKeyDown(Keys.RightAlt));
        io.AddKeyEvent(ImGuiKey.ModShift, keyboardState.IsKeyDown(Keys.LeftShift) || keyboardState.IsKeyDown(Keys.RightShift));
        io.AddKeyEvent(ImGuiKey.ModSuper, keyboardState.IsKeyDown(Keys.LeftSuper) || keyboardState.IsKeyDown(Keys.RightSuper));
    }

    /// <summary>
    /// Maps an OpenTK key to its Dear ImGui key; <see cref="ImGuiKey.None"/> for keys ImGui is not told about.
    /// </summary>
    private static ImGuiKey TranslateKey(Keys key)
    {
        if (key is >= Keys.A and <= Keys.Z)
            return ImGuiKey.A + (key - Keys.A);

        if (key is >= Keys.D0 and <= Keys.D9)
            return ImGuiKey._0 + (key - Keys.D0);

        if (key is >= Keys.F1 and <= Keys.F12)
            return ImGuiKey.F1 + (key - Keys.F1);

        if (key is >= Keys.KeyPad0 and <= Keys.KeyPad9)
            return ImGuiKey.Keypad0 + (key - Keys.KeyPad0);

        return key switch
        {
            Keys.Left => ImGuiKey.LeftArrow,
            Keys.Right => ImGuiKey.RightArrow,
            Keys.Up => ImGuiKey.UpArrow,
            Keys.Down => ImGuiKey.DownArrow,
            Keys.Tab => ImGuiKey.Tab,
            Keys.Enter => ImGuiKey.Enter,
            Keys.Escape => ImGuiKey.Escape,
            Keys.Space => ImGuiKey.Space,
            Keys.Backspace => ImGuiKey.Backspace,
            Keys.Insert => ImGuiKey.Insert,
            Keys.Delete => ImGuiKey.Delete,
            Keys.Home => ImGuiKey.Home,
            Keys.End => ImGuiKey.End,
            Keys.PageUp => ImGuiKey.PageUp,
            Keys.PageDown => ImGuiKey.PageDown,
            Keys.KeyPadDecimal => ImGuiKey.KeypadDecimal,
            Keys.KeyPadDivide => ImGuiKey.KeypadDivide,
            Keys.KeyPadMultiply => ImGuiKey.KeypadMultiply,
            Keys.KeyPadSubtract => ImGuiKey.KeypadSubtract,
            Keys.KeyPadAdd => ImGuiKey.KeypadAdd,
            Keys.KeyPadEnter => ImGuiKey.KeypadEnter,
            Keys.KeyPadEqual => ImGuiKey.KeypadEqual,
            Keys.Apostrophe => ImGuiKey.Apostrophe,
            Keys.Comma => ImGuiKey.Comma,
            Keys.Minus => ImGuiKey.Minus,
            Keys.Period => ImGuiKey.Period,
            Keys.Slash => ImGuiKey.Slash,
            Keys.Semicolon => ImGuiKey.Semicolon,
            Keys.Equal => ImGuiKey.Equal,
            Keys.LeftBracket => ImGuiKey.LeftBracket,
            Keys.Backslash => ImGuiKey.Backslash,
            Keys.RightBracket => ImGuiKey.RightBracket,
            Keys.GraveAccent => ImGuiKey.GraveAccent,
            Keys.LeftControl => ImGuiKey.LeftCtrl,
            Keys.RightControl => ImGuiKey.RightCtrl,
            Keys.LeftShift => ImGuiKey.LeftShift,
            Keys.RightShift => ImGuiKey.RightShift,
            Keys.LeftAlt => ImGuiKey.LeftAlt,
            Keys.RightAlt => ImGuiKey.RightAlt,
            Keys.LeftSuper => ImGuiKey.LeftSuper,
            Keys.RightSuper => ImGuiKey.RightSuper,
            _ => ImGuiKey.None
        };
    }

    private void RenderImDrawData(in ImDrawDataPtr drawData)
    {
        if (drawData.CmdListsCount == 0)
            return;

        // Setup orthographic projection matrix into our constant buffer
        var io = ImGui.GetIO();

        m_shader.Use();
        GL.UniformMatrix4(m_shader.GetUniformLocation("projection_matrix"), 1, false, ref m_imGuiProjection.Column0.X);
        
        GL.BindVertexArray(m_drawObjectInfo.VertexArrayObject);

        for (int i = 0; i < drawData.CmdListsCount; i++)
        {
            var cmd_list = drawData.CmdLists[i];

            int vertexSize = cmd_list.VtxBuffer.Size * Unsafe.SizeOf<ImDrawVert>();
            if (vertexSize > m_vertexBufferSize)
            {
                int newSize = (int)Math.Max(m_vertexBufferSize * 1.5f, vertexSize);
                GL.BindBuffer(BufferTarget.ArrayBuffer, m_drawObjectInfo.VertexBufferObject);
                GL.BufferData(BufferTarget.ArrayBuffer, newSize,IntPtr.Zero, BufferUsageHint.StreamDraw);
                m_vertexBufferSize = newSize;

                m_logger.Information("Resized dear imgui vertex buffer to new size {VertexBufferSize}", m_vertexBufferSize);
            }

            int indexSize = cmd_list.IdxBuffer.Size * sizeof(ushort);
            if (indexSize > m_indexBufferSize)
            {
                int newSize = (int)Math.Max(m_indexBufferSize * 1.5f, indexSize);
                GL.BindBuffer(BufferTarget.ElementArrayBuffer, m_drawObjectInfo.ElementsBufferObject);
                GL.BufferData(BufferTarget.ElementArrayBuffer, newSize, IntPtr.Zero, BufferUsageHint.StaticDraw);
                m_indexBufferSize = newSize;

                m_logger.Information("Resized dear imgui index buffer to new size {IndexBufferSize}", m_indexBufferSize);
            }
        }

        drawData.ScaleClipRects(io.DisplayFramebufferScale);

        var currentGlSettings = GLSettings.GetCurrentGLSettings();
        
        SetupDrawSettings();

        for (int n = 0; n < drawData.CmdListsCount; n++)
        {
            var cmd_list = drawData.CmdLists[n];

            GL.BindBuffer(BufferTarget.ArrayBuffer, m_drawObjectInfo.VertexBufferObject);
            GL.BufferSubData(BufferTarget.ArrayBuffer, IntPtr.Zero, 
                cmd_list.VtxBuffer.Size * Unsafe.SizeOf<ImDrawVert>(), cmd_list.VtxBuffer.Data);

            GL.BindBuffer(BufferTarget.ElementArrayBuffer, m_drawObjectInfo.ElementsBufferObject);
            GL.BufferSubData(BufferTarget.ElementArrayBuffer, IntPtr.Zero, 
                cmd_list.IdxBuffer.Size * sizeof(ushort), cmd_list.IdxBuffer.Data);

            int vtx_offset = 0;
            int idx_offset = 0;

            for (int cmd_i = 0; cmd_i < cmd_list.CmdBuffer.Size; cmd_i++)
            {
                var pcmd = cmd_list.CmdBuffer[cmd_i];

                if (pcmd.TextureId == m_fontTexture.Handle)
                {
                    m_fontTexture.Use(TextureUnit.Texture0);
                }
                else
                {
                    Texture.Use(pcmd.TextureId.ToInt32(), TextureUnit.Texture0);
                }

                // We do _windowHeight - (int)clip.W instead of (int)clip.Y because gl has flipped Y when it comes to these coordinates
                var clip = pcmd.ClipRect;
                GL.Scissor((int)clip.X, m_windowHeight - (int)clip.W, (int)(clip.Z - clip.X),
                    (int)(clip.W - clip.Y));

                if ((io.BackendFlags & ImGuiBackendFlags.RendererHasVtxOffset) != 0)
                    GL.DrawElementsBaseVertex(PrimitiveType.Triangles, (int)pcmd.ElemCount,
                        DrawElementsType.UnsignedShort, (IntPtr)(idx_offset * sizeof(ushort)), vtx_offset);
                else
                    GL.DrawElements(BeginMode.Triangles, (int)pcmd.ElemCount, DrawElementsType.UnsignedShort,
                        (int)pcmd.IdxOffset * sizeof(ushort));

                idx_offset += (int)pcmd.ElemCount;
            }
            vtx_offset += cmd_list.VtxBuffer.Size;
        }

        GLSettings.RestoreGLSettings(currentGlSettings);
    }

    public void Dispose()
    {
        m_fontTexture?.Dispose();
        m_shader.Dispose();
    }
}