using Orivy.Animation;
using Orivy.Collections;
using Orivy.Helpers;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;

namespace Orivy.Controls;

public class GridList : ElementBase
{
    private const float DefaultHeaderHeight = 38f;
    private const float DefaultRowHeight = 36f;
    private const float DefaultGroupHeaderHeight = 30f;
    private const float DefaultCellPadding = 10f;
    private const float ResizeGripWidth = 10f;
    private const float ResizeHitWidth = 18f;
    private const float RowResizeGripHeight = 6f;
    private const float CheckBoxSize = 16f;
    private const float IconSize = 16f;

    // Group collapse state is tracked per rendered group instance (group index) rather than group text.
    // This avoids collapsing every group that shares the same name when multiple groups have identical labels.
    private readonly Dictionary<int, bool> _collapsedGroups = new();
    private readonly Dictionary<int, AnimationManager> _groupAnimations = new();
    private readonly List<ColumnLayout> _columnLayouts = new();
    private readonly List<int> _displayItemIndices = new();
    private readonly List<LayoutEntry> _layoutEntries = new();
    private readonly HashSet<int> _selectedIndices = new();

    private float _bodyViewportHeight;
    private float _bodyViewportWidth;
    private float _contentHeight;
    private float _contentWidth;
    private bool _geometryDirty = true;
    private bool _headerVisible = true;
    private bool _stickyHeader = true;
    private bool _allowColumnResize = true;
    private bool _allowColumnSort = true;
    private bool _fullRowSelect = true;
    private bool _multiSelect;
    private bool _groupingEnabled;
    private bool _checkBoxes;
    private bool _allowRowResize;
    private bool _resizeAllRows;
    private bool _showGridLines = true;
    private bool _autoSortOnHeaderClick = true;
    private float _headerHeight;
    private float _rowHeight;
    private float _groupHeaderHeight;
    private float _cellPadding;
    private bool _hoveredHeader;
    private int _hoveredColumnIndex = -1;
    private int _hoveredHeaderResizeColumnIndex = -1;
    private int _hoveredGroupIndex = -1;
    private int _hoveredItemIndex = -1;
    private bool _rowToolTipActive;
    private int _hoveredRowResizeIndex = -1;
    private float _horizontalOffset;
    private bool _isResizingColumn;
    private bool _isResizingRow;
    private int _pressedColumnIndex = -1;
    private int _pressedItemIndex = -1;
    private float _resizeOriginX;
    private float _resizeOriginY;
    private float _resizeOriginWidth;
    private int _resizingColumnIndex = -1;
    private int _resizingRowIndex = -1;
    private float _resizeOriginRowHeight;
    private int _selectedIndex = -1;
    private GridListSortDirection _sortDirection;
    private int _sortColumnIndex = -1;
    private float _verticalOffset;
    private SKColor _headerBackColor;
    private SKColor _headerForeColor;
    private SKColor _groupHeaderBackColor;
    private SKColor _alternatingRowBackColor;
    private SKColor _hoverRowBackColor;
    private SKColor _selectionBackColor;
    private SKColor _gridLineColor;

    private readonly SKPaint _borderPaint = new() { IsAntialias = true, Style = SKPaintStyle.Stroke };
    private readonly SKPaint _fillPaint = new() { IsAntialias = true, Style = SKPaintStyle.Fill };
    private readonly SKPaint _gridLinePaint = new() { IsAntialias = false, Style = SKPaintStyle.Stroke, StrokeWidth = 1f };
    private readonly SKPaint _textPaint = new() { IsAntialias = true, Style = SKPaintStyle.Fill };
    private readonly SKPaint _groupAccentPaint = new() { IsAntialias = true, Style = SKPaintStyle.Fill };
    private readonly SKPaint _groupAccentBorderPaint = new() { IsAntialias = true, Style = SKPaintStyle.Stroke };
    private readonly SKPaint _chevronPaint = new() { IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeCap = SKStrokeCap.Round, StrokeJoin = SKStrokeJoin.Round };
    private readonly SKPath _chevronPath = new();
    // Cached paints for DrawCheckBox — prevents allocations per visible row per frame
    private readonly SKPaint _checkBoxBackPaint = new() { IsAntialias = true, Style = SKPaintStyle.Fill };
    private readonly SKPaint _checkBoxBorderPaint = new() { IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 1.5f };
    private readonly SKPaint _checkBoxCheckPaint = new() { IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 2f, StrokeCap = SKStrokeCap.Round, StrokeJoin = SKStrokeJoin.Round, Color = SKColors.White };
    private readonly SKPath _checkBoxCheckPath = new();
    // Cached paints for single-call per-frame draw helpers
    private readonly SKPaint _headerDividerPaint = new() { IsAntialias = false, Style = SKPaintStyle.Stroke, StrokeWidth = 1f };
    private readonly SKPaint _columnResizeGripPaint = new() { IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeCap = SKStrokeCap.Round };
    private readonly SKPaint _rowResizePaint = new() { IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 1.4f, StrokeCap = SKStrokeCap.Round };

    public GridList()
    {
        CanSelect = true;
        TabStop = false;
        Cursor = Cursors.Default;
        BackColor = SKColors.Transparent;
        Border = new Thickness(1);
        BorderColor = SKColors.Transparent;
        Radius = new Radius(12);
        HeaderHeight = DefaultHeaderHeight;
        RowHeight = DefaultRowHeight;
        GroupHeaderHeight = DefaultGroupHeaderHeight;
        CellPadding = DefaultCellPadding;
        AlternatingRowBackColor = SKColors.Empty;
        HeaderBackColor = SKColors.Empty;
        HeaderForeColor = SKColors.Empty;
        GroupHeaderBackColor = SKColors.Empty;
        SelectionBackColor = SKColors.Empty;
        HoverRowBackColor = SKColors.Empty;
        GridLineColor = SKColors.Empty;

        Columns = new GridListColumnCollection(this);
        Items = new GridListItemCollection(this);

        if (_vScrollBar != null)
            _vScrollBar.DisplayValueChanged += (_, _) => _verticalOffset = _vScrollBar.DisplayValue;
        
        if(_hScrollBar != null)
            _hScrollBar.DisplayValueChanged += (_, _) =>_horizontalOffset = _hScrollBar.DisplayValue;

        ColorScheme.ThemeChanged += OnThemeChanged;
    }

    public GridListColumnCollection Columns { get; }

    public GridListItemCollection Items { get; }

    [DefaultValue(true)]
    public bool HeaderVisible
    {
        get => _headerVisible;
        set => SetGeometryProperty(ref _headerVisible, value);
    }

    [DefaultValue(true)]
    public bool StickyHeader
    {
        get => _stickyHeader;
        set => SetGeometryProperty(ref _stickyHeader, value);
    }

    [DefaultValue(true)]
    public bool AllowColumnResize
    {
        get => _allowColumnResize;
        set => SetVisualProperty(ref _allowColumnResize, value);
    }

    [DefaultValue(true)]
    public bool AllowColumnSort
    {
        get => _allowColumnSort;
        set => SetVisualProperty(ref _allowColumnSort, value);
    }

    [DefaultValue(true)]
    public bool FullRowSelect
    {
        get => _fullRowSelect;
        set => SetVisualProperty(ref _fullRowSelect, value);
    }

    [DefaultValue(false)]
    public bool MultiSelect
    {
        get => _multiSelect;
        set => SetVisualProperty(ref _multiSelect, value);
    }

    [DefaultValue(false)]
    public bool GroupingEnabled
    {
        get => _groupingEnabled;
        set => SetGeometryProperty(ref _groupingEnabled, value);
    }

    [DefaultValue(false)]
    public bool CheckBoxes
    {
        get => _checkBoxes;
        set => SetGeometryProperty(ref _checkBoxes, value);
    }

    [DefaultValue(false)]
    public bool AllowRowResize
    {
        get => _allowRowResize;
        set => SetVisualProperty(ref _allowRowResize, value);
    }

    [DefaultValue(false)]
    public bool ResizeAllRows
    {
        get => _resizeAllRows;
        set => SetVisualProperty(ref _resizeAllRows, value);
    }

    [DefaultValue(true)]
    public bool ShowGridLines
    {
        get => _showGridLines;
        set => SetVisualProperty(ref _showGridLines, value);
    }

    [DefaultValue(true)]
    public bool AutoSortOnHeaderClick
    {
        get => _autoSortOnHeaderClick;
        set => SetVisualProperty(ref _autoSortOnHeaderClick, value);
    }

    [DefaultValue(DefaultHeaderHeight)]
    public float HeaderHeight
    {
        get => _headerHeight;
        set => SetGeometryProperty(ref _headerHeight, Math.Max(24f, value));
    }

    [DefaultValue(DefaultRowHeight)]
    public float RowHeight
    {
        get => _rowHeight;
        set => SetGeometryProperty(ref _rowHeight, Math.Max(22f, value));
    }

    [DefaultValue(DefaultGroupHeaderHeight)]
    public float GroupHeaderHeight
    {
        get => _groupHeaderHeight;
        set => SetGeometryProperty(ref _groupHeaderHeight, Math.Max(20f, value));
    }

    [DefaultValue(DefaultCellPadding)]
    public float CellPadding
    {
        get => _cellPadding;
        set => SetGeometryProperty(ref _cellPadding, Math.Max(2f, value));
    }

    public SKColor HeaderBackColor
    {
        get => _headerBackColor.IsEmpty() ? ColorScheme.SurfaceContainer : _headerBackColor;
        set => SetVisualProperty(ref _headerBackColor, value);
    }

    public SKColor HeaderForeColor
    {
        get => _headerForeColor.IsEmpty() ? ColorScheme.ForeColor : _headerForeColor;
        set => SetVisualProperty(ref _headerForeColor, value);
    }

    public SKColor GroupHeaderBackColor
    {
        get => _groupHeaderBackColor.IsEmpty() ? ColorScheme.SurfaceContainerHigh : _groupHeaderBackColor;
        set => SetVisualProperty(ref _groupHeaderBackColor, value);
    }

    public SKColor AlternatingRowBackColor
    {
        get => _alternatingRowBackColor.IsEmpty() ? ColorScheme.SurfaceContainer.WithAlpha(58) : _alternatingRowBackColor;
        set => SetVisualProperty(ref _alternatingRowBackColor, value);
    }

    public SKColor HoverRowBackColor
    {
        get => _hoverRowBackColor.IsEmpty() ? ColorScheme.Primary.WithAlpha(22) : _hoverRowBackColor;
        set => SetVisualProperty(ref _hoverRowBackColor, value);
    }

    public SKColor SelectionBackColor
    {
        get => _selectionBackColor.IsEmpty() ? ColorScheme.Primary.WithAlpha(44) : _selectionBackColor;
        set => SetVisualProperty(ref _selectionBackColor, value);
    }

    public SKColor GridLineColor
    {
        get => _gridLineColor.IsEmpty() ? ColorScheme.BorderColor.WithAlpha(64) : _gridLineColor;
        set => SetVisualProperty(ref _gridLineColor, value);
    }

    private void OnThemeChanged(object? sender, EventArgs e)
    {
        Invalidate();
    }

    [Browsable(false)]
    public int SelectedIndex
    {
        get => _selectedIndex;
        set => SetSelectedIndexCore(value, clearExisting: true, raiseEvent: true);
    }

    [Browsable(false)]
    public GridListItem? SelectedItem
    {
        get => _selectedIndex >= 0 && _selectedIndex < Items.Count ? Items[_selectedIndex] : null;
        set => SelectedIndex = value != null ? Items.IndexOf(value) : -1;
    }

    [Browsable(false)]
    public int[] SelectedIndices => [.. _selectedIndices];

    [Browsable(false)]
    public GridListItem[] SelectedItems => [.. _selectedIndices.Select(i => Items[i])];

    [Browsable(false)]
    public GridListSortDirection SortDirection => _sortDirection;

    [Browsable(false)]
    public int SortColumnIndex => _sortColumnIndex;

    public event EventHandler<int>? SelectedIndexChanged;
    public event EventHandler<GridListSelectionChangedEventArgs>? SelectionChanged;
    public event EventHandler<GridListColumnClickEventArgs>? ColumnClick;
    public event EventHandler<GridListCellEventArgs>? CellClick;

    /// <summary>
    /// When true, rows/cells/headers are drawn by the owner via <see cref="DrawItem"/>,
    /// <see cref="DrawSubItem"/> and <see cref="DrawColumnHeader"/> (WinForms ListView semantics:
    /// each handler may set <c>e.DrawDefault = true</c> to fall back to the built-in painting).
    /// </summary>
    [DefaultValue(false)]
    public bool OwnerDraw { get; set; }

    /// <summary>Occurs for each visible row when <see cref="OwnerDraw"/> is enabled.</summary>
    public event EventHandler<GridListDrawItemEventArgs>? DrawItem;

    /// <summary>Occurs for each visible cell when <see cref="OwnerDraw"/> is enabled.</summary>
    public event EventHandler<GridListDrawSubItemEventArgs>? DrawSubItem;

    /// <summary>Occurs for each column header when <see cref="OwnerDraw"/> is enabled.</summary>
    public event EventHandler<GridListDrawColumnHeaderEventArgs>? DrawColumnHeader;
    public event EventHandler<GridListCellCheckChangedEventArgs>? CellCheckChanged;

    internal void OnColumnsChanged(bool layoutAffected)
    {
        InvalidateFromModelChange(layoutAffected);
    }

    internal void OnItemsChanged(bool layoutAffected)
    {
        InvalidateFromModelChange(layoutAffected);
    }

    internal void ClearSelection()
    {
        _selectedIndices.Clear();
        _selectedIndex = -1;
    }

    private void SetGeometryProperty<T>(ref T field, T value)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
            return;

        field = value;
        _geometryDirty = true;
        Invalidate();
    }

    private void SetVisualProperty<T>(ref T field, T value)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
            return;

        field = value;
        Invalidate();
    }

    private void InvalidateFromModelChange(bool layoutAffected)
    {
        if (layoutAffected)
            _geometryDirty = true;

        Invalidate();
    }

    public override void OnDpiChanged(float newDpi, float oldDpi)
    {
        var safeOldDpi = oldDpi <= 0 ? 96f : oldDpi;
        var scale = newDpi <= 0 ? 1f : newDpi / safeOldDpi;

        if (Math.Abs(scale - 1f) > 0.001f)
        {
            HeaderHeight = Math.Max(24f, HeaderHeight * scale);
            RowHeight = Math.Max(22f, RowHeight * scale);
            GroupHeaderHeight = Math.Max(20f, GroupHeaderHeight * scale);
            CellPadding = Math.Max(2f, CellPadding * scale);

            for (var i = 0; i < Columns.Count; i++)
            {
                var column = Columns[i];
                column.MinWidth = Math.Max(24f, column.MinWidth * scale);
                column.MaxWidth = Math.Max(column.MinWidth, column.MaxWidth * scale);

                if (column.SizeMode == GridListColumnSizeMode.Fixed)
                    column.Width = Math.Clamp(column.Width * scale, column.MinWidth, column.MaxWidth);
            }

            for (var i = 0; i < Items.Count; i++)
            {
                if (Items[i].Height > 0.001f)
                    Items[i].Height = Math.Max(22f, Items[i].Height * scale);
            }

            _geometryDirty = true;
        }

        base.OnDpiChanged(newDpi, oldDpi);
    }

    protected override bool HandlesMouseWheelScroll =>
        (_vScrollBar != null && (_vScrollBar.Visible || _vScrollBar.Maximum > 0)) ||
        (_hScrollBar != null && (_hScrollBar.Visible || _hScrollBar.Maximum > 0));

    protected override float MouseWheelScrollLines => 1f;

    protected override float GetMouseWheelScrollStep(ScrollBar scrollBar)
    {
        return Math.Max(8f, scrollBar.SmallChange);
    }

    public override void OnSizeChanged(EventArgs e)
    {
        base.OnSizeChanged(e);
        _geometryDirty = true;
    }

    public override void OnMouseEnter(EventArgs e)
    {
        base.OnMouseEnter(e);
        _vScrollBar?.SetHostHover(_vScrollBar.Visible);
        _hScrollBar?.SetHostHover(_hScrollBar.Visible);
    }

    public override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        _hoveredHeader = false;
        _hoveredColumnIndex = -1;
        _hoveredHeaderResizeColumnIndex = -1;
        _hoveredGroupIndex = -1;
        _hoveredItemIndex = -1;
        _hoveredRowResizeIndex = -1;
        if (_rowToolTipActive)
        {
            ToolTipText = string.Empty;
            _rowToolTipActive = false;
        }
        Cursor = Cursors.Default;
        _vScrollBar?.SetHostHover(false);
        _hScrollBar?.SetHostHover(false);
        Invalidate();
    }

    public override void OnMouseMove(MouseEventArgs e)
    {
        if (TryGetInputTarget(e, out var target, out var childEventArgs) && target != null && childEventArgs != null)
        {
            target.OnMouseMove(childEventArgs);

            // Returning right after forwarding skips ElementBase.OnMouseMove's own hover-tracking
            // below, so a real embedded child control (e.g. PropertyGrid's search TextBox) never
            // becomes this element's LastHoveredElement — and since a child's Cursor only actually
            // reaches the OS cursor when OnCursorChanged finds it at the bottom of that hover chain
            // (see ElementBase.OnCursorChanged), its Cursor (an I-beam, a resize arrow, ...) silently
            // never took effect without mirroring that tracking here.
            if (!ReferenceEquals(target, LastHoveredElement))
            {
                LastHoveredElement?.OnMouseLeave(EventArgs.Empty);
                target.OnMouseEnter(EventArgs.Empty);
                LastHoveredElement = target;

                if (GetParentWindow() is { } window)
                {
                    var cursorElement = target;
                    while (cursorElement.LastHoveredElement != null)
                        cursorElement = cursorElement.LastHoveredElement;
                    window.UpdateCursor(cursorElement);
                }
            }

            return;
        }

        EnsureLayoutState();

        if (_isResizingColumn)
        {
            Cursor = Cursors.SizeWE;
            var column = GetColumn(_resizingColumnIndex);
            if (column != null)
            {
                column.Width = _resizeOriginWidth + (e.X - _resizeOriginX);
                _geometryDirty = true;
                Invalidate();
            }

            return;
        }

        if (_isResizingRow)
        {
            Cursor = Cursors.SizeNS;
            var nextHeight = Math.Max(22f, _resizeOriginRowHeight + (e.Y - _resizeOriginY));
            if (ResizeAllRows)
            {
                RowHeight = nextHeight;
            }
            else if (_resizingRowIndex >= 0 && _resizingRowIndex < Items.Count)
            {
                Items[_resizingRowIndex].Height = nextHeight;
            }

            _geometryDirty = true;
            Invalidate();
            return;
        }

        var hoverInfo = HitTestCore(e.Location);
        _hoveredHeader = hoverInfo.Kind == HitKind.Header || hoverInfo.Kind == HitKind.HeaderResize;
        _hoveredColumnIndex = hoverInfo.ColumnIndex;
        _hoveredHeaderResizeColumnIndex = hoverInfo.Kind == HitKind.HeaderResize ? hoverInfo.ColumnIndex : -1;
        _hoveredGroupIndex = hoverInfo.GroupIndex;
        _hoveredItemIndex = hoverInfo.ItemIndex;
        _hoveredRowResizeIndex = hoverInfo.Kind == HitKind.RowResize ? hoverInfo.ItemIndex : -1;

        // Surface the hovered row's ToolTipText through the element tooltip system. Only manage the
        // control-level tooltip when a row supplies one, so a user-set grid tooltip is preserved.
        var rowTip = hoverInfo.ItemIndex >= 0 && hoverInfo.ItemIndex < Items.Count
            ? Items[hoverInfo.ItemIndex].ToolTipText
            : string.Empty;
        if (!string.IsNullOrEmpty(rowTip))
        {
            if (!string.Equals(ToolTipText, rowTip, StringComparison.Ordinal))
                ToolTipText = rowTip;
            _rowToolTipActive = true;
        }
        else if (_rowToolTipActive)
        {
            ToolTipText = string.Empty;
            _rowToolTipActive = false;
        }
        Cursor = hoverInfo.Kind switch
        {
            HitKind.HeaderResize => Cursors.SizeWE,
            HitKind.RowResize => Cursors.SizeNS,
            _ => Cursors.Default
        };
        Invalidate();
    }

    public override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);

        if (e.Button != MouseButtons.Left)
            return;

        // base.OnMouseDown already routed the event to the hit child (ElementBase does its own
        // TryGetInputTarget dispatch). Dispatching again here would deliver a SECOND OnMouseDown to
        // toggle-style children (ComboBox, DatePicker), instantly closing the drop-down they just
        // opened. Only detect the child hit and skip the grid's own row/header handling.
        if (TryGetInputTarget(e, out var downTarget, out _) && downTarget != null)
            return;

        EnsureLayoutState();

        var hit = HitTestCore(e.Location);
        if (hit.Kind == HitKind.HeaderResize && AllowColumnResize && hit.ColumnIndex >= 0)
        {
            var column = GetColumn(hit.ColumnIndex);
            if (column != null)
            {
                if (TryGetColumnLayout(hit.ColumnIndex, out var columnLayout))
                {
                    column.Width = columnLayout.Width;
                    column.SizeMode = GridListColumnSizeMode.Fixed;
                    _resizeOriginWidth = columnLayout.Width;
                }
                else
                {
                    column.SizeMode = GridListColumnSizeMode.Fixed;
                    _resizeOriginWidth = column.Width;
                }

                _isResizingColumn = true;
                _resizingColumnIndex = hit.ColumnIndex;
                _resizeOriginX = e.X;
                GetParentWindow()?.SetMouseCapture(this);
            }

            return;
        }

        if (hit.Kind == HitKind.RowResize && AllowRowResize && hit.ItemIndex >= 0)
        {
            _isResizingRow = true;
            _resizingRowIndex = hit.ItemIndex;
            _resizeOriginY = e.Y;
            _resizeOriginRowHeight = GetResolvedRowHeight(hit.ItemIndex);
            Cursor = Cursors.SizeNS;
            GetParentWindow()?.SetMouseCapture(this);
            return;
        }

        _pressedColumnIndex = hit.ColumnIndex;
        _pressedItemIndex = hit.ItemIndex;

        switch (hit.Kind)
        {
            case HitKind.GroupHeader:
                ToggleGroupCollapsed(hit.GroupIndex);
                break;
            case HitKind.ItemCell:
                if (hit.ItemIndex >= 0)
                    HandleItemMouseDown(hit, e);
                break;
            case HitKind.Header:
                if (AllowColumnSort && AutoSortOnHeaderClick && hit.ColumnIndex >= 0)
                    ToggleSort(hit.ColumnIndex);
                break;
        }
    }

    public override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);

        if (e.Button != MouseButtons.Left)
            return;

        if (_isResizingColumn)
        {
            _isResizingColumn = false;
            _resizingColumnIndex = -1;
            Cursor = Cursors.Default;
            GetParentWindow()?.ReleaseMouseCapture(this);
            return;
        }

        if (_isResizingRow)
        {
            _isResizingRow = false;
            _resizingRowIndex = -1;
            Cursor = Cursors.Default;
            GetParentWindow()?.ReleaseMouseCapture(this);
            return;
        }

        // See OnMouseDown: base already dispatched to the hit child — don't dispatch twice.
        if (TryGetInputTarget(e, out var upTarget, out _) && upTarget != null)
            return;

        var hit = HitTestCore(e.Location);
        if (hit.Kind == HitKind.ItemCell && hit.ItemIndex == _pressedItemIndex && hit.ColumnIndex == _pressedColumnIndex)
        {
            RaiseCellClick(hit.ItemIndex, hit.ColumnIndex);
        }

        if (hit.Kind == HitKind.Header && hit.ColumnIndex == _pressedColumnIndex)
        {
            var column = GetColumn(hit.ColumnIndex);
            if (column != null)
                ColumnClick?.Invoke(this, new GridListColumnClickEventArgs(column, hit.ColumnIndex, _sortDirection));
        }

        _pressedColumnIndex = -1;
        _pressedItemIndex = -1;
    }

    public override void OnMouseDoubleClick(MouseEventArgs e)
    {
        base.OnMouseDoubleClick(e);

        if (e.Button != MouseButtons.Left)
            return;

        // See OnMouseDown: base already dispatched to the hit child — don't dispatch twice.
        if (TryGetInputTarget(e, out var dblTarget, out _) && dblTarget != null)
            return;

        var hit = HitTestCore(e.Location);
        if (hit.Kind == HitKind.HeaderResize && hit.ColumnIndex >= 0)
        {
            AutoSizeColumn(hit.ColumnIndex);
            return;
        }

        if (hit.Kind == HitKind.RowResize && hit.ItemIndex >= 0)
        {
            ResetRowSize(hit.ItemIndex);
        }
    }

    public override void OnMouseWheel(MouseEventArgs e)
    {
        if (!Enabled || !Visible)
            return;

        var wantsHorizontal = WantsHorizontalMouseWheel(e);
        if (wantsHorizontal && _hScrollBar != null && (_hScrollBar.Visible || _hScrollBar.Maximum > 0))
        {
            var deltaValue = GetMouseWheelDelta(e, _hScrollBar);
            _hScrollBar.ApplyWheelDelta(e.IsHorizontalWheel ? deltaValue : -deltaValue);
            return;
        }

        base.OnMouseWheel(e);
    }

    public override void  OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);

        if (Items.Count == 0)
            return;

        switch (e.KeyCode)
        {
            case Keys.Down:
                MoveSelection(1);
                e.Handled = true;
                break;
            case Keys.Up:
                MoveSelection(-1);
                e.Handled = true;
                break;
            case Keys.PageDown:
                MoveSelection(Math.Max(1, (int)Math.Floor(Math.Max(RowHeight, _bodyViewportHeight) / Math.Max(1f, RowHeight))));
                e.Handled = true;
                break;
            case Keys.PageUp:
                MoveSelection(-Math.Max(1, (int)Math.Floor(Math.Max(RowHeight, _bodyViewportHeight) / Math.Max(1f, RowHeight))));
                e.Handled = true;
                break;
            case Keys.Home:
                SelectedIndex = FindNextSelectableIndex(0, 1);
                e.Handled = true;
                break;
            case Keys.End:
                SelectedIndex = FindNextSelectableIndex(Items.Count - 1, -1);
                e.Handled = true;
                break;
            case Keys.Space:
                if (_selectedIndex >= 0)
                {
                    var checkboxColumn = FindFirstCheckBoxColumn();
                    if (checkboxColumn >= 0)
                    {
                        ToggleCellCheckState(_selectedIndex, checkboxColumn);
                        e.Handled = true;
                    }
                }

                break;
        }
    }

    public override void OnFontChanged(EventArgs e)
    {
        base.OnFontChanged(e);
        _geometryDirty = true;
        Invalidate();
    }

    public override void OnPaint(SKCanvas canvas)
    {
        base.OnPaint(canvas);
        EnsureLayoutState();
        using var renderFont = CreateRenderFont(Font);

        _fillPaint.Color = BackColor == SKColors.Transparent ? ColorScheme.Surface : BackColor;
        _textPaint.Color = ForeColor;
        _borderPaint.Color = GridLineColor;
        _gridLinePaint.Color = GridLineColor;

        var outerRect = GetOuterViewport();
        outerRect = new SKRect(
            (float)Math.Floor(outerRect.Left),
            (float)Math.Floor(outerRect.Top),
            (float)Math.Ceiling(outerRect.Right),
            (float)Math.Ceiling(outerRect.Bottom));

        // Ensure we always clear the background (especially when BackColor is Transparent)
        canvas.DrawRect(outerRect, _fillPaint);

        var bodyViewport = GetBodyViewportRect(outerRect);
        var saveCount = canvas.Save();
        canvas.ClipRect(bodyViewport);

        var roundedHorizontalOffset = (float)Math.Round(_horizontalOffset);
        var roundedVerticalOffset = (float)Math.Round(_verticalOffset);

        for (var i = 0; i < _layoutEntries.Count; i++)
        {
            var entry = _layoutEntries[i];
            var drawRect = entry.Bounds;

            if (HeaderVisible && !StickyHeader && entry.Kind == EntryKind.Header)
                drawRect.Offset(bodyViewport.Left - roundedHorizontalOffset, outerRect.Top - roundedVerticalOffset);
            else
                drawRect.Offset(bodyViewport.Left - roundedHorizontalOffset, bodyViewport.Top - roundedVerticalOffset);

            if (drawRect.Bottom < bodyViewport.Top || drawRect.Top > bodyViewport.Bottom)
                continue;

            switch (entry.Kind)
            {
                case EntryKind.Header:
                    DrawHeader(canvas, drawRect, roundedHorizontalOffset, renderFont);
                    break;
                case EntryKind.GroupHeader:
                    DrawGroupHeader(canvas, drawRect, entry.GroupText ?? string.Empty, entry.GroupKey ?? string.Empty, entry.GroupIndex, renderFont);
                    break;
                case EntryKind.Item:
                    DrawItemRow(canvas, drawRect, entry.ItemIndex, renderFont);
                    break;
            }
        }

        canvas.RestoreToCount(saveCount);

        if (HeaderVisible && StickyHeader)
        {
            var stickyHeaderRect = GetStickyHeaderRect(outerRect);
            DrawHeader(canvas, stickyHeaderRect, roundedHorizontalOffset, renderFont);

            _headerDividerPaint.Color = GridLineColor.WithAlpha(180);
            canvas.DrawLine(stickyHeaderRect.Left, stickyHeaderRect.Bottom, stickyHeaderRect.Right, stickyHeaderRect.Bottom, _headerDividerPaint);
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            ColorScheme.ThemeChanged -= OnThemeChanged;

            foreach (var animation in _groupAnimations.Values)
                animation.Dispose();

            _groupAnimations.Clear();
            _groupAccentPaint.Dispose();
            _groupAccentBorderPaint.Dispose();
            _chevronPaint.Dispose();
            _chevronPath.Dispose();
            _checkBoxBackPaint.Dispose();
            _checkBoxBorderPaint.Dispose();
            _checkBoxCheckPaint.Dispose();
            _checkBoxCheckPath.Dispose();
            _headerDividerPaint.Dispose();
            _columnResizeGripPaint.Dispose();
            _rowResizePaint.Dispose();
        }

        base.Dispose(disposing);
    }

    public void SortByColumn(int columnIndex, GridListSortDirection direction)
    {
        if (columnIndex < 0 || columnIndex >= Columns.Count)
            return;

        _sortColumnIndex = columnIndex;
        _sortDirection = direction;
        _geometryDirty = true;
        Invalidate();
    }

    /// <summary>
    /// Scrolls the grid so the specified item is visible.
    /// </summary>
    public void EnsureVisible(GridListItem item)
    {
        var index = Items.IndexOf(item);
        EnsureItemVisible(index);
    }

    /// <summary>
    /// Scrolls the grid so the item at the specified index is visible.
    /// </summary>
    public void EnsureVisible(int itemIndex)
    {
        EnsureItemVisible(itemIndex);
    }

    private void ToggleSort(int columnIndex)
    {
        var column = GetColumn(columnIndex);
        if (column == null || !column.Sortable)
            return;

        if (_sortColumnIndex != columnIndex)
        {
            _sortColumnIndex = columnIndex;
            _sortDirection = GridListSortDirection.Ascending;
        }
        else
        {
            _sortDirection = _sortDirection switch
            {
                GridListSortDirection.None => GridListSortDirection.Ascending,
                GridListSortDirection.Ascending => GridListSortDirection.Descending,
                _ => GridListSortDirection.None
            };
        }

        _geometryDirty = true;
        Invalidate();
    }

    private void MoveSelection(int delta)
    {
        var startIndex = _selectedIndex < 0 ? 0 : _selectedIndex + delta;
        var direction = delta >= 0 ? 1 : -1;
        var target = FindNextSelectableIndex(startIndex, direction);
        if (target >= 0)
            SelectedIndex = target;
    }

    private int FindNextSelectableIndex(int startIndex, int direction)
    {
        if (Items.Count == 0)
            return -1;

        var index = Math.Clamp(startIndex, 0, Items.Count - 1);
        while (index >= 0 && index < Items.Count)
        {
            if (Items[index].Visible)
                return index;
            index += direction;
        }

        return -1;
    }

    internal bool IsItemSelected(int index) => _selectedIndices.Contains(index);

    /// <summary>Adds/removes a single row to/from the selection (backs GridListItem.Selected).</summary>
    internal void SetItemSelected(int index, bool selected)
    {
        if (index < 0 || index >= Items.Count || selected == _selectedIndices.Contains(index))
            return;

        var previous = _selectedIndex;

        if (selected)
        {
            if (!MultiSelect)
                _selectedIndices.Clear();
            _selectedIndices.Add(index);
            _selectedIndex = index;
        }
        else
        {
            _selectedIndices.Remove(index);
            if (_selectedIndex == index)
                _selectedIndex = _selectedIndices.Count > 0 ? _selectedIndices.First() : -1;
        }

        Invalidate();

        if (previous != _selectedIndex)
        {
            SelectedIndexChanged?.Invoke(this, previous);
            SelectionChanged?.Invoke(this, new GridListSelectionChangedEventArgs(previous, _selectedIndex));
        }
    }

    private void SetSelectedIndexCore(int index, bool clearExisting, bool raiseEvent)
    {
        if (Items.Count == 0)
        {
            index = -1;
        }
        else if (index >= 0)
        {
            index = Math.Clamp(index, 0, Items.Count - 1);
        }

        var previous = _selectedIndex;

        if (clearExisting)
            _selectedIndices.Clear();

        if (index >= 0)
            _selectedIndices.Add(index);

        _selectedIndex = index;
        EnsureItemVisible(_selectedIndex);
        Invalidate();

        if (raiseEvent && previous != _selectedIndex)
        {
            SelectedIndexChanged?.Invoke(this, previous);
            SelectionChanged?.Invoke(this, new GridListSelectionChangedEventArgs(previous, _selectedIndex));
        }
    }

    /// <summary>
    /// Ensures the specified item index is visible in the viewport by scrolling if necessary.
    /// </summary>
    public void EnsureItemVisible(int itemIndex)
    {
        EnsureLayoutState();
        for (var i = 0; i < _layoutEntries.Count; i++)
        {
            var entry = _layoutEntries[i];
            if (entry.Kind != EntryKind.Item || entry.ItemIndex != itemIndex)
                continue;

            var itemTop = entry.Bounds.Top;
            var itemBottom = entry.Bounds.Bottom;
            var viewportHeight = _bodyViewportHeight;

            if (itemTop < _verticalOffset)
                _vScrollBar!.Value = itemTop;
            else if (itemBottom > _verticalOffset + viewportHeight)
                _vScrollBar!.Value = Math.Max(0, itemBottom - viewportHeight);
            return;
        }
    }

    private void HandleItemMouseDown(HitInfo hit, MouseEventArgs e)
    {
        if (hit.ItemIndex < 0 || hit.ItemIndex >= Items.Count || hit.ColumnIndex < 0 || hit.ColumnIndex >= Columns.Count)
            return;

        if (!MultiSelect || (ModifierKeys & Keys.Control) == 0)
        {
            SetSelectedIndexCore(hit.ItemIndex, clearExisting: true, raiseEvent: true);
        }
        else
        {
            var previous = _selectedIndex;
            if (_selectedIndices.Contains(hit.ItemIndex))
                _selectedIndices.Remove(hit.ItemIndex);
            else
                _selectedIndices.Add(hit.ItemIndex);

            _selectedIndex = hit.ItemIndex;
            Invalidate();

            if (previous != _selectedIndex)
            {
                SelectedIndexChanged?.Invoke(this, previous);
                SelectionChanged?.Invoke(this, new GridListSelectionChangedEventArgs(previous, _selectedIndex));
            }
        }

        if (hit.CheckBoxRect.Contains(e.Location))
            ToggleCellCheckState(hit.ItemIndex, hit.ColumnIndex);
    }

    private void ToggleCellCheckState(int itemIndex, int columnIndex)
    {
        var cell = GetCell(itemIndex, columnIndex, createMissing: true);
        var column = GetColumn(columnIndex);
        if (cell == null || column == null)
            return;

        var previous = cell.CheckState;
        cell.CheckState = previous == CheckState.Checked ? CheckState.Unchecked : CheckState.Checked;
        CellCheckChanged?.Invoke(this,
            new GridListCellCheckChangedEventArgs(Items[itemIndex], column, cell, itemIndex, columnIndex, previous, cell.CheckState));
    }

    private void RaiseCellClick(int itemIndex, int columnIndex)
    {
        var column = GetColumn(columnIndex);
        var cell = GetCell(itemIndex, columnIndex, createMissing: true);
        if (column == null || cell == null)
            return;

        CellClick?.Invoke(this, new GridListCellEventArgs(Items[itemIndex], column, cell, itemIndex, columnIndex));
    }

    private void ToggleGroupCollapsed(int groupIndex)
    {
        if (groupIndex < 0)
            return;

        var collapsed = _collapsedGroups.TryGetValue(groupIndex, out var isCollapsed) && isCollapsed;
        var nextCollapsed = !collapsed;
        _collapsedGroups[groupIndex] = nextCollapsed;
        var animation = EnsureGroupAnimation(groupIndex);
        animation.SetProgress(GetGroupExpansionProgress(groupIndex));
        animation.StartNewAnimation(nextCollapsed ? AnimationDirection.Out : AnimationDirection.In);
        _geometryDirty = true;
        Invalidate();
    }

    private void EnsureLayoutState()
    {
        if (!_geometryDirty)
            return;

        using var renderFont = CreateRenderFont(Font);
        var outer = GetOuterViewport();
        var initialWidth = Math.Max(1f, outer.Width);
        BuildColumnLayouts(initialWidth, renderFont);
        BuildDisplayEntries();
        UpdateScrollState();

        BuildColumnLayouts(Math.Max(1f, _bodyViewportWidth), renderFont);
        BuildDisplayEntries();
        UpdateScrollState();
        _geometryDirty = false;
    }

    private void BuildColumnLayouts(float availableWidth, SKFont font)
    {
        _columnLayouts.Clear();

        var resolvedWidths = new float[Columns.Count];
        var fillColumns = new List<int>();
        var fixedWidthTotal = 0f;
        var fillWeightTotal = 0f;

        for (var i = 0; i < Columns.Count; i++)
        {
            var column = Columns[i];
            if (!column.Visible)
                continue;

            if (column.SizeMode == GridListColumnSizeMode.Fill)
            {
                fillColumns.Add(i);
                fillWeightTotal += Math.Max(0.01f, column.FillWeight);
                continue;
            }

            var width = column.SizeMode == GridListColumnSizeMode.Auto
                ? MeasurePreferredColumnWidth(i, font, font)
                : column.Width;

            resolvedWidths[i] = Math.Clamp(width, column.MinWidth, column.MaxWidth);
            fixedWidthTotal += resolvedWidths[i];
        }

        if (fillColumns.Count > 0)
        {
            var remainingWidth = Math.Max(0f, availableWidth - fixedWidthTotal);
            for (var fillIndex = 0; fillIndex < fillColumns.Count; fillIndex++)
            {
                var columnIndex = fillColumns[fillIndex];
                var column = Columns[columnIndex];
                var share = remainingWidth * (column.FillWeight / fillWeightTotal);
                resolvedWidths[columnIndex] = Math.Clamp(Math.Max(column.MinWidth, share), column.MinWidth, column.MaxWidth);
            }
        }

        var x = 0f;
        for (var i = 0; i < Columns.Count; i++)
        {
            var column = Columns[i];
            if (!column.Visible)
                continue;

            var width = resolvedWidths[i] > 0f ? resolvedWidths[i] : Math.Clamp(column.Width, column.MinWidth, column.MaxWidth);
            _columnLayouts.Add(new ColumnLayout(i, x, width));
            x += width;
        }

        _contentWidth = x;
    }

    private void BuildDisplayEntries()
    {
        _displayItemIndices.Clear();
        _layoutEntries.Clear();

        for (var i = 0; i < Items.Count; i++)
        {
            if (Items[i].Visible)
                _displayItemIndices.Add(i);
        }

        if (_sortColumnIndex >= 0 && _sortDirection != GridListSortDirection.None)
            _displayItemIndices.Sort(CompareDisplayItems);

        var y = 0f;
        if (HeaderVisible && !StickyHeader)
        {
            _layoutEntries.Add(LayoutEntry.Header(new SKRect(0, y, _contentWidth, y + HeaderHeight)));
            y += HeaderHeight;
        }

        string currentGroupKey = string.Empty;
        string currentGroupText = string.Empty;
        var currentGroupExpansion = 1f;
        var groupIndex = -1;

        for (var i = 0; i < _displayItemIndices.Count; i++)
        {
            var itemIndex = _displayItemIndices[i];
            var item = Items[itemIndex];

            if (GroupingEnabled)
            {
                var nextGroupKey = string.IsNullOrEmpty(item.GroupKey) ? string.Empty : item.GroupKey;
                var nextGroupText = string.IsNullOrEmpty(item.GroupText) ? nextGroupKey : item.GroupText;
                if (!string.Equals(currentGroupKey, nextGroupKey, StringComparison.Ordinal))
                {
                    currentGroupKey = nextGroupKey;
                    currentGroupText = nextGroupText;
                    groupIndex++;
                    _layoutEntries.Add(LayoutEntry.GroupHeader(new SKRect(0, y, _contentWidth, y + GroupHeaderHeight), currentGroupKey, currentGroupText, groupIndex));
                    y += GroupHeaderHeight;
                    currentGroupExpansion = GetGroupExpansionProgress(groupIndex);
                }

                if (currentGroupExpansion <= 0.001f)
                    continue;

                var itemHeight = Math.Max(0f, GetResolvedRowHeight(itemIndex) * currentGroupExpansion);
                _layoutEntries.Add(LayoutEntry.Item(new SKRect(0, y, _contentWidth, y + itemHeight), itemIndex));
                y += itemHeight;
                continue;
            }

            var rowHeight = GetResolvedRowHeight(itemIndex);
            _layoutEntries.Add(LayoutEntry.Item(new SKRect(0, y, _contentWidth, y + rowHeight), itemIndex));
            y += rowHeight;
        }

        _contentHeight = y;
    }

    private void UpdateScrollState()
    {
        var outer = GetOuterViewport();
        var showStickyHeader = HeaderVisible && StickyHeader;
        var availableWidth = Math.Max(1f, outer.Width);
        var availableHeight = Math.Max(1f, outer.Height - (showStickyHeader ? HeaderHeight : 0f));
        var overlayInset = MathF.Max(2f, 4f * ScaleFactor);

        var needsVScroll = _contentHeight > availableHeight;
        var needsHScroll = _contentWidth > availableWidth;

        _bodyViewportWidth = availableWidth;
        _bodyViewportHeight = availableHeight;

        if (_vScrollBar != null)
        {
            _vScrollBar.Visible = needsVScroll;
            if (needsVScroll)
            {
                _vScrollBar.Location = new SKPoint(Math.Max(0f, outer.Right - _vScrollBar.Thickness - overlayInset), showStickyHeader ? outer.Top + HeaderHeight + overlayInset : outer.Top + overlayInset);
                _vScrollBar.Size = new SKSize(_vScrollBar.Thickness, Math.Max(1f, availableHeight - overlayInset * 2 - (needsHScroll ? _hScrollBar.Thickness : 0)));
                _vScrollBar.Minimum = 0;
                _vScrollBar.Maximum = Math.Max(0, _contentHeight - availableHeight);
                _vScrollBar.SmallChange = Math.Max(8f, RowHeight);
                _vScrollBar.LargeChange = Math.Max(RowHeight, availableHeight * 0.85f);
                if (_vScrollBar.Value > _vScrollBar.Maximum)
                    _vScrollBar.Value = _vScrollBar.Maximum;
                _verticalOffset = _vScrollBar.DisplayValue;
                _vScrollBar.BringToFront();
            }
            else
            {
                _vScrollBar.Value = 0;
                _verticalOffset = 0f;
            }
        }

        if (_hScrollBar != null)
        {
            _hScrollBar.Visible = needsHScroll;
            if (needsHScroll)
            {
                _hScrollBar.Location = new SKPoint(outer.Left + overlayInset, Math.Max(0f, outer.Bottom - _hScrollBar.Thickness - overlayInset));
                _hScrollBar.Size = new SKSize(Math.Max(1f, availableWidth - overlayInset * 2 - (needsVScroll ? _vScrollBar.Thickness : 0)), _hScrollBar.Thickness);
                _hScrollBar.Minimum = 0;
                _hScrollBar.Maximum = Math.Max(0, _contentWidth - availableWidth);
                _hScrollBar.SmallChange = Math.Max(8f, 32f * ScaleFactor);
                _hScrollBar.LargeChange = Math.Max(32f, availableWidth * 0.85f);
                if (_hScrollBar.Value > _hScrollBar.Maximum)
                    _hScrollBar.Value = _hScrollBar.Maximum;
                _horizontalOffset = _hScrollBar.DisplayValue;
                _hScrollBar.BringToFront();
            }
            else
            {
                _hScrollBar.Value = 0;
                _horizontalOffset = 0f;
            }
        }

        _vScrollBar?.SetHostHover(_vScrollBar.Visible && IsPointerOver);
        _hScrollBar?.SetHostHover(_hScrollBar.Visible && IsPointerOver);
    }

    private int CompareDisplayItems(int leftIndex, int rightIndex)
    {
        var leftCell = GetCell(leftIndex, _sortColumnIndex, createMissing: false);
        var rightCell = GetCell(rightIndex, _sortColumnIndex, createMissing: false);

        var compare = CompareCellValues(leftCell, rightCell);
        return _sortDirection == GridListSortDirection.Descending ? -compare : compare;
    }

    private static int CompareCellValues(GridListCell? leftCell, GridListCell? rightCell)
    {
        var leftValue = leftCell?.Value ?? leftCell?.Text ?? string.Empty;
        var rightValue = rightCell?.Value ?? rightCell?.Text ?? string.Empty;

        if (leftValue is IComparable comparable && rightValue != null && leftValue.GetType() == rightValue.GetType())
            return comparable.CompareTo(rightValue);

        return string.Compare(leftValue?.ToString(), rightValue?.ToString(), StringComparison.CurrentCultureIgnoreCase);
    }

    private float GetResolvedRowHeight(int itemIndex)
    {
        if (ResizeAllRows)
            return RowHeight;

        if (itemIndex < 0 || itemIndex >= Items.Count)
            return RowHeight;

        // Allow very small programmatic heights (expand/collapse reveal animations set Height as
        // low as ~2px). The interactive row-resize path applies its own 22px minimum at the drag
        // site, so user resizing is unaffected.
        var customHeight = Items[itemIndex].Height;
        return customHeight > 0.001f ? Math.Max(2f, customHeight) : RowHeight;
    }

    private void ResetRowSize(int itemIndex)
    {
        if (ResizeAllRows)
        {
            RowHeight = DefaultRowHeight;
            return;
        }

        if (itemIndex < 0 || itemIndex >= Items.Count)
            return;

        Items[itemIndex].Height = 0f;
    }

    private void AutoSizeColumn(int columnIndex)
    {
        if (columnIndex < 0 || columnIndex >= Columns.Count)
            return;

        var column = Columns[columnIndex];
        using var renderFont = CreateRenderFont(Font);
        var preferredWidth = MeasurePreferredColumnWidth(columnIndex, renderFont, renderFont);
        column.Width = preferredWidth;
        column.SizeMode = GridListColumnSizeMode.Fixed;
        _geometryDirty = true;
        Invalidate();
    }

    private float MeasurePreferredColumnWidth(int columnIndex, SKFont headerFont, SKFont textFont)
    {
        var column = Columns[columnIndex];
        var maxWidth = CellPadding * 2f;

        if (column.HeaderIcon != null)
            maxWidth += IconSize + CellPadding * 0.75f;

        maxWidth += headerFont.MeasureText(column.Text ?? string.Empty);
        maxWidth += 18f;
        if (AllowColumnResize && column.Resizable)
            maxWidth += ResizeGripWidth + 6f;

        for (var itemIndex = 0; itemIndex < Items.Count; itemIndex++)
        {
            var item = Items[itemIndex];
            if (!item.Visible)
                continue;

            var measuredWidth = CellPadding * 2f;
            if (ShouldShowCheckBox(column, columnIndex))
                measuredWidth += CheckBoxSize + CellPadding * 0.75f;

            var cell = GetCell(itemIndex, columnIndex, createMissing: false);
            var icon = column.ShowIcons ? cell?.Icon ?? item.Icon : null;
            if (icon != null)
                measuredWidth += IconSize + CellPadding * 0.75f;

            var text = cell?.Text;
            if (string.IsNullOrEmpty(text) && cell?.Value != null)
                text = cell.Value.ToString();

            if (!string.IsNullOrEmpty(text))
                measuredWidth += textFont.MeasureText(text);

            maxWidth = Math.Max(maxWidth, measuredWidth);
        }

        return Math.Clamp(maxWidth + 6f, column.MinWidth, column.MaxWidth);
    }

    private AnimationManager EnsureGroupAnimation(int groupIndex)
    {
        if (_groupAnimations.TryGetValue(groupIndex, out var animation))
            return animation;

        animation = new AnimationManager(true)
        {
            Increment = 0.18,
            AnimationType = AnimationType.CubicEaseOut,
            InterruptAnimation = true
        };
        animation.SetProgress(_collapsedGroups.TryGetValue(groupIndex, out var collapsed) && collapsed ? 0d : 1d);
        animation.OnAnimationProgress += _ =>
        {
            _geometryDirty = true;
            Invalidate();
        };
        animation.OnAnimationFinished += _ =>
        {
            _geometryDirty = true;
            Invalidate();
        };
        _groupAnimations[groupIndex] = animation;
        return animation;
    }

    private float GetGroupExpansionProgress(int groupIndex)
    {
        if (groupIndex < 0)
            return 1f;

        if (_groupAnimations.TryGetValue(groupIndex, out var animation))
            return Math.Clamp((float)animation.GetProgress(), 0f, 1f);

        return _collapsedGroups.TryGetValue(groupIndex, out var collapsed) && collapsed ? 0f : 1f;
    }

    /// <summary>
    /// The rect every row/header/scrollbar/hit-test computation is anchored to. Every call site in
    /// this file goes through this single method, so a subclass reserving chrome space above the
    /// rows (e.g. a search box) only needs to override this one spot to stay consistent everywhere.
    /// </summary>
    protected virtual SKRect GetOuterViewport()
    {
        return new SKRect(Border.Left, Border.Top, Width - Border.Right, Height - Border.Bottom);
    }

    private SKRect GetStickyHeaderRect(SKRect outer)
    {
        return new SKRect(outer.Left, outer.Top, outer.Left + _bodyViewportWidth, outer.Top + HeaderHeight);
    }

    private SKRect GetBodyViewportRect(SKRect outer)
    {
        if (HeaderVisible && StickyHeader)
            return new SKRect(outer.Left, outer.Top + HeaderHeight, outer.Left + _bodyViewportWidth, outer.Top + HeaderHeight + _bodyViewportHeight);

        return new SKRect(outer.Left, outer.Top, outer.Left + _bodyViewportWidth, outer.Top + _bodyViewportHeight);
    }

    private bool TryGetColumnLayout(int columnIndex, out ColumnLayout columnLayout)
    {
        for (var i = 0; i < _columnLayouts.Count; i++)
        {
            if (_columnLayouts[i].ColumnIndex != columnIndex)
                continue;

            columnLayout = _columnLayouts[i];
            return true;
        }

        columnLayout = default;
        return false;
    }

    private void DrawHeader(SKCanvas canvas, SKRect bounds, float horizontalScroll, SKFont font)
    {
        _fillPaint.Color = HeaderBackColor;
        canvas.DrawRect(bounds, _fillPaint);

        for (var i = 0; i < _columnLayouts.Count; i++)
        {
            var layout = _columnLayouts[i];
            var cellRect = new SKRect(bounds.Left + layout.X - horizontalScroll, bounds.Top, bounds.Left + layout.X + layout.Width - horizontalScroll, bounds.Bottom);
            if (cellRect.Right < bounds.Left || cellRect.Left > bounds.Right)
                continue;

            var isHovered = _hoveredHeader && _hoveredColumnIndex == layout.ColumnIndex;
            if (isHovered)
            {
                _fillPaint.Color = HeaderBackColor.Brightness(0.05f);
                canvas.DrawRect(cellRect, _fillPaint);
            }

            var column = Columns[layout.ColumnIndex];

            if (OwnerDraw && DrawColumnHeader != null)
            {
                var headerArgs = new GridListDrawColumnHeaderEventArgs(this, canvas, column, layout.ColumnIndex, cellRect, font, isHovered);
                DrawColumnHeader.Invoke(this, headerArgs);
                if (!headerArgs.DrawDefault)
                    continue;
            }

            var contentRect = cellRect;
            contentRect.Inflate(-CellPadding, 0);
            contentRect.Right -= AllowColumnResize && column.Resizable ? ResizeGripWidth + 4f : 0f;

            if (column.HeaderIcon != null)
            {
                var iconRect = new SKRect(contentRect.Left, contentRect.MidY - IconSize / 2f, contentRect.Left + IconSize, contentRect.MidY + IconSize / 2f);
                canvas.DrawImage(column.HeaderIcon, iconRect);
                contentRect.Left = iconRect.Right + CellPadding * 0.75f;
            }

            _textPaint.Color = HeaderForeColor;
            TextRenderer.DrawText(canvas, column.Text, contentRect, _textPaint, font, column.TextAlign, false, true);

            if (_sortColumnIndex == layout.ColumnIndex && _sortDirection != GridListSortDirection.None)
                DrawSortGlyph(canvas, cellRect, _sortDirection);

            if (AllowColumnResize && column.Resizable)
                DrawColumnResizeGrip(canvas, cellRect, isHovered, _hoveredHeaderResizeColumnIndex == layout.ColumnIndex || _resizingColumnIndex == layout.ColumnIndex);

            if (ShowGridLines)
            {
                canvas.DrawLine(cellRect.Right, cellRect.Top, cellRect.Right, cellRect.Bottom, _gridLinePaint);
            }
        }

        if (ShowGridLines)
        {
            canvas.DrawLine(bounds.Left, bounds.Bottom, bounds.Right, bounds.Bottom, _gridLinePaint);
        }
    }

    private void DrawSortGlyph(SKCanvas canvas, SKRect cellRect, GridListSortDirection direction)
    {
        var centerX = cellRect.Right - CellPadding - 8f;
        var centerY = cellRect.MidY;
        DrawChevronGlyph(
            canvas,
            new SKPoint(centerX, centerY),
            HeaderForeColor.WithAlpha(200),
            1.5f,
            4f,
            direction == GridListSortDirection.Ascending ? 180f : 0f);
    }

    private void DrawColumnResizeGrip(SKCanvas canvas, SKRect cellRect, bool emphasized, bool resizeHot)
    {
        _columnResizeGripPaint.Color = resizeHot ? ColorScheme.Primary : HeaderForeColor.WithAlpha(110);
        _columnResizeGripPaint.StrokeWidth = resizeHot ? 2f : 1f;

        var x = cellRect.Right - _columnResizeGripPaint.StrokeWidth / 2f;
        var y1 = cellRect.Top + 8f;
        var y2 = cellRect.Bottom - 8f;

        canvas.DrawLine(x, y1, x, y2, _columnResizeGripPaint);
    }

    private void DrawChevronGlyph(SKCanvas canvas, SKPoint center, SKColor color, float strokeWidth, float size, float rotationDegrees)
    {
        _chevronPaint.Color = color;
        _chevronPaint.StrokeWidth = strokeWidth;

        _chevronPath.Reset();
        _chevronPath.MoveTo(-size, -size * 0.5f);
        _chevronPath.LineTo(0f, size * 0.5f);
        _chevronPath.LineTo(size, -size * 0.5f);

        var saveCount = canvas.Save();
        canvas.Translate(center.X, center.Y);
        canvas.RotateDegrees(rotationDegrees);
        canvas.DrawPath(_chevronPath, _chevronPaint);
        canvas.RestoreToCount(saveCount);
    }

    private void DrawGroupHeader(SKCanvas canvas, SKRect bounds, string text, string groupKey, int groupIndex, SKFont font)
    {
        var expansion = GetGroupExpansionProgress(groupIndex);
        var isHovered = _hoveredGroupIndex >= 0 && _hoveredGroupIndex == groupIndex;
        _fillPaint.Color = isHovered ? GroupHeaderBackColor.Brightness(0.04f) : GroupHeaderBackColor;
        canvas.DrawRect(bounds, _fillPaint);

        var scale = ScaleFactor;
        _groupAccentPaint.Color = GroupHeaderBackColor.Brightness(0.12f).WithAlpha(180);
        _groupAccentBorderPaint.Color = ForeColor.WithAlpha(28);
        _groupAccentBorderPaint.StrokeWidth = Math.Max(1f, scale);
        var accentInsetY = Math.Max(3f, 5f * scale);
        var accentWidth = Math.Max(18f, 22f * scale);
        var accentRect = new SKRect(bounds.Left + CellPadding, bounds.Top + accentInsetY, bounds.Left + CellPadding + accentWidth, bounds.Bottom - accentInsetY);
        var accentRadius = Math.Min(accentRect.Width, accentRect.Height) * 0.5f;
        canvas.DrawRoundRect(accentRect, accentRadius, accentRadius, _groupAccentPaint);
        canvas.DrawRoundRect(accentRect, accentRadius, accentRadius, _groupAccentBorderPaint);

        var chevronCenter = new SKPoint(accentRect.MidX, accentRect.MidY);
        DrawChevronGlyph(canvas, chevronCenter, ForeColor.WithAlpha(220), Math.Max(1.2f, 1.8f * scale), Math.Max(2.8f, 3.6f * scale), -90f + expansion * 90f);

        var textRect = new SKRect(accentRect.Right + CellPadding, bounds.Top, bounds.Right - CellPadding, bounds.Bottom);
        _textPaint.Color = ForeColor;
        TextRenderer.DrawText(canvas, text, textRect, _textPaint, font, ContentAlignment.MiddleLeft, false, true);

        if (ShowGridLines)
        {
            canvas.DrawLine(bounds.Left, bounds.Bottom, bounds.Right, bounds.Bottom, _gridLinePaint);
        }
    }

    // ── Owner-draw helper painting (used by the Draw*EventArgs helper methods) ──

    internal void PaintDefaultRowBackground(SKCanvas canvas, SKRect bounds, int itemIndex, bool selected, bool hovered)
    {
        if (selected) { _fillPaint.Color = SelectionBackColor; canvas.DrawRect(bounds, _fillPaint); }
        else if (hovered) { _fillPaint.Color = HoverRowBackColor; canvas.DrawRect(bounds, _fillPaint); }
        else if (Items[itemIndex].BackColor != SKColor.Empty) { _fillPaint.Color = Items[itemIndex].BackColor; canvas.DrawRect(bounds, _fillPaint); }
        else if ((itemIndex & 1) == 1) { _fillPaint.Color = AlternatingRowBackColor; canvas.DrawRect(bounds, _fillPaint); }
    }

    internal void PaintSolidBackground(SKCanvas canvas, SKRect bounds, SKColor color)
    {
        _fillPaint.Color = color;
        canvas.DrawRect(bounds, _fillPaint);
    }

    internal void PaintDefaultCellText(SKCanvas canvas, SKRect cellBounds, GridListItem item, GridListCell? cell,
        GridListColumn column, SKFont font, ContentAlignment? alignment = null)
    {
        var text = cell?.Text;
        if (string.IsNullOrEmpty(text) && cell?.Value != null)
            text = cell.Value.ToString();
        if (string.IsNullOrEmpty(text))
            return;

        var contentRect = new SKRect(cellBounds.Left + CellPadding, cellBounds.Top, cellBounds.Right - CellPadding, cellBounds.Bottom);
        var foreColor = cell != null && cell.ForeColor != SKColor.Empty ? cell.ForeColor
            : item.ForeColor != SKColor.Empty ? item.ForeColor
            : column.ForeColor != SKColor.Empty ? column.ForeColor
            : (item.Enabled ? ForeColor : ForeColor.WithAlpha(140));
        _textPaint.Color = foreColor;
        TextRenderer.DrawText(canvas, text, contentRect, _textPaint, font, alignment ?? column.CellTextAlign, true, false);
    }

    internal void PaintDefaultRowText(SKCanvas canvas, SKRect rowBounds, int itemIndex, SKFont font)
    {
        var item = Items[itemIndex];
        for (var i = 0; i < _columnLayouts.Count; i++)
        {
            var layout = _columnLayouts[i];
            var cellRect = new SKRect(
                rowBounds.Left + layout.X - _horizontalOffset, rowBounds.Top,
                rowBounds.Left + layout.X + layout.Width - _horizontalOffset, rowBounds.Bottom);
            if (cellRect.Right < rowBounds.Left || cellRect.Left > rowBounds.Right)
                continue;

            PaintDefaultCellText(canvas, cellRect, item, GetCell(itemIndex, layout.ColumnIndex, createMissing: false), Columns[layout.ColumnIndex], font);
        }
    }

    internal void PaintFocusRectangle(SKCanvas canvas, SKRect bounds)
    {
        using var paint = new SKPaint
        {
            IsAntialias = true,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 1f,
            Color = ColorScheme.Primary.WithAlpha(180)
        };
        using var dash = SKPathEffect.CreateDash(new[] { 3f, 2f }, 0f);
        paint.PathEffect = dash;
        var r = new SKRect(bounds.Left + 1.5f, bounds.Top + 1.5f, bounds.Right - 1.5f, bounds.Bottom - 1.5f);
        canvas.DrawRect(r, paint);
    }

    internal void PaintDefaultHeaderBackground(SKCanvas canvas, SKRect bounds, bool hovered)
    {
        _fillPaint.Color = hovered ? HeaderBackColor.Brightness(0.05f) : HeaderBackColor;
        canvas.DrawRect(bounds, _fillPaint);
    }

    internal void PaintDefaultHeaderText(SKCanvas canvas, SKRect bounds, GridListColumn column, SKFont font)
    {
        var contentRect = new SKRect(bounds.Left + CellPadding, bounds.Top, bounds.Right - CellPadding, bounds.Bottom);
        _textPaint.Color = ForeColor.WithAlpha(210);
        TextRenderer.DrawText(canvas, column.Text ?? string.Empty, contentRect, _textPaint, font, column.TextAlign, true, false);
    }

    private void DrawItemRow(SKCanvas canvas, SKRect bounds, int itemIndex, SKFont font)
    {
        if (bounds.Height <= 0.5f)
            return;

        var item = Items[itemIndex];
        var isSelected = _selectedIndices.Contains(itemIndex);
        var isHovered = _hoveredItemIndex == itemIndex;
        var baseRowHeight = GetResolvedRowHeight(itemIndex);
        var revealProgress = baseRowHeight <= 0.001f ? 1f : Math.Clamp(bounds.Height / baseRowHeight, 0f, 1f);
        var saveCount = canvas.Save();
        canvas.ClipRect(bounds);

        if (OwnerDraw && DrawItem != null)
        {
            var ownerArgs = new GridListDrawItemEventArgs(this, canvas, item, itemIndex, bounds, font, isSelected, isHovered);
            DrawItem.Invoke(this, ownerArgs);
            if (!ownerArgs.DrawDefault)
            {
                canvas.RestoreToCount(saveCount);
                return;
            }
        }

        if (isSelected)
        {
            _fillPaint.Color = WithOpacity(SelectionBackColor, revealProgress);
            canvas.DrawRect(bounds, _fillPaint);
        }
        else if (isHovered)
        {
            _fillPaint.Color = WithOpacity(HoverRowBackColor, revealProgress);
            canvas.DrawRect(bounds, _fillPaint);
        }
        else if (item.BackColor != SKColor.Empty)
        {
            _fillPaint.Color = WithOpacity(item.BackColor, revealProgress);
            canvas.DrawRect(bounds, _fillPaint);
        }
        else if ((itemIndex & 1) == 1)
        {
            _fillPaint.Color = WithOpacity(AlternatingRowBackColor, revealProgress);
            canvas.DrawRect(bounds, _fillPaint);
        }

        for (var i = 0; i < _columnLayouts.Count; i++)
        {
            var layout = _columnLayouts[i];
            var column = Columns[layout.ColumnIndex];
            var cellRect = new SKRect(bounds.Left + layout.X - _horizontalOffset, bounds.Top, bounds.Left + layout.X + layout.Width - _horizontalOffset, bounds.Bottom);
            if (cellRect.Right < bounds.Left || cellRect.Left > bounds.Right)
                continue;

            var contentRect = new SKRect(cellRect.Left + CellPadding, cellRect.Top, cellRect.Right - CellPadding, cellRect.Bottom);
            var cell = GetCell(itemIndex, layout.ColumnIndex, createMissing: false);

            if (OwnerDraw && DrawSubItem != null)
            {
                var subArgs = new GridListDrawSubItemEventArgs(this, canvas, item, itemIndex, cell, column,
                    layout.ColumnIndex, cellRect, font, isSelected, isHovered);
                DrawSubItem.Invoke(this, subArgs);
                if (!subArgs.DrawDefault)
                {
                    if (ShowGridLines)
                        canvas.DrawLine(cellRect.Right, cellRect.Top, cellRect.Right, cellRect.Bottom, _gridLinePaint);
                    continue;
                }
            }

            // Per-cell / per-column background (selection & hover keep the row-wide treatment).
            if (!isSelected && !isHovered)
            {
                var cellBack = cell != null && cell.BackColor != SKColor.Empty ? cell.BackColor : column.BackColor;
                if (cellBack != SKColor.Empty)
                {
                    _fillPaint.Color = WithOpacity(cellBack, revealProgress);
                    canvas.DrawRect(cellRect, _fillPaint);
                }
            }

            if (ShouldShowCheckBox(column, layout.ColumnIndex))
            {
                var checkboxRect = GetCheckBoxRect(contentRect);
                DrawCheckBox(canvas, checkboxRect, cell?.CheckState ?? CheckState.Unchecked, isSelected);
                contentRect.Left = checkboxRect.Right + CellPadding * 0.75f;
            }

            var icon = column.ShowIcons ? cell?.Icon ?? item.Icon : null;
            if (icon != null)
            {
                var iconRect = new SKRect(contentRect.Left, contentRect.MidY - IconSize / 2f, contentRect.Left + IconSize, contentRect.MidY + IconSize / 2f);
                canvas.DrawImage(icon, iconRect);
                contentRect.Left = iconRect.Right + CellPadding * 0.75f;
            }

            var text = cell?.Text;
            if (string.IsNullOrEmpty(text) && cell?.Value != null)
                text = cell.Value.ToString();

            if (!string.IsNullOrEmpty(text))
            {
                var foreColor = cell != null && cell.ForeColor != SKColor.Empty ? cell.ForeColor
                    : item.ForeColor != SKColor.Empty ? item.ForeColor
                    : column.ForeColor != SKColor.Empty ? column.ForeColor
                    : (item.Enabled ? ForeColor : ForeColor.WithAlpha(140));
                _textPaint.Color = WithOpacity(foreColor, revealProgress);
                TextRenderer.DrawText(canvas, text, contentRect, _textPaint, font, column.CellTextAlign, true, false);
            }

            if (ShowGridLines)
            {
                canvas.DrawLine(cellRect.Right, cellRect.Top, cellRect.Right, cellRect.Bottom, _gridLinePaint);
            }
        }

        if (AllowRowResize && (_hoveredRowResizeIndex == itemIndex || _isResizingRow && _resizingRowIndex == itemIndex))
        {
            _rowResizePaint.Color = ColorScheme.Primary.WithAlpha(160);
            var y = bounds.Bottom - 1f;
            canvas.DrawLine(bounds.Left + 12f, y, bounds.Right - 12f, y, _rowResizePaint);
        }

        if (ShowGridLines)
            canvas.DrawLine(bounds.Left, bounds.Bottom, bounds.Right, bounds.Bottom, _gridLinePaint);

        canvas.RestoreToCount(saveCount);
    }

    private static SKColor WithOpacity(SKColor color, float opacity)
    {
        var alpha = (byte)Math.Clamp(Math.Round(color.Alpha * opacity), 0d, 255d);
        return color.WithAlpha(alpha);
    }

    private static SKRect GetCheckBoxRect(SKRect contentRect)
    {
        return new SKRect(contentRect.Left, contentRect.MidY - CheckBoxSize / 2f, contentRect.Left + CheckBoxSize, contentRect.MidY + CheckBoxSize / 2f);
    }

    private void DrawCheckBox(SKCanvas canvas, SKRect rect, CheckState state, bool isSelected)
    {
        _checkBoxBackPaint.Color = state == CheckState.Unchecked ? ColorScheme.Surface : ColorScheme.Primary.WithAlpha(isSelected ? (byte)220 : (byte)180);
        _checkBoxBorderPaint.Color = state == CheckState.Unchecked ? ColorScheme.BorderColor : ColorScheme.Primary;

        canvas.DrawRoundRect(rect, 4f, 4f, _checkBoxBackPaint);
        canvas.DrawRoundRect(rect, 4f, 4f, _checkBoxBorderPaint);

        if (state == CheckState.Unchecked)
            return;

        if (state == CheckState.Checked)
        {
            _checkBoxCheckPath.Reset();
            _checkBoxCheckPath.MoveTo(rect.Left + 3, rect.MidY);
            _checkBoxCheckPath.LineTo(rect.Left + 7, rect.Bottom - 4);
            _checkBoxCheckPath.LineTo(rect.Right - 3, rect.Top + 4);
            canvas.DrawPath(_checkBoxCheckPath, _checkBoxCheckPaint);
        }
        else
        {
            canvas.DrawLine(rect.Left + 3, rect.MidY, rect.Right - 3, rect.MidY, _checkBoxCheckPaint);
        }
    }

    private bool ShouldShowCheckBox(GridListColumn column, int columnIndex)
    {
        return column.ShowCheckBox || (CheckBoxes && FindFirstVisibleColumnIndex() == columnIndex);
    }

    private int FindFirstVisibleColumnIndex()
    {
        for (var i = 0; i < Columns.Count; i++)
        {
            if (Columns[i].Visible)
                return i;
        }

        return -1;
    }

    private int FindFirstCheckBoxColumn()
    {
        for (var i = 0; i < Columns.Count; i++)
        {
            if (Columns[i].Visible && ShouldShowCheckBox(Columns[i], i))
                return i;
        }

        return -1;
    }

    private GridListColumn? GetColumn(int columnIndex)
    {
        return columnIndex >= 0 && columnIndex < Columns.Count ? Columns[columnIndex] : null;
    }

    private GridListCell? GetCell(int itemIndex, int columnIndex, bool createMissing)
    {
        if (itemIndex < 0 || itemIndex >= Items.Count || columnIndex < 0 || columnIndex >= Columns.Count)
            return null;

        var item = Items[itemIndex];
        while (createMissing && item.Cells.Count <= columnIndex)
            item.Cells.Add(new GridListCell());

        return columnIndex < item.Cells.Count ? item.Cells[columnIndex] : null;
    }

    /// <summary>
    /// Determines what is located at the given client point — the row item, its cell/sub-item, a
    /// column header, a group header, a resize grip, or a cell check box. Mirrors
    /// System.Windows.Forms.ListView.HitTest. Never returns null; check
    /// <see cref="GridListHitTestInfo.Region"/> / <see cref="GridListHitTestInfo.Item"/>.
    /// </summary>
    public GridListHitTestInfo HitTest(SKPoint point)
    {
        var hit = HitTestCore(point);

        var item = hit.ItemIndex >= 0 && hit.ItemIndex < Items.Count ? Items[hit.ItemIndex] : null;
        var column = hit.ColumnIndex >= 0 && hit.ColumnIndex < Columns.Count ? Columns[hit.ColumnIndex] : null;
        var cell = item != null && hit.ColumnIndex >= 0 && hit.ColumnIndex < item.Cells.Count
            ? item.Cells[hit.ColumnIndex]
            : null;

        var region = hit.Kind switch
        {
            HitKind.Header => GridListHitTestRegion.ColumnHeader,
            HitKind.HeaderResize => GridListHitTestRegion.ColumnHeaderResize,
            HitKind.RowResize => GridListHitTestRegion.RowResize,
            HitKind.GroupHeader => GridListHitTestRegion.GroupHeader,
            HitKind.ItemCell => !hit.CheckBoxRect.IsEmpty && hit.CheckBoxRect.Contains(point)
                ? GridListHitTestRegion.CheckBox
                : GridListHitTestRegion.Cell,
            _ => GridListHitTestRegion.None
        };

        return new GridListHitTestInfo(region, hit.ItemIndex, hit.ColumnIndex, item, cell, column, hit.GroupKey, hit.GroupText);
    }

    /// <summary>Determines what is located at the given client coordinates. See <see cref="HitTest(SKPoint)"/>.</summary>
    public GridListHitTestInfo HitTest(float x, float y) => HitTest(new SKPoint(x, y));

    /// <summary>
    /// Returns the client-area bounds (in this control's local coordinates) of the row at
    /// <paramref name="itemIndex"/>, or <see cref="SKRect.Empty"/> if it is not currently laid out.
    /// Combine with <see cref="ElementBase.PointToScreen(SKPoint)"/> to place popups next to a row.
    /// </summary>
    public SKRect GetItemBounds(int itemIndex)
    {
        if (itemIndex < 0 || itemIndex >= Items.Count)
            return SKRect.Empty;

        EnsureLayoutState();
        if (!TryGetItemEntry(itemIndex, out var entry))
            return SKRect.Empty;

        var bodyViewport = GetBodyViewportRect(GetOuterViewport());
        var r = entry.Bounds;
        r.Offset(bodyViewport.Left - _horizontalOffset, bodyViewport.Top - _verticalOffset);
        return r;
    }

    /// <summary>
    /// Returns the client-area bounds of the cell at (<paramref name="itemIndex"/>,
    /// <paramref name="columnIndex"/>), or <see cref="SKRect.Empty"/> if unavailable.
    /// </summary>
    public SKRect GetCellBounds(int itemIndex, int columnIndex)
    {
        if (columnIndex < 0 || columnIndex >= Columns.Count)
            return SKRect.Empty;

        var row = GetItemBounds(itemIndex);
        if (row.IsEmpty)
            return SKRect.Empty;

        for (var i = 0; i < _columnLayouts.Count; i++)
        {
            var layout = _columnLayouts[i];
            if (layout.ColumnIndex == columnIndex)
                return new SKRect(row.Left + layout.X, row.Top, row.Left + layout.X + layout.Width, row.Bottom);
        }

        return SKRect.Empty;
    }

    /// <summary>
    /// Moves the row at <paramref name="fromIndex"/> to <paramref name="toIndex"/>, keeping the
    /// selection on the same logical rows. Returns false when either index is out of range.
    /// </summary>
    public bool MoveItem(int fromIndex, int toIndex)
    {
        if (fromIndex < 0 || fromIndex >= Items.Count ||
            toIndex < 0 || toIndex >= Items.Count || fromIndex == toIndex)
            return false;

        var item = Items[fromIndex];
        Items.RemoveAt(fromIndex);
        Items.Insert(toIndex, item);

        // Remap the selection so the same rows stay selected after the shift.
        if (_selectedIndices.Count > 0)
        {
            var remapped = new HashSet<int>();
            foreach (var index in _selectedIndices)
                remapped.Add(RemapIndexAfterMove(index, fromIndex, toIndex));

            _selectedIndices.Clear();
            foreach (var index in remapped)
                _selectedIndices.Add(index);
        }

        Invalidate();
        return true;
    }

    /// <summary>Moves the row one position up. Returns false when it is already first.</summary>
    public bool MoveItemUp(int index) => MoveItem(index, index - 1);

    /// <summary>Moves the row one position down. Returns false when it is already last.</summary>
    public bool MoveItemDown(int index) => MoveItem(index, index + 1);

    private static int RemapIndexAfterMove(int index, int fromIndex, int toIndex)
    {
        if (index == fromIndex)
            return toIndex;
        if (fromIndex < toIndex && index > fromIndex && index <= toIndex)
            return index - 1;
        if (toIndex < fromIndex && index >= toIndex && index < fromIndex)
            return index + 1;
        return index;
    }

    private bool TryGetItemEntry(int itemIndex, out LayoutEntry entry)
    {
        for (var i = 0; i < _layoutEntries.Count; i++)
        {
            if (_layoutEntries[i].Kind == EntryKind.Item && _layoutEntries[i].ItemIndex == itemIndex)
            {
                entry = _layoutEntries[i];
                return true;
            }
        }

        entry = default;
        return false;
    }

    // ── Coordinate conversion ──
    // The SKPoint PointToScreen/PointToClient are inherited from ElementBase; these overloads add
    // float and WinForms System.Drawing.Point variants so migrated code such as
    // `grid.PointToScreen(new Point(x, y))` compiles and the conversion is discoverable on GridList.

    /// <summary>Converts a client point (in this control's coordinates) to screen coordinates.</summary>
    public SKPoint PointToScreen(float x, float y) => PointToScreen(new SKPoint(x, y));

    /// <summary>Converts a client point to screen coordinates (WinForms <see cref="System.Drawing.Point"/> overload).</summary>
    public System.Drawing.Point PointToScreen(System.Drawing.Point clientPoint)
    {
        var screen = PointToScreen(new SKPoint(clientPoint.X, clientPoint.Y));
        return new System.Drawing.Point((int)MathF.Round(screen.X), (int)MathF.Round(screen.Y));
    }

    /// <summary>Converts a screen point to a client point (in this control's coordinates).</summary>
    public SKPoint PointToClient(float x, float y) => PointToClient(new SKPoint(x, y));

    /// <summary>Converts a screen point to a client point (WinForms <see cref="System.Drawing.Point"/> overload).</summary>
    public System.Drawing.Point PointToClient(System.Drawing.Point screenPoint)
    {
        var client = PointToClient(new SKPoint(screenPoint.X, screenPoint.Y));
        return new System.Drawing.Point((int)MathF.Round(client.X), (int)MathF.Round(client.Y));
    }

    private HitInfo HitTestCore(SKPoint location)
    {
        EnsureLayoutState();

        var outer = GetOuterViewport();
        var stickyHeaderRect = HeaderVisible && StickyHeader ? GetStickyHeaderRect(outer) : SKRect.Empty;
        if (!stickyHeaderRect.IsEmpty && stickyHeaderRect.Contains(location))
            return HitTestHeader(location, stickyHeaderRect, horizontalOffset: _horizontalOffset);

        var bodyViewport = GetBodyViewportRect(outer);
        if (!bodyViewport.Contains(location))
            return HitInfo.None;

        var contentX = location.X - bodyViewport.Left + _horizontalOffset;
        var contentY = location.Y - bodyViewport.Top + _verticalOffset;

        var entryIndex = FindLayoutEntryIndexAt(contentY);
        if (entryIndex < 0)
            return HitInfo.None;

        var entry = _layoutEntries[entryIndex];
        if (entry.Kind == EntryKind.Header)
            return HitTestHeader(location, new SKRect(bodyViewport.Left, bodyViewport.Top + entry.Bounds.Top - _verticalOffset, bodyViewport.Left + _bodyViewportWidth, bodyViewport.Top + entry.Bounds.Bottom - _verticalOffset), _horizontalOffset);

        if (entry.Kind == EntryKind.GroupHeader)
            return HitInfo.ForGroup(entry.GroupKey, entry.GroupText, entry.GroupIndex);

        if (entry.Kind == EntryKind.Item)
            return HitTestItemCell(location, contentX, contentY, entry);

        return HitInfo.None;
    }

    private int FindLayoutEntryIndexAt(float contentY)
    {
        // Layout entries are appended in vertical order during layout generation, so a first-match binary search is safe here.
        var low = 0;
        var high = _layoutEntries.Count - 1;

        while (low < high)
        {
            var mid = low + ((high - low) >> 1);
            if (_layoutEntries[mid].Bounds.Bottom < contentY)
                low = mid + 1;
            else
                high = mid;
        }

        if (low < 0 || low >= _layoutEntries.Count)
            return -1;

        var candidate = _layoutEntries[low];
        return contentY >= candidate.Bounds.Top && contentY <= candidate.Bounds.Bottom ? low : -1;
    }

    private HitInfo HitTestHeader(SKPoint location, SKRect headerRect, float horizontalOffset)
    {
        if (AllowColumnResize)
        {
            var hitAreaWidth = Math.Max(ResizeGripWidth + 8f, ResizeHitWidth * ScaleFactor);
            var halfHitArea = hitAreaWidth * 0.5f;

            for (var i = 0; i < _columnLayouts.Count; i++)
            {
                var layout = _columnLayouts[i];
                if (!Columns[layout.ColumnIndex].Resizable)
                    continue;

                var cellRect = new SKRect(headerRect.Left + layout.X - horizontalOffset, headerRect.Top, headerRect.Left + layout.X + layout.Width - horizontalOffset, headerRect.Bottom);
                var resizeRect = new SKRect(
                    Math.Max(headerRect.Left, cellRect.Right - halfHitArea),
                    cellRect.Top,
                    Math.Min(headerRect.Right, cellRect.Right + halfHitArea),
                    cellRect.Bottom);

                if (resizeRect.Contains(location))
                    return HitInfo.ForHeaderResize(layout.ColumnIndex);
            }
        }

        for (var i = 0; i < _columnLayouts.Count; i++)
        {
            var layout = _columnLayouts[i];
            var cellRect = new SKRect(headerRect.Left + layout.X - horizontalOffset, headerRect.Top, headerRect.Left + layout.X + layout.Width - horizontalOffset, headerRect.Bottom);
            if (!cellRect.Contains(location))
                continue;

            return HitInfo.ForHeader(layout.ColumnIndex);
        }

        return HitInfo.None;
    }

    private HitInfo HitTestItemCell(SKPoint location, float contentX, float contentY, LayoutEntry entry)
    {
        if (AllowRowResize && entry.Bounds.Height >= GetResolvedRowHeight(entry.ItemIndex) * 0.65f && Math.Abs(contentY - entry.Bounds.Bottom) <= RowResizeGripHeight)
            return HitInfo.ForRowResize(entry.ItemIndex);

        for (var i = 0; i < _columnLayouts.Count; i++)
        {
            var layout = _columnLayouts[i];
            if (contentX < layout.X || contentX > layout.X + layout.Width)
                continue;

            var checkRect = SKRect.Empty;
            var column = Columns[layout.ColumnIndex];
            if (ShouldShowCheckBox(column, layout.ColumnIndex))
            {
                var outer = GetOuterViewport();
                var bodyViewport = GetBodyViewportRect(outer);
                var drawRect = entry.Bounds;
                drawRect.Offset(bodyViewport.Left - _horizontalOffset, bodyViewport.Top - _verticalOffset);
                var cellRect = new SKRect(drawRect.Left + layout.X, drawRect.Top, drawRect.Left + layout.X + layout.Width, drawRect.Bottom);
                var contentRect = new SKRect(cellRect.Left + CellPadding, cellRect.Top, cellRect.Right - CellPadding, cellRect.Bottom);
                checkRect = GetCheckBoxRect(contentRect);
            }

            return HitInfo.ForItem(entry.ItemIndex, layout.ColumnIndex, checkRect);
        }

        return HitInfo.None;
    }

    private readonly record struct ColumnLayout(int ColumnIndex, float X, float Width);

    private enum EntryKind
    {
        Header,
        GroupHeader,
        Item
    }

    private readonly record struct LayoutEntry(EntryKind Kind, SKRect Bounds, int ItemIndex, string? GroupKey, string? GroupText, int GroupIndex)
    {
        public static LayoutEntry Header(SKRect bounds) => new(EntryKind.Header, bounds, -1, null, null, -1);
        public static LayoutEntry GroupHeader(SKRect bounds, string? groupKey, string? groupText, int groupIndex) => new(EntryKind.GroupHeader, bounds, -1, groupKey, groupText, groupIndex);
        public static LayoutEntry Item(SKRect bounds, int itemIndex) => new(EntryKind.Item, bounds, itemIndex, null, null, -1);
    }

    private enum HitKind
    {
        None,
        Header,
        HeaderResize,
        RowResize,
        GroupHeader,
        ItemCell
    }

    private readonly record struct HitInfo(HitKind Kind, int ItemIndex, int ColumnIndex, string? GroupKey, string? GroupText, int GroupIndex, SKRect CheckBoxRect)
    {
        public static HitInfo None => new(HitKind.None, -1, -1, null, null, -1, SKRect.Empty);
        public static HitInfo ForHeader(int columnIndex) => new(HitKind.Header, -1, columnIndex, null, null, -1, SKRect.Empty);
        public static HitInfo ForHeaderResize(int columnIndex) => new(HitKind.HeaderResize, -1, columnIndex, null, null, -1, SKRect.Empty);
        public static HitInfo ForRowResize(int itemIndex) => new(HitKind.RowResize, itemIndex, -1, null, null, -1, SKRect.Empty);
        public static HitInfo ForGroup(string? groupKey, string? groupText, int groupIndex) => new(HitKind.GroupHeader, -1, -1, groupKey, groupText, groupIndex, SKRect.Empty);
        public static HitInfo ForItem(int itemIndex, int columnIndex, SKRect checkBoxRect) => new(HitKind.ItemCell, itemIndex, columnIndex, null, null, -1, checkBoxRect);
    }
}
