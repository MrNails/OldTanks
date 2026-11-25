using System;
using System.Collections.Generic;

namespace OldTanks.UI.SourceGenerators.Generators.Dto
{
    /// <summary>
    /// The attribute arguments of one field. Compared by value (field name, then each attribute's argument dictionary in order)
    /// so the incremental generator can tell an unchanged model from a changed one.
    /// </summary>
    public sealed class FieldAttributeParams : IEquatable<FieldAttributeParams>
    {
        public string FieldName { get; set; } = null!;
        public List<Dictionary<string, string>> AttributeValues { get; set; } = null!;

        public bool Equals(FieldAttributeParams? other)
        {
            if (ReferenceEquals(this, other))
                return true;

            if (other is null || FieldName != other.FieldName || AttributeValues.Count != other.AttributeValues.Count)
                return false;

            for (var i = 0; i < AttributeValues.Count; i++)
            {
                if (!DictionaryEquals(AttributeValues[i], other.AttributeValues[i]))
                    return false;
            }

            return true;
        }

        public override bool Equals(object? obj) => Equals(obj as FieldAttributeParams);

        public override int GetHashCode()
        {
            unchecked
            {
                return (FieldName?.GetHashCode() ?? 0) * 397 ^ AttributeValues.Count;
            }
        }

        private static bool DictionaryEquals(Dictionary<string, string> left, Dictionary<string, string> right)
        {
            if (left.Count != right.Count)
                return false;

            foreach (var pair in left)
            {
                if (!right.TryGetValue(pair.Key, out var value) || value != pair.Value)
                    return false;
            }

            return true;
        }
    }
}
