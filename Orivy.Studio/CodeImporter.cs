using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Orivy;
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
        public DockStyle Dock;
        public AnchorStyles Anchor = AnchorStyles.Top | AnchorStyles.Left;
        public int ZOrder;
        public bool Visible = true;
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
    /// not fatal). This is the sole load path a design document has — there is no separate project
    /// format to fall back to.
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

        // Recognizes both dialects: the compact object-initializer form CodeGenerator itself emits
        // (`button1 = new Button { Location = ..., ... };`) and the classic WinForms Designer.cs shape
        // most hand-written or Visual-Studio-generated files actually use — `this.` prefixed, a bare
        // `new Type()` declaration followed by separate `this.button1.Property = value;` statements,
        // and `this.Controls.Add(this.button1);`. A real Designer.cs someone already has is exactly
        // the "just files in the folder" case this importer needs to open, not only Studio's own export.
        var declaredFields = compilationUnit.DescendantNodes()
            .OfType<FieldDeclarationSyntax>()
            .SelectMany(f => f.Declaration.Variables)
            .Select(v => v.Identifier.Text)
            .ToHashSet(StringComparer.Ordinal);
        var knownTypeNames = ControlCatalog.Discover().Select(e => e.DisplayName).ToHashSet(StringComparer.Ordinal);

        var nodes = new Dictionary<string, NodeInfo>(StringComparer.Ordinal);
        var declarationOrder = new List<string>();
        // Empty parent name means "the design root" (a plain Controls.Add(x) call).
        var addEdges = new List<(string ParentName, string ChildName)>();
        SKSize? clientSize = null;

        // A third dialect: a control declared and configured entirely as a field initializer —
        // `private readonly Button _save = new() { Text = "Save", ... };` — with InitializeComponent
        // doing nothing but a plain Controls.Add(_save) for it. Plenty of natural, modern C# for a
        // hand-written Orivy app looks exactly like this instead of the WinForms-style split between
        // a field declaration and a separate InitializeComponent assignment; missing it meant
        // InitializeComponent could easily contain zero recognizable declarations at all.
        foreach (var field in compilationUnit.DescendantNodes().OfType<FieldDeclarationSyntax>())
        {
            var fieldTypeName = GetSimpleTypeName(field.Declaration.Type);

            foreach (var variable in field.Declaration.Variables)
            {
                var (creationType, fieldInitializer) = variable.Initializer?.Value switch
                {
                    ObjectCreationExpressionSyntax oce => (GetSimpleTypeName(oce.Type), oce.Initializer),
                    ImplicitObjectCreationExpressionSyntax ioce => (fieldTypeName, ioce.Initializer),
                    _ => (null, null),
                };

                if (creationType == null || !knownTypeNames.Contains(creationType))
                    continue;

                var fieldName = variable.Identifier.Text;
                var fieldInfo = new NodeInfo { Name = fieldName, Type = creationType };
                nodes[fieldName] = fieldInfo;
                declarationOrder.Add(fieldName);

                if (fieldInitializer != null)
                {
                    foreach (var member in fieldInitializer.Expressions.OfType<AssignmentExpressionSyntax>())
                    {
                        if (member.Left is IdentifierNameSyntax { Identifier.Text: var propertyName })
                            ApplyProperty(fieldInfo, propertyName, member.Right);
                    }
                }
            }
        }

        foreach (var statement in initMethod.Body.Statements)
        {
            if (statement is not ExpressionStatementSyntax { Expression: var expression })
                continue;

            switch (expression)
            {
                // Studio's own generator always writes the root's size as `ClientSize = ...` (see
                // CodeGenerator), but a real hand-written or Visual-Studio-generated UserControl
                // Designer.cs — exactly what a non-Form component like a WinForms UserControl (and
                // its Orivy equivalent, a root `Container`) actually emits — uses a bare `Size = ...`
                // for that same self-assignment instead. Recognizing only "ClientSize" meant importing
                // one of those files silently kept the design root at its default size instead of the
                // one the file actually specifies (`this` here since the left side of a bare `Size =`
                // has no receiver — it isn't a stray decl and isn't a member access, so it fell through
                // every other case with no effect at all).
                case AssignmentExpressionSyntax { Left: var clientSizeLeft, Right: ObjectCreationExpressionSyntax { ArgumentList.Arguments.Count: 2 } sizeCreation }
                    when ResolveTargetName(clientSizeLeft) is "ClientSize" or "Size":
                    clientSize = new SKSize(
                        ParseFloat(sizeCreation.ArgumentList!.Arguments[0].Expression),
                        ParseFloat(sizeCreation.ArgumentList!.Arguments[1].Expression));
                    break;

                // A declaration: `x = new Type();` / `this.x = new Type();`, with or without an object
                // initializer. Only counts as a control if `x` is a field the class actually declares,
                // or (a partial paste with no field declarations in scope) its type is a known control
                // — otherwise an ordinary Form-level property assignment that happens to construct a
                // value (`this.ClientSize = new Size(...)`, `this.AutoScaleDimensions = new SizeF(...)`)
                // would get misread as declaring a bogus control named "ClientSize"/"AutoScaleDimensions".
                case AssignmentExpressionSyntax { Left: var declLeft, Right: ObjectCreationExpressionSyntax creation }
                    when ResolveTargetName(declLeft) is { } declName
                         && (declaredFields.Contains(declName)
                             || (declaredFields.Count == 0 && knownTypeNames.Contains(GetSimpleTypeName(creation.Type)))):
                    if (!nodes.TryGetValue(declName, out var info))
                    {
                        info = new NodeInfo { Name = declName, Type = GetSimpleTypeName(creation.Type) };
                        nodes[declName] = info;
                        declarationOrder.Add(declName);
                    }
                    else
                    {
                        info.Type = GetSimpleTypeName(creation.Type);
                    }

                    if (creation.Initializer != null)
                    {
                        foreach (var member in creation.Initializer.Expressions.OfType<AssignmentExpressionSyntax>())
                        {
                            if (member.Left is IdentifierNameSyntax { Identifier.Text: var propertyName })
                                ApplyProperty(info, propertyName, member.Right);
                        }
                    }
                    break;

                // A separate property-assignment statement for an already-declared control — the
                // classic WinForms shape (`this.button1.Location = new Point(10, 10);`) sets each
                // property one statement at a time instead of in one object initializer.
                case AssignmentExpressionSyntax { Left: MemberAccessExpressionSyntax { Name: IdentifierNameSyntax propName } memberLeft, Right: var propValue }
                    when ResolveTargetName(memberLeft.Expression) is { } targetName && nodes.TryGetValue(targetName, out var existingInfo):
                    ApplyProperty(existingInfo, propName.Identifier.Text, propValue);
                    break;

                case InvocationExpressionSyntax
                {
                    Expression: MemberAccessExpressionSyntax { Name.Identifier.Text: "Add" } target,
                    ArgumentList.Arguments: { Count: 1 } addArguments
                }:
                    if (ResolveTargetName(addArguments[0].Expression) is not { } childName)
                        break;
                    if (ResolveControlsParentName(target.Expression) is { } addParentName)
                        addEdges.Add((addParentName, childName));
                    break;

                // Visual Studio's own WinForms designer emits one `AddRange(new Control[] { a, b, c })`
                // per parent instead of individual `.Add()` calls whenever a container ends up with more
                // than one child — a real Designer.cs someone hand-wrote or exported from VS is at least
                // as likely to use this form as individual Add() calls. Missing it doesn't fail loudly:
                // every control still gets declared and typed, so the import "succeeds" with an empty
                // canvas, since nothing ever recorded that any of them belonged under a parent.
                case InvocationExpressionSyntax
                {
                    Expression: MemberAccessExpressionSyntax { Name.Identifier.Text: "AddRange" } rangeTarget,
                    ArgumentList.Arguments: { Count: 1 } rangeArguments
                }:
                    if (ResolveControlsParentName(rangeTarget.Expression) is not { } rangeParentName)
                        break;

                    var elements = rangeArguments[0].Expression switch
                    {
                        ArrayCreationExpressionSyntax { Initializer: { } arrayInit } => arrayInit.Expressions,
                        ImplicitArrayCreationExpressionSyntax { Initializer: { } implicitInit } => implicitInit.Expressions,
                        InitializerExpressionSyntax bareInit => bareInit.Expressions,
                        _ => default,
                    };

                    foreach (var element in elements)
                    {
                        if (ResolveTargetName(element) is { } rangeChildName)
                            addEdges.Add((rangeParentName, rangeChildName));
                    }
                    break;
            }
        }

        if (declarationOrder.Count == 0)
            throw new InvalidOperationException("No control declarations found — is this Designer code Orivy Studio generated?");

        // A control field isn't always declared with a catalog type directly — a codebase's own
        // `class ThemedButton : Button` (or a chain of those) is just as likely as a bare `Button`.
        // Only chains fully declared within this same file/paste are resolvable at all here (there's
        // no project-wide symbol resolution), but that covers the common "one subclass, same file"
        // case; ResolveKnownAncestorType below also falls back to a name-suffix guess for anything
        // that still doesn't resolve, so a subclass defined elsewhere isn't necessarily lost either.
        var localBaseTypeOf = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var classDecl in compilationUnit.DescendantNodes().OfType<ClassDeclarationSyntax>())
        {
            var baseType = classDecl.BaseList?.Types.FirstOrDefault();
            if (baseType != null)
                localBaseTypeOf[classDecl.Identifier.Text] = GetSimpleTypeName(baseType.Type);
        }

        return Rebuild(surface, clientSize, nodes, declarationOrder, addEdges, localBaseTypeOf);
    }

    /// <summary>Extracts a plain field/variable name from either a bare identifier (<c>button1</c>) or
    /// a one-level <c>this.</c>-qualified access (<c>this.button1</c>) — the two shapes Studio's own
    /// generated code and classic WinForms Designer.cs respectively use to refer to a declared field.
    /// Anything deeper (e.g. <c>this.button1.Location</c>) is a property access, not a target name, and
    /// correctly falls through to null here.</summary>
    private static string? ResolveTargetName(ExpressionSyntax expression) => expression switch
    {
        IdentifierNameSyntax id => id.Identifier.Text,
        MemberAccessExpressionSyntax { Expression: ThisExpressionSyntax, Name: IdentifierNameSyntax name } => name.Identifier.Text,
        _ => null,
    };

    /// <summary>Given the receiver of a <c>.Add(...)</c>/<c>.AddRange(...)</c> call (e.g. the
    /// <c>X.Controls</c> in <c>X.Controls.Add(y)</c>), resolves which control's child collection this
    /// is — <c>""</c> for the design root's own <c>Controls</c>, a control's name for a nested one, or
    /// <c>null</c> if this isn't a <c>Controls</c> access at all. Shared by both call shapes since the
    /// "whose Controls is this" question is identical either way.</summary>
    private static string? ResolveControlsParentName(ExpressionSyntax controlsAccess) => controlsAccess switch
    {
        IdentifierNameSyntax { Identifier.Text: "Controls" } => "",
        MemberAccessExpressionSyntax { Expression: ThisExpressionSyntax, Name.Identifier.Text: "Controls" } => "",
        MemberAccessExpressionSyntax { Name.Identifier.Text: "Controls", Expression: var parentExpr } when ResolveTargetName(parentExpr) is { } p => p,
        _ => null,
    };

    /// <summary>The catalog-comparable simple name of a type reference, whether written bare
    /// (<c>Button</c>) or fully qualified (<c>Orivy.Controls.Button</c>, parsed as a
    /// <see cref="QualifiedNameSyntax"/>) — real hand-written Designer.cs is not guaranteed to use the
    /// same unqualified style Studio's own generator always emits.</summary>
    private static string GetSimpleTypeName(TypeSyntax type) => type switch
    {
        QualifiedNameSyntax q => q.Right.Identifier.Text,
        GenericNameSyntax g => g.Identifier.Text,
        IdentifierNameSyntax id => id.Identifier.Text,
        _ => type.ToString(),
    };

    private static void ApplyProperty(NodeInfo info, string propertyName, ExpressionSyntax right)
    {
        switch (propertyName)
        {
            case "Text" when right is LiteralExpressionSyntax { Token.Value: string text }:
                info.Text = text;
                break;
            case "Location" when right is ObjectCreationExpressionSyntax { ArgumentList.Arguments.Count: 2 } location:
                info.X = ParseFloat(location.ArgumentList!.Arguments[0].Expression);
                info.Y = ParseFloat(location.ArgumentList!.Arguments[1].Expression);
                break;
            case "Size" when right is ObjectCreationExpressionSyntax { ArgumentList.Arguments.Count: 2 } size:
                info.W = ParseFloat(size.ArgumentList!.Arguments[0].Expression);
                info.H = ParseFloat(size.ArgumentList!.Arguments[1].Expression);
                break;
            case "Dock":
                info.Dock = ParseDock(right);
                break;
            case "Anchor":
                info.Anchor = ParseAnchor(right);
                break;
            case "ZOrder" when right is LiteralExpressionSyntax:
                info.ZOrder = (int)ParseFloat(right);
                break;
            case "Visible":
                info.Visible = !right.IsKind(SyntaxKind.FalseLiteralExpression);
                break;
        }
    }

    private static IReadOnlyList<string> Rebuild(
        DesignSurface surface,
        SKSize? clientSize,
        Dictionary<string, NodeInfo> nodes,
        List<string> declarationOrder,
        List<(string ParentName, string ChildName)> addEdges,
        Dictionary<string, string> localBaseTypeOf)
    {
        var skipped = new List<string>();
        var catalog = ControlCatalog.Discover().ToDictionary(e => e.DisplayName, StringComparer.Ordinal);
        var instances = new Dictionary<string, ElementBase>(StringComparer.Ordinal);
        var usedNames = new HashSet<string>(StringComparer.Ordinal);

        foreach (var name in declarationOrder)
        {
            var info = nodes[name];
            if (!catalog.TryGetValue(info.Type, out var entry)
                && !catalog.TryGetValue(ResolveKnownAncestorType(info.Type, localBaseTypeOf, catalog), out entry))
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
            control.Dock = info.Dock;
            control.Anchor = info.Anchor;
            control.ZOrder = info.ZOrder;
            control.Visible = info.Visible;
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

    /// <summary>Best-effort resolution of an unrecognized declared type down to the nearest catalog
    /// type it's actually built on, so a codebase's own control subclasses import as their closest
    /// known ancestor instead of getting silently dropped. First walks <paramref name="localBaseTypeOf"/>
    /// (base-type chains for classes declared in this same file/paste — the only ones a syntax-only
    /// importer can actually see); if that doesn't reach a catalog name (most often because the
    /// subclass's own declaration lives in a different file this importer never saw), falls back to
    /// the longest catalog type name that <paramref name="typeName"/> ends with (e.g. "ThemedButton"
    /// → "Button") — a heuristic, but subclasses overwhelmingly name themselves after what they extend.
    /// Returns <paramref name="typeName"/> unchanged if neither approach finds anything.</summary>
    private static string ResolveKnownAncestorType(string typeName, Dictionary<string, string> localBaseTypeOf, Dictionary<string, ControlEntry> catalog)
    {
        var current = typeName;
        var guard = 0;
        while (guard++ < 16 && localBaseTypeOf.TryGetValue(current, out var baseType))
        {
            if (catalog.ContainsKey(baseType))
                return baseType;
            current = baseType;
        }

        var bestMatch = catalog.Keys
            .Where(known => typeName.Length > known.Length && typeName.EndsWith(known, StringComparison.Ordinal))
            .OrderByDescending(known => known.Length)
            .FirstOrDefault();

        return bestMatch ?? typeName;
    }

    private static float ParseFloat(ExpressionSyntax expression)
    {
        // Older/VS-generated Designer.cs commonly wraps numeric literals in redundant parens and/or
        // casts (e.g. `((int)(800D))`, seen in ClientSize/Location/Size assignments) — unwrapping them
        // first matters a lot here: falling through to the string-literal parse below on the ORIGINAL
        // (un-unwrapped) expression text (e.g. "((int)(800D))") isn't valid float syntax and silently
        // parsed as 0, which is how a real form's ClientSize ended up looking like it was never set.
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
                    goto unwrapped;
            }
        }

        unwrapped:
        // Handles a plain numeric literal and a unary-minus literal (e.g. "-12"); anything stranger
        // in a hand-edited file just falls back to 0 rather than throwing.
        return expression switch
        {
            LiteralExpressionSyntax { Token.Value: int i } => i,
            LiteralExpressionSyntax { Token.Value: float f } => f,
            LiteralExpressionSyntax { Token.Value: double d } => (float)d,
            PrefixUnaryExpressionSyntax { OperatorToken.Text: "-", Operand: var operand } => -ParseFloat(operand),
            _ => float.TryParse(expression.ToString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed) ? parsed : 0f,
        };
    }

    /// <summary>Parses a <c>DockStyle.Xxx</c> member access (as emitted by <see cref="CodeGenerator"/>);
    /// anything else in a hand-edited file just falls back to <see cref="DockStyle.None"/>.</summary>
    private static DockStyle ParseDock(ExpressionSyntax expression) =>
        expression is MemberAccessExpressionSyntax { Name.Identifier.Text: var name } && Enum.TryParse<DockStyle>(name, out var dock)
            ? dock
            : DockStyle.None;

    /// <summary>Parses a single <c>AnchorStyles.Xxx</c> member access or an <c>A | B | ...</c> chain of
    /// them (as emitted by <see cref="CodeGenerator"/>); anything unrecognized in a hand-edited file
    /// just falls back to <see cref="AnchorStyles.None"/> for that term.</summary>
    private static AnchorStyles ParseAnchor(ExpressionSyntax expression)
    {
        if (expression is BinaryExpressionSyntax { OperatorToken.Text: "|" } binary)
            return ParseAnchor(binary.Left) | ParseAnchor(binary.Right);

        return expression is MemberAccessExpressionSyntax { Name.Identifier.Text: var name } && Enum.TryParse<AnchorStyles>(name, out var anchor)
            ? anchor
            : AnchorStyles.None;
    }
}
