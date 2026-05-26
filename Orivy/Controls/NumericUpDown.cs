using Orivy.Animation;
using Orivy.Helpers;
using SkiaSharp;
using System;
using System.ComponentModel;
using System.Globalization;
using System.Timers;

namespace Orivy.Controls;

public class NumericUpDown : ElementBase
{
    private enum ButtonPart
    {
        None,
        Up,
        Down
    }

    private readonly AnimationManager _textAnimation;
    private readonly SKPaint _fillPaint = new() { IsAntialias = true, Style = SKPaintStyle.Fill };
    private readonly SKPaint _borderPaint = new() { IsAntialias = true, Style = SKPaintStyle.Stroke };
    private readonly SKPaint _textPaint = new() { IsAntialias = true, Style = SKPaintStyle.Fill };
    private readonly SKPaint _glyphPaint = new() { IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeCap = SKStrokeCap.Round, StrokeJoin = SKStrokeJoin.Round };
    private readonly Timer _repeatTimer;
    private readonly TextBox _textBox;

    private decimal _minimum;
    private decimal _maximum = 100m;
    private decimal _value;
    private decimal _increment = 1m;
    private string _format = string.Empty;
    private string _prefix = string.Empty;
    private string _suffix = string.Empty;
    private string _previousText = "0";
    private string _currentText = "0";
    private int _decimalPlaces;
    private int _direction = 1;
    private int _repeatTicks;
    private int _repeatDelay = 220;
    private int _repeatInterval = 48;
    private ButtonPart _pressedPart;
    private ButtonPart _hoverPart;
    private ElementBase? _focusBeforeStepper;
    private bool _restoreFocusAfterStepper;
    private bool _stepperMouseDown;
    private bool _suppressNextStepperClick;
    private bool _thousandsSeparator;
    private bool _mouseWheelEnabled = true;
    private bool _wrapValue;
    private bool _repeatButtonEnabled = true;
    private bool _repeatAcceleration = true;
    private bool _mouseOverControl;
    private bool _textBoxMode;
    private bool _syncingTextBox;
    private NumericUpDownAnimationMode _animationMode = NumericUpDownAnimationMode.Slide;
    private NumericUpDownButtonVisibility _buttonVisibility = NumericUpDownButtonVisibility.Always;

    public NumericUpDown()
    {
        AutoSize = false;
        CanSelect = true;
        TabStop = true;
        UseDefaultPointerVisualStates = true;
        Size = new SKSize(138, 38);
        MinimumSize = new SKSize(84, 32);
        Padding = new Thickness(12, 0, 34, 0);
   
        TextAlign = ContentAlignment.MiddleLeft;

        _textAnimation = new AnimationManager(true)
        {
            AnimationType = AnimationType.CubicEaseOut,
            InterruptAnimation = true,
            Increment = 16d / 180d,
            SecondaryIncrement = 16d / 160d
        };
        _textAnimation.OnAnimationProgress += HandleTextAnimationProgress;
        _textAnimation.OnAnimationFinished += HandleTextAnimationFinished;

        _repeatTimer = new Timer { AutoReset = false, Interval = _repeatDelay };
        _repeatTimer.Elapsed += HandleRepeatTimerElapsed;

        _textBox = CreateHostedTextBox();
        _textBox.TextChanged += HandleHostedTextBoxTextChanged;
        _textBox.LostFocus += HandleHostedTextBoxLostFocus;
        Controls.Add(_textBox);

        ConfigureVisualStyles(styles => styles
            .DefaultTransition(TimeSpan.FromMilliseconds(140), AnimationType.CubicEaseOut)
            .Base(baseStyle => baseStyle
                .Background(ColorScheme.Surface)
                .Foreground(ColorScheme.ForeColor)
                .Border(1)
                .BorderColor(ColorScheme.Outline.WithAlpha(120))
                .Radius(10)
                .Shadow(BoxShadow.None))
            /*.OnHover(rule => rule
                .Background(ColorScheme.SurfaceContainerHigh)
                .BorderColor(ColorScheme.Primary.WithAlpha(120)))*/
            .OnPressed(rule => rule.Scale(0.995f))
            .OnFocused(rule => rule
                .BorderColor(ColorScheme.Primary)
                .Shadow(new BoxShadow(0, 0, 3, 3, ColorScheme.Primary.WithAlpha(42))))
            .OnDisabled(rule => rule
                .Background(ColorScheme.SurfaceVariant)
                .Foreground(ColorScheme.Outline)
                .Opacity(0.72f)));

        ConfigureMotionEffects(scene => scene
            .Rectangle(effect => effect
                .Anchor(0.78f, 0.5f)
                .Size(30f, 24f)
                .Drift(2f, 0f)
                .CornerRadius(8f)
                .Color(ColorScheme.Primary.WithAlpha(10))
                .Opacity(0.01f, 0.06f)
                .Scale(0.9f, 1.08f)
                .Duration(4.8d)
                .SpeedOnHover(1.5f)
                .SpeedOnFocused(1.8f)));
    }

    [DefaultValue(typeof(decimal), "0")]
    public decimal Minimum
    {
        get => _minimum;
        set
        {
            if (_minimum == value)
                return;

            _minimum = value;
            if (_maximum < _minimum)
                _maximum = _minimum;
            Value = Clamp(_value);
            Invalidate();
        }
    }

    [DefaultValue(typeof(decimal), "100")]
    public decimal Maximum
    {
        get => _maximum;
        set
        {
            var normalized = Math.Max(_minimum, value);
            if (_maximum == normalized)
                return;

            _maximum = normalized;
            Value = Clamp(_value);
            Invalidate();
        }
    }

    [DefaultValue(typeof(decimal), "0")]
    public decimal Value
    {
        get => _value;
        set => SetValue(value, raiseChanged: true);
    }

    [DefaultValue(typeof(decimal), "1")]
    public decimal Increment
    {
        get => _increment;
        set => _increment = value <= 0m ? 1m : value;
    }

    [DefaultValue("")]
    public string Format
    {
        get => _format;
        set
        {
            var normalized = value ?? string.Empty;
            if (_format == normalized)
                return;

            _format = normalized;
            RefreshCurrentText();
        }
    }

    [DefaultValue("")]
    public string Prefix
    {
        get => _prefix;
        set
        {
            var normalized = value ?? string.Empty;
            if (_prefix == normalized)
                return;

            _prefix = normalized;
            RefreshCurrentText();
        }
    }

    [DefaultValue("")]
    public string Suffix
    {
        get => _suffix;
        set
        {
            var normalized = value ?? string.Empty;
            if (_suffix == normalized)
                return;

            _suffix = normalized;
            RefreshCurrentText();
        }
    }

    [DefaultValue(0)]
    public int DecimalPlaces
    {
        get => _decimalPlaces;
        set
        {
            var normalized = Math.Clamp(value, 0, 10);
            if (_decimalPlaces == normalized)
                return;

            _decimalPlaces = normalized;
            RefreshCurrentText();
        }
    }

    [DefaultValue(false)]
    public bool ThousandsSeparator
    {
        get => _thousandsSeparator;
        set
        {
            if (_thousandsSeparator == value)
                return;

            _thousandsSeparator = value;
            RefreshCurrentText();
        }
    }

    [DefaultValue(true)]
    public bool MouseWheelEnabled
    {
        get => _mouseWheelEnabled;
        set => _mouseWheelEnabled = value;
    }

    [DefaultValue(false)]
    public bool WrapValue
    {
        get => _wrapValue;
        set => _wrapValue = value;
    }

    [DefaultValue(NumericUpDownButtonVisibility.Always)]
    public NumericUpDownButtonVisibility ButtonVisibility
    {
        get => _buttonVisibility;
        set
        {
            if (_buttonVisibility == value)
                return;

            _buttonVisibility = value;
            Invalidate();
        }
    }

    [DefaultValue(true)]
    public bool RepeatButtonEnabled
    {
        get => _repeatButtonEnabled;
        set => _repeatButtonEnabled = value;
    }

    [DefaultValue(220)]
    public int RepeatDelay
    {
        get => _repeatDelay;
        set
        {
            _repeatDelay = Math.Max(80, value);
            if (!_repeatTimer.Enabled)
                _repeatTimer.Interval = _repeatDelay;
        }
    }

    [DefaultValue(48)]
    public int RepeatInterval
    {
        get => _repeatInterval;
        set => _repeatInterval = Math.Max(16, value);
    }

    [DefaultValue(true)]
    public bool RepeatAcceleration
    {
        get => _repeatAcceleration;
        set => _repeatAcceleration = value;
    }

    [DefaultValue(false)]
    public bool TextBoxMode
    {
        get => _textBoxMode;
        set
        {
            if (_textBoxMode == value)
                return;

            if (_textBoxMode && !value)
                CommitHostedTextBox();

            _textBoxMode = value;
            _textBox.Visible = _textBoxMode;
            if (_textBoxMode)
                SyncHostedTextBoxText();
            UpdateHostedTextBoxBounds();
            Invalidate();
        }
    }

    [DefaultValue(NumericUpDownAnimationMode.Slide)]
    public NumericUpDownAnimationMode AnimationMode
    {
        get => _animationMode;
        set
        {
            if (_animationMode == value)
                return;

            _animationMode = value;
            Invalidate();
        }
    }

    public event EventHandler? ValueChanged;

    protected override bool ShouldRenderDefaultText => false;

    protected override bool HandlesMouseWheelInput => MouseWheelEnabled;

    public override void OnPaint(SKCanvas canvas)
    {
        base.OnPaint(canvas);

        var rect = ClientRectangle;
        if (rect.Width <= 0f || rect.Height <= 0f)
            return;

        DrawStepperButtons(canvas, rect);
        UpdateHostedTextBoxBounds();
        if (!TextBoxMode)
            DrawAnimatedValue(canvas, GetTextRect(rect));
    }

    internal override void OnMouseEnter(EventArgs e)
    {
        _mouseOverControl = true;
        base.OnMouseEnter(e);
        Invalidate();
    }

    internal override void OnMouseDown(MouseEventArgs e)
    {
        if (!Enabled || e.Button != MouseButtons.Left)
        {
            base.OnMouseDown(e);
            return;
        }

        _pressedPart = HitTest(e.Location);
        if (_pressedPart == ButtonPart.None)
        {
            base.OnMouseDown(e);
            return;
        }

        PressStepper(e, raiseMouseDown: true);
    }

    internal override void OnMouseDoubleClick(MouseEventArgs e)
    {
        if (!Enabled || e.Button != MouseButtons.Left)
        {
            base.OnMouseDoubleClick(e);
            return;
        }

        _pressedPart = HitTest(e.Location);
        if (_pressedPart == ButtonPart.None)
        {
            base.OnMouseDoubleClick(e);
            return;
        }

        PressStepper(e, raiseMouseDown: false);
    }

    internal override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        var hit = HitTest(e.Location);
        if (_hoverPart == hit)
            return;

        _hoverPart = hit;
        Invalidate();
    }

    internal override void OnMouseUp(MouseEventArgs e)
    {
        if (!_stepperMouseDown)
            base.OnMouseUp(e);
        else
            RaiseMouseUp(e);

        if (e.Button != MouseButtons.Left)
            return;

        _stepperMouseDown = false;
        _pressedPart = ButtonPart.None;
        StopRepeatTimer();
        GetParentWindow()?.ReleaseMouseCapture(this);
        RestoreStepperFocus();
        _focusBeforeStepper = null;
        _restoreFocusAfterStepper = false;
        Invalidate();
    }

    internal override void OnMouseLeave(EventArgs e)
    {
        _hoverPart = ButtonPart.None;
        _pressedPart = ButtonPart.None;
        _stepperMouseDown = false;
        _mouseOverControl = false;
        StopRepeatTimer();
        GetParentWindow()?.ReleaseMouseCapture(this);
        base.OnMouseLeave(e);
        Invalidate();
    }

    protected internal override void OnMouseClick(MouseEventArgs e)
    {
        if (_suppressNextStepperClick)
        {
            _suppressNextStepperClick = false;
            RaiseMouseClick(e);
            return;
        }

        base.OnMouseClick(e);
    }

    internal override void OnMouseWheel(MouseEventArgs e)
    {
        if (!Enabled || !MouseWheelEnabled)
        {
            base.OnMouseWheel(e);
            return;
        }

        StepValue(e.Delta > 0 ? Increment : -Increment);
        e.Handled = true;
    }

    internal override void OnKeyDown(KeyEventArgs e)
    {
        if (TryHandleKeyboardStep(e))
            return;

        base.OnKeyDown(e);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _textAnimation.OnAnimationProgress -= HandleTextAnimationProgress;
            _textAnimation.OnAnimationFinished -= HandleTextAnimationFinished;
            _textAnimation.Dispose();
            _repeatTimer.Elapsed -= HandleRepeatTimerElapsed;
            _repeatTimer.Dispose();
            _textBox.TextChanged -= HandleHostedTextBoxTextChanged;
            _textBox.LostFocus -= HandleHostedTextBoxLostFocus;
            _fillPaint.Dispose();
            _borderPaint.Dispose();
            _textPaint.Dispose();
            _glyphPaint.Dispose();
        }

        base.Dispose(disposing);
    }

    private void SetValue(decimal value, bool raiseChanged)
    {
        var normalized = NormalizeValue(value);
        if (_value == normalized)
            return;

        _direction = normalized >= _value ? 1 : -1;
        _previousText = _currentText;
        _value = normalized;
        _currentText = FormatValue(_value);
        if (AnimationMode == NumericUpDownAnimationMode.None)
            _textAnimation.SetProgress(1d);
        else
        {
            _textAnimation.SetProgress(0d);
            _textAnimation.StartNewAnimation(AnimationDirection.In);
        }
        RefreshVisualStylesForStateChange();
        Invalidate();

        if (raiseChanged)
            ValueChanged?.Invoke(this, EventArgs.Empty);

        if (TextBoxMode && !_textBox.Focused)
            SyncHostedTextBoxText();
    }

    private bool TryHandleKeyboardStep(KeyEventArgs e)
    {
        if (e.Handled || !Enabled)
            return false;

        switch (e.KeyCode)
        {
            case Keys.Up:
            case Keys.Right:
            case Keys.Add:
            case Keys.OemPlus:
                StepValue(Increment);
                e.Handled = true;
                return true;

            case Keys.Down:
            case Keys.Left:
            case Keys.Subtract:
            case Keys.OemMinus:
                StepValue(-Increment);
                e.Handled = true;
                return true;

            case Keys.PageUp:
                StepValue(Increment * 10m);
                e.Handled = true;
                return true;

            case Keys.PageDown:
                StepValue(-Increment * 10m);
                e.Handled = true;
                return true;

            case Keys.Home:
                Value = Minimum;
                e.Handled = true;
                return true;

            case Keys.End:
                Value = Maximum;
                e.Handled = true;
                return true;

            default:
                return false;
        }
    }

    private void DrawStepperButtons(SKCanvas canvas, SKRect rect)
    {
        if (!ShouldShowStepperButtons())
            return;

        var buttons = GetButtonsRect(rect);
        var up = new SKRect(buttons.Left, buttons.Top, buttons.Right, buttons.MidY);
        var down = new SKRect(buttons.Left, buttons.MidY, buttons.Right, buttons.Bottom);

        _fillPaint.Color = ColorScheme.SurfaceContainer;
        _borderPaint.Color = ColorScheme.Outline.WithAlpha(80);
        _borderPaint.StrokeWidth = Math.Max(1f, ScaleFactor);

        canvas.DrawRoundRect(buttons, 8f * ScaleFactor, 8f * ScaleFactor, _fillPaint);
        canvas.DrawLine(buttons.Left, buttons.MidY, buttons.Right, buttons.MidY, _borderPaint);

        if (_hoverPart != ButtonPart.None || _pressedPart != ButtonPart.None)
            DrawPartOverlay(canvas, _pressedPart != ButtonPart.None ? _pressedPart : _hoverPart, up, down);

        _glyphPaint.Color = Enabled ? ColorScheme.ForeColor.WithAlpha(180) : ColorScheme.Outline;
        _glyphPaint.StrokeWidth = Math.Max(1.7f, 1.8f * ScaleFactor);
        DrawChevron(canvas, up, up: true);
        DrawChevron(canvas, down, up: false);
    }

    private void DrawPartOverlay(SKCanvas canvas, ButtonPart part, SKRect up, SKRect down)
    {
        var rect = part == ButtonPart.Up ? up : down;
        _fillPaint.Color = ColorScheme.Primary.WithAlpha((byte)(_pressedPart == part ? 34 : 20));
        canvas.DrawRoundRect(rect, 7f * ScaleFactor, 7f * ScaleFactor, _fillPaint);
    }

    private void DrawChevron(SKCanvas canvas, SKRect rect, bool up)
    {
        var cx = rect.MidX;
        var cy = rect.MidY;
        var size = 4.5f * ScaleFactor;
        if (up)
        {
            canvas.DrawLine(cx - size, cy + size * 0.35f, cx, cy - size * 0.35f, _glyphPaint);
            canvas.DrawLine(cx, cy - size * 0.35f, cx + size, cy + size * 0.35f, _glyphPaint);
        }
        else
        {
            canvas.DrawLine(cx - size, cy - size * 0.35f, cx, cy + size * 0.35f, _glyphPaint);
            canvas.DrawLine(cx, cy + size * 0.35f, cx + size, cy - size * 0.35f, _glyphPaint);
        }
    }

    private void DrawAnimatedValue(SKCanvas canvas, SKRect rect)
    {
        using var font = CreateRenderFont(Font);
        var progress = Math.Clamp((float)_textAnimation.GetProgress(), 0f, 1f);

        if (!_textAnimation.IsAnimating())
        {
            _textPaint.Color = Enabled ? ForeColor : ColorScheme.Outline;
            TextRenderer.DrawText(canvas, _currentText, rect, _textPaint, font, TextAlign, AutoEllipsis, UseMnemonic, WrapMode);
            return;
        }

        switch (AnimationMode)
        {
            case NumericUpDownAnimationMode.None:
                DrawTextWithAlpha(canvas, _currentText, rect, font, 1f, 0f, 1f);
                break;
            case NumericUpDownAnimationMode.Fade:
                DrawTextWithAlpha(canvas, _previousText, rect, font, MathF.Max(0f, 1f - progress * 1.35f), 0f, 1f);
                DrawTextWithAlpha(canvas, _currentText, rect, font, 0.25f + (0.75f * progress), 0f, 1f);
                break;
            case NumericUpDownAnimationMode.Scale:
                DrawTextWithAlpha(canvas, _previousText, rect, font, MathF.Max(0f, 1f - progress * 1.4f), 0f, 1f);
                DrawTextWithAlpha(canvas, _currentText, rect, font, 0.35f + (0.65f * progress), 0f, 0.96f + 0.04f * progress);
                break;
            case NumericUpDownAnimationMode.Odometer:
                DrawOdometerText(canvas, rect, font, progress);
                break;
            default:
                var distance = rect.Height * 0.82f * _direction;
                DrawTextWithAlpha(canvas, _previousText, rect, font, MathF.Max(0f, 1f - progress * 1.35f), -distance * progress, 1f);
                DrawTextWithAlpha(canvas, _currentText, rect, font, 0.3f + (0.7f * progress), distance * (1f - progress), 1f);
                break;
        }
    }

    private void DrawTextWithAlpha(SKCanvas canvas, string text, SKRect rect, SKFont font, float alpha, float offsetY, float scale)
    {
        if (alpha <= 0.001f)
            return;

        var save = canvas.Save();
        if (Math.Abs(offsetY) > 0.001f)
            canvas.Translate(0f, offsetY);
        if (Math.Abs(scale - 1f) > 0.001f)
            canvas.Scale(scale, scale, rect.MidX, rect.MidY);

        _textPaint.Color = (Enabled ? ForeColor : ColorScheme.Outline).WithAlpha((byte)Math.Clamp((int)Math.Round(255f * alpha), 0, 255));
        TextRenderer.DrawText(canvas, text, rect, _textPaint, font, TextAlign, AutoEllipsis, UseMnemonic, WrapMode);
        canvas.RestoreToCount(save);
    }

    private ButtonPart HitTest(SKPoint point)
    {
        if (!ShouldShowStepperButtons(allowHoverIntent: true))
            return ButtonPart.None;

        var buttons = GetButtonsRect(ClientRectangle);
        if (!buttons.Contains(point))
            return ButtonPart.None;

        return point.Y <= buttons.MidY ? ButtonPart.Up : ButtonPart.Down;
    }

    private SKRect GetTextRect(SKRect rect)
    {
        var buttons = GetButtonsRect(rect);
        var right = !ShouldShowStepperButtons()
            ? rect.Right - Padding.Right
            : buttons.Left - 6f * ScaleFactor;
        return new SKRect(rect.Left + Padding.Left, rect.Top, right, rect.Bottom);
    }

    private SKRect GetButtonsRect(SKRect rect)
    {
        var width = Math.Max(24f, 28f * ScaleFactor);
        var inset = Math.Max(2f, 3f * ScaleFactor);
        return new SKRect(rect.Right - width - inset, rect.Top + 4f * ScaleFactor, rect.Right - inset, rect.Bottom - 4f * ScaleFactor);
    }

    private decimal NormalizeValue(decimal value)
    {
        if (!WrapValue)
            return Clamp(value);

        if (Maximum <= Minimum)
            return Minimum;

        if (value > Maximum)
            return Minimum;
        if (value < Minimum)
            return Maximum;

        return value;
    }

    private decimal Clamp(decimal value) => Math.Min(Math.Max(value, Minimum), Maximum);

    private string FormatValue(decimal value)
    {
        var format = !string.IsNullOrWhiteSpace(Format)
            ? Format
            : (ThousandsSeparator ? $"N{DecimalPlaces}" : $"F{DecimalPlaces}");
        return $"{Prefix}{value.ToString(format)}{Suffix}";
    }

    private void StepValue(decimal delta) => Value = _value + delta;

    private void PressStepper(MouseEventArgs e, bool raiseMouseDown)
    {
        CommitHostedTextBox();
        _stepperMouseDown = true;
        _suppressNextStepperClick = true;
        _restoreFocusAfterStepper = true;
        _focusBeforeStepper = this;
        GetParentWindow()?.FocusManager.SetFocus(this);

        if (raiseMouseDown)
            RaiseMouseDown(e);

        GetParentWindow()?.SetMouseCapture(this);

        if (_pressedPart == ButtonPart.Up)
            StepValue(Increment);
        else if (_pressedPart == ButtonPart.Down)
            StepValue(-Increment);

        StartRepeatTimer();
        Invalidate();
    }

    private void RefreshCurrentText()
    {
        _previousText = _currentText;
        _currentText = FormatValue(_value);
        Invalidate();
    }

    private bool ShouldShowStepperButtons(bool allowHoverIntent = false)
    {
        return ButtonVisibility switch
        {
            NumericUpDownButtonVisibility.Never => false,
            NumericUpDownButtonVisibility.Always => true,
            NumericUpDownButtonVisibility.Hover => _mouseOverControl || _pressedPart != ButtonPart.None,
            NumericUpDownButtonVisibility.Focused => Focused,
            NumericUpDownButtonVisibility.HoverOrFocused => Focused || _mouseOverControl || _pressedPart != ButtonPart.None,
            _ => true
        };
    }

    private TextBox CreateHostedTextBox()
    {
        var textBox = new TextBox
        {
            Name = "numericUpDownTextBox",
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
        textBox.AutoSize = false;
        textBox.Shadow = BoxShadow.None;
        textBox.MinimumSize = new SKSize(0, 0);
        textBox.Border = new Thickness(0);
        textBox.Radius = new Radius(0);
        textBox.BackColor = SKColors.Transparent;
        textBox.ForeColor = ForeColor;
        textBox.Padding = new Thickness(0);
        return textBox;
    }

    private void UpdateHostedTextBoxBounds()
    {
        if (!TextBoxMode)
            return;

        var rect = GetTextRect(ClientRectangle);
        _textBox.Location = new SKPoint(rect.Left, rect.Top);
        _textBox.Size = new SKSize(Math.Max(0f, rect.Width), Math.Max(0f, rect.Height));
        _textBox.ForeColor = ForeColor;
        _textBox.Font = Font;
    }

    private void SyncHostedTextBoxText()
    {
        _syncingTextBox = true;
        _textBox.Text = _value.ToString(GetEditFormat(), CultureInfo.CurrentCulture);
        _syncingTextBox = false;
    }

    private string GetEditFormat()
    {
        return DecimalPlaces > 0 ? $"F{DecimalPlaces}" : "0.#############################";
    }

    private void CommitHostedTextBox()
    {
        if (!TextBoxMode || _syncingTextBox)
            return;

        if (decimal.TryParse(_textBox.Text, NumberStyles.Number, CultureInfo.CurrentCulture, out var parsed))
            Value = parsed;
        else
            SyncHostedTextBoxText();
    }

    private void HandleHostedTextBoxTextChanged(object? sender, EventArgs e)
    {
        if (_syncingTextBox || !TextBoxMode)
            return;

        if (decimal.TryParse(_textBox.Text, NumberStyles.Number, CultureInfo.CurrentCulture, out var parsed))
            SetValue(parsed, raiseChanged: true);
    }

    private void HandleHostedTextBoxLostFocus(object? sender, EventArgs e)
    {
        CommitHostedTextBox();
    }

    private void StartRepeatTimer()
    {
        if (!RepeatButtonEnabled || _pressedPart == ButtonPart.None)
            return;

        _repeatTicks = 0;
        _repeatTimer.Stop();
        _repeatTimer.Interval = RepeatDelay;
        _repeatTimer.Start();
    }

    private void StopRepeatTimer()
    {
        _repeatTimer.Stop();
        _repeatTicks = 0;
    }

    private void HandleRepeatTimerElapsed(object? sender, ElapsedEventArgs e)
    {
        ExecuteOnUiThread(() =>
        {
            if (!_stepperMouseDown || _pressedPart == ButtonPart.None)
                return;

            StepValue(_pressedPart == ButtonPart.Up ? Increment : -Increment);
            _repeatTicks++;
            var acceleration = RepeatAcceleration ? Math.Min(34, _repeatTicks * 4) : 0;
            _repeatTimer.Interval = Math.Max(24, RepeatInterval - acceleration);
            _repeatTimer.Start();
        });
    }

    private void ExecuteOnUiThread(Action action)
    {
        var window = GetParentWindow();
        if (window == null)
        {
            action();
            return;
        }

        try
        {
            window.BeginInvoke(action);
        }
        catch
        {
            action();
        }
    }

    private void DrawOdometerText(SKCanvas canvas, SKRect rect, SKFont font, float progress)
    {
        var previous = _previousText;
        var current = _currentText;
        if (previous.Length != current.Length)
        {
            var distance = rect.Height * 0.82f * _direction;
                DrawTextWithAlpha(canvas, previous, rect, font, MathF.Max(0f, 1f - progress * 1.35f), -distance * progress, 1f);
                DrawTextWithAlpha(canvas, current, rect, font, 0.3f + (0.7f * progress), distance * (1f - progress), 1f);
            return;
        }

        _textPaint.Color = Enabled ? ForeColor : ColorScheme.Outline;

        var x = rect.Left;
        var save = canvas.Save();
        canvas.ClipRect(rect);
        for (var i = 0; i < current.Length; i++)
        {
            var prev = previous[i].ToString();
            var next = current[i].ToString();
            var width = Math.Max(font.MeasureText(prev), font.MeasureText(next));
            var charRect = new SKRect(x, rect.Top, Math.Min(rect.Right, x + width + 1f), rect.Bottom);
            if (previous[i] == current[i])
            {
                TextRenderer.DrawText(canvas, next, charRect, _textPaint, font, ContentAlignment.MiddleLeft, AutoEllipsis, UseMnemonic, WrapMode);
            }
            else
            {
                var distance = rect.Height * 0.82f * _direction;
                DrawTextWithAlpha(canvas, prev, charRect, font, MathF.Max(0f, 1f - progress * 1.35f), -distance * progress, 1f);
                DrawTextWithAlpha(canvas, next, charRect, font, 0.3f + (0.7f * progress), distance * (1f - progress), 1f);
            }

            x += width;
            if (x >= rect.Right)
                break;
        }
        canvas.RestoreToCount(save);
    }

    private void RestoreStepperFocus()
    {
        if (!_restoreFocusAfterStepper)
            return;

        var window = GetParentWindow();
        if (window != null)
            window.FocusManager.SetFocus(_focusBeforeStepper ?? this);
    }

    private void HandleTextAnimationProgress(object _)
    {
        Invalidate();
    }

    private void HandleTextAnimationFinished(object _)
    {
        Invalidate();
    }
}
