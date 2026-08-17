namespace Orivy;

public class SystemInformation
{
    public static int CaretBlinkTime => 550;

    public static bool IsHighDpi => true; // Placeholder for actual DPI detection logic
    public static int MouseWheelScrollLines => 3; // Placeholder for actual system setting retrieval
    public static SkiaSharp.SKSize DragSize => new SkiaSharp.SKSize(3, 3); // Placeholder for actual system setting retrieval
}
