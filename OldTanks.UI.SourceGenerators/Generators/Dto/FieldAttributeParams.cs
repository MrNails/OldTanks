using System.Collections.Generic;

namespace OldTanks.UI.SourceGenerators.Generators.Dto
{
    public sealed class FieldAttributeParams
    {
        public string FieldName { get; set; } = null!;
        public List<Dictionary<string, string>> AttributeValues { get; set; } = null!;
    }
}