using System.Buffers;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using CoolEngine.GraphicalEngine.Core;
using CoolEngine.GraphicalEngine.Core.Font;
using CoolEngine.GraphicalEngine.Core.Primitives;
using CoolEngine.Services.Exceptions;
using CoolEngine.Services.Interfaces;
using CoolEngine.Services.Misc;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;

namespace CoolEngine.Services.Renderers;

public static class ObjectRenderer
{
    private static readonly Dictionary<Type, DrawSceneInfo> m_sceneBuffers = new();

    private static readonly uint[] s_quadIndices = new uint[] { 0, 1, 3, 1, 2, 3 };

    private static DrawObjectInfo s_normalObjInfo;

    private static readonly Font DefaultFont = new Font("Arial", 14);

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

    //TODO: Complete implementing instancing render
    public static void DrawElements(Camera camera, bool faceCounting = false, bool showNormals = false)
    {
        if (s_normalObjInfo == null)
            s_normalObjInfo = CreateDrawNormalInfo(GlobalCache<Shader>.GetItemOrDefault("DefaultShader"));

        var textDrawInfo = new TextDrawInformation
        {
            Color = Colors.Red,
            Scale = 0.1f
        };

        foreach (var elemPair in m_sceneBuffers)
        {
            var drawSceneInfo = elemPair.Value;

            drawSceneInfo.Shader.Use();
            drawSceneInfo.Shader.SetMatrix4("projection", GlobalSettings.Projection);
            drawSceneInfo.Shader.SetMatrix4("view", camera.LookAt);

            if (drawSceneInfo.Drawables.Count == 0)
                continue;

            var normals = ArrayPool<Vector3>.Shared.Rent(drawSceneInfo.Drawables[0].Scene.Meshes
                .Sum(m => m.Faces.Sum(f => f.NormalsIndices.Length)) * 2);

            for (int i = 0; i < drawSceneInfo.Drawables.Count; i++)
            {
                var element = drawSceneInfo.Drawables[i];
                var normalsCount = 0;

                if (!element.Visible)
                    continue;

                textDrawInfo.OriginPosition = element.Position;

                element.AcceptTransform();
                drawSceneInfo.Shader.SetMatrix4("model", element.Transform);
                drawSceneInfo.Shader.SetVector3("textureScale", element.Size / 2);
                drawSceneInfo.Shader.SetVector4("color", Colors.White);

                var scale = new Vector3(element.Width / 2, element.Height / 2, element.Length / 2);

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
                        drawSceneInfo.Shader.SetVector4("color", Colors.Green);
                        drawSceneInfo.Shader.SetBool("hasTexture", false);
                    }
                    else
                        drawSceneInfo.Shader.SetBool("hasTexture", true);
                    
                    mesh.TextureData.Texture?.Use(TextureUnit.Texture0);

                    if (drawObjectInfo.IndicesLength != 0)
                        GL.DrawElements(BeginMode.Triangles, drawObjectInfo.IndicesLength, DrawElementsType.UnsignedInt,
                            0);
                    else
                        GL.DrawArrays(PrimitiveType.Triangles, 0, drawObjectInfo.VerticesLength);

                    if (showNormals)
                    {
                        for (int k = 0; k < mesh.Faces.Count; k++, normalsCount += 2)
                        {
                            for (int l = 0; l < mesh.Faces[k].NormalsIndices.Length; l++)
                            {
                                normals[normalsCount] = mesh.Normals[mesh.Faces[k].NormalsIndices[l]] * scale;
                                normals[normalsCount + 1] =
                                    normals[normalsCount] + mesh.Normals[mesh.Faces[k].NormalsIndices[l]];
                            }
                        }
                    }

                    if (!faceCounting)
                        continue;

                    DrawFaceNumber(mesh, element, camera);

                    drawSceneInfo.Shader.Use();
                }

                if (!showNormals)
                    continue;

                drawSceneInfo.Shader.SetVector4("color", Colors.Red);
                drawSceneInfo.Shader.SetBool("useOnlyColor", true);
                drawSceneInfo.Shader.SetMatrix4("model", element.Transform.ClearScale());

                PrepareNormalToDraw(normals, normalsCount);

                GL.LineWidth(5);

                GL.DrawArrays(PrimitiveType.Lines, 0, normalsCount);

                GL.LineWidth(1);

                drawSceneInfo.Shader.SetBool("useOnlyColor", false);
            }

            ArrayPool<Vector3>.Shared.Return(normals);
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

    private static void DrawFaceNumber(Mesh originalMesh, ITransformable element, Camera camera)
    {
        var textDrawInfo = new TextDrawInformation
        {
            Color = Colors.White,
            OriginPosition = element.Position,
            OriginRotation = element.Direction,
            Scale = 0.1f
        };

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

                TextRenderer.DrawText3D(DefaultFont, i.ToString(), camera, textDrawInfo);
            }
        }
    }
    
    private static unsafe DrawObjectInfo CreateDrawMeshInfo(Mesh mesh, Shader shader)
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
        GL.BufferData(BufferTarget.ArrayBuffer, verticesLength * sizeof(Vertex), vertexRentArr,
            BufferUsageHint.StaticDraw);

        ArrayPool<Vertex>.Shared.Return(vertexRentArr);

        vao = GL.GenVertexArray();
        GL.BindVertexArray(vao);

        var posIndex = shader.GetAttribLocation("iPos");
        GL.VertexAttribPointer(posIndex, 3, VertexAttribPointerType.Float, false, sizeof(Vertex), 0);
        GL.EnableVertexAttribArray(posIndex);

        var textureIndex = shader.GetAttribLocation("iTextureCoord");
        GL.VertexAttribPointer(textureIndex, 2, VertexAttribPointerType.Float, false, sizeof(Vertex), sizeof(Vector3));
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

    private static unsafe DrawObjectInfo CreateDrawNormalInfo(Shader shader)
    {
        int vao = 0, vbo = 0, ebo = 0;

        vbo = GL.GenBuffer();
        GL.BindBuffer(BufferTarget.ArrayBuffer, vbo);
        GL.BufferData(BufferTarget.ArrayBuffer, 1000 * sizeof(Vector3), (Vector3[]?)null,
            BufferUsageHint.StreamDraw);

        vao = GL.GenVertexArray();
        GL.BindVertexArray(vao);

        var posIndex = shader.GetAttribLocation("iPos");
        GL.VertexAttribPointer(posIndex, 3, VertexAttribPointerType.Float, false, sizeof(Vector3), 0);
        GL.EnableVertexAttribArray(posIndex);

        return new DrawObjectInfo(vao, vbo, ebo);
    }

    private static unsafe void PrepareNormalToDraw(Vector3[] vertices, int drawAmount)
    {
        GL.BindVertexArray(s_normalObjInfo.VertexArrayObject);

        GL.BindBuffer(BufferTarget.ArrayBuffer, s_normalObjInfo.VertexBufferObject);
        GL.BufferSubData(BufferTarget.ArrayBuffer, IntPtr.Zero, drawAmount * sizeof(Vector3), vertices);
    }

    private static unsafe DrawObjectInfo CreateSkyBoxDrawMeshInfo(Vector3[] vertices, Shader shader)
    {
        int vao = 0, vbo = 0, ebo = 0;

        vbo = GL.GenBuffer();
        GL.BindBuffer(BufferTarget.ArrayBuffer, vbo);
        GL.BufferData(BufferTarget.ArrayBuffer, vertices.Length * 3 * sizeof(Vector3), vertices,
            BufferUsageHint.StaticDraw);

        vao = GL.GenVertexArray();
        GL.BindVertexArray(vao);

        var posIndex = shader.GetAttribLocation("iPos");
        GL.VertexAttribPointer(posIndex, 3, VertexAttribPointerType.Float, false, sizeof(Vector3), 0);
        GL.EnableVertexAttribArray(posIndex);

        return new DrawObjectInfo(vao, vbo, ebo);
    }
}


// private static unsafe void FillDrawSceneInfo(Shader shader, DrawSceneInfo drawSceneInfo, IDrawable drawable)
// {
//     int vao = 0, vbo = 0, ebo = 0, texturesVBO = 0;
//
//     vbo = GL.GenBuffer();
//     GL.BindBuffer(BufferTarget.ArrayBuffer, vbo);
//     GL.BufferData(BufferTarget.ArrayBuffer, drawable.Scene.Vertices.Length * sizeof(Vector3),
//         drawable.Scene.Vertices,
//         BufferUsageHint.StaticDraw);
//
//     vao = GL.GenVertexArray();
//     GL.BindVertexArray(vao);
//
//     var posIndex = shader.GetAttribLocation("iPos");
//     GL.EnableVertexAttribArray(posIndex);
//     GL.VertexAttribPointer(posIndex, 3, VertexAttribPointerType.Float, false, sizeof(Vector3), 0);
//
//     ebo = GL.GenBuffer();
//     GL.BindBuffer(BufferTarget.ElementArrayBuffer, ebo);
//     GL.BufferData(BufferTarget.ElementArrayBuffer, drawable.Scene.Indices.Length * sizeof(uint),
//         drawable.Scene.Indices.ToArray(), BufferUsageHint.StaticDraw);
//
//     var textureCoordsLength = drawable.Scene.Meshes.Sum(m => m.TextureCoords.Length);
//     var textureCoords = new Vector2[textureCoordsLength];
//
//     for (int i = 0; i < drawable.Scene.Meshes.Count; i++)
//     for (int j = 0; j < drawable.Scene.Meshes[i].TextureCoords.Length; j++)
//         textureCoords[i * drawable.Scene.Meshes[i].TextureCoords.Length + j] = drawable.Scene.Meshes[i].TextureCoords[j];
//
//     texturesVBO = GL.GenBuffer();
//     GL.BindBuffer(BufferTarget.ArrayBuffer, texturesVBO);
//     GL.BufferData(BufferTarget.ArrayBuffer, textureCoords.Length * sizeof(Vector2), textureCoords, BufferUsageHint.StaticDraw);
//
//     var textureIndex = shader.GetAttribLocation("iTextureCoord");
//     
//     GL.EnableVertexAttribArray(textureIndex);
//     GL.BindBuffer(BufferTarget.ArrayBuffer, texturesVBO);
//     
//     GL.VertexAttribPointer(textureIndex, 8, VertexAttribPointerType.Float, false, 4 * sizeof(Vector2), 0);
//     GL.BindBuffer(BufferTarget.ArrayBuffer, 0);
//     GL.VertexAttribDivisor(textureIndex, 1);
//
//     drawSceneInfo.DrawObjectInfo = new DrawObjectInfo(vao, vbo, ebo);
//     drawSceneInfo.TexturesVBO = texturesVBO;
// }