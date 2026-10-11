using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Orivy.Controls;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Orivy.Studio;

/// <summary>
/// Bring-to-front and layer reorders change <see cref="ElementBase.ZOrder"/> only. Import rebuilds
/// stacking from <c>Controls.Add</c> / <c>AddRange</c> order (later add is on top), so Save permutes
/// the child names already in those calls. It does not insert or delete add lines.
/// </summary>
internal static class DesignZOrderSync
{
    public static SyntaxNode Sync(SyntaxNode root, DesignSurface surface, out bool changed)
    {
        changed = false;
        if (surface.ZOrderEditedControls.Count == 0)
            return root;

        var method = root.DescendantNodes().OfType<MethodDeclarationSyntax>()
            .FirstOrDefault(m => m.Identifier.Text == "InitializeComponent" && m.Body != null);
        if (method?.Body == null)
            return root;

        var live = new Dictionary<string, ElementBase>(StringComparer.Ordinal);
        foreach (var control in surface.AllDesignedControls)
        {
            if (!string.IsNullOrEmpty(control.Name))
                live[control.Name] = control;
        }

        var groups = new Dictionary<string, List<AddSite>>(StringComparer.Ordinal);
        foreach (var statement in method.Body.Statements)
        {
            if (statement is not ExpressionStatementSyntax { Expression: InvocationExpressionSyntax call })
                continue;
            if (call.Expression is not MemberAccessExpressionSyntax { Name.Identifier.Text: var methodName } access)
                continue;

            if (methodName == "Add"
                && call.ArgumentList.Arguments.Count == 1
                && ParentOfControls(access.Expression) is { } parent
                && ChildName(call.ArgumentList.Arguments[0].Expression) is { } child
                && NameNode(call.ArgumentList.Arguments[0].Expression) is { } nameNode)
            {
                Add(groups, parent, nameNode, child);
            }
            else if (methodName == "AddRange"
                && call.ArgumentList.Arguments.Count == 1
                && ParentOfControls(access.Expression) is { } rangeParent)
            {
                foreach (var element in RangeElements(call.ArgumentList.Arguments[0].Expression))
                {
                    if (ChildName(element) is { } elementChild && NameNode(element) is { } elementName)
                        Add(groups, rangeParent, elementName, elementChild);
                }
            }
        }

        var replacements = new Dictionary<SyntaxNode, SyntaxNode>();
        foreach (var (parent, sites) in groups)
        {
            if (!sites.Any(site => surface.ZOrderEditedControls.Contains(site.ChildName)))
                continue;

            var editable = new List<int>();
            for (var i = 0; i < sites.Count; i++)
            {
                if (!live.TryGetValue(sites[i].ChildName, out var control))
                    continue;
                if (control.Parent is not ElementBase parentControl || ParentKey(parentControl, surface) != parent)
                    continue;
                if (!surface.IsEditableInThisDocument(control))
                    continue;

                editable.Add(i);
            }

            if (editable.Count < 2)
                continue;

            var desired = editable
                .Select(index => live[sites[index].ChildName])
                .OrderBy(control => control.ZOrder)
                .Select(control => control.Name!)
                .ToList();
            var current = editable.Select(index => sites[index].ChildName).ToList();
            if (current.SequenceEqual(desired))
                continue;

            for (var i = 0; i < editable.Count; i++)
            {
                if (current[i] == desired[i])
                    continue;

                var node = sites[editable[i]].NameNode;
                replacements[node] = SyntaxFactory.IdentifierName(desired[i]).WithTriviaFrom(node);
            }
        }

        if (replacements.Count == 0)
            return root;

        changed = true;
        return root.ReplaceNodes(replacements.Keys, (original, _) => replacements[original]);
    }

    private static void Add(Dictionary<string, List<AddSite>> groups, string parent, IdentifierNameSyntax nameNode, string child)
    {
        if (!groups.TryGetValue(parent, out var list))
        {
            list = new List<AddSite>();
            groups[parent] = list;
        }

        list.Add(new AddSite(nameNode, child));
    }

    private static string ParentKey(ElementBase parent, DesignSurface surface) =>
        ReferenceEquals(parent, surface.DesignRoot) ? string.Empty : parent.Name ?? string.Empty;

    private static string? ParentOfControls(ExpressionSyntax controlsAccess) => controlsAccess switch
    {
        IdentifierNameSyntax { Identifier.Text: "Controls" } => string.Empty,
        MemberAccessExpressionSyntax { Expression: ThisExpressionSyntax, Name.Identifier.Text: "Controls" } => string.Empty,
        MemberAccessExpressionSyntax { Name.Identifier.Text: "Controls", Expression: IdentifierNameSyntax parent } => parent.Identifier.Text,
        MemberAccessExpressionSyntax { Name.Identifier.Text: "Controls", Expression: MemberAccessExpressionSyntax { Expression: ThisExpressionSyntax, Name: IdentifierNameSyntax parent } } => parent.Identifier.Text,
        _ => null
    };

    private static string? ChildName(ExpressionSyntax expression) => expression switch
    {
        IdentifierNameSyntax identifier => identifier.Identifier.Text,
        MemberAccessExpressionSyntax { Expression: ThisExpressionSyntax, Name: IdentifierNameSyntax name } => name.Identifier.Text,
        _ => null
    };

    private static IdentifierNameSyntax? NameNode(ExpressionSyntax expression) => expression switch
    {
        IdentifierNameSyntax identifier => identifier,
        MemberAccessExpressionSyntax { Expression: ThisExpressionSyntax, Name: IdentifierNameSyntax name } => name,
        _ => null
    };

    private static IEnumerable<ExpressionSyntax> RangeElements(ExpressionSyntax expression)
    {
        var elements = expression switch
        {
            ArrayCreationExpressionSyntax { Initializer: { } arrayInit } => arrayInit.Expressions,
            ImplicitArrayCreationExpressionSyntax { Initializer: { } implicitInit } => implicitInit.Expressions,
            InitializerExpressionSyntax bare => bare.Expressions,
            _ => default
        };

        foreach (var element in elements)
            yield return element;
    }

    private sealed class AddSite
    {
        public AddSite(IdentifierNameSyntax nameNode, string childName)
        {
            NameNode = nameNode;
            ChildName = childName;
        }

        public IdentifierNameSyntax NameNode { get; }
        public string ChildName { get; }
    }
}
