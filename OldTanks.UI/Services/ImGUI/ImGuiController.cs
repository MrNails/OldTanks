using System.Drawing;
using System.Runtime.CompilerServices;
using CoolEngine.GraphicalEngine.Core;
using CoolEngine.GraphicalEngine.Core.Texture;
using CoolEngine.GraphicalEngine.Services;
using CoolEngine.Services.Extensions;
using ImGuiNET;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;
using OpenTK.Windowing.Desktop;
using OpenTK.Windowing.GraphicsLibraryFramework;
using Serilog;

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
    private readonly Vector2 m_scaleFactor = Vector2.One;
    
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
        SetKeyMappings();

        SetPerFrameImGuiData(1f / 60f);

        ImGui.NewFrame();
        m_frameBegun = true;
    }

    public Rectangle WindowClip { get; set; }
    
    public void WindowResized(int width, int height)
    {
        m_windowWidth = width;
        m_windowHeight = height - 39;
        
        m_imGuiProjection = Matrix4.CreateOrthographicOffCenter(
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

        io.MouseWheel = offset.Y;
        io.MouseWheelH = offset.X;
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
        io.DisplayFramebufferScale = VectorExtensions.ToSystemVector2(m_scaleFactor);
        io.DeltaTime = deltaSeconds; // DeltaTime is in seconds.
    }

    private void UpdateImGuiInput(GameWindow wnd)
    {
        var io = ImGui.GetIO();

        var mouseState = wnd.MouseState;
        var keyboardState = wnd.KeyboardState;

        io.MouseDown[0] = mouseState[MouseButton.Left];
        io.MouseDown[1] = mouseState[MouseButton.Right];
        io.MouseDown[2] = mouseState[MouseButton.Middle];

        var screenPoint = new Vector2i((int)mouseState.X, (int)mouseState.Y);
        var point = screenPoint; //wnd.PointToClient(screenPoint);
        io.MousePos = new System.Numerics.Vector2(point.X, point.Y);

        foreach (Keys key in Enum.GetValues(typeof(Keys)))
        {
            if (key == Keys.Unknown)
                continue;

            io.KeysDown[(int)key] = keyboardState.IsKeyDown(key);
        }

        while (m_pressedChars.Count != 0)
            io.AddInputCharacter(m_pressedChars.Dequeue());

        io.KeyCtrl = keyboardState.IsKeyDown(Keys.LeftControl) || keyboardState.IsKeyDown(Keys.RightControl);
        io.KeyAlt = keyboardState.IsKeyDown(Keys.LeftAlt) || keyboardState.IsKeyDown(Keys.RightAlt);
        io.KeyShift = keyboardState.IsKeyDown(Keys.LeftShift) || keyboardState.IsKeyDown(Keys.RightShift);
        io.KeySuper = keyboardState.IsKeyDown(Keys.LeftSuper) || keyboardState.IsKeyDown(Keys.RightSuper);
    }

    private void RenderImDrawData(in ImDrawDataPtr drawData)
    {
        if (drawData.CmdListsCount == 0)
            return;

        // Setup orthographic projection matrix into our constant buffer
        var io = ImGui.GetIO();

        m_shader.Use();
        GL.UniformMatrix4(m_shader.GetUniformLocation("projection_matrix"), false, ref m_imGuiProjection);
        
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

    private static void SetKeyMappings()
    {
        var io = ImGui.GetIO();
        io.KeyMap[(int)ImGuiKey.Tab] = (int)Keys.Tab;
        io.KeyMap[(int)ImGuiKey.LeftArrow] = (int)Keys.Left;
        io.KeyMap[(int)ImGuiKey.RightArrow] = (int)Keys.Right;
        io.KeyMap[(int)ImGuiKey.UpArrow] = (int)Keys.Up;
        io.KeyMap[(int)ImGuiKey.DownArrow] = (int)Keys.Down;
        io.KeyMap[(int)ImGuiKey.PageUp] = (int)Keys.PageUp;
        io.KeyMap[(int)ImGuiKey.PageDown] = (int)Keys.PageDown;
        io.KeyMap[(int)ImGuiKey.Home] = (int)Keys.Home;
        io.KeyMap[(int)ImGuiKey.End] = (int)Keys.End;
        io.KeyMap[(int)ImGuiKey.Delete] = (int)Keys.Delete;
        io.KeyMap[(int)ImGuiKey.Backspace] = (int)Keys.Backspace;
        io.KeyMap[(int)ImGuiKey.Enter] = (int)Keys.Enter;
        io.KeyMap[(int)ImGuiKey.Escape] = (int)Keys.Escape;
        io.KeyMap[(int)ImGuiKey.A] = (int)Keys.A;
        io.KeyMap[(int)ImGuiKey.C] = (int)Keys.C;
        io.KeyMap[(int)ImGuiKey.V] = (int)Keys.V;
        io.KeyMap[(int)ImGuiKey.X] = (int)Keys.X;
        io.KeyMap[(int)ImGuiKey.Y] = (int)Keys.Y;
        io.KeyMap[(int)ImGuiKey.Z] = (int)Keys.Z;
    }
    
    public void Dispose()
    {
        m_fontTexture?.Dispose();
        m_shader.Dispose();
    }
}