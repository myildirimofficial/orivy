using System;
using System.Runtime.InteropServices;
using System.Text;

namespace Orivy.Helpers;

internal static class ClipboardHelper
{
    private const uint CfUnicodeText = 13;
    private const uint GmemMoveable = 0x0002;
    private const uint GmemZeroInit = 0x0040;
    private static readonly object s_sync = new();

    public static bool TryGetText(out string text)
    {
        text = string.Empty;
        if (!OperatingSystem.IsWindows())
            return false;

        lock (s_sync)
        {
            if (!OpenClipboard(IntPtr.Zero))
                return false;

            try
            {
                var handle = GetClipboardData(CfUnicodeText);
                if (handle == IntPtr.Zero)
                    return false;

                var locked = GlobalLock(handle);
                if (locked == IntPtr.Zero)
                    return false;

                try
                {
                    text = Marshal.PtrToStringUni(locked) ?? string.Empty;
                    return true;
                }
                finally
                {
                    GlobalUnlock(handle);
                }
            }
            finally
            {
                CloseClipboard();
            }
        }
    }

    public static bool TrySetText(string? text)
    {
        if (!OperatingSystem.IsWindows())
            return false;

        text ??= string.Empty;
        lock (s_sync)
        {
            if (!OpenClipboard(IntPtr.Zero))
                return false;

            IntPtr memoryHandle = IntPtr.Zero;

            try
            {
                if (!EmptyClipboard())
                    return false;

                var bytes = Encoding.Unicode.GetBytes(text + '\0');
                memoryHandle = GlobalAlloc(GmemMoveable | GmemZeroInit, (UIntPtr)bytes.Length);
                if (memoryHandle == IntPtr.Zero)
                    return false;

                var target = GlobalLock(memoryHandle);
                if (target == IntPtr.Zero)
                    return false;

                try
                {
                    Marshal.Copy(bytes, 0, target, bytes.Length);
                }
                finally
                {
                    GlobalUnlock(memoryHandle);
                }

                if (SetClipboardData(CfUnicodeText, memoryHandle) == IntPtr.Zero)
                    return false;

                memoryHandle = IntPtr.Zero;
                return true;
            }
            finally
            {
                if (memoryHandle != IntPtr.Zero)
                    GlobalFree(memoryHandle);

                CloseClipboard();
            }
        }
    }

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool OpenClipboard(IntPtr newOwner);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CloseClipboard();

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool EmptyClipboard();

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr GetClipboardData(uint format);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SetClipboardData(uint format, IntPtr memoryHandle);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr GlobalAlloc(uint flags, UIntPtr bytes);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr GlobalFree(IntPtr memoryHandle);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr GlobalLock(IntPtr memoryHandle);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GlobalUnlock(IntPtr memoryHandle);
}