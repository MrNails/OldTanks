using CoolEngine.GraphicalEngine.Core;
using CoolEngine.Services.Interfaces;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;

namespace CoolEngine.Services.Misc;

internal class CollisionRenderGroup
{
    private const int DefaultRenderCapacity = 64;

    private readonly List<ICollisionable> m_collisionables;

    private readonly Shader m_shader;
    
    private DrawObjectInfo m_drawObjectInfo;

    private int m_verticesPerModel;
    private int m_indicesPerModel;

    private int m_activeCount;

    private Vector3[] m_vertices;
    private uint[] m_indices;

    public CollisionRenderGroup(Shader shader, int capacity = DefaultRenderCapacity)
    {
        if (capacity <= 0)
            throw new ArgumentOutOfRangeException(nameof(capacity),
                $"{nameof(capacity)} cannot be less or equal then 0");

        if (shader == null)
            throw new ArgumentNullException(nameof(shader));

        m_shader = shader;
        
        m_collisionables = new List<ICollisionable>(capacity);

        m_vertices = Array.Empty<Vector3>();
        m_indices = Array.Empty<uint>();
    }

    public int Count => m_collisionables.Count;
    public int ActiveCount => m_activeCount;

    public int VerticesPerModel => m_verticesPerModel;
    public int IndicesPerModel => m_indicesPerModel;

    public Vector3[] Vertices => m_vertices;
    public uint[] Indices => m_indices;

    public List<ICollisionable> Collisionables => m_collisionables;

    public Shader Shader => m_shader;
    
    public DrawObjectInfo DrawObjectInfo => m_drawObjectInfo;

    public void Add(ICollisionable collisionable) => m_collisionables.Add(collisionable);
    public bool Remove(ICollisionable collisionable) => m_collisionables.Remove(collisionable);
    public bool Contains(ICollisionable collisionable) => m_collisionables.Contains(collisionable);
    public void Clear() => m_collisionables.Clear();

    public void UpdateCollisionablesData()
    {
        if (m_vertices.Length == 0 && m_collisionables.Count != 0)
            InitRenderGroup();
        
        if (m_collisionables.Capacity * m_verticesPerModel > m_vertices.Length)
            ResizeCollisionableData();
        
        m_activeCount = 0;
        for (int i = 0; i < m_collisionables.Count; i++)
        {
            var collisionable = m_collisionables[i];

            if (!collisionable.Collision.IsActive)
                continue;
            
            collisionable.Collision.CurrentObject.AcceptTransform();

            for (int j = 0; j < collisionable.Collision.Meshes.Count; j++)
            {
                var mesh = collisionable.Collision.Meshes[j];

                for (int vIndex = 0; vIndex < mesh.Vertices.Length; vIndex++)
                    m_vertices[m_activeCount * m_verticesPerModel + j * mesh.Vertices.Length + vIndex] = mesh.Vertices[vIndex];
            }

            m_activeCount++;
        }
    }
    
    private void InitRenderGroup()
    {
        var collisionable = m_collisionables[0];

        for (int i = 0; i < collisionable.Collision.Meshes.Count; i++)
        {
            m_verticesPerModel += collisionable.Collision.Meshes[i].Vertices.Length;
            m_indicesPerModel += collisionable.Collision.Meshes[i].Indices.Length;
        }

        ResizeCollisionableData();
    }

    private void ResizeCollisionableData()
    {
        m_vertices = new Vector3[m_collisionables.Capacity * m_verticesPerModel];

        if (m_indicesPerModel != 0)
            m_indices = new uint[m_collisionables.Capacity * m_indicesPerModel];

        if (m_collisionables.Count != 0)
        {
            var collisionable = m_collisionables[0];
            for (int i = 0; i < m_collisionables.Capacity; i++)
            {
                for (int j = 0, indexOffset = 0; j < collisionable.Collision.Meshes.Count; j++)
                {
                    var mesh = collisionable.Collision.Meshes[j];

                    for (int iIndex = 0; iIndex < mesh.Indices.Length; iIndex++)
                        m_indices[i * m_indicesPerModel + j * mesh.Indices.Length + iIndex] =
                            mesh.Indices[iIndex] + (uint)(i * m_verticesPerModel + indexOffset);

                    indexOffset += mesh.Vertices.Length;
                }
            }
        }
        
        m_drawObjectInfo = CreateDrawInfo();
    }
    
    private unsafe DrawObjectInfo CreateDrawInfo()
    {
        int vao = 0, vbo = 0, ebo = 0;

        vbo = GL.GenBuffer();
        GL.BindBuffer(BufferTarget.ArrayBuffer, vbo);
        GL.BufferData(BufferTarget.ArrayBuffer, m_vertices.Length * sizeof(Vector3), (Vector3[]?)null,
            BufferUsageHint.StreamDraw);

        vao = GL.GenVertexArray();
        GL.BindVertexArray(vao);

        var posIndex = m_shader.GetAttribLocation("iPos");
        GL.VertexAttribPointer(posIndex, 3, VertexAttribPointerType.Float, false, sizeof(Vector3), 0);
        GL.EnableVertexAttribArray(posIndex);

        if (m_indices.Length != 0)
        {
            ebo = GL.GenBuffer();
            GL.BindBuffer(BufferTarget.ElementArrayBuffer, ebo);
            GL.BufferData(BufferTarget.ElementArrayBuffer, m_indices.Length * sizeof(uint), m_indices,
                BufferUsageHint.StaticDraw);
        }

        return new DrawObjectInfo(vao, vbo, ebo);
    }
}