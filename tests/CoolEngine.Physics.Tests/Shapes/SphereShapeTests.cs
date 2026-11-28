using CoolEngine.Physics.Shapes;

namespace CoolEngine.Physics.Tests.Shapes;

[TestFixture]
public sealed class SphereShapeTests
{
    [Test]
    public void Constructor_Sphere_WritesNothingToConsole()
    {
        var originalOut = Console.Out;
        var captured = new StringWriter();
        Console.SetOut(captured);

        try
        {
            _ = new SphereShape();
        }
        finally
        {
            Console.SetOut(originalOut);
        }

        Assert.That(captured.ToString(), Is.Empty);
    }
}
