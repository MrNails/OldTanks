using CoolEngine.GraphicalEngine.Core;
using CoolEngine.Services.Interfaces;

namespace CoolEngine.PhysicEngine.Core.Collision;

public class CubeCollision : Collision
{
    public CubeCollision(IPhysicObject physicObject, List<Mesh> originalCollision) : base(physicObject, originalCollision)
    {
        CollisionType = CollisionType.Cube;
    }

    //TODO: implement collision check
    public override bool CheckCollision(IPhysicObject t2)
    {
        if (t2 == null)
            return false;

        if (t2.Collision.CollisionType != CollisionType.Cube)
            return false;

        UpdateCollision();
        t2.Collision.UpdateCollision();

        for (int oI = 0; oI < t2.Collision.Meshes.Count; oI++)
        {
            var outerVertices = t2.Collision.Meshes[oI].Vertices;

            for (int oJ = 0; oJ < outerVertices.Length; oJ++)
            {
                var outerVertex = outerVertices[oJ];
                var centerToVertexLength = (outerVertex - CurrentObject.Position).LengthFast;

                if (centerToVertexLength <= CurrentObject.Height / 2 ||
                    centerToVertexLength <= CurrentObject.Width / 2 ||
                    centerToVertexLength <= CurrentObject.Length / 2 )
                    return true;

                // for (int i = 0; i < Meshes.Count; i++)
                // {
                //     var vertices = Meshes[i].Vertices;
                //
                //     if (vertices[0].X > outerVertices[oJ].X)
                //         return true;
                // }
            }
        }

        
        return false;
    }
}