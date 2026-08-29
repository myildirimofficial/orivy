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
/// The reverse of <see cref="CodeGenerator"/>: reads a Designer-code file (either exactly what
/// <see cref="CodeGenerator.Generate"/> produced, or a hand-edited variant of it) back into a
/// <see cref="DesignSurface"/>, so the code view is a real round trip instead of a one-way export.
///
/// Uses Roslyn (<c>Microsoft.CodeAnalysis.CSharp</c>) to parse the file's actual syntax tree rather
/// than a hand-rolled regex/text scanner — the generated code is ordinary C# with object initializers
/// and method-call statements, exactly what a syntax tree is for, and a real parser doesn't come
/// apart the moment whitespace or statement order shifts slightly from one specific text template.
/// </summary>
public static class CodeImporter
{
    private sealed class NodeInfo
    {
        public required string Name;
        public required string Type;
        public string? Text;
        public float X;
        public float Y;
        public float W;
        public float H;
    }

    /// <summary>The name of the <c>partial class</c> the Designer code declares, if any — used to
    /// give the resulting tab a better name than a generic placeholder.</summary>
    public static string? TryGetClassName(string code)
    {
        try
        {
            var tree = CSharpSyntaxTree.ParseText(code);
            return tree.GetCompilationUnitRoot().DescendantNodes()
                .OfType<ClassDeclarationSyntax>()
                .FirstOrDefault()
                ?.Identifier.Text;
        }
        catch (Exception)
        {
            return null;
        }
    }

    /// <summary>
    /// Parses <paramref name="code"/>'s <c>InitializeComponent</c> method and rebuilds
    /// <paramref name="surface"/> from it, replacing whatever the surface currently holds. Returns
    /// the names of any referenced control types that aren't in <see cref="ControlCatalog"/> (skipped,
    /// not fatal) — mirroring <see cref="Orivy.Studio.Persistence.DesignSerializer.Load"/>'s contract.
    /// </summary>
    public static IReadOnlyList<string> Import(DesignSurface surface, string code)
    {
        var tree = CSharpSyntaxTree.ParseText(code);
        var compilationUnit = tree.GetCompilationUnitRoot();

        var initMethod = compilationUnit.DescendantNodes()
            .OfType<MethodDeclarationSyntax>()
            .FirstOrDefault(m => m.Identifier.Text == "InitializeComponent");

        if (initMethod?.Body == null)
            throw new InvalidOperationException("No InitializeComponent() method found in the pasted code.");

        var nodes = new Dictionary<string, NodeInfo>(StringComparer.Ordinal);
        var declarationOrder = new List<string>();
        // Empty parent name means "the design root" (a plain Controls.Add(x) call).
        var addEdges = new List<(string ParentName, string ChildName)>();
        SKSize? clientSize = null;

        foreach (var statement in initMethod.Body.Statements)
        {
            if (statement is not ExpressionStatementSyntax { Expression: var expression })
                continue;

            switch (expression)
            {
                case AssignmentExpressionSyntax
                {
                    Left: IdentifierNameSyntax { Identifier.Text: "ClientSize" },
                    Right: ObjectCreationExpressionSyntax { ArgumentList.Arguments.Count: 2 } sizeCreation
                }:
                    clientSize = new SKSize(
                        ParseFloat(sizeCreation.ArgumentList!.Arguments[0].Expression),
                        ParseFloat(sizeCreation.ArgumentList!.Arguments[1].Expression));
                    break;

                case AssignmentExpressionSyntax
                {
                    Left: IdentifierNameSyntax targetIdentifier,
                    Right: ObjectCreationExpressionSyntax { Initializer: { } initializer } creation
                }:
                    var info = new NodeInfo
                    {
                        Name = targetIdentifier.Identifier.Text,
                        Type = creation.Type is IdentifierNameSyntax typeIdentifier ? typeIdentifier.Identifier.Text : creation.Type.ToString(),
                    };

                    foreach (var member in initializer.Expressions.OfType<AssignmentExpressionSyntax>())
                    {
                        if (member.Left is not IdentifierNameSyntax { Identifier.Text: var propertyName })
                            continue;

                        switch (propertyName)
                        {
                            case "Text" when member.Right is LiteralExpressionSyntax { Token.Value: string text }:
                                info.Text = text;
                                break;
                            case "Location" when member.Right is ObjectCreationExpressionSyntax { ArgumentList.Arguments.Count: 2 } location:
                                info.X = ParseFloat(location.ArgumentList!.Arguments[0].Expression);
                                info.Y = ParseFloat(location.ArgumentList!.Arguments[1].Expression);
                                break;
                            case "Size" when member.Right is ObjectCreationExpressionSyntax { ArgumentList.Arguments.Count: 2 } size:
                                info.W = ParseFloat(size.ArgumentList!.Arguments[0].Expression);
                                info.H = ParseFloat(size.ArgumentList!.Arguments[1].Expression);
                                break;
                        }
                    }

                    nodes[info.Name] = info;
                    declarationOrder.Add(info.Name);
                    break;

                case InvocationExpressionSyntax
                {
                    Expression: MemberAccessExpressionSyntax { Name.Identifier.Text: "Add" } target,
                    ArgumentList.Arguments: { Count: 1 } addArguments
                }:
                    if (addArguments[0].Expression is not IdentifierNameSyntax { Identifier.Text: var childName })
                        break;

                    // Controls.Add(x) → root; {parent}.Controls.Add(x) → nested under parent.
                    string? parentName = target.Expression switch
                    {
                        IdentifierNameSyntax { Identifier.Text: "Controls" } => "",
                        MemberAccessExpressionSyntax { Name.Identifier.Text: "Controls", Expression: IdentifierNameSyntax parentIdentifier } => parentIdentifier.Identifier.Text,
                        _ => null,
                    };

                    if (parentName != null)
                        addEdges.Add((parentName, childName));
                    break;
            }
        }

        if (declarationOrder.Count == 0)
            throw new InvalidOperationException("No control declarations found — is this Designer code Orivy Studio generated?");

        return Rebuild(surface, clientSize, nodes, declarationOrder, addEdges);
    }

    private static IReadOnlyList<string> Rebuild(
        DesignSurface surface,
        SKSize? clientSize,
        Dictionary<string, NodeInfo> nodes,
        List<string> declarationOrder,
        List<(string ParentName, string ChildName)> addEdges)
    {
        var skipped = new List<string>();
        var catalog = ControlCatalog.Discover().ToDictionary(e => e.DisplayName, StringComparer.Ordinal);
        var instances = new Dictionary<string, ElementBase>(StringComparer.Ordinal);
        var usedNames = new HashSet<string>(StringComparer.Ordinal);

        foreach (var name in declarationOrder)
        {
            var info = nodes[name];
            if (!catalog.TryGetValue(info.Type, out var entry))
            {
                skipped.Add(info.Type);
                continue;
            }

            var control = entry.CreateInstance();
            DesignSurface.PrepareForDesign(control);
            // Pasted/hand-edited Designer code isn't guaranteed to declare valid or unique C#
            // identifiers any more than a hand-edited project file is — see DesignNameValidator.
            control.Name = DesignNameValidator.Normalize(info.Name, info.Type, usedNames);
            if (info.Text != null)
                control.Text = info.Text;
            control.Location = new SKPoint(info.X, info.Y);
            control.Size = new SKSize(info.W, info.H);
            instances[name] = control;
        }

        // Only commit to the live surface once parsing has fully succeeded — a half-built tree from
        // a partially-broken paste is worse than leaving the existing design alone.
        surface.Selection.Clear();
        foreach (var existing in surface.DesignedControls.ToList())
            surface.DesignRoot.Controls.Remove(existing);
        surface.Locked.Clear();
        surface.Groups.Clear();

        if (clientSize is { Width: > 0, Height: > 0 })
            surface.DesignRoot.Size = clientSize.Value;

        foreach (var (parentName, childName) in addEdges)
        {
            if (!instances.TryGetValue(childName, out var child))
                continue;

            if (parentName.Length == 0)
            {
                surface.DesignRoot.Controls.Add(child);
            }
            else if (instances.TryGetValue(parentName, out var parent))
            {
                parent.Controls.Add(child);
                surface.Groups.Add(parent);
            }
        }

        surface.Commands.Clear();
        surface.NotifyStructureChanged();
        return skipped;
    }

    private static float ParseFloat(ExpressionSyntax expression)
    {
        // Handles a plain numeric literal and a unary-minus literal (e.g. "-12"); anything stranger
        // in a hand-edited file just falls back to 0 rather than throwing.
        return expression switch
        {
            LiteralExpressionSyntax { Token.Value: int i } => i,
            LiteralExpressionSyntax { Token.Value: float f } => f,
            LiteralExpressionSyntax { Token.Value: double d } => (float)d,
            PrefixUnaryExpressionSyntax { OperatorToken.Text: "-", Operand: LiteralExpressionSyntax { Token.Value: int i } } => -i,
            PrefixUnaryExpressionSyntax { OperatorToken.Text: "-", Operand: LiteralExpressionSyntax { Token.Value: float f } } => -f,
            _ => float.TryParse(expression.ToString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed) ? parsed : 0f,
        };
    }
}
