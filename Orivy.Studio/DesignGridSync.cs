using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Orivy.Controls;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Orivy.Studio;

/// <summary>
/// Grid cells live on the parent via <c>SetPlacement</c>, not on the child. Save updates or inserts
/// that call only when the user moved a child between cells or dropped it into a non-default cell.
/// </summary>
internal static class DesignGridSync
{
    public static SyntaxNode Sync(SyntaxNode root, DesignSurface surface, out bool changed)
    {
        changed = false;
        var method = root.DescendantNodes().OfType<MethodDeclarationSyntax>()
            .FirstOrDefault(m => m.Identifier.Text == "InitializeComponent" && m.Body != null);
        if (method?.Body == null)
            return root;

        var replacements = new Dictionary<InvocationExpressionSyntax, InvocationExpressionSyntax>();
        var inserts = new List<(int Index, StatementSyntax Statement)>();
        var statements = method.Body.Statements;

        foreach (var control in surface.AllDesignedControls)
        {
            if (control.Parent is not Grid grid || string.IsNullOrEmpty(control.Name) || string.IsNullOrEmpty(grid.Name))
                continue;
            if (!surface.IsEditableInThisDocument(control))
                continue;

            var edited = surface.EditedGridPlacements.Contains(control.Name);
            var added = surface.AddedControlNames.Contains(control.Name);
            if (!edited && !added)
                continue;

            var placement = grid.GetPlacement(control);
            if (!edited && placement == GridPlacement.Default)
                continue;

            var existing = FindSetPlacement(method, control.Name);
            if (existing != null)
            {
                var updated = WithPlacementArguments(existing, placement);
                if (!updated.IsEquivalentTo(existing))
                    replacements[existing] = updated;
                continue;
            }

            var indent = DetectStatementIndent(statements);
            var text = $"{indent}{Qualify(statements, grid.Name)}.SetPlacement({Qualify(statements, control.Name)}, {placement.Row}, {placement.Column}, {placement.RowSpan}, {placement.ColumnSpan});";
            inserts.Add((InsertionIndex(statements, control.Name), SyntaxFactory.ParseStatement(text + Environment.NewLine)));
        }

        if (replacements.Count == 0 && inserts.Count == 0)
            return root;

        changed = true;
        if (replacements.Count > 0)
        {
            root = root.ReplaceNodes(replacements.Keys, (original, _) =>
                original is InvocationExpressionSyntax invocation && replacements.TryGetValue(invocation, out var updated)
                    ? updated
                    : original);
            method = root.DescendantNodes().OfType<MethodDeclarationSyntax>()
                .First(m => m.Identifier.Text == "InitializeComponent" && m.Body != null);
        }

        if (inserts.Count == 0)
            return root;

        var next = method.Body!.Statements.ToList();
        foreach (var group in inserts.GroupBy(item => item.Index).OrderByDescending(group => group.Key))
            next.InsertRange(group.Key, group.Select(item => item.Statement));

        var updatedMethod = method.WithBody(method.Body!.WithStatements(SyntaxFactory.List(next)));
        return root.ReplaceNode(method, updatedMethod);
    }

    private static InvocationExpressionSyntax? FindSetPlacement(MethodDeclarationSyntax method, string childName)
    {
        foreach (var call in method.DescendantNodes().OfType<InvocationExpressionSyntax>())
        {
            if (call.Expression is not MemberAccessExpressionSyntax { Name.Identifier.Text: "SetPlacement" })
                continue;
            if (call.ArgumentList.Arguments.Count < 3)
                continue;
            if (ChildName(call.ArgumentList.Arguments[0].Expression) == childName)
                return call;
        }

        return null;
    }

    private static InvocationExpressionSyntax WithPlacementArguments(InvocationExpressionSyntax call, GridPlacement placement)
    {
        var arguments = call.ArgumentList.Arguments.ToList();
        SetNumber(arguments, 1, placement.Row);
        SetNumber(arguments, 2, placement.Column);
        SetNumber(arguments, 3, placement.RowSpan);
        SetNumber(arguments, 4, placement.ColumnSpan);

        var separated = SyntaxFactory.SeparatedList(arguments);
        return call.WithArgumentList(call.ArgumentList.WithArguments(separated));
    }

    private static void SetNumber(List<ArgumentSyntax> arguments, int index, int value)
    {
        var literal = SyntaxFactory.LiteralExpression(
            SyntaxKind.NumericLiteralExpression,
            SyntaxFactory.Literal(value));
        if (index < arguments.Count)
        {
            arguments[index] = arguments[index].WithExpression(literal.WithTriviaFrom(arguments[index].Expression));
            return;
        }

        arguments.Add(SyntaxFactory.Argument(literal));
    }

    private static int InsertionIndex(SyntaxList<StatementSyntax> statements, string childName)
    {
        for (var i = statements.Count - 1; i >= 0; i--)
        {
            if (statements[i] is ExpressionStatementSyntax { Expression: InvocationExpressionSyntax call }
                && call.Expression is MemberAccessExpressionSyntax { Name.Identifier.Text: "Add" or "AddRange" }
                && call.ArgumentList.Arguments.Any(argument => ChildName(argument.Expression) == childName))
                return i + 1;
        }

        return DesignInteractions.PropertyInsertionIndex(statements, childName);
    }

    private static string Qualify(SyntaxList<StatementSyntax> statements, string name)
    {
        var withThis = "this." + name;
        foreach (var statement in statements)
        {
            if (statement.ToString().Contains(withThis, StringComparison.Ordinal))
                return withThis;
        }

        return name;
    }

    private static string DetectStatementIndent(SyntaxList<StatementSyntax> statements)
    {
        foreach (var statement in statements)
        {
            var text = statement.GetLeadingTrivia().ToFullString();
            var start = text.Length;
            while (start > 0 && text[start - 1] is ' ' or '\t')
                start--;
            if (start < text.Length)
                return text[start..];
        }

        return "        ";
    }

    private static string? ChildName(ExpressionSyntax expression) => expression switch
    {
        IdentifierNameSyntax identifier => identifier.Identifier.Text,
        MemberAccessExpressionSyntax { Expression: ThisExpressionSyntax, Name: IdentifierNameSyntax name } => name.Identifier.Text,
        _ => null
    };
}
