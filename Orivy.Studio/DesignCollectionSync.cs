using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Orivy.Controls;
using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;

namespace Orivy.Studio;

/// <summary>Syncs <c>control.Items.Add</c> / <c>AddRange</c> blocks with live collection contents.</summary>
internal static class DesignCollectionSync
{
    public static bool SyncComponent(
        List<StatementSyntax> statements,
        Dictionary<(string ControlKey, string CollectionMember), List<int>> collectionStatements,
        Dictionary<string, List<StatementSyntax>> queued,
        List<string> queueOrder,
        DesignSurface surface,
        string controlKey,
        ElementBase component,
        string assignmentPrefix,
        string? filePath)
    {
        var changed = false;
        var remove = new HashSet<int>();

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
            if (!DesignPersistableProperties.IsCollectionProperty(descriptor) || descriptor.IsReadOnly)
                continue;
            if (!DesignPersistableProperties.ShouldShowInInspector(descriptor))
                continue;

            object? collectionObject;
            try { collectionObject = descriptor.GetValue(component); }
            catch { continue; }

            if (collectionObject is not IEnumerable enumerable)
                continue;

            var liveItems = new List<object?>();
            foreach (var item in enumerable)
                liveItems.Add(item);

            var memberName = descriptor.Name;
            var key = (controlKey, memberName);
            collectionStatements.TryGetValue(key, out var existingIndices);
            existingIndices ??= new List<int>();

            if (!surface.EditedProperties.Contains((controlKey, memberName))
                && !surface.AddedControlNames.Contains(controlKey))
                continue;

            if (existingIndices.Count == 0 && !surface.AddedControlNames.Contains(controlKey))
                continue;

            var elementType = InferElementType(descriptor.PropertyType, liveItems);
            var existingElements = FlattenExistingElements(statements, existingIndices, elementType, filePath);

            if (SequencesEqual(existingElements, liveItems, elementType, filePath))
                continue;

            if (liveItems.Count == 0)
            {
                foreach (var index in existingIndices)
                    remove.Add(index);
                changed = true;
                continue;
            }

            var formatted = new List<string>();
            foreach (var item in liveItems)
            {
                if (item == null)
                {
                    formatted.Clear();
                    break;
                }

                if (!DesignPersistableProperties.TryFormat(item, elementType, filePath, out var expr))
                {
                    formatted.Clear();
                    break;
                }

                formatted.Add(expr);
            }

            if (formatted.Count != liveItems.Count)
                continue;

            foreach (var index in existingIndices)
                remove.Add(index);

            changed = true;
            if (formatted.Count == 1)
            {
                Queue($"        {assignmentPrefix}{memberName}.Add({formatted[0]});");
            }
            else
            {
                var arrayType = FormatArrayType(elementType);
                Queue($"        {assignmentPrefix}{memberName}.AddRange(new {arrayType}[] {{ {string.Join(", ", formatted)} }});");
            }
        }

        if (remove.Count > 0)
        {
            for (var i = statements.Count - 1; i >= 0; i--)
            {
                if (remove.Contains(i))
                    statements.RemoveAt(i);
            }

            ReindexAfterRemoval(collectionStatements, remove);
        }

        return changed;
    }

    internal static Dictionary<(string ControlKey, string CollectionMember), List<int>> IndexCollectionStatements(
        IReadOnlyList<StatementSyntax> statements)
    {
        var map = new Dictionary<(string, string), List<int>>();
        for (var i = 0; i < statements.Count; i++)
        {
            if (statements[i] is not ExpressionStatementSyntax { Expression: var expression })
                continue;

            if (!DesignSyntaxHelpers.TryParseCollectionInvocation(
                    expression,
                    out var controlName,
                    out var collectionMember,
                    out _,
                    out _))
                continue;

            var key = (controlName ?? string.Empty, collectionMember);
            if (!map.TryGetValue(key, out var list))
            {
                list = new List<int>();
                map[key] = list;
            }

            list.Add(i);
        }

        return map;
    }

    private static List<object?> FlattenExistingElements(
        IReadOnlyList<StatementSyntax> statements,
        List<int> indices,
        Type elementType,
        string? filePath)
    {
        var values = new List<object?>();
        foreach (var index in indices.OrderBy(i => i))
        {
            if (statements[index] is not ExpressionStatementSyntax { Expression: var expression })
                continue;

            if (!DesignSyntaxHelpers.TryParseCollectionInvocation(
                    expression,
                    out _,
                    out _,
                    out var method,
                    out var args))
                continue;

            if (method == "Add" && args.Count == 1)
            {
                if (CodeImporter.TryEvaluatePropertyExpression(args[0], elementType, out var value, filePath))
                    values.Add(value);
                continue;
            }

            if (method != "AddRange")
                continue;

            foreach (var argExpr in ExpandAddRangeArguments(args))
            {
                if (CodeImporter.TryEvaluatePropertyExpression(argExpr, elementType, out var value, filePath))
                    values.Add(value);
            }
        }

        return values;
    }

    private static IEnumerable<ExpressionSyntax> ExpandAddRangeArguments(SeparatedSyntaxList<ExpressionSyntax> args)
    {
        if (args.Count != 1)
            yield break;

        var expressions = args[0] switch
        {
            ArrayCreationExpressionSyntax { Initializer: { } init } => init.Expressions,
            ImplicitArrayCreationExpressionSyntax { Initializer: { } implicitInit } => implicitInit.Expressions,
            InitializerExpressionSyntax bare => bare.Expressions,
            _ => default(SeparatedSyntaxList<ExpressionSyntax>)
        };

        foreach (var expression in expressions)
            yield return expression;
    }

    private static bool SequencesEqual(
        IReadOnlyList<object?> fileValues,
        IReadOnlyList<object?> liveValues,
        Type elementType,
        string? filePath)
    {
        if (fileValues.Count != liveValues.Count)
            return false;

        for (var i = 0; i < liveValues.Count; i++)
        {
            if (!DesignPersistableProperties.ValuesEqual(fileValues[i], liveValues[i], elementType))
                return false;
        }

        return true;
    }

    private static Type InferElementType(Type collectionPropertyType, IReadOnlyList<object?> items)
    {
        if (items.FirstOrDefault(i => i != null) is { } sample)
            return sample.GetType();

        if (collectionPropertyType.IsGenericType)
            return collectionPropertyType.GetGenericArguments()[0];

        return typeof(object);
    }

    private static string FormatArrayType(Type elementType) =>
        elementType == typeof(object) ? "object" : elementType.Name;

    private static void ReindexAfterRemoval(
        Dictionary<(string ControlKey, string CollectionMember), List<int>> map,
        HashSet<int> removed)
    {
        foreach (var key in map.Keys.ToList())
        {
            map[key] = map[key].Where(i => !removed.Contains(i)).Select(i =>
            {
                var offset = removed.Count(r => r < i);
                return i - offset;
            }).ToList();
        }
    }
}
