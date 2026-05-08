using Orivy.Controls;
using System.Runtime.Versioning;

namespace Orivy.Windowing.Desktop.Windows;

[SupportedOSPlatform("windows")]
public sealed class SaveFileDialog : FileDialogBase
{
    public string FileName { get; set; } = string.Empty;

    public bool CreatePrompt { get; set; }

    public bool OverwritePrompt { get; set; } = true;

    public string? ShowDialog(WindowBase? owner = null)
    {
        return NativeFileDialogInterop.ShowSaveFileSelection(ResolveOwnerHandle(owner), this);
    }
}