using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using OldTanks.UI.SourceGenerators.Generators.Dto;

namespace OldTanks.UI.SourceGenerators.Generators.Extensions;

internal enum ElementFindType
{
    AsParent,
    AsChild,
    AsParentOrChild
}

internal static class SyntaxNodeExtensions
{
    public static string GetFullNameSpace(this SyntaxNode syntaxNode)
    {
        var nameSpace = string.Empty;

        var potentialNamespaceParent = syntaxNode.Parent;

        // Keep moving "out" of nested classes etc until we get to a namespace
        // or until we run out of parents
        while (potentialNamespaceParent != null &&
               potentialNamespaceParent is not NamespaceDeclarationSyntax
               && potentialNamespaceParent is not FileScopedNamespaceDeclarationSyntax)
        {
            potentialNamespaceParent = potentialNamespaceParent.Parent;
        }

        // Build up the final namespace by looping until we no longer have a namespace declaration
        if (potentialNamespaceParent is BaseNamespaceDeclarationSyntax namespaceParent)
        {
            // We have a namespace. Use that as the type
            nameSpace = namespaceParent.Name.ToString();

            // Keep moving "out" of the namespace declarations until we 
            // run out of nested namespace declarations
            while (true)
            {
                if (namespaceParent.Parent is not NamespaceDeclarationSyntax parent)
                {
                    break;
                }

                // Add the outer namespace as a prefix to the final namespace
                nameSpace = $"{namespaceParent.Name}.{nameSpace}";
                namespaceParent = parent;
            }
        }

        return nameSpace;
    }

    public static AttributeSyntax? FindAttributeSyntax<TAttribute>(this SyntaxNode syntaxNode,
        ElementFindType elementFindType)
        where TAttribute : Attribute
    {
        var potentialNamespaceParent = syntaxNode.Parent;
        var attributeName = typeof(TAttribute).Name;

        if (elementFindType == ElementFindType.AsParent || elementFindType == ElementFindType.AsParentOrChild)
        {
            while (potentialNamespaceParent != null &&
                   potentialNamespaceParent is not AttributeListSyntax)
            {
                potentialNamespaceParent = potentialNamespaceParent.Parent;
            }

            var attrListSyntax = potentialNamespaceParent as AttributeListSyntax;

            if (elementFindType == ElementFindType.AsParent || attrListSyntax != null)
            {
                return attrListSyntax?.Attributes.FirstOrDefault(attributeSyntax => AttributeNamePredicate(attributeSyntax, attributeName));
            }
        }

        return syntaxNode.FindSyntaxNodeInChild<AttributeListSyntax>()?
            .Attributes
            .FirstOrDefault(attributeSyntax => AttributeNamePredicate(attributeSyntax, attributeName));
    }

    public static FieldAttributeParams[] GetAttributeParamsFromNode<TAttribute>(this SyntaxNode node)
    {
        var ctorParams = typeof(TAttribute).GetBiggestCtorParams();

        if (ctorParams.Length == 0)
        {
            return Array.Empty<FieldAttributeParams>();
        }

        var fieldsWithAttributeParams = FindAllFieldsWithAttributeParams(typeof(TAttribute).Name, node);

        var result = new FieldAttributeParams[fieldsWithAttributeParams.Count];
        var idx = 0;
        foreach (var fieldWithAttributeParams in fieldsWithAttributeParams)
        {
            var fieldAttrParams = new FieldAttributeParams
            {
                FieldName = fieldWithAttributeParams.Key,
                AttributeValues = new List<Dictionary<string, string>>()
            };

            foreach (var fieldParams in fieldWithAttributeParams.Value)
            {
                var dictionary = new Dictionary<string, string>();
                for (int i = 0; i < fieldParams.Length && i < ctorParams.Length; i++)
                {
                    var ctorParam = ctorParams[i];
                    var attributeParam = fieldParams[i];

                    if (attributeParam.Contains(':'))
                    {
                        var name = attributeParam.Split(':');
                        dictionary[name[0]] = name[1].Trim(' ');
                    }
                    else
                    {
                        dictionary[ctorParam] = attributeParam;
                    }
                }

                fieldAttrParams.AttributeValues.Add(dictionary);
            }

            result[idx++] = fieldAttrParams;
        }

        return result;
    }

    public static List<string> GetAllChilds(this SyntaxNode syntaxNode, int intend = 0)
    {
        var result = new List<string>();
        result.Add($"{new string(' ', intend)}{syntaxNode} ({syntaxNode.GetType().Name})");

        var potentialChilds = syntaxNode.ChildNodes();

        foreach (var child in potentialChilds)
        {
            result.AddRange(child.GetAllChilds(intend + 1));
        }

        return result;
    }

    private static bool AttributeNamePredicate(AttributeSyntax attributeSyntax, string attributeName)
    {
        var attrName = attributeSyntax.Name.ToString();

        return attributeName.Contains(attrName) || attrName.Contains(attributeName);
    }

    private static TSyntaxNode? FindSyntaxNodeInChild<TSyntaxNode>(this SyntaxNode syntaxNode)
        where TSyntaxNode : SyntaxNode
    {
        if (syntaxNode.GetType() == typeof(TSyntaxNode))
        {
            return (TSyntaxNode)syntaxNode;
        }

        foreach (var childNode in syntaxNode.ChildNodes())
        {
            var node = childNode.FindSyntaxNodeInChild<TSyntaxNode>();

            if (node != null)
            {
                return node;
            }
        }

        return null;
    }

    private static Dictionary<string, List<string[]>> FindAllFieldsWithAttributeParams(string attributeName,
        SyntaxNode node)
    {
        var nodeStack = new Stack<(SyntaxNode Parent, IEnumerator<SyntaxNode> ChildsEnumerator)>(100);
        var result = new Dictionary<string, List<string[]>>();

        var tmpNode = node;
        var nodesEnumerator = tmpNode.ChildNodes().GetEnumerator();
        var isAttrListSyntaxLayer = false;

        //Needs to avoid if statement for code below
        nodeStack.Push((tmpNode, nodesEnumerator));

        do
        {
            (tmpNode, nodesEnumerator) = nodeStack.Pop();

            StartFieldsAttributeFindingTraversal:

            isAttrListSyntaxLayer = false;

            while (nodesEnumerator.MoveNext())
            {
                var child = nodesEnumerator.Current;

                if (child is AttributeListSyntax { Parent: FieldDeclarationSyntax fds } als)
                {
                    isAttrListSyntaxLayer = true;
                    var attribute = als.Attributes.FirstOrDefault(a => AttributeNamePredicate(a, attributeName));

                    if (attribute != null)
                    {
                        var attributeParams = GetParamsForAttributeNode(attribute);
                        var outerVariableSyntax = fds.ChildNodes().FirstOrDefault(n => n is VariableDeclarationSyntax);
                        var innerVariableSyntax = outerVariableSyntax?.ChildNodes()
                            .FirstOrDefault(n => n is VariableDeclaratorSyntax) as VariableDeclaratorSyntax;

                        if (innerVariableSyntax != null)
                        {
                            var innerVariableSyntaxStr = innerVariableSyntax.ToString();
                            if (!result.TryGetValue(innerVariableSyntaxStr, out var values))
                            {
                                values = new List<string[]>();
                                result.Add(innerVariableSyntaxStr, values);
                            }

                            values.Add(attributeParams);
                        }
                    }
                }

                if (!isAttrListSyntaxLayer)
                {
                    nodeStack.Push((tmpNode, nodesEnumerator));

                    tmpNode = child;
                    nodesEnumerator = child.ChildNodes().GetEnumerator();
                    goto StartFieldsAttributeFindingTraversal;
                }
            }
        } while (nodeStack.Count != 0);

        return result;
    }

    private static string[] GetParamsForAttributeNode(AttributeSyntax node)
    {
        var attributeArgsListSyntax = node.ArgumentList?.Arguments;

        if (!attributeArgsListSyntax.HasValue)
        {
            return Array.Empty<string>();
        }

        var result = new string[attributeArgsListSyntax.Value.Count];
        var idx = 0;
        foreach (var attributeArgumentSyntax in attributeArgsListSyntax)
        {
            var nodes = attributeArgumentSyntax.ChildNodes();

            if (nodes.FirstOrDefault() is LiteralExpressionSyntax les)
            {
                result[idx] = les.Token.ValueText;
            }
            else if (nodes.FirstOrDefault() is NameColonSyntax)
            {
                result[idx] = attributeArgumentSyntax.ToString();
            }
            else
            {
                result[idx] = string.Empty;
            }

            idx++;
        }

        return result;
    }
}