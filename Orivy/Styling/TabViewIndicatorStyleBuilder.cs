using SkiaSharp;
using System;

namespace Orivy;

public sealed class TabViewIndicatorStyleBuilder
{
    private TabViewIndicatorStyle _indicator;

    internal TabViewIndicatorStyleBuilder(TabViewIndicatorStyle indicator)
    {
        _indicator = indicator;
    }

    internal TabViewIndicatorStyle Build() => _indicator;

    public TabViewIndicatorStyleBuilder Color(SKColor color)
    {
        _indicator.Color = color;
        return this;
    }

    public TabViewIndicatorStyleBuilder Thickness(float thickness)
    {
        _indicator.Thickness = Math.Max(0f, thickness);
        return this;
    }
}
