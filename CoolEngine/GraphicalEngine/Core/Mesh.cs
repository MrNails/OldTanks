using CoolEngine.GraphicalEngine.Core.Texture;
using OpenTK.Mathematics;

namespace CoolEngine.GraphicalEngine.Core;

public class Mesh
{
    public Mesh(Vector3[] vertices)
    {
        Vertices = vertices;
        TextureCoords = Array.Empty<Vector2>();
        Normals = Array.Empty<Vector3>();

        TextureData = new TextureData();

        Faces = new List<Face>();
    }
    
    public List<Face> Faces { get; }
    
    public TextureData TextureData { get; }

    
    public Vector3[] Vertices { get; set; }

    public Vector2[] TextureCoords { get; set; }

    public Vector3[] Normals { get; set; }

    public bool HasTextureCoords => TextureCoords.Length > 0;
    public bool HasNormals => Normals.Length > 0;
    
    public Mesh Copy()
    {
        var mesh = new Mesh(Vertices)
        {
            Normals = Normals,
            TextureCoords = TextureCoords
        };

        mesh.TextureData.Texture = TextureData.Texture;

        for (int i = 0; i < Faces.Count; i++)
            mesh.Faces.Add(new Face(Faces[i].Indices, Faces[i].TextureIndices, Faces[i].NormalsIndices));

        return mesh;
    }
}