using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Orivy.Studio;

/// <summary>
/// Rewrites a control's identifier in the designer file after a PropertyGrid rename.
/// Member names (<c>button.Text</c>) stay; the receiver, field, and <c>this.button</c> change.
/// </summary>
internal static class DesignRename
{
    public static CompilationUnitSyntax Apply(CompilationUnitSyntax root, string from, string to)
    {
        if (from.Length == 0 || to.Length == 0 || from == to)
            return root;

        return (CompilationUnitSyntax)(new Rewriter(from, to).Visit(root) ?? root);
    }

    private sealed class Rewriter : CSharpSyntaxRewriter
    {
        private readonly string _from;
        private readonly string _to;

        public Rewriter(string from, string to)
        {
            _from = from;
            _to = to;
        }

        public override SyntaxNode? VisitIdentifierName(IdentifierNameSyntax node)
        {
            var visited = (IdentifierNameSyntax)base.VisitIdentifierName(node)!;
            if (visited.Identifier.Text != _from)
                return visited;
            if (visited.Parent is QualifiedNameSyntax)
                return visited;
            if (visited.Parent is MemberAccessExpressionSyntax access
                && access.Name == visited
                && access.Expression is not ThisExpressionSyntax)
                return visited;

            return visited.WithIdentifier(Renamed(visited.Identifier));
        }

        public override SyntaxNode? VisitVariableDeclarator(VariableDeclaratorSyntax node)
        {
            var visited = (VariableDeclaratorSyntax)base.VisitVariableDeclarator(node)!;
            if (node.Identifier.Text != _from)
                return visited;

            return visited.WithIdentifier(Renamed(node.Identifier));
        }

        public override SyntaxNode? VisitParameter(ParameterSyntax node)
        {
            var visited = (ParameterSyntax)base.VisitParameter(node)!;
            if (node.Identifier.Text != _from)
                return visited;

            return visited.WithIdentifier(Renamed(node.Identifier));
        }

        private SyntaxToken Renamed(SyntaxToken token) =>
            SyntaxFactory.Identifier(token.LeadingTrivia, _to, token.TrailingTrivia);
    }
}
