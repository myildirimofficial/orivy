namespace Orivy;

public struct WindowTabStyle
{
    public WindowTabVisual Normal { get; set; }
    public WindowTabVisual Hover { get; set; }
    public WindowTabVisual Selected { get; set; }
    public WindowTabMetrics Metrics { get; set; }
    public WindowTabHeaderStyle Header { get; set; }
    public WindowTabIndicatorStyle Indicator { get; set; }
}
