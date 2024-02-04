using CoolEngine.Services.Extensions;

namespace CoolEngine.GraphicalEngine.Core;

public enum FaceType : byte
{
    Unknown,
    Dot,
    Line,
    Triangle,
    Quad
}

public sealed class Face
{
    public Face() : this(Array.Empty<uint>(), Array.Empty<uint>(), Array.Empty<uint>()) {}

    public Face(uint[] indices, uint[] textureIndices, uint[] normalsIndices)
    {
        Indices = indices ?? throw new ArgumentNullException(nameof(indices));
        TextureIndices = textureIndices ?? throw new ArgumentNullException(nameof(textureIndices));
        NormalsIndices = normalsIndices ?? throw new ArgumentNullException(nameof(normalsIndices));
    }
    
    public uint[] Indices { get; }
    public uint[] TextureIndices { get; }
    public uint[] NormalsIndices { get; }

    public FaceType FaceType => Indices.Length.GetFaceType();

    public bool HasTextureIndices => TextureIndices.Length > 0;
    public bool HasNormalIndices => NormalsIndices.Length > 0;
}