using System;

namespace OldTanks.UI.SourceGenerators.Attributes
{
    public enum BindingWay
    {
        OneWay,
        TwoWay,
        OneWayToSource
    }

    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property, AllowMultiple = true)]
    public sealed class BindableElementAttribute : Attribute
    {
        public BindableElementAttribute(string fromProperty, string toProperty, 
            string toObject, string castObjectAs,
            string sourceTrigger, string sourceTriggerArgs, 
            string converterFrom = null, string converterTo = null,
            BindingWay bindingWay = BindingWay.TwoWay)
        { }
    }
}