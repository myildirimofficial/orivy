using SkiaSharp;
using System;
using System.ComponentModel;
using System.Globalization;
using Orivy.Helpers;

namespace Orivy.Controls;

public class TimePicker : ElementBase
{
    private readonly SKPaint _iconPaint = new() { IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeCap = SKStrokeCap.Round, StrokeJoin = SKStrokeJoin.Round };
    private readonly TimePickerDropDown _dropDown;
    private readonly TextBox _textBox;
    private EventHandler? _ownerDeactivateHandler;
    private KeyEventHandler? _ownerKeyDownHandler;
    private MouseEventHandler? _ownerMouseDownHandler;
    private WindowBase? _handlerWindow;
    private TimeSpan _value = DateTime.Now.TimeOfDay;
    private string _format = "HH:mm";
    private int _minuteStep = 5;
    private bool _ownerHandlersAttached;
    private bool _syncingTextBox;
    private bool _textBoxMode;
    private bool _use24HourClock = true;

    public TimePicker()
    {
        _value = new TimeSpan(_value.Hours, (_value.Minutes / _minuteStep) * _minuteStep, 0);
        _dropDown = new TimePickerDropDown(this);
        _textBox = CreateHostedTextBox();
        _textBox.TextChanged += HandleHostedTextBoxTextChanged;
        _textBox.LostFocus += HandleHostedTextBoxLostFocus;
        _textBox.KeyDown += HandleHostedTextBoxKeyDown;

        AutoEllipsis = true;
        CanSelect = true;
        MinimumSize = new SKSize(120, 40);
        Padding = new Thickness(14, 0, 46, 0);
        Radius = new Radius(12);
        Size = new SKSize(150, 40);
        TabStop = true;
        TextAlign = ContentAlignment.MiddleLeft;
        ApplyTheme();
        Controls.Add(_textBox);
        ColorScheme.ThemeChanged += OnThemeChanged;
        UpdateText();
        UpdateHostedTextBoxBounds();
    }

    [Category("Behavior")]
    [DefaultValue(false)]
    public bool DroppedDown => _dropDown.IsOpen;

    [Category("Behavior")]
    [DefaultValue(1000000)]
    public int DropDownZIndex { get; set; } = 1_000_000;

    [Category("Data")]
    public TimeSpan Value
    {
        get => _value;
        set => SetValue(value, true);
    }

    [Category("Appearance")]
    [DefaultValue("HH:mm")]
    public string Format
    {
        get => _format;
        set
        {
            var next = string.IsNullOrWhiteSpace(value) ? "HH:mm" : value;
            if (_format == next)
                return;

            _format = next;
            UpdateText();
            InvalidateMeasure();
            Invalidate();
        }
    }

    [Category("Behavior")]
    [DefaultValue(5)]
    public int MinuteStep
    {
        get => _minuteStep;
        set
        {
            var next = Math.Clamp(value, 1, 30);
            if (_minuteStep == next)
                return;

            _minuteStep = next;
            SetValue(_value, false);
            _dropDown.Invalidate();
        }
    }

    [Category("Behavior")]
    [DefaultValue(true)]
    public bool Use24HourClock
    {
        get => _use24HourClock;
        set
        {
            if (_use24HourClock == value)
                return;

            _use24HourClock = value;
            Format = value ? "HH:mm" : "h:mm tt";
            _dropDown.Invalidate();
        }
    }

    [Category("Behavior")]
    [DefaultValue(false)]
    public bool TextBoxMode
    {
        get => _textBoxMode;
        set
        {
            if (_textBoxMode == value)
                return;

            _textBoxMode = value;
            _textBox.Visible = value;
            if (value)
                SyncHostedTextBoxText();
            UpdateHostedTextBoxBounds();
            Invalidate();
        }
    }

    public event EventHandler? ValueChanged;
    public event EventHandler? DropDownOpened;
    public event EventHandler? DropDownClosed;

    protected override bool ShouldRenderDefaultText => !TextBoxMode;

    public void ShowDropDown()
    {
        if (!Enabled || DroppedDown || ParentWindow == null || (!Visible && Parent is not ElementBase { IsFloatingOverlay: true }))
            return;

        _dropDown.SyncFromOwner();
        _dropDown.ShowForOwner();
        AttachOwnerWindowHandlers();
        ReevaluateVisualStyles();
        Invalidate();
        DropDownOpened?.Invoke(this, EventArgs.Empty);
    }

    public void HideDropDown()
    {
        if (!DroppedDown)
            return;

        _dropDown.HideDropDown();
        DetachOwnerWindowHandlers();
        ReevaluateVisualStyles();
        Invalidate();
        DropDownClosed?.Invoke(this, EventArgs.Empty);
    }

    public void ToggleDropDown()
    {
        if (DroppedDown)
            HideDropDown();
        else
            ShowDropDown();
    }

    internal void UpdateDropDownAnchor()
    {
        if (DroppedDown)
            _dropDown.UpdateAnchor();
    }

    public override void OnPaint(SKCanvas canvas)
    {
        base.OnPaint(canvas);
        DrawClockIcon(canvas);
    }

    internal override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        if (!Enabled || e.Button != MouseButtons.Left)
            return;

        ToggleDropDown();
        UpdatePressedState(false);
        e.Handled = true;
    }

    internal override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Handled || !Enabled)
            return;

        switch (e.KeyCode)
        {
            case Keys.Enter:
            case Keys.Space:
                ToggleDropDown();
                e.Handled = true;
                break;
            case Keys.Escape:
                HideDropDown();
                e.Handled = true;
                break;
            case Keys.Up:
            case Keys.Right:
                Value = _value.Add(TimeSpan.FromMinutes(_minuteStep));
                e.Handled = true;
                break;
            case Keys.Down:
            case Keys.Left:
                Value = _value.Subtract(TimeSpan.FromMinutes(_minuteStep));
                e.Handled = true;
                break;
        }
    }

    internal override void OnVisibleChanged(EventArgs e)
    {
        if (!Visible)
            HideDropDown();

        base.OnVisibleChanged(e);
    }

    internal override void OnEnabledChanged(EventArgs e)
    {
        if (!Enabled)
            HideDropDown();

        base.OnEnabledChanged(e);
    }

    internal override void OnLocationChanged(EventArgs e)
    {
        base.OnLocationChanged(e);
        _dropDown.UpdateAnchor();
    }

    internal override void OnSizeChanged(EventArgs e)
    {
        base.OnSizeChanged(e);
        _dropDown.UpdateAnchor();
        UpdateHostedTextBoxBounds();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            ColorScheme.ThemeChanged -= OnThemeChanged;
            DetachOwnerWindowHandlers();
            _textBox.TextChanged -= HandleHostedTextBoxTextChanged;
            _textBox.LostFocus -= HandleHostedTextBoxLostFocus;
            _textBox.KeyDown -= HandleHostedTextBoxKeyDown;
            _dropDown.Dispose();
            _iconPaint.Dispose();
        }

        base.Dispose(disposing);
    }

    internal void CommitTime(int hour, int minute)
    {
        SetValue(new TimeSpan(Math.Clamp(hour, 0, 23), Math.Clamp(minute, 0, 59), 0), true);
    }

    private void SetValue(TimeSpan value, bool raiseChanged)
    {
        var totalMinutes = (int)Math.Round(value.TotalMinutes);
        var dayMinutes = 24 * 60;
        totalMinutes %= dayMinutes;
        if (totalMinutes < 0)
            totalMinutes += dayMinutes;

        var minute = totalMinutes % 60;
        minute = (minute / _minuteStep) * _minuteStep;
        totalMinutes = (totalMinutes / 60) * 60 + minute;
        totalMinutes %= dayMinutes;

        var next = TimeSpan.FromMinutes(totalMinutes);
        if (_value == next)
            return;

        _value = next;
        UpdateText();
        ReevaluateVisualStyles();
        InvalidateMeasure();
        Invalidate();
        _dropDown.Invalidate();
        if (TextBoxMode && !_textBox.Focused)
            SyncHostedTextBoxText();

        if (raiseChanged)
            ValueChanged?.Invoke(this, EventArgs.Empty);
    }

    private void UpdateText()
    {
        base.Text = DateTime.Today.Add(_value).ToString(_format, CultureInfo.CurrentCulture);
    }

    private void ApplyTheme()
    {
        BackColor = ColorScheme.Surface;
        ForeColor = ColorScheme.ForeColor;
        Border = new Thickness(1);
        BorderColor = ColorScheme.Outline.WithAlpha(104);
        Shadow = BoxShadow.None;
        _textBox.ForeColor = ForeColor;
    }

    private void OnThemeChanged(object? sender, EventArgs e)
    {
        ApplyTheme();
        _dropDown.ApplyTheme();
        Invalidate();
    }

    private TextBox CreateHostedTextBox()
    {
        var textBox = new TextBox
        {
            Name = "timePickerTextBox",
            Visible = false,
            Border = new Thickness(0),
            Radius = new Radius(0),
            BackColor = SKColors.Transparent,
            ForeColor = ForeColor,
            Padding = new Thickness(0),
            TextAlign = ContentAlignment.MiddleLeft,
            AutoScroll = false,
            AutoSize = false,
            MinimumSize = new SKSize(0, 0),
            TabStop = false,
            UseDefaultPointerVisualStates = false
        };
        textBox.ClearVisualStyles();
        textBox.ClearMotionEffects();
        textBox.Shadow = BoxShadow.None;
        textBox.Border = new Thickness(0);
        textBox.BackColor = SKColors.Transparent;
        return textBox;
    }

    private void UpdateHostedTextBoxBounds()
    {
        if (!TextBoxMode)
            return;

        var scale = ScaleFactor;
        var rect = SKRect.Create(
            Padding.Left,
            4f * scale,
            Math.Max(0f, Width - Padding.Left - 42f * scale),
            Math.Max(0f, Height - 8f * scale));
        _textBox.Location = new SKPoint(rect.Left, rect.Top);
        _textBox.Size = new SKSize(rect.Width, rect.Height);
        _textBox.ForeColor = ForeColor;
        _textBox.Font = Font;
    }

    private void SyncHostedTextBoxText()
    {
        _syncingTextBox = true;
        _textBox.Text = DateTime.Today.Add(_value).ToString(_format, CultureInfo.CurrentCulture);
        _syncingTextBox = false;
    }

    private void CommitHostedTextBox()
    {
        if (!TextBoxMode || _syncingTextBox)
            return;

        if (TryParseTextValue(_textBox.Text, out var parsed))
            Value = parsed.TimeOfDay;
        else if (TimeSpan.TryParse(_textBox.Text, CultureInfo.CurrentCulture, out var span))
            Value = span;
        else
            SyncHostedTextBoxText();
    }

    private void HandleHostedTextBoxTextChanged(object? sender, EventArgs e)
    {
        if (_syncingTextBox || !TextBoxMode)
            return;

        if (TryParseTextValue(_textBox.Text, out var parsed))
            SetValue(parsed.TimeOfDay, true);
        else if (TimeSpan.TryParse(_textBox.Text, CultureInfo.CurrentCulture, out var span))
            SetValue(span, true);
    }

    private bool TryParseTextValue(string text, out DateTime value)
    {
        return DateTime.TryParseExact(text, _format, CultureInfo.CurrentCulture, DateTimeStyles.NoCurrentDateDefault | DateTimeStyles.AllowWhiteSpaces, out value)
            || DateTime.TryParse(text, CultureInfo.CurrentCulture, DateTimeStyles.NoCurrentDateDefault | DateTimeStyles.AllowWhiteSpaces, out value);
    }

    private void HandleHostedTextBoxLostFocus(object? sender, EventArgs e)
    {
        CommitHostedTextBox();
    }

    private void HandleHostedTextBoxKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.KeyCode == Keys.Enter)
        {
            CommitHostedTextBox();
            e.Handled = true;
        }
        else if (e.KeyCode == Keys.Escape)
        {
            SyncHostedTextBoxText();
            e.Handled = true;
        }
    }

    private void DrawClockIcon(SKCanvas canvas)
    {
        var scale = ScaleFactor;
        var cx = Width - 24f * scale;
        var cy = Height / 2f;
        var radius = 8.2f * scale;
        _iconPaint.Color = Enabled ? ForeColor.WithAlpha((byte)(DroppedDown ? 230 : 168)) : ForeColor.WithAlpha(90);
        _iconPaint.StrokeWidth = Math.Max(1.35f, 1.35f * scale);
        canvas.DrawCircle(cx, cy, radius, _iconPaint);
        canvas.DrawLine(cx, cy, cx, cy - 4.8f * scale, _iconPaint);
        canvas.DrawLine(cx, cy, cx + 4.5f * scale, cy + 2.7f * scale, _iconPaint);
    }

    private void AttachOwnerWindowHandlers()
    {
        var window = ParentWindow;
        if (window == null || _ownerHandlersAttached)
            return;

        _ownerMouseDownHandler ??= OnOwnerWindowMouseDown;
        _ownerDeactivateHandler ??= (_, _) => HideDropDown();
        _ownerKeyDownHandler ??= (_, e) =>
        {
            if (e.KeyCode == Keys.Escape)
            {
                HideDropDown();
                e.Handled = true;
            }
        };

        _handlerWindow = window;
        _handlerWindow.MouseDown += _ownerMouseDownHandler;
        _handlerWindow.Deactivate += _ownerDeactivateHandler;
        _handlerWindow.KeyDown += _ownerKeyDownHandler;
        _ownerHandlersAttached = true;
    }

    private void DetachOwnerWindowHandlers()
    {
        if (!_ownerHandlersAttached || _handlerWindow == null)
        {
            _ownerHandlersAttached = false;
            _handlerWindow = null;
            return;
        }

        _handlerWindow.MouseDown -= _ownerMouseDownHandler;
        _handlerWindow.Deactivate -= _ownerDeactivateHandler;
        _handlerWindow.KeyDown -= _ownerKeyDownHandler;
        _ownerHandlersAttached = false;
        _handlerWindow = null;
    }

    private void OnOwnerWindowMouseDown(object sender, MouseEventArgs e)
    {
        if (!DroppedDown)
            return;

        var ownerBounds = GetPickerWindowRelativeBounds(this);
        var popupBounds = GetPickerWindowRelativeBounds(_dropDown);
        if (!ownerBounds.Contains(e.Location) && !popupBounds.Contains(e.Location))
            HideDropDown();
    }

    private static SKRect GetPickerWindowRelativeBounds(ElementBase element)
    {
        var window = element.ParentWindow;
        if (window == null)
            return element.Bounds;

        var topLeft = window.PointToClient(element.PointToScreen(new SKPoint(0, 0)));
        return SKRect.Create(topLeft.X, topLeft.Y, element.Width, element.Height);
    }

    internal bool ContainsDropDownWindowPoint(SKPoint point)
    {
        return DroppedDown && GetPickerWindowRelativeBounds(_dropDown).Contains(point);
    }

    private sealed class TimePickerDropDown : ElementBase
    {
        private readonly TimePicker _owner;
        private readonly SKPaint _fillPaint = new() { IsAntialias = true, Style = SKPaintStyle.Fill };
        private readonly SKPaint _strokePaint = new() { IsAntialias = true, Style = SKPaintStyle.Stroke };
        private readonly SKPaint _textPaint = new() { IsAntialias = true, Style = SKPaintStyle.Fill };
        private int _hoverHour = -1;
        private int _hoverMinute = -1;
        private int _pressedHour = -1;
        private int _pressedMinute = -1;

        public TimePickerDropDown(TimePicker owner)
        {
            _owner = owner;
            Visible = false;
            AutoSize = false;
            CanSelect = false;
            Size = new SKSize(318, 270);
            Padding = new Thickness(12);
            Radius = new Radius(14);
            Border = new Thickness(1);
            Shadow = new BoxShadow(0, 10, 28, 0, ColorScheme.ShadowColor.WithAlpha(54));
            ApplyTheme();
        }

        public bool IsOpen => Visible;

        protected internal override bool IsFloatingOverlay => Visible;

        internal void ApplyTheme()
        {
            BackColor = ColorScheme.Surface;
            ForeColor = ColorScheme.ForeColor;
            BorderColor = ColorScheme.Outline.WithAlpha(96);
        }

        internal void SyncFromOwner()
        {
            ApplyTheme();
            _hoverHour = -1;
            _hoverMinute = -1;
            _pressedHour = -1;
            _pressedMinute = -1;
        }

        internal void ShowForOwner()
        {
            var window = _owner.ParentWindow;
            if (window == null)
                return;

            if (!window.Controls.Contains(this))
                window.Controls.Add(this);

            UpdateAnchor();
            Visible = true;
            window.Controls.SetChildIndex(this, window.Controls.Count - 1);
            window.UpdateZOrder();
            ZOrder = Math.Max(ZOrder, GetPopupZOrder());
            BringToFront();
            window.BringToFront(this);
            window.Invalidate();
        }

        private int GetPopupZOrder()
        {
            if (_owner.Parent is ElementBase { IsFloatingOverlay: true } parentPopup)
                return Math.Max(_owner.DropDownZIndex, parentPopup.ZOrder + 1);

            return _owner.DropDownZIndex;
        }

        internal void HideDropDown()
        {
            if (!Visible)
                return;

            Visible = false;
            ParentWindow?.Invalidate();
        }

        internal void UpdateAnchor()
        {
            var window = _owner.ParentWindow;
            if (window == null)
                return;

            var leftTop = GetPreferredAnchor(window);
            var client = window.ClientRectangle;
            var x = Math.Clamp(leftTop.X, 8f, Math.Max(8f, client.Right - Width - 8f));
            var y = leftTop.Y;
            if (y + Height > client.Bottom - 8f)
                y = GetFallbackAnchor(window).Y;
            y = Math.Clamp(y, 8f, Math.Max(8f, client.Bottom - Height - 8f));
            Location = new SKPoint(x, y);
        }

        private SKPoint GetPreferredAnchor(WindowBase window)
        {
            if (_owner.Parent is ElementBase { IsFloatingOverlay: true } parentPopup)
            {
                var parentBounds = GetWindowRelativeBounds(parentPopup);
                return new SKPoint(parentBounds.Right + 8f, parentBounds.Top);
            }

            return window.PointToClient(_owner.PointToScreen(new SKPoint(0, _owner.Height + 6)));
        }

        private SKPoint GetFallbackAnchor(WindowBase window)
        {
            if (_owner.Parent is ElementBase { IsFloatingOverlay: true } parentPopup)
            {
                var parentBounds = GetWindowRelativeBounds(parentPopup);
                return new SKPoint(parentBounds.Right + 8f, parentBounds.Bottom - Height);
            }

            return window.PointToClient(_owner.PointToScreen(new SKPoint(0, -Height - 6)));
        }

        public override void OnPaint(SKCanvas canvas)
        {
            base.OnPaint(canvas);
            DrawHeader(canvas);
            DrawHours(canvas);
            DrawMinutes(canvas);
        }

        internal override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            var hour = HitTestHour(e.Location);
            var minute = HitTestMinute(e.Location);
            if (_hoverHour == hour && _hoverMinute == minute)
                return;

            _hoverHour = hour;
            _hoverMinute = minute;
            Invalidate();
        }

        internal override void OnMouseLeave(EventArgs e)
        {
            _hoverHour = -1;
            _hoverMinute = -1;
            _pressedHour = -1;
            _pressedMinute = -1;
            base.OnMouseLeave(e);
            Invalidate();
        }

        internal override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button != MouseButtons.Left)
                return;

            var hour = HitTestHour(e.Location);
            if (hour >= 0)
            {
                _pressedHour = hour;
                _pressedMinute = -1;
                _owner.CommitTime(hour, _owner.Value.Minutes);
                if (_owner.Parent is ElementBase { IsFloatingOverlay: true })
                    _owner.HideDropDown();
                e.Handled = true;
                Invalidate();
                return;
            }

            var minute = HitTestMinute(e.Location);
            if (minute >= 0)
            {
                _pressedHour = -1;
                _pressedMinute = minute;
                _owner.CommitTime(_owner.Value.Hours, minute);
                _owner.HideDropDown();
                e.Handled = true;
                Invalidate();
            }
        }

        internal override void OnMouseUp(MouseEventArgs e)
        {
            if (_pressedHour >= 0 || _pressedMinute >= 0)
            {
                _pressedHour = -1;
                _pressedMinute = -1;
                Invalidate();
            }

            base.OnMouseUp(e);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _fillPaint.Dispose();
                _strokePaint.Dispose();
                _textPaint.Dispose();
            }

            base.Dispose(disposing);
        }

        private void DrawHeader(SKCanvas canvas)
        {
            using var titleFont = new SKFont(SKTypeface.Default, 16f * ScaleFactor) { Embolden = true };
            _textPaint.Color = ForeColor;
            TextRenderer.DrawText(canvas, _owner.Text, SKRect.Create(14, 10, Width - 28, 30), _textPaint, titleFont, ContentAlignment.MiddleCenter, false, true);
        }

        private void DrawHours(SKCanvas canvas)
        {
            DrawGroupTitle(canvas, "Hour", GetHourRect());
            using var font = new SKFont(SKTypeface.Default, 13.5f * ScaleFactor);
            var rect = GetHourRect();
            var cellW = rect.Width / 4f;
            var cellH = (rect.Height - 26f) / 6f;

            for (var hour = 0; hour < 24; hour++)
            {
                var cell = SKRect.Create(rect.Left + (hour % 4) * cellW + 3, rect.Top + 26f + (hour / 4) * cellH + 3, cellW - 6, cellH - 6);
                DrawCell(canvas, cell, hour.ToString("00", CultureInfo.CurrentCulture), hour == _owner.Value.Hours, hour == _hoverHour, hour == _pressedHour, font);
            }
        }

        private void DrawMinutes(SKCanvas canvas)
        {
            DrawGroupTitle(canvas, "Minute", GetMinuteRect());
            using var font = new SKFont(SKTypeface.Default, 13.5f * ScaleFactor);
            var rect = GetMinuteRect();
            var minutes = 60 / _owner.MinuteStep;
            var columns = 3;
            var rows = (int)Math.Ceiling(minutes / (float)columns);
            var cellW = rect.Width / columns;
            var cellH = (rect.Height - 26f) / rows;

            for (var i = 0; i < minutes; i++)
            {
                var minute = i * _owner.MinuteStep;
                var cell = SKRect.Create(rect.Left + (i % columns) * cellW + 3, rect.Top + 26f + (i / columns) * cellH + 3, cellW - 6, cellH - 6);
                DrawCell(canvas, cell, minute.ToString("00", CultureInfo.CurrentCulture), minute == _owner.Value.Minutes, minute == _hoverMinute, minute == _pressedMinute, font);
            }
        }

        private void DrawGroupTitle(SKCanvas canvas, string text, SKRect rect)
        {
            using var font = new SKFont(SKTypeface.Default, 12.5f * ScaleFactor) { Embolden = true };
            _textPaint.Color = ForeColor.WithAlpha(128);
            TextRenderer.DrawText(canvas, text, SKRect.Create(rect.Left, rect.Top, rect.Width, 22), _textPaint, font, ContentAlignment.MiddleLeft, false, true);
        }

        private void DrawCell(SKCanvas canvas, SKRect rect, string text, bool selected, bool hovered, bool pressed, SKFont font)
        {
            if (selected || hovered || pressed)
            {
                _fillPaint.Color = selected
                    ? ColorScheme.Primary
                    : pressed
                        ? ColorScheme.Primary.WithAlpha(46)
                        : ColorScheme.Primary.WithAlpha(24);
                canvas.DrawRoundRect(rect, 8, 8, _fillPaint);
            }

            _textPaint.Color = selected ? SKColors.White : (hovered || pressed ? ColorScheme.Primary : ForeColor).WithAlpha(220);
            TextRenderer.DrawText(canvas, text, rect, _textPaint, font, ContentAlignment.MiddleCenter, false, true);
        }

        private SKRect GetHourRect() => SKRect.Create(14, 50, 168, Height - 64);
        private SKRect GetMinuteRect() => SKRect.Create(196, 50, Width - 210, Height - 64);

        private int HitTestHour(SKPoint point)
        {
            var rect = GetHourRect();
            var grid = SKRect.Create(rect.Left, rect.Top + 26f, rect.Width, rect.Height - 26f);
            if (!grid.Contains(point))
                return -1;

            var column = (int)((point.X - grid.Left) / (grid.Width / 4f));
            var row = (int)((point.Y - grid.Top) / (grid.Height / 6f));
            var hour = row * 4 + column;
            return hour is >= 0 and < 24 ? hour : -1;
        }

        private int HitTestMinute(SKPoint point)
        {
            var rect = GetMinuteRect();
            var grid = SKRect.Create(rect.Left, rect.Top + 26f, rect.Width, rect.Height - 26f);
            if (!grid.Contains(point))
                return -1;

            var minutes = 60 / _owner.MinuteStep;
            var columns = 3;
            var rows = (int)Math.Ceiling(minutes / (float)columns);
            var column = (int)((point.X - grid.Left) / (grid.Width / columns));
            var row = (int)((point.Y - grid.Top) / (grid.Height / rows));
            var index = row * columns + column;
            if (index < 0 || index >= minutes)
                return -1;

            return index * _owner.MinuteStep;
        }
    }
}
