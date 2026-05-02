using SkiaSharp;
using System;

namespace Orivy;

public sealed class WindowTabIndicatorStyleBuilder
{
    private WindowTabIndicatorStyle _indicator;

    internal WindowTabIndicatorStyleBuilder(WindowTabIndicatorStyle indicator)
    {
        _indicator = indicator;
    }

    internal WindowTabIndicatorStyle Build() => _indicator;

    public WindowTabIndicatorStyleBuilder Color(SKColor color)
    {
        _indicator.Color = color;
        return this;
    }

    public WindowTabIndicatorStyleBuilder Thickness(float thickness)
    {
        _indicator.Thickness = Math.Max(0f, thickness);
        return this;
    }
}
