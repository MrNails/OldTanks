using System;
using System.Linq;
using System.Reflection;

namespace OldTanks.UI.SourceGenerators.Generators.Extensions;

public static class TypeExtensions
{
    public static string[] GetBiggestCtorParams(this Type type)
    {
        var ctors = type
            .GetConstructors()
            .GroupBy(k => k.Name, v => v.GetParameters());

        IGrouping<string, ParameterInfo[]> biggestCtor = null;
        var biggestCtorCount = -1;
        foreach (var ctor in ctors)
        {
            var tmpCount = ctor.Count();
            if (biggestCtorCount < tmpCount)
            {
                biggestCtorCount = tmpCount;
                biggestCtor = ctor;
            }
        }

        return biggestCtor?.SelectMany(p => p.Select(par => par.Name!))
            .ToArray() ?? Array.Empty<string>();
    }
}