using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Orivy.Controls;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;

namespace Orivy.Studio;

/// <summary>Updates property assignments inside <c>new Type { ... }</c> object initializers.</summary>
internal static class DesignInitializerSync
{
    public static SyntaxNode Patch(
        SyntaxNode root,
        IReadOnlyDictionary<string, ElementBase> live,
        ElementBase designRoot,
        DesignSurface surface,
        string? filePath,
        ref bool changed)
    {
        var patchChanged = false;
        var rewriter = new Rewriter(live, designRoot, surface, filePath, () => patchChanged = true);
        var updated = rewriter.Visit(root) ?? root;
        if (patchChanged)
            changed = true;
        return updated;
    }

    private sealed class Rewriter : CSharpSyntaxRewriter
    {
        private readonly IReadOnlyDictionary<string, ElementBase> _live;
        private readonly ElementBase _designRoot;
        private readonly DesignSurface _surface;
        private readonly string? _filePath;
        private readonly Action _markChanged;

        public Rewriter(
            IReadOnlyDictionary<string, ElementBase> live,
            ElementBase designRoot,
            DesignSurface surface,
            string? filePath,
            Action markChanged)
        {
            _live = live;
            _designRoot = designRoot;
            _surface = surface;
            _filePath = filePath;
            _markChanged = markChanged;
        }

        public override SyntaxNode? VisitAssignmentExpression(AssignmentExpressionSyntax node)
        {
            var visited = (AssignmentExpressionSyntax?)base.VisitAssignmentExpression(node);
            if (visited == null)
                return null;

            if (visited.Parent is not InitializerExpressionSyntax)
                return visited;

            if (visited.Left is not IdentifierNameSyntax { Identifier.Text: var propertyName })
                return visited;

            var controlName = DesignSyntaxHelpers.CreationAssignmentTarget(visited);
            if (controlName == null)
                return visited;

            if (!DesignPersistScope.ShouldPersistControlToFile(_surface, controlName))
                return visited;

            var component = ResolveComponent(controlName);
            if (component == null)
                return visited;

            var descriptor = TypeDescriptor.GetProperties(component)[propertyName];
            if (descriptor == null || !DesignPersistableProperties.ShouldAttemptPersist(descriptor))
                return visited;
            if (DesignPersistableProperties.IsCollectionProperty(descriptor))
                return visited;

            object? liveValue;
            try { liveValue = DesignPersistableProperties.GetLiveValue(component, descriptor); }
            catch { return visited; }

            if (propertyName == "Name" && liveValue is string liveName
                && liveName.Equals("designRoot", StringComparison.OrdinalIgnoreCase))
                return visited;

            if (!_surface.EditedProperties.Contains((controlName, propertyName)))
                return visited;

            if (CodeImporter.TryReadPropertyValue(visited.Right, descriptor.PropertyType, out var fileValue, _filePath)
                && DesignPersistableProperties.ValuesEqual(fileValue, liveValue, descriptor.PropertyType))
                return visited;

            if (!DesignPersistableProperties.TryFormat(liveValue, descriptor.PropertyType, _filePath, out var formatted))
                return visited;

            var replacement = SyntaxFactory.ParseExpression(formatted).WithTriviaFrom(visited.Right);
            if (replacement.IsEquivalentTo(visited.Right))
                return visited;

            _markChanged();
            return visited.WithRight(replacement);
        }

        public override SyntaxNode? VisitInitializerExpression(InitializerExpressionSyntax node)
        {
            var expressions = new List<ExpressionSyntax>(node.Expressions.Count);
            var listChanged = false;

            foreach (var expression in node.Expressions)
            {
                var visited = Visit(expression);
                if (visited is not ExpressionSyntax kept)
                {
                    listChanged = true;
                    continue;
                }

                if (!ReferenceEquals(kept, expression))
                    listChanged = true;
                expressions.Add(kept);
            }

            if (!listChanged)
                return node;

            return node.WithExpressions(SyntaxFactory.SeparatedList(expressions));
        }

        private ElementBase? ResolveComponent(string controlName)
        {
            if (controlName.Length == 0)
                return _designRoot;

            return _live.TryGetValue(controlName, out var control) ? control : null;
        }
    }
}
