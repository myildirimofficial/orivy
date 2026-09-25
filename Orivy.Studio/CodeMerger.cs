using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Orivy.Controls;
using Orivy.Studio.Toolbox;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Orivy.Studio;

/// <summary>
/// Writes a design back into the file it was opened from. Unlike <see cref="CodeGenerator.Generate"/>
/// which generates a bare-bones stub from scratch, this patches the existing syntax tree in place:
/// updating only the modified coordinates, sizes, dock and anchor properties while leaving everything
/// else (event handlers, custom methods, menu trees, comments, formatting) untouched.
/// </summary>
public static class CodeMerger
{
    private static readonly string[] TrackedProperties = { "Location", "Size", "Dock", "Anchor", "Text", "Visible" };

    public static string Apply(string? original, DesignSurface surface, string className)
    {
        if (string.IsNullOrWhiteSpace(original))
            return CodeGenerator.Generate(surface, className);

        var root = CSharpSyntaxTree.ParseText(original).GetCompilationUnitRoot();
        var initMethod = root.DescendantNodes().OfType<MethodDeclarationSyntax>()
            .FirstOrDefault(m => m.Identifier.Text == "InitializeComponent");
        if (initMethod == null)
            return original;

        var live = new Dictionary<string, ElementBase>(StringComparer.Ordinal);
        foreach (var control in surface.AllDesignedControls)
        {
            if (!string.IsNullOrEmpty(control.Name))
                live[control.Name] = control;
        }

        var added = surface.AllDesignedControls
            .Where(control => !string.IsNullOrEmpty(control.Name) && surface.AddedControlNames.Contains(control.Name))
            .ToList();

        var deleted = new HashSet<string>(surface.DeletedControlNames, StringComparer.Ordinal);

        var rewriter = new DesignSourceRewriter(live, deleted, added, surface.DesignRoot);
        var rewritten = rewriter.Visit(root);
        if (rewritten == null || !rewriter.Changed)
            return original;

        return rewritten.ToFullString();
    }

    private static string? ResolveTargetName(ExpressionSyntax expression) => expression switch
    {
        IdentifierNameSyntax identifier => identifier.Identifier.Text,
        MemberAccessExpressionSyntax { Expression: ThisExpressionSyntax, Name: IdentifierNameSyntax name } => name.Identifier.Text,
        _ => null
    };

    private sealed class DesignSourceRewriter : CSharpSyntaxRewriter
    {
        private readonly Dictionary<string, ElementBase> _live;
        private readonly HashSet<string> _deleted;
        private readonly List<ElementBase> _added;
        private readonly ElementBase _root;

        public bool Changed { get; private set; }

        public DesignSourceRewriter(
            Dictionary<string, ElementBase> live,
            HashSet<string> deleted,
            List<ElementBase> added,
            ElementBase root)
        {
            _live = live;
            _deleted = deleted;
            _added = added;
            _root = root;
        }

        public override SyntaxNode? VisitClassDeclaration(ClassDeclarationSyntax node)
        {
            var visited = (ClassDeclarationSyntax)base.VisitClassDeclaration(node)!;
            if (_added.Count == 0)
                return visited;
            if (!visited.Members.OfType<MethodDeclarationSyntax>().Any(m => m.Identifier.Text == "InitializeComponent"))
                return visited;

            var existingDeclared = new HashSet<string>(
                visited.Members.OfType<FieldDeclarationSyntax>()
                    .SelectMany(f => f.Declaration.Variables.Select(v => v.Identifier.Text)),
                StringComparer.Ordinal);

            var trulyNewFields = _added.Where(c => !existingDeclared.Contains(c.Name)).ToList();
            if (trulyNewFields.Count == 0)
                return visited;

            var insertAt = 0;
            for (var i = 0; i < visited.Members.Count; i++)
            {
                if (visited.Members[i] is FieldDeclarationSyntax)
                    insertAt = i + 1;
            }

            Changed = true;
            var fields = trulyNewFields.Select(control =>
                (MemberDeclarationSyntax)SyntaxFactory.ParseMemberDeclaration(
                    $"    private {control.GetType().Name} {control.Name} = null!;{Environment.NewLine}")!);
            return visited.WithMembers(visited.Members.InsertRange(insertAt, fields));
        }

        public override SyntaxNode? VisitMethodDeclaration(MethodDeclarationSyntax node)
        {
            var visited = (MethodDeclarationSyntax)base.VisitMethodDeclaration(node)!;
            if (visited.Identifier.Text != "InitializeComponent" || visited.Body == null || _added.Count == 0)
                return visited;

            var statements = visited.Body.Statements;
            var insertAt = statements.Count;
            for (var i = statements.Count - 1; i >= 0; i--)
            {
                if (statements[i] is ExpressionStatementSyntax
                    {
                        Expression: InvocationExpressionSyntax
                        {
                            Expression: MemberAccessExpressionSyntax { Name.Identifier.Text: "ResumeLayout" }
                        }
                    })
                {
                    insertAt = i;
                    break;
                }
            }

            Changed = true;
            var extra = new List<StatementSyntax>();
            foreach (var control in _added)
            {
                extra.Add(SyntaxFactory.ParseStatement(BuildCreation(control) + Environment.NewLine));
                extra.Add(SyntaxFactory.ParseStatement(BuildAdd(control) + Environment.NewLine));
            }

            return visited.WithBody(visited.Body.WithStatements(statements.InsertRange(insertAt, extra)));
        }

        public override SyntaxNode? VisitFieldDeclaration(FieldDeclarationSyntax node)
        {
            if (_deleted.Count == 0)
                return base.VisitFieldDeclaration(node);

            var kept = node.Declaration.Variables.Where(v => !_deleted.Contains(v.Identifier.Text)).ToList();
            if (kept.Count == node.Declaration.Variables.Count)
                return base.VisitFieldDeclaration(node);
            Changed = true;
            if (kept.Count == 0)
                return null;

            return node.WithDeclaration(node.Declaration.WithVariables(SyntaxFactory.SeparatedList(kept)));
        }

        public override SyntaxNode? VisitExpressionStatement(ExpressionStatementSyntax node)
        {
            if (_deleted.Count > 0)
            {
                if (TryRewriteAddRange(node, out var rewritten))
                {
                    Changed = true;
                    return rewritten;
                }

                if (IsDeletedControlStatement(node))
                {
                    Changed = true;
                    return null;
                }
            }

            return base.VisitExpressionStatement(node);
        }

        public override SyntaxNode? VisitAssignmentExpression(AssignmentExpressionSyntax node)
        {
            var visited = (AssignmentExpressionSyntax)base.VisitAssignmentExpression(node)!;
            if (TryMatchFormSize(visited))
            {
                if (SamePair(visited.Right, _root.Width, _root.Height))
                    return visited;
                Changed = true;
                return visited.WithRight(SizeExpression(visited.Right, _root.Width, _root.Height).WithTriviaFrom(visited.Right));
            }

            if (!TryMatchControlProperty(visited, out var control, out var property))
                return visited;

            if (SameProperty(visited.Right, control, property))
                return visited;

            var replacement = PropertyExpression(visited.Right, control, property);
            if (replacement.IsEquivalentTo(visited.Right))
                return visited;

            Changed = true;
            return visited.WithRight(replacement.WithTriviaFrom(visited.Right));
        }

        private bool TryRewriteAddRange(ExpressionStatementSyntax node, out StatementSyntax? rewritten)
        {
            rewritten = null;
            if (node.Expression is not InvocationExpressionSyntax
                {
                    Expression: MemberAccessExpressionSyntax { Name.Identifier.Text: "AddRange" } target,
                    ArgumentList.Arguments: { Count: 1 } arguments
                })
                return false;
            if (ResolveControlsParent(target.Expression) == null)
                return false;

            var elements = arguments[0].Expression switch
            {
                ArrayCreationExpressionSyntax { Initializer: { } arrayInit } => arrayInit.Expressions,
                ImplicitArrayCreationExpressionSyntax { Initializer: { } implicitInit } => implicitInit.Expressions,
                InitializerExpressionSyntax bare => bare.Expressions,
                _ => default
            };
            if (elements.Count == 0)
                return false;

            var kept = elements.Where(element => ResolveTargetName(element) is not { } name || !_deleted.Contains(name)).ToList();
            if (kept.Count == elements.Count)
                return false;
            if (kept.Count == 0)
            {
                rewritten = null;
                return true;
            }

            var initializer = arguments[0].Expression switch
            {
                ArrayCreationExpressionSyntax array => array.WithInitializer(array.Initializer!.WithExpressions(SyntaxFactory.SeparatedList(kept))),
                ImplicitArrayCreationExpressionSyntax implicitArray => implicitArray.WithInitializer(implicitArray.Initializer.WithExpressions(SyntaxFactory.SeparatedList(kept))),
                InitializerExpressionSyntax bare => bare.WithExpressions(SyntaxFactory.SeparatedList(kept)),
                _ => arguments[0].Expression
            };
            var invocation = (InvocationExpressionSyntax)node.Expression;
            rewritten = node.WithExpression(invocation.WithArgumentList(
                invocation.ArgumentList.WithArguments(
                    SyntaxFactory.SingletonSeparatedList(arguments[0].WithExpression(initializer)))));
            return true;
        }

        private bool IsDeletedControlStatement(ExpressionStatementSyntax node)
        {
            switch (node.Expression)
            {
                case AssignmentExpressionSyntax { Left: var left } when ResolveTargetName(left) is { } created && _deleted.Contains(created):
                    return true;
                case AssignmentExpressionSyntax { Left: MemberAccessExpressionSyntax member }
                    when ResolveTargetName(member.Expression) is { } owner && _deleted.Contains(owner):
                    return true;
                case InvocationExpressionSyntax
                {
                    Expression: MemberAccessExpressionSyntax { Name.Identifier.Text: "Add" } target,
                    ArgumentList.Arguments: { Count: 1 } arguments
                }
                    when ResolveControlsParent(target.Expression) != null
                         && ResolveTargetName(arguments[0].Expression) is { } child
                         && _deleted.Contains(child):
                    return true;
                default:
                    return false;
            }
        }

        private bool TryMatchFormSize(AssignmentExpressionSyntax assignment)
        {
            if (assignment.Parent is InitializerExpressionSyntax)
                return false;
            if (assignment.Right is not ObjectCreationExpressionSyntax { ArgumentList.Arguments.Count: 2 }
                && assignment.Right is not ImplicitObjectCreationExpressionSyntax { ArgumentList.Arguments.Count: 2 })
                return false;

            return assignment.Left switch
            {
                IdentifierNameSyntax { Identifier.Text: "ClientSize" or "Size" } => true,
                MemberAccessExpressionSyntax { Expression: ThisExpressionSyntax, Name.Identifier.Text: "ClientSize" or "Size" } => true,
                _ => false
            };
        }

        private bool TryMatchControlProperty(AssignmentExpressionSyntax assignment, out ElementBase control, out string property)
        {
            control = null!;
            property = string.Empty;

            if (assignment.Left is MemberAccessExpressionSyntax { Name: IdentifierNameSyntax memberName } member
                && IsTracked(memberName.Identifier.Text)
                && ResolveTargetName(member.Expression) is { } owner
                && _live.TryGetValue(owner, out control!))
            {
                property = memberName.Identifier.Text;
                return true;
            }

            if (assignment.Left is IdentifierNameSyntax { Identifier.Text: var initializerProperty }
                && IsTracked(initializerProperty)
                && CreationTargetName(assignment) is { } created
                && _live.TryGetValue(created, out control!))
            {
                property = initializerProperty;
                return true;
            }

            return false;
        }

        private static bool IsTracked(string property) =>
            TrackedProperties.Contains(property, StringComparer.Ordinal);

        private static string? CreationTargetName(SyntaxNode node)
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

        private static string? ResolveControlsParent(ExpressionSyntax controlsAccess) => controlsAccess switch
        {
            IdentifierNameSyntax { Identifier.Text: "Controls" } => "",
            MemberAccessExpressionSyntax { Expression: ThisExpressionSyntax, Name.Identifier.Text: "Controls" } => "",
            MemberAccessExpressionSyntax { Name.Identifier.Text: "Controls", Expression: var parent } when ResolveTargetName(parent) is { } name => name,
            _ => null
        };

        private string BuildCreation(ElementBase control)
        {
            var text = string.IsNullOrEmpty(control.Text) ? string.Empty : $", Text = \"{Escape(control.Text)}\"";
            var visible = control.Visible ? string.Empty : ", Visible = false";
            return $"        {control.Name} = new {control.GetType().Name} {{ Name = \"{Escape(control.Name)}\"{text}, Location = new SKPoint({F(control.Location.X)}, {F(control.Location.Y)}), Size = new SKSize({F(control.Width)}, {F(control.Height)}), Dock = DockStyle.{control.Dock}, Anchor = {FormatAnchor(control.Anchor)}{visible} }};";
        }

        private string BuildAdd(ElementBase control)
        {
            var parent = control.Parent;
            if (parent == null || ReferenceEquals(parent, _root))
                return $"        Controls.Add({control.Name});";

            return $"        {parent.Name}.Controls.Add({control.Name});";
        }

        private static bool SameProperty(ExpressionSyntax existing, ElementBase control, string property)
        {
            return property switch
            {
                "Location" => SamePair(existing, control.Location.X, control.Location.Y),
                "Size" => SamePair(existing, control.Width, control.Height),
                "Dock" => existing is MemberAccessExpressionSyntax { Name.Identifier.Text: var name }
                    && name == control.Dock.ToString(),
                "Anchor" => string.Equals(existing.ToString().Replace(" ", string.Empty),
                    FormatAnchor(control.Anchor).Replace(" ", string.Empty), StringComparison.Ordinal),
                "Text" => existing is LiteralExpressionSyntax { Token.Value: string text }
                    && text == (control.Text ?? string.Empty),
                "Visible" => (existing.IsKind(SyntaxKind.TrueLiteralExpression) && control.Visible)
                    || (existing.IsKind(SyntaxKind.FalseLiteralExpression) && !control.Visible),
                _ => true
            };
        }

        private static bool SamePair(ExpressionSyntax existing, float a, float b)
        {
            var args = existing switch
            {
                ObjectCreationExpressionSyntax { ArgumentList.Arguments.Count: 2 } creation => creation.ArgumentList.Arguments,
                ImplicitObjectCreationExpressionSyntax { ArgumentList.Arguments.Count: 2 } implicitCreation => implicitCreation.ArgumentList.Arguments,
                _ => default
            };
            if (args.Count != 2)
                return false;

            return NearlyEqual(ParseNumeric(args[0].Expression), a)
                && NearlyEqual(ParseNumeric(args[1].Expression), b);
        }

        private static float ParseNumeric(ExpressionSyntax expression)
        {
            while (true)
            {
                switch (expression)
                {
                    case ParenthesizedExpressionSyntax paren:
                        expression = paren.Expression;
                        continue;
                    case CastExpressionSyntax cast:
                        expression = cast.Expression;
                        continue;
                    default:
                        return float.TryParse(expression.ToString().TrimEnd('f', 'F', 'd', 'D'),
                            NumberStyles.Float, CultureInfo.InvariantCulture, out var value)
                            ? value
                            : float.NaN;
                }
            }
        }

        private static bool NearlyEqual(float left, float right) =>
            !float.IsNaN(left) && Math.Abs(left - right) < 0.51f;

        private static ExpressionSyntax PropertyExpression(ExpressionSyntax existing, ElementBase control, string property)
        {
            return property switch
            {
                "Location" => SizeExpression(existing, control.Location.X, control.Location.Y, "SKPoint"),
                "Size" => SizeExpression(existing, control.Width, control.Height, "SKSize"),
                "Dock" => MemberUpdate(existing, "DockStyle", control.Dock.ToString()),
                "Anchor" => SyntaxFactory.ParseExpression(FormatAnchor(control.Anchor)),
                "Text" => SyntaxFactory.LiteralExpression(SyntaxKind.StringLiteralExpression, SyntaxFactory.Literal(control.Text ?? string.Empty)),
                "Visible" => SyntaxFactory.ParseExpression(control.Visible ? "true" : "false"),
                _ => existing
            };
        }

        private static ExpressionSyntax SizeExpression(ExpressionSyntax existing, float a, float b, string fallbackType = "SKSize")
        {
            // Keep the existing `new Type` / `new()` node and only replace its arguments. Building a
            // fresh ObjectCreationExpression drops the space trivia after `new`, so ToFullString()
            // emits `newSKSize(...)`.
            var argumentList = SyntaxFactory.ParseArgumentList($"({F(a)}, {F(b)})");
            return existing switch
            {
                ImplicitObjectCreationExpressionSyntax implicitCreation =>
                    implicitCreation.WithArgumentList(argumentList.WithTriviaFrom(implicitCreation.ArgumentList)),
                ObjectCreationExpressionSyntax creation =>
                    creation.WithArgumentList(argumentList.WithTriviaFrom(creation.ArgumentList)),
                _ => SyntaxFactory.ParseExpression($"new {fallbackType}({F(a)}, {F(b)})")
            };
        }

        private static ExpressionSyntax MemberUpdate(ExpressionSyntax existing, string typeName, string member)
        {
            if (existing is MemberAccessExpressionSyntax access)
                return access.WithName(SyntaxFactory.IdentifierName(member));

            return SyntaxFactory.ParseExpression($"{typeName}.{member}");
        }

        private static string FormatAnchor(AnchorStyles anchor)
        {
            if (anchor == AnchorStyles.None)
                return "AnchorStyles.None";

            var flags = Enum.GetValues<AnchorStyles>()
                .Where(flag => flag != AnchorStyles.None && anchor.HasFlag(flag))
                .Select(flag => $"AnchorStyles.{flag}");
            return string.Join(" | ", flags);
        }

        private static string F(float value)
        {
            var rounded = MathF.Round(value);
            return Math.Abs(value - rounded) < 0.01f
                ? ((int)rounded).ToString(CultureInfo.InvariantCulture)
                : value.ToString("0.##", CultureInfo.InvariantCulture);
        }

        private static string Escape(string value) =>
            value.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n").Replace("\r", string.Empty);
    }
}
