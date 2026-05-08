using Orivy.Controls;
using System;

namespace Orivy.Windowing.Desktop.Windows;

public abstract class NativeSelectionDialogBase
{
    public string Title { get; set; } = string.Empty;

    public string InitialDirectory { get; set; } = string.Empty;

    public bool CheckPathExists { get; set; } = true;

    public bool ForceFileSystem { get; set; } = true;

    protected IntPtr ResolveOwnerHandle(WindowBase? owner)
    {
        return owner?.Handle ?? IntPtr.Zero;
    }
}