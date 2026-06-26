using SkiaSharp;
using System;
using System.ComponentModel;
using System.Globalization;
using Orivy.Helpers;

namespace Orivy.Controls;

public class DatePicker : ElementBase
{
    private readonly DatePickerDropDown _dropDown;
    private readonly SKPaint _iconPaint = new() { IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeCap = SKStrokeCap.Round, StrokeJoin = SKStrokeJoin.Round };
    private readonly TextBox _textBox;
    private EventHandler? _ownerDeactivateHandler;
    private KeyEventHandler? _ownerKeyDownHandler;
    private MouseEventHandler? _ownerMouseDownHandler;
    private WindowBase? _handlerWindow;
    private DateTime _displayMonth;
    private DateTime _maxDate = DateTime.MaxValue.Date;
    private DateTime _minDate = DateTime.MinValue.Date;
    private DateTime _value = DateTime.Today;
    private string _dateTimeFormat = string.Empty;
    private string _format = "MMM d, yyyy";
    private string _placeholderText = "Select date";
    private string _timeFormat = "HH:mm";
    private int _minuteStep = 5;
    private bool _ownerHandlersAttached;
    private bool _showTimePicker;
    private bool _syncingTextBox;
    private bool _textBoxMode;
    private bool _use24HourClock = true;

    public DatePicker()
    {
        _displayMonth = new DateTime(_value.Year, _value.Month, 1);
        _dropDown = new DatePickerDropDown(this);
        _textBox = CreateHostedTextBox();
        _textBox.TextChanged += HandleHostedTextBoxTextChanged;
        _textBox.LostFocus += HandleHostedTextBoxLostFocus;
        _textBox.KeyDown += HandleHostedTextBoxKeyDown;

        AutoEllipsis = true;
        CanSelect = true;
        MinimumSize = new SKSize(150, 40);
        Padding = new Thickness(14, 0, 46, 0);
        Radius = new Radius(12);
        Size = new SKSize(190, 40);
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
    public DateTime Value
    {
        get => _value;
        set => SetValue(value, true);
    }

    [Category("Data")]
    public DateTime MinDate
    {
        get => _minDate;
        set
        {
            var next = value.Date;
            if (_minDate == next)
                return;

            _minDate = next;
            if (_maxDate < _minDate)
                _maxDate = _minDate;
            SetValue(_value, false);
            Invalidate();
        }
    }

    [Category("Data")]
    public DateTime MaxDate
    {
        get => _maxDate;
        set
        {
            var next = value.Date;
            if (_maxDate == next)
                return;

            _maxDate = next;
            if (_minDate > _maxDate)
                _minDate = _maxDate;
            SetValue(_value, false);
            Invalidate();
        }
    }

    [Category("Appearance")]
    [DefaultValue("MMM d, yyyy")]
    public string Format
    {
        get => _format;
        set
        {
            var next = string.IsNullOrWhiteSpace(value) ? "MMM d, yyyy" : value;
            if (_format == next)
                return;

            _format = next;
            UpdateText();
            InvalidateMeasure();
            Invalidate();
        }
    }

    [Category("Appearance")]
    [DefaultValue("")]
    public string DateTimeFormat
    {
        get => _dateTimeFormat;
        set
        {
            var next = value ?? string.Empty;
            if (_dateTimeFormat == next)
                return;

            _dateTimeFormat = next;
            UpdateText();
            if (TextBoxMode)
                SyncHostedTextBoxText();
            InvalidateMeasure();
            Invalidate();
        }
    }

    [Category("Appearance")]
    [DefaultValue("Select date")]
    public string PlaceholderText
    {
        get => _placeholderText;
        set
        {
            var next = value ?? string.Empty;
            if (_placeholderText == next)
                return;

            _placeholderText = next;
            UpdateText();
            Invalidate();
        }
    }

    [Category("Behavior")]
    [DefaultValue(false)]
    public bool ShowTimePicker
    {
        get => _showTimePicker;
        set
        {
            if (_showTimePicker == value)
                return;

            _showTimePicker = value;
            UpdateText();
            _dropDown.SyncFromOwner();
            _dropDown.Invalidate();
            if (TextBoxMode)
                SyncHostedTextBoxText();
            InvalidateMeasure();
            Invalidate();
        }
    }

    [Category("Appearance")]
    [DefaultValue("HH:mm")]
    public string TimeFormat
    {
        get => _timeFormat;
        set
        {
            var next = string.IsNullOrWhiteSpace(value) ? "HH:mm" : value;
            if (_timeFormat == next)
                return;

            _timeFormat = next;
            UpdateText();
            if (TextBoxMode)
                SyncHostedTextBoxText();
            _dropDown.Invalidate();
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
            TimeFormat = value ? "HH:mm" : "h:mm tt";
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
        if (!Enabled || !Visible || DroppedDown || ParentWindow == null)
            return;

        _displayMonth = new DateTime(_value.Year, _value.Month, 1);
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
        if (!DroppedDown)
            return;

        _dropDown.UpdateAnchor();
        _dropDown.UpdateOwnedPopupAnchor();
    }

    public override void  OnPaint(SKCanvas canvas)
    {
        base.OnPaint(canvas);
        DrawCalendarIcon(canvas);
    }

    public override void  OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        if (!Enabled || e.Button != MouseButtons.Left)
            return;

        ToggleDropDown();
        UpdatePressedState(false);
        e.Handled = true;
    }

    public override void  OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Handled || !Enabled)
            return;

        if (HandleDropDownKeyDown(e))
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
        }
    }

    public override void  OnVisibleChanged(EventArgs e)
    {
        if (!Visible)
            HideDropDown();

        base.OnVisibleChanged(e);
    }

    public override void  OnEnabledChanged(EventArgs e)
    {
        if (!Enabled)
            HideDropDown();

        base.OnEnabledChanged(e);
    }

    public override void  OnLocationChanged(EventArgs e)
    {
        base.OnLocationChanged(e);
        _dropDown.UpdateAnchor();
    }

    public override void  OnSizeChanged(EventArgs e)
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

    internal DateTime DisplayMonth
    {
        get => _displayMonth;
        set
        {
            _displayMonth = new DateTime(value.Year, value.Month, 1);
            _dropDown.Invalidate();
        }
    }

    internal bool IsSelectable(DateTime date)
    {
        var day = date.Date;
        return day >= _minDate && day <= _maxDate;
    }

    internal void CommitDate(DateTime date)
    {
        if (!IsSelectable(date))
            return;

        SetValue(date.Date.Add(_value.TimeOfDay), true);
        if (!ShowTimePicker)
            HideDropDown();
    }

    internal void CommitTime(int hour, int minute)
    {
        var totalMinutes = (hour * 60) + minute;
        var dayMinutes = 24 * 60;
        totalMinutes %= dayMinutes;
        if (totalMinutes < 0)
            totalMinutes += dayMinutes;

        SetValue(_value.Date.Add(TimeSpan.FromMinutes(totalMinutes)), true);
    }

    private void SetValue(DateTime value, bool raiseChanged)
    {
        var time = ShowTimePicker ? value.TimeOfDay : TimeSpan.Zero;
        var nextDate = value.Date;
        if (nextDate < _minDate)
            nextDate = _minDate;
        if (nextDate > _maxDate)
            nextDate = _maxDate;

        var minute = (int)time.TotalMinutes % 60;
        minute = (minute / _minuteStep) * _minuteStep;
        var next = nextDate.Add(new TimeSpan(time.Hours, minute, 0));

        if (_value == next)
            return;

        _value = next;
        _displayMonth = new DateTime(_value.Year, _value.Month, 1);
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
        base.Text = _value == default
            ? _placeholderText
            : _value.ToString(GetDisplayFormat(), CultureInfo.CurrentCulture);
    }

    private string GetDisplayFormat()
    {
        return ShowTimePicker && !string.IsNullOrWhiteSpace(_dateTimeFormat)
            ? _dateTimeFormat
            : ShowTimePicker
                ? $"{_format} {_timeFormat}"
                : _format;
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
            Name = "datePickerTextBox",
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
        _textBox.Text = _value.ToString(GetDisplayFormat(), CultureInfo.CurrentCulture);
        _syncingTextBox = false;
    }

    private void CommitHostedTextBox()
    {
        if (!TextBoxMode || _syncingTextBox)
            return;

        if (TryParseTextValue(_textBox.Text, out var parsed))
            Value = parsed;
        else
            SyncHostedTextBoxText();
    }

    private void HandleHostedTextBoxTextChanged(object? sender, EventArgs e)
    {
        if (_syncingTextBox || !TextBoxMode)
            return;

        if (TryParseTextValue(_textBox.Text, out var parsed))
            SetValue(parsed, true);
    }

    private bool TryParseTextValue(string text, out DateTime value)
    {
        var formats = ShowTimePicker && !string.IsNullOrWhiteSpace(_dateTimeFormat)
            ? new[] { _dateTimeFormat, $"{_format} {_timeFormat}", _format }
            : ShowTimePicker
                ? new[] { $"{_format} {_timeFormat}", _format }
                : new[] { _format };

        return DateTime.TryParseExact(text, formats, CultureInfo.CurrentCulture, DateTimeStyles.AllowWhiteSpaces, out value)
            || DateTime.TryParse(text, CultureInfo.CurrentCulture, DateTimeStyles.AllowWhiteSpaces, out value);
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

    private void DrawCalendarIcon(SKCanvas canvas)
    {
        var scale = ScaleFactor;
        var rect = SKRect.Create(Width - 33f * scale, (Height - 17f * scale) / 2f, 17f * scale, 17f * scale);
        _iconPaint.Color = Enabled ? ForeColor.WithAlpha((byte)(DroppedDown ? 230 : 168)) : ForeColor.WithAlpha(90);
        _iconPaint.StrokeWidth = Math.Max(1.35f, 1.35f * scale);
        canvas.DrawRoundRect(rect, 3f * scale, 3f * scale, _iconPaint);
        canvas.DrawLine(rect.Left, rect.Top + 5f * scale, rect.Right, rect.Top + 5f * scale, _iconPaint);
        canvas.DrawLine(rect.Left + 4.5f * scale, rect.Top - 2f * scale, rect.Left + 4.5f * scale, rect.Top + 2f * scale, _iconPaint);
        canvas.DrawLine(rect.Right - 4.5f * scale, rect.Top - 2f * scale, rect.Right - 4.5f * scale, rect.Top + 2f * scale, _iconPaint);
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
            HandleDropDownKeyDown(e);
        };

        _handlerWindow = window;
        _handlerWindow.MouseDown += _ownerMouseDownHandler;
        _handlerWindow.Deactivate += _ownerDeactivateHandler;
        _handlerWindow.KeyDown += _ownerKeyDownHandler;
        _ownerHandlersAttached = true;
    }

    private bool HandleDropDownKeyDown(KeyEventArgs e)
    {
        if (!DroppedDown || e.Handled || !Enabled)
            return false;

        switch (e.KeyCode)
        {
            case Keys.Escape:
                HideDropDown();
                e.Handled = true;
                return true;
            case Keys.Enter:
                HideDropDown();
                e.Handled = true;
                return true;
            case Keys.Left:
                MoveDropDownSelection(_value.AddDays(-1));
                e.Handled = true;
                return true;
            case Keys.Right:
                MoveDropDownSelection(_value.AddDays(1));
                e.Handled = true;
                return true;
            case Keys.Up:
                MoveDropDownSelection(_value.AddDays(-7));
                e.Handled = true;
                return true;
            case Keys.Down:
                MoveDropDownSelection(_value.AddDays(7));
                e.Handled = true;
                return true;
            case Keys.PageUp:
                MoveDropDownSelection(_value.AddMonths(-1));
                e.Handled = true;
                return true;
            case Keys.PageDown:
                MoveDropDownSelection(_value.AddMonths(1));
                e.Handled = true;
                return true;
            case Keys.Home:
                MoveDropDownSelection(new DateTime(_value.Year, _value.Month, 1).Add(_value.TimeOfDay));
                e.Handled = true;
                return true;
            case Keys.End:
                var lastDay = DateTime.DaysInMonth(_value.Year, _value.Month);
                MoveDropDownSelection(new DateTime(_value.Year, _value.Month, lastDay).Add(_value.TimeOfDay));
                e.Handled = true;
                return true;
        }

        return false;
    }

    private void MoveDropDownSelection(DateTime value)
    {
        SetValue(value, true);
        _dropDown.Invalidate();
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
        if (!ownerBounds.Contains(e.Location) && !popupBounds.Contains(e.Location) && !_dropDown.ContainsOwnedPopupPoint(e.Location))
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

    private sealed class DatePickerDropDown : ElementBase
    {
        private readonly DatePicker _owner;
        private readonly TimePicker _timePicker;
        private readonly SKPaint _fillPaint = new() { IsAntialias = true, Style = SKPaintStyle.Fill };
        private readonly SKPaint _strokePaint = new() { IsAntialias = true, Style = SKPaintStyle.Stroke };
        private readonly SKPaint _textPaint = new() { IsAntialias = true, Style = SKPaintStyle.Fill };
        private int _hoverDayIndex = -1;
        private PickerButtonPart _hoverPart;
        private PickerButtonPart _pressedPart;
        private bool _syncingTimePicker;

        public DatePickerDropDown(DatePicker owner)
        {
            _owner = owner;
            _timePicker = new TimePicker
            {
                Visible = false,
                Size = new SKSize(264, 40),
                Location = new SKPoint(14, 318),
                TextBoxMode = false,
                Shadow = BoxShadow.None
            };
            _timePicker.ValueChanged += HandleTimePickerValueChanged;
            Visible = false;
            AutoSize = false;
            CanSelect = false;
            Size = new SKSize(292, 316);
            Padding = new Thickness(12);
            Radius = new Radius(14);
            Border = new Thickness(1);
            Shadow = new BoxShadow(0, 10, 28, 0, ColorScheme.ShadowColor.WithAlpha(54));
            ApplyTheme();
            Controls.Add(_timePicker);
        }

        public bool IsOpen => Visible;

        protected internal override bool IsFloatingOverlay => Visible;

        internal void ApplyTheme()
        {
            BackColor = ColorScheme.Surface;
            ForeColor = ColorScheme.ForeColor;
            BorderColor = ColorScheme.Outline.WithAlpha(96);
            _timePicker.BackColor = ColorScheme.SurfaceContainer.WithAlpha(150);
            _timePicker.ForeColor = ForeColor;
            _timePicker.BorderColor = ColorScheme.Outline.WithAlpha(84);
            _timePicker.Shadow = BoxShadow.None;
        }

        internal void SyncFromOwner()
        {
            ApplyTheme();
            Size = new SKSize(292, 316);
            _timePicker.Visible = false;
            _timePicker.Location = new SKPoint(14, Height - 54);
            _timePicker.Size = new SKSize(Width - 28, 40);
            _syncingTimePicker = true;
            try
            {
                _timePicker.Use24HourClock = _owner.Use24HourClock;
                _timePicker.Format = _owner.TimeFormat;
                _timePicker.MinuteStep = _owner.MinuteStep;
                _timePicker.DropDownZIndex = Math.Max(_owner.DropDownZIndex + 1, _owner.DropDownZIndex);
                _timePicker.Value = _owner.Value.TimeOfDay;
            }
            finally
            {
                _syncingTimePicker = false;
            }
            _hoverDayIndex = -1;
            _hoverPart = PickerButtonPart.None;
            _pressedPart = PickerButtonPart.None;
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
            ZOrder = Math.Max(ZOrder, _owner.DropDownZIndex);
            BringToFront();
            window.BringToFront(this);
            if (_owner.ShowTimePicker)
                _timePicker.ShowDropDown();
            window.Invalidate();
        }

        public override void  OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if (!e.Handled)
                _owner.HandleDropDownKeyDown(e);
        }

        internal void HideDropDown()
        {
            if (!Visible)
                return;

            _timePicker.HideDropDown();
            Visible = false;
            ParentWindow?.Invalidate();
        }

        internal void UpdateAnchor()
        {
            if (!Visible && ParentWindow == null)
                return;

            var window = _owner.ParentWindow;
            if (window == null)
                return;

            var leftTop = window.PointToClient(_owner.PointToScreen(new SKPoint(0, _owner.Height + 6)));
            var client = window.ClientRectangle;
            var x = Math.Clamp(leftTop.X, 8f, Math.Max(8f, client.Right - Width - 8f));
            var y = leftTop.Y;
            if (y + Height > client.Bottom - 8f)
                y = window.PointToClient(_owner.PointToScreen(new SKPoint(0, -Height - 6))).Y;
            y = Math.Clamp(y, 8f, Math.Max(8f, client.Bottom - Height - 8f));
            Location = new SKPoint(x, y);
        }

        public override void  OnPaint(SKCanvas canvas)
        {
            base.OnPaint(canvas);
            DrawHeader(canvas);
            DrawWeekdays(canvas);
            DrawDays(canvas);
        }

        public override void  OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            var next = HitTestDay(e.Location);
            var part = HitTestButtonPart(e.Location);
            if (_hoverDayIndex == next && _hoverPart == part)
                return;

            _hoverDayIndex = next;
            _hoverPart = part;
            Invalidate();
        }

        public override void  OnMouseLeave(EventArgs e)
        {
            _hoverDayIndex = -1;
            _hoverPart = PickerButtonPart.None;
            _pressedPart = PickerButtonPart.None;
            base.OnMouseLeave(e);
            Invalidate();
        }

        public override void  OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Handled)
                return;

            if (e.Button != MouseButtons.Left)
                return;

            _pressedPart = HitTestButtonPart(e.Location);
            if (_pressedPart != PickerButtonPart.None)
                Invalidate();

            if (_pressedPart == PickerButtonPart.PreviousMonth)
            {
                _owner.DisplayMonth = _owner.DisplayMonth.AddMonths(-1);
                e.Handled = true;
                return;
            }

            if (_pressedPart == PickerButtonPart.NextMonth)
            {
                _owner.DisplayMonth = _owner.DisplayMonth.AddMonths(1);
                e.Handled = true;
                return;
            }

            var dayIndex = HitTestDay(e.Location);
            if (dayIndex >= 0)
            {
                _owner.CommitDate(GetDateForIndex(dayIndex));
                e.Handled = true;
            }
        }

        public override void  OnMouseUp(MouseEventArgs e)
        {
            if (_pressedPart != PickerButtonPart.None)
            {
                _pressedPart = PickerButtonPart.None;
                Invalidate();
            }

            base.OnMouseUp(e);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _timePicker.ValueChanged -= HandleTimePickerValueChanged;
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
            TextRenderer.DrawText(canvas, _owner.DisplayMonth.ToString("MMMM yyyy", CultureInfo.CurrentCulture), SKRect.Create(44, 12, Width - 88, 32), _textPaint, titleFont, ContentAlignment.MiddleCenter, false, true);
            DrawNavButton(canvas, GetPrevRect(), false, PickerButtonPart.PreviousMonth);
            DrawNavButton(canvas, GetNextRect(), true, PickerButtonPart.NextMonth);
        }

        private void DrawNavButton(SKCanvas canvas, SKRect rect, bool next, PickerButtonPart part)
        {
            var pressed = _pressedPart == part;
            var hovered = _hoverPart == part;
            _fillPaint.Color = pressed
                ? ColorScheme.Primary.WithAlpha(40)
                : hovered
                    ? ColorScheme.Primary.WithAlpha(22)
                    : ColorScheme.SurfaceContainer.WithAlpha(170);
            canvas.DrawRoundRect(rect, 8, 8, _fillPaint);
            _strokePaint.Color = hovered || pressed ? ColorScheme.Primary.WithAlpha(150) : ColorScheme.Outline.WithAlpha(70);
            _strokePaint.StrokeWidth = 1;
            canvas.DrawRoundRect(rect, 8, 8, _strokePaint);
            _strokePaint.Color = (hovered || pressed ? ColorScheme.Primary : ForeColor).WithAlpha(190);
            _strokePaint.StrokeWidth = 1.55f;
            var cx = rect.MidX + (next ? 0.8f : -0.8f);
            var cy = rect.MidY;
            var sign = next ? 1f : -1f;
            canvas.DrawLine(cx - sign * 3.5f, cy - 5f, cx + sign * 2.2f, cy, _strokePaint);
            canvas.DrawLine(cx + sign * 2.2f, cy, cx - sign * 3.5f, cy + 5f, _strokePaint);
        }

        private void DrawWeekdays(SKCanvas canvas)
        {
            using var font = new SKFont(SKTypeface.Default, 12.5f * ScaleFactor) { Embolden = true };
            var names = CultureInfo.CurrentCulture.DateTimeFormat.AbbreviatedDayNames;
            var first = (int)CultureInfo.CurrentCulture.DateTimeFormat.FirstDayOfWeek;
            var grid = GetGridRect();
            var cellW = grid.Width / 7f;

            _textPaint.Color = ForeColor.WithAlpha(128);
            for (var i = 0; i < 7; i++)
            {
                var text = names[(first + i) % 7];
                var rect = SKRect.Create(grid.Left + i * cellW, 58, cellW, 24);
                TextRenderer.DrawText(canvas, text, rect, _textPaint, font, ContentAlignment.MiddleCenter, false, true);
            }
        }

        private void DrawDays(SKCanvas canvas)
        {
            using var font = new SKFont(SKTypeface.Default, 14f * ScaleFactor);
            var grid = GetGridRect();
            var cellW = grid.Width / 7f;
            var cellH = grid.Height / 6f;
            var today = DateTime.Today;

            for (var i = 0; i < 42; i++)
            {
                var date = GetDateForIndex(i);
                var rect = SKRect.Create(grid.Left + (i % 7) * cellW + 3, grid.Top + (i / 7) * cellH + 3, cellW - 6, cellH - 6);
                var currentMonth = date.Month == _owner.DisplayMonth.Month;
                var selected = date.Date == _owner.Value.Date;
                var todayMatch = date == today;
                var enabled = _owner.IsSelectable(date);
                var hovered = i == _hoverDayIndex && enabled;

                if (selected || hovered)
                {
                    _fillPaint.Color = selected
                        ? ColorScheme.Primary
                        : ColorScheme.Primary.WithAlpha(24);
                    canvas.DrawRoundRect(rect, 9, 9, _fillPaint);
                }
                else if (todayMatch)
                {
                    _strokePaint.Color = ColorScheme.Primary.WithAlpha(150);
                    _strokePaint.StrokeWidth = 1.2f;
                    canvas.DrawRoundRect(rect, 9, 9, _strokePaint);
                }

                _textPaint.Color = selected
                    ? SKColors.White
                    : enabled
                        ? ForeColor.WithAlpha(currentMonth ? (byte)230 : (byte)105)
                        : ForeColor.WithAlpha(62);
                TextRenderer.DrawText(canvas, date.Day.ToString(CultureInfo.CurrentCulture), rect, _textPaint, font, ContentAlignment.MiddleCenter, false, true);
            }
        }

        private SKRect GetPrevRect() => SKRect.Create(14, 13, 30, 30);
        private SKRect GetNextRect() => SKRect.Create(Width - 44, 13, 30, 30);
        private SKRect GetGridRect() => SKRect.Create(14, 84, Width - 28, Height - 98);

        private int HitTestDay(SKPoint point)
        {
            var grid = GetGridRect();
            if (!grid.Contains(point))
                return -1;

            var column = (int)((point.X - grid.Left) / (grid.Width / 7f));
            var row = (int)((point.Y - grid.Top) / (grid.Height / 6f));
            var index = row * 7 + column;
            return index is >= 0 and < 42 ? index : -1;
        }

        private PickerButtonPart HitTestButtonPart(SKPoint point)
        {
            if (GetPrevRect().Contains(point))
                return PickerButtonPart.PreviousMonth;
            if (GetNextRect().Contains(point))
                return PickerButtonPart.NextMonth;

            return PickerButtonPart.None;
        }

        internal bool ContainsOwnedPopupPoint(SKPoint point)
        {
            return _timePicker.ContainsDropDownWindowPoint(point);
        }

        internal void UpdateOwnedPopupAnchor()
        {
            _timePicker.UpdateDropDownAnchor();
        }

        private void HandleTimePickerValueChanged(object? sender, EventArgs e)
        {
            if (_syncingTimePicker)
                return;

            _owner.CommitTime(_timePicker.Value.Hours, _timePicker.Value.Minutes);
            _owner.HideDropDown();
        }

        private DateTime GetDateForIndex(int index)
        {
            var firstOfMonth = _owner.DisplayMonth;
            var firstDay = (int)CultureInfo.CurrentCulture.DateTimeFormat.FirstDayOfWeek;
            var offset = ((int)firstOfMonth.DayOfWeek - firstDay + 7) % 7;
            return firstOfMonth.AddDays(index - offset);
        }

        private enum PickerButtonPart
        {
            None,
            PreviousMonth,
            NextMonth
        }
    }
}
