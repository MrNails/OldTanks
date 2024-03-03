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
            new Vector3(-1.0f,    1.0f,   1.0f),
            new Vector3(1.0f,    1.0f,   1.0f),
            new Vector3(1.0f,   -1.0f,   1.0f),
            new Vector3(-1.0f,   -1.0f,   1.0f),
            new Vector3(-1.0f,    1.0f,    -1.0f),
            new Vector3(1.0f,    1.0f,    -1.0f),
            new Vector3(1.0f,   -1.0f,    -1.0f),
            new Vector3(-1.0f,   -1.0f,    -1.0f),
        };

        uint[] frontCollisionIndices = { 0, 1, 3, 2 };
        uint[] backCollisionIndices = { 5, 4, 6, 7 };
        uint[] leftCollisionIndices = { 3, 0, 7, 4 };
        uint[] rightCollisionIndices = { 6, 5, 2, 1 };
        uint[] topCollisionIndices = { 1, 5, 0, 4 };
        uint[] downCollisionIndices = { 6, 2, 7, 3 };

        collisionData.Meshes.Add(new CollisionMesh(frontCollisionIndices) { Normal = new Vector3(0, 0, 1)});
        collisionData.Meshes.Add(new CollisionMesh(backCollisionIndices) { Normal = new Vector3(0, 0, -1)});
        collisionData.Meshes.Add(new CollisionMesh(topCollisionIndices) { Normal = new Vector3(0, 1, 0)});
        collisionData.Meshes.Add(new CollisionMesh(downCollisionIndices) { Normal = new Vector3(0, -1, 0)});
        collisionData.Meshes.Add(new CollisionMesh(rightCollisionIndices) { Normal = new Vector3(1, 0, 0)});
        collisionData.Meshes.Add(new CollisionMesh(leftCollisionIndices) { Normal = new Vector3(-1, 0, 0)});
        collisionData.Vertices = collisionVertices;
        
        GlobalCache<CollisionData>.Default.AddOrUpdateItem("CubeCollision", collisionData);
    }
    
    public Cube() : base(GlobalCache<Scene>.Default.GetItemOrDefault("Cube_Cube.005"))
    {
        
    }
}