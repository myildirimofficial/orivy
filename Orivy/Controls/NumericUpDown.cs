using Orivy.Animation;
using Orivy.Helpers;
using Orivy.Layout;
using SkiaSharp;
using System;
using System.ComponentModel;

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

    private decimal _minimum;
    private decimal _maximum = 100m;
    private decimal _value;
    private decimal _increment = 1m;
    private string _previousText = "0";
    private string _currentText = "0";
    private int _direction = 1;
    private ButtonPart _pressedPart;
    private ButtonPart _hoverPart;
    private NumericUpDownAnimationMode _animationMode = NumericUpDownAnimationMode.Slide;

    public NumericUpDown()
    {
        AutoSize = false;
        CanSelect = true;
        TabStop = true;
        UseDefaultPointerVisualStates = false;
        Size = new SKSize(138, 38);
        MinimumSize = new SKSize(84, 32);
        Padding = new Thickness(12, 0, 34, 0);
        Border = new Thickness(1);
        Radius = new Radius(10);
        BackColor = ColorScheme.Surface;
        ForeColor = ColorScheme.ForeColor;
        BorderColor = ColorScheme.Outline.WithAlpha(110);
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

        ConfigureVisualStyles(styles => styles
            .DefaultTransition(TimeSpan.FromMilliseconds(140), AnimationType.CubicEaseOut)
            .Base(baseStyle => baseStyle
                .Background(ColorScheme.Surface)
                .Foreground(ColorScheme.ForeColor)
                .Border(1)
                .BorderColor(ColorScheme.Outline.WithAlpha(120))
                .Radius(10)
                .Shadow(BoxShadow.None))
            .OnHover(rule => rule
                .Background(ColorScheme.SurfaceContainer)
                .Border(1)
                .BorderColor(ColorScheme.Primary.WithAlpha(120)))
            .OnPressed(rule => rule.Scale(0.995f))
            .OnFocused(rule => rule
                .Border(1)
                .BorderColor(ColorScheme.Primary)
                .Shadow(new BoxShadow(0, 0, 0, 3, ColorScheme.Primary.WithAlpha(42))))
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

    [DefaultValue("0")]
    public string Format { get; set; } = "0";

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

    public override void OnPaint(SKCanvas canvas)
    {
        base.OnPaint(canvas);

        var rect = ClientRectangle;
        if (rect.Width <= 0f || rect.Height <= 0f)
            return;

        DrawStepperButtons(canvas, rect);
        DrawAnimatedValue(canvas, GetTextRect(rect));
    }

    internal override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        if (!Enabled || e.Button != MouseButtons.Left)
            return;

        _pressedPart = HitTest(e.Location);
        if (_pressedPart != ButtonPart.None)
        {
            Focus();
            GetParentWindow()?.SetMouseCapture(this);
        }

        if (_pressedPart == ButtonPart.Up)
            Value += Increment;
        else if (_pressedPart == ButtonPart.Down)
            Value -= Increment;

        Invalidate();
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
        base.OnMouseUp(e);
        if (e.Button != MouseButtons.Left)
            return;

        _pressedPart = ButtonPart.None;
        GetParentWindow()?.ReleaseMouseCapture(this);
        if (Enabled && Visible)
            Focus();
        Invalidate();
    }

    internal override void OnMouseLeave(EventArgs e)
    {
        _hoverPart = ButtonPart.None;
        _pressedPart = ButtonPart.None;
        GetParentWindow()?.ReleaseMouseCapture(this);
        base.OnMouseLeave(e);
        Invalidate();
    }

    internal override void OnMouseWheel(MouseEventArgs e)
    {
        if (!Enabled)
        {
            base.OnMouseWheel(e);
            return;
        }

        Value += e.Delta > 0 ? Increment : -Increment;
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
            _fillPaint.Dispose();
            _borderPaint.Dispose();
            _textPaint.Dispose();
            _glyphPaint.Dispose();
        }

        base.Dispose(disposing);
    }

    private void SetValue(decimal value, bool raiseChanged)
    {
        var normalized = Clamp(value);
        if (_value == normalized)
            return;

        _direction = normalized >= _value ? 1 : -1;
        _previousText = _currentText;
        _value = normalized;
        _currentText = FormatValue(_value);
        _textAnimation.SetProgress(0d);
        _textAnimation.StartNewAnimation(AnimationDirection.In);
        RefreshVisualStylesForStateChange();
        Invalidate();

        if (raiseChanged)
            ValueChanged?.Invoke(this, EventArgs.Empty);
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
                Value += Increment;
                e.Handled = true;
                return true;

            case Keys.Down:
            case Keys.Left:
            case Keys.Subtract:
            case Keys.OemMinus:
                Value -= Increment;
                e.Handled = true;
                return true;

            default:
                return false;
        }
    }

    private void DrawStepperButtons(SKCanvas canvas, SKRect rect)
    {
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
            case NumericUpDownAnimationMode.Fade:
                DrawTextWithAlpha(canvas, _previousText, rect, font, 1f - progress, 0f, 1f);
                DrawTextWithAlpha(canvas, _currentText, rect, font, progress, 0f, 1f);
                break;
            case NumericUpDownAnimationMode.Scale:
                DrawTextWithAlpha(canvas, _previousText, rect, font, 1f - progress, 0f, 1f);
                DrawTextWithAlpha(canvas, _currentText, rect, font, progress, 0f, 0.92f + 0.08f * progress);
                break;
            default:
                var distance = rect.Height * 0.82f * _direction;
                DrawTextWithAlpha(canvas, _previousText, rect, font, 1f - progress, -distance * progress, 1f);
                DrawTextWithAlpha(canvas, _currentText, rect, font, progress, distance * (1f - progress), 1f);
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
        var buttons = GetButtonsRect(ClientRectangle);
        if (!buttons.Contains(point))
            return ButtonPart.None;

        return point.Y <= buttons.MidY ? ButtonPart.Up : ButtonPart.Down;
    }

    private SKRect GetTextRect(SKRect rect)
    {
        var buttons = GetButtonsRect(rect);
        return new SKRect(rect.Left + Padding.Left, rect.Top, buttons.Left - 6f * ScaleFactor, rect.Bottom);
    }

    private SKRect GetButtonsRect(SKRect rect)
    {
        var width = Math.Max(24f, 28f * ScaleFactor);
        var inset = Math.Max(2f, 3f * ScaleFactor);
        return new SKRect(rect.Right - width - inset, rect.Top + 4f * ScaleFactor, rect.Right - inset, rect.Bottom - 4f * ScaleFactor);
    }

    private decimal Clamp(decimal value) => Math.Min(Math.Max(value, Minimum), Maximum);

    private string FormatValue(decimal value) => value.ToString(Format);

    private void HandleTextAnimationProgress(object _)
    {
        Invalidate();
    }

    private void HandleTextAnimationFinished(object _)
    {
        Invalidate();
    }
}
