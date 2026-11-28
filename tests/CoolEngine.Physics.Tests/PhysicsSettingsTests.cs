using System.Numerics;

namespace CoolEngine.Physics.Tests;

[TestFixture]
public sealed class PhysicsSettingsTests
{
    [TestCase(0)]
    [TestCase(129)]
    public void Constructor_SubStepCountOutOfRange_ThrowsArgumentOutOfRangeException(int subStepCount)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            _ = new PhysicsSettings(new Vector3(0, -1, 0), 9.80665f, 88.25985f, subStepCount));
    }

    [TestCase(-2f)]
    [TestCase(float.NaN)]
    public void Constructor_GravityDirectionNotUnitOrNotFinite_ThrowsArgumentOutOfRangeException(float y)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            _ = new PhysicsSettings(new Vector3(0, y, 0), 9.80665f, 88.25985f, 5));
    }

    [TestCase(0f, 9.80665f)]
    [TestCase(88.25985f, -1f)]
    public void Constructor_TerminalSpeedOrAccelerationNotPositive_ThrowsArgumentOutOfRangeException(float terminalSpeed,
        float gravityAcceleration)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            _ = new PhysicsSettings(new Vector3(0, -1, 0), gravityAcceleration, terminalSpeed, 5));
    }
}
