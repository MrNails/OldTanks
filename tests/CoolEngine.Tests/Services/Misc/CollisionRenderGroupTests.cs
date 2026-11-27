using System.Numerics;
using System.Runtime.CompilerServices;
using CoolEngine.GraphicalEngine.Core;
using CoolEngine.Mathematics;
using CoolEngine.PhysicEngine.Core.Collision;
using CoolEngine.Services.Misc;
using CoolEngine.Tests.TestSupport;
using PhysicsFace = CoolEngine.PhysicEngine.Core.Face;

namespace CoolEngine.Tests.Services.Misc;

[TestFixture]
public sealed class CollisionRenderGroupTests
{
    // This process has no OpenGL bindings, so any GL call throws: a call that does not throw made no GL call.
    [Test]
    public void UpdateCollisionablesData_SphereGroup_MakesNoGlCall()
    {
        var group = CreateGroupWithTwoSpheres();

        Assert.DoesNotThrow(group.UpdateCollisionablesData);
        Assert.DoesNotThrow(group.UpdateCollisionablesData);

        Assert.That(group.DrawObjectInfo, Is.Null);
    }

    [Test]
    public void UpdateCollisionablesData_SphereGroupUpdatedTwice_KeepsItsVertexArray()
    {
        var group = CreateGroupWithTwoSpheres();

        group.UpdateCollisionablesData();
        var vertices = group.Vertices;
        group.UpdateCollisionablesData();

        Assert.Multiple(() =>
        {
            Assert.That(group.Vertices, Is.SameAs(vertices));
            Assert.That(group.Vertices, Is.Empty);
            Assert.That(group.VerticesPerModel, Is.EqualTo(0));
            Assert.That(group.IndicesPerModel, Is.EqualTo(0));
            Assert.That(group.ActiveCount, Is.EqualTo(2));
        });
    }

    // InitRenderGroup adds the first member's face indices to IndicesPerModel, so a second init would double them.
    [Test]
    public void UpdateCollisionablesData_ShapeWithFacesButNoVertices_CountsIndicesOnce()
    {
        var shape = new CollisionData(CollisionType.Sphere);
        shape.Faces.Add(new PhysicsFace(new uint[] { 0, 1 }) { Normal = Vector3.UnitY });
        var group = new CollisionRenderGroup(CreateShaderWithoutGl());
        group.Add(CollisionTestPhysicObject.Create(shape, Matrix4.Identity, Vector3.Zero, Vector3.One));

        Assert.DoesNotThrow(group.UpdateCollisionablesData);
        Assert.DoesNotThrow(group.UpdateCollisionablesData);

        Assert.Multiple(() =>
        {
            Assert.That(group.IndicesPerModel, Is.EqualTo(2));
            Assert.That(group.VerticesPerModel, Is.EqualTo(0));
            Assert.That(group.DrawObjectInfo, Is.Null);
        });
    }

    [Test]
    public void UpdateCollisionablesData_SphereGroupWithAnInactiveSphere_CountsOnlyTheActiveOne()
    {
        var group = new CollisionRenderGroup(CreateShaderWithoutGl());
        var inactiveSphere = CreateSphere(new Vector3(3, 5, 0));
        inactiveSphere.Collision!.IsActive = false;
        group.Add(CreateSphere(new Vector3(0, 5, 0)));
        group.Add(inactiveSphere);

        group.UpdateCollisionablesData();

        Assert.That(group.ActiveCount, Is.EqualTo(1));
    }

    [Test]
    public void UpdateCollisionablesData_SphereGroupAfterFirstUpdate_AllocatesNothing()
    {
        var group = CreateGroupWithTwoSpheres();
        group.UpdateCollisionablesData();

        var before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 100; i++)
            group.UpdateCollisionablesData();
        var after = GC.GetAllocatedBytesForCurrentThread();

        Assert.That(after - before, Is.EqualTo(0));
    }

    [Test]
    public void UpdateCollisionablesData_EmptyGroup_MakesNoGlCallAndCountsNothing()
    {
        var group = new CollisionRenderGroup(CreateShaderWithoutGl());

        Assert.DoesNotThrow(group.UpdateCollisionablesData);

        Assert.Multiple(() =>
        {
            Assert.That(group.ActiveCount, Is.EqualTo(0));
            Assert.That(group.Vertices, Is.Empty);
        });
    }

    // Shader's constructor and finalizer call OpenGL (KI-017); the group only stores the shader until it makes GL objects.
    private static Shader CreateShaderWithoutGl()
    {
        var shader = (Shader)RuntimeHelpers.GetUninitializedObject(typeof(Shader));
        GC.SuppressFinalize(shader);

        return shader;
    }

    private static CollisionTestPhysicObject CreateSphere(Vector3 centre) =>
        CollisionTestPhysicObject.Create(CollisionTestShapes.Sphere(), Matrix4.CreateTranslation(centre), centre,
            Vector3.One);

    private static CollisionRenderGroup CreateGroupWithTwoSpheres()
    {
        var group = new CollisionRenderGroup(CreateShaderWithoutGl());
        group.Add(CreateSphere(new Vector3(0, 5, 0)));
        group.Add(CreateSphere(new Vector3(3, 5, 0)));

        return group;
    }
}
