namespace Orivy;

public struct TabViewStyle
{
    public TabViewVisual Normal { get; set; }
    public TabViewVisual Hover { get; set; }
    public TabViewVisual Selected { get; set; }
    public TabViewMetrics Metrics { get; set; }
    public TabViewHeaderStyle Header { get; set; }
    public TabViewIndicatorStyle Indicator { get; set; }
}
