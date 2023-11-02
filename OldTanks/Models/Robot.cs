using CoolEngine.GraphicalEngine.Core;
using CoolEngine.Services;

namespace OldTanks.Models;

public class Robot : WorldObject
{
    public Robot() : base(GlobalCache<Scene>.Default.GetItemOrDefault("Robot"))
    {
    }
}