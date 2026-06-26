using Orivy.Layout;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.ComponentModel;

namespace Orivy.Controls;

public class Grid : Container
{
    private readonly Dictionary<ElementBase, GridPlacement> _placements = new();
    private int _rowCount = 2;
    private int _columnCount = 2;
    private float _rowGap = 8f;
    private float _columnGap = 8f;

    public Grid()
    {
        BackColor = SKColors.Transparent;
    }

    [DefaultValue(2)]
    public int RowCount
    {
        get => _rowCount;
        set
        {
            var normalized = Math.Max(1, value);
            if (_rowCount == normalized)
                return;

            _rowCount = normalized;
            InvalidateMeasure();
            PerformLayout();
        }
    }

    [DefaultValue(2)]
    public int ColumnCount
    {
        get => _columnCount;
        set
        {
            var normalized = Math.Max(1, value);
            if (_columnCount == normalized)
                return;

            _columnCount = normalized;
            InvalidateMeasure();
            PerformLayout();
        }
    }

    [DefaultValue(8f)]
    public float RowGap
    {
        get => _rowGap;
        set
        {
            var normalized = Math.Max(0f, value);
            if (Math.Abs(_rowGap - normalized) < 0.001f)
                return;

            _rowGap = normalized;
            InvalidateMeasure();
            PerformLayout();
        }
    }

    [DefaultValue(8f)]
    public float ColumnGap
    {
        get => _columnGap;
        set
        {
            var normalized = Math.Max(0f, value);
            if (Math.Abs(_columnGap - normalized) < 0.001f)
                return;

            _columnGap = normalized;
            InvalidateMeasure();
            PerformLayout();
        }
    }

    public void Add(ElementBase child, int row, int column, int rowSpan = 1, int columnSpan = 1)
    {
        ArgumentNullException.ThrowIfNull(child);

        SetPlacement(child, row, column, rowSpan, columnSpan);
        Controls.Add(child);
    }

    public void SetPlacement(ElementBase child, int row, int column, int rowSpan = 1, int columnSpan = 1)
    {
        ArgumentNullException.ThrowIfNull(child);

        _placements[child] = new GridPlacement(
            Math.Max(0, row),
            Math.Max(0, column),
            Math.Max(1, rowSpan),
            Math.Max(1, columnSpan));
        InvalidateMeasure();
        PerformLayout();
    }

    public GridPlacement GetPlacement(ElementBase child)
    {
        return _placements.TryGetValue(child, out var placement)
            ? placement
            : GridPlacement.Default;
    }

    public override void  OnLayout(LayoutEventArgs e)
    {
        var display = DisplayRectangle;
        var cellWidth = (display.Width - ColumnGap * (ColumnCount - 1)) / ColumnCount;
        cellWidth = Math.Max(1f, cellWidth);
        var rowHeights = AutoSize
            ? MeasureRowHeights(cellWidth)
            : CreateUniformRowHeights(display.Height);

        for (var i = 0; i < Controls.Count; i++)
        {
            if (Controls[i] is not ElementBase child || !child.Visible || child is ScrollBar)
                continue;

            var placement = GetPlacement(child);
            var row = Math.Min(placement.Row, RowCount - 1);
            var column = Math.Min(placement.Column, ColumnCount - 1);
            var rowSpan = Math.Min(placement.RowSpan, RowCount - row);
            var columnSpan = Math.Min(placement.ColumnSpan, ColumnCount - column);

            var left = display.Left + column * (cellWidth + ColumnGap);
            var top = display.Top + GetRowOffset(rowHeights, row);
            var width = cellWidth * columnSpan + ColumnGap * (columnSpan - 1);
            var height = GetSpannedHeight(rowHeights, row, rowSpan);

            child.Bounds = new SKRect(left, top, left + width, top + height);
            if (child.Controls.Count > 0)
                child.PerformLayout();
        }
    }

    protected override SKSize GetPreferredSizeCore(SKSize proposedSize)
    {
        var columnWidths = new float[ColumnCount];
        var displayWidth = Math.Max(0f, proposedSize.Width - Padding.Left - Padding.Right - Border.Left - Border.Right);
        var availableCellWidth = ColumnCount > 0
            ? Math.Max(1f, (displayWidth - ColumnGap * (ColumnCount - 1)) / ColumnCount)
            : 1f;
        var rowHeights = MeasureRowHeights(availableCellWidth);

        for (var i = 0; i < Controls.Count; i++)
        {
            if (Controls[i] is not ElementBase child || !child.Visible || child is ScrollBar)
                continue;

            var placement = GetPlacement(child);
            var row = Math.Min(placement.Row, RowCount - 1);
            var column = Math.Min(placement.Column, ColumnCount - 1);
            var rowSpan = Math.Min(placement.RowSpan, RowCount - row);
            var columnSpan = Math.Min(placement.ColumnSpan, ColumnCount - column);
            if (rowSpan <= 0 || columnSpan <= 0)
                continue;

            var childConstraint = new SKSize(
                availableCellWidth * columnSpan + ColumnGap * (columnSpan - 1),
                proposedSize.Height);
            var preferred = child.GetPreferredSize(childConstraint);
            var desiredWidth = Math.Max(child.Width, preferred.Width);
            var widthPerColumn = Math.Max(1f, (desiredWidth - ColumnGap * (columnSpan - 1)) / columnSpan);

            for (var c = column; c < column + columnSpan && c < ColumnCount; c++)
                columnWidths[c] = Math.Max(columnWidths[c], widthPerColumn);
        }

        var width = Padding.Left + Padding.Right + Border.Left + Border.Right + ColumnGap * Math.Max(0, ColumnCount - 1);
        for (var i = 0; i < columnWidths.Length; i++)
            width += columnWidths[i];

        var height = Padding.Top + Padding.Bottom + Border.Top + Border.Bottom + RowGap * Math.Max(0, RowCount - 1);
        for (var i = 0; i < rowHeights.Length; i++)
            height += rowHeights[i];

        return new SKSize(MathF.Ceiling(width), MathF.Ceiling(height));
    }

    private float[] MeasureRowHeights(float cellWidth)
    {
        var rowHeights = new float[RowCount];

        for (var i = 0; i < Controls.Count; i++)
        {
            if (Controls[i] is not ElementBase child || !child.Visible || child is ScrollBar)
                continue;

            var placement = GetPlacement(child);
            var row = Math.Min(placement.Row, RowCount - 1);
            var column = Math.Min(placement.Column, ColumnCount - 1);
            var rowSpan = Math.Min(placement.RowSpan, RowCount - row);
            var columnSpan = Math.Min(placement.ColumnSpan, ColumnCount - column);
            if (rowSpan <= 0 || columnSpan <= 0)
                continue;

            var width = cellWidth * columnSpan + ColumnGap * (columnSpan - 1);
            var preferred = child.GetPreferredSize(new SKSize(width, child.Height));
            var desiredHeight = Math.Max(child.Height, preferred.Height);
            var heightPerRow = Math.Max(1f, (desiredHeight - RowGap * (rowSpan - 1)) / rowSpan);

            for (var r = row; r < row + rowSpan && r < RowCount; r++)
                rowHeights[r] = Math.Max(rowHeights[r], heightPerRow);
        }

        for (var i = 0; i < rowHeights.Length; i++)
            rowHeights[i] = Math.Max(1f, rowHeights[i]);

        return rowHeights;
    }

    private float[] CreateUniformRowHeights(float displayHeight)
    {
        var rowHeights = new float[RowCount];
        var rowHeight = Math.Max(1f, (displayHeight - RowGap * (RowCount - 1)) / RowCount);
        Array.Fill(rowHeights, rowHeight);
        return rowHeights;
    }

    private float GetRowOffset(float[] rowHeights, int row)
    {
        var offset = 0f;
        for (var i = 0; i < row; i++)
            offset += rowHeights[i] + RowGap;

        return offset;
    }

    private float GetSpannedHeight(float[] rowHeights, int row, int rowSpan)
    {
        var height = RowGap * Math.Max(0, rowSpan - 1);
        for (var i = row; i < row + rowSpan && i < rowHeights.Length; i++)
            height += rowHeights[i];

        return Math.Max(1f, height);
    }
}

public readonly record struct GridPlacement(int Row, int Column, int RowSpan, int ColumnSpan)
{
    public static GridPlacement Default { get; } = new(0, 0, 1, 1);
}
