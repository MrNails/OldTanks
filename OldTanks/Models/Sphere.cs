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

    static Sphere()
    {
        WaveFrontLoader waveFrontLoader = new WaveFrontLoader();
        var res = waveFrontLoader.LoadAsync(@"C:\Users\popov\Downloads\Telegram Desktop\Sphere_Edges.obj").Result;
        
        // var stride = 2;
        // var vertices = new Vector3[180 * 360 / stride];
        // var idx = 0;
        //
        // for (int zAngle = -180; zAngle < 0; zAngle += stride)
        // {
        //     var zCalcAngle = (float)Math.Cos(MathHelper.DegreesToRadians(zAngle));
        //     for (int xAngle = 0, yAngle = 0; xAngle < 360; xAngle += stride, yAngle += stride)
        //     {
        //         var zInverted = 1 - Math.Abs(zCalcAngle);
        //         vertices[idx++] = new Vector3(zInverted * (float)Math.Sin(MathHelper.DegreesToRadians(xAngle)), 
        //             (float)Math.Cos(MathHelper.DegreesToRadians(yAngle)), zCalcAngle);
        //         
        //         // vertices[idx++] = new Vector3(zInverted * (float)Math.Sin(MathHelper.DegreesToRadians(xAngle + 10)), 
        //         //     zInverted * (float)Math.Cos(MathHelper.DegreesToRadians(yAngle + 10)), zCalcAngle);
        //     }
        // }

            
            // vertices[idx++] = new Vector3((float)Math.Sin(yAngle), (float)Math.Cos(yAngle), 0);
        
        GlobalCache<CollisionData>.AddOrUpdateItem("SphereCollision", new CollisionData() { Vertices = res.Scene.Meshes[0].Vertices });
    }
}