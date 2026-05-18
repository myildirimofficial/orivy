using Orivy.Animation;
using Orivy.Helpers;
using Orivy.Layout;
using SkiaSharp;
using System;
using System.ComponentModel;

namespace Orivy.Controls;

public class TrackBar : ElementBase
{
    private readonly AnimationManager _valueAnimation;
    private readonly SKPaint _trackPaint = new() { IsAntialias = true, Style = SKPaintStyle.Fill };
    private readonly SKPaint _valuePaint = new() { IsAntialias = true, Style = SKPaintStyle.Fill };
    private readonly SKPaint _thumbPaint = new() { IsAntialias = true, Style = SKPaintStyle.Fill };
    private readonly SKPaint _thumbBorderPaint = new() { IsAntialias = true, Style = SKPaintStyle.Stroke };
    private readonly SKPaint _textPaint = new() { IsAntialias = true, Style = SKPaintStyle.Fill };

    private float _minimum;
    private float _maximum = 100f;
    private float _value;
    private float _displayValue;
    private float _animationFrom;
    private float _animationTo;
    private bool _dragging;

    public TrackBar()
    {
        AutoSize = false;
        CanSelect = true;
        TabStop = true;
        UseDefaultPointerVisualStates = false;
        Size = new SKSize(220, 42);
        MinimumSize = new SKSize(72, 28);
        Padding = new Thickness(12, 10, 12, 10);
        Border = new Thickness(0);
        Radius = new Radius(10);
        BackColor = SKColors.Transparent;
        ForeColor = ColorScheme.Primary;

        _valueAnimation = new AnimationManager(true)
        {
            AnimationType = AnimationType.CubicEaseOut,
            InterruptAnimation = true,
            Increment = 16d / 140d,
            SecondaryIncrement = 16d / 120d
        };
        _valueAnimation.OnAnimationProgress += HandleValueAnimationProgress;
        _valueAnimation.OnAnimationFinished += HandleValueAnimationFinished;

        ConfigureVisualStyles(styles => styles
            .DefaultTransition(TimeSpan.FromMilliseconds(140), AnimationType.CubicEaseOut)
            .Base(baseStyle => baseStyle
                .Background(SKColors.Transparent)
                .Foreground(ColorScheme.Primary)
                .Border(0)
                .Radius(10)
                .Shadow(BoxShadow.None))
            .OnHover(rule => rule.Background(ColorScheme.Primary.WithAlpha(10)))
            .OnPressed(rule => rule.Background(ColorScheme.Primary.WithAlpha(18)).Scale(0.995f))
            .OnFocused(rule => rule.Background(ColorScheme.Primary.WithAlpha(14)))
            .OnDisabled(rule => rule.Foreground(ColorScheme.Outline).Opacity(0.72f)));

        ConfigureMotionEffects(scene => scene
            .Circle(effect => effect
                .Anchor(0.5f, 0.5f)
                .Size(28f, 28f)
                .Orbit(3f, 1f)
                .Color(ColorScheme.Primary.WithAlpha(10))
                .Opacity(0.01f, 0.06f)
                .Scale(0.9f, 1.08f)
                .Duration(4.8d)
                .SpeedOnHover(1.6f)
                .SpeedOnPressed(2.4f)));
    }

    [DefaultValue(0f)]
    public float Minimum
    {
        get => _minimum;
        set
        {
            if (Math.Abs(_minimum - value) < 0.001f)
                return;

            _minimum = value;
            if (_maximum < _minimum)
                _maximum = _minimum;
            Value = Math.Clamp(_value, _minimum, _maximum);
            Invalidate();
        }
    }

    [DefaultValue(100f)]
    public float Maximum
    {
        get => _maximum;
        set
        {
            var normalized = Math.Max(_minimum, value);
            if (Math.Abs(_maximum - normalized) < 0.001f)
                return;

            _maximum = normalized;
            Value = Math.Clamp(_value, _minimum, _maximum);
            Invalidate();
        }
    }

    [DefaultValue(0f)]
    public float Value
    {
        get => _value;
        set => SetValue(value, animate: !_dragging, raiseChanged: true);
    }

    [DefaultValue(1f)]
    public float Step { get; set; } = 1f;

    [DefaultValue(false)]
    public bool ShowValue { get; set; }

    public event EventHandler? ValueChanged;

    public override void OnPaint(SKCanvas canvas)
    {
        base.OnPaint(canvas);

        var rect = DisplayRectangle;
        if (rect.Width <= 0f || rect.Height <= 0f)
            return;

        var track = GetTrackRect(rect);
        var fraction = GetFraction(_displayValue);
        var filled = new SKRect(track.Left, track.Top, track.Left + track.Width * fraction, track.Bottom);
        var thumbCenter = new SKPoint(filled.Right, track.MidY);
        var thumbRadius = GetThumbRadius();

        _trackPaint.Color = ColorScheme.SurfaceContainerHigh;
        _valuePaint.Color = Enabled ? ForeColor : ColorScheme.Outline;
        _thumbPaint.Color = Enabled ? ColorScheme.Surface : ColorScheme.SurfaceVariant;
        _thumbBorderPaint.Color = Enabled ? ForeColor : ColorScheme.Outline;
        _thumbBorderPaint.StrokeWidth = Math.Max(2f, 2f * ScaleFactor);

        canvas.DrawRoundRect(track, track.Height * 0.5f, track.Height * 0.5f, _trackPaint);
        if (filled.Width > 0f)
            canvas.DrawRoundRect(filled, track.Height * 0.5f, track.Height * 0.5f, _valuePaint);

        _trackPaint.Color = ForeColor.WithAlpha(28);
        canvas.DrawCircle(thumbCenter, thumbRadius + 4f * ScaleFactor, _trackPaint);
        canvas.DrawCircle(thumbCenter, thumbRadius, _thumbPaint);
        canvas.DrawCircle(thumbCenter, thumbRadius - _thumbBorderPaint.StrokeWidth * 0.5f, _thumbBorderPaint);

        if (ShowValue)
            DrawValue(canvas, rect);
    }

    internal override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        if (!Enabled || e.Button != MouseButtons.Left)
            return;

        _dragging = true;
        GetParentWindow()?.SetMouseCapture(this);
        UpdateValueFromPoint(e.Location);
    }

    internal override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        if (_dragging)
            UpdateValueFromPoint(e.Location);
    }

    internal override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);
        if (e.Button != MouseButtons.Left)
            return;

        _dragging = false;
        GetParentWindow()?.ReleaseMouseCapture(this);
        SetValue(_value, animate: true, raiseChanged: false);
    }

    internal override void OnLostFocus(EventArgs e)
    {
        _dragging = false;
        GetParentWindow()?.ReleaseMouseCapture(this);
        base.OnLostFocus(e);
    }

    internal override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Handled || !Enabled)
            return;

        if (e.KeyCode == Keys.Left || e.KeyCode == Keys.Down)
        {
            Value -= Step;
            e.Handled = true;
        }
        else if (e.KeyCode == Keys.Right || e.KeyCode == Keys.Up)
        {
            Value += Step;
            e.Handled = true;
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _valueAnimation.OnAnimationProgress -= HandleValueAnimationProgress;
            _valueAnimation.OnAnimationFinished -= HandleValueAnimationFinished;
            _valueAnimation.Dispose();
            _trackPaint.Dispose();
            _valuePaint.Dispose();
            _thumbPaint.Dispose();
            _thumbBorderPaint.Dispose();
            _textPaint.Dispose();
        }

        base.Dispose(disposing);
    }

    private void SetValue(float value, bool animate, bool raiseChanged)
    {
        var normalized = Snap(Math.Clamp(value, Minimum, Maximum));
        if (Math.Abs(_value - normalized) < 0.001f)
            return;

        _value = normalized;
        if (animate)
            StartValueAnimation(normalized);
        else
        {
            _displayValue = normalized;
            Invalidate();
        }

        if (raiseChanged)
            ValueChanged?.Invoke(this, EventArgs.Empty);
    }

    private void UpdateValueFromPoint(SKPoint point)
    {
        var track = GetTrackRect(DisplayRectangle);
        var fraction = track.Width <= 0f ? 0f : Math.Clamp((point.X - track.Left) / track.Width, 0f, 1f);
        SetValue(Minimum + (Maximum - Minimum) * fraction, animate: false, raiseChanged: true);
    }

    private void StartValueAnimation(float nextValue)
    {
        _animationFrom = _displayValue;
        _animationTo = nextValue;
        _valueAnimation.SetProgress(0d);
        _valueAnimation.StartNewAnimation(AnimationDirection.In);
        Invalidate();
    }

    private void HandleValueAnimationProgress(object _)
    {
        var progress = Math.Clamp((float)_valueAnimation.GetProgress(), 0f, 1f);
        _displayValue = _animationFrom + ((_animationTo - _animationFrom) * progress);
        Invalidate();
    }

    private void HandleValueAnimationFinished(object _)
    {
        _displayValue = _animationTo;
        Invalidate();
    }

    private SKRect GetTrackRect(SKRect rect)
    {
        var thumb = GetThumbRadius();
        var trackHeight = Math.Max(6f, 8f * ScaleFactor);
        var valueWidth = ShowValue ? GetValueLabelWidth() : 0f;
        var left = rect.Left + Padding.Left + thumb;
        var right = rect.Right - Padding.Right - thumb - valueWidth;
        var centerY = rect.MidY;
        return new SKRect(left, centerY - trackHeight * 0.5f, Math.Max(left, right), centerY + trackHeight * 0.5f);
    }

    private void DrawValue(SKCanvas canvas, SKRect rect)
    {
        using var font = CreateRenderFont(Font);
        var textRect = new SKRect(rect.Right - GetValueLabelWidth() + 4f * ScaleFactor, rect.Top, rect.Right - Padding.Right, rect.Bottom);
        _textPaint.Color = Enabled ? ColorScheme.ForeColor.WithAlpha(180) : ColorScheme.Outline;
        TextRenderer.DrawText(canvas, $"{_displayValue:0}", textRect, _textPaint, font, ContentAlignment.MiddleRight, false, false);
    }

    private float GetFraction(float value)
    {
        var range = Math.Max(0.001f, Maximum - Minimum);
        return Math.Clamp((value - Minimum) / range, 0f, 1f);
    }

    private float Snap(float value)
    {
        if (Step <= 0f)
            return value;

        return Minimum + MathF.Round((value - Minimum) / Step) * Step;
    }

    private float GetThumbRadius() => Math.Max(8f, 10f * ScaleFactor);

    private float GetValueLabelWidth() => Math.Max(36f, 42f * ScaleFactor);
}
