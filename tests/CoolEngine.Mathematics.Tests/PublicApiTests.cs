using System.Numerics;
using System.Reflection;

namespace CoolEngine.Mathematics.Tests;

[TestFixture]
public sealed class PublicApiTests
{
    private static readonly Type[] s_rowVectorTypes = [typeof(Matrix4x4), typeof(Matrix3x2), typeof(Plane)];

    [Test]
    public void PublicSurface_NeverUsesRowVectorTypes()
    {
        const BindingFlags declared = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static
                                      | BindingFlags.DeclaredOnly;
        var offenders = new List<string>();

        foreach (var type in typeof(Matrix4).Assembly.GetExportedTypes())
        {
            Check(type.BaseType, $"{type.FullName}: base type", offenders);
            foreach (var implemented in type.GetInterfaces())
                Check(implemented, $"{type.FullName}: interface {implemented.Name}", offenders);

            foreach (var field in type.GetFields(declared).Where(f => f.IsPublic || f.IsFamily || f.IsFamilyOrAssembly))
                Check(field.FieldType, $"{type.FullName}.{field.Name}: field type", offenders);

            foreach (var property in type.GetProperties(declared))
            {
                var accessors = property.GetAccessors(nonPublic: true);
                if (!accessors.Any(IsVisible))
                    continue;

                Check(property.PropertyType, $"{type.FullName}.{property.Name}: property type", offenders);
                foreach (var parameter in property.GetIndexParameters())
                    Check(parameter.ParameterType, $"{type.FullName}.{property.Name}: index parameter {parameter.Name}", offenders);
            }

            foreach (var method in type.GetMethods(declared).Where(IsVisible))
            {
                Check(method.ReturnType, $"{type.FullName}.{method.Name}: return type", offenders);
                foreach (var parameter in method.GetParameters())
                    Check(parameter.ParameterType, $"{type.FullName}.{method.Name}: parameter {parameter.Name}", offenders);
            }

            foreach (var constructor in type.GetConstructors(declared).Where(IsVisible))
            {
                foreach (var parameter in constructor.GetParameters())
                    Check(parameter.ParameterType, $"{type.FullName}..ctor: parameter {parameter.Name}", offenders);
            }

            foreach (var @event in type.GetEvents(declared))
            {
                if (@event.GetAddMethod(nonPublic: true) is { } add && IsVisible(add))
                    Check(@event.EventHandlerType, $"{type.FullName}.{@event.Name}: event type", offenders);
            }
        }

        Assert.That(offenders, Is.Empty, "Row-vector System.Numerics types in the public API:\n" + string.Join("\n", offenders));
    }

    // Public or protected (protected internal counts: other assemblies can reach it).
    private static bool IsVisible(MethodBase method)
    {
        return method.IsPublic || method.IsFamily || method.IsFamilyOrAssembly;
    }

    private static void Check(Type? type, string where, List<string> offenders)
    {
        if (type is not null && UsesRowVectorType(type))
            offenders.Add($"{where} is {type}");
    }

    // The type itself, the element of an array, pointer or by-ref, or any generic argument.
    private static bool UsesRowVectorType(Type type)
    {
        if (s_rowVectorTypes.Contains(type))
            return true;

        if (type.HasElementType && type.GetElementType() is { } element && UsesRowVectorType(element))
            return true;

        return type.IsGenericType && type.GetGenericArguments().Any(UsesRowVectorType);
    }
}
