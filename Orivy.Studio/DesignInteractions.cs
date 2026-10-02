using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

namespace Orivy.Studio;

/// <summary>One <c>control.Event += handler</c> line the designer owns.</summary>
public sealed class EventWire
{
    public EventWire(string controlName, string eventName, string handler)
    {
        ControlName = controlName;
        EventName = eventName;
        Handler = handler;
    }

    public string ControlName { get; }
    public string EventName { get; }
    public string Handler { get; set; }
}

/// <summary>
/// One <c>Link(...).From(...)</c> or <c>FromData</c> line the designer owns.
/// <see cref="SourceControl"/> is null when the source is the control's DataContext.
/// </summary>
public sealed class ControlBinding
{
    public ControlBinding(string controlName, string targetProperty, string? sourceControl, string? sourceType, string sourceProperty, bool twoWay)
    {
        ControlName = controlName;
        TargetProperty = targetProperty;
        SourceControl = sourceControl;
        SourceType = sourceType;
        SourceProperty = sourceProperty;
        TwoWay = twoWay;
    }

    public string ControlName { get; }
    public string TargetProperty { get; }
    public string? SourceControl { get; }
    public string? SourceType { get; }
    public string SourceProperty { get; }
    public bool TwoWay { get; }
}

/// <summary>
/// Reads and writes the event and binding statements Studio's Events and Bindings pages edit.
/// Property assignments stay with <see cref="CodeMerger"/>; this only touches <c>+=</c> and <c>Link</c> calls.
/// </summary>
public static class DesignInteractions
{
    public static void Collect(ExpressionSyntax expression, List<EventWire>? events, List<ControlBinding>? bindings)
    {
        if (events != null && TryReadEvent(expression, out var wire))
            events.Add(wire);
        if (bindings != null && TryReadBinding(expression, out var binding))
            bindings.Add(binding);
    }

    public static SyntaxNode Sync(SyntaxNode root, DesignSurface surface, out bool changed, string? filePath = null)
    {
        changed = false;
        var method = root.DescendantNodes().OfType<MethodDeclarationSyntax>()
            .FirstOrDefault(m => m.Identifier.Text == "InitializeComponent" && m.Body != null);
        if (method?.Body == null)
            return root;

        var wantedEvents = surface.EventWires
            .Where(wire => !string.IsNullOrWhiteSpace(wire.Handler))
            .ToList();
        var wantedBindings = surface.ControlBindings.ToList();
        var seenEvents = new HashSet<string>(StringComparer.Ordinal);
        var seenBindings = new HashSet<string>(StringComparer.Ordinal);
        var next = new List<StatementSyntax>();
        var queued = new Dictionary<string, List<StatementSyntax>>(StringComparer.Ordinal);
        var queueOrder = new List<string>();

        void Queue(string controlName, string text)
        {
            if (!queued.TryGetValue(controlName, out var list))
            {
                list = new List<StatementSyntax>();
                queued[controlName] = list;
                queueOrder.Add(controlName);
            }

            list.Add(Statement(text));
        }

        foreach (var statement in method.Body.Statements)
        {
            if (statement is not ExpressionStatementSyntax { Expression: var expression })
            {
                next.Add(statement);
                continue;
            }

            if (TryReadEvent(expression, out var wire))
            {
                var match = wantedEvents.FirstOrDefault(item => SameEvent(item, wire));
                if (match == null)
                {
                    changed = true;
                    continue;
                }

                seenEvents.Add(Key(wire.ControlName, wire.EventName));
                if (!string.Equals(match.Handler, wire.Handler, StringComparison.Ordinal))
                {
                    changed = true;
                    Queue(match.ControlName, FormatEvent(match));
                }
                else
                {
                    next.Add(statement);
                }

                continue;
            }

            if (TryReadBinding(expression, out var binding))
            {
                var match = wantedBindings.FirstOrDefault(item => SameBinding(item, binding));
                if (match == null)
                {
                    changed = true;
                    continue;
                }

                seenBindings.Add(Key(binding.ControlName, binding.TargetProperty));
                if (!SameBindingText(match, binding))
                {
                    changed = true;
                    Queue(match.ControlName, FormatBinding(surface, match));
                }
                else
                {
                    next.Add(statement);
                }

                continue;
            }

            next.Add(statement);
        }

        foreach (var wire in wantedEvents)
        {
            if (seenEvents.Add(Key(wire.ControlName, wire.EventName)))
                Queue(wire.ControlName, FormatEvent(wire));
        }

        foreach (var binding in wantedBindings)
        {
            if (seenBindings.Add(Key(binding.ControlName, binding.TargetProperty)))
                Queue(binding.ControlName, FormatBinding(surface, binding));
        }

        SyntaxNode result = root;
        if (queueOrder.Count > 0 || changed)
        {
            changed = true;
            foreach (var controlName in queueOrder)
            {
                var at = InsertionIndex(next, controlName);
                next.InsertRange(at, queued[controlName]);
            }

            var updated = method.WithBody(method.Body.WithStatements(SyntaxFactory.List(next)));
            result = root.ReplaceNode(method, updated);
        }

        return EnsureHandlerMethods(result, surface, filePath, ref changed);
    }

    internal static int PropertyInsertionIndex(IReadOnlyList<StatementSyntax> statements, string controlName)
    {
        if (controlName.Length == 0)
            return FormPropertyInsertionIndex(statements);

        return InsertionIndex(statements, controlName);
    }

    private static int FormPropertyInsertionIndex(IReadOnlyList<StatementSyntax> statements)
    {
        var lastFormSetup = -1;
        for (var i = 0; i < statements.Count; i++)
        {
            if (statements[i] is not ExpressionStatementSyntax { Expression: AssignmentExpressionSyntax assign })
                continue;
            if (!DesignPersistableProperties.TryParseAssignmentTarget(assign, out var controlName, out _)
                || !string.IsNullOrEmpty(controlName))
                continue;

            lastFormSetup = i;
        }

        if (lastFormSetup >= 0)
            return lastFormSetup + 1;

        for (var i = statements.Count - 1; i >= 0; i--)
        {
            if (IsResumeLayout(statements[i]))
                return i;
        }

        return statements.Count;
    }

    internal static StatementSyntax ParseStatement(string text) => Statement(text);

    /// <summary>
    /// Index of the first line after this control's creation or last property assignment.
    /// An <c>Add</c> call is the fallback when the control has no initializer in this method.
    /// </summary>
    private static int InsertionIndex(IReadOnlyList<StatementSyntax> statements, string controlName)
    {
        var setup = -1;
        var add = -1;
        for (var i = 0; i < statements.Count; i++)
        {
            if (IsSetupStatement(statements[i], controlName))
                setup = i;
            else if (AddsControl(statements[i], controlName))
                add = i;
        }

        if (setup >= 0)
            return setup + 1;
        if (add >= 0)
            return add + 1;

        for (var i = statements.Count - 1; i >= 0; i--)
        {
            if (IsResumeLayout(statements[i]))
                return i;
        }

        return statements.Count;
    }

    private static bool IsSetupStatement(StatementSyntax statement, string controlName)
    {
        if (statement is LocalDeclarationStatementSyntax local)
        {
            return local.Declaration.Variables.Any(variable => variable.Identifier.Text == controlName);
        }

        if (statement is not ExpressionStatementSyntax { Expression: AssignmentExpressionSyntax assign })
            return false;
        if (assign.IsKind(SyntaxKind.AddAssignmentExpression) || assign.IsKind(SyntaxKind.SubtractAssignmentExpression))
            return false;

        return ConfiguredControl(assign.Left) == controlName;
    }

    private static bool AddsControl(StatementSyntax statement, string controlName)
    {
        if (statement is not ExpressionStatementSyntax { Expression: InvocationExpressionSyntax call })
            return false;
        if (MethodName(call) is not ("Add" or "AddRange"))
            return false;

        return call.ArgumentList.DescendantNodes().OfType<IdentifierNameSyntax>()
            .Any(identifier => identifier.Identifier.Text == controlName);
    }

    private static bool IsResumeLayout(StatementSyntax statement) =>
        statement is ExpressionStatementSyntax
        {
            Expression: InvocationExpressionSyntax
            {
                Expression: MemberAccessExpressionSyntax { Name.Identifier.Text: "ResumeLayout" }
            }
        };

    private static string? ConfiguredControl(ExpressionSyntax left)
    {
        var names = new List<string>();
        var current = left;
        while (current is MemberAccessExpressionSyntax access)
        {
            names.Add(access.Name.Identifier.Text);
            current = access.Expression;
        }

        if (current is IdentifierNameSyntax identifier)
            return identifier.Identifier.Text;
        if (current is ThisExpressionSyntax && names.Count > 0)
            return names[^1];
        return null;
    }

    /// <summary>
    /// A plain method name (<c>name_Click</c> or <c>this.name_Click</c>). Lambdas and other expressions stay as written.
    /// </summary>
    public static bool TryPlainHandlerName(string? handler, out string name)
    {
        name = string.Empty;
        if (string.IsNullOrWhiteSpace(handler))
            return false;

        var text = handler.Trim();
        if (text.StartsWith("this.", StringComparison.Ordinal))
            text = text[5..];
        if (text.Length == 0 || !IsIdentifier(text))
            return false;

        name = text;
        return true;
    }

    /// <summary>Empty method matching the event's invoke signature. Existing methods are left alone.</summary>
    public static string FormatHandlerMethod(DesignSurface surface, EventWire wire)
    {
        if (!TryPlainHandlerName(wire.Handler, out var name))
            return string.Empty;

        return $"    private void {name}({HandlerParameters(surface, wire)})\n    {{\n    }}";
    }

    public static string FormatEvent(EventWire wire) =>
        $"        {wire.ControlName}.{wire.EventName} += {wire.Handler};";

    public static string FormatBinding(DesignSurface surface, ControlBinding binding)
    {
        var mode = binding.TwoWay ? "TwoWay" : "OneWay";
        if (string.IsNullOrEmpty(binding.SourceControl))
        {
            var sourceType = string.IsNullOrWhiteSpace(binding.SourceType) ? "object" : binding.SourceType;
            var valueType = ValueTypeName(surface, binding);
            return $"        {binding.ControlName}.Link(c => c.{binding.TargetProperty}).FromData<{sourceType}, {valueType}>(s => s.{binding.SourceProperty}).{mode}();";
        }

        return $"        {binding.ControlName}.Link(c => c.{binding.TargetProperty}).From({binding.SourceControl}, s => s.{binding.SourceProperty}).{mode}();";
    }

    private static bool TryReadEvent(ExpressionSyntax expression, out EventWire wire)
    {
        wire = null!;
        if (expression is not AssignmentExpressionSyntax assign || !assign.IsKind(SyntaxKind.AddAssignmentExpression))
            return false;
        if (assign.Left is not MemberAccessExpressionSyntax member)
            return false;

        var control = ReceiverName(member.Expression);
        var eventName = member.Name.Identifier.Text;
        if (control == null || string.IsNullOrEmpty(eventName))
            return false;

        wire = new EventWire(control, eventName, HandlerText(assign.Right));
        return !string.IsNullOrWhiteSpace(wire.Handler);
    }

    private static bool TryReadBinding(ExpressionSyntax expression, out ControlBinding binding)
    {
        binding = null!;
        if (expression is not InvocationExpressionSyntax modeCall)
            return false;

        var modeName = MethodName(modeCall);
        if (modeName is not ("OneWay" or "TwoWay"))
            return false;
        if (modeCall.Expression is not MemberAccessExpressionSyntax { Expression: InvocationExpressionSyntax fromCall })
            return false;

        var fromName = MethodName(fromCall);
        if (fromName is not ("From" or "FromData"))
            return false;
        if (fromCall.Expression is not MemberAccessExpressionSyntax { Expression: InvocationExpressionSyntax linkCall })
            return false;
        if (MethodName(linkCall) != "Link")
            return false;
        if (linkCall.Expression is not MemberAccessExpressionSyntax linkAccess)
            return false;

        var control = ReceiverName(linkAccess.Expression);
        var targetProperty = linkCall.ArgumentList.Arguments.Count == 1
            ? LambdaMember(linkCall.ArgumentList.Arguments[0].Expression)
            : null;
        if (control == null || targetProperty == null)
            return false;

        string? sourceControl = null;
        string? sourceType = null;
        string? sourceProperty;
        if (fromName == "FromData")
        {
            sourceProperty = fromCall.ArgumentList.Arguments.Count == 1
                ? LambdaMember(fromCall.ArgumentList.Arguments[0].Expression)
                : null;
            if (fromCall.Expression is MemberAccessExpressionSyntax { Name: GenericNameSyntax generic }
                && generic.TypeArgumentList.Arguments.Count > 0)
                sourceType = generic.TypeArgumentList.Arguments[0].ToString();
        }
        else
        {
            if (fromCall.ArgumentList.Arguments.Count != 2)
                return false;
            sourceControl = ReceiverName(fromCall.ArgumentList.Arguments[0].Expression);
            sourceProperty = LambdaMember(fromCall.ArgumentList.Arguments[1].Expression);
        }

        if (sourceProperty == null || (fromName == "From" && sourceControl == null))
            return false;

        binding = new ControlBinding(control, targetProperty, sourceControl, sourceType, sourceProperty, modeName == "TwoWay");
        return true;
    }

    private static string HandlerText(ExpressionSyntax right)
    {
        switch (right)
        {
            case IdentifierNameSyntax identifier:
                return identifier.Identifier.Text;
            case MemberAccessExpressionSyntax { Expression: ThisExpressionSyntax, Name: IdentifierNameSyntax name }:
                return "this." + name.Identifier.Text;
            case ObjectCreationExpressionSyntax creation when creation.ArgumentList?.Arguments.Count == 1:
                return HandlerText(creation.ArgumentList.Arguments[0].Expression);
            default:
                return right.ToString().Trim();
        }
    }

    private static string? MethodName(InvocationExpressionSyntax call) => call.Expression switch
    {
        MemberAccessExpressionSyntax { Name: IdentifierNameSyntax identifier } => identifier.Identifier.Text,
        MemberAccessExpressionSyntax { Name: GenericNameSyntax generic } => generic.Identifier.Text,
        _ => null
    };

    private static string? LambdaMember(ExpressionSyntax expression)
    {
        var body = expression switch
        {
            SimpleLambdaExpressionSyntax simple => simple.Body,
            ParenthesizedLambdaExpressionSyntax parenthesized => parenthesized.Body,
            _ => null
        };
        return body is MemberAccessExpressionSyntax { Name: IdentifierNameSyntax name }
            ? name.Identifier.Text
            : null;
    }

    private static string? ReceiverName(ExpressionSyntax expression) => expression switch
    {
        IdentifierNameSyntax identifier => identifier.Identifier.Text,
        MemberAccessExpressionSyntax { Expression: ThisExpressionSyntax, Name: IdentifierNameSyntax name } => name.Identifier.Text,
        _ => null
    };

    private static bool SameEvent(EventWire left, EventWire right) =>
        string.Equals(left.ControlName, right.ControlName, StringComparison.Ordinal)
        && string.Equals(left.EventName, right.EventName, StringComparison.Ordinal);

    private static bool SameBinding(ControlBinding left, ControlBinding right) =>
        string.Equals(left.ControlName, right.ControlName, StringComparison.Ordinal)
        && string.Equals(left.TargetProperty, right.TargetProperty, StringComparison.Ordinal);

    private static bool SameBindingText(ControlBinding left, ControlBinding right) =>
        left.TwoWay == right.TwoWay
        && string.Equals(left.SourceControl, right.SourceControl, StringComparison.Ordinal)
        && string.Equals(left.SourceType, right.SourceType, StringComparison.Ordinal)
        && string.Equals(left.SourceProperty, right.SourceProperty, StringComparison.Ordinal);

    private static string Key(string left, string right) => left + "\n" + right;

    private static StatementSyntax Statement(string text) =>
        SyntaxFactory.ParseStatement(text + Environment.NewLine);

    private static string ValueTypeName(DesignSurface surface, ControlBinding binding)
    {
        var control = surface.AllDesignedControls.FirstOrDefault(item => item.Name == binding.ControlName);
        var property = control?.GetType().GetProperty(binding.TargetProperty);
        if (property == null)
            return "object";

        var type = property.PropertyType;
        if (type == typeof(string)) return "string";
        if (type == typeof(bool)) return "bool";
        if (type == typeof(int)) return "int";
        if (type == typeof(long)) return "long";
        if (type == typeof(float)) return "float";
        if (type == typeof(double)) return "double";
        if (type == typeof(decimal)) return "decimal";
        var underlying = Nullable.GetUnderlyingType(type);
        if (underlying != null)
            return ValueTypeName(underlying) + "?";
        return type.Name;
    }

    private static SyntaxNode EnsureHandlerMethods(SyntaxNode root, DesignSurface surface, string? filePath, ref bool changed)
    {
        var init = root.DescendantNodes().OfType<MethodDeclarationSyntax>()
            .FirstOrDefault(m => m.Identifier.Text == "InitializeComponent");
        var classDecl = init?.FirstAncestorOrSelf<ClassDeclarationSyntax>();
        if (classDecl == null)
            return root;

        var existing = new HashSet<string>(
            classDecl.Members.OfType<MethodDeclarationSyntax>().Select(member => member.Identifier.Text),
            StringComparer.Ordinal);
        var additions = new List<MemberDeclarationSyntax>();
        foreach (var wire in surface.EventWires)
        {
            if (!TryPlainHandlerName(wire.Handler, out var name) || !existing.Add(name))
                continue;
            if (MethodExistsInSibling(filePath, name))
                continue;

            var text = FormatHandlerMethod(surface, wire);
            if (text.Length == 0)
                continue;
            var member = SyntaxFactory.ParseMemberDeclaration(text + Environment.NewLine);
            if (member != null)
                additions.Add(member);
        }

        if (additions.Count == 0)
            return root;

        changed = true;
        return root.ReplaceNode(classDecl, classDecl.AddMembers(additions.ToArray()));
    }

    private static bool MethodExistsInSibling(string? filePath, string methodName)
    {
        if (string.IsNullOrEmpty(filePath))
            return false;

        var fileName = Path.GetFileName(filePath);
        const string suffix = ".Designer.cs";
        if (!fileName.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
            return false;

        var sibling = Path.Combine(
            Path.GetDirectoryName(filePath) ?? string.Empty,
            fileName[..^suffix.Length] + ".cs");
        if (!File.Exists(sibling))
            return false;

        try
        {
            var siblingRoot = CSharpSyntaxTree.ParseText(File.ReadAllText(sibling)).GetRoot();
            return siblingRoot.DescendantNodes().OfType<MethodDeclarationSyntax>()
                .Any(member => member.Identifier.Text == methodName);
        }
        catch (IOException)
        {
            return false;
        }
    }

    private static string HandlerParameters(DesignSurface surface, EventWire wire)
    {
        var control = surface.AllDesignedControls.FirstOrDefault(item => item.Name == wire.ControlName);
        var eventInfo = control?.GetType().GetEvent(wire.EventName, BindingFlags.Instance | BindingFlags.Public);
        var parameters = eventInfo?.EventHandlerType?.GetMethod("Invoke")?.GetParameters();
        if (parameters == null || parameters.Length == 0)
            return "object sender, EventArgs e";

        return string.Join(", ", parameters.Select(parameter =>
            $"{FriendlyType(parameter.ParameterType)} {ParameterName(parameter)}"));
    }

    private static string ParameterName(ParameterInfo parameter)
    {
        if (!string.IsNullOrEmpty(parameter.Name) && IsIdentifier(parameter.Name))
            return parameter.Name;
        return "arg" + parameter.Position;
    }

    private static string FriendlyType(Type type)
    {
        if (type == typeof(object)) return "object";
        if (type == typeof(string)) return "string";
        if (type == typeof(bool)) return "bool";
        if (type == typeof(int)) return "int";
        if (type == typeof(long)) return "long";
        if (type == typeof(float)) return "float";
        if (type == typeof(double)) return "double";
        if (type == typeof(decimal)) return "decimal";
        if (type.IsGenericType)
        {
            var name = type.Name;
            var tick = name.IndexOf('`');
            if (tick > 0)
                name = name[..tick];
            return name + "<" + string.Join(", ", type.GetGenericArguments().Select(FriendlyType)) + ">";
        }

        return type.Name;
    }

    private static bool IsIdentifier(string text)
    {
        if (!char.IsLetter(text[0]) && text[0] != '_')
            return false;
        for (var i = 1; i < text.Length; i++)
        {
            if (!char.IsLetterOrDigit(text[i]) && text[i] != '_')
                return false;
        }

        return true;
    }

    private static string ValueTypeName(Type type)
    {
        if (type == typeof(string)) return "string";
        if (type == typeof(bool)) return "bool";
        if (type == typeof(int)) return "int";
        if (type == typeof(decimal)) return "decimal";
        return type.Name;
    }
}
