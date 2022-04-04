using CoolEngine.Core.Primitives;
using OpenTK.Mathematics;

namespace CoolEngine.GraphicalEngine.Core;

public class Scene
{
    private List<Mesh> m_meshes;

    public Scene()
    {
        m_meshes = new List<Mesh>();
    }

    public List<Mesh> Meshes => m_meshes;

    public Vector3 Center { get; private set; }
    
    public void EvaluateSceneCenter()
    {
        Vector3 min = new Vector3(float.MaxValue), max = new Vector3(float.MinValue);

        for (int i = 0; i < Meshes.Count; i++)
        {
            for (int j = 0; j < Meshes[i].Vertices.Length; j++)
            {
                if (min.X > Meshes[i].Vertices[j].X) min.X = Meshes[i].Vertices[j].X;
                if (min.Y > Meshes[i].Vertices[j].Y) min.Y = Meshes[i].Vertices[j].Y;
                if (min.Z > Meshes[i].Vertices[j].Z) min.Z = Meshes[i].Vertices[j].Z;
            
                if (max.X < Meshes[i].Vertices[j].X) min.X = Meshes[i].Vertices[j].X;
                if (max.Y < Meshes[i].Vertices[j].Y) min.Y = Meshes[i].Vertices[j].Y;
                if (max.Z < Meshes[i].Vertices[j].Z) min.Z = Meshes[i].Vertices[j].Z;
            }
        }


        Center = (min + max) / 2.0f;
    }

    public Scene Copy()
    {
        var newScene = new Scene
        {
            Center = Center
        };

        foreach (var mesh in m_meshes)
            newScene.Meshes.Add(mesh.Copy());

        return newScene;
    }
}