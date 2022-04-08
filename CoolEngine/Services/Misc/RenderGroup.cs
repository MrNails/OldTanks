using System.Collections;
using System.Diagnostics.CodeAnalysis;
using CoolEngine.GraphicalEngine.Core;
using CoolEngine.GraphicalEngine.Core.Texture;

namespace CoolEngine.Services.Misc;

public class RenderGroup : IEnumerable<KeyValuePair<Texture, MeshGroup>>
{
    private readonly Dictionary<Texture, MeshGroup> m_elements;

    public RenderGroup()
    {
        m_elements = new Dictionary<Texture, MeshGroup>();
    }

    public ICollection<Texture> TexturesHandles => m_elements.Keys;

    public ICollection<MeshGroup> TextureMeshes => m_elements.Values;

    public MeshGroup this[Texture key]
    {
        get => m_elements[key];
        set => m_elements[key] = value;
    }

    public IEnumerator<KeyValuePair<Texture, MeshGroup>> GetEnumerator() => m_elements.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    public void Add(Mesh mesh)
    {
        MeshGroup? meshes;
        if (!m_elements.TryGetValue(mesh.TextureData.Texture, out meshes))
        {
            meshes = new MeshGroup();
            m_elements.Add(mesh.TextureData.Texture, meshes);
        }

        mesh.TextureChanging += TextureChanged;
        meshes.Add(mesh);
    }

    public void Add(List<Mesh> meshes)
    {
        for (int i = 0; i < meshes.Count; i++)
        {
            var mesh = meshes[i];

            Add(mesh);
        }
    }

    public bool Remove(Mesh mesh)
    {
        MeshGroup? meshes;
        if (!m_elements.TryGetValue(mesh.TextureData.Texture, out meshes))
            return false;
        
        mesh.TextureChanging -= TextureChanged;
        
        return meshes.Remove(mesh);
    }

    public void Remove(List<Mesh> meshes)
    {
        for (int i = 0; i < meshes.Count; i++)
        {
            var mesh = meshes[i];
            MeshGroup? _meshes;

            if (m_elements.TryGetValue(mesh.TextureData.Texture, out _meshes))
                _meshes.Remove(mesh);
        }
    }

    public bool Contains(Mesh mesh)
    {
        MeshGroup? meshes;
        if (!m_elements.TryGetValue(mesh.TextureData.Texture, out meshes))
            return false;

        return meshes.Contains(mesh);
    }

    public bool ContainsTexture(Texture texture) => m_elements.ContainsKey(texture);

    public bool TryGetValue(Texture texture, out MeshGroup value) => m_elements.TryGetValue(texture, out value);

    public void Clear() => m_elements.Clear();
    
    private void TextureChanged(Texture old, Mesh source)
    {
        m_elements[old].Remove(source);
        
        MeshGroup? meshes;
        if (!m_elements.TryGetValue(source.TextureData.Texture, out meshes))
        {
            meshes = new MeshGroup();
            m_elements.Add(source.TextureData.Texture, meshes);
        }
        
        meshes.Add(source);
    }
}

// public class RenderGroupComparer : IEqualityComparer<Mesh?>
// {
//     public static readonly RenderGroupComparer Default = new RenderGroupComparer();
//
//     public bool Equals(Mesh? x, Mesh? y)
//     {
//         if (ReferenceEquals(x, y)) return true;
//         if (ReferenceEquals(x, null)) return false;
//         if (ReferenceEquals(y, null)) return false;
//         // if (x.GetType() != y.GetType()) return false;
//         return x == y;
//     }
//
//     public int GetHashCode(Mesh obj)
//     {
//         return HashCode.Combine(obj.Normal, obj.Texture, obj.Vertices, obj.Indices);
//     }
// }