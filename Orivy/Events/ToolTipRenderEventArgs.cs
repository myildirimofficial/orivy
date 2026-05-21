
using System;
using System.Collections.Generic;
using Orivy.Controls;
using SkiaSharp;

public sealed class ToolTipRenderEventArgs : EventArgs
{
    internal ToolTipRenderEventArgs(
        SKCanvas canvas,
        ElementBase owner,
        SKRect bounds,
        SKRect textBounds,
        IReadOnlyList<string> lines,
        SKFont font,
        Position placement,
        float progress)
    {
        Canvas = canvas;
        Owner = owner;
        Bounds = bounds;
        TextBounds = textBounds;
        Lines = lines;
        Font = font;
        Placement = placement;
        Progress = progress;
    }

    public SKCanvas Canvas { get; }

    public ElementBase Owner { get; }

    public SKRect Bounds { get; }

    public SKRect TextBounds { get; }

    public IReadOnlyList<string> Lines { get; }

    public SKFont Font { get; }

    public Position Placement { get; }

    public float Progress { get; }

    public bool Handled { get; set; }
}
