using Orivy.Controls;
using SkiaSharp;
using System;

namespace Orivy;

/// <summary>
/// Data for <see cref="GridList.DrawItem"/> (owner-drawn rows). Mirrors WinForms ListView
/// owner-draw semantics: the event fires before default painting; set <see cref="DrawDefault"/>
/// to true to let the grid paint the row itself anyway, or use the <c>Draw*</c> helper methods
/// to compose default pieces (background, text, focus rectangle) with your own drawing.
/// </summary>
public class GridListDrawItemEventArgs : EventArgs
{
    internal GridListDrawItemEventArgs(GridList owner, SKCanvas canvas, GridListItem item, int itemIndex,
        SKRect bounds, SKFont font, bool selected, bool hovered)
    {
        Owner = owner;
        Canvas = canvas;
        Item = item;
        ItemIndex = itemIndex;
        Bounds = bounds;
        Font = font;
        Selected = selected;
        Hovered = hovered;
    }

    internal GridList Owner { get; }

    /// <summary>The canvas to draw on (grid-local coordinates).</summary>
    public SKCanvas Canvas { get; }

    /// <summary>The row item being drawn.</summary>
    public GridListItem Item { get; }

    /// <summary>The zero-based row index.</summary>
    public int ItemIndex { get; }

    /// <summary>The bounds being drawn (full row, or the cell for sub-items).</summary>
    public SKRect Bounds { get; }

    /// <summary>The font the grid would use for cell text.</summary>
    public SKFont Font { get; }

    public bool Selected { get; }
    public bool Hovered { get; }

    /// <summary>Set to true to have the grid perform its default painting after the handler runs.</summary>
    public bool DrawDefault { get; set; }

    /// <summary>Draws the default state background (selection / hover / alternating row).</summary>
    public void DrawBackground()
        => Owner.PaintDefaultRowBackground(Canvas, Bounds, ItemIndex, Selected, Hovered);

    /// <summary>Fills <see cref="Bounds"/> with a specific color.</summary>
    public void DrawBackground(SKColor color)
        => Owner.PaintSolidBackground(Canvas, Bounds, color);

    /// <summary>Draws the default text of every cell in this row.</summary>
    public virtual void DrawText()
        => Owner.PaintDefaultRowText(Canvas, Bounds, ItemIndex, Font);

    /// <summary>Draws a dashed focus rectangle inside <see cref="Bounds"/>.</summary>
    public void DrawFocusRectangle()
        => Owner.PaintFocusRectangle(Canvas, Bounds);
}

/// <summary>Data for <see cref="GridList.DrawSubItem"/> (owner-drawn cells).</summary>
public sealed class GridListDrawSubItemEventArgs : GridListDrawItemEventArgs
{
    internal GridListDrawSubItemEventArgs(GridList owner, SKCanvas canvas, GridListItem item, int itemIndex,
        GridListCell? cell, GridListColumn column, int columnIndex,
        SKRect cellBounds, SKFont font, bool selected, bool hovered)
        : base(owner, canvas, item, itemIndex, cellBounds, font, selected, hovered)
    {
        Cell = cell;
        Column = column;
        ColumnIndex = columnIndex;
    }

    /// <summary>The cell (sub-item) being drawn; may be null when the row has fewer cells than columns.</summary>
    public GridListCell? Cell { get; }

    public GridListColumn Column { get; }

    public int ColumnIndex { get; }

    /// <summary>Draws this cell's default text (padding, color and column alignment applied).</summary>
    public override void DrawText()
        => Owner.PaintDefaultCellText(Canvas, Bounds, Item, Cell, Column, Font);

    /// <summary>Draws this cell's default text with a specific alignment.</summary>
    public void DrawText(ContentAlignment alignment)
        => Owner.PaintDefaultCellText(Canvas, Bounds, Item, Cell, Column, Font, alignment);
}

/// <summary>Data for <see cref="GridList.DrawColumnHeader"/> (owner-drawn column headers).</summary>
public sealed class GridListDrawColumnHeaderEventArgs : EventArgs
{
    internal GridListDrawColumnHeaderEventArgs(GridList owner, SKCanvas canvas, GridListColumn column,
        int columnIndex, SKRect bounds, SKFont font, bool hovered)
    {
        Owner = owner;
        Canvas = canvas;
        Column = column;
        ColumnIndex = columnIndex;
        Bounds = bounds;
        Font = font;
        Hovered = hovered;
    }

    internal GridList Owner { get; }

    public SKCanvas Canvas { get; }
    public GridListColumn Column { get; }
    public int ColumnIndex { get; }
    public SKRect Bounds { get; }
    public SKFont Font { get; }
    public bool Hovered { get; }

    /// <summary>Set to true to have the grid perform its default header painting after the handler runs.</summary>
    public bool DrawDefault { get; set; }

    /// <summary>Draws the default header background (hover-aware).</summary>
    public void DrawBackground()
        => Owner.PaintDefaultHeaderBackground(Canvas, Bounds, Hovered);

    /// <summary>Fills <see cref="Bounds"/> with a specific color.</summary>
    public void DrawBackground(SKColor color)
        => Owner.PaintSolidBackground(Canvas, Bounds, color);

    /// <summary>Draws the default header caption.</summary>
    public void DrawText()
        => Owner.PaintDefaultHeaderText(Canvas, Bounds, Column, Font);
}
