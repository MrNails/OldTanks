using System.Buffers;
using System.Numerics;
using System.Runtime.CompilerServices;
using CoolEngine.GraphicalEngine.Core;
using CoolEngine.GraphicalEngine.Core.Primitives;
using CoolEngine.Mathematics;
using CoolEngine.Services.Interfaces;
using OpenTK.Graphics.OpenGL4;
using Serilog;

namespace CoolEngine.Services.Renderers;

public static class ObjectRendererOld
{
    /// <summary>The smallest capacity, in vertices, of a primitives buffer.</summary>
    internal const int MinPrimitivesCapacity = 128;

    /// <summary>The largest capacity, in vertices, of a primitives buffer; a longer draw is cut to it.</summary>
    internal const int MaxPrimitivesCapacity = 1 << 20;

    private static readonly Dictionary<Type, DrawSceneInfoOld> m_sceneBuffers = new();
    private static readonly Dictionary<PrimitiveType, DrawObjectInfo> m_primitives = new();

    // The primitive types whose cut to MaxPrimitivesCapacity has been logged.
    private static readonly HashSet<PrimitiveType> s_truncatedPrimitiveTypes = new();

    private static readonly uint[] s_quadIndices = new uint[] { 0, 1, 3, 1, 2, 3 };

    public static bool RegisterScene(Type type, Shader shader)
    {
        if (type == null)
            throw new ArgumentNullException(nameof(type));

        if (shader == null)
            throw new ArgumentNullException(nameof(shader));

        return m_sceneBuffers.TryAdd(type, new DrawSceneInfoOld(shader));
    }

    //TODO: Refactor AddDrawable to make it internal 
    public static bool AddDrawable(IDrawable drawable, Shader shader)
    {
        if (drawable == null)
            return false;

        if (shader == null)
            return false;

        DrawSceneInfoOld drawSceneInfo;
        var drawableType = drawable.GetType();

        if (!m_sceneBuffers.TryGetValue(drawableType, out drawSceneInfo))
        {
            drawSceneInfo = new DrawSceneInfoOld(shader);
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
        DrawSceneInfoOld drawSceneInfo;

        return m_sceneBuffers.TryGetValue(drawableType, out drawSceneInfo) && 
               drawSceneInfo.Drawables.Remove(drawable);
    }
    
    // public static void DrawElements(Camera camera)
    // {
    //     foreach (var elemPair in m_sceneBuffers)
    //     {
    //         var drawSceneInfo = elemPair.Value;
    //
    //         if (elemPair.Key == typeof(SkyBox) ||
    //             drawSceneInfo.Drawables.Count == 0)
    //             continue;
    //
    //         drawSceneInfo.Shader.Use();
    //         drawSceneInfo.Shader.SetMatrix4("projection", EngineSettings.Current.Projection);
    //         drawSceneInfo.Shader.SetMatrix4("view", camera.LookAt);
    //
    //         for (int i = 0; i < drawSceneInfo.Drawables.Count; i++)
    //         {
    //             var element = drawSceneInfo.Drawables[i];
    //
    //             if (!element.Visible)
    //                 continue;
    //
    //             drawSceneInfo.Shader.SetMatrix4("model", element.Transformation);
    //             drawSceneInfo.Shader.SetVector4("color", Colors.White);
    //
    //             for (int j = 0; j < element.Scene.Meshes.Length; j++)
    //             {
    //                 var mesh = element.Scene.Meshes[j];
    //                 DrawObjectInfo drawObjectInfo;
    //
    //                 //Find existing draw mesh info and if it don't exists - create it
    //                 if (!drawSceneInfo.Buffers.TryGetValue(j, out drawObjectInfo))
    //                 {
    //                     drawObjectInfo = CreateDrawMeshInfo(mesh, drawSceneInfo.Shader);
    //                     drawSceneInfo.Buffers.Add(j, drawObjectInfo);
    //                 }
    //
    //                 drawSceneInfo.Shader.SetMatrix2("textureTransform",
    //                     Matrix2.CreateScale(mesh.TextureData.Scale.X, mesh.TextureData.Scale.Y) *
    //                     Matrix2.CreateRotation(MathHelper.DegreesToRadians(mesh.TextureData.RotationAngle)));
    //
    //                 GL.BindVertexArray(drawObjectInfo.VertexArrayObject);
    //
    //                 if (mesh.TextureData.Texture.Handle == 0 || !mesh.HasTextureCoords)
    //                 {
    //                     drawSceneInfo.Shader.SetVector4("color", new Vector4(0xAA, 0x55, 0x6F, 1));
    //                     drawSceneInfo.Shader.SetBool("hasTexture", false);
    //                 }
    //                 else
    //                     drawSceneInfo.Shader.SetBool("hasTexture", true);
    //                 
    //                 mesh.TextureData.Texture.Use(TextureUnit.Texture0);
    //
    //                 if (drawObjectInfo.IndicesLength != 0)
    //                     GL.DrawElements(BeginMode.Triangles, drawObjectInfo.IndicesLength, DrawElementsType.UnsignedInt, 0);
    //                 else
    //                     GL.DrawArrays(PrimitiveType.Triangles, 0, drawObjectInfo.VerticesLength);
    //             }
    //         }
    //     }
    // }

    public static void DrawSkyBox(SkyBox skyBox, Camera camera)
    {
        var elemType = typeof(SkyBox);
        DrawSceneInfoOld drawSceneInfo;

        if (!m_sceneBuffers.TryGetValue(elemType, out drawSceneInfo))
        {
            Console.WriteLine("Cannot draw skybox because DrawSceneInfo not exists.");
            return;
        }

        drawSceneInfo.Shader.Use();
        drawSceneInfo.Shader.SetMatrix4("projection", EngineSettings.Current.Projection);
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
    /// Draws primitives. Each primitive type keeps one buffer, created on its first draw and grown to the next power
    /// of two at or above the draw (at least <see cref="MinPrimitivesCapacity"/>, at most
    /// <see cref="MaxPrimitivesCapacity"/> vertices); it never shrinks. A draw longer than
    /// <see cref="MaxPrimitivesCapacity"/> is cut to its first <see cref="MaxPrimitivesCapacity"/> vertices, with one
    /// Warning per type for the process. GL thread only.
    /// </summary>
    /// <param name="type">Primitive type</param>
    /// <param name="shader">Shader program</param>
    /// <param name="primitives">Primitives indices</param>
    /// <param name="drawAmount">Primitives draw amount. If it's default (-1) - draw primitives based on <paramref name="primitives"/> length</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="drawAmount"/> is greater than the length of
    /// <paramref name="primitives"/>; thrown before any GL call.</exception>
    public static void DrawPrimitives(PrimitiveType type, Shader shader, Vector3[] primitives, int drawAmount = -1)
    {
        if (primitives == null || primitives.Length == 0)
            return;

        var requested = drawAmount <= 0 ? primitives.Length : drawAmount;

        if (drawAmount > primitives.Length)
            throw new ArgumentOutOfRangeException(nameof(drawAmount), drawAmount,
                $"The draw amount exceeds the primitives array length ({primitives.Length}).");

        shader.Use();

        m_primitives.TryGetValue(type, out var drawObjectInfo);
        var currentCapacity = drawObjectInfo?.VerticesLength ?? 0;

        var (capacity, drawCount) = GetPrimitivesUpload(currentCapacity, requested);

        if (drawObjectInfo == null)
        {
            drawObjectInfo = CreateDrawVerticesInfo(shader, capacity);
            m_primitives.Add(type, drawObjectInfo);

            Log.Logger.Information("Primitives buffer for {PrimitiveType} created for {Capacity} vertices.", type,
                capacity);
        }
        else if (capacity != currentCapacity)
        {
            GrowDrawVerticesInfo(drawObjectInfo, capacity);

            Log.Logger.Information("Primitives buffer for {PrimitiveType} grown from {OldCapacity} to {Capacity} vertices.",
                type, currentCapacity, capacity);
        }

        if (drawCount < requested && s_truncatedPrimitiveTypes.Add(type))
        {
            Log.Logger.Warning(
                "A {PrimitiveType} draw of {Requested} vertices exceeds the primitives buffer limit of {Capacity}; only the first {Capacity} are drawn (logged once per type).",
                type, requested, capacity, capacity);
        }

        PrepareVerticesToDraw(drawObjectInfo, primitives, drawCount);

        GL.DrawArrays(type, 0, drawCount);
    }

    /// <summary>
    /// Decides the capacity of a primitive type's buffer and the number of vertices to upload and draw. The capacity
    /// never shrinks: a request that fits keeps it; a larger one grows it to the next power of two at or above the
    /// request, at least <see cref="MinPrimitivesCapacity"/> and at most <see cref="MaxPrimitivesCapacity"/>. The draw
    /// count is the request cut to the capacity. Pure: no GL call, no state.
    /// </summary>
    /// <param name="currentCapacity">The buffer's capacity in vertices; 0 means no buffer yet. At most
    /// <see cref="MaxPrimitivesCapacity"/>, because only this method sets the primitives buffers' capacities; a larger
    /// value would be cut to the limit for a request above it.</param>
    /// <param name="requestedVertices">The vertices to draw; at least 1 (<see cref="DrawPrimitives"/> returns before
    /// for an empty array).</param>
    internal static (int Capacity, int DrawCount) GetPrimitivesUpload(int currentCapacity, int requestedVertices)
    {
        if (requestedVertices <= currentCapacity)
            return (currentCapacity, requestedVertices);

        var capacity = (int)BitOperations.RoundUpToPowerOf2((uint)Math.Min(requestedVertices, MaxPrimitivesCapacity));
        capacity = Math.Max(capacity, MinPrimitivesCapacity);

        return (capacity, Math.Min(requestedVertices, capacity));
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

        for (int i = 0, vIdx = 0; i < mesh.Faces.Length; i++)
        {
            var face = mesh.Faces[i];

            if (face.Indices.Length == 3)
                for (int j = 0; j < face.Indices.Length; j++, vIdx++)
                    vertexRentArr[vIdx] = new Vertex(mesh.Vertices[face.Indices[j]],
                        face.HasNormalIndices ? mesh.Normals[face.NormalsIndices[j]] : Vector3.Zero,
                        face.HasTextureIndices ? mesh.TextureCoords[face.TextureIndices[j]] : Vector2.Zero);
            else
            {
                for (int j = 0; j < s_quadIndices.Length; j++)
                    indicesRentArr[i * s_quadIndices.Length + j] = s_quadIndices[j] + (uint)vIdx;

                for (int j = 0; j < face.Indices.Length; j++, vIdx++)
                {
                    vertexRentArr[vIdx] = new Vertex(mesh.Vertices[face.Indices[j]],
                        face.HasNormalIndices ? mesh.Normals[face.NormalsIndices[j]] : Vector3.Zero,
                        face.HasTextureIndices
                            ? mesh.TextureCoords[face.TextureIndices[j]]
                            : Vector2.Zero);
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
        GL.VertexAttribPointer(textureIndex, 2, VertexAttribPointerType.Float, false, Vertex.SizeInBytes, Unsafe.SizeOf<Vector3>() * 2);
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
        // Exactly the array: DrawSkyBox draws its 36 vertices (stride one Vector3). The old size, three times the array,
        // made the driver read past the managed array's end.
        GL.BufferData(BufferTarget.ArrayBuffer, vertices.Length * Unsafe.SizeOf<Vector3>(), vertices,
            BufferUsageHint.StaticDraw);

        vao = GL.GenVertexArray();
        GL.BindVertexArray(vao);

        var posIndex = shader.GetAttribLocation("iPos");
        GL.VertexAttribPointer(posIndex, 3, VertexAttribPointerType.Float, false, Unsafe.SizeOf<Vector3>(), 0);
        GL.EnableVertexAttribArray(posIndex);

        return new DrawObjectInfo(vao, vbo, ebo);
    }
    
    // private static void DrawFaceNumber(Mesh originalMesh, ITransformable element, Camera camera, Font font)
    // {
    //     var textDrawInfo = new TextDrawInformation(color: Colors.White, originPosition: element.Position,
    //         originRotation: new Vector3(element.Direction.X, -element.Direction.Y, element.Direction.Z), scale: 0.1f);
    //
    //     for (int i = 0; i < originalMesh.Faces.Count; i++)
    //     {
    //         for (int j = 0; j < originalMesh.Faces[i].NormalsIndices.Length; j++)
    //         {
    //             var norm = originalMesh.Normals[(int)originalMesh.Faces[i].NormalsIndices[j]];
    //             textDrawInfo.SelfPosition = new Vector3(
    //                 norm.X != 0 ? norm.X * (element.Width / 2 + 0.05f) : 0,
    //                 norm.Y != 0 ? norm.Y * (element.Height / 2 + 0.05f) : 0,
    //                 norm.Z != 0 ? norm.Z * (element.Length / 2 + 0.05f) : 0);
    //
    //             textDrawInfo.SelfRotation = new Vector3(norm.Z < 0 ? 180 : norm.Y != 0 ? -norm.Y * 90 : 0,
    //                 norm.X != 0 ? norm.X * 90 : 0,
    //                 norm.Z < 0 ? 180 : 0);
    //
    //             TextRenderer.DrawText3D(font, i.ToString(), camera, textDrawInfo);
    //         }
    //     }
    // }
    
    private static DrawObjectInfo CreateDrawVerticesInfo(Shader shader, int capacity)
    {
        int vao = 0, vbo = 0, ebo = 0;

        vbo = GL.GenBuffer();
        GL.BindBuffer(BufferTarget.ArrayBuffer, vbo);
        GL.BufferData(BufferTarget.ArrayBuffer, capacity * Unsafe.SizeOf<Vector3>(), (Vector3[]?)null,
            BufferUsageHint.StreamDraw);

        vao = GL.GenVertexArray();
        GL.BindVertexArray(vao);

        var posIndex = shader.GetAttribLocation("iPos");
        GL.VertexAttribPointer(posIndex, 3, VertexAttribPointerType.Float, false, Unsafe.SizeOf<Vector3>(), 0);
        GL.EnableVertexAttribArray(posIndex);

        return new DrawObjectInfo(vao, vbo, ebo)
        {
            VerticesLength = capacity
        };
    }

    private static void GrowDrawVerticesInfo(DrawObjectInfo drawObjectInfo, int capacity)
    {
        // The VAO's attribute refers to the buffer object, not its store, so a new store keeps the VAO valid.
        GL.BindBuffer(BufferTarget.ArrayBuffer, drawObjectInfo.VertexBufferObject);
        GL.BufferData(BufferTarget.ArrayBuffer, capacity * Unsafe.SizeOf<Vector3>(), (Vector3[]?)null,
            BufferUsageHint.StreamDraw);

        drawObjectInfo.VerticesLength = capacity;
    }

    private static void PrepareVerticesToDraw(DrawObjectInfo drawObjectInfo, Vector3[] vertices, int drawAmount)
    {
        GL.BindVertexArray(drawObjectInfo.VertexArrayObject);

        GL.BindBuffer(BufferTarget.ArrayBuffer, drawObjectInfo.VertexBufferObject);
        GL.BufferSubData(BufferTarget.ArrayBuffer, IntPtr.Zero, drawAmount * Unsafe.SizeOf<Vector3>(), vertices);
    }
}