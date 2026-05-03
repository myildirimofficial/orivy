using System;

namespace Orivy;

public sealed class TabViewStyleBuilder
{
    private TabViewStyle _style;

    public TabViewStyleBuilder() { }

    public TabViewStyleBuilder(TabViewStyle style)
    {
        _style = style;
    }

    public TabViewStyle Build() => _style;

    public TabViewStyleBuilder Normal(Action<TabViewVisualBuilder> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);
        var builder = new TabViewVisualBuilder(_style.Normal);
        configure(builder);
        _style.Normal = builder.Build();
        return this;
    }

    public TabViewStyleBuilder Hover(Action<TabViewVisualBuilder> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);
        var builder = new TabViewVisualBuilder(_style.Hover);
        configure(builder);
        _style.Hover = builder.Build();
        return this;
    }

    public TabViewStyleBuilder Selected(Action<TabViewVisualBuilder> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);
        var builder = new TabViewVisualBuilder(_style.Selected);
        configure(builder);
        _style.Selected = builder.Build();
        return this;
    }

    public TabViewStyleBuilder Metrics(Action<TabViewMetricsBuilder> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);
        var builder = new TabViewMetricsBuilder(_style.Metrics);
        configure(builder);
        _style.Metrics = builder.Build();
        return this;
    }

    public TabViewStyleBuilder Header(Action<TabViewHeaderStyleBuilder> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);
        var builder = new TabViewHeaderStyleBuilder(_style.Header);
        configure(builder);
        _style.Header = builder.Build();
        return this;
    }

    public TabViewStyleBuilder Indicator(Action<TabViewIndicatorStyleBuilder> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);
        var builder = new TabViewIndicatorStyleBuilder(_style.Indicator);
        configure(builder);
        _style.Indicator = builder.Build();
        return this;
    }
}
