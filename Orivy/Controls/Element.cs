namespace Orivy.Controls;

public class Element : ElementBase;

public class Container : ElementBase
{
    public Container()
    {
        BackColor = SkiaSharp.SKColors.Transparent;
        // Panels / tab pages host children; Image is chrome (e.g. tab glyph), not body content.
        RenderImageAsContent = false;
    }
}
