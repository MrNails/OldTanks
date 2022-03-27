using CoolEngine.Core.Primitives;
using CoolEngine.GraphicalEngine.Core;
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
        var collisionScene = new List<CollisionMesh>();

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

        uint[] upDownSideIndices =
        {
            0, 1, 3,
            1, 2, 3
        };
        
        uint[] collisionSceneIndices =
        {
            0, 1, 
            2, 3,
            1, 2,
            0, 3
        };

        scene.Meshes.Add(new Mesh(0, backSide, upDownSideIndices) { Normal = new Vector3(0, 0, 1)});
        scene.Meshes.Add(new Mesh(1, frontSide, upDownSideIndices) { Normal = new Vector3(0, 0, -1)});
        scene.Meshes.Add(new Mesh(2, topSide, upDownSideIndices) { Normal = new Vector3(0, 1, 0)});
        scene.Meshes.Add(new Mesh(3, downSide, upDownSideIndices) { Normal = new Vector3(0, -1, 0)});
        scene.Meshes.Add(new Mesh(4, rightSide, upDownSideIndices) { Normal = new Vector3(1, 0, 0)});
        scene.Meshes.Add(new Mesh(5, leftSide, upDownSideIndices) { Normal = new Vector3(-1, 0, 0)});
        
        collisionScene.Add(new CollisionMesh(backSide.Select(b => b.Position).ToArray(), collisionSceneIndices) { Normal = new Vector3(0, 0, 1)});
        collisionScene.Add(new CollisionMesh(frontSide.Select(b => b.Position).ToArray(), collisionSceneIndices) { Normal = new Vector3(0, 0, -1)});
        collisionScene.Add(new CollisionMesh(topSide.Select(b => b.Position).ToArray(), collisionSceneIndices) { Normal = new Vector3(0, 1, 0)});
        collisionScene.Add(new CollisionMesh(rightSide.Select(b => b.Position).ToArray(), collisionSceneIndices) { Normal = new Vector3(1, 0, 0)});
        collisionScene.Add(new CollisionMesh(leftSide.Select(b => b.Position).ToArray(), collisionSceneIndices) { Normal = new Vector3(-1, 0, 0)});


        GlobalCache<Scene>.AddOrUpdateItem("CubeScene", scene);
        GlobalCache<List<CollisionMesh>>.AddOrUpdateItem("CubeCollision", collisionScene);
    }
}