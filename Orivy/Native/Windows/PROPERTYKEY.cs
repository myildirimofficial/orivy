using System;
using System.Runtime.InteropServices;

namespace Orivy.Native.Windows;

[StructLayout(LayoutKind.Sequential, Pack = 4)]
internal struct PROPERTYKEY
{
    public Guid fmtid;
    public uint pid;
}