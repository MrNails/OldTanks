using Common.Extensions;
using CoolEngine.GraphicalEngine.Core;
using CoolEngine.PhysicEngine.Core.Collision;
using CoolEngine.Services;
using OldTanks.Infrastructure;
using OpenTK.Mathematics;

namespace OldTanks.Models;

public class Sphere : WorldObject
{
    static Sphere()
    {
        GlobalCache<CollisionData>.Default.AddOrUpdateItem(CollisionConstants.SphereCollisionName, new CollisionData(CollisionType.Sphere));
    }
    
    public Sphere() : base(GlobalCache<Scene>.Default.GetItemOrDefault("Sphere"))
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
            
            if (!Height.ApproximateEqual(value))
                Height = value;
            if (!Length.ApproximateEqual(value))
                Length = value;
        }
    }

    public override float Height
    {
        get => m_size.Y;
        set
        {
            base.Height = value;

            if (!Width.ApproximateEqual(value))
                Width = value;
            if (!Length.ApproximateEqual(value))
                Length = value;
        }
    }

    public override float Length
    {
        get => m_size.Z;
        set
        {
            base.Length = value;
            
            if (!Width.ApproximateEqual(value))
                Width = value;
            if (!Height.ApproximateEqual(value))
                Height = value;
        }
    }
}