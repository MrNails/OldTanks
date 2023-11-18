using System;

namespace OldTanks.UI.SourceGenerators.Attributes;

[AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
public sealed class TriggerUpdateOnAttribute : Attribute
{
    public TriggerUpdateOnAttribute(string triggeredFromEvent, string eventParams, string? checkOnNullElement = null) { }
}