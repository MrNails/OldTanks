using CoolEngine.Core.Primitives;
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
        CreateCubeScene();
    }
    
    public Cube() : base(GlobalCache<Scene>.GetItemOrDefault("CubeScene")?.Copy())
    {
        
    }

    private static void CreateCubeScene()
    {
        var scene = new Scene();
        var collisionData = new CollisionData();

        //x, y, z, u, v
        Vertex[] topSide =
        {
            new Vertex(1.0f,    1.0f,    1.0f,    1.0f,    1.0f, 0),
            new Vertex(1.0f,    1.0f,   -1.0f,    1.0f,    0.0f, 0),
            new Vertex(-1.0f,    1.0f,   -1.0f,    0.0f,    0.0f, 0),
            new Vertex(-1.0f,    1.0f,    1.0f,    0.0f,    1.0f, 0)
        };
        
        Vertex[] downSide =
        {
            new Vertex(-1.0f,   -1.0f,   -1.0f,    1.0f,    0.0f, 0),
            new Vertex(1.0f,   -1.0f,   -1.0f,    0.0f,    0.0f, 0),
            new Vertex(1.0f,   -1.0f,    1.0f,    0.0f,    1.0f, 0),
            new Vertex(-1.0f,   -1.0f,    1.0f,    1.0f,    1.0f, 0)
        };
        
        Vertex[] backSide =
        {
            new Vertex(-1.0f,   -1.0f,    1.0f,    1.0f,    0.0f, 0),
            new Vertex(1.0f,   -1.0f,    1.0f,    0.0f,    0.0f, 0),
            new Vertex(1.0f,    1.0f,    1.0f,    0.0f,    1.0f, 0),
            new Vertex(-1.0f,    1.0f,    1.0f,    1.0f,    1.0f, 0)
        };
        
        Vertex[] frontSide =
        { 
           new Vertex(1.0f,    1.0f,   -1.0f,    1.0f,    1.0f, 0), 
           new Vertex(1.0f,   -1.0f,   -1.0f,    1.0f,    0.0f, 0),
           new Vertex(-1.0f,   -1.0f,   -1.0f,    0.0f,    0.0f, 0),
           new Vertex(-1.0f,    1.0f,   -1.0f,    0.0f,    1.0f, 0),
        };

        Vertex[] rightSide =
        {
            new Vertex(1.0f,    1.0f,    1.0f,    1.0f,    1.0f, 0),
            new Vertex(1.0f,   -1.0f,    1.0f,    1.0f,    0.0f, 0),
            new Vertex(1.0f,   -1.0f,   -1.0f,    0.0f,    0.0f, 0),
            new Vertex(1.0f,    1.0f,   -1.0f,    0.0f,    1.0f, 0)
        };
        
        Vertex[] leftSide =
        { 
           new Vertex(-1.0f,    1.0f,    1.0f,    1.0f,    0.0f, 0),
           new Vertex(-1.0f,    1.0f,   -1.0f,    0.0f,    0.0f, 0),
           new Vertex(-1.0f,   -1.0f,   -1.0f,    0.0f,    1.0f, 0),
           new Vertex(-1.0f,   -1.0f,    1.0f,    1.0f,    1.0f, 0)
        }; 

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

        uint[] upDownSideIndices =
        {
            0, 1, 3,
            1, 2, 3
        };
        
        uint[] frontCollisionIndices = { 0, 1, 2, 3 };
        uint[] backCollisionIndices = { 4, 5, 6, 7 };
        uint[] leftCollisionIndices = { 0, 3, 4, 7 };
        uint[] rightCollisionIndices = { 1, 2, 5, 6 };
        uint[] topCollisionIndices = { 0, 5, 1, 4 };
        uint[] downCollisionIndices = { 3, 6, 2, 7 };

        scene.Meshes.Add(new Mesh(0, backSide, upDownSideIndices) { Normal = new Vector3(0, 0, 1)});
        scene.Meshes.Add(new Mesh(1, frontSide, upDownSideIndices) { Normal = new Vector3(0, 0, -1)});
        scene.Meshes.Add(new Mesh(2, topSide, upDownSideIndices) { Normal = new Vector3(0, 1, 0)});
        scene.Meshes.Add(new Mesh(3, downSide, upDownSideIndices) { Normal = new Vector3(0, -1, 0)});
        scene.Meshes.Add(new Mesh(4, rightSide, upDownSideIndices) { Normal = new Vector3(1, 0, 0)});
        scene.Meshes.Add(new Mesh(5, leftSide, upDownSideIndices) { Normal = new Vector3(-1, 0, 0)});
        
        collisionData.Meshes.Add(new CollisionMesh(frontCollisionIndices) { Normal = new Vector3(0, 0, 1)});
        collisionData.Meshes.Add(new CollisionMesh(backCollisionIndices) { Normal = new Vector3(0, 0, -1)});
        collisionData.Meshes.Add(new CollisionMesh(topCollisionIndices) { Normal = new Vector3(0, 1, 0)});
        collisionData.Meshes.Add(new CollisionMesh(downCollisionIndices) { Normal = new Vector3(0, -1, 0)});
        collisionData.Meshes.Add(new CollisionMesh(rightCollisionIndices) { Normal = new Vector3(1, 0, 0)});
        collisionData.Meshes.Add(new CollisionMesh(leftCollisionIndices) { Normal = new Vector3(-1, 0, 0)});
        collisionData.Vertices = collisionVertices;

        GlobalCache<Scene>.AddOrUpdateItem("CubeScene", scene);
        GlobalCache<CollisionData>.AddOrUpdateItem("CubeCollision", collisionData);
    }
}