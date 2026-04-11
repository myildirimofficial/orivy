namespace Orivy.Controls;

internal readonly record struct WindowPageChromeLayoutContext(
    float StartX,
    float AvailableWidth,
    float Top,
    float Height,
    float CenterY,
    float MaxTabWidth)
{
    public float Bottom => Top + Height;
}