using System;
using System.Runtime.InteropServices;

namespace Orivy.Native.Windows;

[ComImport]
[Guid("b63ea76d-1f85-456f-a19c-48159efa858b")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IShellItemArray
{
    void BindToHandler(IntPtr bindingContext, ref Guid handlerGuid, ref Guid interfaceGuid, out IntPtr result);

    void GetPropertyStore(int flags, ref Guid interfaceGuid, out IntPtr propertyStore);

    void GetPropertyDescriptionList(ref PROPERTYKEY keyType, ref Guid interfaceGuid, out IntPtr propertyDescriptionList);

    void GetAttributes(int attributeFlags, uint attributesMask, out uint attributes);

    void GetCount(out uint count);

    void GetItemAt(uint index, out IShellItem shellItem);

    void EnumItems(out IntPtr enumShellItems);
}