using System.Buffers;
using CoolEngine.GraphicalEngine.Core;
using CoolEngine.GraphicalEngine.Core.Font;
using CoolEngine.Services.Interfaces;
using CoolEngine.Services.Misc;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;

namespace CoolEngine.Services.Renderers;

public class CollisionRenderer
{
    private static readonly Dictionary<Type, CollisionRenderGroup> m_drawCollisions = new();
    private static readonly Dictionary<ICollisionable, DrawSceneInfo> m_collisionInfo = new();

    private static readonly Font DefaultFont = new Font("Arial", 32);

    private static DrawObjectInfo s_normalObjInfo;

    public static Shader? Shader { get; set; }

    public static void AddCollision(ICollisionable collisionable)
    {
        if (collisionable == null || collisionable.Collision == null)
            return;

        CollisionRenderGroup collisionRenderGroup;
        var collisionType = collisionable.GetType();

        if (!m_drawCollisions.TryGetValue(collisionType, out collisionRenderGroup))
        {
            collisionRenderGroup = new CollisionRenderGroup(Shader);
            m_drawCollisions.Add(collisionType, collisionRenderGroup);
        }

        collisionRenderGroup.Add(collisionable);
    }

    public static void AddCollisions<T>(List<T> collisionables)
        where T : ICollisionable
    {
        if (collisionables == null)
            return;

        foreach (var collisionable in collisionables)
            AddCollision(collisionable);
    }

    public static void DrawElementsCollision(Camera camera, int lineWidth = 3,
        bool useLookAt = true, bool drawVerticesPositions = true, bool drawNormals = true)
    {
        if (Shader == null)
        {
            Console.WriteLine("Cannot find collision shader for drawing collision.");
            return;
        }

        if (s_normalObjInfo == null)
            s_normalObjInfo = CreateDrawNormalInfo(Shader);

        Shader.Use();
        Shader.SetMatrix4("projection", GlobalSettings.Projection);
        Shader.SetMatrix4("view", useLookAt ? camera.LookAt : Matrix4.Identity);
        Shader.SetVector4("color", Colors.Orange);

        var textDrawInfo = new TextDrawInformation
        {
            Color = Colors.Orange,
            Scale = 0.01f
        };

        foreach (var elementPair in m_drawCollisions)
        {
            var element = elementPair.Value;

            element.UpdateCollisionablesData();

            GL.BindVertexArray(element.DrawObjectInfo.VertexArrayObject);

            PrepareCollisionToDraw(element);

            GL.LineWidth(lineWidth);

            if (element.IndicesPerModel != 0)
                GL.DrawElements(BeginMode.Lines, element.IndicesPerModel * element.ActiveCount,
                    DrawElementsType.UnsignedInt, 0);
            else
                GL.DrawArrays(PrimitiveType.Lines, 0, element.VerticesPerModel * element.ActiveCount);

            if (drawVerticesPositions)
            {
                for (int j = 0; j < element.ActiveCount * element.VerticesPerModel; j++)
                {
                    var pos = element.Vertices[j];
                    textDrawInfo.SelfPosition = pos;

                    TextRenderer.DrawText3D(DefaultFont, $"{j % element.VerticesPerModel} {pos}", camera, textDrawInfo, true);
                }
            }

            Shader.Use();

            if (drawNormals)
                for (int i = 0; i < element.Collisionables.Count; i++)
                    DrawNormals(element.Collisionables[i]);
        }
    }

    public static void DrawNormals(ICollisionable collisionable)
    {
        var normals = ArrayPool<Vector3>.Shared.Rent(collisionable.Collision.CollisionData.Vertices.Length * 4);

        for (int i = 0; i < collisionable.Collision.CollisionData.Meshes.Count; i++)
        {
            var normalsCount = 0;
            var mesh = collisionable.Collision.CollisionData.Meshes[i];

            normals[normalsCount] = new Vector3(new Vector4(mesh.Normal, 1) *
                                                collisionable.Collision.CurrentObject.Transform.ClearRotation());
            normals[normalsCount + 1] = normals[normalsCount] + mesh.Normal;
            normalsCount += 2;

            Shader.Use();

            Shader.SetVector4("color", mesh.Color);

            PrepareNormalToDraw(normals, normalsCount);

            GL.LineWidth(5);

            GL.DrawArrays(PrimitiveType.Lines, 0, normalsCount);

            GL.LineWidth(1);

            mesh.Color = Colors.Orange;
        }
    }

    private static unsafe void PrepareNormalToDraw(Vector3[] vertices, int drawAmount)
    {
        GL.BindVertexArray(s_normalObjInfo.VertexArrayObject);

        GL.BindBuffer(BufferTarget.ArrayBuffer, s_normalObjInfo.VertexBufferObject);
        GL.BufferSubData(BufferTarget.ArrayBuffer, IntPtr.Zero, drawAmount * sizeof(Vector3), vertices);
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

    // public static void DrawCollision(ICollisionable collisionable, Camera camera, bool useLookAt = true)
    // {
    //     if (collisionable == null)
    //         return;
    //
    //     DrawSceneInfo drawSceneInfo;
    //
    //     if (!m_collisionInfo.TryGetValue(collisionable, out drawSceneInfo))
    //     {
    //         drawSceneInfo = new DrawSceneInfo(Shader);
    //         m_collisionInfo.Add(collisionable, drawSceneInfo);
    //     }
    //
    //     Shader.Use();
    //     Shader.SetMatrix4("projection", GlobalSettings.Projection);
    //     Shader.SetMatrix4("view", useLookAt ? camera.LookAt : Matrix4.Identity);
    //     Shader.SetVector3("color", Colors.Orange);
    //     
    //     collisionable.Collision.CurrentObject.AcceptTransform();
    //
    //     for (int i = 0; i < collisionable.Collision.CollisionData.Meshes.Count; i++)
    //     {
    //         var mesh = collisionable.Collision.CollisionData.Meshes[i];
    //         DrawObjectInfo drawObjectInfo;
    //
    //         //Find existing draw mesh info and if it don't exists - create it
    //         if (!drawSceneInfo.Buffers.TryGetValue(i, out drawObjectInfo))
    //         {
    //             drawObjectInfo = CreateCollisionDrawMeshInfo(mesh, Shader);
    //             drawSceneInfo.Buffers.Add(i, drawObjectInfo);
    //         }
    //
    //         GL.BindVertexArray(drawObjectInfo.VertexArrayObject);
    //
    //         GL.LineWidth(3);
    //         
    //         GL.DrawElements(BeginMode.Lines, mesh.Indices.Length, DrawElementsType.UnsignedInt, 0);
    //
    //         // for (int j = 0; j < mesh.Vertices.Length; j++)
    //         // {
    //         //     var pos = mesh.Vertices[j];
    //         //     TextRenderer.DrawText3D(DefaultFont, pos.ToString(), pos, Colors.Orange, default,
    //         //         0.01f, camera, true);
    //         // }
    //         //
    //         // Shader.Use();
    //     }
    // }

    private static unsafe void PrepareCollisionToDraw(CollisionRenderGroup collisionRenderGroup)
    {
        GL.BindBuffer(BufferTarget.ArrayBuffer, collisionRenderGroup.DrawObjectInfo.VertexBufferObject);
        GL.BufferSubData(BufferTarget.ArrayBuffer, IntPtr.Zero,
            collisionRenderGroup.ActiveCount * collisionRenderGroup.VerticesPerModel * sizeof(Vector3),
            collisionRenderGroup.Vertices);
    }

    // private static unsafe DrawObjectInfo CreateCollisionDrawMeshInfo(PhysicEngine.Core.Mesh mesh, Shader shader)
    // {
    //     int vao = 0, vbo = 0, ebo = 0;
    //
    //     vbo = GL.GenBuffer();
    //     GL.BindBuffer(BufferTarget.ArrayBuffer, vbo);
    //     GL.BufferData(BufferTarget.ArrayBuffer, mesh.Vertices.Length * sizeof(Vector3), mesh.Vertices,
    //         BufferUsageHint.StreamDraw);
    //
    //     vao = GL.GenVertexArray();
    //     GL.BindVertexArray(vao);
    //
    //     var posIndex = shader.GetAttribLocation("iPos");
    //     GL.VertexAttribPointer(posIndex, 3, VertexAttribPointerType.Float, false, sizeof(Vector3), 0);
    //     GL.EnableVertexAttribArray(posIndex);
    //
    //     ebo = GL.GenBuffer();
    //     GL.BindBuffer(BufferTarget.ElementArrayBuffer, ebo);
    //     GL.BufferData(BufferTarget.ElementArrayBuffer, mesh.Indices.Length * sizeof(uint), mesh.Indices,
    //         BufferUsageHint.StaticDraw);
    //
    //     return new DrawObjectInfo(vao, vbo, ebo);
    // }
}