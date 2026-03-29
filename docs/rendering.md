Rendering notes — Skia, GPU, and best practices
===============================================

This page explains how Orivy uses SkiaSharp for rendering, the GPU vs CPU rendering paths, and full-stack best practices for paints, fonts, and resources.

## Rendering pipeline overview

Orivy renders in a retained-mode compositing pipeline:

1. The window invalidation system triggers `PerformLayout` + `Invalidate` and eventually `OnPaint` on root `WindowBase`.
2. `WindowBase` delegates draw to the active `IWindowRenderer` implementation in `Orivy/Rendering`.
3. `IWindowRenderer.Render(width, height, draw)` provides an `SKCanvas` and a callback that runs the scene graph painting.
4. Each control calls its `OnPaint(SKCanvas canvas)`; the framework may render overlapping children, states, and backdrops.

Control-level rendering occurs in `Orivy/Controls/ElementBase.cs` via overrideable `OnPaint`.

## Render backends in Orivy

Backend selection lives in `Orivy/Rendering/RendererFactory.cs`.

- `SoftwareRenderer` (fallback): `Orivy/Rendering/SoftwareRenderer.cs`.
  - Creates a memory DIB via Win32 GDI, wraps it in a CPU `SKSurface` (`SKImageInfo` Bgra8888), draws into it, and copies to window using `BitBlt` or `AlphaBlend`.
  - `TrimCaches()` frees and recreates DIB resources on demand.

- `OpenGLRenderer` (hardware acceleration): `Orivy/Rendering/OpenGLRenderer.cs`.
  - Uses Win32 wgl calls to create an OpenGL context.
  - Creates a Skia `GRContext` and an `SKSurface` bound to the native framebuffer.
  - Handles `Resize`, context activation, and `SwapBuffers`.

- `RenderBackend.DirectX11` is reserved for future or platform-specific implementation (not implemented in this code path). `Vulkan`/`Metal` are placeholders.

### Key interface: `Orivy/Rendering/IWindowRenderer.cs`

- `bool IsSkiaGpuActive { get; }
- `RenderBackend Backend { get; }
- `GRContext? GrContext { get; }
- `Initialize(nint hwnd)`
- `Resize(int width, int height)`
- `bool Render(int width, int height, Action<SKCanvas, SKImageInfo> draw)`
- `void TrimCaches()`

## Skia resources and object lifecycle

### SKCanvas and SKSurface

- `SKCanvas` is always local to a frame; do not retain across frames."
- `SKSurface` is created each frame in software path or cached in GPU path.
- `WindowBase` uses `GRContext` for GPU where available (`renderer.IsSkiaGpuActive`).

### Paint objects and caching

- Avoid allocating `new SKPaint` in tight loops. Share or scope it in the control instance.
- Same applies for `SKPath`, `SKShader`, `SKFont`.
- Dispose resources in `Dispose()` or when DPI changes (`OnDpiChanged` on controls).

### Color spaces

- `ElementBase` uses `SKColorSpace.CreateSrgb()` when available.
- If `SKColorSpace.CreateSrgb()` throws, fallback to default null color space.

## Text rendering best practices

- `TextRenderer.MeasureText` is the canonical path for layout.
- `LayoutUtils.MeasureTextCache` (layout helper) caches MRU results and avoids repeated wrapping measurement.
- `Button.GetPreferredSize` demonstrates robust usage: clamps width/height, includes padding and border, and respects min/max size.

## High-level rendering scenarios

### 1. Basic control custom paint

```csharp
public class IconLabel : ElementBase
{
    private SKPaint _textPaint = new SKPaint { Color = SKColors.Black, IsAntialias = true };

    public override void OnPaint(SKCanvas canvas)
    {
        base.OnPaint(canvas);

        canvas.Clear(BackColor);
        canvas.DrawText(Text, Padding.Left, Padding.Top + Font.Metrics.Ascent, _textPaint);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
            _textPaint.Dispose();

        base.Dispose(disposing);
    }
}
```

### 2. Animate styles with opacity (from Animation system)

```csharp
public class FadeInControl : ElementBase
{
    private readonly AnimationManager _animation = new AnimationManager();

    public FadeInControl()
    {
        _animation.OnAnimationProgress += _ => Invalidate();
        _animation.StartNewAnimation(AnimationDirection.In);
    }

    public override void OnPaint(SKCanvas canvas)
    {
        base.OnPaint(canvas);

        var alpha = (byte)(_animation.GetProgress() * 255);
        using var paint = new SKPaint { Color = BackColor.WithAlpha(alpha) };
        canvas.DrawRect(ClientRectangle, paint);
    }
}
```

## Error handling and robustness

- If `RendererFactory` throws, fall back to `SoftwareRenderer`.
- `OpenGLRenderer` catches initialization patterns and throws descriptive errors (`InvalidOperationException`, `NotSupportedException`).
- `SoftwareRenderer` returns false safely when sizes are invalid, or lock on HDC fails.

## Debugging tips

- If UI is blank, check `WindowBase` log for renderer failure and fallback path.
- Enable `ElementBase` debug invalidations via `DebugSettings.EnableRenderLogging`.
- Validate that `OnPaint` does not throw; framework catches and writes `Debug.WriteLine` messages.

## Where to look

- `Orivy/Rendering/SoftwareRenderer.cs`
- `Orivy/Rendering/OpenGLRenderer.cs`
- `Orivy/Rendering/RendererFactory.cs`
- `Orivy/Rendering/IWindowRenderer.cs`
- `Orivy/Controls/ElementBase.cs` (painting and DPI handling)
- `Orivy/Application.cs` (initial renderer selection and window loop)
