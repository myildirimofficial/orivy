using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Generic;

namespace Orivy.Studio;

internal static class DesignSyntaxHelpers
{
    internal static string? ResolveTargetName(ExpressionSyntax expression) => expression switch
    {
        IdentifierNameSyntax identifier => identifier.Identifier.Text,
        MemberAccessExpressionSyntax { Expression: ThisExpressionSyntax, Name: IdentifierNameSyntax name } => name.Identifier.Text,
        MemberAccessExpressionSyntax { Name: IdentifierNameSyntax name, Expression: var inner } =>
            ResolveTargetName(inner) != null ? name.Identifier.Text : null,
        _ => null
    };

    internal static string? CreationAssignmentTarget(SyntaxNode node)
    {
        var current = node.Parent;
        while (current != null
               && current is not ObjectCreationExpressionSyntax
               && current is not ImplicitObjectCreationExpressionSyntax)
            current = current.Parent;

        return current?.Parent switch
        {
            EqualsValueClauseSyntax { Parent: VariableDeclaratorSyntax declarator } => declarator.Identifier.Text,
            AssignmentExpressionSyntax { Left: var left } => ResolveTargetName(left),
            _ => null
        };
    }

    internal static bool TryParseCollectionInvocation(
        ExpressionSyntax expression,
        out string? controlName,
        out string collectionMember,
        out string methodName,
        out SeparatedSyntaxList<ExpressionSyntax> arguments)
    {
        controlName = null;
        collectionMember = string.Empty;
        methodName = string.Empty;
        arguments = default;

        if (expression is not InvocationExpressionSyntax
            {
                Expression: MemberAccessExpressionSyntax { Name.Identifier.Text: var method } invoke,
                ArgumentList.Arguments: var args
            })
            return false;

        if (method is not ("Add" or "AddRange") || args.Count == 0)
            return false;

        if (invoke.Expression is not MemberAccessExpressionSyntax { Name.Identifier.Text: var member } ownerAccess)
            return false;

        if (member == "Controls")
            return false;

        controlName = ResolveCollectionOwnerName(ownerAccess.Expression);
        if (controlName == null)
            return false;

        collectionMember = member;
        methodName = method;
        var expressions = new List<ExpressionSyntax>(args.Count);
        foreach (var arg in args)
            expressions.Add(arg.Expression);
        arguments = SyntaxFactory.SeparatedList(expressions);
        return true;
    }

    private static string? ResolveCollectionOwnerName(ExpressionSyntax expression) => expression switch
    {
        IdentifierNameSyntax id => id.Identifier.Text,
        MemberAccessExpressionSyntax { Name: IdentifierNameSyntax name, Expression: ThisExpressionSyntax } => name.Identifier.Text,
        MemberAccessExpressionSyntax { Name: IdentifierNameSyntax name, Expression: var inner }
            when ResolveCollectionOwnerName(inner) != null => name.Identifier.Text,
        _ => null
    };
}
