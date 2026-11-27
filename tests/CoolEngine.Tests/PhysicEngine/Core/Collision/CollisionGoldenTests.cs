namespace CoolEngine.Tests.PhysicEngine.Core.Collision;

// Inside the namespace so that "Collision" names the engine class, not this test namespace's last segment.
using CoolEngine.PhysicEngine.Core.Collision;
using System.Numerics;
using CoolEngine.Mathematics;
using CoolEngine.Tests.TestSupport;

[TestFixture]
public sealed class CollisionGoldenTests
{
    // Uneven scale, three rotations and a translation, applied in that order: scale first, translation last.
    private static readonly Matrix4 s_transformation =
        Matrix4.CreateTranslation(-2f, 1f, 4f) *
        (Matrix4.CreateRotationZ(0.25f) *
         (Matrix4.CreateRotationY(-0.9f) *
          (Matrix4.CreateRotationX(0.4f) * Matrix4.CreateScale(1.5f, 0.75f, 2f))));

    private static readonly Vector3 s_position = new Vector3(-2f, 1f, 4f);
    private static readonly Vector3 s_size = new Vector3(3f, 1.5f, 4f);

    [Test]
    public void TransformedShape_RotatedScaledTranslated_MatchesGolden()
    {
        var cube = CollisionTestPhysicObject.Create(CollisionTestShapes.Cube(), s_transformation, s_position, s_size)
            .Collision!.CollisionData;
        var tetrahedron = CollisionTestPhysicObject
            .Create(CollisionTestShapes.Tetrahedron(), s_transformation, s_position, s_size)
            .Collision!.CollisionData;

        // Recorded on 2026-11-26 at 3d22d58 with OpenTK.Mathematics, before the move onto CoolEngine.Mathematics.
        // Vertices and normals in shape order, three floats each.
        float[] expectedCubeVertices =
        [
            -4.501441f, 0.27041155f, 4.151641f,
            -2.694584f, 0.73177785f, 6.5016217f,
            -1.9094344f, -0.49366045f, 6.138522f,
            -3.7162914f, -0.95502687f, 3.7885413f,
            -2.0905657f, 2.4936604f, 1.8614779f,
            -0.28370857f, 2.9550269f, 4.2114587f,
            0.501441f, 1.7295885f, 3.8483593f,
            -1.3054161f, 1.2682221f, 1.4983783f
        ];
        float[] expectedCubeNormals =
        [
            -0.6027188f, -0.5558122f, 0.5725407f,
            0.6027188f, 0.5558122f, -0.5725407f,
            -0.52343297f, 0.81695884f, 0.24206632f,
            0.52343297f, -0.81695884f, -0.24206632f,
            0.6022856f, 0.15378875f, 0.783327f,
            -0.6022856f, -0.15378875f, -0.783327f
        ];
        float[] expectedTetrahedronVertices =
        [
            -2.694584f, 0.73177785f, 6.5016217f,
            0.501441f, 1.7295885f, 3.8483593f,
            -2.0905657f, 2.4936604f, 1.8614779f,
            -3.7162914f, -0.95502687f, 3.7885413f
        ];
        float[] expectedTetrahedronNormals =
        [
            0.38016587f, -0.58123803f, -0.7194693f,
            -0.890903f, 0.45082548f, 0.055209477f,
            0.5075746f, -0.80432135f, 0.3089257f,
            0.003162478f, 0.9347339f, 0.35533416f
        ];

        Assert.Multiple(() =>
        {
            GoldenAssert.AreClose(expectedCubeVertices, GoldenAssert.Floats(cube.Vertices), "cube vertices");
            GoldenAssert.AreClose(expectedCubeNormals, GoldenAssert.Floats(cube.Faces.Select(face => face.Normal)),
                "cube normals");
            GoldenAssert.AreClose(expectedTetrahedronVertices, GoldenAssert.Floats(tetrahedron.Vertices),
                "tetrahedron vertices");
            GoldenAssert.AreClose(expectedTetrahedronNormals,
                GoldenAssert.Floats(tetrahedron.Faces.Select(face => face.Normal)), "tetrahedron normals");
        });
    }
}
