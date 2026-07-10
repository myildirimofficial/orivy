using SkiaSharp;
using System;

namespace Orivy;

/// <summary>
/// Provides data for owner-drawn item rendering (e.g. <see cref="Orivy.Controls.ListBox"/> with
/// <see cref="DrawMode.OwnerDrawFixed"/>). Skia-flavored equivalent of
/// System.Windows.Forms.DrawItemEventArgs — <see cref="Canvas"/> replaces Graphics.
/// </summary>
public sealed class DrawItemEventArgs : EventArgs
{
    public DrawItemEventArgs(SKCanvas canvas, SKFont font, SKRect bounds, int index,
        DrawItemState state, SKColor foreColor, SKColor backColor)
    {
        Canvas = canvas;
        Font = font;
        Bounds = bounds;
        Index = index;
        State = state;
        ForeColor = foreColor;
        BackColor = backColor;
    }

    /// <summary>The canvas to draw the item on. Coordinates are in the control's local space.</summary>
    public SKCanvas Canvas { get; }

    /// <summary>The font the control would use for the item.</summary>
    public SKFont Font { get; }

    /// <summary>The bounds of the item being drawn, in control-local coordinates.</summary>
    public SKRect Bounds { get; }

    /// <summary>The zero-based index of the item being drawn.</summary>
    public int Index { get; }

    /// <summary>The visual state of the item (selected, focused, disabled, ...).</summary>
    public DrawItemState State { get; }

    /// <summary>The foreground (text) color the control suggests for this state.</summary>
    public SKColor ForeColor { get; }

    /// <summary>The background color the control suggests for this state.</summary>
    public SKColor BackColor { get; }

    public bool Selected => (State & DrawItemState.Selected) != 0;
    public bool Focused => (State & DrawItemState.Focus) != 0;
    public bool Disabled => (State & DrawItemState.Disabled) != 0;

    /// <summary>Fills the item bounds with <see cref="BackColor"/> (rounded corners optional).</summary>
    public void DrawBackground(float cornerRadius = 0f)
    {
        if (BackColor.Alpha == 0)
            return;

        using var paint = new SKPaint { Color = BackColor, IsAntialias = true };
        if (cornerRadius > 0f)
            Canvas.DrawRoundRect(Bounds, cornerRadius, cornerRadius, paint);
        else
            Canvas.DrawRect(Bounds, paint);
    }
}
