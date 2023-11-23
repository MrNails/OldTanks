using System.Collections.Generic;
using System.Diagnostics;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using OldTanks.UI.SourceGenerators.Attributes;
using OldTanks.UI.SourceGenerators.Generators.Dto;
using OldTanks.UI.SourceGenerators.Generators.Extensions;

namespace OldTanks.UI.SourceGenerators.Generators;

internal sealed class BindableClassesSyntaxReceiver : ISyntaxReceiver
{
    public ClassDeclarationSyntax? ClassToAugment { get; private set; }
    public AttributeSyntax? AttributeOfClass { get; private set; }
    public string? NameSpace { get; private set; }

    public Dictionary<string, FieldAttributeParams[]> FieldAttributeParamsArray { get; } = new();

    public void OnVisitSyntaxNode(SyntaxNode syntaxNode)
    {
        if (syntaxNode is not ClassDeclarationSyntax cds || cds.Identifier.ValueText != "MainWindow")
            return;

        AttributeOfClass = cds.FindAttributeSyntax<BindableClassAttribute>(ElementFindType.AsChild);

        if (AttributeOfClass == null)
            return;

        ClassToAugment = cds;
        NameSpace = cds.GetFullNameSpace();

        FieldAttributeParamsArray[nameof(BindableElementAttribute)] = syntaxNode.GetAttributeParamsFromNode<BindableElementAttribute>();
        FieldAttributeParamsArray[nameof(TriggerUpdateOnAttribute)] = syntaxNode.GetAttributeParamsFromNode<TriggerUpdateOnAttribute>();
    }
}