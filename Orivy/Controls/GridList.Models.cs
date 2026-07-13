using Orivy.Collections;
using SkiaSharp;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;

namespace Orivy.Controls;

/// <summary>
/// Identifies the part of a <see cref="GridList"/> returned by <see cref="GridList.HitTest(SKPoint)"/>.
/// Mirrors the intent of System.Windows.Forms.ListViewHitTestLocations.
/// </summary>
public enum GridListHitTestRegion
{
    /// <summary>The point is not over any interactive element.</summary>
    None,

    /// <summary>The point is over a cell (sub-item) of a row.</summary>
    Cell,

    /// <summary>The point is over a cell's check box.</summary>
    CheckBox,

    /// <summary>The point is over a column header.</summary>
    ColumnHeader,

    /// <summary>The point is over the resize grip between two column headers.</summary>
    ColumnHeaderResize,

    /// <summary>The point is over a group header row.</summary>
    GroupHeader,

    /// <summary>The point is over the resize grip at the bottom edge of a row.</summary>
    RowResize
}

/// <summary>
/// Describes what is located at a point in a <see cref="GridList"/>. Returned by
/// <see cref="GridList.HitTest(SKPoint)"/>. Analogous to System.Windows.Forms.ListViewHitTestInfo.
/// </summary>
public sealed class GridListHitTestInfo
{
    internal GridListHitTestInfo(
        GridListHitTestRegion region,
        int itemIndex,
        int columnIndex,
        GridListItem? item,
        GridListCell? subItem,
        GridListColumn? column,
        string? groupKey,
        string? groupText)
    {
        Region = region;
        ItemIndex = itemIndex;
        ColumnIndex = columnIndex;
        Item = item;
        SubItem = subItem;
        Column = column;
        GroupKey = groupKey;
        GroupText = groupText;
    }

    /// <summary>The kind of element under the point.</summary>
    public GridListHitTestRegion Region { get; }

    /// <summary>The row item under the point, or null when the point is not over an item row.</summary>
    public GridListItem? Item { get; }

    /// <summary>The cell (sub-item) under the point, or null when not over a cell.</summary>
    public GridListCell? SubItem { get; }

    /// <summary>The column under the point (for cells and headers), or null.</summary>
    public GridListColumn? Column { get; }

    /// <summary>The zero-based row index, or -1 when not over an item row.</summary>
    public int ItemIndex { get; }

    /// <summary>The zero-based column index, or -1 when not over a column/cell.</summary>
    public int ColumnIndex { get; }

    /// <summary>The group key when the point is over a group header; otherwise null.</summary>
    public string? GroupKey { get; }

    /// <summary>The group display text when the point is over a group header; otherwise null.</summary>
    public string? GroupText { get; }

    /// <summary>True when the point is over an item row or one of its cells.</summary>
    public bool IsOverItem => Item != null;
}

public sealed class GridListColumn
{
    private ContentAlignment _headerTextAlign = ContentAlignment.MiddleLeft;
    private ContentAlignment _cellTextAlign = ContentAlignment.MiddleLeft;
    private float _fillWeight = 1f;
    private string _text = string.Empty;
    private float _maxWidth = 1200f;
    private float _minWidth = 56f;
    private string _name = string.Empty;
    private bool _resizable = true;
    private GridListColumnSizeMode _sizeMode;
    private bool _sortable = true;
    private bool _visible = true;
    private float _width = 160f;

    internal GridList? Owner { get; private set; }

    [DefaultValue("")]
    public string Name
    {
        get => _name;
        set
        {
            if (_name == value)
                return;

            _name = value ?? string.Empty;
            Owner?.OnColumnsChanged(layoutAffected: false);
        }
    }

    [DefaultValue("")]
    public string Text
    {
        get => _text;
        set
        {
            if (_text == value)
                return;

            _text = value ?? string.Empty;
            Owner?.OnColumnsChanged(layoutAffected: false);
        }
    }

    [DefaultValue(160f)]
    public float Width
    {
        get => _width;
        set
        {
            var clamped = Math.Clamp(value, MinWidth, MaxWidth);
            if (Math.Abs(_width - clamped) < 0.001f)
                return;

            _width = clamped;
            Owner?.OnColumnsChanged(layoutAffected: true);
        }
    }

    [DefaultValue(56f)]
    public float MinWidth
    {
        get => _minWidth;
        set
        {
            var clamped = Math.Max(24f, value);
            if (Math.Abs(_minWidth - clamped) < 0.001f)
                return;

            _minWidth = clamped;
            if (_maxWidth < _minWidth)
                _maxWidth = _minWidth;
            if (_width < _minWidth)
                _width = _minWidth;
            Owner?.OnColumnsChanged(layoutAffected: true);
        }
    }

    [DefaultValue(1200f)]
    public float MaxWidth
    {
        get => _maxWidth;
        set
        {
            var clamped = Math.Max(MinWidth, value);
            if (Math.Abs(_maxWidth - clamped) < 0.001f)
                return;

            _maxWidth = clamped;
            if (_width > _maxWidth)
                _width = _maxWidth;
            Owner?.OnColumnsChanged(layoutAffected: true);
        }
    }

    [DefaultValue(true)]
    public bool Visible
    {
        get => _visible;
        set
        {
            if (_visible == value)
                return;

            _visible = value;
            Owner?.OnColumnsChanged(layoutAffected: true);
        }
    }

    [DefaultValue(true)]
    public bool Sortable
    {
        get => _sortable;
        set
        {
            if (_sortable == value)
                return;

            _sortable = value;
            Owner?.OnColumnsChanged(layoutAffected: false);
        }
    }

    [DefaultValue(true)]
    public bool Resizable
    {
        get => _resizable;
        set
        {
            if (_resizable == value)
                return;

            _resizable = value;
            Owner?.OnColumnsChanged(layoutAffected: false);
        }
    }

    [DefaultValue(GridListColumnSizeMode.Fixed)]
    public GridListColumnSizeMode SizeMode
    {
        get => _sizeMode;
        set
        {
            if (_sizeMode == value)
                return;

            _sizeMode = value;
            Owner?.OnColumnsChanged(layoutAffected: true);
        }
    }

    [DefaultValue(1f)]
    public float FillWeight
    {
        get => _fillWeight;
        set
        {
            var clamped = Math.Max(0.01f, value);
            if (Math.Abs(_fillWeight - clamped) < 0.001f)
                return;

            _fillWeight = clamped;
            Owner?.OnColumnsChanged(layoutAffected: true);
        }
    }

    [DefaultValue(false)]
    public bool ShowCheckBox { get; set; }

    [DefaultValue(true)]
    public bool ShowIcons { get; set; } = true;

    [DefaultValue(ContentAlignment.MiddleLeft)]
    public ContentAlignment TextAlign
    {
        get => _headerTextAlign;
        set
        {
            if (_headerTextAlign == value)
                return;

            _headerTextAlign = value;
            Owner?.OnColumnsChanged(layoutAffected: false);
        }
    }

    [DefaultValue(ContentAlignment.MiddleLeft)]
    public ContentAlignment CellTextAlign
    {
        get => _cellTextAlign;
        set
        {
            if (_cellTextAlign == value)
                return;

            _cellTextAlign = value;
            Owner?.OnColumnsChanged(layoutAffected: false);
        }
    }

    public SKImage? HeaderIcon { get; set; }

    internal void AttachOwner(GridList owner)
    {
        Owner = owner;
    }
}

public sealed class GridListCell
{
    private CheckState _checkState;
    private string _text = string.Empty;
    private object? _value;

    internal GridListItem? ParentItem { get; private set; }

    public GridListCell()
    {
        
    }
    
    public GridListCell(string text)
    {
        Text = text;
    }

    public object? Value
    {
        get => _value;
        set
        {
            if (ReferenceEquals(_value, value))
                return;

            _value = value;
            ParentItem?.NotifyCellChanged(layoutAffected: false);
        }
    }

    [DefaultValue("")]
    public string Text
    {
        get => _text;
        set
        {
            if (_text == value)
                return;

            _text = value ?? string.Empty;
            ParentItem?.NotifyCellChanged(layoutAffected: false);
        }
    }

    [DefaultValue(typeof(CheckState), "Unchecked")]
    public CheckState CheckState
    {
        get => _checkState;
        set
        {
            if (_checkState == value)
                return;

            _checkState = value;
            ParentItem?.NotifyCellChanged(layoutAffected: false);
        }
    }

    [DefaultValue(false)]
    public bool Checked
    {
        get => _checkState == CheckState.Checked;
        set => CheckState = value ? CheckState.Checked : CheckState.Unchecked;
    }

    public SKImage? Icon { get; set; }

    public SKColor ForeColor { get; set; } = SKColor.Empty;

    internal void AttachParent(GridListItem parent)
    {
        ParentItem = parent;
    }
}

public sealed class GridListItem
{
    private string _name = string.Empty;
    private bool _enabled = true;
    private string _groupKey = string.Empty;
    private string _groupText = string.Empty;
    private float _height;
    private bool _visible = true;

    public GridListItem()
    {
        Cells = new GridListCellCollection(this);
    }

    public GridListItem(string text)
    {
        Cells = new GridListCellCollection(this)
        {
            new GridListCell { Text = text }
        };
    }

    /// <summary>Creates an item with one text cell per string (WinForms ListViewItem(string[]) ergonomics).</summary>
    public GridListItem(string?[] cells)
    {
        Cells = new GridListCellCollection(this);
        if (cells != null)
            Cells.AddRange(cells);
    }

    internal GridList? Owner { get; private set; }

    public GridListCellCollection Cells { get; }

    /// <summary>
    /// The text of the first cell (WinForms ListViewItem.Text equivalent). Setting it creates the
    /// first cell when the item is still empty.
    /// </summary>
    public string Text
    {
        get => Cells.Count > 0 ? Cells[0].Text : string.Empty;
        set
        {
            if (Cells.Count == 0)
                Cells.Add(new GridListCell { Text = value ?? string.Empty });
            else
                Cells[0].Text = value ?? string.Empty;
        }
    }

    public object? Tag { get; set; }

    public string Name
    {
        get => _name;
        set
        {
            if (_name == value)
                return;
            _name = value ?? string.Empty;
            Owner?.OnItemsChanged(layoutAffected: false);
        }
    }

    [DefaultValue(true)]
    public bool Visible
    {
        get => _visible;
        set
        {
            if (_visible == value)
                return;

            _visible = value;
            Owner?.OnItemsChanged(layoutAffected: true);
        }
    }

    [DefaultValue(true)]
    public bool Enabled
    {
        get => _enabled;
        set
        {
            if (_enabled == value)
                return;

            _enabled = value;
            Owner?.OnItemsChanged(layoutAffected: false);
        }
    }

    [DefaultValue("")]
    public string GroupKey
    {
        get => _groupKey;
        set
        {
            if (_groupKey == value)
                return;

            _groupKey = value ?? string.Empty;
            Owner?.OnItemsChanged(layoutAffected: true);
        }
    }

    [DefaultValue("")]
    public string GroupText
    {
        get => _groupText;
        set
        {
            if (_groupText == value)
                return;

            _groupText = value ?? string.Empty;
            Owner?.OnItemsChanged(layoutAffected: true);
        }
    }

    [DefaultValue(0f)]
    public float Height
    {
        get => _height;
        set
        {
            var clamped = Math.Max(0f, value);
            if (Math.Abs(_height - clamped) < 0.001f)
                return;

            _height = clamped;
            Owner?.OnItemsChanged(layoutAffected: true);
        }
    }

    public SKImage? Icon { get; set; }

    internal void AttachOwner(GridList owner)
    {
        Owner = owner;
        for (var i = 0; i < Cells.Count; i++)
            Cells[i].AttachParent(this);
    }

    internal void NotifyCellChanged(bool layoutAffected)
    {
        Owner?.OnItemsChanged(layoutAffected);
    }

    public GridListItem Clone() => (GridListItem)this.MemberwiseClone();
}