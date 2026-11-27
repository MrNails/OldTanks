using CoolEngine.GraphicalEngine.Core;
using System.Numerics;

namespace CoolEngine.Tests.GraphicalEngine.Core;

[TestFixture]
public sealed class SceneTests
{
    private static Mesh MeshOf(params Vector3[] vertices) => new(vertices, Array.Empty<Face>());

    [Test]
    public void Center_OneMesh_IsMidpointOfItsBounds()
    {
        var scene = new Scene([MeshOf(new Vector3(-1, -2, -3), new Vector3(5, 6, 7), new Vector3(0, 0, 0))]);

        Assert.That(scene.Center, Is.EqualTo(new Vector3(2, 2, 2)));
    }

    [Test]
    public void Center_SeveralMeshes_IsMidpointOfTheCombinedBounds()
    {
        var scene = new Scene(
        [
            MeshOf(new Vector3(0, 0, 0), new Vector3(1, 1, 1)),
            MeshOf(new Vector3(4, -6, 2), new Vector3(10, -2, 8))
        ]);

        Assert.That(scene.Center, Is.EqualTo(new Vector3(5, -2.5f, 4)));
    }

    [Test]
    public void Center_AllCoordinatesNegative_IsMidpointOfItsBounds()
    {
        var scene = new Scene([MeshOf(new Vector3(-4, -6, -8), new Vector3(-2, -2, -2))]);

        Assert.That(scene.Center, Is.EqualTo(new Vector3(-3, -4, -5)));
    }

    [Test]
    public void Center_OneVertex_IsThatVertex()
    {
        var scene = new Scene([MeshOf(new Vector3(3, -4, 5))]);

        Assert.That(scene.Center, Is.EqualTo(new Vector3(3, -4, 5)));
    }

    [Test]
    public void Center_EmptyMeshBesideOthers_IsIgnored()
    {
        var scene = new Scene([MeshOf(), MeshOf(new Vector3(2, 2, 2), new Vector3(4, 4, 4))]);

        Assert.That(scene.Center, Is.EqualTo(new Vector3(3, 3, 3)));
    }

    [Test]
    public void Center_NoMeshes_IsZero()
    {
        Assert.That(new Scene(Array.Empty<Mesh>()).Center, Is.EqualTo(Vector3.Zero));
    }

    [Test]
    public void Center_OnlyEmptyMeshes_IsZero()
    {
        Assert.That(new Scene([MeshOf()]).Center, Is.EqualTo(Vector3.Zero));
    }

    [Test]
    public void Copy_AnyScene_KeepsTheCenter()
    {
        var scene = new Scene([MeshOf(new Vector3(-1, -2, -3), new Vector3(5, 6, 7), new Vector3(0, 0, 0))]);

        Assert.That(scene.Copy().Center, Is.EqualTo(scene.Center));
    }
}
