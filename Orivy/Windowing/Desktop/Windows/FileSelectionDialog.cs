using Orivy.Controls;
using System.Runtime.Versioning;

namespace Orivy.Windowing.Desktop.Windows;

[SupportedOSPlatform("windows")]
public class FileSelectionDialog : FileDialogBase
{
    public bool AllowMultipleSelection { get; set; }

    public bool CheckFileExists { get; set; } = true;

    /// <summary>
    /// The file name shown in the dialog's "File name" edit box. Acts as both input and output:
    /// set it before <see cref="ShowDialog"/> to pre-fill the box (the user can still edit/type it),
    /// and after the dialog closes it holds the selected file's path (the first one when
    /// <see cref="AllowMultipleSelection"/> is enabled).
    /// </summary>
    public string FileName { get; set; } = string.Empty;

    public string[] ShowDialog(WindowBase? owner = null)
    {
        var result = NativeFileDialogInterop.ShowFileSelection(ResolveOwnerHandle(owner), this);
        if (result.Length > 0)
            FileName = result[0];
        return result;
    }
}
