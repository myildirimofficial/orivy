using Orivy.Collections;
using Orivy.Animation;
using Orivy.Layout;
using System;
using System.Collections.Generic;
using System.Drawing;
using SkiaSharp;

namespace Orivy.Controls;

public sealed class ButtonGroup<TValue> : Container
{
    private bool _suppressCheckedChanged;
    private bool _scrollable;
    private Button? _selectedButton;
    private readonly Dictionary<Button, Radius> _buttonOriginalRadii = new();
    private readonly List<Button> _autoButtons = new();
    private Action<Button, TValue>? _configureButton;
    private Func<TValue, string>? _labelSelector;
    private readonly ItemCollection<TValue> _items;
    private ContentAlignment _alignment = ContentAlignment.MiddleLeft;
    private Orientation _orientation = Orientation.Horizontal;
    private int _gap = 8;
    private bool _applySelectionStyle = true;

    public ButtonGroup()
    {
        _items = new ItemCollection<TValue>(SyncButtonsToItems);
        BackColor = SkiaSharp.SKColors.Transparent;
        Border = new Thickness(0);
        Scrollable = true;
        AutoSize = true;
        MouseWheel += OnHorizontalMouseWheel;

        if (typeof(TValue).IsEnum)
            _items.AddRange((TValue[])Enum.GetValues(typeof(TValue)));
    }

    public bool AllowEmptySelection { get; set; }

    public bool ApplySelectionStyle
    {
        get => _applySelectionStyle;
        set
        {
            if (_applySelectionStyle == value)
                return;

            _applySelectionStyle = value;
            ApplySelectionStyleToButtons(this);
            Invalidate();
        }
    }

    public SKColor SelectedBackColor { get; set; } = SKColor.Empty;

    public SKColor SelectedForeColor { get; set; } = SKColor.Empty;

    public SKColor SelectedBorderColor { get; set; } = SKColor.Empty;

    public ItemCollection<TValue> Items => _items;

    public Func<TValue, string>? LabelSelector
    {
        get => _labelSelector;
        set
        {
            _labelSelector = value;
            SyncButtonsToItems();
        }
    }

    public Action<Button, TValue>? ConfigureButton
    {
        get => _configureButton;
        set
        {
            _configureButton = value;
            for (var i = 0; i < _autoButtons.Count; i++)
            {
                if (TryGetButtonValue(_autoButtons[i], out var itemValue))
                    value?.Invoke(_autoButtons[i], itemValue);
                ApplyButtonSelectionStyle(_autoButtons[i]);
            }
            PerformLayout();
            Invalidate();
        }
    }

    public void SetItems(IEnumerable<TValue> items, Func<TValue, string>? labelSelector = null)
    {
        _labelSelector = labelSelector;
        _items.ReplaceAll(items);
    }

    public void SetItems(Func<TValue, string> labelSelector, params TValue[] items)
        => SetItems(items, labelSelector);

    private void SyncButtonsToItems()
    {
        foreach (var btn in _autoButtons)
            Controls.Remove(btn);
        _autoButtons.Clear();

        for (var i = 0; i < _items.Count; i++)
        {
            var item = _items[i];
            var text = _labelSelector != null ? _labelSelector(item) : item?.ToString() ?? string.Empty;
            var button = new Button { Text = text, Tag = item! };
            _configureButton?.Invoke(button, item);
            ApplyButtonSelectionStyle(button);
            _autoButtons.Add(button);
            Controls.Add(button);
        }
    }

    public Orientation Orientation
    {
        get => _orientation;
        set
        {
            if (_orientation == value)
                return;

            _orientation = value;
            InvalidateMeasure();
            PerformLayout();
            Invalidate();
        }
    }

    public ContentAlignment Alignment
    {
        get => _alignment;
        set
        {
            if (_alignment == value)
                return;

            _alignment = value;
            PerformLayout();
            Invalidate();
        }
    }

    public int Gap
    {
        get => _gap;
        set
        {
            var normalized = Math.Max(0, value);
            if (_gap == normalized)
                return;

            _gap = normalized;
            InvalidateMeasure();
            PerformLayout();
            Invalidate();
        }
    }

    public int ButtonSpacing
    {
        get => Gap;
        set => Gap = value;
    }

    public bool Scrollable
    {
        get => _scrollable;
        set
        {
            if (_scrollable == value)
                return;

            _scrollable = value;
            AutoSize = !value;
            AutoScroll = value;
            InvalidateMeasure();
            PerformLayout();
            Invalidate();
        }
    }

    protected override bool HandlesMouseWheelScroll => false;

    private void OnHorizontalMouseWheel(object? sender, MouseEventArgs e)
    {
        if (!_scrollable || _hScrollBar == null || !_hScrollBar.Visible)
            return;

        if (!WantsHorizontalMouseWheel(e))
            return;

        _hScrollBar.ApplyWheelDelta(-GetMouseWheelDelta(e, _hScrollBar));
        e.Handled = true;
    }

    public Button? SelectedButton => _selectedButton;

    public bool HasSelection => _selectedButton != null;

    public TValue SelectedValue => TryGetButtonValue(_selectedButton, out var value) ? value : default!;

    public event EventHandler<ButtonGroupSelectionChangedEventArgs<TValue>>? SelectedValueChanged;

    public bool SetSelectedValue(TValue value, bool raiseChanged = true)
    {
        var button = FindButtonByValue(this, value);
        if (button == null)
            return false;

        SetSelectedButton(button, raiseChanged);
        return true;
    }

    public void ClearSelection(bool raiseChanged = true)
    {
        if (!AllowEmptySelection)
            return;

        var previousButton = _selectedButton;
        var previousValue = SelectedValue;
        _selectedButton = null;

        _suppressCheckedChanged = true;
        try
        {
            SetButtonCheckedRecursive(this, null);
        }
        finally
        {
            _suppressCheckedChanged = false;
        }

        if (previousButton != null && raiseChanged)
            RaiseSelectedValueChanged(previousButton, previousValue);
    }

    public override void  OnControlAdded(ElementEventArgs e)
    {
        if (e.Element is Button incomingButton && !_autoButtons.Contains(incomingButton))
            throw new InvalidOperationException("Use SetItems() or ConfigureButton to manage ButtonGroup contents.");

        base.OnControlAdded(e);
        RegisterElement(e.Element as ElementBase);
        if (e.Element is Button addedButton)
            ApplyButtonSelectionStyle(addedButton);
        InvalidateMeasure();
        PerformLayout();
        Invalidate();
    }

    public override void  OnControlRemoved(ElementEventArgs e)
    {
        UnregisterElement(e.Element as ElementBase);
        base.OnControlRemoved(e);
        InvalidateMeasure();
        PerformLayout();
        Invalidate();
    }

    public override SKSize GetPreferredSize(SKSize proposedSize)
    {
        var measured = MeasureButtonGroup(this, proposedSize);
        var width = measured.Width + Padding.Left + Padding.Right + Border.Left + Border.Right;
        var height = measured.Height + Padding.Top + Padding.Bottom + Border.Top + Border.Bottom;

        if (MinimumSize.Width > 0)
            width = Math.Max(width, MinimumSize.Width);
        if (MinimumSize.Height > 0)
            height = Math.Max(height, MinimumSize.Height);

        if (MaximumSize.Width > 0)
            width = Math.Min(width, MaximumSize.Width);
        if (MaximumSize.Height > 0)
            height = Math.Min(height, MaximumSize.Height);

        return new SKSize(width, height);
    }

    public override void  Dispose(bool disposing)
    {
        if (disposing)
            UnregisterElement(this);

        base.Dispose(disposing);
    }

    public override void  OnLayout(LayoutEventArgs e)
    {
        base.OnLayout(e);
        ArrangeButtonRows(this);
        if (_scrollable)
            UpdateScrollBars();
    }

    private void RegisterElement(ElementBase? element)
    {
        if (element == null)
            return;

        element.ControlAdded += HandleDescendantControlAdded;
        element.ControlRemoved += HandleDescendantControlRemoved;

        if (element is Button button)
        {
            if (!_buttonOriginalRadii.ContainsKey(button))
                _buttonOriginalRadii[button] = button.Radius;

            button.CheckOnClick = true;
            button.Dock = DockStyle.None;
            button.CheckedChanged += HandleButtonCheckedChanged;

            if (button.Checked)
                SetSelectedButton(button, raiseChanged: false);
        }

        for (var i = 0; i < element.Controls.Count; i++)
            RegisterElement(element.Controls[i]);
    }

    private void UnregisterElement(ElementBase? element)
    {
        if (element == null)
            return;

        element.ControlAdded -= HandleDescendantControlAdded;
        element.ControlRemoved -= HandleDescendantControlRemoved;

        if (element is Button button)
        {
            button.CheckedChanged -= HandleButtonCheckedChanged;
            _buttonOriginalRadii.Remove(button);
            if (ReferenceEquals(_selectedButton, button))
                _selectedButton = null;
        }

        for (var i = 0; i < element.Controls.Count; i++)
            UnregisterElement(element.Controls[i]);
    }

    private void HandleDescendantControlAdded(object? sender, ElementEventArgs e)
    {
        RegisterElement(e.Element as ElementBase);
        InvalidateMeasure();
        PerformLayout();
        Invalidate();
    }

    private void HandleDescendantControlRemoved(object? sender, ElementEventArgs e)
    {
        UnregisterElement(e.Element as ElementBase);
        InvalidateMeasure();
        PerformLayout();
        Invalidate();
    }

    private void HandleButtonCheckedChanged(object? sender, EventArgs e)
    {
        if (_suppressCheckedChanged || sender is not Button button)
            return;

        if (button.Checked)
        {
            SetSelectedButton(button, raiseChanged: true);
            return;
        }

        if (!ReferenceEquals(_selectedButton, button))
            return;

        if (!AllowEmptySelection)
        {
            _suppressCheckedChanged = true;
            try
            {
                button.Checked = true;
            }
            finally
            {
                _suppressCheckedChanged = false;
            }

            return;
        }

        var previousValue = SelectedValue;
        _selectedButton = null;
        RaiseSelectedValueChanged(button, previousValue);
    }

    private void SetSelectedButton(Button button, bool raiseChanged)
    {
        var previousButton = _selectedButton;
        var previousValue = SelectedValue;
        var changed = !ReferenceEquals(previousButton, button);

        _selectedButton = button;

        _suppressCheckedChanged = true;
        try
        {
            SetButtonCheckedRecursive(this, button);
        }
        finally
        {
            _suppressCheckedChanged = false;
        }

        if (changed && raiseChanged)
            RaiseSelectedValueChanged(previousButton, previousValue);
    }

    private void RaiseSelectedValueChanged(Button? previousButton, TValue previousValue)
    {
        SelectedValueChanged?.Invoke(
            this,
            new ButtonGroupSelectionChangedEventArgs<TValue>(
                previousButton,
                _selectedButton,
                previousValue,
                SelectedValue));
    }

    private static void SetButtonCheckedRecursive(ElementBase element, Button? selectedButton)
    {
        for (var i = 0; i < element.Controls.Count; i++)
        {
            var child = element.Controls[i];

            if (child is Button button)
                button.Checked = ReferenceEquals(button, selectedButton);

            SetButtonCheckedRecursive(child, selectedButton);
        }
    }

    private void ArrangeButtonRows(ElementBase parent)
    {
        var buttons = GetDirectButtons(parent);
        if (buttons.Count > 0)
            ArrangeButtonRow(parent, buttons);

        for (var i = 0; i < parent.Controls.Count; i++)
        {
            if (parent.Controls[i] is ElementBase child && child is not Button)
                ArrangeButtonRows(child);
        }
    }

    private void ArrangeButtonRow(ElementBase parent, List<Button> buttons)
    {
        var bounds = parent.DisplayRectangle;
        if (bounds.Width <= 0 || bounds.Height <= 0)
            return;

        if (Orientation == Orientation.Vertical)
            ArrangeButtonColumn(parent, buttons, bounds);
        else
            ArrangeButtonRow(buttons, bounds);
    }

    private void ArrangeButtonRow(List<Button> buttons, SKRect bounds)
    {
        var availableBounds = bounds;
        var spacing = Gap;
        var overlap = spacing == 0 ? 1f : 0f;
        var visibleCount = 0;
        var totalWidth = 0f;
        var rowHeight = 0f;

        for (var i = 0; i < buttons.Count; i++)
        {
            var button = buttons[i];
            if (!button.Visible)
                continue;

            var buttonSize = GetButtonLayoutSize(button);
            visibleCount++;
            totalWidth += buttonSize.Width;
            rowHeight = Math.Max(rowHeight, buttonSize.Height);
        }

        if (visibleCount == 0)
            return;

        totalWidth += (spacing - overlap) * Math.Max(0, visibleCount - 1);

        var x = _scrollable ? availableBounds.Left : GetAlignedX(availableBounds, totalWidth);
        var visibleIndex = 0;

        for (var i = 0; i < buttons.Count; i++)
        {
            var button = buttons[i];
            if (!button.Visible)
                continue;

            var buttonSize = GetButtonLayoutSize(button);
            var width = buttonSize.Width;
            var height = Math.Min(rowHeight, availableBounds.Height);
            var y = GetAlignedY(availableBounds, height);

            ApplySegmentedRadius(button, visibleIndex, visibleCount);
            button.Bounds = SKRect.Create(x, y, width, height);
            DefaultLayout.SetAnchorInfo(button, null);
            x += width + spacing - overlap;
            visibleIndex++;
        }
    }

    private void ArrangeButtonColumn(ElementBase parent, List<Button> buttons, SKRect bounds)
    {
        var availableBounds = bounds;
        var spacing = Gap;
        var overlap = spacing == 0 ? 1f : 0f;
        var visibleCount = 0;
        var totalHeight = 0f;
        var maxWidth = 0f;
        var itemHeight = 0f;

        for (var i = 0; i < buttons.Count; i++)
        {
            var button = buttons[i];
            if (!button.Visible)
                continue;

            var buttonSize = GetButtonLayoutSize(button);
            visibleCount++;
            maxWidth = Math.Max(maxWidth, buttonSize.Width);
            itemHeight = Math.Max(itemHeight, buttonSize.Height);
        }

        if (visibleCount == 0)
            return;

        totalHeight = (itemHeight * visibleCount) + ((spacing - overlap) * Math.Max(0, visibleCount - 1));

        var columnWidth = Math.Min(maxWidth, bounds.Width);
        var y = _scrollable ? availableBounds.Top : GetAlignedY(availableBounds, totalHeight);
        var x = GetAlignedX(availableBounds, columnWidth);
        var visibleIndex = 0;

        for (var i = 0; i < buttons.Count; i++)
        {
            var button = buttons[i];
            if (!button.Visible)
                continue;

            var width = columnWidth;
            var height = itemHeight;

            ApplySegmentedRadius(button, visibleIndex, visibleCount);
            button.Bounds = SKRect.Create(x, y, width, height);
            DefaultLayout.SetAnchorInfo(button, null);
            y += height + spacing - overlap;
            visibleIndex++;
        }
    }

    private float GetAlignedX(SKRect bounds, float totalWidth)
    {
        return Alignment switch
        {
            ContentAlignment.TopCenter or ContentAlignment.MiddleCenter or ContentAlignment.BottomCenter =>
                bounds.Left + Math.Max(0f, (bounds.Width - totalWidth) / 2f),
            ContentAlignment.TopRight or ContentAlignment.MiddleRight or ContentAlignment.BottomRight =>
                bounds.Right - totalWidth,
            _ => bounds.Left,
        };
    }

    private float GetAlignedY(SKRect bounds, float height)
    {
        return Alignment switch
        {
            ContentAlignment.BottomLeft or ContentAlignment.BottomCenter or ContentAlignment.BottomRight =>
                bounds.Bottom - height,
            ContentAlignment.TopLeft or ContentAlignment.TopCenter or ContentAlignment.TopRight =>
                bounds.Top,
            _ => bounds.Top + Math.Max(0f, (bounds.Height - height) / 2f),
        };
    }

    private static SKSize GetButtonLayoutSize(Button button)
    {
        if (!button.AutoSize)
            return button.Size;

        var preferred = button.GetPreferredSize(SKSize.Empty);
        return new SKSize(
            Math.Max(1f, preferred.Width),
            Math.Max(1f, preferred.Height));
    }

    private SKSize MeasureButtonGroup(ElementBase parent, SKSize proposedSize)
    {
        var measured = MeasureDirectButtons(parent, proposedSize);

        for (var i = 0; i < parent.Controls.Count; i++)
        {
            if (parent.Controls[i] is not ElementBase child || child is Button || !child.Visible)
                continue;

            var childSize = child.AutoSize
                ? child.GetPreferredSize(proposedSize)
                : child.Size;
            var childMargin = child.Margin;
            var childTotalWidth = childSize.Width + childMargin.Left + childMargin.Right;
            var childTotalHeight = childSize.Height + childMargin.Top + childMargin.Bottom;

            measured = new SKSize(
                Math.Max(measured.Width, childTotalWidth),
                measured.Height + childTotalHeight);

            var nestedMeasured = MeasureButtonGroup(child, proposedSize);
            measured = new SKSize(
                Math.Max(measured.Width, nestedMeasured.Width),
                Math.Max(measured.Height, nestedMeasured.Height));
        }

        return measured;
    }

    private SKSize MeasureDirectButtons(ElementBase parent, SKSize proposedSize)
    {
        var spacing = Gap;
        var overlap = spacing == 0 ? 1f : 0f;
        var visibleCount = 0;
        var totalWidth = 0f;
        var totalHeight = 0f;
        var maxWidth = 0f;
        var maxHeight = 0f;

        for (var i = 0; i < parent.Controls.Count; i++)
        {
            if (parent.Controls[i] is not Button button || !button.Visible)
                continue;

            var buttonSize = MeasureButtonForGroup(button, proposedSize);
            visibleCount++;
            totalWidth += buttonSize.Width;
            totalHeight += buttonSize.Height;
            maxWidth = Math.Max(maxWidth, buttonSize.Width);
            maxHeight = Math.Max(maxHeight, buttonSize.Height);
        }

        if (visibleCount == 0)
            return SKSize.Empty;

        var spacingExtent = (spacing - overlap) * Math.Max(0, visibleCount - 1);
        return Orientation == Orientation.Vertical
            ? new SKSize(maxWidth, totalHeight + spacingExtent)
            : new SKSize(totalWidth + spacingExtent, maxHeight);
    }

    private SKSize MeasureButtonForGroup(Button button, SKSize proposedSize)
    {
        if (!button.AutoSize)
            return button.Size;

        var constraints = proposedSize;
        if (Orientation == Orientation.Horizontal && constraints.Height <= 0)
            constraints.Height = button.Height > 0 ? button.Height : short.MaxValue;
        if (Orientation == Orientation.Vertical && constraints.Width <= 0)
            constraints.Width = button.Width > 0 ? button.Width : short.MaxValue;

        var preferred = button.GetPreferredSize(constraints);
        return new SKSize(
            Math.Max(1f, preferred.Width),
            Math.Max(1f, preferred.Height));
    }

    private void ApplySegmentedRadius(Button button, int visibleIndex, int visibleCount)
    {
        if (Gap > 0)
        {
            if (_buttonOriginalRadii.TryGetValue(button, out var originalRadius))
                button.Radius = originalRadius;

            return;
        }

        var radius = _buttonOriginalRadii.TryGetValue(button, out var storedRadius)
            ? storedRadius
            : button.Radius;
        if (visibleCount <= 1)
        {
            button.Radius = radius;
            return;
        }

        var maxRadius = Math.Max(
            Math.Max(radius.TopLeft, radius.TopRight),
            Math.Max(radius.BottomLeft, radius.BottomRight));

        var first = visibleIndex == 0;
        var last = visibleIndex == visibleCount - 1;

        button.Radius = Orientation == Orientation.Vertical
            ? new Radius(
                first ? maxRadius : 0,
                first ? maxRadius : 0,
                last ? maxRadius : 0,
                last ? maxRadius : 0)
            : new Radius(
                first ? maxRadius : 0,
                last ? maxRadius : 0,
                first ? maxRadius : 0,
                last ? maxRadius : 0);
    }

    private void ApplySelectionStyleToButtons(ElementBase parent)
    {
        for (var i = 0; i < parent.Controls.Count; i++)
        {
            if (parent.Controls[i] is Button button)
                ApplyButtonSelectionStyle(button);
            else
                ApplySelectionStyleToButtons(parent.Controls[i]);
        }
    }

    private void ApplyButtonSelectionStyle(Button button)
    {
        if (!ApplySelectionStyle)
            return;

        var selectedBack = SelectedBackColor == SKColor.Empty ? ColorScheme.Primary : SelectedBackColor;
        var selectedFore = SelectedForeColor == SKColor.Empty ? SKColors.White : SelectedForeColor;
        var selectedBorder = SelectedBorderColor == SKColor.Empty ? selectedBack.Brightness(-0.16f) : SelectedBorderColor;

        button.ConfigureVisualStyles(styles => styles
            .DefaultTransition(TimeSpan.FromMilliseconds(120), AnimationType.CubicEaseOut)
            .Base(rule => rule
                .Background(ColorScheme.Surface)
                .Foreground(ColorScheme.ForeColor)
                .Border(1)
                .BorderColor(ColorScheme.Outline.WithAlpha(120))
                .Radius(10)
                .Shadow(BoxShadow.None))
            .OnHover(rule => rule
                .Background(ColorScheme.SurfaceContainerHigh)
                .BorderColor(ColorScheme.Primary.WithAlpha(118)))
            .OnPressed(rule => rule
                .Background(ColorScheme.Primary.WithAlpha(38))
                .BorderColor(ColorScheme.Primary.WithAlpha(160))
                .Scale(0.99f))
            .OnChecked(rule => rule
                .Background(selectedBack)
                .Foreground(selectedFore)
                .BorderColor(selectedBorder)
                .Shadow(new BoxShadow(0f, 6f, 14f, 0, selectedBack.WithAlpha(30))))
            .OnFocused(rule => rule
                .Border(2)
                .BorderColor(ColorScheme.Primary.WithAlpha(210)))
            .OnDisabled(rule => rule
                .Background(ColorScheme.SurfaceVariant)
                .Foreground(ColorScheme.ForeColor.WithAlpha(150))
                .BorderColor(ColorScheme.Outline.WithAlpha(80))
                .Opacity(0.72f)
                .Shadow(BoxShadow.None)),
            clearExisting: true);
    }

    private static List<Button> GetDirectButtons(ElementBase parent)
    {
        var buttons = new List<Button>();
        for (var i = 0; i < parent.Controls.Count; i++)
        {
            if (parent.Controls[i] is Button button)
                buttons.Add(button);
        }

        return buttons;
    }

    private static Button? FindButtonByValue(ElementBase element, TValue value)
    {
        var comparer = EqualityComparer<TValue>.Default;

        for (var i = 0; i < element.Controls.Count; i++)
        {
            var child = element.Controls[i];

            if (child is Button button &&
                TryGetButtonValue(button, out var buttonValue) &&
                comparer.Equals(buttonValue, value))
            {
                return button;
            }

            var nestedButton = FindButtonByValue(child, value);
            if (nestedButton != null)
                return nestedButton;
        }

        return null;
    }

    private static bool TryGetButtonValue(Button? button, out TValue value)
    {
        if (button?.Tag is TValue typedValue)
        {
            value = typedValue;
            return true;
        }

        value = default!;
        return false;
    }
}

public sealed class ButtonGroupSelectionChangedEventArgs<TValue> : EventArgs
{
    public ButtonGroupSelectionChangedEventArgs(
        Button? previousButton,
        Button? selectedButton,
        TValue previousValue,
        TValue selectedValue)
    {
        PreviousButton = previousButton;
        SelectedButton = selectedButton;
        PreviousValue = previousValue;
        SelectedValue = selectedValue;
    }

    public Button? PreviousButton { get; }
    public Button? SelectedButton { get; }
    public TValue PreviousValue { get; }
    public TValue SelectedValue { get; }
}
