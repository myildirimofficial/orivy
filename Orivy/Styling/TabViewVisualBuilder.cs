using SkiaSharp;
using System;

namespace Orivy;

public sealed class TabViewVisualBuilder
{
    private TabViewVisual _visual;

    internal TabViewVisualBuilder(TabViewVisual visual)
    {
        _visual = visual;
    }

    internal TabViewVisual Build() => _visual;

    public TabViewVisualBuilder Background(SKColor color)
    {
        _visual.BackgroundColor = color;
        return this;
    }

    public TabViewVisualBuilder Foreground(SKColor color)
    {
        _visual.ForegroundColor = color;
        return this;
    }

    public TabViewVisualBuilder Border(SKColor color, float? thickness = null)
    {
        _visual.BorderColor = color;
        if (thickness.HasValue)
            _visual.BorderThickness = Math.Max(0f, thickness.Value);
        return this;
    }

    public TabViewVisualBuilder Radius(float radius)
    {
        _visual.BorderRadius = Math.Max(0f, radius);
        return this;
    }

    public TabViewVisualBuilder Blur(float blur)
    {
        _visual.Blur = Math.Max(0f, blur);
        return this;
    }
}
