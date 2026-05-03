using SkiaSharp;

namespace Orivy;

public struct TabViewVisual
{
    public SKColor BackgroundColor { get; set; }
    public SKColor ForegroundColor { get; set; }
    public SKColor BorderColor { get; set; }
    public float? BorderRadius { get; set; }
    public float? BorderThickness { get; set; }
    public float? Blur { get; set; }
}
