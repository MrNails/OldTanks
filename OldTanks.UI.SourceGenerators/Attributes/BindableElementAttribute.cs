using System;

namespace OldTanks.UI.SourceGenerators.Attributes
{
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
    public sealed class BindableElementAttribute : Attribute
    {
        public BindableElementAttribute(string fromProperty, string toProperty, 
            string toObject, string castObjectAs,
            string sourceTrigger, string sourceTriggerArgs, 
            string converterFrom = null, string converterTo = null)
        { }
    }
}