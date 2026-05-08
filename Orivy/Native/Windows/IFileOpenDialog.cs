using System;
using System.Runtime.InteropServices;

namespace Orivy.Native.Windows;

[ComImport]
[Guid("d57c7288-d4ad-4768-be02-9d969532d960")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IFileOpenDialog
{
    [PreserveSig]
    int Show(IntPtr parent);

    void SetFileTypes(uint cFileTypes, [MarshalAs(UnmanagedType.LPArray)] COMDLG_FILTERSPEC[] filterSpec);

    void SetFileTypeIndex(uint fileTypeIndex);

    void GetFileTypeIndex(out uint fileTypeIndex);

    void Advise(IntPtr fileDialogEvents, out uint cookie);

    void Unadvise(uint cookie);

    void SetOptions(FILEOPENDIALOGOPTIONS options);

    void GetOptions(out FILEOPENDIALOGOPTIONS options);

    void SetDefaultFolder(IShellItem shellItem);

    void SetFolder(IShellItem shellItem);

    void GetFolder(out IShellItem shellItem);

    void GetCurrentSelection(out IShellItem shellItem);

    void SetFileName([MarshalAs(UnmanagedType.LPWStr)] string name);

    void GetFileName([MarshalAs(UnmanagedType.LPWStr)] out string name);

    void SetTitle([MarshalAs(UnmanagedType.LPWStr)] string title);

    void SetOkButtonLabel([MarshalAs(UnmanagedType.LPWStr)] string text);

    void SetFileNameLabel([MarshalAs(UnmanagedType.LPWStr)] string label);

    void GetResult(out IShellItem shellItem);

    void AddPlace(IShellItem shellItem, int alignment);

    void SetDefaultExtension([MarshalAs(UnmanagedType.LPWStr)] string defaultExtension);

    void Close(int hr);

    void SetClientGuid(ref Guid guid);

    void ClearClientData();

    void SetFilter(IntPtr filter);

    void GetResults(out IShellItemArray shellItemArray);

    void GetSelectedItems(out IShellItemArray shellItemArray);
}