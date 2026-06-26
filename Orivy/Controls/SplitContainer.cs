using Orivy.Layout;
using SkiaSharp;
using System;
using System.ComponentModel;

namespace Orivy.Controls;

public class SplitContainer : Container
{
    private readonly SKPaint _splitterPaint = new() { IsAntialias = true, Style = SKPaintStyle.Fill };
    private readonly SKPaint _gripPaint = new() { IsAntialias = true, Style = SKPaintStyle.Fill };
    private Orientation _orientation = Orientation.Vertical;
    private float _splitterDistance = 180f;
    private float _splitterWidth = 6f;
    private bool _dragging;
    private bool _splitterHovered;

    public SplitContainer()
    {
        Panel1 = new Element { Name = "panel1", BackColor = SKColors.Transparent, Border = new Thickness(0) };
        Panel2 = new Element { Name = "panel2", BackColor = SKColors.Transparent, Border = new Thickness(0) };
        Controls.Add(Panel2);
        Controls.Add(Panel1);
        BackColor = SKColors.Transparent;
        MinimumSize = new SKSize(80, 60);
        Cursor = Cursors.Default;
    }

    [Browsable(false)]
    public Element Panel1 { get; }

    [Browsable(false)]
    public Element Panel2 { get; }

    [DefaultValue(Orientation.Vertical)]
    public Orientation Orientation
    {
        get => _orientation;
        set
        {
            if (_orientation == value)
                return;

            _orientation = value;
            PerformLayout();
            Invalidate();
        }
    }

    [DefaultValue(180f)]
    public float SplitterDistance
    {
        get => _splitterDistance;
        set
        {
            var normalized = Math.Max(0f, value);
            if (Math.Abs(_splitterDistance - normalized) < 0.001f)
                return;

            _splitterDistance = normalized;
            PerformLayout();
            Invalidate();
        }
    }

    [DefaultValue(6f)]
    public float SplitterWidth
    {
        get => _splitterWidth;
        set
        {
            var normalized = Math.Max(1f, value);
            if (Math.Abs(_splitterWidth - normalized) < 0.001f)
                return;

            _splitterWidth = normalized;
            PerformLayout();
            Invalidate();
        }
    }

    [DefaultValue(48f)]
    public float PanelMinSize { get; set; } = 48f;

    public override void  OnLayout(LayoutEventArgs e)
    {
        if (Panel1 == null || Panel2 == null)
            return;

        var display = DisplayRectangle;
        var splitter = GetSplitterRect(display);

        if (Orientation == Orientation.Vertical)
        {
            Panel1.Bounds = new SKRect(display.Left, display.Top, splitter.Left, display.Bottom);
            Panel2.Bounds = new SKRect(splitter.Right, display.Top, display.Right, display.Bottom);
        }
        else
        {
            Panel1.Bounds = new SKRect(display.Left, display.Top, display.Right, splitter.Top);
            Panel2.Bounds = new SKRect(display.Left, splitter.Bottom, display.Right, display.Bottom);
        }

        Panel1.PerformLayout();
        Panel2.PerformLayout();
    }

    public override void  OnPaint(SKCanvas canvas)
    {
        base.OnPaint(canvas);

        var splitter = GetSplitterRect(DisplayRectangle);
        var scale = ScaleFactor;
        var hover = _splitterHovered || _dragging;

        _gripPaint.Color = hover ? ColorScheme.Primary : ColorScheme.Outline.WithAlpha(165);
        if (Orientation == Orientation.Vertical)
        {
            var cx = splitter.MidX;
            for (var i = -1; i <= 1; i++)
                canvas.DrawCircle(cx, splitter.MidY + i * 5f * scale, 1.65f * scale, _gripPaint);
        }
        else
        {
            var cy = splitter.MidY;
            for (var i = -1; i <= 1; i++)
                canvas.DrawCircle(splitter.MidX + i * 5f * scale, cy, 1.65f * scale, _gripPaint);
        }
    }

    public override void  OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        if (e.Button != MouseButtons.Left || !GetSplitterRect(DisplayRectangle).Contains(e.Location))
            return;

        _dragging = true;
        UpdateSplitterHoverState(true);
        GetParentWindow()?.SetMouseCapture(this);
    }

    public override void  OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        UpdateSplitterHoverState(_dragging || GetSplitterRect(DisplayRectangle).Contains(e.Location));

        if (!_dragging)
            return;

        SplitterDistance = Orientation == Orientation.Vertical
            ? e.X - DisplayRectangle.Left
            : e.Y - DisplayRectangle.Top;
    }

    public override void  OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);
        if (e.Button != MouseButtons.Left)
            return;

        _dragging = false;
        UpdateSplitterHoverState(GetSplitterRect(DisplayRectangle).Contains(e.Location));
        GetParentWindow()?.ReleaseMouseCapture(this);
    }

    public override void  OnMouseLeave(EventArgs e)
    {
        if (!_dragging)
            UpdateSplitterHoverState(false);

        base.OnMouseLeave(e);
    }

    public override void  OnLostFocus(EventArgs e)
    {
        _dragging = false;
        UpdateSplitterHoverState(false);
        GetParentWindow()?.ReleaseMouseCapture(this);
        base.OnLostFocus(e);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _splitterPaint.Dispose();
            _gripPaint.Dispose();
        }

        base.Dispose(disposing);
    }

    private SKRect GetSplitterRect(SKRect display)
    {
        if (Orientation == Orientation.Vertical)
        {
            var min = Math.Min(PanelMinSize, Math.Max(0f, (display.Width - SplitterWidth) * 0.5f));
            var max = Math.Max(min, display.Width - SplitterWidth - min);
            var distance = Math.Clamp(SplitterDistance, min, max);
            return new SKRect(display.Left + distance, display.Top, display.Left + distance + SplitterWidth, display.Bottom);
        }

        var minY = Math.Min(PanelMinSize, Math.Max(0f, (display.Height - SplitterWidth) * 0.5f));
        var maxY = Math.Max(minY, display.Height - SplitterWidth - minY);
        var y = Math.Clamp(SplitterDistance, minY, maxY);
        return new SKRect(display.Left, display.Top + y, display.Right, display.Top + y + SplitterWidth);
    }

    private void UpdateSplitterHoverState(bool hovered)
    {
        if (_splitterHovered == hovered)
            return;

        _splitterHovered = hovered;
        Cursor = hovered
            ? Orientation == Orientation.Vertical ? Cursors.SizeWE : Cursors.SizeNS
            : Cursors.Default;
        GetParentWindow()?.UpdateCursor(this);
        Invalidate();
    }
}
