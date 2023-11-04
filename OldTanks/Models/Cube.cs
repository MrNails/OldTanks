using CoolEngine.GraphicalEngine.Core;
using CoolEngine.PhysicEngine.Core.Collision;
using CoolEngine.Services;
using OpenTK.Mathematics;
using CollisionMesh = CoolEngine.PhysicEngine.Core.Mesh;

namespace OldTanks.Models;

public class Cube : WorldObject
{
    static Cube()
    {
        var collisionData = new CollisionData(CollisionType.Polygon);
        
        Vector3[] collisionVertices =
        {
            new Vector3(-1.0f,    1.0f,   -1.0f),
            new Vector3(1.0f,    1.0f,   -1.0f),
            new Vector3(1.0f,   -1.0f,   -1.0f),
            new Vector3(-1.0f,   -1.0f,   -1.0f),
            new Vector3(1.0f,    1.0f,    1.0f),
            new Vector3(-1.0f,    1.0f,    1.0f),
            new Vector3(-1.0f,   -1.0f,    1.0f),
            new Vector3(1.0f,   -1.0f,    1.0f),
        };

        uint[] frontCollisionIndices = { 0, 1, 2, 3 };
        uint[] backCollisionIndices = { 4, 5, 6, 7 };
        uint[] leftCollisionIndices = { 0, 3, 4, 7 };
        uint[] rightCollisionIndices = { 1, 2, 5, 6 };
        uint[] topCollisionIndices = { 0, 5, 1, 4 };
        uint[] downCollisionIndices = { 3, 6, 2, 7 };

        collisionData.Meshes.Add(new CollisionMesh(frontCollisionIndices) { Normal = new Vector3(0, 0, -1)});
        collisionData.Meshes.Add(new CollisionMesh(backCollisionIndices) { Normal = new Vector3(0, 0, 1)});
        collisionData.Meshes.Add(new CollisionMesh(topCollisionIndices) { Normal = new Vector3(0, 1, 0)});
        collisionData.Meshes.Add(new CollisionMesh(downCollisionIndices) { Normal = new Vector3(0, -1, 0)});
        collisionData.Meshes.Add(new CollisionMesh(rightCollisionIndices) { Normal = new Vector3(1, 0, 0)});
        collisionData.Meshes.Add(new CollisionMesh(leftCollisionIndices) { Normal = new Vector3(-1, 0, 0)});
        collisionData.Vertices = collisionVertices;
        
        GlobalCache<CollisionData>.Default.AddOrUpdateItem("CubeCollision", collisionData);
    }
    
    public Cube() : base(GlobalCache<Scene>.Default.GetItemOrDefault("Cube_Cube.005")?.Copy())
    {
        
    }
}