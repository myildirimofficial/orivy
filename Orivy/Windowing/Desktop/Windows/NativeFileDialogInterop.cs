using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Threading;
using Orivy.Native.Windows;

using static Orivy.Native.Windows.Methods;

namespace Orivy.Windowing.Desktop.Windows;

[SupportedOSPlatform("windows")]
internal static class NativeFileDialogInterop
{
    private const int ErrorCancelledHResult = unchecked((int)0x800704C7);

    internal static string[] ShowFileSelection(IntPtr ownerHandle, FileSelectionDialog dialog)
    {
        ArgumentNullException.ThrowIfNull(dialog);

        var options = FILEOPENDIALOGOPTIONS.FOS_NOCHANGEDIR | FILEOPENDIALOGOPTIONS.FOS_DONTADDTORECENT;
        if (dialog.ForceFileSystem)
            options |= FILEOPENDIALOGOPTIONS.FOS_FORCEFILESYSTEM;
        if (dialog.CheckPathExists)
            options |= FILEOPENDIALOGOPTIONS.FOS_PATHMUSTEXIST;
        if (dialog.CheckFileExists)
            options |= FILEOPENDIALOGOPTIONS.FOS_FILEMUSTEXIST;
        if (dialog.AllowMultipleSelection)
            options |= FILEOPENDIALOGOPTIONS.FOS_ALLOWMULTISELECT;

        return ShowSelections(
            ownerHandle,
            dialog.Title,
            dialog.InitialDirectory,
            dialog.DefaultExtension,
            dialog.BuildFilterSpecs(),
            options,
            dialog.AllowMultipleSelection);
    }

    internal static string? ShowFolderSelection(IntPtr ownerHandle, FolderSelectionDialog dialog)
    {
        ArgumentNullException.ThrowIfNull(dialog);

        var options = FILEOPENDIALOGOPTIONS.FOS_PICKFOLDERS |
                      FILEOPENDIALOGOPTIONS.FOS_NOCHANGEDIR |
                      FILEOPENDIALOGOPTIONS.FOS_DONTADDTORECENT;
        if (dialog.ForceFileSystem)
            options |= FILEOPENDIALOGOPTIONS.FOS_FORCEFILESYSTEM;
        if (dialog.CheckPathExists)
            options |= FILEOPENDIALOGOPTIONS.FOS_PATHMUSTEXIST;

        var selections = ShowSelections(
            ownerHandle,
            dialog.Title,
            dialog.InitialDirectory,
            string.Empty,
            Array.Empty<COMDLG_FILTERSPEC>(),
            options,
            allowMultipleSelection: false);

        return selections.Length == 0 ? null : selections[0];
    }

    internal static string? ShowSaveFileSelection(IntPtr ownerHandle, SaveFileDialog dialog)
    {
        ArgumentNullException.ThrowIfNull(dialog);

        var options = FILEOPENDIALOGOPTIONS.FOS_NOCHANGEDIR | FILEOPENDIALOGOPTIONS.FOS_DONTADDTORECENT;
        if (dialog.ForceFileSystem)
            options |= FILEOPENDIALOGOPTIONS.FOS_FORCEFILESYSTEM;
        if (dialog.CheckPathExists)
            options |= FILEOPENDIALOGOPTIONS.FOS_PATHMUSTEXIST;
        if (dialog.CreatePrompt)
            options |= FILEOPENDIALOGOPTIONS.FOS_CREATEPROMPT;
        if (dialog.OverwritePrompt)
            options |= FILEOPENDIALOGOPTIONS.FOS_OVERWRITEPROMPT;

        return ShowSaveSelection(
            ownerHandle,
            dialog.Title,
            dialog.InitialDirectory,
            dialog.DefaultExtension,
            dialog.FileName,
            dialog.BuildFilterSpecs(),
            options);
    }

    private static string[] ShowSelections(
        IntPtr ownerHandle,
        string title,
        string initialDirectory,
        string defaultExtension,
        COMDLG_FILTERSPEC[] filterSpecs,
        FILEOPENDIALOGOPTIONS options,
        bool allowMultipleSelection)
    {
        EnsureWindows();

        IFileOpenDialog? dialog = null;
        IShellItem? initialFolder = null;
        IShellItem? selectedItem = null;
        IShellItemArray? selectedItems = null;

        try
        {
            dialog = (IFileOpenDialog)Activator.CreateInstance(typeof(FileOpenDialogComClass))!;
            dialog.GetOptions(out var currentOptions);
            dialog.SetOptions(currentOptions | options);

            if (!string.IsNullOrWhiteSpace(title))
                dialog.SetTitle(title);

            if (filterSpecs.Length > 0)
            {
                dialog.SetFileTypes((uint)filterSpecs.Length, filterSpecs);
                dialog.SetFileTypeIndex(1);
            }

            if (!string.IsNullOrWhiteSpace(defaultExtension))
                dialog.SetDefaultExtension(defaultExtension.Trim().TrimStart('.'));

            if (TryCreateInitialFolder(initialDirectory, out initialFolder) && initialFolder != null)
            {
                dialog.SetDefaultFolder(initialFolder);
                dialog.SetFolder(initialFolder);
            }

            var hr = dialog.Show(ownerHandle);
            if (hr == ErrorCancelledHResult)
                return Array.Empty<string>();

            Marshal.ThrowExceptionForHR(hr);

            if (allowMultipleSelection)
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
        string title,
        string initialDirectory,
        string defaultExtension,
        string fileName,
        COMDLG_FILTERSPEC[] filterSpecs,
        FILEOPENDIALOGOPTIONS options)
    {
        EnsureWindows();

        IFileSaveDialog? dialog = null;
        IShellItem? initialFolder = null;
        IShellItem? selectedItem = null;

        try
        {
            dialog = (IFileSaveDialog)Activator.CreateInstance(typeof(FileSaveDialogComClass))!;
            dialog.GetOptions(out var currentOptions);
            dialog.SetOptions(currentOptions | options);

            if (!string.IsNullOrWhiteSpace(title))
                dialog.SetTitle(title);

            if (filterSpecs.Length > 0)
            {
                dialog.SetFileTypes((uint)filterSpecs.Length, filterSpecs);
                dialog.SetFileTypeIndex(1);
            }

            if (!string.IsNullOrWhiteSpace(defaultExtension))
                dialog.SetDefaultExtension(defaultExtension.Trim().TrimStart('.'));

            if (!string.IsNullOrWhiteSpace(fileName))
                dialog.SetFileName(fileName);

            if (TryCreateInitialFolder(initialDirectory, out initialFolder) && initialFolder != null)
            {
                dialog.SetDefaultFolder(initialFolder);
                dialog.SetFolder(initialFolder);
            }

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

    private static void EnsureWindows()
    {
        if (!OperatingSystem.IsWindows())
            throw new PlatformNotSupportedException("Native file dialogs are only available on Windows.");

        if (Thread.CurrentThread.GetApartmentState() != ApartmentState.STA)
            throw new InvalidOperationException("Native Windows file dialogs require an STA thread. Mark the entry point with [STAThread].");
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
}
