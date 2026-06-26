using Orivy.Animation;
using Orivy.Layout;
using SkiaSharp;
using System.ComponentModel;

namespace Orivy.Controls;

public class Separator : ElementBase
{
    private readonly SKPaint _linePaint = new() { IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeCap = SKStrokeCap.Round };
    private Orientation _orientation = Orientation.Horizontal;
    private float _lineThickness = 1f;

    public Separator()
    {
        BackColor = SKColors.Transparent;
        ForeColor = ColorScheme.Outline;
        Border = new Thickness(0);
        Padding = new Thickness(0);
        AutoSize = false;
        Size = new SKSize(120, 12);

        ConfigureVisualStyles(styles => styles
            .DefaultTransition(System.TimeSpan.FromMilliseconds(120), AnimationType.CubicEaseOut)
            .Base(baseStyle => baseStyle
                .Background(SKColors.Transparent)
                .Foreground(ColorScheme.Outline.WithAlpha(150))
                .Border(0)
                .Shadow(BoxShadow.None))
            .OnDisabled(rule => rule
                .Foreground(ColorScheme.Outline.WithAlpha(70))
                .Opacity(0.72f)));
    }

    [DefaultValue(Orientation.Horizontal)]
    public Orientation Orientation
    {
        get => _orientation;
        set
        {
            if (_orientation == value)
                return;

            _orientation = value;
            InvalidateMeasure();
            Invalidate();
        }
    }

    [DefaultValue(1f)]
    public float LineThickness
    {
        get => _lineThickness;
        set
        {
            var normalized = System.Math.Max(0.5f, value);
            if (System.Math.Abs(_lineThickness - normalized) < 0.001f)
                return;

            _lineThickness = normalized;
            InvalidateMeasure();
            Invalidate();
        }
    }

    protected override bool ShouldRenderDefaultText => false;

    public override SKSize GetPreferredSize(SKSize proposedSize)
    {
        var thickness = System.Math.Max(1f, LineThickness * ScaleFactor);
        return Orientation == Orientation.Horizontal
            ? new SKSize(System.Math.Max(1f, proposedSize.Width > 0 ? proposedSize.Width : Size.Width), thickness + Padding.Top + Padding.Bottom + 8f * ScaleFactor)
            : new SKSize(thickness + Padding.Left + Padding.Right + 8f * ScaleFactor, System.Math.Max(1f, proposedSize.Height > 0 ? proposedSize.Height : Size.Height));
    }

    public override void  OnPaint(SKCanvas canvas)
    {
        base.OnPaint(canvas);

        var rect = DisplayRectangle;
        if (rect.Width <= 0f || rect.Height <= 0f)
            return;

        _linePaint.Color = Enabled ? ForeColor : ForeColor.WithAlpha(90);
        _linePaint.StrokeWidth = System.Math.Max(1f, LineThickness * ScaleFactor);

        if (Orientation == Orientation.Horizontal)
        {
            var y = rect.MidY;
            canvas.DrawLine(rect.Left, y, rect.Right, y, _linePaint);
        }
        else
        {
            var x = rect.MidX;
            canvas.DrawLine(x, rect.Top, x, rect.Bottom, _linePaint);
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
            _linePaint.Dispose();

        base.Dispose(disposing);
    }
}
