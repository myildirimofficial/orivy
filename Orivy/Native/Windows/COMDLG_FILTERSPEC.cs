using System.Runtime.InteropServices;

namespace Orivy.Native.Windows;

[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
internal readonly struct COMDLG_FILTERSPEC
{
    public COMDLG_FILTERSPEC(string name, string spec)
    {
        pszName = name;
        pszSpec = spec;
    }

    [MarshalAs(UnmanagedType.LPWStr)]
    public readonly string pszName;

    [MarshalAs(UnmanagedType.LPWStr)]
    public readonly string pszSpec;
}