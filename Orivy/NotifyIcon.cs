using Orivy.Controls;
using Orivy.Native.Windows;
using System;
using System.Drawing;
using System.Runtime.InteropServices;
using static Orivy.Native.Windows.Methods;

namespace Orivy;

public class NotifyIcon : IDisposable
{
    private const int NIF_MESSAGE = 0x00000001;
    private const int NIF_ICON = 0x00000002;
    private const int NIF_TIP = 0x00000004;
    private const int NIF_INFO = 0x00000010;
    private const int WM_MOUSEMOVE = 0x0200;
    private const int WM_LBUTTONDOWN = 0x0201;
    private const int WM_LBUTTONUP = 0x0202;
    private const int WM_RBUTTONDOWN = 0x0204;
    private const int NIM_ADD = 0;
    private const int NIM_MODIFY = 1;
    private const int NIM_DELETE = 2;
    private const int NIIF_INFO = 1;
    private const int NIIF_WARNING = 2;
    private const int NIIF_ERROR = 3;
    private const int NIIF_USER = 4;

    private IntPtr _hIcon;
    private IntPtr _windowHandle;
    private uint _uid;
    private bool _disposed;

    public event EventHandler Click;
    public event EventHandler DoubleClick;
    public event MouseEventHandler MouseMove;

    public string Title { get; set; } = string.Empty;
    public string Text { get; set; } = string.Empty;
    public bool Visible { get; set; } = true;

    public NotifyIcon()
    {
        _uid = (uint)Guid.NewGuid().GetHashCode() & 0xFFFF;
    }

    public Icon Icon
    {
        get => _hIcon != IntPtr.Zero ? Icon.FromHandle(_hIcon) : null;
        set
        {
            _hIcon = value?.Handle ?? IntPtr.Zero;
            UpdateIcon();
        }
    }

    public ContextMenuStrip ContextMenu { get; set; }

    public void AttachToWindow(IntPtr windowHandle)
    {
        _windowHandle = windowHandle;
        CreateIcon();
    }

    public void ShowBalloonTip(int timeoutMs = 5000, string title = "", string text = "", MessageBoxIcon icon = MessageBoxIcon.Information)
    {
        if (!_disposed && Visible && _windowHandle != IntPtr.Zero)
        {
            var info = new NOTIFYICONDATA
            {
                cbSize = (uint)Marshal.SizeOf<NOTIFYICONDATA>(),
                hWnd = _windowHandle,
                uID = _uid,
                uFlags = NIF_INFO,
                uInfoFlags = icon switch
                {
                    MessageBoxIcon.Error => NIIF_ERROR,
                    MessageBoxIcon.Warning => NIIF_WARNING,
                    MessageBoxIcon.Question => NIIF_USER,
                    MessageBoxIcon.Information => NIIF_INFO,
                    _ => NIIF_INFO
                },
                szInfoTitle = string.IsNullOrWhiteSpace(title) ? Title : title,
                szInfo = string.IsNullOrWhiteSpace(text) ? Text : text,
                uTimeout = (uint)timeoutMs
            };
            Shell_NotifyIcon(NIM_MODIFY, ref info);
        }
    }

    public void Show() => CreateIcon();
    public void Hide() => DestroyIcon();
    public void DisposeIcon() => Dispose();

    private void CreateIcon()
    {
        if (_windowHandle == IntPtr.Zero) return;

        var nid = new NOTIFYICONDATA
        {
            cbSize = (uint)Marshal.SizeOf<NOTIFYICONDATA>(),
            hWnd = _windowHandle,
            uID = _uid,
            uFlags = NIF_MESSAGE | NIF_ICON | NIF_TIP,
            uCallbackMessage = (uint)WM_MOUSEMOVE,
            hIcon = _hIcon,
            szTip = Text,
            szInfoTitle = Title
        };
        Shell_NotifyIcon(NIM_ADD, ref nid);
    }

    private void DestroyIcon()
    {
        if (_windowHandle == IntPtr.Zero) return;
        
        var nid = new NOTIFYICONDATA
        {
            cbSize = (uint)Marshal.SizeOf<NOTIFYICONDATA>(),
            hWnd = _windowHandle,
            uID = _uid
        };
        Shell_NotifyIcon(NIM_DELETE, ref nid);
    }

    private void UpdateIcon()
    {
        if (_windowHandle != IntPtr.Zero)
        {
            DestroyIcon();
            CreateIcon();
        }
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            DestroyIcon();
            _disposed = true;
        }
    }
}