using System;

namespace Orivy;

public sealed class TabViewMetricsBuilder
{
    private TabViewMetrics _metrics;

    internal TabViewMetricsBuilder(TabViewMetrics metrics)
    {
        _metrics = metrics;
    }

    internal TabViewMetrics Build() => _metrics;

    public TabViewMetricsBuilder Padding(int horizontal, int vertical)
    {
        _metrics.Padding = new Thickness(horizontal, vertical, horizontal, vertical);
        return this;
    }

    public TabViewMetricsBuilder Padding(Thickness padding)
    {
        _metrics.Padding = padding;
        return this;
    }

    public TabViewMetricsBuilder SurfaceInset(int all)
    {
        _metrics.SurfaceInset = new Thickness(all);
        return this;
    }

    public TabViewMetricsBuilder SurfaceInset(Thickness inset)
    {
        _metrics.SurfaceInset = inset;
        return this;
    }

    public TabViewMetricsBuilder Gap(float gap)
    {
        _metrics.Gap = Math.Max(0f, gap);
        return this;
    }

    public TabViewMetricsBuilder Width(float min, float max)
    {
        _metrics.MinWidth = Math.Max(0f, min);
        _metrics.MaxWidth = Math.Max(_metrics.MinWidth.Value, max);
        return this;
    }

    public TabViewMetricsBuilder Height(float min, float max)
    {
        _metrics.MinHeight = Math.Max(0f, min);
        _metrics.MaxHeight = Math.Max(_metrics.MinHeight.Value, max);
        return this;
    }
}
