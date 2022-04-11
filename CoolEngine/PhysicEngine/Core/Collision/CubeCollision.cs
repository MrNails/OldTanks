// using System.Collections.ObjectModel;
// using System.Runtime.CompilerServices;
// using CoolEngine.GraphicalEngine.Core;
// using CoolEngine.Services;
// using CoolEngine.Services.Extensions;
// using CoolEngine.Services.Interfaces;
// using OpenTK.Mathematics;
//
// namespace CoolEngine.PhysicEngine.Core.Collision;
//
// public class CubeCollision : Collision
// {
//     private Vector3 m_minVertex;
//     private Vector3 m_maxVertex;
//
//     public CubeCollision(IPhysicObject physicObject, CollisionData originalCollision) : base(physicObject,
//         originalCollision)
//     {
//         CollisionType = CollisionType.Cube;
//     }
//
//     public override void UpdateCollision()
//     {
//         base.UpdateCollision();
//
//         if (CurrentObject.Collision.CollisionData.Vertices.Length == 0)
//             return;
//
//         GetBoundingBoxCoors(CurrentObject.Collision.CollisionData.Vertices, out m_minVertex, out m_maxVertex);
//     }
//     
//     /// <inheritdoc/>
//     public override bool CheckCollision(IPhysicObject t2, out Vector3 normal)
//     {
//         normal = Vector3.Zero;
//
//         if (t2 == null)
//             return false;
//
//         if (t2.Collision.CollisionType != CollisionType.Cube)
//             return false;
//
//         normal = (t2.Position - CurrentObject.Position).Normalized();
//
//         // return AABBCollisionCheck(t2) || 
//         return OBBCollisionCheck(t2);
//     }
//
//     private void GetBoundingBoxCoors(Vector3[] vertices, out Vector3 min, out Vector3 max)
//     {
//         min = new Vector3(float.MaxValue);
//         max = new Vector3(float.MinValue);
//
//         for (int j = 0; j < vertices.Length; j++)
//         {
//             var current = vertices[j];
//
//             if (max.X < current.X) max.X = current.X;
//             if (max.Y < current.Y) max.Y = current.Y;
//             if (max.Z < current.Z) max.Z = current.Z;
//
//             if (min.X > current.X) min.X = current.X;
//             if (min.Y > current.Y) min.Y = current.Y;
//             if (min.Z > current.Z) min.Z = current.Z;
//         }
//     }
//
//     private bool AABBCollisionCheck(IPhysicObject t2)
//     {
//         var outerVertices = t2.Collision.CollisionData.Vertices;
//
//         for (int oJ = 0; oJ < outerVertices.Length; oJ++)
//         {
//             if (VectorExtensions.GreaterThan(m_maxVertex, outerVertices[oJ]) &&
//                 VectorExtensions.LoverThan(m_minVertex, outerVertices[oJ]))
//                 return true;
//         }
//
//         return false;
//     }
//
//     private bool OBBCollisionCheck(IPhysicObject t2)
//     {
//         for (int i = 0; i < CurrentObject.Collision.CollisionData.Meshes.Count; i++)
//         {
//             var mesh = CurrentObject.Collision.CollisionData.Meshes[i];
//             float currMin, currMax, t2Min, t2Max;
//
//             ProjectionMinMaxVertices(mesh.Normal, CurrentObject.Collision.CollisionData.Vertices,
//                 out currMin, out currMax);
//             ProjectionMinMaxVertices(mesh.Normal, t2.Collision.CollisionData.Vertices,
//                 out t2Min, out t2Max);
//
//             if (currMin >= t2Max || t2Min >= currMax)
//             {
//                 mesh.Color = Colors.Red;
//                 t2.Collision.CollisionData.Meshes[i].Color = Colors.Red;
//                 return false;
//             }
//         }
//
//         return true;
//     }
//
//     private void ProjectionMinMaxVertices(in Vector3 normal, Vector3[] vertices, out float min, out float max)
//     {
//         min = float.MaxValue;
//         max = float.MinValue;
//
//         for (int j = 0; j < vertices.Length; j++)
//         {
//             var dotRes = Vector3.Dot(vertices[j], normal);
//
//             if (min > dotRes) min = dotRes;
//             if (max < dotRes) max = dotRes;
//         }
//     }
// }