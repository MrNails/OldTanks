using System.Collections.ObjectModel;
using CoolEngine.Core;
using CoolEngine.GraphicalEngine.Core;
using CoolEngine.Services.Interfaces;
using OpenTK.Mathematics;

namespace CoolEngine.PhysicEngine.Core;

public class Collision
{
    private readonly Scene m_originalCollision;
    private readonly List<Mesh> m_meshes;
    private readonly ReadOnlyCollection<Mesh> m_readOnlyMeshes;
    
    private IPhysicObject m_currentObj;

    public Collision(IPhysicObject physicObject, Scene originalCollision)
    {
        m_meshes = new List<Mesh>();
        m_readOnlyMeshes = new ReadOnlyCollection<Mesh>(m_meshes);
        m_currentObj = physicObject;

        if (originalCollision == null)
            throw new ArgumentNullException(nameof(originalCollision));

        m_originalCollision = originalCollision;
        
        InitCollision(originalCollision);
    }

    public ReadOnlyCollection<Mesh> Meshes => m_readOnlyMeshes;

    public IPhysicObject CurrentObject => m_currentObj;

    public bool CheckCollision(IPhysicObject t2)
    {
        return false;
    }

    public void UpdateCollision()
    {
        m_currentObj.AcceptTransform();
        var transformation = m_currentObj.Transform;

        for (int i = 0, m = 0; i < m_originalCollision.Meshes.Count && m < m_meshes.Count; i++)
        {
            var mesh = m_originalCollision.Meshes[i];
            var originalVertices = mesh.Vertices;
            
            var isTexturedMesh = mesh.Vertices.Length % 5 == 0;
            var isCommonMesh = mesh.Vertices.Length % 3 == 0;
            var stride = isTexturedMesh ? 5 : isCommonMesh ? 3 : 0;
            
            if (stride == 0)
                continue;

            var currentMesh = m_meshes[m++];

            for (int j = 0, k = 0; j < originalVertices.Length; k += 3, j += stride)
            {
                var transformResult = new Vector4(originalVertices[j], originalVertices[j + 1], originalVertices[j + 2], 1) * transformation;

                currentMesh.Vertices[k] = transformResult.X;
                currentMesh.Vertices[k + 1] = transformResult.Y;
                currentMesh.Vertices[k + 2] = transformResult.Z;
            }
                
        }
    }

    private void InitCollision(Scene originalCollision)
    {
        foreach (var mesh in originalCollision.Meshes)
        {
            var isTexturedMesh = mesh.Vertices.Length % 5 == 0;
            var isCommonMesh = mesh.Vertices.Length % 3 == 0;

            var newVertices = new float[isTexturedMesh ? mesh.Vertices.Length - (mesh.Vertices.Length / 5) * 2 :
                isCommonMesh ? mesh.Vertices.Length : 0];

            if (newVertices.Length == 0)
            {
                Console.WriteLine($"Cannot copy mesh for object {m_currentObj.GetType().FullName}. It's not fit both textured mesh and common mesh type.");
                continue;
            }

            var stride = isTexturedMesh ? 5 : 3;
            var newVerticesStride = 3;
            
            for (int i = 0, j = 0; i < mesh.Vertices.Length; i += stride, j += newVerticesStride)
            {
                newVertices[j] = mesh.Vertices[i];
                newVertices[j + 1] = mesh.Vertices[i + 1];
                newVertices[j + 2] = mesh.Vertices[i + 2];
            }
            
            m_meshes.Add(new Mesh(mesh.MeshId, newVertices, mesh.Indices) { Normal = mesh.Normal});
        }
    }
}