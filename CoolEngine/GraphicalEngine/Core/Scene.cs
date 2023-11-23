using OpenTK.Mathematics;

namespace CoolEngine.GraphicalEngine.Core;

public class Scene
{
    public List<Mesh> Meshes { get; } = new();

    public Vector3 Center { get; private set; }
    
    public void EvaluateSceneCenter()
    {
        Vector3 min = new Vector3(float.MaxValue), max = new Vector3(float.MinValue);

        for (int i = 0; i < Meshes.Count; i++)
        {
            var vertices = Meshes[i].Vertices;
            
            for (int j = 0; j < Meshes[i].Vertices.Length; j++)
            {
                if (min.X > vertices[j].X) min.X = vertices[j].X;
                if (min.Y > vertices[j].Y) min.Y = vertices[j].Y;
                if (min.Z > vertices[j].Z) min.Z = vertices[j].Z;
            
                if (max.X < vertices[j].X) min.X = vertices[j].X;
                if (max.Y < vertices[j].Y) min.Y = vertices[j].Y;
                if (max.Z < vertices[j].Z) min.Z = vertices[j].Z;
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

        foreach (var mesh in Meshes)
            newScene.Meshes.Add(mesh.Copy());

        return newScene;
    }
}