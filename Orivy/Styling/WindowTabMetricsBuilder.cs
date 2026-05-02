using System;

namespace Orivy;

public sealed class WindowTabMetricsBuilder
{
    private WindowTabMetrics _metrics;

    internal WindowTabMetricsBuilder(WindowTabMetrics metrics)
    {
        _metrics = metrics;
    }

    internal WindowTabMetrics Build() => _metrics;

    public WindowTabMetricsBuilder Padding(int horizontal, int vertical)
    {
        _metrics.Padding = new Thickness(horizontal, vertical, horizontal, vertical);
        return this;
    }

    public WindowTabMetricsBuilder Padding(Thickness padding)
    {
        _metrics.Padding = padding;
        return this;
    }

    public WindowTabMetricsBuilder SurfaceInset(int all)
    {
        _metrics.SurfaceInset = new Thickness(all);
        return this;
    }

    public WindowTabMetricsBuilder SurfaceInset(Thickness inset)
    {
        _metrics.SurfaceInset = inset;
        return this;
    }

    public WindowTabMetricsBuilder Gap(float gap)
    {
        _metrics.Gap = Math.Max(0f, gap);
        return this;
    }

    public WindowTabMetricsBuilder Width(float min, float max)
    {
        _metrics.MinWidth = Math.Max(0f, min);
        _metrics.MaxWidth = Math.Max(_metrics.MinWidth.Value, max);
        return this;
    }

    public WindowTabMetricsBuilder Height(float min, float max)
    {
        _metrics.MinHeight = Math.Max(0f, min);
        _metrics.MaxHeight = Math.Max(_metrics.MinHeight.Value, max);
        return this;
    }
}
