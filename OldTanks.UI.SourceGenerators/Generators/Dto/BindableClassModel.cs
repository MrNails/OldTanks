using System;

namespace OldTanks.UI.SourceGenerators.Generators.Dto;

/// <summary>
/// What <see cref="BindingGenerator"/> needs from the class it augments: plain values only (no syntax nodes or symbols),
/// compared by value so an edit that does not change them does not regenerate the file.
/// </summary>
internal sealed class BindableClassModel : IEquatable<BindableClassModel>
{
    public BindableClassModel(string nameSpace, string className, EquatableArray<FieldAttributeParams> bindableElements,
        EquatableArray<FieldAttributeParams> triggerUpdateOn)
    {
        NameSpace = nameSpace;
        ClassName = className;
        BindableElements = bindableElements;
        TriggerUpdateOn = triggerUpdateOn;
    }

    public string NameSpace { get; }
    public string ClassName { get; }

    /// <summary>The fields carrying <c>[BindableElement]</c>.</summary>
    public EquatableArray<FieldAttributeParams> BindableElements { get; }

    /// <summary>The fields carrying <c>[TriggerUpdateOn]</c>.</summary>
    public EquatableArray<FieldAttributeParams> TriggerUpdateOn { get; }

    public bool Equals(BindableClassModel? other)
    {
        if (ReferenceEquals(this, other))
            return true;

        return other is not null
               && NameSpace == other.NameSpace
               && ClassName == other.ClassName
               && BindableElements.Equals(other.BindableElements)
               && TriggerUpdateOn.Equals(other.TriggerUpdateOn);
    }

    public override bool Equals(object? obj) => Equals(obj as BindableClassModel);

    public override int GetHashCode()
    {
        unchecked
        {
            var hash = NameSpace.GetHashCode();
            hash = hash * 397 ^ ClassName.GetHashCode();
            hash = hash * 397 ^ BindableElements.GetHashCode();
            hash = hash * 397 ^ TriggerUpdateOn.GetHashCode();
            return hash;
        }
    }
}
