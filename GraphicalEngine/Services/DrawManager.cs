using System.Buffers;
using System.Drawing;
using GraphicalEngine.Core;
using GraphicalEngine.Core.Font;
using GraphicalEngine.Services.Exceptions;
using GraphicalEngine.Services.Interfaces;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;

namespace GraphicalEngine.Services;

public static class DrawManager
{
    private static readonly Dictionary<Type, DrawSceneInfo> m_sceneBuffers = new Dictionary<Type, DrawSceneInfo>();
    private static readonly Font DefaultFont = new Font("Arial", 14);

    public static bool RegisterScene(Type type, Shader shader)
    {
        if (type == null)
            throw new ArgumentNullException(nameof(type));

        if (shader == null)
            throw new ArgumentNullException(nameof(shader));

        return m_sceneBuffers.TryAdd(type, new DrawSceneInfo(shader, new Dictionary<int, DrawObjectInfo>()));
    }

    public static void DrawElements<T>(List<T> elements, Camera camera, bool faceCounting = false)
        where T : IDrawable
    {
        int lastShaderHandle = -1;
        foreach (var element in elements)
        {
            var scene = element.Scene;
            var elemType = element.GetType();
            DrawSceneInfo drawSceneInfo;

            if (!m_sceneBuffers.TryGetValue(elemType, out drawSceneInfo))
                throw new DrawException($"Cannot draw object {element.GetType().FullName}");

            if (lastShaderHandle != drawSceneInfo.Shader.Handle)
            {
                drawSceneInfo.Shader.Use();
                drawSceneInfo.Shader.SetMatrix4("projection", GlobalSettings.Projection);
                drawSceneInfo.Shader.SetMatrix4("view", camera.LookAt);

                lastShaderHandle = drawSceneInfo.Shader.Handle;
            }

            element.AcceptTransform();
            drawSceneInfo.Shader.SetMatrix4("model", element.Transform);

            for (int i = 0; i < scene.Meshes.Count; i++)
            {
                var mesh = scene.Meshes[i];
                DrawObjectInfo drawObjectInfo;

                //Find existing draw mesh info and if it don't exists - create it
                if (!drawSceneInfo.Buffers.TryGetValue(mesh.MeshId, out drawObjectInfo))
                {
                    drawObjectInfo = CreateDrawMeshInfo(mesh, drawSceneInfo.Shader);
                    drawSceneInfo.Buffers.Add(mesh.MeshId, drawObjectInfo);
                }

                GL.BindVertexArray(drawObjectInfo.VertexArrayObject);

                mesh.Texture.Use(TextureUnit.Texture0);

                GL.DrawElements(BeginMode.Triangles, mesh.Indices.Length, DrawElementsType.UnsignedInt, 0);

                if (!faceCounting)
                    continue;

                DrawFaceNumber(mesh, element, camera);

                // Console.WriteLine($"Index: {i} Pos: {pos}; Rotation: {rotation}; Norm: {norm}");

                drawSceneInfo.Shader.Use();
                drawSceneInfo.Shader.SetMatrix4("projection", GlobalSettings.Projection);
                drawSceneInfo.Shader.SetMatrix4("view", camera.LookAt);
            }

            // Console.WriteLine("\n\n");
        }
    }


    public static bool DrawElementsCollision<T>(List<T> elements, Camera camera)
        where T : ICollisionable
    {
        int lastShaderHandle = -1;
        var collisionShader = GlobalCache<Shader>.GetItemOrDefault("CollisionShader");

        if (collisionShader == null)
        {
            Console.WriteLine("Cannot find collision shader for drawing collision.");
            return false;
        }
        
        foreach (var element in elements)
        {
            var collisionScene = element.Collision;
            var elemType = element.GetType();
            DrawSceneInfo drawSceneInfo;

            if (!m_sceneBuffers.TryGetValue(elemType, out drawSceneInfo))
                throw new DrawException($"Cannot draw {element.GetType().FullName} collision");

            if (lastShaderHandle != collisionShader.Handle)
            {
                collisionShader.Use();
                collisionShader.SetMatrix4("projection", GlobalSettings.Projection);
                collisionShader.SetMatrix4("view", camera.LookAt);
                collisionShader.SetVector3("color", Colors.Orange);

                lastShaderHandle = collisionShader.Handle;
            }

            collisionScene.UpdateCollision();

            for (int i = 0; i < collisionScene.Meshes.Count; i++)
            {
                var mesh = collisionScene.Meshes[i];
                DrawObjectInfo drawObjectInfo;

                //Find existing draw mesh info and if it don't exists - create it
                if (!drawSceneInfo.Buffers.TryGetValue(mesh.MeshId, out drawObjectInfo))
                {
                    drawObjectInfo = CreateCollisionDrawMeshInfo(mesh, collisionShader);
                    drawSceneInfo.Buffers.Add(mesh.MeshId, drawObjectInfo);
                }

                GL.BindVertexArray(drawObjectInfo.VertexArrayObject);

                PrepareCollisionToDraw(mesh.Vertices, drawObjectInfo, collisionShader);

                GL.LineWidth(3);
                GL.DrawElements(BeginMode.Lines, mesh.Indices.Length, DrawElementsType.UnsignedInt, 0);

                DrawFaceNumber(mesh, collisionScene.CurrentObject, camera);

                GL.Disable(EnableCap.CullFace);
                
                for (int j = 0; j < mesh.Vertices.Length; j += 3)
                {
                    var pos = new Vector3(mesh.Vertices[j], mesh.Vertices[j + 1], mesh.Vertices[j + 2]);
                    
                    DrawText3D(DefaultFont, pos.ToString(), pos, Colors.Orange, new Vector3(180, 0, 0), 
                        0.005f, camera, true);
                }
                
                GL.Enable(EnableCap.CullFace);

                collisionShader.Use();
            }
            
        }

        return true;
    }

    public static void DrawText2D(Font font, string text, Vector2 position)
    {
        DrawText2D(font, text, position, Colors.White, null);
    }

    public static void DrawText2D(Font font, string text, Vector2 position, Vector3 color)
    {
        DrawText2D(font, text, position, color, null);
    }

    public static void DrawText2D(Font font, string text, Vector2 position, Vector3 color, RectangleF? border = null)
    {
        var scene = GlobalCache<Scene>.GetItemOrDefault("DynamicFontScene");

        DrawSceneInfo drawSceneInfo;

        if (!m_sceneBuffers.TryGetValue(typeof(string), out drawSceneInfo))
            return;

        // var transOrigin = Matrix4.CreateTranslation(position);
        // var normalTextRotate = Matrix4.CreateRotationZ(MathHelper.DegreesToRadians(40));
        var sizeMultiplier = font.FontSize / font.FontInformation.OriginalFontSize;

        drawSceneInfo.Shader.Use();
        drawSceneInfo.Shader.SetMatrix4("projection", GlobalSettings.ScreenProjection);
        drawSceneInfo.Shader.SetVector3("color", color);

        var mesh = scene.Meshes[0];
        DrawObjectInfo drawObjInfo;

        if (!drawSceneInfo.Buffers.TryGetValue(mesh.MeshId, out drawObjInfo))
        {
            drawObjInfo = CreateFontDrawMeshInfo(mesh, drawSceneInfo.Shader);
            drawSceneInfo.Buffers.Add(mesh.MeshId, drawObjInfo);
        }

        float x = position.X, y = position.Y;
        // float x = 0, y = 0;
        for (int i = 0; i < text.Length; i++)
        {
            var _char = text[i];
            CharacterInfo characterInfo;

            if (_char == '\n')
            {
                x = position.X;
                y -= font.FontSize;
                // y += font.FontSize * 2f;
                continue;
            }

            if (border.HasValue &&
                (x < border.Value.Left + position.X || x > border.Value.Right + position.X ||
                 y < border.Value.Top + position.Y || y > border.Value.Bottom + position.Y)
               )
                continue;

            if (!font.FontInformation.CharacterInformations.TryGetValue(_char, out characterInfo))
                continue;

            var cWidth = characterInfo.Size.X * sizeMultiplier;
            var cHeight = characterInfo.Size.Y * sizeMultiplier;
            var xTransPos = x + characterInfo.Bearing.X * sizeMultiplier;
            var yTransPos = y - (characterInfo.Size.Y - characterInfo.Bearing.Y) * sizeMultiplier;

            GL.BindVertexArray(drawObjInfo.VertexArrayObject);

            var vertices = ArrayPool<float>.Shared.Rent(20);

            FontVertices(vertices, xTransPos, yTransPos, cWidth, cHeight);

            drawSceneInfo.Shader.SetMatrix4("model", Matrix4.Identity);

            characterInfo.Texture.Use(TextureUnit.Texture0);

            PrepareFontToDraw(vertices, drawObjInfo, drawSceneInfo.Shader);

            GL.DrawArrays(PrimitiveType.Triangles, 0, 6);

            x += characterInfo.Advance * sizeMultiplier;
        }

        // GL.BindVertexArray(0);
        // GL.BindTexture(TextureTarget.Texture2D, 0);
    }

    public static void DrawText3D(Font font, string text, Vector3 position, Vector3 color,
        Vector3 rotation, float scale, Camera camera, bool useBillboardView = false, Vector3 outerRotation = default)
    {
        var scene = GlobalCache<Scene>.GetItemOrDefault("FontScene");
        DrawSceneInfo drawSceneInfo;

        if (!m_sceneBuffers.TryGetValue(typeof(string), out drawSceneInfo))
            return;

        var sizeMultiplier = font.FontSize / font.FontInformation.OriginalFontSize;

        drawSceneInfo.Shader.Use();
        drawSceneInfo.Shader.SetMatrix4("projection", camera.LookAt * GlobalSettings.Projection);
        drawSceneInfo.Shader.SetVector3("color", color);

        var mesh = scene.Meshes[0];
        DrawObjectInfo drawObjInfo;

        //Find existing draw mesh info and if it don't exists - create it
        if (!drawSceneInfo.Buffers.TryGetValue(mesh.MeshId, out drawObjInfo))
        {
            drawObjInfo = CreateFontDrawMeshInfo(mesh, drawSceneInfo.Shader);
            drawSceneInfo.Buffers.Add(mesh.MeshId, drawObjInfo);
        }

        Matrix4 mTransOrigin;
        var mRotationX = Matrix4.CreateRotationX(MathHelper.DegreesToRadians(rotation.X));
        var mRotationY = Matrix4.CreateRotationY(MathHelper.DegreesToRadians(rotation.Y));
        var mRotationZ = Matrix4.CreateRotationZ(MathHelper.DegreesToRadians(rotation.Z));
        
        var mOutRotationX = Matrix4.CreateRotationX(MathHelper.DegreesToRadians(outerRotation.X));
        var mOutRotationY = Matrix4.CreateRotationY(MathHelper.DegreesToRadians(outerRotation.Y));
        var mOutRotationZ = Matrix4.CreateRotationZ(MathHelper.DegreesToRadians(outerRotation.Z));

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

        float x = 0, y = 0;
        for (int i = 0; i < text.Length; i++)
        {
            var _char = text[i];
            CharacterInfo characterInfo;

            if (_char == '\n')
            {
                x = 0;
                y += font.FontSize * scale;
                continue;
            }

            if (!font.FontInformation.CharacterInformations.TryGetValue(_char, out characterInfo))
                continue;

            var cWidth = characterInfo.Size.X * sizeMultiplier * scale;
            var cHeight = characterInfo.Size.Y * sizeMultiplier * scale;
            var xTransPos = x + characterInfo.Bearing.X * characterInfo.Bearing.X * 0.5f * sizeMultiplier * scale;
            var yTransPos = y + (characterInfo.Size.Y - characterInfo.Bearing.Y) * sizeMultiplier * scale;

            x += characterInfo.Advance * 2f * sizeMultiplier * scale;

            var mScale = Matrix4.CreateScale(cWidth, cHeight, 1.0f);
            var mTranslate = Matrix4.CreateTranslation(xTransPos, yTransPos, 0);

            var mModel = mScale * mTranslate * mRotationX * mRotationY * mRotationZ * 
                         mTransOrigin * mOutRotationX * mOutRotationY * mOutRotationZ;

            drawSceneInfo.Shader.SetMatrix4("model", mModel);

            characterInfo.Texture.Use(TextureUnit.Texture0);

            GL.BindVertexArray(drawObjInfo.VertexArrayObject);
            GL.DrawElements(BeginMode.Triangles, mesh.Indices.Length, DrawElementsType.UnsignedInt, 0);
        }
    }

    private static DrawObjectInfo CreateDrawMeshInfo(Mesh mesh, Shader shader)
    {
        int vao = 0, vbo = 0, ebo = 0;

        vbo = GL.GenBuffer();
        GL.BindBuffer(BufferTarget.ArrayBuffer, vbo);
        GL.BufferData(BufferTarget.ArrayBuffer, mesh.Vertices.Length * sizeof(float), mesh.Vertices,
            BufferUsageHint.StaticDraw);

        vao = GL.GenVertexArray();
        GL.BindVertexArray(vao);

        var posIndex = shader.GetAttribLocation("iPos");
        GL.VertexAttribPointer(posIndex, 3, VertexAttribPointerType.Float, false, 5 * sizeof(float), 0);
        GL.EnableVertexAttribArray(posIndex);

        var textureIndex = shader.GetAttribLocation("iTextureCoord");
        GL.VertexAttribPointer(textureIndex, 2, VertexAttribPointerType.Float, false, 5 * sizeof(float),
            3 * sizeof(float));
        GL.EnableVertexAttribArray(textureIndex);

        ebo = GL.GenBuffer();
        GL.BindBuffer(BufferTarget.ElementArrayBuffer, ebo);
        GL.BufferData(BufferTarget.ElementArrayBuffer, mesh.Indices.Length * sizeof(uint), mesh.Indices,
            BufferUsageHint.StaticDraw);

        return new DrawObjectInfo(vao, vbo, ebo);
    }

    private static DrawObjectInfo CreateFontDrawMeshInfo(Mesh mesh, Shader shader)
    {
        int vao = 0, vbo = 0, ebo = 0;

        vbo = GL.GenBuffer();
        GL.BindBuffer(BufferTarget.ArrayBuffer, vbo);
        GL.BufferData(BufferTarget.ArrayBuffer, mesh.Vertices.Length * sizeof(float), mesh.Vertices,
            BufferUsageHint.StreamDraw);

        vao = GL.GenVertexArray();
        GL.BindVertexArray(vao);

        var posIndex = shader.GetAttribLocation("iPos");
        GL.VertexAttribPointer(posIndex, 3, VertexAttribPointerType.Float, false, 5 * sizeof(float), 0);
        GL.EnableVertexAttribArray(posIndex);

        var textureIndex = shader.GetAttribLocation("iTexture");
        GL.VertexAttribPointer(textureIndex, 2, VertexAttribPointerType.Float, false, 5 * sizeof(float),
            3 * sizeof(float));
        GL.EnableVertexAttribArray(textureIndex);

        ebo = GL.GenBuffer();
        GL.BindBuffer(BufferTarget.ElementArrayBuffer, ebo);
        GL.BufferData(BufferTarget.ElementArrayBuffer, mesh.Indices.Length * sizeof(uint), mesh.Indices,
            BufferUsageHint.StaticDraw);

        return new DrawObjectInfo(vao, vbo, ebo);
    }

    private static DrawObjectInfo CreateCollisionDrawMeshInfo(Mesh mesh, Shader shader)
    {
        int vao = 0, vbo = 0, ebo = 0;

        vbo = GL.GenBuffer();
        GL.BindBuffer(BufferTarget.ArrayBuffer, vbo);
        GL.BufferData(BufferTarget.ArrayBuffer, mesh.Vertices.Length * sizeof(float), mesh.Vertices,
            BufferUsageHint.StreamDraw);

        vao = GL.GenVertexArray();
        GL.BindVertexArray(vao);

        var posIndex = shader.GetAttribLocation("iPos");
        GL.VertexAttribPointer(posIndex, 3, VertexAttribPointerType.Float, false, 3 * sizeof(float), 0);
        GL.EnableVertexAttribArray(posIndex);

        ebo = GL.GenBuffer();
        GL.BindBuffer(BufferTarget.ElementArrayBuffer, ebo);
        GL.BufferData(BufferTarget.ElementArrayBuffer, mesh.Indices.Length * sizeof(uint), mesh.Indices,
            BufferUsageHint.StaticDraw);

        return new DrawObjectInfo(vao, vbo, ebo);
    }

    private static void DrawFaceNumber(Mesh originalMesh, ITransformable element, Camera camera)
    {
        var norm = originalMesh.Normal;
        var elPos = element.Position;
        var pos = new Vector3(elPos.X + (norm.X != 0 ? norm.X * (element.Width / 2 + element.Width * 0.05f) : 0),
            elPos.Y + (norm.Y != 0 ? norm.Y * (element.Height / 2 + element.Height * 0.05f) : 0),
            elPos.Z + (norm.Z != 0 ? norm.Z * (element.Length / 2 + element.Length * 0.05f) : 0));

        var rotation = new Vector3(norm.X != 0 ? 180 : norm.Y < 0 ? 90 : norm.Y > 0 ? 270 : 0,
            norm.Y != 0 ? 180 : norm.Z > 0 ? 180 : norm.X > 0 ? 90 : norm.X < 0 ? 270 : 0,
            norm.Z != 0 ? 180 : norm.Y != 0 ? 180 : 0);

        DrawText3D(DefaultFont, originalMesh.MeshId.ToString(), pos, Colors.White,
            rotation, 0.01f, camera, false, element.Direction);
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

    private static void PrepareCollisionToDraw(float[] vertices, DrawObjectInfo drawObjectInfo, Shader shader)
    {
        GL.BindBuffer(BufferTarget.ArrayBuffer, drawObjectInfo.VertexBufferObject);
        GL.BufferData(BufferTarget.ArrayBuffer, vertices.Length * sizeof(float), vertices, BufferUsageHint.StreamDraw);

        var posIndex = shader.GetAttribLocation("iPos");
        GL.VertexAttribPointer(posIndex, 3, VertexAttribPointerType.Float, false, 3 * sizeof(float), 0);
        GL.EnableVertexAttribArray(posIndex);
    }

    private static void FontVertices(float[] vertices, float xTransPos, float yTransPos, float cWidth, float cHeight)
    {
        // update VBO for each character
        vertices[0] = xTransPos;           vertices[1] = yTransPos + cHeight; vertices[2] = 0.0f;  vertices[3] = 0.0f;  vertices[4] = 0.0f;
        vertices[5] = xTransPos;           vertices[6] = yTransPos;           vertices[7] = 0.0f;  vertices[8] = 0.0f;  vertices[9] = 1.0f;
        vertices[10] = xTransPos + cWidth; vertices[11] = yTransPos;          vertices[12] = 0.0f; vertices[13] = 1.0f; vertices[14] = 1.0f;

        vertices[15] = xTransPos;          vertices[16] = yTransPos + cHeight; vertices[17] = 0.0f; vertices[18] = 0.0f; vertices[19] = 0.0f;
        vertices[20] = xTransPos + cWidth; vertices[21] = yTransPos;           vertices[22] = 0.0f; vertices[23] = 1.0f; vertices[24] = 1.0f;
        vertices[25] = xTransPos + cWidth; vertices[26] = yTransPos + cHeight; vertices[27] = 0.0f; vertices[28] = 1.0f; vertices[29] = 0.0f;
    }
}