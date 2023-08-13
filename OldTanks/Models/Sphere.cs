using CoolEngine.GraphicalEngine.Core;
using CoolEngine.PhysicEngine.Core;
using CoolEngine.PhysicEngine.Core.Collision;
using CoolEngine.Services;
using CoolEngine.Services.Loaders;
using OpenTK.Mathematics;

using CollMesh = CoolEngine.PhysicEngine.Core.Mesh;

namespace OldTanks.Models;

public class Sphere : WorldObject
{
    public Sphere() : base(GlobalCache<Scene>.GetItemOrDefault("Sphere"))
    {
    }

    public override Vector3 Size
    {
        get => base.Size;
        set => base.Size = new Vector3(value.X);
    }

    public override float Width
    {
        get => m_size.X;
        set
        {
            base.Width = value;
            
            if (Height != value)
                Height = value;
            if (Length != value)
                Length = value;
        }
    }

    public override float Height
    {
        get => m_size.Y;
        set
        {
            base.Height = value;

            if (Width != value)
                Width = value;
            if (Length != value)
                Length = value;
        }
    }

    public override float Length
    {
        get => m_size.Z;
        set
        {
            base.Length = value;
            
            if (Width != value)
                Width = value;
            if (Height != value)
                Height = value;
        }
    }
}