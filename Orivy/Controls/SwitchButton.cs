using Orivy.Animation;
using Orivy.Helpers;
using Orivy.Layout;
using SkiaSharp;
using System;
using System.ComponentModel;

namespace Orivy.Controls;

public enum SwitchButtonTransitionMode
{
    Slide,
    SoftElastic,
    Bounce,
    Stretch,
    Snap,
    Fade
}

public enum SwitchButtonToggleArea
{
    FullControl,
    SwitchOnly,
    ThumbOnly
}

public class SwitchButton : ElementBase
{
    private const float DefaultSwitchWidth = 46f;
    private const float DefaultSwitchHeight = 26f;
    private const float DefaultTextGap = 10f;

    private readonly AnimationManager _thumbAnimation;
    private readonly AnimationManager _pressAnimation;
    private readonly SKPaint _trackPaint = new() { IsAntialias = true, Style = SKPaintStyle.Fill };
    private readonly SKPaint _borderPaint = new() { IsAntialias = true, Style = SKPaintStyle.Stroke };
    private readonly SKPaint _thumbPaint = new() { IsAntialias = true, Style = SKPaintStyle.Fill };
    private readonly SKPaint _textPaint = new() { IsAntialias = true, Style = SKPaintStyle.Fill };

    private bool _checked;
    private bool _keyboardPressArmed;
    private bool _toggleArmedByPointer = true;
    private bool _toggleRequestedByKeyboard;
    private SwitchButtonTransitionMode _transitionMode = SwitchButtonTransitionMode.SoftElastic;
    private TimeSpan _transitionDuration = TimeSpan.FromMilliseconds(210);

    public SwitchButton()
    {
        AutoSize = true;
        CommonProperties.SetSelfAutoSizeInDefaultLayout(this, true);
        AutoSizeMode = AutoSizeMode.GrowOnly;
        AutoEllipsis = true;
        WrapMode = TextWrap.None;
        CanSelect = true;
        TabStop = true;
        UseDefaultPointerVisualStates = false;
        MinimumSize = new SKSize(46, 26);
        Size = new SKSize(122, 30);
        Padding = new Thickness(0);
        Border = new Thickness(0);
        Radius = new Radius(16);
        BackColor = SKColors.Transparent;
        ForeColor = ColorScheme.ForeColor;
        TextAlign = ContentAlignment.MiddleLeft;

        _thumbAnimation = new AnimationManager(true)
        {
            AnimationType = AnimationType.Linear,
            InterruptAnimation = true,
            Increment = 16d / 210d,
            SecondaryIncrement = 16d / 180d
        };
        _thumbAnimation.OnAnimationProgress += HandleThumbAnimationProgress;
        _thumbAnimation.OnAnimationFinished += HandleThumbAnimationFinished;

        _pressAnimation = new AnimationManager(true)
        {
            AnimationType = AnimationType.CubicEaseOut,
            InterruptAnimation = true,
            Increment = 16d / 95d,
            SecondaryIncrement = 16d / 135d
        };
        _pressAnimation.OnAnimationProgress += HandleThumbAnimationProgress;
        _pressAnimation.OnAnimationFinished += HandleThumbAnimationFinished;

        ConfigureVisualStyles(styles => styles
            .DefaultTransition(TimeSpan.FromMilliseconds(120), AnimationType.CubicEaseOut)
            .Base(baseStyle => baseStyle
                .Background(SKColors.Transparent)
                .Foreground(ColorScheme.ForeColor)
                .Border(0)
                .Radius(16)
                .Shadow(BoxShadow.None))
            .OnHover(rule => rule.Background(ColorScheme.Primary.WithAlpha(8)))
            .OnPressed(rule => rule.Background(ColorScheme.Primary.WithAlpha(14)).Scale(0.985f))
            .OnDisabled(rule => rule.Foreground(ColorScheme.Outline).Opacity(0.72f)),
            clearExisting: true);

        ConfigureMotionEffects(scene => scene
            .Circle(effect => effect
                .Anchor(0.26f, 0.5f)
                .Size(30f, 30f)
                .Orbit(2f, 1f)
                .Color(ColorScheme.Primary.WithAlpha(10))
                .Opacity(0.01f, 0.06f)
                .Scale(0.92f, 1.08f)
                .Duration(4.2d)
                .SpeedOnHover(1.6f)
                .SpeedOnPressed(2.3f)
                .SpeedOnFocused(1.7f)));
    }

    [DefaultValue(false)]
    public bool Checked
    {
        get => _checked;
        set
        {
            if (_checked == value)
                return;

            _checked = value;
            StartThumbAnimation();
            RefreshVisualStylesForStateChange();
            CheckedChanged?.Invoke(this, EventArgs.Empty);
            Invalidate();
        }
    }

    public SKColor OnColor { get; set; } = ColorScheme.Primary;

    public SKColor OffColor { get; set; } = ColorScheme.SurfaceContainerHigh;

    public SKColor ThumbColor { get; set; } = ColorScheme.Surface;

    public SKColor ThumbCheckedColor { get; set; } = SKColors.White;

    [DefaultValue(SwitchButtonTransitionMode.SoftElastic)]
    public SwitchButtonTransitionMode TransitionMode
    {
        get => _transitionMode;
        set
        {
            if (_transitionMode == value)
                return;

            _transitionMode = value;
            ConfigureThumbAnimation();
            Invalidate();
        }
    }

    [DefaultValue(typeof(TimeSpan), "00:00:00.2100000")]
    public TimeSpan TransitionDuration
    {
        get => _transitionDuration;
        set
        {
            var normalized = value <= TimeSpan.Zero ? TimeSpan.FromMilliseconds(1) : value;
            if (_transitionDuration == normalized)
                return;

            _transitionDuration = normalized;
            ConfigureThumbAnimation();
        }
    }

    [DefaultValue(SwitchButtonToggleArea.FullControl)]
    public SwitchButtonToggleArea ToggleArea { get; set; } = SwitchButtonToggleArea.FullControl;

    public event EventHandler? CheckedChanged;

    protected override bool ShouldRenderDefaultText => false;

    protected override bool GetVisualCheckedState() => Checked;

    public override void OnClick(EventArgs e)
    {
        if (_toggleRequestedByKeyboard || _toggleArmedByPointer)
            Checked = !Checked;

        _toggleRequestedByKeyboard = false;
        _toggleArmedByPointer = ToggleArea == SwitchButtonToggleArea.FullControl;
        base.OnClick(e);
    }

    public override SKSize GetPreferredSize(SKSize proposedSize)
    {
        using var font = CreateRenderFont(Font);
        var switchSize = GetScaledSwitchSize();
        var textSize = string.IsNullOrEmpty(Text)
            ? SKSize.Empty
            : TextRenderer.MeasureText(
                Text,
                font,
                proposedSize.Width <= 1f ? new SKSize(short.MaxValue, short.MaxValue) : proposedSize,
                new TextRenderOptions
                {
                    MaxWidth = proposedSize.Width <= 1f ? short.MaxValue : proposedSize.Width,
                    Trimming = AutoEllipsis ? TextTrimming.CharacterEllipsis : TextTrimming.None,
                    UseMnemonic = UseMnemonic,
                    Wrap = TextWrap.None
                });

        var gap = textSize.Width > 0f ? DefaultTextGap * ScaleFactor : 0f;
        var width = Padding.Left + Padding.Right + Border.Left + Border.Right + switchSize.Width + gap + textSize.Width;
        var height = Padding.Top + Padding.Bottom + Border.Top + Border.Bottom + Math.Max(switchSize.Height, textSize.Height);

        if (AutoSizeMode == AutoSizeMode.GrowOnly)
        {
            width = Math.Max(width, Size.Width);
            height = Math.Max(height, Size.Height);
        }

        if (MinimumSize.Width > 0)
            width = Math.Max(width, MinimumSize.Width);
        if (MinimumSize.Height > 0)
            height = Math.Max(height, MinimumSize.Height);
        if (MaximumSize.Width > 0)
            width = Math.Min(width, MaximumSize.Width);
        if (MaximumSize.Height > 0)
            height = Math.Min(height, MaximumSize.Height);

        return new SKSize((float)Math.Ceiling(width), (float)Math.Ceiling(height));
    }

    public override void OnPaint(SKCanvas canvas)
    {
        base.OnPaint(canvas);

        var content = DisplayRectangle;
        if (content.Width <= 0f || content.Height <= 0f)
            return;

        var switchRect = GetSwitchRect(content);
        DrawSwitchTrack(canvas, switchRect);
        DrawSwitchThumb(canvas, switchRect);
        DrawSwitchText(canvas, content, switchRect);
    }

    internal override void OnMouseDown(MouseEventArgs e)
    {
        _toggleArmedByPointer = IsPointInToggleArea(e.Location);
        if (_toggleArmedByPointer && e.Button == MouseButtons.Left)
            _pressAnimation.StartNewAnimation(AnimationDirection.In);

        base.OnMouseDown(e);
    }

    internal override void OnMouseUp(MouseEventArgs e)
    {
        if (e.Button == MouseButtons.Left)
            _pressAnimation.StartNewAnimation(AnimationDirection.Out);

        base.OnMouseUp(e);
    }

    internal override void OnMouseLeave(EventArgs e)
    {
        _pressAnimation.StartNewAnimation(AnimationDirection.Out);
        base.OnMouseLeave(e);
    }

    internal override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);

        if (e.Handled || !Enabled)
            return;

        if (e.KeyCode != Keys.Enter && e.KeyCode != Keys.Space)
            return;

        _keyboardPressArmed = true;
        e.Handled = true;
    }

    internal override void OnKeyUp(KeyEventArgs e)
    {
        base.OnKeyUp(e);

        if (!_keyboardPressArmed)
            return;

        if (e.KeyCode != Keys.Enter && e.KeyCode != Keys.Space)
            return;

        _keyboardPressArmed = false;
        e.Handled = true;

        if (Enabled && Visible)
        {
            _toggleRequestedByKeyboard = true;
            PerformClick();
        }
    }

    internal override void OnLostFocus(EventArgs e)
    {
        _keyboardPressArmed = false;
        _pressAnimation.StartNewAnimation(AnimationDirection.Out);
        base.OnLostFocus(e);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _thumbAnimation.OnAnimationProgress -= HandleThumbAnimationProgress;
            _thumbAnimation.OnAnimationFinished -= HandleThumbAnimationFinished;
            _pressAnimation.OnAnimationProgress -= HandleThumbAnimationProgress;
            _pressAnimation.OnAnimationFinished -= HandleThumbAnimationFinished;
            _thumbAnimation.Dispose();
            _pressAnimation.Dispose();
            _trackPaint.Dispose();
            _borderPaint.Dispose();
            _thumbPaint.Dispose();
            _textPaint.Dispose();
        }

        base.Dispose(disposing);
    }

    private void StartThumbAnimation()
    {
        ConfigureThumbAnimation();

        if (TransitionMode == SwitchButtonTransitionMode.Snap)
        {
            _thumbAnimation.SetProgress(Checked ? 1d : 0d);
            Invalidate();
            return;
        }

        _thumbAnimation.StartNewAnimation(Checked ? AnimationDirection.In : AnimationDirection.Out);
    }

    private void HandleThumbAnimationProgress(object _)
    {
        Invalidate();
    }

    private void HandleThumbAnimationFinished(object _)
    {
        Invalidate();
    }

    private SKSize GetScaledSwitchSize()
    {
        return new SKSize(DefaultSwitchWidth * ScaleFactor, DefaultSwitchHeight * ScaleFactor);
    }

    private SKRect GetSwitchRect(SKRect content)
    {
        var size = GetScaledSwitchSize();
        var left = content.Left;
        var top = content.Top + Math.Max(0f, (content.Height - size.Height) * 0.5f);
        return new SKRect(left, top, left + size.Width, top + size.Height);
    }

    private void DrawSwitchTrack(SKCanvas canvas, SKRect rect)
    {
        var progress = GetClampedVisualProgress();
        var offColor = Enabled ? OffColor : ColorScheme.SurfaceVariant;
        var onColor = Enabled ? OnColor : ColorScheme.Outline;
        _trackPaint.Color = offColor.InterpolateColor(onColor, progress);
        canvas.DrawRoundRect(rect, rect.Height * 0.5f, rect.Height * 0.5f, _trackPaint);

        _borderPaint.Color = Enabled
            ? ColorScheme.Outline.WithAlpha((byte)(Checked ? 40 : 115))
            : ColorScheme.Outline.WithAlpha(70);
        _borderPaint.StrokeWidth = Math.Max(1f, ScaleFactor);
        canvas.DrawRoundRect(InflateRect(rect, -_borderPaint.StrokeWidth * 0.5f), rect.Height * 0.5f, rect.Height * 0.5f, _borderPaint);
    }

    private void DrawSwitchThumb(SKCanvas canvas, SKRect rect)
    {
        var rawProgress = (float)_thumbAnimation.GetProgress();
        var visualProgress = GetElasticVisualProgress(rawProgress);
        var clamped = Math.Clamp(visualProgress, 0f, 1f);
        var phase = GetAnimationPhase(rawProgress);
        var progress = Math.Clamp(visualProgress, -0.22f, 1.22f);
        var padding = 3f * ScaleFactor;
        var diameter = Math.Max(10f, rect.Height - padding * 2f);
        var travel = Math.Max(0f, rect.Width - diameter - padding * 2f);
        var centerX = rect.Left + padding + diameter * 0.5f + travel * progress;
        var centerY = rect.MidY;
        var overshoot = Math.Abs(visualProgress - clamped);
        var settle = _thumbAnimation.Running && IsElasticTransition()
            ? MathF.Abs(MathF.Sin(phase * MathF.PI * (TransitionMode == SwitchButtonTransitionMode.Bounce ? 5.6f : 4.2f))) * MathF.Pow(Math.Clamp(1f - phase, 0f, 1f), 0.55f)
            : 0f;
        var stretch = TransitionMode == SwitchButtonTransitionMode.Fade
            ? 0f
            : Math.Min(6.5f * ScaleFactor, (overshoot * 24f + settle * 3.3f) * ScaleFactor);
        var squash = TransitionMode == SwitchButtonTransitionMode.Fade
            ? 0f
            : Math.Min(2.4f * ScaleFactor, (overshoot * 11f + settle * 1.2f) * ScaleFactor);
        var pressProgress = Math.Clamp((float)_pressAnimation.GetProgress(), 0f, 1f);
        if (pressProgress > 0.001f)
        {
            var pressStretch = TransitionMode == SwitchButtonTransitionMode.Stretch ? 8.8f : 6.2f;
            stretch += pressStretch * pressProgress * ScaleFactor;
            squash += 1.45f * pressProgress * ScaleFactor;
        }

        var thumbRect = new SKRect(
            centerX - diameter * 0.5f - stretch,
            centerY - diameter * 0.5f + squash,
            centerX + diameter * 0.5f + stretch,
            centerY + diameter * 0.5f - squash);

        _thumbPaint.Color = (Enabled ? ThumbColor : ColorScheme.Surface).InterpolateColor(
            Enabled ? ThumbCheckedColor : ColorScheme.SurfaceVariant,
            GetClampedVisualProgress());

        if (_thumbAnimation.Running && IsElasticTransition())
        {
            var pulseAlpha = (byte)Math.Clamp(22f * (1f - phase), 0f, 22f);
            _trackPaint.Color = OnColor.WithAlpha(pulseAlpha);
            canvas.DrawRoundRect(InflateRect(thumbRect, 4f * ScaleFactor), thumbRect.Height * 0.5f + 4f * ScaleFactor, thumbRect.Height * 0.5f + 4f * ScaleFactor, _trackPaint);
        }

        _trackPaint.Color = ColorScheme.ShadowColor.WithAlpha(Checked ? (byte)34 : (byte)22);
        canvas.DrawRoundRect(OffsetRect(thumbRect, 0f, 1f * ScaleFactor), thumbRect.Height * 0.5f, thumbRect.Height * 0.5f, _trackPaint);
        canvas.DrawRoundRect(thumbRect, thumbRect.Height * 0.5f, thumbRect.Height * 0.5f, _thumbPaint);
    }

    private void DrawSwitchText(SKCanvas canvas, SKRect content, SKRect switchRect)
    {
        if (string.IsNullOrEmpty(Text))
            return;

        var gap = DefaultTextGap * ScaleFactor;
        var textRect = new SKRect(
            switchRect.Right + gap,
            content.Top,
            content.Right,
            content.Bottom);
        if (textRect.Width <= 0f || textRect.Height <= 0f)
            return;

        using var font = CreateRenderFont(Font);
        _textPaint.Color = Enabled ? ForeColor : ColorScheme.Outline;
        TextRenderer.DrawText(canvas, Text, textRect, _textPaint, font, TextAlign, AutoEllipsis, UseMnemonic, WrapMode);
    }

    private float GetClampedVisualProgress()
    {
        return Math.Clamp(GetElasticVisualProgress((float)_thumbAnimation.GetProgress()), 0f, 1f);
    }

    private float GetAnimationPhase(float rawProgress)
    {
        return Math.Clamp(Checked ? rawProgress : 1f - rawProgress, 0f, 1f);
    }

    private float GetElasticVisualProgress(float rawProgress)
    {
        if (!_thumbAnimation.Running)
            return rawProgress;

        if (TransitionMode == SwitchButtonTransitionMode.Fade)
            return Checked ? 1f : 0f;

        if (!IsElasticTransition())
            return rawProgress;

        var phase = GetAnimationPhase(rawProgress);
        var decay = MathF.Pow(Math.Clamp(1f - phase, 0f, 1f), TransitionMode == SwitchButtonTransitionMode.Bounce ? 1.08f : 1.35f);
        var wave = TransitionMode == SwitchButtonTransitionMode.Bounce ? 5.4f : 3.6f;
        var amount = TransitionMode == SwitchButtonTransitionMode.Stretch ? 0.045f : TransitionMode == SwitchButtonTransitionMode.Bounce ? 0.09f : 0.07f;
        var spring = MathF.Sin(phase * MathF.PI * wave) * decay * amount;
        return rawProgress + (Checked ? spring : -spring);
    }

    private bool IsElasticTransition()
    {
        return TransitionMode == SwitchButtonTransitionMode.SoftElastic
            || TransitionMode == SwitchButtonTransitionMode.Bounce
            || TransitionMode == SwitchButtonTransitionMode.Stretch;
    }

    private bool IsPointInToggleArea(SKPoint point)
    {
        if (ToggleArea == SwitchButtonToggleArea.FullControl)
            return true;

        var switchRect = GetSwitchRect(DisplayRectangle);
        if (ToggleArea == SwitchButtonToggleArea.SwitchOnly)
            return switchRect.Contains(point);

        var progress = GetClampedVisualProgress();
        var padding = 3f * ScaleFactor;
        var diameter = Math.Max(10f, switchRect.Height - padding * 2f);
        var travel = Math.Max(0f, switchRect.Width - diameter - padding * 2f);
        var centerX = switchRect.Left + padding + diameter * 0.5f + travel * progress;
        var thumbRect = new SKRect(
            centerX - diameter * 0.5f - 4f * ScaleFactor,
            switchRect.MidY - diameter * 0.5f - 4f * ScaleFactor,
            centerX + diameter * 0.5f + 4f * ScaleFactor,
            switchRect.MidY + diameter * 0.5f + 4f * ScaleFactor);
        return thumbRect.Contains(point);
    }

    private void ConfigureThumbAnimation()
    {
        var milliseconds = Math.Max(1d, TransitionDuration.TotalMilliseconds);
        _thumbAnimation.InterruptAnimation = true;
        _thumbAnimation.AnimationType = TransitionMode switch
        {
            SwitchButtonTransitionMode.Slide => AnimationType.CubicEaseOut,
            SwitchButtonTransitionMode.Snap => AnimationType.Linear,
            SwitchButtonTransitionMode.Fade => AnimationType.CubicEaseInOut,
            SwitchButtonTransitionMode.Stretch => AnimationType.CubicEaseOut,
            SwitchButtonTransitionMode.Bounce => AnimationType.CubicEaseOut,
            _ => AnimationType.CubicEaseOut
        };
        _thumbAnimation.Increment = 16d / milliseconds;
        _thumbAnimation.SecondaryIncrement = 16d / Math.Max(1d, milliseconds * 0.86d);
    }

    private static SKRect InflateRect(SKRect rect, float amount)
    {
        return new SKRect(rect.Left - amount, rect.Top - amount, rect.Right + amount, rect.Bottom + amount);
    }

    private static SKRect OffsetRect(SKRect rect, float x, float y)
    {
        return new SKRect(rect.Left + x, rect.Top + y, rect.Right + x, rect.Bottom + y);
    }
}
