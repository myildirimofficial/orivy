namespace Orivy.Controls;

public class Element : ElementBase
{
}

public class Container : ElementBase
{
    public Container()
    {
        BackColor = SkiaSharp.SKColors.Transparent;
    }
}

public class TextBox : ElementBase
{
}
