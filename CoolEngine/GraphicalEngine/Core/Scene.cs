using OpenTK.Mathematics;

namespace CoolEngine.GraphicalEngine.Core;

public sealed class Scene
{
    public Scene(Mesh[] meshes)
    {
        Meshes = meshes;
        EvaluateSceneCenter();
    }
    
    public Mesh[] Meshes { get; }

    public Vector3 Center { get; private set; }
    
    public Scene Copy()
    {
        var copiedMeshes = new Mesh[Meshes.Length];

        for (int i = 0; i < Meshes.Length; i++)
            copiedMeshes[i] = Meshes[i].Copy();

        return new Scene(copiedMeshes);
    }
    
    private void EvaluateSceneCenter()
    {
        var min = new Vector3(float.MaxValue);
        var max = new Vector3(float.MinValue);

        for (int i = 0; i < Meshes.Length; i++)
        {
            var vertices = Meshes[i].Vertices;
            
            for (int j = 0; j < vertices.Length; j++)
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
}