using System;
using System.Collections.Generic;

namespace Orivy.Studio;

/// <summary>
/// Normalizes a control's <c>Name</c> when it comes from outside the interactive canvas — a
/// hand-edited <c>.orivy.json</c> (<see cref="Persistence.DesignSerializer"/>) or a pasted/hand-edited
/// Designer-code file (<see cref="CodeImporter"/>) — into something both a legal C# identifier and
/// unique within the document being built. Interactive placement never needs this: <see cref="DesignSurface.AddControl"/>
/// already generates a valid, unique name itself. External data has no such guarantee, and
/// <see cref="CodeGenerator"/> emits <c>Name</c> directly as a field identifier — an invalid or
/// duplicate name would only surface as a C# compile error at Export time, far from its actual cause.
/// </summary>
internal static class DesignNameValidator
{
    /// <summary>Returns a name safe to assign to <see cref="Orivy.Controls.ElementBase.Name"/>: valid
    /// C# identifier syntax, not a reserved keyword, and not already present in <paramref name="used"/>
    /// (which is updated with the returned name before returning).</summary>
    public static string Normalize(string? candidate, string typeName, HashSet<string> used)
    {
        var baseName = Sanitize(candidate, typeName);
        var name = baseName;
        var suffix = 1;
        while (!used.Add(name))
            name = $"{baseName}{suffix++}";

        return name;
    }

    private static string Sanitize(string? candidate, string typeName)
    {
        if (IsValidIdentifier(candidate) && !Keywords.Contains(candidate))
            return candidate!;

        // Falls back to the same "lowercase-first-letter of the type name" convention
        // DesignSurface.AddControl already uses for interactively placed controls.
        return char.ToLowerInvariant(typeName[0]) + typeName[1..];
    }

    private static bool IsValidIdentifier(string? s)
    {
        if (string.IsNullOrEmpty(s) || !(char.IsLetter(s[0]) || s[0] == '_'))
            return false;

        for (var i = 1; i < s.Length; i++)
            if (!(char.IsLetterOrDigit(s[i]) || s[i] == '_'))
                return false;

        return true;
    }

    private static readonly HashSet<string> Keywords = new(StringComparer.Ordinal)
    {
        "abstract", "as", "base", "bool", "break", "byte", "case", "catch", "char", "checked", "class",
        "const", "continue", "decimal", "default", "delegate", "do", "double", "else", "enum", "event",
        "explicit", "extern", "false", "finally", "fixed", "float", "for", "foreach", "goto", "if",
        "implicit", "in", "int", "interface", "internal", "is", "lock", "long", "namespace", "new",
        "null", "object", "operator", "out", "override", "params", "private", "protected", "public",
        "readonly", "ref", "return", "sbyte", "sealed", "short", "sizeof", "stackalloc", "static",
        "string", "struct", "switch", "this", "throw", "true", "try", "typeof", "uint", "ulong",
        "unchecked", "unsafe", "ushort", "using", "virtual", "void", "volatile", "while",
    };
}
