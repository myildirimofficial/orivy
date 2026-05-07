using SkiaSharp;

namespace Orivy;

public sealed class TabViewHeaderStyleBuilder
{
    private TabViewHeaderStyle _header;

    internal TabViewHeaderStyleBuilder(TabViewHeaderStyle header)
    {
        _header = header;
    }

    internal TabViewHeaderStyle Build() => _header;

    public TabViewHeaderStyleBuilder Background(SKColor color)
    {
        _header.BackgroundColor = color;
        return this;
    }

    public TabViewHeaderStyleBuilder Border(SKColor color)
    {
        _header.BorderColor = color;
        return this;
    }
}
