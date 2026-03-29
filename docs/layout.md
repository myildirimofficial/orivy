
Layout deep dive — Measure and Arrange
=====================================

Orivy's layout system uses a two-phase model: Measure and Arrange. This gives controls the opportunity to declare desired sizes before the container determines final bounds.

Why two phases
- Measurement decouples size calculation from final placement. A control's preferred width or height may depend on available width, wrapping, or other children.

APIs you need to know
- `GetPreferredSize(SKSize proposedSize)` — return the desired size given the available space.
- `PerformLayout()` — triggers a layout pass for a container and its children.
- `IArrangedElement.SetBounds(SKRect bounds, BoundsSpecified specified)` — invoked by the layout engine to apply final bounds to children.

Common patterns
- Text-based controls: typically measure text using `TextRenderer.MeasureText` with trimming/wrapping options, add padding and border thickness to compute preferred size (see `Button.GetPreferredSize`).
- Containers: measure children (possibly in two passes for complex layouts), compute final child rectangles, then call `SetBounds`.

Example — implementing a small reusable badge control

```csharp
public class Badge : Element
{
    public override SKSize GetPreferredSize(SKSize proposedSize)
    {
        // 1) ensure we have a reasonable measurement width
        var measurement = proposedSize;
        if (measurement.Width <= 0) measurement.Width = float.MaxValue;

        // 2) measure the text with TextRenderer
        var textSize = TextRenderer.MeasureText(
            Text,
            Font,
            measurement,
            new TextRenderOptions { MaxWidth = measurement.Width, Wrap = TextWrap.None });

        // 3) add padding and border
        var width = textSize.Width + Padding.Left + Padding.Right + Border.Left + Border.Right;
        var height = textSize.Height + Padding.Top + Padding.Bottom + Border.Top + Border.Bottom;

        return new SKSize((float)Math.Ceiling(width), (float)Math.Ceiling(height));
    }

    public override void OnPaint(SKCanvas canvas)
    {
        base.OnPaint(canvas);
        // Draw background using current visual style
        using var p = new SKPaint { Color = ColorScheme.Primary };
        canvas.DrawRoundRect(DisplayRectangle, 6, 6, p);
        // Render text using TextRenderer
    }
}
```

Advanced layout topics
- `AutoSize`: controls that autosize typically return their preferred size from `GetPreferredSize` and the container respects that during arrange.
- `Dock` and `Anchor`: WinForms-like semantics are implemented by the layout engine in `Orivy/Layout` — use them for responsive resizing inside containers.
- Performance: avoid allocating objects during measurement and arrange. Reuse temporary buffers (like float arrays) and cache measurements when possible.

Debugging layout
- Insert logging in `GetPreferredSize` and `OnSizeChanged` to track measurement and arrangement sizes.
- Use `SuspendLayout()` and `ResumeLayout()` while adding many children to reduce layout churn.

