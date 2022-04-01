using System.Buffers;
using CoolEngine.Core.Primitives;
using CoolEngine.GraphicalEngine.Core;
using CoolEngine.GraphicalEngine.Core.Font;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;

namespace CoolEngine.Services.Renderers;

public static class TextRenderer
{
    private record struct CharSize(float Width, float Height, float TranslationX, float TranslationY);

    private static readonly ArrayPool<FontVertex> m_verticesPool;
    private static readonly ArrayPool<uint> m_indicesPool;

    private static readonly int s_maxRenderAmount = 2000;
    private static readonly int s_defaultRenderAmount = 500;
    private static readonly float s_resizeThreshold = 0.9f;

    private static readonly int s_sceneVerticesAmount = 4;
    private static readonly int s_sceneIndicesAmount = 6;


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

    public static void DrawText2D(string text, Font font, in Vector2 position, in Vector3 color,
        in Vector2 rotation = default)
    {
        if ((s_drawObjectInfo == null || text.Length > s_renderAmount * s_resizeThreshold)
            && s_maxRenderAmount >= text.Length * 2)
            SetNewRenderAmount(text.Length * 2);

        var sizeMultiplier = font.FontSize / font.FontInformation.OriginalFontSize;

        Shader.Use();
        Shader.SetMatrix4("projection", GlobalSettings.ScreenProjection);
        Shader.SetMatrix4("model", Matrix4.Identity);
        Shader.SetVector3("color", color);

        font.FontInformation.Texture.Use(TextureUnit.Texture0);

        var mRotation = Matrix3.CreateRotationX(MathHelper.DegreesToRadians(rotation.X)) *
                        Matrix3.CreateRotationY(MathHelper.DegreesToRadians(rotation.Y));

        float x = position.X, y = position.Y;
        var setsAmount = text.Length / s_maxRenderAmount >= 1 ? text.Length / s_maxRenderAmount : 1;

        for (int j = 0; j < setsAmount; j++)
        {
            var offset = 0;
            var spacesAmount = 0;

            for (int i = 0; i < text.Length; i++)
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

                x += characterInfo.Advance * sizeMultiplier;

                if (_char == ' ')
                {
                    spacesAmount++;
                    continue;
                }

                offset = FillVertices(offset, s_vertices, new CharSize(cWidth, cHeight, xTransPos, yTransPos),
                    characterInfo, font.FontInformation.Texture);
            }

            GL.BindVertexArray(s_drawObjectInfo.VertexArrayObject);

            PrepareFontToDraw(offset, s_drawObjectInfo, Shader);

            GL.DrawElements(BeginMode.Triangles, (text.Length - spacesAmount) * s_sceneIndicesAmount,
                DrawElementsType.UnsignedInt, 0);
        }
    }

    public static void DrawText3D(Font font, string text, Vector3 position, Vector3 color,
        Vector3 rotation, float scale, Camera camera, bool useBillboardView = false, Vector3 outerRotation = default)
    {
        if ((s_drawObjectInfo == null || text.Length > s_renderAmount * s_resizeThreshold)
            && s_maxRenderAmount >= text.Length * 2)
            SetNewRenderAmount(text.Length * 2);
        
        var sizeMultiplier = (font.FontSize / font.FontInformation.OriginalFontSize) * scale;

        Shader.Use();
        Shader.SetMatrix4("projection", camera.LookAt * GlobalSettings.Projection);
        Shader.SetVector3("color", color);

        Matrix4 mTransOrigin;
        var mRotation = Matrix4.CreateRotationX(MathHelper.DegreesToRadians(rotation.X)) *
                        Matrix4.CreateRotationY(MathHelper.DegreesToRadians(rotation.Y)) *
                        Matrix4.CreateRotationZ(MathHelper.DegreesToRadians(rotation.Z));

        var mOutRotation = Matrix4.CreateRotationX(MathHelper.DegreesToRadians(outerRotation.X)) *
                           Matrix4.CreateRotationY(MathHelper.DegreesToRadians(outerRotation.Y)) *
                           Matrix4.CreateRotationZ(MathHelper.DegreesToRadians(outerRotation.Z));

        if (useBillboardView)
        {
            var camLookAt = camera.LookAt;
            var invertedMat = camLookAt.Inverted();

            var bMatrix = new Matrix4
            (
                camLookAt.M11, camLookAt.M21, camLookAt.M31, 0,
                camLookAt.M12, camLookAt.M22, camLookAt.M32, 0,
                invertedMat.M41, invertedMat.M42, invertedMat.M43, 0,
                position.X, position.Y, position.Z, 1
            );
            mTransOrigin = bMatrix;
        }
        else
            mTransOrigin = Matrix4.CreateTranslation(position);

        font.FontInformation.Texture.Use(TextureUnit.Texture0);

        var setsAmount = text.Length / s_maxRenderAmount >= 1 ? text.Length / s_maxRenderAmount : 1;

        float x = 0, y = 0;
        for (int j = 0; j < setsAmount; j++)
        {
            var offset = 0;
            var spacesAmount = 0;

            for (int i = 0; i < text.Length; i++)
            {
                var _char = text[i];
                CharacterInfo characterInfo;

                if (_char == '\n')
                {
                    x = 0;
                    y += font.FontSize * sizeMultiplier;
                    continue;
                }

                if (!font.FontInformation.CharacterInformations.TryGetValue(_char, out characterInfo))
                    continue;

                var cWidth = characterInfo.Size.X * sizeMultiplier;
                var cHeight = characterInfo.Size.Y * sizeMultiplier;
                var xTransPos = x + characterInfo.Bearing.X * sizeMultiplier;
                var yTransPos = y - (characterInfo.Size.Y - characterInfo.Bearing.Y) * sizeMultiplier;

                x += characterInfo.Advance * sizeMultiplier;

                if (_char == ' ')
                {
                    spacesAmount++;
                    continue;
                }
                
                offset = FillVertices(offset, s_vertices, new CharSize(cWidth, cHeight, xTransPos, yTransPos),
                    characterInfo, font.FontInformation.Texture);
            }

            Shader.SetMatrix4("model",  Matrix4.CreateTranslation(-x / 2, -(y + font.FontSize * sizeMultiplier) / 2, 0) * mRotation * mTransOrigin * mOutRotation);
            
            GL.BindVertexArray(s_drawObjectInfo.VertexArrayObject);

            PrepareFontToDraw(offset, s_drawObjectInfo, Shader);

            GL.DrawElements(BeginMode.Triangles, (text.Length - spacesAmount) * s_sceneIndicesAmount,
                DrawElementsType.UnsignedInt, 0);
        }
    }

    private static void SetNewRenderAmount(int renderAmount)
    {
        s_renderAmount = renderAmount <= 0 ? s_defaultRenderAmount : renderAmount;
        s_renderVerticesAmount = s_renderAmount * s_sceneVerticesAmount;
        s_renderIndicesAmount = s_renderAmount * s_sceneIndicesAmount;

        if (s_indices.Length != 0)
            m_indicesPool.Return(s_indices);

        if (s_vertices.Length != 0)
            m_verticesPool.Return(s_vertices);

        s_indices = m_indicesPool.Rent(s_renderIndicesAmount);
        s_vertices = m_verticesPool.Rent(s_renderVerticesAmount);

        var mesh = s_originalScene.Meshes[0];

        //Filling indices depends on original scene indices.
        //E.g. 0 + 4 * 0, 0 + 4 * 1, 0 + 4 * 2, ....
        for (int i = 0; i < s_renderAmount; i++)
        for (int j = 0; j < s_sceneIndicesAmount; j++)
            s_indices[i * s_sceneIndicesAmount + j] = mesh.Indices[j] + (uint)(s_sceneVerticesAmount * i);

        if (s_drawObjectInfo != null)
        {
            GL.BindBuffer(BufferTarget.ArrayBuffer, 0);
            GL.BindBuffer(BufferTarget.ElementArrayBuffer, 0);
            GL.BindVertexArray(0);

            GL.DeleteBuffer(s_drawObjectInfo.ElementsBufferObject);
            GL.DeleteBuffer(s_drawObjectInfo.VertexBufferObject);
            GL.DeleteVertexArray(s_drawObjectInfo.VertexArrayObject);

            s_drawObjectInfo = null;
        }

        s_drawObjectInfo = CreateDrawInfo();
    }

    private static unsafe DrawObjectInfo CreateDrawInfo()
    {
        int vao = 0, vbo = 0, ebo = 0;

        vbo = GL.GenBuffer();
        GL.BindBuffer(BufferTarget.ArrayBuffer, vbo);
        GL.BufferData(BufferTarget.ArrayBuffer, s_renderVerticesAmount * sizeof(FontVertex), (FontVertex[]?)null,
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
        GL.BufferData(BufferTarget.ElementArrayBuffer, s_renderIndicesAmount * sizeof(uint), s_indices,
            BufferUsageHint.StaticDraw);

        return new DrawObjectInfo(vao, vbo, ebo);
    }

    private static unsafe void PrepareFontToDraw(int vertexAmount, DrawObjectInfo drawObjectInfo, Shader shader)
    {
        GL.BindBuffer(BufferTarget.ArrayBuffer, drawObjectInfo.VertexBufferObject);
        GL.BufferSubData(BufferTarget.ArrayBuffer, IntPtr.Zero, vertexAmount * sizeof(FontVertex), s_vertices);

        var posIndex = shader.GetAttribLocation("iPos");
        GL.VertexAttribPointer(posIndex, 3, VertexAttribPointerType.Float, false, sizeof(FontVertex), 0);
        GL.EnableVertexAttribArray(posIndex);

        var textureIndex = shader.GetAttribLocation("iTexture");
        GL.VertexAttribPointer(textureIndex, 2, VertexAttribPointerType.Float, false, sizeof(FontVertex),
            sizeof(Vector3));
        GL.EnableVertexAttribArray(textureIndex);
    }

    private static int FillVertices(int offset, FontVertex[] vertices, CharSize size, CharacterInfo characterInfo,
        Texture texture)
    {
        var texXStart = characterInfo.Position / texture.Width;
        var texXEnd = (characterInfo.Position + characterInfo.Size.X) / texture.Width;

        var texYEnd = characterInfo.Size.Y / texture.Height;
        
        vertices[offset] = new FontVertex(
            new Vector3(size.TranslationX, size.TranslationY + size.Height, 0),
            new Vector2(texXStart, 0));

        vertices[offset + 1] = new FontVertex(
            new Vector3(size.TranslationX, size.TranslationY, 0),
            new Vector2(texXStart, texYEnd));

        vertices[offset + 2] = new FontVertex(
            new Vector3(size.TranslationX + size.Width, size.TranslationY, 0),
            new Vector2(texXEnd, texYEnd));

        vertices[offset + 3] = new FontVertex(
            new Vector3(size.TranslationX + size.Width, size.TranslationY + size.Height, 0),
            new Vector2(texXEnd, 0));
        
        return offset + 4;
    }
}