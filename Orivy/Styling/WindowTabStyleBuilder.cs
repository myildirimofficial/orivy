using System;

namespace Orivy;

public sealed class WindowTabStyleBuilder
{
    private WindowTabStyle _style;

    public WindowTabStyleBuilder() { }

    public WindowTabStyleBuilder(WindowTabStyle style)
    {
        _style = style;
    }

    public WindowTabStyle Build() => _style;

    public WindowTabStyleBuilder Normal(Action<WindowTabVisualBuilder> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);
        var builder = new WindowTabVisualBuilder(_style.Normal);
        configure(builder);
        _style.Normal = builder.Build();
        return this;
    }

    public WindowTabStyleBuilder Hover(Action<WindowTabVisualBuilder> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);
        var builder = new WindowTabVisualBuilder(_style.Hover);
        configure(builder);
        _style.Hover = builder.Build();
        return this;
    }

    public WindowTabStyleBuilder Selected(Action<WindowTabVisualBuilder> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);
        var builder = new WindowTabVisualBuilder(_style.Selected);
        configure(builder);
        _style.Selected = builder.Build();
        return this;
    }

    public WindowTabStyleBuilder Metrics(Action<WindowTabMetricsBuilder> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);
        var builder = new WindowTabMetricsBuilder(_style.Metrics);
        configure(builder);
        _style.Metrics = builder.Build();
        return this;
    }

    public WindowTabStyleBuilder Header(Action<WindowTabHeaderStyleBuilder> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);
        var builder = new WindowTabHeaderStyleBuilder(_style.Header);
        configure(builder);
        _style.Header = builder.Build();
        return this;
    }

    public WindowTabStyleBuilder Indicator(Action<WindowTabIndicatorStyleBuilder> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);
        var builder = new WindowTabIndicatorStyleBuilder(_style.Indicator);
        configure(builder);
        _style.Indicator = builder.Build();
        return this;
    }
}
