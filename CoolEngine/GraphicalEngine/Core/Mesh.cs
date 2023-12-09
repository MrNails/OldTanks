using CoolEngine.GraphicalEngine.Core.Texture;
using OpenTK.Mathematics;

namespace CoolEngine.GraphicalEngine.Core;

public sealed class Mesh
{
    public Mesh(Vector3[] vertices) : this(vertices, Array.Empty<Vector2>(), Array.Empty<Vector3>()) { }
    
    public Mesh(Vector3[] vertices, Vector2[] textureCoords, Vector3[] normals)
    {
        Vertices = vertices;
        TextureCoords = textureCoords;
        Normals = normals;

        TextureData = new TextureData();

        Faces = new List<Face>();
    }


    public List<Face> Faces { get; }
    
    public TextureData TextureData { get; }

    
    public Vector3[] Vertices { get; }

    public Vector2[] TextureCoords { get; }

    public Vector3[] Normals { get; }

    public bool HasTextureCoords => TextureCoords.Length > 0;
    public bool HasNormals => Normals.Length > 0;
    
    public Mesh Copy()
    {
        var mesh = new Mesh(Vertices, TextureCoords, Normals);
        mesh.TextureData.Texture = TextureData.Texture;

        for (int i = 0; i < Faces.Count; i++)
            mesh.Faces.Add(new Face(Faces[i].Indices, Faces[i].TextureIndices, Faces[i].NormalsIndices));

        return mesh;
    }
}