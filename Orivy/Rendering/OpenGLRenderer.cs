using System;
using System.Runtime.InteropServices;
using SkiaSharp;
using Orivy.Native.Windows;

namespace Orivy.Rendering;

internal sealed class OpenGLRenderer : IWindowRenderer
{
    private nint _hwnd;
    private nint _glContext;
    private nint _deviceContext;
    private GRContext? _grContext;
    private SKSurface? _surface;
    private int _width;
    private int _height;
    private bool _disposed;
    private bool _isInitialized;

    public bool IsSkiaGpuActive => _grContext != null && !_disposed;
    public RenderBackend Backend => RenderBackend.OpenGL;
    public GRContext? GrContext => _grContext;

    public void Initialize(nint hwnd)
    {
        if (hwnd == IntPtr.Zero)
            throw new ArgumentException("Invalid window handle", nameof(hwnd));

        if (_isInitialized)
            return;

        _hwnd = hwnd;
        _deviceContext = WglNativeMethods.GetDC(_hwnd);
        if (_deviceContext == IntPtr.Zero)
            throw new InvalidOperationException("Failed to get device context for OpenGL rendering.");

        try
        {
            var pfd = new PIXELFORMATDESCRIPTOR
            {
                nSize = (ushort)Marshal.SizeOf<PIXELFORMATDESCRIPTOR>(),
                nVersion = 1,
                dwFlags = 0x00000004 | 0x00000020 | 0x00000001,
                iPixelType = 0,
                cColorBits = 32,
                cAlphaBits = 8,
                cDepthBits = 24,
                cStencilBits = 8,
                iLayerType = 0
            };

            int pixelFormat = WglNativeMethods.ChoosePixelFormat(_deviceContext, ref pfd);
            if (pixelFormat == 0)
                throw new InvalidOperationException("Failed to choose OpenGL pixel format.");

            if (!WglNativeMethods.SetPixelFormat(_deviceContext, pixelFormat, ref pfd))
                throw new InvalidOperationException("Failed to set OpenGL pixel format.");

            _glContext = WglNativeMethods.wglCreateContext(_deviceContext);
            if (_glContext == IntPtr.Zero)
                throw new InvalidOperationException("Failed to create OpenGL rendering context.");

            if (!WglNativeMethods.wglMakeCurrent(_deviceContext, _glContext))
            {
                WglNativeMethods.wglDeleteContext(_glContext);
                _glContext = IntPtr.Zero;
                throw new InvalidOperationException("Failed to make OpenGL context current.");
            }

            var glInterface = GRGlInterface.Create();
            if (glInterface == null)
            {
                WglNativeMethods.wglMakeCurrent(IntPtr.Zero, IntPtr.Zero);
                WglNativeMethods.wglDeleteContext(_glContext);
                _glContext = IntPtr.Zero;
                throw new InvalidOperationException("Failed to assemble OpenGL interface for SkiaSharp.");
            }

            _grContext = GRContext.CreateGl(glInterface);
            if (_grContext == null)
            {
                WglNativeMethods.wglMakeCurrent(IntPtr.Zero, IntPtr.Zero);
                WglNativeMethods.wglDeleteContext(_glContext);
                _glContext = IntPtr.Zero;
                throw new InvalidOperationException("Failed to create SkiaSharp GRContext for OpenGL.");
            }

            _isInitialized = true;
        }
        catch
        {
            Dispose();
            throw;
        }
    }

    public void Resize(int width, int height)
    {
        if (_disposed || !_isInitialized)
            return;

        if (width <= 0 || height <= 0)
            return;

        if (_glContext != IntPtr.Zero && _deviceContext != IntPtr.Zero)
        {
            WglNativeMethods.wglMakeCurrent(_deviceContext, _glContext);
            WglNativeMethods.glViewport(0, 0, width, height);
        }

        _surface?.Dispose();
        _surface = null;
        _width = width;
        _height = height;
    }

    public bool Render(int width, int height, Action<SKCanvas, SKImageInfo> draw)
    {
        if (_disposed || !_isInitialized || _hwnd == IntPtr.Zero)
            return false;

        if (width <= 0 || height <= 0 || _grContext == null)
            return false;

        if (!WglNativeMethods.wglMakeCurrent(_deviceContext, _glContext))
        {
            System.Diagnostics.Debug.WriteLine("[OpenGLRenderer] wglMakeCurrent failed");
            return false;
        }

        try
        {
            WglNativeMethods.glViewport(0, 0, width, height);

            if (_surface == null || _width != width || _height != height)
            {
                _surface?.Dispose();
                _surface = null;

                int stencilBits = WglNativeMethods.glGetIntegervSafe(0x1E05);
                int sampleCount = 0;

                uint fboId = 0;

                var framebufferInfo = new GRGlFramebufferInfo(fboId, 0x8058);
                var backendRenderTarget = new GRBackendRenderTarget(
                    width,
                    height,
                    sampleCount,
                    stencilBits,
                    framebufferInfo);

                _surface = SKSurface.Create(
                    _grContext,
                    backendRenderTarget,
                    GRSurfaceOrigin.BottomLeft,
                    SKColorType.Bgra8888);

                if (_surface == null)
                {
                    System.Diagnostics.Debug.WriteLine("[OpenGLRenderer] SKSurface.Create returned null");
                    return false;
                }

                _width = width;
                _height = height;
            }

            var canvas = _surface?.Canvas;
            if (canvas == null)
            {
                System.Diagnostics.Debug.WriteLine("[OpenGLRenderer] Canvas is null");
                return false;
            }

            canvas.Clear(SKColors.Transparent);
            draw(canvas, new SKImageInfo(width, height, SKColorType.Bgra8888, SKAlphaType.Premul));
            canvas.Flush();

            WglNativeMethods.SwapBuffers(_deviceContext);
            _grContext.Flush();

            return true;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[OpenGLRenderer] Render exception: {ex.Message}");
            _surface?.Dispose();
            _surface = null;
            return false;
        }
    }

    public void TrimCaches()
    {
        if (_disposed || _grContext == null)
            return;

        _grContext.PurgeResources();
        
        _surface?.Dispose();
        _surface = null;
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _surface?.Dispose();
        _surface = null;
        _grContext?.Dispose();
        _grContext = null;

        if (_glContext != IntPtr.Zero)
        {
            if (_deviceContext != IntPtr.Zero)
            {
                WglNativeMethods.wglMakeCurrent(IntPtr.Zero, IntPtr.Zero);
            }
            WglNativeMethods.wglDeleteContext(_glContext);
            _glContext = IntPtr.Zero;
        }

        if (_deviceContext != IntPtr.Zero && _hwnd != IntPtr.Zero)
        {
            WglNativeMethods.ReleaseDC(_hwnd, _deviceContext);
            _deviceContext = IntPtr.Zero;
        }

        _disposed = true;
        _isInitialized = false;
    }
}