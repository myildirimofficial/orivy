using System;
using System.Runtime.InteropServices;

namespace Orivy.Native.Windows;

[ComImport]
[Guid("84BCCD23-5FDE-4CDB-AEA4-AF64B83D78AB")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IFileSaveDialog
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

    void SetSaveAsItem(IShellItem shellItem);

    void SetProperties(IntPtr propertyStore);

    void SetCollectedProperties(IntPtr shellItemArray, [MarshalAs(UnmanagedType.Bool)] bool appendDefault);

    void GetProperties(out IntPtr propertyStore);

    void ApplyProperties(IShellItem shellItem, IntPtr propertyStore, IntPtr hwnd, IntPtr progressSink);
}