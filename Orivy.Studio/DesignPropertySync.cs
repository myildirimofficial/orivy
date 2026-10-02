using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Orivy.Controls;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;

namespace Orivy.Studio;

/// <summary>
/// Persists PropertyGrid edits into <c>InitializeComponent</c> for every browsable, writable property
/// that can be expressed as a literal (same surface as the inspector). Layout/text fields also updated
/// here when missing from the file; <see cref="CodeMerger"/> still patches existing layout lines first.
/// </summary>
internal static class DesignPropertySync
{
    /// <summary>Key for form/self assignments (<c>ClientSize =</c>, <c>this.Text =</c>).</summary>
    private const string FormKey = "";

    public static SyntaxNode Sync(
        SyntaxNode root,
        IReadOnlyDictionary<string, ElementBase> live,
        ElementBase designRoot,
        DesignSurface surface,
        string? filePath,
        out bool changed)
    {
        changed = false;
        var method = root.DescendantNodes().OfType<MethodDeclarationSyntax>()
            .FirstOrDefault(m => m.Identifier.Text == "InitializeComponent" && m.Body != null);
        if (method?.Body == null)
            return root;

        var statements = method.Body.Statements.ToList();
        var assignments = IndexPropertyAssignments(statements);
        var initializerProperties = IndexInitializerProperties(root);
        var collectionIndex = DesignCollectionSync.IndexCollectionStatements(statements);

        var queued = new Dictionary<string, List<StatementSyntax>>(StringComparer.Ordinal);
        var queueOrder = new List<string>();
        var removeAssignmentIndices = new HashSet<int>();

        foreach (var (controlName, control) in live)
        {
            if (!DesignPersistScope.ShouldPersistControlToFile(surface, controlName))
                continue;
            changed |= SyncComponent(statements, assignments, initializerProperties, queued, queueOrder, removeAssignmentIndices, surface, controlName, control, filePath);
        }

        changed |= SyncComponent(statements, assignments, initializerProperties, queued, queueOrder, removeAssignmentIndices, surface, FormKey, designRoot, filePath);

        foreach (var (controlName, control) in live)
        {
            if (!DesignPersistScope.ShouldPersistControlToFile(surface, controlName))
                continue;
            changed |= DesignCollectionSync.SyncComponent(
                statements, collectionIndex, queued, queueOrder, surface, controlName, control, $"{controlName}.", filePath);
        }

        if (queueOrder.Count > 0)
        {
            foreach (var key in queueOrder)
            {
                var at = DesignInteractions.PropertyInsertionIndex(statements, key);
                statements.InsertRange(at, queued[key]);
            }
        }

        if (removeAssignmentIndices.Count > 0)
        {
            foreach (var index in removeAssignmentIndices.OrderByDescending(i => i))
                statements.RemoveAt(index);
            changed = true;
        }

        if (!changed)
            return root;

        var updated = method.WithBody(method.Body.WithStatements(SyntaxFactory.List(statements)));
        root = root.ReplaceNode(method, updated);
        return DesignInitializerSync.Patch(root, live, designRoot, surface, filePath, ref changed);
    }

    private static bool SyncComponent(
        List<StatementSyntax> statements,
        Dictionary<(string ControlKey, string Property), int> assignments,
        HashSet<(string ControlKey, string Property)> initializerProperties,
        Dictionary<string, List<StatementSyntax>> queued,
        List<string> queueOrder,
        HashSet<int> removeAssignmentIndices,
        DesignSurface surface,
        string controlKey,
        ElementBase component,
        string? filePath)
    {
        var changed = false;
        var indent = DetectStatementIndent(statements);
        var qualifier = controlKey.Length == 0
            ? DetectFormQualifier(statements)
            : DetectControlQualifier(statements, controlKey);

        void Queue(string text)
        {
            if (!queued.TryGetValue(controlKey, out var list))
            {
                list = new List<StatementSyntax>();
                queued[controlKey] = list;
                queueOrder.Add(controlKey);
            }

            list.Add(DesignInteractions.ParseStatement(text));
        }

        foreach (PropertyDescriptor descriptor in TypeDescriptor.GetProperties(component))
        {
            if (!DesignPersistableProperties.ShouldAttemptPersist(descriptor))
                continue;
            if (DesignPersistableProperties.IsCollectionProperty(descriptor))
                continue;

            object? liveValue;
            try { liveValue = DesignPersistableProperties.GetLiveValue(component, descriptor); }
            catch { continue; }

            var propertyName = descriptor.Name;
            if (propertyName == "Name" && liveValue is string liveName
                && liveName.Equals("designRoot", StringComparison.OrdinalIgnoreCase))
                continue;

            var mapKey = (controlKey, propertyName);

            if (!assignments.TryGetValue(mapKey, out var index))
            {
                if (initializerProperties.Contains(mapKey))
                    continue;

                var userEdited = surface.EditedProperties.Contains(mapKey);
                var newlyAdded = surface.AddedControlNames.Contains(controlKey);
                if (!userEdited && !newlyAdded)
                    continue;

                // An inspector edit is explicit: write it even when it matches a core default.
                // Newly dropped controls still skip defaults so the file does not fill with noise.
                if (!userEdited && !DesignPersistableProperties.ShouldWriteValue(component, descriptor, liveValue))
                    continue;

                if (!DesignPersistableProperties.TryFormat(liveValue, descriptor.PropertyType, filePath, out var newRhs))
                    continue;

                changed = true;
                Queue($"{indent}{qualifier}{propertyName} = {newRhs};");
                continue;
            }

            if (statements[index] is not ExpressionStatementSyntax
                {
                    Expression: AssignmentExpressionSyntax { Right: var existingRight } assignExpr
                } statement)
                continue;

            // Leave every line the user did not edit. Rewriting from the live object deletes or
            // zeroes assignments the importer did not round-trip (Name, TextAlign, MaximumSize, …).
            if (!surface.EditedProperties.Contains(mapKey))
                continue;

            if (CodeImporter.TryReadPropertyValue(existingRight, descriptor.PropertyType, out var fileValue, filePath)
                && DesignPersistableProperties.ValuesEqual(fileValue, liveValue, descriptor.PropertyType))
                continue;

            if (!DesignPersistableProperties.TryFormat(liveValue, descriptor.PropertyType, filePath, out var formatted))
                continue;

            var replacement = SyntaxFactory.ParseExpression(formatted).WithTriviaFrom(existingRight);
            if (replacement.IsEquivalentTo(existingRight))
                continue;

            changed = true;
            statements[index] = statement.WithExpression(assignExpr.WithRight(replacement));
        }

        return changed;
    }

    private static Dictionary<(string ControlKey, string Property), int> IndexPropertyAssignments(
        IReadOnlyList<StatementSyntax> statements)
    {
        var map = new Dictionary<(string, string), int>();
        for (var i = 0; i < statements.Count; i++)
        {
            if (statements[i] is not ExpressionStatementSyntax { Expression: AssignmentExpressionSyntax assign })
                continue;
            if (assign.Parent is InitializerExpressionSyntax)
                continue;

            if (!DesignPersistableProperties.TryParseAssignmentTarget(assign, out var controlName, out var propertyName))
                continue;

            var key = controlName ?? FormKey;
            map[(key, propertyName)] = i;
        }

        return map;
    }

    /// <summary>Properties already set inside <c>new Type { Prop = ... }</c>. Those are patched by
    /// <see cref="DesignInitializerSync"/>; emitting a second statement would duplicate them.</summary>
    private static HashSet<(string ControlKey, string Property)> IndexInitializerProperties(SyntaxNode root)
    {
        var set = new HashSet<(string, string)>();
        foreach (var assign in root.DescendantNodes().OfType<AssignmentExpressionSyntax>())
        {
            if (assign.Parent is not InitializerExpressionSyntax)
                continue;
            if (assign.Left is not IdentifierNameSyntax { Identifier.Text: var propertyName })
                continue;

            var controlName = DesignSyntaxHelpers.CreationAssignmentTarget(assign);
            if (controlName == null)
                continue;

            set.Add((controlName, propertyName));
        }

        return set;
    }

    private static string DetectStatementIndent(IReadOnlyList<StatementSyntax> statements)
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

    private static string DetectControlQualifier(IReadOnlyList<StatementSyntax> statements, string controlKey)
    {
        var withThis = "this." + controlKey + ".";
        var bare = controlKey + ".";
        foreach (var statement in statements)
        {
            var text = statement.ToString();
            if (text.Contains(withThis, StringComparison.Ordinal))
                return withThis;
            if (text.Contains(bare, StringComparison.Ordinal))
                return bare;
        }

        return bare;
    }

    private static string DetectFormQualifier(IReadOnlyList<StatementSyntax> statements)
    {
        foreach (var statement in statements)
        {
            if (statement is ExpressionStatementSyntax
                {
                    Expression: AssignmentExpressionSyntax { Left: MemberAccessExpressionSyntax { Expression: ThisExpressionSyntax } }
                })
                return "this.";
        }

        return string.Empty;
    }
}
