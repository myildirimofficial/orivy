using Orivy.Helpers;
using SkiaSharp;
using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;

namespace Orivy.Controls;

/// <summary>
/// A scrollable list of items with single or multiple selection. API mirrors
/// System.Windows.Forms.ListBox (Items, SelectedIndex/Item, SelectedIndices/Items,
/// SelectionMode, SelectedIndexChanged, etc.) so migrated WinForms code ports over.
/// </summary>
public class ListBox : ElementBase
{
    public const int NoMatches = -1;

    private readonly ObjectCollection _items;
    private readonly List<int> _selectedIndices = new();
    private readonly ScrollBar _vScroll;

    private SelectionMode _selectionMode = SelectionMode.One;
    private DrawMode _drawMode = DrawMode.Normal;
    private string _displayMember = string.Empty;
    private int _hoverIndex = -1;
    private int _anchorIndex = -1;
    private int _itemHeight; // 0 => auto from font
    private int _updateSuspendCount;
    private bool _pendingSelectionChanged;

    // Check-box mode (folds the old CheckedListBox into ListBox as a mode).
    private readonly List<CheckState> _checkStates = new();
    private bool _checkBoxes;
    private bool _checkOnClick;

    private SKFont? _renderFont;
    private SKFont? _renderFontSource;
    private int _renderFontDpi;
    private SKPaint? _itemTextPaint;
    private SKPaint? _rowPaint;

    /// <summary>Occurs when the <see cref="SelectedIndex"/> changes.</summary>
    public event EventHandler? SelectedIndexChanged;

    /// <summary>Occurs when the value of the selected item changes.</summary>
    public event EventHandler? SelectedValueChanged;

    /// <summary>
    /// Occurs for each visible item when <see cref="DrawMode"/> is an owner-draw mode, letting you
    /// paint the item yourself.
    /// </summary>
    public event EventHandler<DrawItemEventArgs>? DrawItem;

    /// <summary>Occurs just before an item's check state changes (cancelable via NewValue).</summary>
    public event EventHandler<ItemCheckEventArgs>? ItemCheck;

    /// <summary>Occurs after an item's check state has changed.</summary>
    public event EventHandler? ItemChecked;

    // Report that we consume the wheel so the router actually forwards it to OnMouseWheel even
    // though we manage our own (non-AutoScroll) scrollbar.
    protected override bool HandlesMouseWheelInput => true;

    public ListBox()
    {
        _items = new ObjectCollection(this);

        CanSelect = true;
        TabStop = true;
        Radius = new Radius(10);
        Border = new Thickness(1);
        Padding = new Thickness(8);
        Size = new SKSize(200, 220);
        MinimumSize = new SKSize(60, 40);

        _vScroll = new ScrollBar
        {
            Orientation = Orientation.Vertical,
            Dock = DockStyle.None,
            Visible = false,
            AutoHide = true,
            Thickness = 8,
        };
        _vScroll.DisplayValueChanged += (_, _) => Invalidate();
        _vScroll.ValueChanged += (_, _) => Invalidate();
        Controls.Add(_vScroll);

        ApplyTheme();
        ColorScheme.ThemeChanged += OnListThemeChanged;
    }

    #region Public API

    [Browsable(false)]
    public ObjectCollection Items => _items;

    /// <summary>
    /// Gets or sets whether items are drawn by the control (<see cref="DrawMode.Normal"/>) or by the
    /// owner via the <see cref="DrawItem"/> event.
    /// </summary>
    [DefaultValue(DrawMode.Normal)]
    public DrawMode DrawMode
    {
        get => _drawMode;
        set
        {
            if (_drawMode == value)
                return;

            _drawMode = value;
            Invalidate();
        }
    }

    /// <summary>
    /// When true, a check box is shown next to every item (the ListBox behaves like a WinForms
    /// CheckedListBox). Checking is independent of selection.
    /// </summary>
    [DefaultValue(false)]
    public bool CheckBoxes
    {
        get => _checkBoxes;
        set
        {
            if (_checkBoxes == value)
                return;

            _checkBoxes = value;
            Invalidate();
        }
    }

    /// <summary>When true (and <see cref="CheckBoxes"/> is on), a single click anywhere on a row toggles its check.</summary>
    [DefaultValue(false)]
    public bool CheckOnClick
    {
        get => _checkOnClick;
        set => _checkOnClick = value;
    }

    /// <summary>The indices of all checked (or indeterminate) items, ascending. Requires <see cref="CheckBoxes"/>.</summary>
    [Browsable(false)]
    public IReadOnlyList<int> CheckedIndices
    {
        get
        {
            var result = new List<int>();
            for (var i = 0; i < _checkStates.Count; i++)
                if (_checkStates[i] != CheckState.Unchecked)
                    result.Add(i);
            return result;
        }
    }

    /// <summary>The checked (or indeterminate) item objects. Requires <see cref="CheckBoxes"/>.</summary>
    [Browsable(false)]
    public IReadOnlyList<object?> CheckedItems
    {
        get
        {
            var result = new List<object?>();
            foreach (var i in CheckedIndices)
                result.Add(_items[i]);
            return result;
        }
    }

    public bool GetItemChecked(int index) => GetItemCheckState(index) != CheckState.Unchecked;

    public void SetItemChecked(int index, bool value)
        => SetItemCheckState(index, value ? CheckState.Checked : CheckState.Unchecked);

    public CheckState GetItemCheckState(int index)
        => index >= 0 && index < _checkStates.Count ? _checkStates[index] : CheckState.Unchecked;

    public void SetItemCheckState(int index, CheckState value)
    {
        if (index < 0 || index >= _checkStates.Count)
            return;

        var current = _checkStates[index];
        if (current == value)
            return;

        var args = new ItemCheckEventArgs(index, value, current);
        ItemCheck?.Invoke(this, args);

        if (args.NewValue == current)
            return; // handler cancelled the change

        _checkStates[index] = args.NewValue;
        ItemChecked?.Invoke(this, EventArgs.Empty);
        Invalidate();
    }

    [DefaultValue(SelectionMode.One)]
    public SelectionMode SelectionMode
    {
        get => _selectionMode;
        set
        {
            if (_selectionMode == value)
                return;

            _selectionMode = value;

            // Collapse to a single selection when leaving a multi mode.
            if (value is SelectionMode.None)
                ClearSelectedCore(raise: true);
            else if (value == SelectionMode.One && _selectedIndices.Count > 1)
            {
                var keep = _selectedIndices[0];
                _selectedIndices.Clear();
                _selectedIndices.Add(keep);
                RaiseSelectionChanged();
            }

            Invalidate();
        }
    }

    /// <summary>
    /// Gets or sets the zero-based index of the currently selected item, or -1 if none.
    /// In multi-selection modes this is the last item that was selected.
    /// </summary>
    [Browsable(false)]
    public int SelectedIndex
    {
        get => _selectedIndices.Count == 0 ? -1 : _selectedIndices[^1];
        set
        {
            if (value < -1 || value >= _items.Count)
                value = -1;

            if (SelectedIndex == value && _selectedIndices.Count <= 1)
                return;

            _selectedIndices.Clear();
            if (value >= 0)
            {
                _selectedIndices.Add(value);
                _anchorIndex = value;
                EnsureVisible(value);
            }

            RaiseSelectionChanged();
            Invalidate();
        }
    }

    /// <summary>Gets or sets the currently selected item object, or null if none.</summary>
    [Browsable(false)]
    public object? SelectedItem
    {
        get
        {
            var i = SelectedIndex;
            return i >= 0 && i < _items.Count ? _items[i] : null;
        }
        set => SelectedIndex = value is null ? -1 : _items.IndexOf(value);
    }

    /// <summary>Gets or sets the text of the selected item (or selects the first matching item).</summary>
    [Browsable(false)]
    public string SelectedText
    {
        get
        {
            var i = SelectedIndex;
            return i >= 0 ? GetItemText(_items[i]) : string.Empty;
        }
        set
        {
            var idx = FindStringExact(value);
            if (idx != NoMatches)
                SelectedIndex = idx;
        }
    }

    /// <summary>Gets the zero-based indices of all selected items (ascending order).</summary>
    [Browsable(false)]
    public IReadOnlyList<int> SelectedIndices
    {
        get
        {
            var copy = new List<int>(_selectedIndices);
            copy.Sort();
            return copy;
        }
    }

    /// <summary>Gets the selected item objects (in ascending index order).</summary>
    [Browsable(false)]
    public IReadOnlyList<object?> SelectedItems
    {
        get
        {
            var result = new List<object?>(_selectedIndices.Count);
            foreach (var i in SelectedIndices)
                result.Add(_items[i]);
            return result;
        }
    }

    /// <summary>Gets or sets the height, in pixels, of each item. 0 auto-sizes from the font.</summary>
    [DefaultValue(0)]
    public int ItemHeight
    {
        get => EffectiveItemHeight;
        set
        {
            var v = Math.Max(0, value);
            if (_itemHeight == v)
                return;

            _itemHeight = v;
            UpdateScrollMetrics();
            Invalidate();
        }
    }

    /// <summary>Gets or sets the property name used to obtain each item's display text.</summary>
    [DefaultValue("")]
    public string DisplayMember
    {
        get => _displayMember;
        set
        {
            var v = value ?? string.Empty;
            if (_displayMember == v)
                return;

            _displayMember = v;
            Invalidate();
        }
    }

    /// <summary>Gets or sets the index of the first visible item.</summary>
    [Browsable(false)]
    public int TopIndex
    {
        get => EffectiveItemHeight <= 0 ? 0 : (int)(_vScroll.Value / EffectiveItemHeight);
        set
        {
            var index = Math.Clamp(value, 0, Math.Max(0, _items.Count - 1));
            _vScroll.Value = index * EffectiveItemHeight;
            Invalidate();
        }
    }

    public bool GetSelected(int index) => _selectedIndices.Contains(index);

    public void SetSelected(int index, bool value)
    {
        if (index < 0 || index >= _items.Count || _selectionMode == SelectionMode.None)
            return;

        var contains = _selectedIndices.Contains(index);
        if (value == contains)
            return;

        if (value)
        {
            if (_selectionMode == SelectionMode.One)
                _selectedIndices.Clear();
            _selectedIndices.Add(index);
            _anchorIndex = index;
            EnsureVisible(index);
        }
        else
        {
            _selectedIndices.Remove(index);
        }

        RaiseSelectionChanged();
        Invalidate();
    }

    public void ClearSelected() => ClearSelectedCore(raise: true);

    /// <summary>Returns the index of the item at the given client point, or -1 if none.</summary>
    public int IndexFromPoint(float x, float y)
    {
        var content = ContentBounds;
        if (!content.Contains(x, y) || EffectiveItemHeight <= 0)
            return -1;

        var index = (int)((y - content.Top + _vScroll.DisplayValue) / EffectiveItemHeight);
        return index >= 0 && index < _items.Count ? index : -1;
    }

    public int IndexFromPoint(SKPoint point) => IndexFromPoint(point.X, point.Y);

    /// <summary>Finds the first item that starts with the given text (case-insensitive).</summary>
    public int FindString(string s) => FindStringCore(s, exact: false);

    /// <summary>Finds the first item equal to the given text (case-insensitive).</summary>
    public int FindStringExact(string s) => FindStringCore(s, exact: true);

    /// <summary>Suspends redraw/metric updates while items are added in bulk.</summary>
    public void BeginUpdate() => _updateSuspendCount++;

    /// <summary>Resumes redraw after <see cref="BeginUpdate"/>.</summary>
    public void EndUpdate()
    {
        if (_updateSuspendCount > 0)
            _updateSuspendCount--;

        if (_updateSuspendCount == 0)
        {
            UpdateScrollMetrics();
            if (_pendingSelectionChanged)
            {
                _pendingSelectionChanged = false;
                RaiseSelectionChanged();
            }
            Invalidate();
        }
    }

    /// <summary>Returns the display text of an item, honoring <see cref="DisplayMember"/>.</summary>
    public string GetItemText(object? item)
    {
        if (item is null)
            return string.Empty;

        if (!string.IsNullOrWhiteSpace(_displayMember))
        {
            var descriptor = TypeDescriptor.GetProperties(item).Find(_displayMember, true);
            if (descriptor != null)
                return Convert.ToString(descriptor.GetValue(item)) ?? string.Empty;
        }

        return item.ToString() ?? string.Empty;
    }

    #endregion

    #region Rendering

    private int EffectiveItemHeight
    {
        get
        {
            if (_itemHeight > 0)
                return _itemHeight;

            EnsureRenderFont();
            var m = (_renderFont ?? Application.SharedDefaultFont).Metrics;
            var line = MathF.Ceiling(-m.Ascent + m.Descent);
            return Math.Max(1, (int)(line + 16f * ScaleFactor));
        }
    }

    private SKRect ContentBounds
    {
        get
        {
            var r = DisplayRectangle;
            return r;
        }
    }

    public override void OnPaint(SKCanvas canvas)
    {
        base.OnPaint(canvas);

        UpdateScrollMetrics();

        var content = ContentBounds;
        if (content.Width <= 0 || content.Height <= 0 || _items.Count == 0)
            return;

        var itemH = EffectiveItemHeight;
        var offset = _vScroll.DisplayValue;
        var scrollVisible = _vScroll.Visible;
        var textRight = content.Right - (scrollVisible ? _vScroll.Thickness + 2f * ScaleFactor : 0f);

        EnsureRenderFont();
        var font = _renderFont ?? Application.SharedDefaultFont;

        _itemTextPaint ??= new SKPaint { IsAntialias = true };
        _rowPaint ??= new SKPaint { IsAntialias = true };

        var first = Math.Max(0, (int)(offset / itemH));
        var last = Math.Min(_items.Count - 1, (int)((offset + content.Height) / itemH));

        var clip = canvas.Save();
        canvas.ClipRect(new SKRect(content.Left, content.Top, textRight, content.Bottom));

        var ownerDraw = _drawMode != DrawMode.Normal && DrawItem != null;

        for (var i = first; i <= last; i++)
        {
            var rowTop = content.Top + i * itemH - offset;
            var rowRect = new SKRect(content.Left, rowTop, textRight, rowTop + itemH);

            var isSelected = _selectedIndices.Contains(i);
            var isHovered = i == _hoverIndex;

            if (ownerDraw)
            {
                var state = DrawItemState.None;
                if (isSelected) state |= DrawItemState.Selected;
                if (isHovered) state |= DrawItemState.HotLight;
                if (Focused && i == SelectedIndex) state |= DrawItemState.Focus;
                if (!Enabled) state |= DrawItemState.Disabled;

                var back = isSelected ? ColorScheme.Primary
                    : isHovered ? ColorScheme.Primary.WithAlpha(28)
                    : SKColors.Transparent;
                var fore = isSelected ? ColorScheme.Primary.Determine() : ForeColor;

                DrawItem.Invoke(this, new DrawItemEventArgs(canvas, font, rowRect, i, state, fore, back));
            }
            else
            {
                OnDrawItemContent(canvas, i, rowRect, font, isSelected, isHovered);
            }
        }

        canvas.RestoreToCount(clip);
    }

    /// <summary>
    /// Draws a single item's default content (row background + optional check box + text). Override
    /// in a derived control to customize the built-in appearance.
    /// </summary>
    protected virtual void OnDrawItemContent(SKCanvas canvas, int index, SKRect bounds, SKFont font, bool selected, bool hovered)
    {
        DrawRowBackground(canvas, bounds, selected, hovered);

        var textColor = selected ? ColorScheme.Primary.Determine() : ForeColor;
        var textLeft = bounds.Left + 10f * ScaleFactor;

        if (_checkBoxes)
        {
            var box = GetCheckBoxRect(bounds);
            DrawCheckGlyph(canvas, box, GetItemCheckState(index), selected);
            textLeft = box.Right + 8f * ScaleFactor;
        }

        var textRect = new SKRect(textLeft, bounds.Top, bounds.Right - 6f * ScaleFactor, bounds.Bottom);
        DrawRowText(canvas, GetItemText(_items[index]), textRect, font, textColor);
    }

    private float CheckBoxSize => MathF.Round(17f * ScaleFactor);

    private SKRect GetCheckBoxRect(SKRect rowBounds)
    {
        var size = CheckBoxSize;
        var left = rowBounds.Left + 10f * ScaleFactor;
        var top = rowBounds.MidY - size / 2f;
        return new SKRect(left, top, left + size, top + size);
    }

    private void DrawCheckGlyph(SKCanvas canvas, SKRect box, CheckState state, bool rowSelected)
    {
        var radius = MathF.Max(2f, 4f * ScaleFactor);
        var accent = ColorScheme.Primary;
        var isChecked = state != CheckState.Unchecked;

        if (isChecked)
        {
            // Filled box. On a selected (accent) row, invert so the box stays visible.
            using var fill = new SKPaint { IsAntialias = true, Color = rowSelected ? accent.Determine() : accent };
            canvas.DrawRoundRect(box, radius, radius, fill);

            using var mark = new SKPaint
            {
                IsAntialias = true,
                Style = SKPaintStyle.Stroke,
                StrokeCap = SKStrokeCap.Round,
                StrokeJoin = SKStrokeJoin.Round,
                StrokeWidth = MathF.Max(1.6f, 2f * ScaleFactor),
                Color = rowSelected ? accent : accent.Determine()
            };

            if (state == CheckState.Indeterminate)
            {
                var y = box.MidY;
                canvas.DrawLine(box.Left + box.Width * 0.25f, y, box.Right - box.Width * 0.25f, y, mark);
            }
            else
            {
                using var path = new SKPath();
                path.MoveTo(box.Left + box.Width * 0.24f, box.Top + box.Height * 0.52f);
                path.LineTo(box.Left + box.Width * 0.43f, box.Top + box.Height * 0.70f);
                path.LineTo(box.Left + box.Width * 0.78f, box.Top + box.Height * 0.30f);
                canvas.DrawPath(path, mark);
            }
        }
        else
        {
            using var stroke = new SKPaint
            {
                IsAntialias = true,
                Style = SKPaintStyle.Stroke,
                StrokeWidth = MathF.Max(1.4f, 1.6f * ScaleFactor),
                Color = rowSelected ? ColorScheme.Primary.Determine().WithAlpha(200) : ColorScheme.Outline.WithAlpha(180)
            };
            var inset = stroke.StrokeWidth / 2f;
            var r = new SKRect(box.Left + inset, box.Top + inset, box.Right - inset, box.Bottom - inset);
            canvas.DrawRoundRect(r, radius, radius, stroke);
        }
    }

    /// <summary>Fills the selection/hover background for a row. Available to derived controls.</summary>
    protected void DrawRowBackground(SKCanvas canvas, SKRect bounds, bool selected, bool hovered)
    {
        _rowPaint ??= new SKPaint { IsAntialias = true };
        var rowRadius = MathF.Max(2f, 6f * ScaleFactor);

        if (selected)
        {
            _rowPaint.Color = ColorScheme.Primary;
            canvas.DrawRoundRect(bounds, rowRadius, rowRadius, _rowPaint);
        }
        else if (hovered)
        {
            _rowPaint.Color = ColorScheme.Primary.WithAlpha(28);
            canvas.DrawRoundRect(bounds, rowRadius, rowRadius, _rowPaint);
        }
    }

    /// <summary>Draws left-aligned, vertically-centered, ellipsized row text. Available to derived controls.</summary>
    protected void DrawRowText(SKCanvas canvas, string text, SKRect textRect, SKFont font, SKColor color)
    {
        _itemTextPaint ??= new SKPaint { IsAntialias = true };
        _itemTextPaint.Color = color;
        TextRenderer.DrawText(canvas, text, textRect, _itemTextPaint, font, ContentAlignment.MiddleLeft, autoEllipsis: true);
    }

    /// <summary>The current vertical scroll offset in pixels. Available to derived controls.</summary>
    protected float ScrollOffset => _vScroll.DisplayValue;

    /// <summary>The font used to render items (already DPI-scaled). Available to derived controls.</summary>
    protected SKFont ItemFont
    {
        get
        {
            EnsureRenderFont();
            return _renderFont ?? Application.SharedDefaultFont;
        }
    }

    #endregion

    #region Input

    public override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);

        var index = IndexFromPoint(e.X, e.Y);
        if (index != _hoverIndex)
        {
            _hoverIndex = index;
            Invalidate();
        }
    }

    public override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        if (_hoverIndex != -1)
        {
            _hoverIndex = -1;
            Invalidate();
        }
    }

    public override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);

        if (e.Button != MouseButtons.Left)
            return;

        Focus();

        var index = IndexFromPoint(e.X, e.Y);
        if (index < 0)
            return;

        if (_selectionMode != SelectionMode.None)
        {
            var mods = ModifierKeys;
            var shift = (mods & Keys.Shift) == Keys.Shift;
            var control = (mods & Keys.Control) == Keys.Control;
            ApplyClickSelection(index, extend: shift, toggle: control);
        }

        if (_checkBoxes)
        {
            var itemH = EffectiveItemHeight;
            var rowTop = ContentBounds.Top + index * itemH - _vScroll.DisplayValue;
            var rowRect = new SKRect(ContentBounds.Left, rowTop, ContentBounds.Right, rowTop + itemH);
            var onCheckBox = GetCheckBoxRect(rowRect).Contains(e.X, e.Y);

            if (onCheckBox || _checkOnClick)
            {
                var current = GetItemCheckState(index);
                SetItemCheckState(index, current == CheckState.Checked ? CheckState.Unchecked : CheckState.Checked);
            }
        }
    }

    public override void OnMouseWheel(MouseEventArgs e)
    {
        base.OnMouseWheel(e);

        if (!_vScroll.Visible)
            return;

        // e.Delta is +/-120 per notch. Scroll ~3 items per notch.
        var notches = e.Delta / 120f;
        var step = EffectiveItemHeight * 3f;
        _vScroll.Value = Math.Clamp(_vScroll.Value - notches * step, 0f, _vScroll.Maximum);
        e.Handled = true;
    }

    public override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);

        if (e.Handled || _selectionMode == SelectionMode.None || _items.Count == 0)
            return;

        var current = SelectedIndex;
        var pageItems = Math.Max(1, (int)(ContentBounds.Height / EffectiveItemHeight));
        int target;

        switch (e.KeyCode)
        {
            case Keys.Up: target = Math.Max(0, (current < 0 ? 0 : current) - 1); break;
            case Keys.Down: target = Math.Min(_items.Count - 1, (current < 0 ? -1 : current) + 1); break;
            case Keys.Home: target = 0; break;
            case Keys.End: target = _items.Count - 1; break;
            case Keys.PageUp: target = Math.Max(0, (current < 0 ? 0 : current) - pageItems); break;
            case Keys.PageDown: target = Math.Min(_items.Count - 1, (current < 0 ? 0 : current) + pageItems); break;
            default: return;
        }

        if (_selectionMode == SelectionMode.MultiExtended && e.Shift)
            ExtendSelectionTo(target);
        else
            SelectedIndex = target;

        EnsureVisible(target);
        e.Handled = true;
    }

    private void ApplyClickSelection(int index, bool extend, bool toggle)
    {
        switch (_selectionMode)
        {
            case SelectionMode.One:
                SelectedIndex = index;
                break;

            case SelectionMode.MultiSimple:
                if (_selectedIndices.Contains(index))
                    _selectedIndices.Remove(index);
                else
                    _selectedIndices.Add(index);
                _anchorIndex = index;
                RaiseSelectionChanged();
                Invalidate();
                break;

            case SelectionMode.MultiExtended:
                if (extend && _anchorIndex >= 0)
                {
                    ExtendSelectionTo(index);
                }
                else if (toggle)
                {
                    if (_selectedIndices.Contains(index))
                        _selectedIndices.Remove(index);
                    else
                        _selectedIndices.Add(index);
                    _anchorIndex = index;
                    RaiseSelectionChanged();
                    Invalidate();
                }
                else
                {
                    SelectedIndex = index;
                }
                break;
        }
    }

    private void ExtendSelectionTo(int index)
    {
        if (_anchorIndex < 0)
            _anchorIndex = index;

        var lo = Math.Min(_anchorIndex, index);
        var hi = Math.Max(_anchorIndex, index);

        _selectedIndices.Clear();
        for (var i = lo; i <= hi; i++)
            _selectedIndices.Add(i);

        EnsureVisible(index);
        RaiseSelectionChanged();
        Invalidate();
    }

    #endregion

    #region Internal helpers

    protected internal virtual void HandleItemsCollectionChanged()
    {
        // Drop selections that point past the end of the list.
        for (var i = _selectedIndices.Count - 1; i >= 0; i--)
            if (_selectedIndices[i] >= _items.Count)
                _selectedIndices.RemoveAt(i);

        if (_anchorIndex >= _items.Count)
            _anchorIndex = -1;

        // Keep the parallel check-state list the same length as Items (new items start unchecked).
        while (_checkStates.Count < _items.Count)
            _checkStates.Add(CheckState.Unchecked);
        while (_checkStates.Count > _items.Count)
            _checkStates.RemoveAt(_checkStates.Count - 1);

        if (_updateSuspendCount > 0)
            return;

        UpdateScrollMetrics();
        Invalidate();
    }

    private void EnsureVisible(int index)
    {
        if (index < 0 || EffectiveItemHeight <= 0)
            return;

        var itemH = EffectiveItemHeight;
        var viewport = ContentBounds.Height;
        var itemTop = index * itemH;
        var itemBottom = itemTop + itemH;
        var value = _vScroll.Value;

        if (itemTop < value)
            value = itemTop;
        else if (itemBottom > value + viewport)
            value = itemBottom - viewport;

        _vScroll.Value = Math.Clamp(value, 0f, Math.Max(0f, _vScroll.Maximum));
    }

    private void UpdateScrollMetrics()
    {
        var content = ContentBounds;
        var itemH = EffectiveItemHeight;
        var totalHeight = _items.Count * itemH;
        var viewport = content.Height;
        var needsScroll = totalHeight > viewport + 0.5f;

        _vScroll.Visible = needsScroll;

        if (needsScroll)
        {
            var thickness = _vScroll.Thickness;
            _vScroll.Location = new SKPoint(content.Right - thickness, content.Top);
            _vScroll.Size = new SKSize(thickness, content.Height);
            _vScroll.Maximum = Math.Max(0f, totalHeight - viewport);
            _vScroll.LargeChange = Math.Max(1f, viewport);
            _vScroll.SmallChange = itemH;
        }
        else
        {
            _vScroll.Maximum = 0f;
            _vScroll.Value = 0f;
        }
    }

    private void ClearSelectedCore(bool raise)
    {
        if (_selectedIndices.Count == 0)
            return;

        _selectedIndices.Clear();
        if (raise)
            RaiseSelectionChanged();
        Invalidate();
    }

    private void RaiseSelectionChanged()
    {
        if (_updateSuspendCount > 0)
        {
            _pendingSelectionChanged = true;
            return;
        }

        SelectedIndexChanged?.Invoke(this, EventArgs.Empty);
        SelectedValueChanged?.Invoke(this, EventArgs.Empty);
    }

    private int FindStringCore(string s, bool exact)
    {
        if (s is null)
            return NoMatches;

        for (var i = 0; i < _items.Count; i++)
        {
            var text = GetItemText(_items[i]);
            if (exact
                ? string.Equals(text, s, StringComparison.CurrentCultureIgnoreCase)
                : text.StartsWith(s, StringComparison.CurrentCultureIgnoreCase))
                return i;
        }

        return NoMatches;
    }

    private void EnsureRenderFont()
    {
        var source = Font;
        if (_renderFont != null && ReferenceEquals(_renderFontSource, source) && _renderFontDpi == DeviceDpi)
            return;

        _renderFont?.Dispose();
        _renderFont = CreateRenderFont(source);
        _renderFontSource = source;
        _renderFontDpi = DeviceDpi;
    }

    private void ApplyTheme()
    {
        BackColor = ColorScheme.SurfaceContainerLow;
        ForeColor = ColorScheme.ForeColor;
        BorderColor = ColorScheme.Outline.WithAlpha(110);
    }

    private void OnListThemeChanged(object? sender, EventArgs e)
    {
        ApplyTheme();
        Invalidate();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            ColorScheme.ThemeChanged -= OnListThemeChanged;
            _renderFont?.Dispose();
            _itemTextPaint?.Dispose();
            _rowPaint?.Dispose();
        }

        base.Dispose(disposing);
    }

    #endregion

    #region Items collection

    /// <summary>Holds the items displayed by the <see cref="ListBox"/>.</summary>
    public sealed class ObjectCollection : IList
    {
        private readonly List<object?> _items = new();
        private readonly ListBox _owner;

        internal ObjectCollection(ListBox owner) => _owner = owner;

        public int Count => _items.Count;
        public bool IsFixedSize => false;
        public bool IsReadOnly => false;
        public bool IsSynchronized => false;
        public object SyncRoot => this;

        public object? this[int index]
        {
            get => _items[index];
            set
            {
                _items[index] = value;
                _owner.HandleItemsCollectionChanged();
            }
        }

        public int Add(object? value)
        {
            _items.Add(value);
            _owner.HandleItemsCollectionChanged();
            return _items.Count - 1;
        }

        public void AddRange(IEnumerable values)
        {
            ArgumentNullException.ThrowIfNull(values);

            _owner.BeginUpdate();
            try
            {
                foreach (var value in values)
                    _items.Add(value);
            }
            finally
            {
                _owner.EndUpdate();
            }

            _owner.HandleItemsCollectionChanged();
        }

        public void AddRange(params object?[] values) => AddRange((IEnumerable)values);

        public void Clear()
        {
            if (_items.Count == 0)
                return;

            _items.Clear();
            _owner._selectedIndices.Clear();
            _owner._anchorIndex = -1;
            _owner.HandleItemsCollectionChanged();
        }

        public bool Contains(object? value) => _items.Contains(value);

        public void CopyTo(Array array, int index) => ((ICollection)_items).CopyTo(array, index);

        public IEnumerator GetEnumerator() => _items.GetEnumerator();

        public int IndexOf(object? value) => _items.IndexOf(value);

        public void Insert(int index, object? value)
        {
            _items.Insert(index, value);
            _owner.HandleItemsCollectionChanged();
        }

        public void Remove(object? value)
        {
            var index = _items.IndexOf(value);
            if (index >= 0)
                RemoveAt(index);
        }

        public void RemoveAt(int index)
        {
            _items.RemoveAt(index);
            _owner.HandleItemsCollectionChanged();
        }
    }

    #endregion
}
