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
    Cupertino,
    Material,
    Jelly,
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

public sealed class SwitchButtonRenderEventArgs : EventArgs
{
    internal SwitchButtonRenderEventArgs(SKCanvas canvas, SwitchButton switchButton, SKRect bounds, SKRect trackBounds, float progress, float pressProgress)
    {
        Canvas = canvas;
        SwitchButton = switchButton;
        Bounds = bounds;
        TrackBounds = trackBounds;
        Progress = progress;
        PressProgress = pressProgress;
    }

    public SKCanvas Canvas { get; }

    public SwitchButton SwitchButton { get; }

    public SKRect Bounds { get; }

    public SKRect TrackBounds { get; }

    public float Progress { get; }

    public float PressProgress { get; }

    public bool Handled { get; set; }
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
    private bool _pointerPressActive;
    private bool _pointerPressCheckedState;
    private bool _suppressNextPointerClick;
    private bool _toggleArmedByPointer = true;
    private bool _toggleRequestedByKeyboard;
    private bool _useThemeColors = true;
    private bool _customOnColor;
    private bool _customOffColor;
    private bool _customThumbColor;
    private bool _customThumbCheckedColor;
    private SKColor _onColor;
    private SKColor _offColor;
    private SKColor _thumbColor;
    private SKColor _thumbCheckedColor;
    private SwitchButtonTransitionMode _transitionMode = SwitchButtonTransitionMode.Cupertino;
    private TimeSpan _transitionDuration = TimeSpan.FromMilliseconds(210);
    private float _elasticity = 0.04f;
    private float _pressStretch = 1f;

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
            Increment = 16d / 180d,
            SecondaryIncrement = 16d / 170d
        };
        _thumbAnimation.OnAnimationProgress += HandleThumbAnimationProgress;
        _thumbAnimation.OnAnimationFinished += HandleThumbAnimationFinished;

        _pressAnimation = new AnimationManager(true)
        {
            AnimationType = AnimationType.CubicEaseOut,
            InterruptAnimation = true,
            Increment = 16d / 85d,
            SecondaryIncrement = 16d / 170d
        };
        _pressAnimation.OnAnimationProgress += HandleThumbAnimationProgress;
        _pressAnimation.OnAnimationFinished += HandleThumbAnimationFinished;

        ApplyThemeColors();

        ConfigureVisualStyles(styles => styles
            .DefaultTransition(TimeSpan.FromMilliseconds(120), AnimationType.CubicEaseOut)
            .Base(baseStyle => baseStyle
                .Background(SKColors.Transparent)
                .Foreground(ColorScheme.ForeColor)
                .Border(0)
                .Radius(16)
                .Shadow(BoxShadow.None))
            .OnHover(rule => rule.Foreground(ColorScheme.ForeColor))
            .OnPressed(rule => rule.Foreground(ColorScheme.ForeColor))
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

        ColorScheme.ThemeChanged += HandleThemeChanged;
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

    public SKColor OnColor
    {
        get => _onColor;
        set
        {
            if (_onColor == value)
                return;

            _customOnColor = true;
            _onColor = value;
            Invalidate();
        }
    }

    public SKColor OffColor
    {
        get => _offColor;
        set
        {
            if (_offColor == value)
                return;

            _customOffColor = true;
            _offColor = value;
            Invalidate();
        }
    }

    public SKColor ThumbColor
    {
        get => _thumbColor;
        set
        {
            if (_thumbColor == value)
                return;

            _customThumbColor = true;
            _thumbColor = value;
            Invalidate();
        }
    }

    public SKColor ThumbCheckedColor
    {
        get => _thumbCheckedColor;
        set
        {
            if (_thumbCheckedColor == value)
                return;

            _customThumbCheckedColor = true;
            _thumbCheckedColor = value;
            Invalidate();
        }
    }

    [DefaultValue(true)]
    public bool UseThemeColors
    {
        get => _useThemeColors;
        set
        {
            if (_useThemeColors == value)
                return;

            _useThemeColors = value;
            if (value)
                ApplyThemeColors();
            Invalidate();
        }
    }

    [DefaultValue(SwitchButtonTransitionMode.Cupertino)]
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

    [Browsable(false)]
    public Func<float, float>? AnimationFunction { get; set; }

    [DefaultValue(0.04f)]
    public float Elasticity
    {
        get => _elasticity;
        set
        {
            var normalized = Math.Clamp(value, 0f, 0.16f);
            if (Math.Abs(_elasticity - normalized) < 0.0001f)
                return;

            _elasticity = normalized;
            Invalidate();
        }
    }

    [DefaultValue(1f)]
    public float PressStretch
    {
        get => _pressStretch;
        set
        {
            var normalized = Math.Clamp(value, 0f, 2.5f);
            if (Math.Abs(_pressStretch - normalized) < 0.0001f)
                return;

            _pressStretch = normalized;
            Invalidate();
        }
    }

    public event EventHandler? CheckedChanged;

    public event EventHandler<SwitchButtonRenderEventArgs>? SwitchRender;

    protected override bool ShouldRenderDefaultText => false;

    protected override bool GetVisualCheckedState() => Checked;

    public override void  OnClick(EventArgs e)
    {
        if (_suppressNextPointerClick)
        {
            _suppressNextPointerClick = false;
            base.OnClick(e);
            return;
        }

        if (_toggleRequestedByKeyboard)
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

    public override void  OnPaint(SKCanvas canvas)
    {
        base.OnPaint(canvas);

        var content = DisplayRectangle;
        if (content.Width <= 0f || content.Height <= 0f)
            return;

        var switchRect = GetSwitchRect(content);
        if (SwitchRender != null)
        {
            var args = new SwitchButtonRenderEventArgs(
                canvas,
                this,
                content,
                switchRect,
                GetClampedVisualProgress(),
                Math.Clamp((float)_pressAnimation.GetProgress(), 0f, 1f));
            SwitchRender.Invoke(this, args);
            if (args.Handled)
                return;
        }

        DrawSwitchTrack(canvas, switchRect);
        DrawSwitchThumb(canvas, switchRect);
        DrawSwitchText(canvas, content, switchRect);
    }

    public override void  OnMouseDown(MouseEventArgs e)
    {
        _toggleArmedByPointer = IsPointInToggleArea(e.Location);
        if (_toggleArmedByPointer && e.Button == MouseButtons.Left)
        {
            _pointerPressActive = true;
            _pointerPressCheckedState = Checked;
            _pressAnimation.StartNewAnimation(AnimationDirection.In);
            GetParentWindow()?.SetMouseCapture(this);
            Invalidate();
        }

        base.OnMouseDown(e);
    }

    public override void  OnMouseUp(MouseEventArgs e)
    {
        if (e.Button == MouseButtons.Left)
        {
            var shouldToggle = _pointerPressActive && _toggleArmedByPointer && IsPointInToggleArea(e.Location);
            if (shouldToggle)
            {
                _suppressNextPointerClick = true;
                Checked = !Checked;
            }

            _pressAnimation.StartNewAnimation(AnimationDirection.Out);
            _pointerPressActive = false;
            GetParentWindow()?.ReleaseMouseCapture(this);
            Invalidate();
        }

        base.OnMouseUp(e);
    }

    public override void  OnMouseLeave(EventArgs e)
    {
        if (!_pointerPressActive)
            _pressAnimation.StartNewAnimation(AnimationDirection.Out);

        base.OnMouseLeave(e);
    }

    public override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);

        if (e.Handled || !Enabled)
            return;

        if (e.KeyCode != Keys.Enter && e.KeyCode != Keys.Space)
            return;

        _keyboardPressArmed = true;
        e.Handled = true;
    }

    public override void  OnKeyUp(KeyEventArgs e)
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

    public override void  OnLostFocus(EventArgs e)
    {
        _keyboardPressArmed = false;
        _pointerPressActive = false;
        GetParentWindow()?.ReleaseMouseCapture(this);
        _pressAnimation.StartNewAnimation(AnimationDirection.Out);
        base.OnLostFocus(e);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            ColorScheme.ThemeChanged -= HandleThemeChanged;
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

        if (Enabled && progress > 0.01f)
        {
            _trackPaint.Color = SKColors.White.WithAlpha((byte)Math.Clamp(24f * progress, 0f, ColorScheme.IsDarkMode ? 18f : 24f));
            var highlight = new SKRect(rect.Left + 1f * ScaleFactor, rect.Top + 1f * ScaleFactor, rect.Right - 1f * ScaleFactor, rect.Top + rect.Height * 0.46f);
            canvas.DrawRoundRect(highlight, highlight.Height * 0.5f, highlight.Height * 0.5f, _trackPaint);
        }

        _borderPaint.Color = Enabled
            ? ColorScheme.Outline.WithAlpha((byte)Math.Clamp(ColorScheme.IsDarkMode ? 96f - progress * 42f : 92f - progress * 56f, 28f, 110f))
            : ColorScheme.Outline.WithAlpha(70);
        _borderPaint.StrokeWidth = Math.Max(1f, ScaleFactor);
        canvas.DrawRoundRect(InflateRect(rect, -_borderPaint.StrokeWidth * 0.5f), rect.Height * 0.5f, rect.Height * 0.5f, _borderPaint);
    }

    private void DrawSwitchThumb(SKCanvas canvas, SKRect rect)
    {
        var rawProgress = GetAnimatedProgress();
        var visualProgress = GetElasticVisualProgress(rawProgress);
        var clamped = Math.Clamp(visualProgress, 0f, 1f);
        var phase = GetAnimationPhase(rawProgress);
        var progress = clamped;
        var padding = 3f * ScaleFactor;
        var diameter = Math.Max(10f, rect.Height - padding * 2f);
        var travel = Math.Max(0f, rect.Width - diameter - padding * 2f);
        var centerX = rect.Left + padding + diameter * 0.5f + travel * progress;
        var centerY = rect.MidY;
        var overshoot = TransitionMode == SwitchButtonTransitionMode.Bounce
            ? Math.Abs(visualProgress - clamped)
            : 0f;
        var settle = _thumbAnimation.Running && TransitionMode == SwitchButtonTransitionMode.Bounce
            ? MathF.Abs(MathF.Sin(phase * MathF.PI * 4.2f)) * MathF.Pow(Math.Clamp(1f - phase, 0f, 1f), 0.7f)
            : 0f;
        var stretch = TransitionMode == SwitchButtonTransitionMode.Fade
            ? 0f
            : Math.Min(GetTransitionStretchLimit(), (overshoot * 14f + settle * 1.8f) * ScaleFactor);
        var squash = TransitionMode == SwitchButtonTransitionMode.Fade
            ? 0f
            : Math.Min(1.2f * ScaleFactor, (overshoot * 6f + settle * 0.7f) * ScaleFactor);
        var thumbRect = new SKRect(
            centerX - diameter * 0.5f - stretch,
            centerY - diameter * 0.5f + squash,
            centerX + diameter * 0.5f + stretch,
            centerY + diameter * 0.5f - squash);

        thumbRect = ApplyPressedThumbStretch(thumbRect, rect, padding);
        thumbRect = ClampThumbRectToTrack(thumbRect, rect, padding);

        _thumbPaint.Color = (Enabled ? ThumbColor : ColorScheme.Surface).InterpolateColor(
            Enabled ? ThumbCheckedColor : ColorScheme.SurfaceVariant,
            GetClampedVisualProgress());

        if (_thumbAnimation.Running && TransitionMode == SwitchButtonTransitionMode.Bounce)
        {
            var pulseAlpha = (byte)Math.Clamp(22f * (1f - phase), 0f, 22f);
            _trackPaint.Color = OnColor.WithAlpha(pulseAlpha);
            canvas.DrawRoundRect(InflateRect(thumbRect, 4f * ScaleFactor), thumbRect.Height * 0.5f + 4f * ScaleFactor, thumbRect.Height * 0.5f + 4f * ScaleFactor, _trackPaint);
        }

        _trackPaint.Color = ColorScheme.ShadowColor.WithAlpha(ColorScheme.IsDarkMode ? (byte)130 : Checked ? (byte)34 : (byte)22);
        canvas.DrawRoundRect(OffsetRect(thumbRect, 0f, 1f * ScaleFactor), thumbRect.Height * 0.5f, thumbRect.Height * 0.5f, _trackPaint);
        canvas.DrawRoundRect(thumbRect, thumbRect.Height * 0.5f, thumbRect.Height * 0.5f, _thumbPaint);

        if (!ColorScheme.IsDarkMode)
            return;

        _borderPaint.Color = SKColors.White.WithAlpha(Checked ? (byte)54 : (byte)40);
        _borderPaint.StrokeWidth = Math.Max(1f, ScaleFactor);
        canvas.DrawRoundRect(InflateRect(thumbRect, -_borderPaint.StrokeWidth * 0.5f), thumbRect.Height * 0.5f, thumbRect.Height * 0.5f, _borderPaint);
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
        return Math.Clamp(GetElasticVisualProgress(GetAnimatedProgress()), 0f, 1f);
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

        if (TransitionMode is SwitchButtonTransitionMode.Stretch or SwitchButtonTransitionMode.Cupertino or SwitchButtonTransitionMode.Material)
            return rawProgress;

        if (!IsElasticTransition())
            return rawProgress;

        var phase = GetAnimationPhase(rawProgress);
        var decay = MathF.Pow(Math.Clamp(1f - phase, 0f, 1f), TransitionMode == SwitchButtonTransitionMode.Bounce ? 1.25f : 1.65f);
        var wave = TransitionMode switch
        {
            SwitchButtonTransitionMode.Bounce => 3.8f,
            SwitchButtonTransitionMode.Jelly => 3.25f,
            _ => 2.55f
        };
        var amount = TransitionMode switch
        {
            SwitchButtonTransitionMode.Bounce => _elasticity,
            SwitchButtonTransitionMode.Jelly => _elasticity * 0.75f,
            _ => _elasticity * 0.42f
        };
        var spring = MathF.Sin(phase * MathF.PI * wave) * decay * amount;
        return rawProgress + (Checked ? spring : -spring);
    }

    private bool IsElasticTransition()
    {
        return TransitionMode == SwitchButtonTransitionMode.SoftElastic
            || TransitionMode == SwitchButtonTransitionMode.Bounce
            || TransitionMode == SwitchButtonTransitionMode.Jelly
            || TransitionMode == SwitchButtonTransitionMode.Stretch;
    }

    private SKRect ApplyPressedThumbStretch(SKRect thumbRect, SKRect trackRect, float padding)
    {
        var pressProgress = Math.Clamp((float)_pressAnimation.GetProgress(), 0f, 1f);
        if (_pointerPressActive)
            pressProgress = Math.Max(pressProgress, 0.72f);

        if (pressProgress <= 0.001f || TransitionMode == SwitchButtonTransitionMode.Fade)
            return thumbRect;

        var amount = GetPressedStretchAmount() * PressStretch * ScaleFactor * pressProgress;
        var maxWidth = Math.Max(thumbRect.Width, trackRect.Width - padding * 2f);
        var targetWidth = Math.Min(thumbRect.Width + amount, maxWidth);

        if (_pointerPressActive ? _pointerPressCheckedState : Checked)
        {
            thumbRect.Left = thumbRect.Right - targetWidth;
            return thumbRect;
        }

        thumbRect.Right = thumbRect.Left + targetWidth;
        return thumbRect;
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
            SwitchButtonTransitionMode.Cupertino => AnimationType.CubicEaseInOut,
            SwitchButtonTransitionMode.Material => AnimationType.CubicEaseOut,
            SwitchButtonTransitionMode.Jelly => AnimationType.CubicEaseOut,
            SwitchButtonTransitionMode.Stretch => AnimationType.CubicEaseOut,
            SwitchButtonTransitionMode.Bounce => AnimationType.CubicEaseOut,
            _ => AnimationType.CubicEaseOut
        };
        _thumbAnimation.Increment = 16d / milliseconds;
        _thumbAnimation.SecondaryIncrement = 16d / Math.Max(1d, milliseconds * 0.86d);
    }

    private void HandleThemeChanged(object? sender, EventArgs e)
    {
        if (!UseThemeColors)
            return;

        ApplyThemeColors();
        RefreshVisualStylesForStateChange();
        Invalidate();
    }

    private void ApplyThemeColors()
    {
        ForeColor = ColorScheme.ForeColor;
        if (!_customOnColor)
            _onColor = ColorScheme.IsDarkMode ? ColorScheme.Primary.Brightness(0.14f) : ColorScheme.Primary;
        if (!_customOffColor)
            _offColor = ColorScheme.IsDarkMode
                ? new SKColor(39, 39, 42)
                : ColorScheme.SurfaceContainerHigh;
        if (!_customThumbColor)
            _thumbColor = ColorScheme.IsDarkMode
                ? new SKColor(250, 250, 250)
                : ColorScheme.Surface;
        if (!_customThumbCheckedColor)
            _thumbCheckedColor = SKColors.White;
    }

    public SwitchButton RenderSwitchWith(EventHandler<SwitchButtonRenderEventArgs> renderer)
    {
        SwitchRender += renderer;
        Invalidate();
        return this;
    }

    private float GetPressedStretchAmount()
    {
        return TransitionMode switch
        {
            SwitchButtonTransitionMode.Cupertino => 10.5f,
            SwitchButtonTransitionMode.Jelly => 9.5f,
            SwitchButtonTransitionMode.Material => 5.5f,
            SwitchButtonTransitionMode.Snap => 0f,
            SwitchButtonTransitionMode.Fade => 0f,
            _ => 8.5f
        };
    }

    private float GetAnimatedProgress()
    {
        var raw = Math.Clamp((float)_thumbAnimation.GetProgress(), 0f, 1f);
        if (AnimationFunction == null)
            return raw;

        try
        {
            return AnimationFunction(raw);
        }
        catch
        {
            return raw;
        }
    }

    private float GetTransitionStretchLimit()
    {
        return TransitionMode switch
        {
            SwitchButtonTransitionMode.Material => 1.8f * ScaleFactor,
            SwitchButtonTransitionMode.Cupertino => 3.6f * ScaleFactor,
            SwitchButtonTransitionMode.Jelly => 4.2f * ScaleFactor,
            _ => 3.2f * ScaleFactor
        };
    }

    private static SKRect ClampThumbRectToTrack(SKRect thumbRect, SKRect trackRect, float padding)
    {
        var minLeft = trackRect.Left + padding;
        var maxRight = trackRect.Right - padding;

        if (thumbRect.Left < minLeft)
            thumbRect.Offset(minLeft - thumbRect.Left, 0f);

        if (thumbRect.Right > maxRight)
            thumbRect.Offset(maxRight - thumbRect.Right, 0f);

        return thumbRect;
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
