using System.Buffers;
using CoolEngine.GraphicalEngine.Core;
using CoolEngine.GraphicalEngine.Core.Font;
using CoolEngine.GraphicalEngine.Core.Primitives;
using CoolEngine.Services.Interfaces;
using CoolEngine.Services.Misc;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;

namespace CoolEngine.Services.Renderers;

public static class ObjectRenderer
{
    private static readonly int s_maxPrimitivesIndices = 6900;
    
    private static readonly Dictionary<Type, DrawSceneInfo> m_sceneBuffers = new();
    private static readonly Dictionary<PrimitiveType, DrawObjectInfo> m_primitives = new();

    private static readonly uint[] s_quadIndices = new uint[] { 0, 1, 3, 1, 2, 3 };

    private static int m_currentPrimitivesIndices = 100;

    public static bool RegisterScene(Type type, Shader shader)
    {
        if (type == null)
            throw new ArgumentNullException(nameof(type));

        if (shader == null)
            throw new ArgumentNullException(nameof(shader));

        return m_sceneBuffers.TryAdd(type, new DrawSceneInfo(shader));
    }

    public static bool AddDrawable(IDrawable drawable, Shader shader)
    {
        if (drawable == null)
            return false;

        if (shader == null)
            return false;

        DrawSceneInfo drawSceneInfo;
        var drawableType = drawable.GetType();

        if (!m_sceneBuffers.TryGetValue(drawableType, out drawSceneInfo))
        {
            drawSceneInfo = new DrawSceneInfo(shader);
            m_sceneBuffers.Add(drawableType, drawSceneInfo);
        }

        drawSceneInfo.Drawables.Add(drawable);

        return true;
    }

    public static void AddDrawables<T>(IList<T> drawables, Shader shader)
        where T : IDrawable
    {
        if (drawables == null)
            return;

        for (int i = 0; i < drawables.Count; i++)
        {
            var drawable = drawables[i];

            AddDrawable(drawable, shader);
        }
    }

    public static bool RemoveDrawable(IDrawable drawable)
    {
        if (drawable == null)
            return false;

        var drawableType = drawable.GetType();
        DrawSceneInfo drawSceneInfo;

        return m_sceneBuffers.TryGetValue(drawableType, out drawSceneInfo) && 
               drawSceneInfo.Drawables.Remove(drawable);
    }
    
    public static void DrawElements(Camera camera)
    {
        foreach (var elemPair in m_sceneBuffers)
        {
            var drawSceneInfo = elemPair.Value;

            if (elemPair.Key == typeof(SkyBox) ||
                drawSceneInfo.Drawables.Count == 0)
                continue;

            drawSceneInfo.Shader.Use();
            drawSceneInfo.Shader.SetMatrix4("projection", GlobalSettings.Projection);
            drawSceneInfo.Shader.SetMatrix4("view", camera.LookAt);

            for (int i = 0; i < drawSceneInfo.Drawables.Count; i++)
            {
                var element = drawSceneInfo.Drawables[i];

                if (!element.Visible)
                    continue;

                drawSceneInfo.Shader.SetMatrix4("model", element.Transform);
                drawSceneInfo.Shader.SetVector4("color", Colors.White);

                for (int j = 0; j < element.Scene.Meshes.Count; j++)
                {
                    var mesh = element.Scene.Meshes[j];
                    DrawObjectInfo drawObjectInfo;

                    //Find existing draw mesh info and if it don't exists - create it
                    if (!drawSceneInfo.Buffers.TryGetValue(j, out drawObjectInfo))
                    {
                        drawObjectInfo = CreateDrawMeshInfo(mesh, drawSceneInfo.Shader);
                        drawSceneInfo.Buffers.Add(j, drawObjectInfo);
                    }

                    drawSceneInfo.Shader.SetMatrix2("textureTransform",
                        Matrix2.CreateScale(mesh.TextureData.Scale.X, mesh.TextureData.Scale.Y) *
                        Matrix2.CreateRotation(MathHelper.DegreesToRadians(mesh.TextureData.RotationAngle)));

                    GL.BindVertexArray(drawObjectInfo.VertexArrayObject);

                    if (mesh.TextureData.Texture.Handle == 0 || !mesh.HasTextureCoords)
                    {
                        drawSceneInfo.Shader.SetVector4("color", new Vector4(0xAA, 0x55, 0x6F, 1));
                        drawSceneInfo.Shader.SetBool("hasTexture", false);
                    }
                    else
                        drawSceneInfo.Shader.SetBool("hasTexture", true);
                    
                    mesh.TextureData.Texture?.Use(TextureUnit.Texture0);

                    if (drawObjectInfo.IndicesLength != 0)
                        GL.DrawElements(BeginMode.Triangles, drawObjectInfo.IndicesLength, DrawElementsType.UnsignedInt, 0);
                    else
                        GL.DrawArrays(PrimitiveType.Triangles, 0, drawObjectInfo.VerticesLength);
                }
            }
        }
    }

    public static void DrawSkyBox(SkyBox skyBox, Camera camera)
    {
        var elemType = typeof(SkyBox);
        DrawSceneInfo drawSceneInfo;

        if (!m_sceneBuffers.TryGetValue(elemType, out drawSceneInfo))
        {
            Console.WriteLine("Cannot draw skybox because DrawSceneInfo not exists.");
            return;
        }

        drawSceneInfo.Shader.Use();
        drawSceneInfo.Shader.SetMatrix4("projection", GlobalSettings.Projection);
        drawSceneInfo.Shader.SetMatrix3("view", new Matrix3(camera.LookAt));
        // drawSceneInfo.Shader.SetMatrix4("model", Matrix4.CreateRotationY(MathHelper.DegreesToRadians(skyBox.Rotation.Y)));

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

    /// <summary>
    /// Draws primitives
    /// </summary>
    /// <param name="type">Primitive type</param>
    /// <param name="shader">Shader program</param>
    /// <param name="primitives">Primitives indices</param>
    /// <param name="drawAmount">Primitives draw amount. If it's default (-1) - draw primitives based on <paramref name="primitives"/> length</param>
    public static void DrawPrimitives(PrimitiveType type, Shader shader, Vector3[] primitives, int drawAmount = -1)
    {
        if (primitives == null || primitives.Length == 0)
            return;

        shader.Use();

        var tmpDrawLength = drawAmount <= 0 ? primitives.Length : drawAmount;
        var needRecreate = false;

        DrawObjectInfo drawObjectInfo;

        if (tmpDrawLength > m_currentPrimitivesIndices && tmpDrawLength <= s_maxPrimitivesIndices &&
            tmpDrawLength * 2 <= s_maxPrimitivesIndices)
        {
            m_currentPrimitivesIndices = tmpDrawLength * 2;
            needRecreate = true;
        }
        else if (tmpDrawLength > m_currentPrimitivesIndices)
        {
            m_currentPrimitivesIndices = s_maxPrimitivesIndices;
            needRecreate = true;
        }

        if (needRecreate)
        {
            m_primitives.Remove(type, out drawObjectInfo);

            if (drawObjectInfo != null)
            {
                GL.BindBuffer(BufferTarget.ArrayBuffer, 0);
                GL.BindBuffer(BufferTarget.ElementArrayBuffer, 0);
                GL.BindVertexArray(0);

                GL.DeleteBuffer(drawObjectInfo.ElementsBufferObject);
                GL.DeleteBuffer(drawObjectInfo.VertexBufferObject);
                GL.DeleteVertexArray(drawObjectInfo.VertexArrayObject);
            }
            
            drawObjectInfo = CreateDrawVerticesInfo(shader);
            m_primitives.Add(type, drawObjectInfo);
        }
        else if (!m_primitives.TryGetValue(type, out drawObjectInfo))
        {
            drawObjectInfo = CreateDrawVerticesInfo(shader);
            m_primitives.Add(type, drawObjectInfo);
        }

        PrepareVerticesToDraw(drawObjectInfo, primitives, tmpDrawLength);
        
        GL.DrawArrays(type, 0, tmpDrawLength);
    }
    
    private static DrawObjectInfo CreateDrawMeshInfo(Mesh mesh, Shader shader)
    {
        int vao = 0, vbo = 0, ebo = 0;

        var indicesLength = 0;
        var verticesLength = mesh.Faces.Sum(f =>
        {
            indicesLength += f.Indices.Length * (f.Indices.Length == 3 ? 1 : 2);
            return f.Indices.Length;
        });

        var vertexRentArr = ArrayPool<Vertex>.Shared.Rent(verticesLength);
        var indicesRentArr = indicesLength == verticesLength
            ? Array.Empty<uint>()
            : ArrayPool<uint>.Shared.Rent(indicesLength);

        for (int i = 0, vIdx = 0; i < mesh.Faces.Count; i++)
        {
            var face = mesh.Faces[i];

            if (face.Indices.Length == 3)
                for (int j = 0; j < face.Indices.Length; j++, vIdx++)
                    vertexRentArr[vIdx] = new Vertex(mesh.Vertices[face.Indices[j]],
                        face.HasTextureIndices ? mesh.TextureCoords[face.TextureIndices[j]] : Vector2.Zero,
                        face.HasNormalIndices ? mesh.Normals[face.NormalsIndices[j]] : Vector3.Zero,
                        0);
            else
            {
                for (int j = 0; j < s_quadIndices.Length; j++)
                    indicesRentArr[i * s_quadIndices.Length + j] = s_quadIndices[j] + (uint)vIdx;

                for (int j = 0; j < face.Indices.Length; j++, vIdx++)
                {
                    vertexRentArr[vIdx] = new Vertex(mesh.Vertices[face.Indices[j]],
                        face.HasTextureIndices
                            ? mesh.TextureCoords[face.TextureIndices[j]]
                            : Vector2.Zero,
                        face.HasNormalIndices ? mesh.Normals[face.NormalsIndices[j]] : Vector3.Zero,
                        0);
                }
            }
        }

        vbo = GL.GenBuffer();
        GL.BindBuffer(BufferTarget.ArrayBuffer, vbo);
        GL.BufferData(BufferTarget.ArrayBuffer, verticesLength * Vertex.SizeInBytes, vertexRentArr,
            BufferUsageHint.StaticDraw);

        ArrayPool<Vertex>.Shared.Return(vertexRentArr);

        vao = GL.GenVertexArray();
        GL.BindVertexArray(vao);

        var posIndex = shader.GetAttribLocation("iPos");
        GL.VertexAttribPointer(posIndex, 3, VertexAttribPointerType.Float, false, Vertex.SizeInBytes, 0);
        GL.EnableVertexAttribArray(posIndex);

        var textureIndex = shader.GetAttribLocation("iTextureCoord");
        GL.VertexAttribPointer(textureIndex, 2, VertexAttribPointerType.Float, false, Vertex.SizeInBytes, Vector3.SizeInBytes);
        GL.EnableVertexAttribArray(textureIndex);

        if (indicesRentArr.Length != 0)
        {
            ebo = GL.GenBuffer();
            GL.BindBuffer(BufferTarget.ElementArrayBuffer, ebo);
            GL.BufferData(BufferTarget.ElementArrayBuffer, indicesLength * sizeof(uint), indicesRentArr,
                BufferUsageHint.StaticDraw);

            ArrayPool<uint>.Shared.Return(indicesRentArr);
        }

        return new DrawObjectInfo(vao, vbo, ebo)
        {
            IndicesLength = indicesLength != verticesLength ? indicesLength : 0,
            VerticesLength = verticesLength
        };
    }

    private static DrawObjectInfo CreateSkyBoxDrawMeshInfo(Vector3[] vertices, Shader shader)
    {
        int vao = 0, vbo = 0, ebo = 0;

        vbo = GL.GenBuffer();
        GL.BindBuffer(BufferTarget.ArrayBuffer, vbo);
        GL.BufferData(BufferTarget.ArrayBuffer, vertices.Length * 3 * Vector3.SizeInBytes, vertices,
            BufferUsageHint.StaticDraw);

        vao = GL.GenVertexArray();
        GL.BindVertexArray(vao);

        var posIndex = shader.GetAttribLocation("iPos");
        GL.VertexAttribPointer(posIndex, 3, VertexAttribPointerType.Float, false, Vector3.SizeInBytes, 0);
        GL.EnableVertexAttribArray(posIndex);

        return new DrawObjectInfo(vao, vbo, ebo);
    }
    
    private static void DrawFaceNumber(Mesh originalMesh, ITransformable element, Camera camera, Font font)
    {
        var textDrawInfo = new TextDrawInformation(color: Colors.White, originPosition: element.Position,
            originRotation: new Vector3(element.Direction.X, -element.Direction.Y, element.Direction.Z), scale: 0.1f);

        for (int i = 0; i < originalMesh.Faces.Count; i++)
        {
            for (int j = 0; j < originalMesh.Faces[i].NormalsIndices.Length; j++)
            {
                var norm = originalMesh.Normals[(int)originalMesh.Faces[i].NormalsIndices[j]];
                textDrawInfo.SelfPosition = new Vector3(
                    norm.X != 0 ? norm.X * (element.Width / 2 + 0.05f) : 0,
                    norm.Y != 0 ? norm.Y * (element.Height / 2 + 0.05f) : 0,
                    norm.Z != 0 ? norm.Z * (element.Length / 2 + 0.05f) : 0);

                textDrawInfo.SelfRotation = new Vector3(norm.Z < 0 ? 180 : norm.Y != 0 ? -norm.Y * 90 : 0,
                    norm.X != 0 ? norm.X * 90 : 0,
                    norm.Z < 0 ? 180 : 0);

                TextRenderer.DrawText3D(font, i.ToString(), camera, textDrawInfo);
            }
        }
    }
    
    private static DrawObjectInfo CreateDrawVerticesInfo(Shader shader)
    {
        int vao = 0, vbo = 0, ebo = 0;

        vbo = GL.GenBuffer();
        GL.BindBuffer(BufferTarget.ArrayBuffer, vbo);
        GL.BufferData(BufferTarget.ArrayBuffer, m_currentPrimitivesIndices * Vector3.SizeInBytes, (Vector3[]?)null,
            BufferUsageHint.StreamDraw);

        vao = GL.GenVertexArray();
        GL.BindVertexArray(vao);

        var posIndex = shader.GetAttribLocation("iPos");
        GL.VertexAttribPointer(posIndex, 3, VertexAttribPointerType.Float, false, Vector3.SizeInBytes, 0);
        GL.EnableVertexAttribArray(posIndex);

        return new DrawObjectInfo(vao, vbo, ebo);
    }

    private static void PrepareVerticesToDraw(DrawObjectInfo drawObjectInfo, Vector3[] vertices, int drawAmount)
    {
        GL.BindVertexArray(drawObjectInfo.VertexArrayObject);

        GL.BindBuffer(BufferTarget.ArrayBuffer, drawObjectInfo.VertexBufferObject);
        GL.BufferSubData(BufferTarget.ArrayBuffer, IntPtr.Zero, drawAmount * Vector3.SizeInBytes, vertices);
    }
}