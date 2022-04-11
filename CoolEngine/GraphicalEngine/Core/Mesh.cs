using CoolEngine.GraphicalEngine.Core.Primitives;
using CoolEngine.GraphicalEngine.Core.Texture;
using CoolEngine.Services.Interfaces;
using OpenTK.Mathematics;
using SixLabors.ImageSharp.PixelFormats;

namespace CoolEngine.GraphicalEngine.Core;

public struct VertexTextureIndices
{
    public uint VertexIndex;
    public uint TextureIndex;
}

public class Mesh
{
    private readonly List<Face> m_faces;

    public Mesh(Vector3[] vertices)
    {
        Vertices = vertices;

        TextureData = new TextureData();

        Texture = Core.Texture.Texture.Empty;

        m_faces = new List<Face>();
    }
    
    public Vector3[] Vertices { get; set; }

    public Vector2[] TextureCoords { get; set; }

    public Vector3[] Normals { get; set; }

    public List<Face> Faces => m_faces;

    public bool HasTextureCoords => TextureCoords.Length > 0;
    public bool HasNormals => Normals.Length > 0;

    public TextureData TextureData { get; }
    
    public Texture.Texture Texture { get; set; }

    public Mesh Copy()
    {
        var mesh = new Mesh(Vertices)
        {
            Normals = Normals,
            TextureCoords = TextureCoords,
            Texture = Texture
        };

        mesh.TextureData.Texture = TextureData.Texture;

        for (int i = 0; i < Faces.Count; i++)
            mesh.Faces.Add(new Face(Faces[i].Indices, Faces[i].TextureIndices, Faces[i].NormalsIndices));

        return mesh;
    }
}