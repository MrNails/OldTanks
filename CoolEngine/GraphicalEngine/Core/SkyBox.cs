using Common.Models;
using OpenTK.Mathematics;

namespace CoolEngine.GraphicalEngine.Core;

public sealed class SkyBox : ObservableObject
{
    public static readonly Vector3[] Vertices;
    
    private Texture.Texture? m_texture;

    static SkyBox()
    {
        Vertices = new Vector3[]
        {
            new Vector3(-1.0f, 1.0f, -1.0f),
            new Vector3(-1.0f, -1.0f, -1.0f),
            new Vector3(1.0f, -1.0f, -1.0f),
            new Vector3(1.0f, -1.0f, -1.0f),
            new Vector3(1.0f, 1.0f, -1.0f),
            new Vector3(-1.0f, 1.0f, -1.0f),

            new Vector3(-1.0f, -1.0f, 1.0f),
            new Vector3(-1.0f, -1.0f, -1.0f),
            new Vector3(-1.0f, 1.0f, -1.0f),
            new Vector3(-1.0f, 1.0f, -1.0f),
            new Vector3(-1.0f, 1.0f, 1.0f),
            new Vector3(-1.0f, -1.0f, 1.0f),

            new Vector3(1.0f, -1.0f, -1.0f),
            new Vector3(1.0f, -1.0f, 1.0f),
            new Vector3(1.0f, 1.0f, 1.0f),
            new Vector3(1.0f, 1.0f, 1.0f),
            new Vector3(1.0f, 1.0f, -1.0f),
            new Vector3(1.0f, -1.0f, -1.0f),

            new Vector3(-1.0f, -1.0f, 1.0f),
            new Vector3(-1.0f, 1.0f, 1.0f),
            new Vector3(1.0f, 1.0f, 1.0f),
            new Vector3(1.0f, 1.0f, 1.0f),
            new Vector3(1.0f, -1.0f, 1.0f),
            new Vector3(-1.0f, -1.0f, 1.0f),

            new Vector3(-1.0f, 1.0f, -1.0f),
            new Vector3(1.0f, 1.0f, -1.0f),
            new Vector3(1.0f, 1.0f, 1.0f),
            new Vector3(1.0f, 1.0f, 1.0f),
            new Vector3(-1.0f, 1.0f, 1.0f),
            new Vector3(-1.0f, 1.0f, -1.0f),

            new Vector3(-1.0f, -1.0f, -1.0f),
            new Vector3(-1.0f, -1.0f, 1.0f),
            new Vector3(1.0f, -1.0f, -1.0f),
            new Vector3(1.0f, -1.0f, -1.0f),
            new Vector3(-1.0f, -1.0f, 1.0f),
            new Vector3(1.0f, -1.0f, 1.0f)
        };
    }

    public Texture.Texture? Texture
    {
        get => m_texture;
        set => SetField(ref m_texture, value);
    }
}