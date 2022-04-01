using CoolEngine.Core.Primitives;
using CoolEngine.GraphicalEngine.Core;
using CoolEngine.GraphicalEngine.Core.Font;
using CoolEngine.Services.Exceptions;
using CoolEngine.Services.Interfaces;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;

namespace CoolEngine.Services.Renderers;

public static class ObjectRenderer
{
    private static readonly Dictionary<Type, DrawSceneInfo> m_sceneBuffers = new();

    private static readonly Dictionary<Type, List<IDrawable>> m_elements = new();
    
    private static readonly Font DefaultFont = new Font("Arial", 14);

    public static bool RegisterScene(Type type, Shader shader)
    {
        if (type == null)
            throw new ArgumentNullException(nameof(type));

        if (shader == null)
            throw new ArgumentNullException(nameof(shader));

        return m_sceneBuffers.TryAdd(type, new DrawSceneInfo(shader, new Dictionary<int, DrawObjectInfo>()));
    }

    public static void AddDrawable(IDrawable drawable)
    {
        if (drawable == null)
            return;

        List<IDrawable> drawables;
        var drawableType = drawable.GetType();

        if (!m_elements.TryGetValue(drawableType, out drawables))
        {
            drawables = new List<IDrawable>();
            m_elements.Add(drawableType, drawables);
        }

        drawables.Add(drawable);
    }

    public static void AddDrawables<T>(IList<T> drawables)
        where T : IDrawable
    {
        if (drawables == null)
            return;

        for (int i = 0; i < drawables.Count; i++)
        {
            var drawable = drawables[i];

            AddDrawable(drawable);
        }
    }

    //TODO: Complete implementing instancing render
    public static void DrawElements(Camera camera, bool faceCounting = false)
    {
        foreach (var elemPair in m_elements)
        {
            DrawSceneInfo drawSceneInfo;

            if (!m_sceneBuffers.TryGetValue(elemPair.Key, out drawSceneInfo))
            {
                Console.WriteLine($"Cannot draw object {elemPair.Key.FullName}");
                continue;
            }

            drawSceneInfo.Shader.Use();
            drawSceneInfo.Shader.SetMatrix4("projection", GlobalSettings.Projection);
            drawSceneInfo.Shader.SetMatrix4("view", camera.LookAt);

            for (int i = 0; i < elemPair.Value.Count; i++)
            {
                var element = elemPair.Value[i];

                element.AcceptTransform();
                drawSceneInfo.Shader.SetMatrix4("model", element.Transform);

                for (int j = 0; j < element.Scene.Meshes.Count; j++)
                {
                    var mesh = element.Scene.Meshes[j];
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
            rotation, 0.1f, camera, false, element.Direction);
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
}