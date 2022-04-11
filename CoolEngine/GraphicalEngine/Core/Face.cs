using OpenTK.Mathematics;

namespace CoolEngine.GraphicalEngine.Core;

public class Face
{
    public Face() : this(Array.Empty<uint>(), Array.Empty<uint>(), Array.Empty<uint>()) {}

    public Face(uint[] indices, uint[] textureIndices, uint[] normalsIndices)
    {
        Indices = indices ?? throw new ArgumentNullException(nameof(indices));
        TextureIndices = textureIndices ?? throw new ArgumentNullException(nameof(textureIndices));
        NormalsIndices = normalsIndices ?? throw new ArgumentNullException(nameof(normalsIndices));
    }
    
    public uint[] Indices { get; set; }
    public uint[] TextureIndices { get; set; }
    public uint[] NormalsIndices { get; set; }

    public bool HasTextureIndices => TextureIndices.Length > 0;
    public bool HasNormalIndices => NormalsIndices.Length > 0;
}