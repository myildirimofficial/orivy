using System;
using System.Runtime.InteropServices;

namespace Orivy.Native.Windows;

[ComImport]
[Guid("43826D1E-E718-42EE-BC55-A1E261C37BFE")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IShellItem
{
    void BindToHandler(IntPtr bindingContext, ref Guid handlerGuid, ref Guid interfaceGuid, out IntPtr result);

    void GetParent(out IShellItem shellItem);

    void GetDisplayName(SIGDN displayNameKind, out IntPtr displayName);

    void GetAttributes(uint attributesMask, out uint attributes);

    void Compare(IShellItem shellItem, uint hint, out int order);
}