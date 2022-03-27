using CoolEngine.Core.Primitives;
using CoolEngine.GraphicalEngine.Core;
using CoolEngine.GraphicalEngine.Core.Font;
using CoolEngine.Services.Exceptions;
using CoolEngine.Services.Interfaces;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;

namespace CoolEngine.Services;

public static class DrawManager
{
    private static readonly Dictionary<Type, DrawSceneInfo> m_sceneBuffers = new Dictionary<Type, DrawSceneInfo>();
    private static readonly Dictionary<Type, DrawSceneInfo> m_collisionBuffers = new Dictionary<Type, DrawSceneInfo>();
    private static readonly Font DefaultFont = new Font("Arial", 14);

    public static bool RegisterScene(Type type, Shader shader)
    {
        if (type == null)
            throw new ArgumentNullException(nameof(type));

        if (shader == null)
            throw new ArgumentNullException(nameof(shader));

        return m_sceneBuffers.TryAdd(type, new DrawSceneInfo(shader, new Dictionary<int, DrawObjectInfo>()));
    }
    
    public static bool RegisterCollisionScene(Type type, Shader shader)
    {
        if (type == null)
            throw new ArgumentNullException(nameof(type));

        if (shader == null)
            throw new ArgumentNullException(nameof(shader));

        return m_collisionBuffers.TryAdd(type, new DrawSceneInfo(shader, new Dictionary<int, DrawObjectInfo>()));
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

                drawSceneInfo.Shader.Use();
                drawSceneInfo.Shader.SetMatrix4("projection", GlobalSettings.Projection);
                drawSceneInfo.Shader.SetMatrix4("view", camera.LookAt);
            }
        }
    }


    public static bool DrawElementsCollision<T>(List<T> elements, Camera camera, bool useLookAt = true)
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

            if (!m_collisionBuffers.TryGetValue(elemType, out drawSceneInfo))
                throw new DrawException($"Cannot draw {element.GetType().FullName} collision");

            if (lastShaderHandle != collisionShader.Handle)
            {
                collisionShader.Use();
                collisionShader.SetMatrix4("projection", GlobalSettings.Projection);
                collisionShader.SetMatrix4("view", useLookAt ? camera.LookAt : Matrix4.Identity);
                collisionShader.SetVector3("color", Colors.Orange);

                lastShaderHandle = collisionShader.Handle;
            }

            collisionScene.UpdateCollision();

            for (int i = 0; i < collisionScene.Meshes.Count; i++)
            {
                var mesh = collisionScene.Meshes[i];
                DrawObjectInfo drawObjectInfo;

                //Find existing draw mesh info and if it don't exists - create it
                if (!drawSceneInfo.Buffers.TryGetValue(i, out drawObjectInfo))
                {
                    drawObjectInfo = CreateCollisionDrawMeshInfo(mesh, collisionShader);
                    drawSceneInfo.Buffers.Add(i, drawObjectInfo);
                }

                GL.BindVertexArray(drawObjectInfo.VertexArrayObject);

                PrepareCollisionToDraw(mesh.Vertices, drawObjectInfo, collisionShader);

                GL.LineWidth(3);
                GL.DrawElements(BeginMode.Lines, mesh.Indices.Length, DrawElementsType.UnsignedInt, 0);

                for (int j = 0; j < mesh.Vertices.Length; j++)
                {
                    var pos = mesh.Vertices[j];
                    TextRenderer.DrawText3D(DefaultFont, pos.ToString(), pos, Colors.Orange, default,
                        0.01f, camera, true);
                }

                collisionShader.Use();
            }
        }

        return true;
    }

    public static void DrawSkyBox(SkyBox skyBox, Camera camera)
    {
        var elemType = typeof(SkyBox);
        DrawSceneInfo drawSceneInfo;

        if (!m_sceneBuffers.TryGetValue(elemType, out drawSceneInfo))
            throw new DrawException($"Cannot draw object {elemType.FullName}");

        drawSceneInfo.Shader.Use();
        drawSceneInfo.Shader.SetMatrix4("projection", GlobalSettings.Projection);
        drawSceneInfo.Shader.SetMatrix3("view", new Matrix3(camera.LookAt));

        DrawObjectInfo drawObjectInfo;

        //Find existing draw mesh info and if it don't exists - create it
        if (!drawSceneInfo.Buffers.TryGetValue(0, out drawObjectInfo))
        {
            drawObjectInfo = CreateSkyBoxDrawMeshInfo(SkyBox.Vertices, drawSceneInfo.Shader);
            drawSceneInfo.Buffers.Add(0, drawObjectInfo);
        }

        GL.BindVertexArray(drawObjectInfo.VertexArrayObject);

        skyBox.Texture?.Use(TextureUnit.Texture0, TextureTarget.TextureCubeMap);

        GL.DrawArrays(PrimitiveType.Triangles, 0, 36);
    }

    private static unsafe DrawObjectInfo CreateDrawMeshInfo(Mesh mesh, Shader shader)
    {
        int vao = 0, vbo = 0, ebo = 0;

        vbo = GL.GenBuffer();
        GL.BindBuffer(BufferTarget.ArrayBuffer, vbo);
        GL.BufferData(BufferTarget.ArrayBuffer, mesh.Vertices.Length * sizeof(Vertex), mesh.Vertices,
            BufferUsageHint.StaticDraw);

        vao = GL.GenVertexArray();
        GL.BindVertexArray(vao);

        var posIndex = shader.GetAttribLocation("iPos");
        GL.VertexAttribPointer(posIndex, 3, VertexAttribPointerType.Float, false, sizeof(Vertex), 0);
        GL.EnableVertexAttribArray(posIndex);

        var textureIndex = shader.GetAttribLocation("iTextureCoord");
        GL.VertexAttribPointer(textureIndex, 2, VertexAttribPointerType.Float, false, sizeof(Vertex), sizeof(Vector3));
        GL.EnableVertexAttribArray(textureIndex);

        ebo = GL.GenBuffer();
        GL.BindBuffer(BufferTarget.ElementArrayBuffer, ebo);
        GL.BufferData(BufferTarget.ElementArrayBuffer, mesh.Indices.Length * sizeof(uint), mesh.Indices,
            BufferUsageHint.StaticDraw);

        return new DrawObjectInfo(vao, vbo, ebo);
    }

    private static unsafe DrawObjectInfo CreateCollisionDrawMeshInfo(PhysicEngine.Core.Mesh mesh, Shader shader)
    {
        int vao = 0, vbo = 0, ebo = 0;

        vbo = GL.GenBuffer();
        GL.BindBuffer(BufferTarget.ArrayBuffer, vbo);
        GL.BufferData(BufferTarget.ArrayBuffer, mesh.Vertices.Length * sizeof(Vector3), mesh.Vertices,
            BufferUsageHint.StreamDraw);

        vao = GL.GenVertexArray();
        GL.BindVertexArray(vao);

        var posIndex = shader.GetAttribLocation("iPos");
        GL.VertexAttribPointer(posIndex, 3, VertexAttribPointerType.Float, false, sizeof(Vector3), 0);
        GL.EnableVertexAttribArray(posIndex);

        ebo = GL.GenBuffer();
        GL.BindBuffer(BufferTarget.ElementArrayBuffer, ebo);
        GL.BufferData(BufferTarget.ElementArrayBuffer, mesh.Indices.Length * sizeof(uint), mesh.Indices,
            BufferUsageHint.StaticDraw);

        return new DrawObjectInfo(vao, vbo, ebo);
    }

    private static unsafe DrawObjectInfo CreateSkyBoxDrawMeshInfo(Vector3[] vertices, Shader shader)
    {
        int vao = 0, vbo = 0, ebo = 0;

        vbo = GL.GenBuffer();
        GL.BindBuffer(BufferTarget.ArrayBuffer, vbo);
        GL.BufferData(BufferTarget.ArrayBuffer, vertices.Length  * 3 * sizeof(Vector3), vertices, BufferUsageHint.StaticDraw);

        vao = GL.GenVertexArray();
        GL.BindVertexArray(vao);

        var posIndex = shader.GetAttribLocation("iPos");
        GL.VertexAttribPointer(posIndex, 3, VertexAttribPointerType.Float, false, sizeof(Vector3), 0);
        GL.EnableVertexAttribArray(posIndex);

        return new DrawObjectInfo(vao, vbo, ebo);
    }

    private static void DrawFaceNumber(Mesh originalMesh, ITransformable element, Camera camera)
    {
        var norm = originalMesh.Normal;
        var elPos = element.Position;
        var pos = new Vector3(elPos.X + (norm.X != 0 ? norm.X * (element.Width / 2 + element.Width * 0.05f) : 0),
            elPos.Y + (norm.Y != 0 ? norm.Y * (element.Height / 2 + element.Height * 0.05f) : 0),
            elPos.Z + (norm.Z != 0 ? norm.Z * (element.Length / 2 + element.Length * 0.05f) : 0));

        var rotation = new Vector3(norm.Z < 0 ? 180 : norm.Y != 0 ? -norm.Y * 90 : 0,
            norm.X != 0 ? norm.X * 90 : 0,
            norm.Z < 0 ? 180 : 0);

        TextRenderer.DrawText3D(DefaultFont, originalMesh.MeshId.ToString(), pos, Colors.White,
            rotation, 0.01f, camera, false, element.Direction);
    }

    private static unsafe void PrepareCollisionToDraw(Vector3[] vertices, DrawObjectInfo drawObjectInfo, Shader shader)
    {
        GL.BindBuffer(BufferTarget.ArrayBuffer, drawObjectInfo.VertexBufferObject);
        GL.BufferData(BufferTarget.ArrayBuffer, vertices.Length * sizeof(Vector3), vertices, BufferUsageHint.StreamDraw);

        var posIndex = shader.GetAttribLocation("iPos");
        GL.VertexAttribPointer(posIndex, 3, VertexAttribPointerType.Float, false, sizeof(Vector3), 0);
        GL.EnableVertexAttribArray(posIndex);
    }
}