using Orivy.Controls;
using System.Runtime.Versioning;
using System.Threading;
using System.Threading.Tasks;

namespace Orivy.Windowing.Desktop.Windows;

[SupportedOSPlatform("windows")]
public sealed class SaveFileDialog : FileDialogBase
{
    public string FileName { get; set; } = string.Empty;

    public bool CreatePrompt { get; set; }

    public bool OverwritePrompt { get; set; } = true;

    /// <summary>
    /// Shows the dialog and blocks the calling thread until it is closed. Must be called from an STA thread.
    /// </summary>
    /// <param name="owner">Optional owner window.</param>
    /// <returns>The chosen file path, or <see langword="null"/> if the user dismissed the dialog.</returns>
    public string? ShowDialog(WindowBase? owner = null)
    {
        return NativeFileDialogInterop.ShowSaveFileSelection(ResolveOwnerHandle(owner), this);
    }

    /// <summary>
    /// Shows the dialog without blocking the calling thread. Can be called from any thread.
    /// </summary>
    /// <remarks>
    /// The dialog settings are captured when this method is called; changes made to this instance while
    /// the dialog is open do not affect it. The continuation resumes on the caller's captured
    /// synchronization context (e.g. the UI thread).
    /// </remarks>
    /// <param name="owner">Optional owner window.</param>
    /// <param name="cancellationToken">
    /// Closes the dialog and cancels the returned task when signaled.
    /// </param>
    /// <returns>The chosen file path, or <see langword="null"/> if the user dismissed the dialog.</returns>
    /// <exception cref="OperationCanceledException">The operation was canceled through <paramref name="cancellationToken"/>.</exception>
    public Task<string?> ShowDialogAsync(WindowBase? owner = null, CancellationToken cancellationToken = default)
    {
        return NativeFileDialogInterop.ShowSaveFileSelectionAsync(
            ResolveOwnerHandle(owner),
            this,
            cancellationToken);
    }
}
