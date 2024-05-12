using Common.Infrastructure.Delegates;
using CoolEngine.Services.Interfaces;

namespace CoolEngine.GraphicalEngine.Core.Texture;

public sealed class TexturedObjectInfo
{
    public readonly record struct TextureChangedArg(IDrawable Drawable, Mesh Mesh, Texture OldTexture, Texture NewTexture, TextureData TextureData);

    public EventHandler<TexturedObjectInfo, TextureChangedArg>? TextureChanged;
    
    public TexturedObjectInfo(IDrawable drawable)
    {
        Drawable = drawable;
    }
    
    /// <summary>
    /// Key - Texture.Id, Value = TextureData
    /// </summary>
    public Dictionary<Mesh, TextureData> TexturedMeshes { get; } = new();

    public IDrawable Drawable { get; }
    
    public TextureData this[Mesh mesh]
    {
        get => TexturedMeshes[mesh];
        set => TexturedMeshes[mesh] = value;
    }

    public void ChangeTexture(Mesh mesh, Texture newTexture)
    {
        if (!TexturedMeshes.TryGetValue(mesh, out var textureData) ||
            textureData.Texture == newTexture)
            return;

        var old = textureData.Texture;
        textureData.Texture = newTexture;
        TextureChanged?.Invoke(this, new TextureChangedArg(Drawable, mesh, old, newTexture, textureData));
    }

    public void Clear()
    {
        TexturedMeshes.Clear();
    }
}