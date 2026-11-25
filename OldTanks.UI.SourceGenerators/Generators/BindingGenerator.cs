using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using OldTanks.UI.SourceGenerators.Attributes;
using OldTanks.UI.SourceGenerators.Generators.Dto;
using OldTanks.UI.SourceGenerators.Generators.Extensions;

namespace OldTanks.UI.SourceGenerators.Generators;

[Generator]
internal sealed class BindingGenerator : IIncrementalGenerator
{
    private const string IntendLevel_ = "\t\t";
    private const string MethodBodyIntendLevel_ = "\t\t\t";

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var bindableClasses = context.SyntaxProvider.CreateSyntaxProvider(
            static (node, _) => IsBindableClass(node),
            static (syntaxContext, _) => CreateModel((ClassDeclarationSyntax)syntaxContext.Node));

        // As with the former syntax receiver, the last matching class in syntax order is the one augmented.
        var classToAugment = bindableClasses
            .Collect()
            .Select(static (models, _) => models.IsDefaultOrEmpty ? null : models[models.Length - 1]);

        context.RegisterSourceOutput(classToAugment, (spc, model) =>
        {
            if (model is not null)
            {
                Execute(spc, model);
            }
        });
    }

    private static bool IsBindableClass(SyntaxNode syntaxNode)
    {
        return syntaxNode is ClassDeclarationSyntax cds
               && cds.Identifier.ValueText == "MainWindow"
               && cds.FindAttributeSyntax<BindableClassAttribute>(ElementFindType.AsChild) != null;
    }

    private static BindableClassModel CreateModel(ClassDeclarationSyntax cds)
    {
        return new BindableClassModel(
            cds.GetFullNameSpace(),
            cds.Identifier.ValueText,
            new EquatableArray<FieldAttributeParams>(cds.GetAttributeParamsFromNode<BindableElementAttribute>()),
            new EquatableArray<FieldAttributeParams>(cds.GetAttributeParamsFromNode<TriggerUpdateOnAttribute>()));
    }

    private void Execute(SourceProductionContext context, BindableClassModel classDeclaration)
    {
        var generatedBindsIntializerBuilder = new StringBuilder();
        generatedBindsIntializerBuilder.AppendFormat("{0}partial void InitGeneratedData()\n{0}{{\n", IntendLevel_);

        var bindableElementParams = classDeclaration.BindableElements.AsArray();

        var membersBindingString = CreateMembersBinding(bindableElementParams, generatedBindsIntializerBuilder);
        var updateFromTriggerMethod = CreateUpdateFromTriggerMethod(classDeclaration.TriggerUpdateOn.AsArray(),
            bindableElementParams, generatedBindsIntializerBuilder);

        generatedBindsIntializerBuilder.AppendFormat("{0}}}", IntendLevel_);

        context.AddSource($"{classDeclaration.NameSpace}.{classDeclaration.ClassName}.g.cs", $@"#nullable enable

namespace {classDeclaration.NameSpace} 
{{
    public partial class {classDeclaration.ClassName}
    {{
{generatedBindsIntializerBuilder}

{membersBindingString}

{updateFromTriggerMethod}
    }}
}}
");
    }

    private string CreateUpdateFromTriggerMethod(FieldAttributeParams[] triggerUpdatedOnParams, FieldAttributeParams[] bindableElementParams, 
        StringBuilder generatedBindsIntializerBuilder)
    {
        var updateMethodStr = new StringBuilder();
        
        for (int i = 0; i < triggerUpdatedOnParams.Length; i++)
        {
            var triggerUpdatedOnAttrParam = triggerUpdatedOnParams[i];

            foreach (var attrValues in triggerUpdatedOnAttrParam.AttributeValues)
            {
                updateMethodStr.AppendFormat("{0}private void __{1}UpdateBindingsHandler({2})\n{0}{{\n{3}__{1}UpdateBindings();\n{0}}}\n\n{0}private void __{1}UpdateBindings()\n{0}{{\n",
                IntendLevel_,
                triggerUpdatedOnAttrParam.FieldName,
                attrValues["eventParams"],
                MethodBodyIntendLevel_);

                if (attrValues.TryGetValue("checkOnNullElement", out var elementToCheck))
                {
                    updateMethodStr.AppendFormat("{0}if ({1} is null) return;\n", MethodBodyIntendLevel_, elementToCheck);
                }

                for (int j = 0; j < bindableElementParams.Length; j++)
                {
                    var bindableElementParam = bindableElementParams[j];

                    CreateObjectToSourceAssignment(bindableElementParam, updateMethodStr);
                }

                updateMethodStr.AppendFormat("{0}}}", IntendLevel_);

                generatedBindsIntializerBuilder.AppendFormat("{0}{1}.{2} += __{1}UpdateBindingsHandler;\n{0}__{1}UpdateBindings();\n",
                    MethodBodyIntendLevel_,
                    triggerUpdatedOnAttrParam.FieldName,
                    attrValues["triggeredFromEvent"]);
            }
        }

        return updateMethodStr.ToString();
    }
    
    private string CreateMembersBinding(FieldAttributeParams[] bindableElementParams, StringBuilder generatedBindsIntializerBuilder)
    {
        var objectToSourceBuilders = new Dictionary<string, StringBuilder>();
        var sourceToObjectMethodBuilder = new StringBuilder();

        for (var i = 0; i < bindableElementParams.Length; i++)
        {
            var fieldAttrParam = bindableElementParams[i];

            foreach (var attrValues in fieldAttrParam.AttributeValues)
            {
                var obj = attrValues["toObject"];
                var bindingWay = GetBindingWayFromDictionary(attrValues);

                if (bindingWay is BindingWay.TwoWay or BindingWay.OneWayToSource)
                {
                    generatedBindsIntializerBuilder.AppendFormat("{0}{1}.{2} += __{1}GeneratedBindingMethod;\n",
                    MethodBodyIntendLevel_,
                    fieldAttrParam.FieldName,
                    attrValues["sourceTrigger"]);

                    CreateSourceToObjectMethod(fieldAttrParam, sourceToObjectMethodBuilder);
                }

                if (bindingWay is BindingWay.OneWay or BindingWay.TwoWay)
                {
                    if (!objectToSourceBuilders.TryGetValue(obj, out var objectToSourceBuilder))
                    {
                        objectToSourceBuilder = new StringBuilder($@"
{IntendLevel_}[System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
{IntendLevel_}private void __{obj.Substring(obj.LastIndexOf('.') + 1)}GeneratedBindingMethodToObject(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
{IntendLevel_}{{
{MethodBodyIntendLevel_}var tmpObject = sender as {attrValues["castObjectAs"]};
{MethodBodyIntendLevel_}if (!tmpObject.Equals({AsNullableSafeObject(obj)}) || tmpObject is null) return;
{MethodBodyIntendLevel_}switch(e.PropertyName)
{MethodBodyIntendLevel_}{{
");
                        objectToSourceBuilders.Add(obj, objectToSourceBuilder);
                    }

                    objectToSourceBuilder.AppendFormat("{0}case \"{1}\":\n",
                        MethodBodyIntendLevel_,
                        attrValues["toProperty"]);

                    CreateObjectToSourceAssignment(fieldAttrParam, objectToSourceBuilder, "tmpObject");

                    objectToSourceBuilder.AppendFormat("{0}\tbreak;\n", MethodBodyIntendLevel_);
                }
            }
        }
        
        foreach (var builder in objectToSourceBuilders)
        {
            builder.Value.AppendFormat("{0}}}\n{1}}}", MethodBodyIntendLevel_, IntendLevel_);
        }

        return $"""
                {sourceToObjectMethodBuilder}

                {string.Join("\n", objectToSourceBuilders.Values)}
                """;
    }

    private void CreateObjectToSourceAssignment(FieldAttributeParams bindableElementParams, StringBuilder stringBuilder, string newSourceObject = null)
    {
        foreach (var attrValues in bindableElementParams.AttributeValues)
        {
            var bindingWay = GetBindingWayFromDictionary(attrValues);

            if (bindingWay is BindingWay.OneWayToSource)
                continue;

            if (attrValues.TryGetValue("converterTo", out var converterTo))
            {
                stringBuilder.AppendFormat("{0}\t{1}.{3} = {2}({4}.{5});\n",
                    MethodBodyIntendLevel_,
                    bindableElementParams.FieldName,
                    converterTo,
                    attrValues["fromProperty"],
                    newSourceObject ?? attrValues["toObject"],
                    attrValues["toProperty"]);
            }
            else
            {
                stringBuilder.AppendFormat("{0}\t{1}.{2} = {3}.{4};\n",
                    MethodBodyIntendLevel_,
                    bindableElementParams.FieldName,
                    attrValues["fromProperty"],
                    newSourceObject ?? attrValues["toObject"],
                    attrValues["toProperty"]);
            }
        }
    }

    private void CreateSourceToObjectMethod(FieldAttributeParams bindableElementParams, StringBuilder stringBuilder)
    {
        foreach (var attrValues in bindableElementParams.AttributeValues)
        {
            stringBuilder.AppendFormat(@"
{0}[System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
{0}private void __{1}GeneratedBindingMethod{2}
{0}{{
{3}if ({4} is null) return;
",
            IntendLevel_,
            bindableElementParams.FieldName,
            attrValues["sourceTriggerArgs"],
            MethodBodyIntendLevel_,
            AsNullableSafeObject(attrValues["toObject"]));

            if (attrValues.TryGetValue("converterFrom", out var converterFrom))
            {
                stringBuilder.AppendFormat("{0}{4}.{5} = {2}({1}.{3});\n",
                    MethodBodyIntendLevel_,
                    bindableElementParams.FieldName,
                    converterFrom,
                    attrValues["fromProperty"],
                    attrValues["toObject"],
                    attrValues["toProperty"]);
            }
            else
            {
                stringBuilder.AppendFormat("{0}{3}.{4} = {1}.{2};\n",
                    MethodBodyIntendLevel_,
                    bindableElementParams.FieldName,
                    attrValues["fromProperty"],
                    attrValues["toObject"],
                    attrValues["toProperty"]);
            }

            stringBuilder.AppendFormat("{0}}}\n",
                IntendLevel_);
        }
    }

    private string AsNullableSafeObject(string objectData)
    {
        return objectData.Count(c => c == '.') > 1 ? objectData.Replace(".", "?.") : objectData;
    }

    private BindingWay GetBindingWayFromDictionary(Dictionary<string, string> attrValues)
    {
        attrValues.TryGetValue("bindingWay", out var bindingWayStr);

        var bindingWay = BindingWay.TwoWay;
        if (!string.IsNullOrWhiteSpace(bindingWayStr))
        {
            Enum.TryParse(bindingWayStr.Substring(bindingWayStr.LastIndexOf('.') + 1), out bindingWay);
        }

        return bindingWay;
    }
}