using Orivy.Native.Windows;
using System.Collections.Generic;

namespace Orivy.Windowing.Desktop.Windows;

public abstract class FileDialogBase : NativeSelectionDialogBase
{
    private readonly List<FileDialogFilter> _filters = new();

    public string DefaultExtension { get; set; } = string.Empty;

    public IList<FileDialogFilter> Filters => _filters;

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
