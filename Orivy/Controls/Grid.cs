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
        PerformLayout();
    }

    public GridPlacement GetPlacement(ElementBase child)
    {
        return _placements.TryGetValue(child, out var placement)
            ? placement
            : GridPlacement.Default;
    }

    protected override void OnLayout(LayoutEventArgs e)
    {
        var display = DisplayRectangle;
        var cellWidth = (display.Width - ColumnGap * (ColumnCount - 1)) / ColumnCount;
        var cellHeight = (display.Height - RowGap * (RowCount - 1)) / RowCount;

        cellWidth = Math.Max(1f, cellWidth);
        cellHeight = Math.Max(1f, cellHeight);

        for (var i = 0; i < Controls.Count; i++)
        {
            if (Controls[i] is not ElementBase child || !child.Visible)
                continue;

            var placement = GetPlacement(child);
            var row = Math.Min(placement.Row, RowCount - 1);
            var column = Math.Min(placement.Column, ColumnCount - 1);
            var rowSpan = Math.Min(placement.RowSpan, RowCount - row);
            var columnSpan = Math.Min(placement.ColumnSpan, ColumnCount - column);

            var left = display.Left + column * (cellWidth + ColumnGap);
            var top = display.Top + row * (cellHeight + RowGap);
            var width = cellWidth * columnSpan + ColumnGap * (columnSpan - 1);
            var height = cellHeight * rowSpan + RowGap * (rowSpan - 1);

            child.Bounds = new SKRect(left, top, left + width, top + height);
            if (child.Controls.Count > 0)
                child.PerformLayout();
        }
    }
}

public readonly record struct GridPlacement(int Row, int Column, int RowSpan, int ColumnSpan)
{
    public static GridPlacement Default { get; } = new(0, 0, 1, 1);
}
