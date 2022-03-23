using GraphicalEngine.Core;
using GraphicalEngine.Services;
using OldTanks.Services;
using OpenTK.Mathematics;

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
        var collisionScene = new Scene();

        //x, y, z, u, v
        float[] topSide =
        {
             1.0f,    1.0f,    1.0f,    1.0f,    1.0f,
             1.0f,    1.0f,   -1.0f,    1.0f,    0.0f,
            -1.0f,    1.0f,   -1.0f,    0.0f,    0.0f,
            -1.0f,    1.0f,    1.0f,    0.0f,    1.0f,
        };
        
        float[] downSide =
        {
            -1.0f,   -1.0f,   -1.0f,    1.0f,    0.0f,
             1.0f,   -1.0f,   -1.0f,    0.0f,    0.0f,
             1.0f,   -1.0f,    1.0f,    0.0f,    1.0f,
            -1.0f,   -1.0f,    1.0f,    1.0f,    1.0f,
        };
        
        float[] backSide =
        {
            -1.0f,   -1.0f,    1.0f,    1.0f,    0.0f,
             1.0f,   -1.0f,    1.0f,    0.0f,    0.0f,
             1.0f,    1.0f,    1.0f,    0.0f,    1.0f,
            -1.0f,    1.0f,    1.0f,    1.0f,    1.0f,
        };
        
        float[] frontSide =
        {
            1.0f,    1.0f,   -1.0f,    1.0f,    1.0f,
            1.0f,   -1.0f,   -1.0f,    1.0f,    0.0f,
           -1.0f,   -1.0f,   -1.0f,    0.0f,    0.0f,
           -1.0f,    1.0f,   -1.0f,    0.0f,    1.0f,
        };

        float[] rightSide =
        {
            1.0f,    1.0f,    1.0f,    1.0f,    1.0f,
            1.0f,   -1.0f,    1.0f,    1.0f,    0.0f,
            1.0f,   -1.0f,   -1.0f,    0.0f,    0.0f,
            1.0f,    1.0f,   -1.0f,    0.0f,    1.0f,
        };
        
        float[] leftSide =
        { 
           -1.0f,    1.0f,    1.0f,    1.0f,    0.0f,
           -1.0f,    1.0f,   -1.0f,    0.0f,    0.0f,
           -1.0f,   -1.0f,   -1.0f,    0.0f,    1.0f,
           -1.0f,   -1.0f,    1.0f,    1.0f,    1.0f,
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
        
        collisionScene.Meshes.Add(new Mesh(6, backSide, collisionSceneIndices) { Normal = new Vector3(0, 0, 1)});
        collisionScene.Meshes.Add(new Mesh(7, frontSide, collisionSceneIndices) { Normal = new Vector3(0, 0, -1)});
        collisionScene.Meshes.Add(new Mesh(8, topSide, collisionSceneIndices) { Normal = new Vector3(0, 1, 0)});
        collisionScene.Meshes.Add(new Mesh(9, downSide, collisionSceneIndices) { Normal = new Vector3(0, -1, 0)});
        collisionScene.Meshes.Add(new Mesh(10, rightSide, collisionSceneIndices) { Normal = new Vector3(1, 0, 0)});
        collisionScene.Meshes.Add(new Mesh(11, leftSide, collisionSceneIndices) { Normal = new Vector3(-1, 0, 0)});


        GlobalCache<Scene>.AddOrUpdateItem("CubeScene", scene);
        GlobalCache<Scene>.AddOrUpdateItem("CubeCollision", collisionScene);
    }
}