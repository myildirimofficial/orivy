using SkiaSharp;

namespace Orivy;

public sealed class WindowTabHeaderStyleBuilder
{
    private WindowTabHeaderStyle _header;

    internal WindowTabHeaderStyleBuilder(WindowTabHeaderStyle header)
    {
        _header = header;
    }

    internal WindowTabHeaderStyle Build() => _header;

    public WindowTabHeaderStyleBuilder Background(SKColor color)
    {
        _header.BackgroundColor = color;
        return this;
    }

    public WindowTabHeaderStyleBuilder Border(SKColor color)
    {
        _header.BorderColor = color;
        return this;
    }
}
