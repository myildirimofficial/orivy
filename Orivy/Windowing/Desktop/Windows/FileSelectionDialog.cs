using Orivy.Controls;
using System.Runtime.Versioning;

namespace Orivy.Windowing.Desktop.Windows;

[SupportedOSPlatform("windows")]
public sealed class FileSelectionDialog : FileDialogBase
{
    public bool AllowMultipleSelection { get; set; }

    public bool CheckFileExists { get; set; } = true;

    public string[] ShowDialog(WindowBase? owner = null)
    {
        return NativeFileDialogInterop.ShowFileSelection(ResolveOwnerHandle(owner), this);
    }
}
