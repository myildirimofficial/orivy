using Orivy.Collections;
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
        RowDefinitions = new RowDefinitionCollection(this);
        ColumnDefinitions = new ColumnDefinitionCollection(this);
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

    // Explicit per-row/per-column sizing (Auto/Pixel/Star), WPF Grid style. Empty by default, in
    // which case RowCount/ColumnCount/RowGap/ColumnGap alone describe a uniformly divided grid
    // exactly as before these existed — adding a definition switches that axis over to this richer
    // model without disturbing a grid that never uses it.
    public RowDefinitionCollection RowDefinitions { get; }
    public ColumnDefinitionCollection ColumnDefinitions { get; }

    internal void OnDefinitionsChanged()
    {
        InvalidateMeasure();
        PerformLayout();
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

    /// <summary>Which (row, column) cell contains <paramref name="localPoint"/>, given in this Grid's
    /// own local coordinate space (i.e. relative to its own top-left, same space child <see
    /// cref="ElementBase.Bounds"/> is laid out in) — lets a caller (a designer's drag-and-drop, a
    /// runtime click-to-place feature, ...) turn a point into a placement without duplicating the
    /// track-sizing math <see cref="OnLayout"/> already does.</summary>
    public GridPlacement HitTestCell(SKPoint localPoint)
    {
        var display = DisplayRectangle;
        var columnWidths = ComputeColumnWidths(display.Width);
        var rowHeights = ComputeRowHeights(display.Height, columnWidths);

        var column = FindTrackIndex(columnWidths, ColumnGap, localPoint.X - display.Left);
        var row = FindTrackIndex(rowHeights, RowGap, localPoint.Y - display.Top);
        return new GridPlacement(row, column, 1, 1);
    }

    /// <summary>The overlay-drawable rect (in this Grid's own local space) of one cell, from the same
    /// track sizes <see cref="OnLayout"/> just measured — used to highlight a drop target.</summary>
    public SKRect GetCellRect(int row, int column)
    {
        var display = DisplayRectangle;
        var columnWidths = ComputeColumnWidths(display.Width);
        var rowHeights = ComputeRowHeights(display.Height, columnWidths);

        row = Math.Clamp(row, 0, rowHeights.Length - 1);
        column = Math.Clamp(column, 0, columnWidths.Length - 1);

        var left = display.Left + GetTrackOffset(columnWidths, ColumnGap, column);
        var top = display.Top + GetTrackOffset(rowHeights, RowGap, row);
        return SKRect.Create(left, top, columnWidths[column], rowHeights[row]);
    }

    private static int FindTrackIndex(float[] sizes, float gap, float offset)
    {
        var cursor = 0f;
        for (var i = 0; i < sizes.Length; i++)
        {
            var end = cursor + sizes[i];
            if (offset < end || i == sizes.Length - 1)
                return i;
            cursor = end + gap;
        }
        return Math.Max(0, sizes.Length - 1);
    }

    public override void OnLayout(LayoutEventArgs e)
    {
        // ElementBase's own constructor (InitializeScrollBars) adds a scroll bar to Controls and lays
        // out immediately, before Grid's constructor body has run — RowDefinitions/ColumnDefinitions
        // are still null at that point. Nothing meaningful to lay out yet either way; a real layout
        // follows once construction finishes and actual children are added.
        if (RowDefinitions == null || ColumnDefinitions == null)
            return;

        var display = DisplayRectangle;
        var columnWidths = ComputeColumnWidths(display.Width);
        var rowHeights = ComputeRowHeights(display.Height, columnWidths);
        var rowCount = rowHeights.Length;
        var columnCount = columnWidths.Length;

        for (var i = 0; i < Controls.Count; i++)
        {
            if (Controls[i] is not ElementBase child || !child.Visible || child is ScrollBar)
                continue;

            var placement = GetPlacement(child);
            var row = Math.Min(placement.Row, rowCount - 1);
            var column = Math.Min(placement.Column, columnCount - 1);
            var rowSpan = Math.Min(placement.RowSpan, rowCount - row);
            var columnSpan = Math.Min(placement.ColumnSpan, columnCount - column);

            var left = display.Left + GetTrackOffset(columnWidths, ColumnGap, column);
            var top = display.Top + GetTrackOffset(rowHeights, RowGap, row);
            var width = GetSpannedSize(columnWidths, ColumnGap, column, columnSpan);
            var height = GetSpannedSize(rowHeights, RowGap, row, rowSpan);

            child.Bounds = new SKRect(left, top, left + width, top + height);
            if (child.Controls.Count > 0)
                child.PerformLayout();
        }
    }

    protected override SKSize GetPreferredSizeCore(SKSize proposedSize)
    {
        if (RowDefinitions == null || ColumnDefinitions == null)
            return base.GetPreferredSizeCore(proposedSize);

        var columnCount = ColumnDefinitions.Count > 0 ? ColumnDefinitions.Count : ColumnCount;
        var displayWidth = Math.Max(0f, proposedSize.Width - Padding.Left - Padding.Right - Border.Left - Border.Right);

        var columnWidths = new float[columnCount];
        if (ColumnDefinitions.Count > 0)
        {
            var autoColumnWidths = MeasureAutoColumnWidths(columnCount);
            for (var i = 0; i < columnCount; i++)
                columnWidths[i] = ColumnDefinitions[i].Width.IsPixel
                    ? Math.Max(0f, ColumnDefinitions[i].Width.Value)
                    : Math.Max(1f, autoColumnWidths[i]);
        }
        else
        {
            var availableCellWidth = columnCount > 0
                ? Math.Max(1f, (displayWidth - ColumnGap * (columnCount - 1)) / columnCount)
                : 1f;

            for (var i = 0; i < Controls.Count; i++)
            {
                if (Controls[i] is not ElementBase child || !child.Visible || child is ScrollBar)
                    continue;

                var placement = GetPlacement(child);
                var column = Math.Min(placement.Column, columnCount - 1);
                var columnSpan = Math.Min(placement.ColumnSpan, columnCount - column);
                if (columnSpan <= 0)
                    continue;

                var childConstraint = new SKSize(availableCellWidth * columnSpan + ColumnGap * (columnSpan - 1), proposedSize.Height);
                var preferred = child.GetPreferredSize(childConstraint);
                var desiredWidth = Math.Max(child.Width, preferred.Width);
                var widthPerColumn = Math.Max(1f, (desiredWidth - ColumnGap * (columnSpan - 1)) / columnSpan);

                for (var c = column; c < column + columnSpan && c < columnCount; c++)
                    columnWidths[c] = Math.Max(columnWidths[c], widthPerColumn);
            }
        }

        var rowCount = RowDefinitions.Count > 0 ? RowDefinitions.Count : RowCount;
        float[] rowHeights;
        if (RowDefinitions.Count > 0)
        {
            var autoRowHeights = MeasureRowHeights(columnWidths, rowCount);
            rowHeights = new float[rowCount];
            for (var i = 0; i < rowCount; i++)
                rowHeights[i] = RowDefinitions[i].Height.IsPixel
                    ? Math.Max(0f, RowDefinitions[i].Height.Value)
                    : Math.Max(1f, autoRowHeights[i]);
        }
        else
        {
            rowHeights = MeasureRowHeights(columnWidths, rowCount);
        }

        var width = Padding.Left + Padding.Right + Border.Left + Border.Right + ColumnGap * Math.Max(0, columnCount - 1);
        for (var i = 0; i < columnWidths.Length; i++)
            width += columnWidths[i];

        var height = Padding.Top + Padding.Bottom + Border.Top + Border.Bottom + RowGap * Math.Max(0, rowCount - 1);
        for (var i = 0; i < rowHeights.Length; i++)
            height += rowHeights[i];

        return new SKSize(MathF.Ceiling(width), MathF.Ceiling(height));
    }

    private float[] ComputeColumnWidths(float displayWidth)
    {
        if (ColumnDefinitions.Count == 0)
        {
            var count = ColumnCount;
            var cellWidth = Math.Max(1f, (displayWidth - ColumnGap * (count - 1)) / count);
            var widths = new float[count];
            Array.Fill(widths, cellWidth);
            return widths;
        }

        var n = ColumnDefinitions.Count;
        var sizes = new float[n];
        var kinds = new GridUnitType[n];
        for (var i = 0; i < n; i++)
            kinds[i] = ColumnDefinitions[i].Width.UnitType;

        for (var i = 0; i < n; i++)
            if (kinds[i] == GridUnitType.Pixel)
                sizes[i] = Math.Max(0f, ColumnDefinitions[i].Width.Value);

        if (Array.IndexOf(kinds, GridUnitType.Auto) >= 0)
        {
            var autoWidths = MeasureAutoColumnWidths(n);
            for (var i = 0; i < n; i++)
                if (kinds[i] == GridUnitType.Auto)
                    sizes[i] = Math.Max(1f, autoWidths[i]);
        }

        DistributeStar(sizes, kinds, i => ColumnDefinitions[i].Width.Value, displayWidth, ColumnGap);

        for (var i = 0; i < n; i++)
            ColumnDefinitions[i].ActualWidth = sizes[i];

        return sizes;
    }

    private float[] ComputeRowHeights(float displayHeight, float[] columnWidths)
    {
        if (RowDefinitions.Count == 0)
        {
            return AutoSize
                ? MeasureRowHeights(columnWidths, RowCount)
                : CreateUniformRowHeights(displayHeight, RowCount);
        }

        var n = RowDefinitions.Count;
        var sizes = new float[n];
        var kinds = new GridUnitType[n];
        for (var i = 0; i < n; i++)
            kinds[i] = RowDefinitions[i].Height.UnitType;

        for (var i = 0; i < n; i++)
            if (kinds[i] == GridUnitType.Pixel)
                sizes[i] = Math.Max(0f, RowDefinitions[i].Height.Value);

        if (Array.IndexOf(kinds, GridUnitType.Auto) >= 0)
        {
            var autoHeights = MeasureRowHeights(columnWidths, n);
            for (var i = 0; i < n; i++)
                if (kinds[i] == GridUnitType.Auto)
                    sizes[i] = Math.Max(1f, autoHeights[i]);
        }

        DistributeStar(sizes, kinds, i => RowDefinitions[i].Height.Value, displayHeight, RowGap);

        for (var i = 0; i < n; i++)
            RowDefinitions[i].ActualHeight = sizes[i];

        return sizes;
    }

    /// <summary>Splits whatever's left after Pixel/Auto tracks are sized among the Star tracks,
    /// proportional to their weight — same algorithm WPF's Grid uses for its own Star rows/columns.</summary>
    private static void DistributeStar(float[] sizes, GridUnitType[] kinds, Func<int, float> starWeight, float available, float gap)
    {
        var allocated = gap * Math.Max(0, sizes.Length - 1);
        var totalStar = 0f;
        for (var i = 0; i < sizes.Length; i++)
        {
            if (kinds[i] == GridUnitType.Star)
                totalStar += Math.Max(0f, starWeight(i));
            else
                allocated += sizes[i];
        }

        if (totalStar <= 0f)
            return;

        var remaining = Math.Max(0f, available - allocated);
        for (var i = 0; i < sizes.Length; i++)
            if (kinds[i] == GridUnitType.Star)
                sizes[i] = Math.Max(1f, remaining * (Math.Max(0f, starWeight(i)) / totalStar));
    }

    private float[] MeasureAutoColumnWidths(int columnCount)
    {
        const float unconstrained = 1_000_000f;
        var widths = new float[columnCount];

        for (var i = 0; i < Controls.Count; i++)
        {
            if (Controls[i] is not ElementBase child || !child.Visible || child is ScrollBar)
                continue;

            var placement = GetPlacement(child);
            var column = Math.Min(placement.Column, columnCount - 1);
            var columnSpan = Math.Min(placement.ColumnSpan, columnCount - column);
            if (columnSpan <= 0)
                continue;

            var preferred = child.GetPreferredSize(new SKSize(unconstrained, child.Height));
            var desiredWidth = Math.Max(child.Width, preferred.Width);
            var widthPerColumn = Math.Max(1f, (desiredWidth - ColumnGap * (columnSpan - 1)) / columnSpan);

            for (var c = column; c < column + columnSpan && c < columnCount; c++)
                widths[c] = Math.Max(widths[c], widthPerColumn);
        }

        return widths;
    }

    private float[] MeasureRowHeights(float[] columnWidths, int rowCount)
    {
        var rowHeights = new float[rowCount];
        var columnCount = columnWidths.Length;

        for (var i = 0; i < Controls.Count; i++)
        {
            if (Controls[i] is not ElementBase child || !child.Visible || child is ScrollBar)
                continue;

            var placement = GetPlacement(child);
            var row = Math.Min(placement.Row, rowCount - 1);
            var column = Math.Min(placement.Column, columnCount - 1);
            var rowSpan = Math.Min(placement.RowSpan, rowCount - row);
            var columnSpan = Math.Min(placement.ColumnSpan, columnCount - column);
            if (rowSpan <= 0 || columnSpan <= 0)
                continue;

            var width = GetSpannedSize(columnWidths, ColumnGap, column, columnSpan);
            var preferred = child.GetPreferredSize(new SKSize(width, child.Height));
            var desiredHeight = Math.Max(child.Height, preferred.Height);
            var heightPerRow = Math.Max(1f, (desiredHeight - RowGap * (rowSpan - 1)) / rowSpan);

            for (var r = row; r < row + rowSpan && r < rowCount; r++)
                rowHeights[r] = Math.Max(rowHeights[r], heightPerRow);
        }

        for (var i = 0; i < rowHeights.Length; i++)
            rowHeights[i] = Math.Max(1f, rowHeights[i]);

        return rowHeights;
    }

    private float[] CreateUniformRowHeights(float displayHeight, int rowCount)
    {
        var rowHeights = new float[rowCount];
        var rowHeight = Math.Max(1f, (displayHeight - RowGap * (rowCount - 1)) / rowCount);
        Array.Fill(rowHeights, rowHeight);
        return rowHeights;
    }

    private static float GetTrackOffset(float[] sizes, float gap, int index)
    {
        var offset = 0f;
        for (var i = 0; i < index; i++)
            offset += sizes[i] + gap;

        return offset;
    }

    private static float GetSpannedSize(float[] sizes, float gap, int index, int span)
    {
        var size = gap * Math.Max(0, span - 1);
        for (var i = index; i < index + span && i < sizes.Length; i++)
            size += sizes[i];

        return Math.Max(1f, size);
    }
}

public readonly record struct GridPlacement(int Row, int Column, int RowSpan, int ColumnSpan)
{
    public static GridPlacement Default { get; } = new(0, 0, 1, 1);
}
