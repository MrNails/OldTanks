// using System.Collections;
// using CoolEngine.GraphicalEngine.Core;
//
// namespace CoolEngine.Services.Misc;
//
// public class MeshGroup : IEnumerable<List<Mesh>>
// {
//     private readonly Dictionary<int, List<Mesh>> m_elements;
//
//     public MeshGroup()
//     {
//         m_elements = new Dictionary<int, List<Mesh>>();
//     }
//
//     public ICollection<List<Mesh>> TextureMeshes => m_elements.Values;
//
//     public List<Mesh> this[int key]
//     {
//         get => m_elements[key];
//         set => m_elements[key] = value;
//     }
//
//     public IEnumerator<List<Mesh>> GetEnumerator() => TextureMeshes.GetEnumerator();
//
//     IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
//
//     public void Add(Mesh mesh)
//     {
//         List<Mesh>? meshes;
//         if (!m_elements.TryGetValue(mesh.MeshId, out meshes))
//         {
//             meshes = new List<Mesh>();
//             m_elements.Add(mesh.MeshId, meshes);
//         }
//         
//         meshes.Add(mesh);
//     }
//
//     public void Add(List<Mesh> meshes)
//     {
//         for (int i = 0; i < meshes.Count; i++)
//         {
//             var mesh = meshes[i];
//
//             Add(mesh);
//         }
//     }
//
//     public bool Remove(Mesh mesh)
//     {
//         List<Mesh>? meshes;
//         if (!m_elements.TryGetValue(mesh.MeshId, out meshes))
//             return false;
//
//         return meshes.Remove(mesh);
//     }
//
//     public void Remove(List<Mesh> meshes)
//     {
//         for (int i = 0; i < meshes.Count; i++)
//         {
//             var mesh = meshes[i];
//             List<Mesh>? _meshes;
//
//             if (m_elements.TryGetValue(mesh.MeshId, out _meshes))
//                 _meshes.Remove(mesh);
//         }
//     }
//
//     public bool Contains(Mesh mesh)
//     {
//         List<Mesh>? meshes;
//         if (!m_elements.TryGetValue(mesh.MeshId, out meshes))
//             return false;
//
//         return meshes.Contains(mesh);
//     }
//
//     public bool TryGetValue(int meshId, out List<Mesh>? value) => m_elements.TryGetValue(meshId, out value);
//
//     public void Clear() => m_elements.Clear();
// }