using Orivy;
using Orivy.Controls;
using Orivy.Helpers;
using Orivy.Studio.Toolbox;
using SkiaSharp;
using System;

namespace Orivy.Studio.Canvas;

/// <summary>
/// A full-window transparent overlay used for toolbox drag-and-drop. While a drag is in progress it
/// captures the mouse, draws a small "ghost" chip that follows the cursor, and on release reports
/// the drop in screen coordinates so the shell can place the control on the active canvas.
/// </summary>
public sealed class DragLayer : Element
{
    private ControlEntry? _entry;
    private SKPoint _ghost;

    private readonly SKPaint _chipFill = new() { IsAntialias = true };
    private readonly SKPaint _chipText = new() { IsAntialias = true };

    public DragLayer()
    {
        Dock = DockStyle.Fill;
        BackColor = SKColors.Transparent;
        Border = new Thickness(0);
        Radius = new Radius(0);
        ZOrder = 1_000_000;
        Visible = false;
        CanSelect = false;
        TabStop = false;
    }

    /// <summary>Raised on drop with the dragged entry and the drop point in screen coordinates.</summary>
    public event Action<ControlEntry, SKPoint>? Dropped;

    public bool IsDragging => _entry != null;

    /// <summary>Starts a drag for <paramref name="entry"/>, given the current cursor in screen space.</summary>
    public void Begin(ControlEntry entry, SKPoint screenPoint)
    {
        _entry = entry;
        _ghost = PointToClient(screenPoint);
        Visible = true;
        GetParentWindow()?.SetMouseCapture(this);
        Invalidate();
    }

    public override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        if (!IsDragging)
            return;
        _ghost = e.Location;
        Invalidate();
    }

    public override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);
        if (!IsDragging)
            return;

        var entry = _entry!;
        var screen = PointToScreen(e.Location);
        Cancel();
        Dropped?.Invoke(entry, screen);
    }

    private void Cancel()
    {
        _entry = null;
        Visible = false;
        GetParentWindow()?.ReleaseMouseCapture(this);
        Invalidate();
    }

    public override void OnPaint(SKCanvas canvas)
    {
        base.OnPaint(canvas);
        if (_entry == null)
            return;

        var label = _entry.DisplayName;
        using var font = Application.DefaultFont;
        var textWidth = font.MeasureText(label);
        var padding = 12f;
        var chip = SKRect.Create(_ghost.X + 14f, _ghost.Y + 10f, textWidth + padding * 2f, 30f);

        _chipFill.Color = ColorScheme.Primary;
        canvas.DrawRoundRect(chip, 8f, 8f, _chipFill);

        _chipText.Color = SKColors.White;
        var baseline = chip.MidY + font.Size * 0.35f;
        TextRenderer.DrawText(canvas, label, chip.Left + padding, baseline, font, _chipText);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _chipFill.Dispose();
            _chipText.Dispose();
        }

        base.Dispose(disposing);
    }
}
