using System.Buffers;
using GraphicalEngine.Core;
using GraphicalEngine.Core.Font;
using GraphicalEngine.Core.Primitives;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;

namespace GraphicalEngine.Services;

public static class TextRenderer
{
    private static readonly ArrayPool<FontVertex> m_verticesPool;
    private static readonly ArrayPool<uint> m_indicesPool;

    private static readonly int s_defaultRenderAmount = 500;
    private static readonly float s_resizeThreshold = 0.9f;

    private static int s_renderAmount;
    private static int s_renderVerticesAmount;
    private static int s_renderIndicesAmount;

    private static FontVertex[] s_vertices;
    private static uint[] s_indices;

    private static DrawObjectInfo? s_drawObjectInfo;

    private static Shader s_shader;
    private static Scene s_originalScene;

    static TextRenderer()
    {
        m_verticesPool = ArrayPool<FontVertex>.Create();
        m_indicesPool = ArrayPool<uint>.Create();

        s_vertices = Array.Empty<FontVertex>();
        s_indices = Array.Empty<uint>();
    }

    public static Shader Shader
    {
        get => s_shader;
        set
        {
            if (value == null)
                throw new ArgumentNullException(nameof(value));

            s_shader = value;
        }
    }

    public static Scene OriginalScene
    {
        get => s_originalScene;
        set
        {
            if (value == null)
                throw new ArgumentNullException(nameof(value));

            s_originalScene = value;
        }
    }

    public static void DrawText2D(string text, Font font, in Vector2 position)
    {
        DrawText2D(text, font, position, Colors.White);
    }
    
    public static void DrawText2D(string text, Font font, in Vector2 position, in Vector3 color)
    {
        if (s_drawObjectInfo == null || text.Length > s_renderAmount * s_resizeThreshold)
            SetNewRenderAmount(text.Length * 2);
        
        var sizeMultiplier = font.FontSize / font.FontInformation.OriginalFontSize;

        Shader.Use();
        Shader.SetMatrix4("projection", GlobalSettings.ScreenProjection);
        Shader.SetMatrix4("model", Matrix4.Identity);
        Shader.SetVector3("color", color);
        
        float x = position.X, y = position.Y;
        for (int i = 0, offset = 0; i < text.Length; i++, offset+=4)
        {
            var _char = text[i];
            CharacterInfo characterInfo;

            if (_char == '\n')
            {
                x = position.X;
                y -= font.FontSize;
                continue;
            }

            // if (border.HasValue &&
            //     (x < border.Value.Left + position.X || x > border.Value.Right + position.X ||
            //      y < border.Value.Top + position.Y || y > border.Value.Bottom + position.Y)
            //    )
            //     continue;

            if (!font.FontInformation.CharacterInformations.TryGetValue(_char, out characterInfo))
                continue;

            var cWidth = characterInfo.Size.X * sizeMultiplier;
            var cHeight = characterInfo.Size.Y * sizeMultiplier;
            var xTransPos = x + characterInfo.Bearing.X * sizeMultiplier;
            var yTransPos = y - (characterInfo.Size.Y - characterInfo.Bearing.Y) * sizeMultiplier;

            FontVertices(offset, s_vertices, xTransPos, yTransPos, cWidth, cHeight);

            // characterInfo.Texture.Use(TextureUnit.Texture0);

            x += characterInfo.Advance * sizeMultiplier;
        }
        
        GL.BindVertexArray(s_drawObjectInfo.VertexArrayObject);
        
        // PrepareFontToDraw();
        
        GL.DrawElements(BeginMode.Triangles, s_renderIndicesAmount, DrawElementsType.UnsignedInt, 0);
    }

    private static void SetNewRenderAmount(int renderAmount)
    {
        s_renderAmount = renderAmount <= 0 ? s_defaultRenderAmount : renderAmount;
        s_renderVerticesAmount = s_renderAmount * 4;
        s_renderIndicesAmount = s_renderAmount * 6;

        if (s_indices.Length != 0)
            m_indicesPool.Return(s_indices);

        if (s_vertices.Length != 0)
            m_verticesPool.Return(s_vertices);

        s_indices = m_indicesPool.Rent(s_renderIndicesAmount);
        s_vertices = m_verticesPool.Rent(s_renderVerticesAmount);

        var mesh = s_originalScene.Meshes[0];
        
        //Filling indices depends on original scene indices.
        //E.g. 0 + 4 * 0, 0 + 4 * 1, 0 + 4 * 2, ....
        for (int i = 0; i < s_renderIndicesAmount / mesh.Indices.Length; i++)
        for (int j = 0; j < mesh.Indices.Length; j++)
            s_indices[i + j] = mesh.Indices[j] + (uint)(mesh.Vertices.Length * i);
    }

    private static unsafe DrawObjectInfo CreateDrawInfo()
    {
        int vao = 0, vbo = 0, ebo = 0;

        vbo = GL.GenBuffer();
        GL.BindBuffer(BufferTarget.ArrayBuffer, vbo);
        GL.BufferData(BufferTarget.ArrayBuffer, s_renderIndicesAmount * sizeof(FontVertex), (FontVertex[]?)null,
            BufferUsageHint.StreamDraw);

        vao = GL.GenVertexArray();
        GL.BindVertexArray(vao);

        var posIndex = s_shader.GetAttribLocation("iPos");
        GL.VertexAttribPointer(posIndex, 3, VertexAttribPointerType.Float, false, sizeof(FontVertex), 0);
        GL.EnableVertexAttribArray(posIndex);

        var textureIndex = s_shader.GetAttribLocation("iTexture");
        GL.VertexAttribPointer(textureIndex, 2, VertexAttribPointerType.Float, false, sizeof(FontVertex),
            sizeof(Vector3));
        GL.EnableVertexAttribArray(textureIndex);

        ebo = GL.GenBuffer();
        GL.BindBuffer(BufferTarget.ElementArrayBuffer, ebo);
        GL.BufferData(BufferTarget.ElementArrayBuffer, s_renderIndicesAmount * sizeof(uint), s_indices, BufferUsageHint.StaticDraw);


        return new DrawObjectInfo(vao, vbo, ebo);
    }
    
    private static void PrepareFontToDraw(float[] vertices, DrawObjectInfo drawObjectInfo, Shader shader)
    {
        GL.BindBuffer(BufferTarget.ArrayBuffer, drawObjectInfo.VertexBufferObject);
        GL.BufferData(BufferTarget.ArrayBuffer, vertices.Length * sizeof(float), vertices, BufferUsageHint.StreamDraw);

        var posIndex = shader.GetAttribLocation("iPos");
        GL.VertexAttribPointer(posIndex, 3, VertexAttribPointerType.Float, false, 5 * sizeof(float), 0);
        GL.EnableVertexAttribArray(posIndex);

        var textureIndex = shader.GetAttribLocation("iTexture");
        GL.VertexAttribPointer(textureIndex, 2, VertexAttribPointerType.Float, false, 5 * sizeof(float),
            3 * sizeof(float));
        GL.EnableVertexAttribArray(textureIndex);
    }
    
    private static void FontVertices(int offset, FontVertex[] vertices, float xTransPos, float yTransPos, float cWidth, float cHeight)
    {
        // update VBO for each character
        vertices[offset] = new FontVertex(new Vector3(xTransPos, yTransPos + cHeight, 0), new Vector2(0, 0));
        vertices[offset + 1] = new FontVertex(new Vector3(xTransPos, yTransPos, 0), new Vector2(0, 1));
        vertices[offset + 2] = new FontVertex(new Vector3(xTransPos + cWidth, yTransPos, 0), new Vector2(1, 0));
        vertices[offset + 3] = new FontVertex(new Vector3(xTransPos + cWidth, yTransPos + cHeight, 0), new Vector2(1, 1));
    }
}