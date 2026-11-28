using System.Reflection;
using System.Runtime.CompilerServices;
using CoolEngine.Physics.Shapes;

namespace CoolEngine.Physics.Tests;

[TestFixture]
public sealed class PublicApiTests
{
    [Test]
    public void ExportedTypes_AreExactlyThePlannedOnes()
    {
        // The 13 planned public types, plus PhysicsWorld's nested enumerator (C2's GetEnumerator).
        var expected = new[]
        {
            typeof(BodyDescription), typeof(BodyHandle), typeof(BodyState), typeof(PhysicsQueries),
            typeof(PhysicsSettings), typeof(PhysicsWorld), typeof(PhysicsWorld.BodyEnumerator), typeof(Ray),
            typeof(RayHit), typeof(StepPhase), typeof(PolygonShape), typeof(Shape), typeof(ShapeFace),
            typeof(SphereShape)
        }.Select(type => type.FullName).OrderBy(name => name, StringComparer.Ordinal);

        var exported = typeof(PhysicsWorld).Assembly.GetExportedTypes()
            .Select(type => type.FullName)
            .OrderBy(name => name, StringComparer.Ordinal);

        Assert.That(exported, Is.EqualTo(expected));
    }

    [Test]
    public void Types_DeclareNoStaticFields()
    {
        var offenders = typeof(PhysicsWorld).Assembly.GetTypes()
            .Where(type => type.GetCustomAttribute<CompilerGeneratedAttribute>() == null)
            .SelectMany(type => type.GetFields(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic |
                                               BindingFlags.DeclaredOnly)
                .Where(field => !field.IsLiteral)
                .Select(field => $"{type.FullName}.{field.Name}"))
            .ToArray();

        Assert.That(offenders, Is.Empty);
    }
}
