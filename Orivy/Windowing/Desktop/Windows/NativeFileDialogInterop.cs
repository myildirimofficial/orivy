using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Threading;
using System.Threading.Tasks;
using Orivy.Native.Windows;

using static Orivy.Native.Windows.Methods;

namespace Orivy.Windowing.Desktop.Windows;

/// <summary>
/// Thin interop layer over the native Windows Common Item Dialogs (<c>IFileOpenDialog</c> / <c>IFileSaveDialog</c>).
/// </summary>
/// <remarks>
/// <para>
/// The synchronous methods (<c>ShowFileSelection</c>, <c>ShowFolderSelection</c>, <c>ShowSaveFileSelection</c>)
/// block the calling thread and must be invoked from an STA thread.
/// </para>
/// <para>
/// The asynchronous methods (<c>*Async</c>) can be called from any thread (UI thread, thread-pool thread, MTA, ...).
/// Each call hosts the dialog on its own short-lived STA thread, so the caller's thread (and message pump)
/// is never blocked and no <c>[STAThread]</c> requirement is imposed on the caller.
/// </para>
/// </remarks>
[SupportedOSPlatform("windows")]
internal static class NativeFileDialogInterop
{
    private const int ErrorCancelledHResult = unchecked((int)0x800704C7);

    #region Synchronous API

    internal static string[] ShowFileSelection(IntPtr ownerHandle, FileSelectionDialog dialog)
    {
        ArgumentNullException.ThrowIfNull(dialog);

        return ShowSelections(ownerHandle, CreateFileSelectionRequest(dialog));
    }

    internal static string? ShowFolderSelection(IntPtr ownerHandle, FolderSelectionDialog dialog)
    {
        ArgumentNullException.ThrowIfNull(dialog);

        return FirstOrNull(ShowSelections(ownerHandle, CreateFolderSelectionRequest(dialog)));
    }

    internal static string? ShowSaveFileSelection(IntPtr ownerHandle, SaveFileDialog dialog)
    {
        ArgumentNullException.ThrowIfNull(dialog);

        return ShowSaveSelection(ownerHandle, CreateSaveSelectionRequest(dialog));
    }

    #endregion

    #region Asynchronous API

    /// <summary>
    /// Shows the "open file" dialog without blocking the calling thread.
    /// </summary>
    /// <param name="ownerHandle">Owner window handle (may be <see cref="IntPtr.Zero"/>).</param>
    /// <param name="dialog">Dialog settings. They are snapshotted on the calling thread, so the instance may be modified afterwards.</param>
    /// <param name="cancellationToken">
    /// Cancels the operation. If the dialog is already visible it is closed programmatically
    /// and the returned task transitions to the canceled state.
    /// </param>
    /// <returns>The selected paths, or an empty array if the user dismissed the dialog.</returns>
    internal static Task<string[]> ShowFileSelectionAsync(
        IntPtr ownerHandle,
        FileSelectionDialog dialog,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dialog);
        EnsureWindowsPlatform();

        var request = CreateFileSelectionRequest(dialog);
        return RunOnStaThreadAsync(ct => ShowSelections(ownerHandle, request, ct), cancellationToken);
    }

    /// <summary>
    /// Shows the "select folder" dialog without blocking the calling thread.
    /// </summary>
    /// <returns>The selected folder path, or <see langword="null"/> if the user dismissed the dialog.</returns>
    internal static Task<string?> ShowFolderSelectionAsync(
        IntPtr ownerHandle,
        FolderSelectionDialog dialog,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dialog);
        EnsureWindowsPlatform();

        var request = CreateFolderSelectionRequest(dialog);
        return RunOnStaThreadAsync(
            ct => FirstOrNull(ShowSelections(ownerHandle, request, ct)),
            cancellationToken);
    }

    /// <summary>
    /// Shows the "save file" dialog without blocking the calling thread.
    /// </summary>
    /// <returns>The chosen file path, or <see langword="null"/> if the user dismissed the dialog.</returns>
    internal static Task<string?> ShowSaveFileSelectionAsync(
        IntPtr ownerHandle,
        SaveFileDialog dialog,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dialog);
        EnsureWindowsPlatform();

        var request = CreateSaveSelectionRequest(dialog);
        return RunOnStaThreadAsync(ct => ShowSaveSelection(ownerHandle, request, ct), cancellationToken);
    }

    #endregion

    #region Request construction

    private static DialogRequest CreateFileSelectionRequest(FileSelectionDialog dialog)
    {
        var options = FILEOPENDIALOGOPTIONS.FOS_NOCHANGEDIR | FILEOPENDIALOGOPTIONS.FOS_DONTADDTORECENT;
        if (dialog.ForceFileSystem)
            options |= FILEOPENDIALOGOPTIONS.FOS_FORCEFILESYSTEM;
        if (dialog.CheckPathExists)
            options |= FILEOPENDIALOGOPTIONS.FOS_PATHMUSTEXIST;
        if (dialog.CheckFileExists)
            options |= FILEOPENDIALOGOPTIONS.FOS_FILEMUSTEXIST;
        if (dialog.AllowMultipleSelection)
            options |= FILEOPENDIALOGOPTIONS.FOS_ALLOWMULTISELECT;

        return new DialogRequest(
            dialog.Title,
            dialog.InitialDirectory,
            dialog.DefaultExtension,
            dialog.FileName,
            dialog.BuildFilterSpecs(),
            options,
            dialog.AllowMultipleSelection);
    }

    private static DialogRequest CreateFolderSelectionRequest(FolderSelectionDialog dialog)
    {
        var options = FILEOPENDIALOGOPTIONS.FOS_PICKFOLDERS |
                      FILEOPENDIALOGOPTIONS.FOS_NOCHANGEDIR |
                      FILEOPENDIALOGOPTIONS.FOS_DONTADDTORECENT;
        if (dialog.ForceFileSystem)
            options |= FILEOPENDIALOGOPTIONS.FOS_FORCEFILESYSTEM;
        if (dialog.CheckPathExists)
            options |= FILEOPENDIALOGOPTIONS.FOS_PATHMUSTEXIST;

        return new DialogRequest(
            dialog.Title,
            dialog.InitialDirectory,
            string.Empty,
            string.Empty,
            Array.Empty<COMDLG_FILTERSPEC>(),
            options,
            AllowMultipleSelection: false);
    }

    private static DialogRequest CreateSaveSelectionRequest(SaveFileDialog dialog)
    {
        var options = FILEOPENDIALOGOPTIONS.FOS_NOCHANGEDIR | FILEOPENDIALOGOPTIONS.FOS_DONTADDTORECENT;
        if (dialog.ForceFileSystem)
            options |= FILEOPENDIALOGOPTIONS.FOS_FORCEFILESYSTEM;
        if (dialog.CheckPathExists)
            options |= FILEOPENDIALOGOPTIONS.FOS_PATHMUSTEXIST;
        if (dialog.CreatePrompt)
            options |= FILEOPENDIALOGOPTIONS.FOS_CREATEPROMPT;
        if (dialog.OverwritePrompt)
            options |= FILEOPENDIALOGOPTIONS.FOS_OVERWRITEPROMPT;

        return new DialogRequest(
            dialog.Title,
            dialog.InitialDirectory,
            dialog.DefaultExtension,
            dialog.FileName,
            dialog.BuildFilterSpecs(),
            options,
            AllowMultipleSelection: false);
    }

    private static string? FirstOrNull(string[] selections)
    {
        return selections.Length == 0 ? null : selections[0];
    }

    #endregion

    #region Dialog execution

    private static string[] ShowSelections(
        IntPtr ownerHandle,
        DialogRequest request,
        CancellationToken cancellationToken = default)
    {
        EnsureWindowsPlatform();
        EnsureStaThread();

        IFileOpenDialog? dialog = null;
        IShellItem? initialFolder = null;
        IShellItem? selectedItem = null;
        IShellItemArray? selectedItems = null;

        try
        {
            dialog = (IFileOpenDialog)Activator.CreateInstance(typeof(FileOpenDialogComClass))!;
            dialog.GetOptions(out var currentOptions);
            dialog.SetOptions(currentOptions | request.Options);

            if (!string.IsNullOrWhiteSpace(request.Title))
                dialog.SetTitle(request.Title);

            if (request.FilterSpecs.Length > 0)
            {
                dialog.SetFileTypes((uint)request.FilterSpecs.Length, request.FilterSpecs);
                dialog.SetFileTypeIndex(1);
            }

            if (!string.IsNullOrWhiteSpace(request.DefaultExtension))
                dialog.SetDefaultExtension(request.DefaultExtension.Trim().TrimStart('.'));

            // Pre-fill the "File name" edit box so callers can supply a default/typed name.
            if (!string.IsNullOrWhiteSpace(request.FileName))
                dialog.SetFileName(request.FileName);

            if (TryCreateInitialFolder(request.InitialDirectory, out initialFolder) && initialFolder != null)
            {
                dialog.SetDefaultFolder(initialFolder);
                dialog.SetFolder(initialFolder);
            }

            // Last cheap exit point before the dialog window is created.
            cancellationToken.ThrowIfCancellationRequested();

            var hr = dialog.Show(ownerHandle);
            if (hr == ErrorCancelledHResult)
                return Array.Empty<string>();

            Marshal.ThrowExceptionForHR(hr);

            if (request.AllowMultipleSelection)
            {
                dialog.GetResults(out selectedItems);
                return ReadSelectionArray(selectedItems);
            }

            dialog.GetResult(out selectedItem);
            var selectedPath = TryGetDisplayPath(selectedItem);
            return string.IsNullOrWhiteSpace(selectedPath)
                ? Array.Empty<string>()
                : [selectedPath];
        }
        finally
        {
            ReleaseComObject(selectedItems);
            ReleaseComObject(selectedItem);
            ReleaseComObject(initialFolder);
            ReleaseComObject(dialog);
        }
    }

    private static string? ShowSaveSelection(
        IntPtr ownerHandle,
        DialogRequest request,
        CancellationToken cancellationToken = default)
    {
        EnsureWindowsPlatform();
        EnsureStaThread();

        IFileSaveDialog? dialog = null;
        IShellItem? initialFolder = null;
        IShellItem? selectedItem = null;

        try
        {
            dialog = (IFileSaveDialog)Activator.CreateInstance(typeof(FileSaveDialogComClass))!;
            dialog.GetOptions(out var currentOptions);
            dialog.SetOptions(currentOptions | request.Options);

            if (!string.IsNullOrWhiteSpace(request.Title))
                dialog.SetTitle(request.Title);

            if (request.FilterSpecs.Length > 0)
            {
                dialog.SetFileTypes((uint)request.FilterSpecs.Length, request.FilterSpecs);
                dialog.SetFileTypeIndex(1);
            }

            if (!string.IsNullOrWhiteSpace(request.DefaultExtension))
                dialog.SetDefaultExtension(request.DefaultExtension.Trim().TrimStart('.'));

            if (!string.IsNullOrWhiteSpace(request.FileName))
                dialog.SetFileName(request.FileName);

            if (TryCreateInitialFolder(request.InitialDirectory, out initialFolder) && initialFolder != null)
            {
                dialog.SetDefaultFolder(initialFolder);
                dialog.SetFolder(initialFolder);
            }

            // Last cheap exit point before the dialog window is created.
            cancellationToken.ThrowIfCancellationRequested();

            var hr = dialog.Show(ownerHandle);
            if (hr == ErrorCancelledHResult)
                return null;

            Marshal.ThrowExceptionForHR(hr);

            dialog.GetResult(out selectedItem);
            var selectedPath = TryGetDisplayPath(selectedItem);
            return string.IsNullOrWhiteSpace(selectedPath) ? null : selectedPath;
        }
        finally
        {
            ReleaseComObject(selectedItem);
            ReleaseComObject(initialFolder);
            ReleaseComObject(dialog);
        }
    }

    #endregion

    #region STA hosting

    /// <summary>
    /// Runs <paramref name="work"/> on a dedicated, short-lived STA thread and exposes the result as a <see cref="Task{TResult}"/>.
    /// </summary>
    /// <remarks>
    /// Native common dialogs are modal COM objects that require an STA apartment. Hosting them on their own
    /// thread keeps the caller's thread free and avoids requiring <c>[STAThread]</c> on the caller.
    /// Cancellation closes the dialog window (WM_CLOSE) and cancels the returned task.
    /// </remarks>
    private static Task<T> RunOnStaThreadAsync<T>(Func<CancellationToken, T> work, CancellationToken cancellationToken)
    {
        if (cancellationToken.IsCancellationRequested)
            return Task.FromCanceled<T>(cancellationToken);

        var completion = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
        var session = new StaDialogSession();
        CancellationTokenRegistration registration = default;

        var thread = new Thread(() =>
        {
            session.AttachToCurrentThread();

            try
            {
                cancellationToken.ThrowIfCancellationRequested();

                var result = work(cancellationToken);

                // The dialog may have been closed by our own cancellation request; honor the cancellation.
                if (cancellationToken.IsCancellationRequested)
                    completion.TrySetCanceled(cancellationToken);
                else
                    completion.TrySetResult(result);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                completion.TrySetCanceled(cancellationToken);
            }
            catch (Exception ex)
            {
                completion.TrySetException(ex);
            }
            finally
            {
                session.MarkCompleted();
                registration.Dispose();
            }
        })
        {
            IsBackground = true,
            Name = "Orivy.NativeFileDialog.STA"
        };

        thread.SetApartmentState(ApartmentState.STA);

        if (cancellationToken.CanBeCanceled)
            registration = cancellationToken.Register(static state => ((StaDialogSession)state!).RequestClose(), session);

        try
        {
            thread.Start();
        }
        catch (Exception ex)
        {
            session.MarkCompleted();
            registration.Dispose();
            completion.TrySetException(ex);
        }

        return completion.Task;
    }

    /// <summary>
    /// Tracks a dialog hosted on a dedicated STA thread and knows how to close its window on cancellation.
    /// </summary>
    private sealed class StaDialogSession
    {
        private const int CloseRetryDelayMilliseconds = 20;
        private const long CloseRetryTimeoutMilliseconds = 10_000;

        private volatile uint _threadId;
        private volatile bool _completed;

        public void AttachToCurrentThread() => _threadId = DialogWindowInterop.GetCurrentThreadId();

        public void MarkCompleted() => _completed = true;

        /// <summary>
        /// Asks the dialog window to close. If the window has not been created yet
        /// (cancellation raced with dialog start-up) the request is retried briefly in the background.
        /// </summary>
        public void RequestClose()
        {
            if (TryCloseDialogWindows())
                return;

            _ = Task.Run(RetryCloseAsync);
        }

        private async Task RetryCloseAsync()
        {
            var deadline = Environment.TickCount64 + CloseRetryTimeoutMilliseconds;

            while (!_completed && Environment.TickCount64 < deadline)
            {
                await Task.Delay(CloseRetryDelayMilliseconds).ConfigureAwait(false);

                if (_completed || TryCloseDialogWindows())
                    return;
            }
        }

        private bool TryCloseDialogWindows()
        {
            var threadId = _threadId;
            if (threadId == 0 || _completed)
                return false;

            var posted = false;

            DialogWindowInterop.EnumThreadWindows(
                threadId,
                (hWnd, _) =>
                {
                    if (DialogWindowInterop.IsWindowVisible(hWnd) &&
                        DialogWindowInterop.PostMessage(hWnd, DialogWindowInterop.WmClose, IntPtr.Zero, IntPtr.Zero))
                    {
                        posted = true;
                    }

                    return true;
                },
                IntPtr.Zero);

            return posted;
        }
    }

    /// <summary>
    /// Minimal user32/kernel32 surface needed to close a dialog that is blocked inside <c>IModalWindow.Show</c>.
    /// </summary>
    private static class DialogWindowInterop
    {
        internal const uint WmClose = 0x0010;

        internal delegate bool EnumThreadWindowsProc(IntPtr hWnd, IntPtr lParam);

        [DllImport("kernel32.dll", ExactSpelling = true)]
        internal static extern uint GetCurrentThreadId();

        [DllImport("user32.dll", ExactSpelling = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool EnumThreadWindows(uint dwThreadId, EnumThreadWindowsProc lpfn, IntPtr lParam);

        [DllImport("user32.dll", ExactSpelling = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool IsWindowVisible(IntPtr hWnd);

        [DllImport("user32.dll", EntryPoint = "PostMessageW", ExactSpelling = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool PostMessage(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);
    }

    #endregion

    #region Helpers

    /// <summary>Immutable snapshot of everything needed to show a dialog, safe to hand to another thread.</summary>
    private sealed record DialogRequest(
        string Title,
        string InitialDirectory,
        string DefaultExtension,
        string FileName,
        COMDLG_FILTERSPEC[] FilterSpecs,
        FILEOPENDIALOGOPTIONS Options,
        bool AllowMultipleSelection);

    private static void EnsureWindowsPlatform()
    {
        if (!OperatingSystem.IsWindows())
            throw new PlatformNotSupportedException("Native file dialogs are only available on Windows.");
    }

    private static void EnsureStaThread()
    {
        if (Thread.CurrentThread.GetApartmentState() != ApartmentState.STA)
            throw new InvalidOperationException("Native Windows file dialogs require an STA thread. Mark the entry point with [STAThread], or use the *Async overloads.");
    }

    private static bool TryCreateInitialFolder(string path, out IShellItem? shellItem)
    {
        shellItem = null;

        if (string.IsNullOrWhiteSpace(path))
            return false;

        var candidatePath = path;
        if (File.Exists(candidatePath))
        {
            var directoryName = Path.GetDirectoryName(candidatePath);
            if (string.IsNullOrWhiteSpace(directoryName))
                return false;

            candidatePath = directoryName;
        }

        if (!Directory.Exists(candidatePath))
            return false;

        try
        {
            var shellItemGuid = typeof(IShellItem).GUID;
            SHCreateItemFromParsingName(candidatePath, IntPtr.Zero, ref shellItemGuid, out shellItem);
            return shellItem != null;
        }
        catch
        {
            ReleaseComObject(shellItem);
            shellItem = null;
            return false;
        }
    }

    private static string[] ReadSelectionArray(IShellItemArray selectionArray)
    {
        selectionArray.GetCount(out var count);
        if (count == 0)
            return Array.Empty<string>();

        var selections = new List<string>((int)count);
        for (uint i = 0; i < count; i++)
        {
            IShellItem? item = null;
            try
            {
                selectionArray.GetItemAt(i, out item);
                var path = TryGetDisplayPath(item);
                if (!string.IsNullOrWhiteSpace(path))
                    selections.Add(path);
            }
            finally
            {
                ReleaseComObject(item);
            }
        }

        return selections.ToArray();
    }

    private static string TryGetDisplayPath(IShellItem shellItem)
    {
        return TryGetDisplayName(shellItem, SIGDN.SIGDN_FILESYSPATH)
               ?? TryGetDisplayName(shellItem, SIGDN.SIGDN_DESKTOPABSOLUTEPARSING)
               ?? string.Empty;
    }

    private static string? TryGetDisplayName(IShellItem shellItem, SIGDN displayNameKind)
    {
        IntPtr displayName = IntPtr.Zero;

        try
        {
            shellItem.GetDisplayName(displayNameKind, out displayName);
            return displayName == IntPtr.Zero ? null : Marshal.PtrToStringUni(displayName);
        }
        catch
        {
            return null;
        }
        finally
        {
            if (displayName != IntPtr.Zero)
                Marshal.FreeCoTaskMem(displayName);
        }
    }

    private static void ReleaseComObject(object? comObject)
    {
        if (comObject == null || !Marshal.IsComObject(comObject))
            return;

        Marshal.FinalReleaseComObject(comObject);
    }

    #endregion
}