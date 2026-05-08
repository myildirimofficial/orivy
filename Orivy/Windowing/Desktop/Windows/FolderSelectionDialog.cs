using Orivy.Controls;
using System.Runtime.Versioning;

namespace Orivy.Windowing.Desktop.Windows;

[SupportedOSPlatform("windows")]
public sealed class FolderSelectionDialog : NativeSelectionDialogBase
{
    public string? ShowDialog(WindowBase? owner = null)
    {
        return NativeFileDialogInterop.ShowFolderSelection(ResolveOwnerHandle(owner), this);
    }
}