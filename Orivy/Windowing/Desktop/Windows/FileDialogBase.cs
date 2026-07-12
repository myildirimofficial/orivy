using Orivy.Native.Windows;
using System.Collections.Generic;

namespace Orivy.Windowing.Desktop.Windows;

public abstract class FileDialogBase : NativeSelectionDialogBase
{
    private readonly List<FileDialogFilter> _filters = new();

    public string DefaultExtension { get; set; } = string.Empty;

    public IList<FileDialogFilter> Filters => _filters;

    /// <summary>
    /// WinForms-compatible filter string, e.g. <c>"Text files (*.txt)|*.txt|All files (*.*)|*.*"</c>.
    /// Setting it replaces <see cref="Filters"/>; getting it round-trips the current filters.
    /// </summary>
    public string Filter
    {
        get
        {
            var parts = new List<string>(_filters.Count * 2);
            foreach (var f in _filters)
            {
                parts.Add(f.Name);
                parts.Add(f.Patterns.Count == 0 ? "*.*" : string.Join(";", f.Patterns));
            }

            return string.Join("|", parts);
        }
        set
        {
            _filters.Clear();
            if (string.IsNullOrWhiteSpace(value))
                return;

            var segments = value.Split('|');
            for (var i = 0; i + 1 < segments.Length; i += 2)
            {
                var patterns = segments[i + 1].Split(';', System.StringSplitOptions.RemoveEmptyEntries | System.StringSplitOptions.TrimEntries);
                _filters.Add(new FileDialogFilter(segments[i], patterns));
            }
        }
    }

    internal COMDLG_FILTERSPEC[] BuildFilterSpecs()
    {
        if (_filters.Count == 0)
            return [new COMDLG_FILTERSPEC("All Files", "*.*")];

        var specs = new COMDLG_FILTERSPEC[_filters.Count];
        for (var i = 0; i < _filters.Count; i++)
            specs[i] = _filters[i].ToNativeSpec();

        return specs;
    }
}
