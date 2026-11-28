using System.Numerics;
using System.Reflection;
using CoolEngine.Physics.Tests.TestSupport;

namespace CoolEngine.Physics.Tests;

[TestFixture]
public sealed class PhysicsWorldTests
{
    #region Handles

    [Test]
    public void Add_FirstBody_ReturnsSlotZeroGenerationOne()
    {
        var world = TestBodies.World();

        var handle = world.Add(TestBodies.Cube(Vector3.Zero, Vector3.One));

        Assert.Multiple(() =>
        {
            Assert.That(handle.Slot, Is.EqualTo(0));
            Assert.That(handle.Generation, Is.EqualTo(1));
            Assert.That(world.Count, Is.EqualTo(1));
        });
    }

    [TestCase("null shape")]
    [TestCase("NaN position")]
    [TestCase("mass 0")]
    [TestCase("mass NaN")]
    public void Add_InvalidDescription_ThrowsArgumentException(string defect)
    {
        var world = TestBodies.World();
        var valid = TestBodies.Cube(Vector3.Zero, Vector3.One);
        var description = defect switch
        {
            "null shape" => new BodyDescription(null!, valid.Position, valid.Linear, valid.Size, valid.VelocityFrame),
            "NaN position" => new BodyDescription(valid.Shape, new Vector3(float.NaN, 0, 0), valid.Linear, valid.Size,
                valid.VelocityFrame),
            "mass 0" => valid with { Mass = 0 },
            _ => valid with { Mass = float.NaN },
        };

        var exception = Assert.Throws<ArgumentException>(() => world.Add(description));

        Assert.Multiple(() =>
        {
            Assert.That(exception?.ParamName, Is.EqualTo("description"));
            Assert.That(world.Count, Is.EqualTo(0));
            Assert.That(world.SlotCount, Is.EqualTo(0));
        });
    }

    [Test]
    public void Remove_ThenGetPosition_ThrowsArgumentException()
    {
        var world = TestBodies.World();
        var handle = world.Add(TestBodies.Cube(Vector3.Zero, Vector3.One));
        world.Remove(handle);

        var exception = Assert.Throws<ArgumentException>(() => world.GetPosition(handle));

        Assert.That(exception?.ParamName, Is.EqualTo("handle"));
    }

    [Test]
    public void Remove_Twice_ThrowsArgumentException()
    {
        var world = TestBodies.World();
        var handle = world.Add(TestBodies.Cube(Vector3.Zero, Vector3.One));
        world.Remove(handle);

        var exception = Assert.Throws<ArgumentException>(() => world.Remove(handle));

        Assert.That(exception?.ParamName, Is.EqualTo("handle"));
    }

    [Test]
    public void Contains_DefaultHandle_ReturnsFalse()
    {
        var world = TestBodies.World();
        world.Add(TestBodies.Cube(Vector3.Zero, Vector3.One));

        Assert.That(world.Contains(default), Is.False);
    }

    [Test]
    public void Add_AfterRemove_ReusesTheFreedSlotWithTheNextGeneration()
    {
        var world = TestBodies.World();
        world.Add(TestBodies.Cube(new Vector3(0, 0, 0), Vector3.One));
        var b = world.Add(TestBodies.Cube(new Vector3(1, 0, 0), Vector3.One));
        world.Add(TestBodies.Cube(new Vector3(2, 0, 0), Vector3.One));
        world.Remove(b);

        var d = world.Add(TestBodies.Cube(new Vector3(3, 0, 0), Vector3.One));

        Assert.Multiple(() =>
        {
            Assert.That(d.Slot, Is.EqualTo(1));
            Assert.That(d.Generation, Is.EqualTo(2));
        });
    }

    [Test]
    public void Add_AfterTwoRemoves_ReusesTheLastFreedSlotFirst()
    {
        var world = TestBodies.World();
        var handles = new BodyHandle[4];
        for (var i = 0; i < handles.Length; i++)
            handles[i] = world.Add(TestBodies.Cube(new Vector3(i, 0, 0), Vector3.One));
        world.Remove(handles[1]);
        world.Remove(handles[3]);

        var first = world.Add(TestBodies.Cube(new Vector3(10, 0, 0), Vector3.One));
        var second = world.Add(TestBodies.Cube(new Vector3(11, 0, 0), Vector3.One));

        Assert.Multiple(() =>
        {
            Assert.That(first.Slot, Is.EqualTo(3));
            Assert.That(second.Slot, Is.EqualTo(1));
        });
    }

    [Test]
    public void Enumerate_AfterRemoveAndAdd_YieldsLiveBodiesInSlotOrder()
    {
        var world = TestBodies.World();
        var a = world.Add(TestBodies.Cube(new Vector3(0, 0, 0), Vector3.One));
        var b = world.Add(TestBodies.Cube(new Vector3(1, 0, 0), Vector3.One));
        var c = world.Add(TestBodies.Cube(new Vector3(2, 0, 0), Vector3.One));
        world.Remove(b);
        var d = world.Add(TestBodies.Cube(new Vector3(3, 0, 0), Vector3.One));

        var enumerated = new List<BodyHandle>();
        foreach (var handle in world)
            enumerated.Add(handle);

        Assert.That(enumerated, Is.EqualTo(new[] { a, d, c }));
    }

    [Test]
    public void Add_BeyondInitialCapacity_GrowsAndKeepsEveryBodyState()
    {
        var world = TestBodies.World(initialCapacity: 2);
        var descriptions = new BodyDescription[5];
        var handles = new BodyHandle[5];
        for (var i = 0; i < descriptions.Length; i++)
        {
            descriptions[i] = TestBodies.Cube(new Vector3(i, 2 * i, 3 * i), new Vector3(1 + i, 2, 3),
                new Vector3(0.1f * i, 0.2f * i, 0.3f * i), isStatic: i % 2 == 1, velocity: new Vector3(i, -i, 0.5f * i),
                mass: 1 + i) with { Force = new Vector3(-i, i, 0) };
            handles[i] = world.Add(descriptions[i]);
        }

        Assert.Multiple(() =>
        {
            Assert.That(world.Count, Is.EqualTo(5));

            for (var i = 0; i < descriptions.Length; i++)
            {
                var state = world.GetState(handles[i]);
                var expected = descriptions[i];
                Assert.That(state.Handle, Is.EqualTo(handles[i]), $"body {i} handle");
                Assert.That(world.GetShape(handles[i]), Is.SameAs(expected.Shape), $"body {i} shape");
                Assert.That(state.Position, Is.EqualTo(expected.Position), $"body {i} position");
                Assert.That(state.Linear, Is.EqualTo(expected.Linear), $"body {i} linear");
                Assert.That(state.Size, Is.EqualTo(expected.Size), $"body {i} size");
                Assert.That(state.VelocityFrame, Is.EqualTo(expected.VelocityFrame), $"body {i} velocity frame");
                Assert.That(state.Velocity, Is.EqualTo(expected.Velocity), $"body {i} velocity");
                Assert.That(state.Force, Is.EqualTo(expected.Force), $"body {i} force");
                Assert.That(state.Mass, Is.EqualTo(expected.Mass), $"body {i} mass");
                Assert.That(state.IsStatic, Is.EqualTo(expected.IsStatic), $"body {i} static");
                Assert.That(state.OnGround, Is.False, $"body {i} ground flag");
            }
        });
    }

    [Test]
    public void Enumerate_BodyAddedDuringEnumeration_ThrowsInvalidOperationException()
    {
        var world = TestBodies.World();
        world.Add(TestBodies.Cube(new Vector3(0, 0, 0), Vector3.One));
        world.Add(TestBodies.Cube(new Vector3(1, 0, 0), Vector3.One));
        InvalidOperationException? caught = null;
        var additions = 0;

        // At most three additions, so a regression that never throws ends instead of growing the world for ever.
        try
        {
            foreach (var _ in world)
            {
                if (additions == 3)
                    break;

                world.Add(TestBodies.Cube(new Vector3(5, 0, 0), Vector3.One));
                additions++;
            }
        }
        catch (InvalidOperationException exception)
        {
            caught = exception;
        }

        Assert.That(caught, Is.Not.Null, $"no InvalidOperationException after {additions} additions");
    }

    [Test]
    public void Enumerator_CurrentAfterTheEnd_IsDefault()
    {
        // The slot count equals the capacity, so the slot past the end is outside every field array.
        var world = TestBodies.World(initialCapacity: 2);
        world.Add(TestBodies.Cube(new Vector3(0, 0, 0), Vector3.One));
        world.Add(TestBodies.Cube(new Vector3(1, 0, 0), Vector3.One));
        var enumerator = world.GetEnumerator();
        while (enumerator.MoveNext())
        {
        }

        Assert.That(enumerator.Current, Is.EqualTo(default(BodyHandle)));
    }

    [Test]
    public void Add_SlotReusedWithALargerShape_GeometryIsComplete()
    {
        var world = TestBodies.World();
        var tetrahedron = world.Add(TestBodies.Polygon(TestShapes.Tetrahedron(), new Vector3(0, 0, 0), Vector3.One));
        world.Add(TestBodies.Cube(new Vector3(5, 0, 0), Vector3.One));
        world.Remove(tetrahedron);
        var position = new Vector3(1, 2, 3);
        var size = new Vector3(2, 1, 3);
        var expectedVertices = new Vector3[8];
        var expectedNormals = new Vector3[6];
        PhysicsQueries.ComputeWorldVertices(TestBodies.CubeShape, position, TestBodies.Linear(default, size),
            expectedVertices);
        PhysicsQueries.ComputeWorldNormals(TestBodies.CubeShape, position, TestBodies.Linear(default, size),
            expectedNormals);

        var cube = world.Add(TestBodies.Cube(position, size));

        Assert.Multiple(() =>
        {
            Assert.That(cube.Slot, Is.EqualTo(tetrahedron.Slot), "slot");
            Assert.That(world.GetWorldVertices(cube).ToArray(), Is.EqualTo(expectedVertices), "vertices");
            Assert.That(world.GetWorldNormals(cube).ToArray(), Is.EqualTo(expectedNormals), "normals");
        });
    }

    [Test]
    public void Add_SlotReusedWithASmallerShape_GeometryIsSliced()
    {
        var world = TestBodies.World();
        var cube = world.Add(TestBodies.Cube(new Vector3(0, 0, 0), Vector3.One));
        world.Add(TestBodies.Cube(new Vector3(5, 0, 0), Vector3.One));
        world.Remove(cube);
        var shape = TestShapes.Tetrahedron();
        var position = new Vector3(1, 2, 3);
        var expectedVertices = new Vector3[4];
        var expectedNormals = new Vector3[4];
        PhysicsQueries.ComputeWorldVertices(shape, position, TestBodies.Linear(default, Vector3.One), expectedVertices);
        PhysicsQueries.ComputeWorldNormals(shape, position, TestBodies.Linear(default, Vector3.One), expectedNormals);

        var tetrahedron = world.Add(TestBodies.Polygon(shape, position, Vector3.One));

        Assert.Multiple(() =>
        {
            Assert.That(tetrahedron.Slot, Is.EqualTo(cube.Slot), "slot");
            Assert.That(world.GetWorldVertices(tetrahedron).ToArray(), Is.EqualTo(expectedVertices), "vertices");
            Assert.That(world.GetWorldNormals(tetrahedron).ToArray(), Is.EqualTo(expectedNormals), "normals");
        });
    }

    [Test]
    public void Remove_GenerationAtIntMaxValue_WrapsToOne()
    {
        var world = TestBodies.World();
        var first = world.Add(TestBodies.Cube(new Vector3(0, 0, 0), Vector3.One));
        var generations = (int[])typeof(PhysicsWorld)
            .GetField("m_generations", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(world)!;
        generations[first.Slot] = int.MaxValue;
        world.TryGetHandle(first.Slot, out var old);

        world.Remove(old);
        var reused = world.Add(TestBodies.Cube(new Vector3(1, 0, 0), Vector3.One));

        Assert.Multiple(() =>
        {
            Assert.That(old.Generation, Is.EqualTo(int.MaxValue), "precondition: the old generation");
            Assert.That(reused.Slot, Is.EqualTo(first.Slot), "slot");
            Assert.That(reused.Generation, Is.EqualTo(1), "generation");
            Assert.That(world.Contains(old), Is.False, "the old handle");
            Assert.That(() => world.GetPosition(old), Throws.ArgumentException, "the old handle's position");
        });
    }

    [Test]
    public void TryGetHandle_FreeSlot_ReturnsFalse()
    {
        var world = TestBodies.World();
        var first = world.Add(TestBodies.Cube(new Vector3(0, 0, 0), Vector3.One));
        var second = world.Add(TestBodies.Cube(new Vector3(1, 0, 0), Vector3.One));
        world.Remove(first);

        var slot0 = world.TryGetHandle(0, out _);
        var slot1 = world.TryGetHandle(1, out var handle1);
        var slot2 = world.TryGetHandle(2, out _);

        Assert.Multiple(() =>
        {
            Assert.That(slot0, Is.False);
            Assert.That(slot1, Is.True);
            Assert.That(handle1, Is.EqualTo(second));
            Assert.That(slot2, Is.False);
        });
    }

    #endregion

    #region State

    [TestCase(0f)]
    [TestCase(-1f)]
    [TestCase(float.NaN)]
    [TestCase(float.PositiveInfinity)]
    public void SetMass_NotPositiveOrNotFinite_ThrowsArgumentOutOfRangeException(float mass)
    {
        var world = TestBodies.World();
        var handle = world.Add(TestBodies.Cube(Vector3.Zero, Vector3.One, mass: 2));

        Assert.Multiple(() =>
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => world.SetMass(handle, mass));
            Assert.That(world.GetMass(handle), Is.EqualTo(2f));
        });
    }

    [Test]
    public void SetVelocity_StaticBody_ThrowsInvalidOperationException()
    {
        var world = TestBodies.World();
        var handle = world.Add(TestBodies.Floor(Vector3.Zero, Vector3.One));

        Assert.Throws<InvalidOperationException>(() => world.SetVelocity(handle, new Vector3(1, 0, 0)));
    }

    [Test]
    public void SetForce_StaticBody_ThrowsInvalidOperationException()
    {
        var world = TestBodies.World();
        var handle = world.Add(TestBodies.Floor(Vector3.Zero, Vector3.One));

        Assert.Throws<InvalidOperationException>(() => world.SetForce(handle, new Vector3(1, 0, 0)));
    }

    [Test]
    public void SetPosition_NotFinite_ThrowsArgumentOutOfRangeException()
    {
        var world = TestBodies.World();
        var handle = world.Add(TestBodies.Cube(Vector3.Zero, Vector3.One));

        Assert.Multiple(() =>
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => world.SetPosition(handle, new Vector3(float.NaN, 0, 0)));
            Assert.That(world.GetPosition(handle), Is.EqualTo(Vector3.Zero));
        });
    }

    [Test]
    public void SetPosition_Moved_GeometryAndRayCastUseTheNewPosition()
    {
        var world = TestBodies.World();
        var handle = world.Add(TestBodies.Cube(Vector3.Zero, new Vector3(2)));
        var before = world.GetWorldVertices(handle).ToArray();

        world.SetPosition(handle, new Vector3(10, 0, 0));
        var after = world.GetWorldVertices(handle).ToArray();
        var hit = world.RayCast(new Ray(Vector3.Zero, new Vector3(100, 0, 0)), out var rayHit);

        Assert.Multiple(() =>
        {
            for (var i = 0; i < before.Length; i++)
            {
                Assert.That(after[i].X, Is.EqualTo(before[i].X + 10).Within(1e-5f), $"vertex {i} X");
                Assert.That(after[i].Y, Is.EqualTo(before[i].Y), $"vertex {i} Y");
                Assert.That(after[i].Z, Is.EqualTo(before[i].Z), $"vertex {i} Z");
            }

            Assert.That(hit, Is.True, "ray hit");
            Assert.That(rayHit.Body, Is.EqualTo(handle), "hit body");
            Assert.That(rayHit.Point.X, Is.EqualTo(9f).Within(1e-5f), "hit X");
        });
    }

    [Test]
    public void SetStatic_MovingBody_KeepsVelocityForceAndGroundFlag()
    {
        var world = TestBodies.World();
        world.Add(TestBodies.Floor(new Vector3(0, -2, 0), new Vector3(20, 1, 20)));
        var handle = world.Add(TestBodies.Cube(new Vector3(0, -1, 0), Vector3.One));
        for (var tick = 0; tick < 30; tick++)
            world.Step(TestBodies.TickTime);
        world.SetVelocity(handle, new Vector3(1, 0, 0));
        world.SetForce(handle, new Vector3(3, 0, 0));
        Assert.That(world.IsOnGround(handle), Is.True, "precondition: the cube rests on the floor");

        world.SetStatic(handle, true);

        Assert.Multiple(() =>
        {
            Assert.That(world.IsStatic(handle), Is.True);
            Assert.That(world.GetVelocity(handle), Is.EqualTo(new Vector3(1, 0, 0)));
            Assert.That(world.GetForce(handle), Is.EqualTo(new Vector3(3, 0, 0)));
            Assert.That(world.IsOnGround(handle), Is.True);
        });
    }

    #endregion
}
