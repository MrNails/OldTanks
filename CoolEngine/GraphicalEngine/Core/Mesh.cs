using CoolEngine.Core.Primitives;
using CoolEngine.GraphicalEngine.Core.Texture;
using CoolEngine.Services.Interfaces;
using OpenTK.Mathematics;

namespace CoolEngine.GraphicalEngine.Core;

public delegate void TextureDelegate(Texture.Texture old, Mesh source);

public class Mesh
{
    private int m_meshId;
    private Vertex[] m_vertices;
    private uint[] m_indices;

    public event TextureDelegate? TextureChanging;

    public Mesh(int meshId) : this(meshId, Array.Empty<Vertex>(), Array.Empty<uint>())
    {
    }

    public Mesh(int meshId, Vertex[] vertices, uint[] indices)
    {
        Vertices = vertices;
        Indices = indices;
        m_meshId = meshId;

        TextureData = new TextureData();
    }

    /// <summary>
    /// Represent mesh id related to specified scene.
    /// </summary>
    public int MeshId => m_meshId;

    public Vertex[] Vertices
    {
        get => m_vertices;
        set
        {
            if (value == null)
                throw new ArgumentNullException(nameof(value));

            m_vertices = value;
        }
    }

    public uint[] Indices
    {
        get => m_indices;
        set
        {
            if (value == null)
                throw new ArgumentNullException(nameof(value));

            m_indices = value;
        }
    }

    public Vector3 Normal { get; set; }

    public TextureData TextureData { get; }

    public IDrawable Drawable { get; set; }
    
    public Mesh Copy()
    {
        var mesh = new Mesh(MeshId)
        {
            Vertices = m_vertices,
            Indices = m_indices,
            Normal = Normal,
            Drawable = Drawable
        };

        mesh.TextureData.Texture = TextureData.Texture;

        return mesh;
    }

    public static bool operator ==(Mesh left, Mesh right)
    {
        return left.Vertices == right.Vertices &&
               left.Indices == right.Indices &&
               left.TextureData.Texture?.Handle == right.TextureData.Texture?.Handle &&
               left.Normal == right.Normal;
    }

    public static bool operator !=(Mesh left, Mesh right)
    {
        return !(left == right);
    }
}