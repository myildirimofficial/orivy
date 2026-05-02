using SkiaSharp;
using System;

namespace Orivy;

public sealed class WindowTabVisualBuilder
{
    private WindowTabVisual _visual;

    internal WindowTabVisualBuilder(WindowTabVisual visual)
    {
        _visual = visual;
    }

    internal WindowTabVisual Build() => _visual;

    public WindowTabVisualBuilder Background(SKColor color)
    {
        _visual.BackgroundColor = color;
        return this;
    }

    public WindowTabVisualBuilder Foreground(SKColor color)
    {
        _visual.ForegroundColor = color;
        return this;
    }

    public WindowTabVisualBuilder Border(SKColor color, float? thickness = null)
    {
        _visual.BorderColor = color;
        if (thickness.HasValue)
            _visual.BorderThickness = Math.Max(0f, thickness.Value);
        return this;
    }

    public WindowTabVisualBuilder Radius(float radius)
    {
        _visual.BorderRadius = Math.Max(0f, radius);
        return this;
    }

    public WindowTabVisualBuilder Blur(float blur)
    {
        _visual.Blur = Math.Max(0f, blur);
        return this;
    }
}
