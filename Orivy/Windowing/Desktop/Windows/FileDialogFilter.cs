using Orivy.Native.Windows;
using System;
using System.Collections.Generic;

namespace Orivy.Windowing.Desktop.Windows;

public sealed class FileDialogFilter
{
    private readonly List<string> _patterns = new();

    public FileDialogFilter(string name, params string[] patterns)
    {
        Name = string.IsNullOrWhiteSpace(name) ? "Files" : name.Trim();

        for (var i = 0; i < patterns.Length; i++)
            AddPattern(patterns[i]);
    }

    public string Name { get; }

    public IList<string> Patterns => _patterns;

    private void AddPattern(string? pattern)
    {
        var normalized = NormalizePattern(pattern);
        if (string.IsNullOrWhiteSpace(normalized))
            return;

        for (var i = 0; i < _patterns.Count; i++)
        {
            if (string.Equals(_patterns[i], normalized, StringComparison.OrdinalIgnoreCase))
                return;
        }

        _patterns.Add(normalized);
    }

    internal COMDLG_FILTERSPEC ToNativeSpec()
    {
        return new COMDLG_FILTERSPEC(Name, _patterns.Count == 0 ? "*.*" : string.Join(";", _patterns));
    }

    private static string NormalizePattern(string? pattern)
    {
        if (string.IsNullOrWhiteSpace(pattern))
            return string.Empty;

        var trimmed = pattern.Trim();
        if (trimmed == "*" || trimmed == ".*")
            return "*.*";

        if (trimmed.StartsWith("*.", StringComparison.Ordinal))
            return trimmed;

        if (trimmed.StartsWith('.'))
            return $"*{trimmed}";

        if (trimmed.Contains('*'))
            return trimmed;

        return $"*.{trimmed.TrimStart('.')}";
    }
}