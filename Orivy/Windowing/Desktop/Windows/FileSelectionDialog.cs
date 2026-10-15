using Orivy.Controls;
using System.Runtime.Versioning;
using System.Threading;
using System.Threading.Tasks;

namespace Orivy.Windowing.Desktop.Windows;

[SupportedOSPlatform("windows")]
public class FileSelectionDialog : FileDialogBase
{
    public bool AllowMultipleSelection { get; set; }

    public bool CheckFileExists { get; set; } = true;

    /// <summary>
    /// The file name shown in the dialog's "File name" edit box. Acts as both input and output:
    /// set it before <see cref="ShowDialog"/> / <see cref="ShowDialogAsync"/> to pre-fill the box
    /// (the user can still edit/type it), and after the dialog closes it holds the selected file's path
    /// (the first one when <see cref="AllowMultipleSelection"/> is enabled).
    /// </summary>
    public string FileName { get; set; } = string.Empty;

    /// <summary>
    /// Shows the dialog and blocks the calling thread until it is closed. Must be called from an STA thread.
    /// </summary>
    /// <param name="owner">Optional owner window.</param>
    /// <returns>The selected paths, or an empty array if the user dismissed the dialog.</returns>
    public string[] ShowDialog(WindowBase? owner = null)
    {
        var result = NativeFileDialogInterop.ShowFileSelection(ResolveOwnerHandle(owner), this);
        if (result.Length > 0)
            FileName = result[0];
        return result;
    }

    /// <summary>
    /// Shows the dialog without blocking the calling thread. Can be called from any thread.
    /// </summary>
    /// <remarks>
    /// The dialog settings are captured when this method is called; changes made to this instance while
    /// the dialog is open do not affect it. The continuation resumes on the caller's captured
    /// synchronization context (e.g. the UI thread), where <see cref="FileName"/> is updated.
    /// </remarks>
    /// <param name="owner">Optional owner window.</param>
    /// <param name="cancellationToken">
    /// Closes the dialog and cancels the returned task when signaled.
    /// </param>
    /// <returns>The selected paths, or an empty array if the user dismissed the dialog.</returns>
    /// <exception cref="OperationCanceledException">The operation was canceled through <paramref name="cancellationToken"/>.</exception>
    public async Task<string[]> ShowDialogAsync(WindowBase? owner = null, CancellationToken cancellationToken = default)
    {
        var result = await NativeFileDialogInterop.ShowFileSelectionAsync(
            ResolveOwnerHandle(owner),
            this,
            cancellationToken);

        if (result.Length > 0)
            FileName = result[0];

        return result;
    }
}
