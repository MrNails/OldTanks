using CoolEngine.GraphicalEngine.Core.Texture;
using OpenTK.Mathematics;

namespace CoolEngine.GraphicalEngine.Core;

public sealed class Mesh
{
    public Mesh(Vector3[] vertices, Face[] faces) : this(vertices, Array.Empty<Vector2>(), Array.Empty<Vector3>(), faces) { }
    
    public Mesh(Vector3[] vertices, Vector2[] textureCoords, Vector3[] normals, Face[] faces)
    {
        Vertices = vertices ?? throw new ArgumentNullException(nameof(vertices));;
        TextureCoords = textureCoords ?? throw new ArgumentNullException(nameof(textureCoords));;
        Normals = normals ?? throw new ArgumentNullException(nameof(normals));;
        Faces = faces ?? throw new ArgumentNullException(nameof(faces));
    }
    
    public Face[] Faces { get; }
    
    public Vector3[] Vertices { get; }

    public Vector2[] TextureCoords { get; }

    public Vector3[] Normals { get; }

    public FaceType FaceType => Faces.FirstOrDefault()?.FaceType ?? FaceType.Unknown;

    public bool HasTextureCoords => TextureCoords.Length > 0;
    public bool HasNormals => Normals.Length > 0;
    
    public Mesh Copy()
    {
        return new Mesh(Vertices.ToArray(), TextureCoords.ToArray(), Normals.ToArray(), Faces.ToArray());
    }
}