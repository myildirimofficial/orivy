using Orivy.Animation;
using Orivy.Helpers;
using Orivy.Layout;
using SkiaSharp;
using System;
using System.ComponentModel;

namespace Orivy.Controls;

public class ProgressBar : ElementBase
{
    private readonly AnimationManager _valueAnimation;
    private readonly AnimationManager _indeterminateAnimation;
    private readonly SKPaint _trackPaint = new() { IsAntialias = true, Style = SKPaintStyle.Fill };
    private readonly SKPaint _valuePaint = new() { IsAntialias = true, Style = SKPaintStyle.Fill, StrokeCap = SKStrokeCap.Round };
    private readonly SKPaint _textPaint = new() { IsAntialias = true, Style = SKPaintStyle.Fill };

    private float _minimum;
    private float _maximum = 100f;
    private float _value;
    private float _displayValue;
    private float _animationFrom;
    private float _animationTo;
    private ProgressBarMode _mode = ProgressBarMode.Linear;
    private ProgressBarTextMode _textMode = ProgressBarTextMode.None;

    public ProgressBar()
    {
        AutoSize = false;
        CanSelect = false;
        TabStop = false;
        UseDefaultPointerVisualStates = false;
        Size = new SKSize(180, 12);
        MinimumSize = new SKSize(32, 8);
        Padding = new Thickness(0);
        Border = new Thickness(0);
        Radius = new Radius(6);
        BackColor = SKColors.Transparent;
        ForeColor = ColorScheme.Primary;
        TextAlign = ContentAlignment.MiddleCenter;

        _valueAnimation = new AnimationManager(true)
        {
            AnimationType = AnimationType.CubicEaseOut,
            InterruptAnimation = true,
            Increment = 16d / 220d,
            SecondaryIncrement = 16d / 220d
        };
        _valueAnimation.OnAnimationProgress += HandleValueAnimationProgress;
        _valueAnimation.OnAnimationFinished += HandleValueAnimationFinished;

        _indeterminateAnimation = new AnimationManager(true)
        {
            AnimationType = AnimationType.Linear,
            InterruptAnimation = true,
            Increment = 16d / 1100d,
            SecondaryIncrement = 16d / 1100d
        };
        _indeterminateAnimation.OnAnimationProgress += HandleIndeterminateProgress;
        _indeterminateAnimation.OnAnimationFinished += HandleIndeterminateFinished;

        ConfigureVisualStyles(styles => styles
            .DefaultTransition(TimeSpan.FromMilliseconds(160), AnimationType.CubicEaseOut)
            .Base(baseStyle => baseStyle
                .Background(SKColors.Transparent)
                .Foreground(ColorScheme.Primary)
                .Border(0)
                .Radius(6)
                .Shadow(BoxShadow.None))
            .OnHover(rule => rule
                .Foreground(ColorScheme.Primary.Brightness(0.06f)))
            .OnDisabled(rule => rule
                .Foreground(ColorScheme.Outline)
                .Opacity(0.72f)));

        ConfigureMotionEffects(scene => scene
            .Rectangle(effect => effect
                .Anchor(0.5f, 0.5f)
                .Size(80f, 8f)
                .Drift(8f, 0f)
                .CornerRadius(8f)
                .Color(ColorScheme.Primary.WithAlpha(8))
                .Opacity(0.01f, 0.06f)
                .Scale(0.9f, 1.08f)
                .Duration(5.4d)
                .SpeedOnHover(1.4f)));
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
        set
        {
            var normalized = Math.Clamp(value, _minimum, _maximum);
            if (Math.Abs(_value - normalized) < 0.001f)
                return;

            _value = normalized;
            StartValueAnimation(normalized);
        }
    }

    [DefaultValue(ProgressBarMode.Linear)]
    public ProgressBarMode Mode
    {
        get => _mode;
        set
        {
            if (_mode == value)
                return;

            _mode = value;
            UpdateIndeterminateAnimation();
            Invalidate();
        }
    }

    [DefaultValue(false)]
    public bool ShowValue
    {
        get => _textMode != ProgressBarTextMode.None;
        set => TextMode = value ? ProgressBarTextMode.Percent : ProgressBarTextMode.None;
    }

    [DefaultValue(ProgressBarTextMode.None)]
    public ProgressBarTextMode TextMode
    {
        get => _textMode;
        set
        {
            if (_textMode == value)
                return;

            _textMode = value;
            Invalidate();
        }
    }

    public int PercentIndices { get; set; } = 0;

    [DefaultValue(false)]
    public bool UseHatchFill { get; set; }

    [DefaultValue(HatchStyle.LightDownwardDiagonal)]
    public HatchStyle HatchStyle { get; set; } = HatchStyle.LightDownwardDiagonal;

    protected override bool ShouldRenderDefaultText => false;

    public override void  OnPaint(SKCanvas canvas)
    {
        base.OnPaint(canvas);

        var rect = DisplayRectangle;
        if (rect.Width <= 0f || rect.Height <= 0f)
            return;

        if (Mode == ProgressBarMode.Circular || Mode == ProgressBarMode.Ring)
            DrawCircular(canvas, rect);
        else
            DrawLinear(canvas, rect);

        if (TextMode != ProgressBarTextMode.None && Mode != ProgressBarMode.Indeterminate)
            DrawProgressText(canvas, rect);
    }

    public override void  Dispose(bool disposing)
    {
        if (disposing)
        {
            _valueAnimation.OnAnimationProgress -= HandleValueAnimationProgress;
            _valueAnimation.OnAnimationFinished -= HandleValueAnimationFinished;
            _valueAnimation.Dispose();
            _indeterminateAnimation.OnAnimationProgress -= HandleIndeterminateProgress;
            _indeterminateAnimation.OnAnimationFinished -= HandleIndeterminateFinished;
            _indeterminateAnimation.Dispose();
            _trackPaint.Dispose();
            _valuePaint.Dispose();
            _textPaint.Dispose();
        }

        base.Dispose(disposing);
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

    private void UpdateIndeterminateAnimation()
    {
        if (Mode == ProgressBarMode.Indeterminate && !_indeterminateAnimation.IsAnimating())
            _indeterminateAnimation.StartNewAnimation(AnimationDirection.In);
        else if (Mode != ProgressBarMode.Indeterminate)
            _indeterminateAnimation.Stop();
    }

    private void HandleIndeterminateProgress(object _)
    {
        Invalidate();
    }

    private void HandleIndeterminateFinished(object _)
    {
        if (Mode != ProgressBarMode.Indeterminate)
            return;

        _indeterminateAnimation.SetProgress(0d);
        _indeterminateAnimation.StartNewAnimation(AnimationDirection.In);
        Invalidate();
    }

    private void DrawLinear(SKCanvas canvas, SKRect rect)
    {
        var radius = Math.Min(rect.Height, rect.Width) * 0.5f;
        _trackPaint.Color = BackColor == SKColors.Transparent ? ColorScheme.SurfaceContainerHigh : BackColor;
        _valuePaint.Color = Enabled ? ForeColor : ColorScheme.Outline;

        if (Mode == ProgressBarMode.Indeterminate)
        {
            canvas.DrawRoundRect(rect, radius, radius, _trackPaint);
            DrawIndeterminate(canvas, rect, radius);
            return;
        }

        var fraction = GetFraction();
        if (Mode == ProgressBarMode.Segmented)
        {
            DrawSegments(canvas, rect, fraction);
            return;
        }

        if (Mode == ProgressBarMode.Blocks)
        {
            DrawBlocks(canvas, rect, fraction);
            return;
        }

        if (Mode == ProgressBarMode.Dots)
        {
            DrawDots(canvas, rect, fraction);
            return;
        }

        canvas.DrawRoundRect(rect, radius, radius, _trackPaint);
        if (fraction <= 0f)
            return;

        var valueRect = new SKRect(rect.Left, rect.Top, rect.Left + rect.Width * fraction, rect.Bottom);
        DrawValueFill(canvas, valueRect, radius);
    }

    private void DrawSegments(SKCanvas canvas, SKRect rect, float fraction)
    {
        var count = Math.Max(5, (int)MathF.Floor(rect.Width / Math.Max(18f, 24f * ScaleFactor)));
        var gap = Math.Max(2f, 3f * ScaleFactor);
        var segmentWidth = (rect.Width - gap * (count - 1)) / count;
        var filled = fraction * count;

        for (var i = 0; i < count; i++)
        {
            var left = rect.Left + i * (segmentWidth + gap);
            var segment = new SKRect(left, rect.Top, left + segmentWidth, rect.Bottom);
            canvas.DrawRoundRect(segment, rect.Height * 0.5f, rect.Height * 0.5f, _trackPaint);

            var local = Math.Clamp(filled - i, 0f, 1f);
            if (local <= 0f)
                continue;

            var valueSegment = new SKRect(segment.Left, segment.Top, segment.Left + segment.Width * local, segment.Bottom);
            DrawValueFill(canvas, valueSegment, rect.Height * 0.5f);
        }
    }

    private void DrawBlocks(SKCanvas canvas, SKRect rect, float fraction)
    {
        var count = Math.Max(6, (int)MathF.Floor(rect.Width / Math.Max(14f, 18f * ScaleFactor)));
        var gap = Math.Max(1f, 2f * ScaleFactor);
        var blockWidth = (rect.Width - gap * (count - 1)) / count;
        var filled = fraction * count;
        var radius = Math.Min(4f * ScaleFactor, rect.Height * 0.35f);

        for (var i = 0; i < count; i++)
        {
            var left = rect.Left + i * (blockWidth + gap);
            var block = new SKRect(left, rect.Top, left + blockWidth, rect.Bottom);
            canvas.DrawRoundRect(block, radius, radius, _trackPaint);

            var local = Math.Clamp(filled - i, 0f, 1f);
            if (local <= 0f)
                continue;

            DrawValueFill(canvas, new SKRect(block.Left, block.Top, block.Left + block.Width * local, block.Bottom), radius);
        }
    }

    private void DrawDots(SKCanvas canvas, SKRect rect, float fraction)
    {
        var radius = Math.Max(3f, rect.Height * 0.34f);
        var step = Math.Max(radius * 2.5f, 12f * ScaleFactor);
        var count = Math.Max(2, (int)MathF.Floor((rect.Width + step * 0.5f) / step));
        var filled = fraction * Math.Max(1, count - 1);
        var y = rect.MidY;

        for (var i = 0; i < count; i++)
        {
            var x = count == 1 ? rect.MidX : rect.Left + (rect.Width * i / (count - 1));
            _trackPaint.Color = (BackColor == SKColors.Transparent ? ColorScheme.SurfaceContainerHigh : BackColor).WithAlpha(210);
            canvas.DrawCircle(x, y, radius, _trackPaint);

            var local = Math.Clamp(filled - i + 1f, 0f, 1f);
            if (local <= 0f)
                continue;

            canvas.DrawCircle(x, y, radius * (0.72f + 0.28f * local), _valuePaint);
        }
    }

    private void DrawIndeterminate(SKCanvas canvas, SKRect rect, float radius)
    {
        var progress = Math.Clamp((float)_indeterminateAnimation.GetProgress(), 0f, 1f);
        var width = rect.Width * 0.34f;
        var travel = rect.Width + width * 2f;
        var left = rect.Left - width + travel * progress;
        var chunk = new SKRect(left, rect.Top, left + width, rect.Bottom);
        DrawValueFill(canvas, chunk, radius);
    }

    private void DrawCircular(SKCanvas canvas, SKRect rect)
    {
        var size = Math.Min(rect.Width, rect.Height);
        var stroke = Math.Max(3f, size * (Mode == ProgressBarMode.Ring ? 0.075f : 0.085f));
        var inset = stroke * 0.9f + 1f * ScaleFactor;
        var arcRect = new SKRect(
            rect.MidX - size / 2f + inset,
            rect.MidY - size / 2f + inset,
            rect.MidX + size / 2f - inset,
            rect.MidY + size / 2f - inset);

        _trackPaint.Color = BackColor == SKColors.Transparent
            ? ColorScheme.Outline.WithAlpha(48)
            : BackColor;
        _trackPaint.Style = SKPaintStyle.Stroke;
        _trackPaint.StrokeWidth = stroke;
        _trackPaint.StrokeCap = Mode == ProgressBarMode.Ring ? SKStrokeCap.Butt : SKStrokeCap.Round;

        _valuePaint.Color = Enabled ? ForeColor : ColorScheme.Outline;
        _valuePaint.Style = SKPaintStyle.Stroke;
        _valuePaint.StrokeWidth = stroke;
        _valuePaint.StrokeCap = SKStrokeCap.Round;

        if (Mode == ProgressBarMode.Ring)
        {
            canvas.DrawArc(arcRect, -90f, 360f, false, _trackPaint);
            canvas.DrawArc(arcRect, -90f, 360f * GetFraction(), false, _valuePaint);
        }
        else
        {
            canvas.DrawOval(arcRect, _trackPaint);
            canvas.DrawArc(arcRect, -90f, 360f * GetFraction(), false, _valuePaint);
        }

        _trackPaint.Style = SKPaintStyle.Fill;
        _valuePaint.Style = SKPaintStyle.Fill;
    }

    private void DrawProgressText(SKCanvas canvas, SKRect rect)
    {
        if (TextMode == ProgressBarTextMode.PercentWhenWide && !HasEnoughTextRoom(rect))
            return;

        var text = TextMode switch
        {
            ProgressBarTextMode.Percent => $"{MathF.Round(GetFraction() * 100f, PercentIndices)}%",
            ProgressBarTextMode.PercentWhenWide => $"{MathF.Round(GetFraction() * 100f, PercentIndices)}%",
            ProgressBarTextMode.Value => $"{_displayValue:0}",
            ProgressBarTextMode.ValueRange => $"{_displayValue:0} / {Maximum:0}",
            _ => string.Empty
        };

        if (string.IsNullOrEmpty(text))
            return;

        using var font = CreateRenderFont(Font);
        var baseColor = ColorScheme.ForeColor;
        var filledColor = (Enabled ? ForeColor : ColorScheme.Outline).Determine();
        var fillClip = GetFilledTextClip(rect);

        DrawProgressTextLayer(canvas, text, rect, font, baseColor);

        if (fillClip is not { } clip || clip.Width <= 0f || clip.Height <= 0f)
            return;

        var save = canvas.Save();
        canvas.ClipRect(clip, SKClipOperation.Intersect, antialias: true);
        DrawProgressTextLayer(canvas, text, rect, font, filledColor);
        canvas.RestoreToCount(save);
    }

    private float GetFraction()
    {
        var range = Math.Max(0.001f, Maximum - Minimum);
        return Math.Clamp((_displayValue - Minimum) / range, 0f, 1f);
    }

    private bool HasEnoughTextRoom(SKRect rect)
    {
        if (Mode == ProgressBarMode.Circular || Mode == ProgressBarMode.Ring)
            return Math.Min(rect.Width, rect.Height) >= 54f * ScaleFactor;

        return rect.Width >= 84f * ScaleFactor && rect.Height >= 14f * ScaleFactor;
    }

    private void DrawValueFill(SKCanvas canvas, SKRect rect, float radius)
    {
        if (rect.Width <= 0f || rect.Height <= 0f)
            return;

        DrawBaseValueFill(canvas, rect, radius);

        if (Mode == ProgressBarMode.Striped)
            DrawStripeOverlay(canvas, rect, radius);

        if (!ShouldDrawHatchFill())
            return;

        using var hatchBrush = new HatchBrush(HatchStyle, _valuePaint.Color.Determine().WithAlpha(24), SKColors.Transparent);
        using var hatchPaint = hatchBrush.CreatePaint();
        var save = canvas.Save();
        using var roundRect = new SKRoundRect(rect, radius, radius);
        canvas.ClipRoundRect(roundRect, antialias: true);
        canvas.DrawRect(rect, hatchPaint);
        canvas.RestoreToCount(save);
    }

    private SKRect? GetFilledTextClip(SKRect rect)
    {
        if (Mode == ProgressBarMode.Circular
            || Mode == ProgressBarMode.Ring
            || Mode == ProgressBarMode.Indeterminate)
            return null;

        var fraction = GetFraction();
        if (fraction <= 0f)
            return SKRect.Empty;

        return new SKRect(rect.Left, rect.Top, rect.Left + rect.Width * fraction, rect.Bottom);
    }

    private void DrawProgressTextLayer(SKCanvas canvas, string text, SKRect rect, SKFont font, SKColor color)
    {
        _textPaint.Color = color;
        TextRenderer.DrawText(canvas, text, rect, _textPaint, font, ContentAlignment.MiddleCenter, false, false);
    }

    private void DrawGradientFill(SKCanvas canvas, SKRect rect, float radius)
    {
        using var paint = new SKPaint { IsAntialias = true, Style = SKPaintStyle.Fill };
        using var shader = SKShader.CreateLinearGradient(
            new SKPoint(rect.Left, rect.MidY),
            new SKPoint(rect.Right, rect.MidY),
            new[] { _valuePaint.Color.Brightness(0.10f), _valuePaint.Color.Brightness(-0.06f) },
            null,
            SKShaderTileMode.Clamp);

        paint.Shader = shader;
        canvas.DrawRoundRect(rect, radius, radius, paint);
    }

    private void DrawBaseValueFill(SKCanvas canvas, SKRect rect, float radius)
    {
        if (Mode == ProgressBarMode.Gradient)
        {
            DrawGradientFill(canvas, rect, radius);
            return;
        }

        canvas.DrawRoundRect(rect, radius, radius, _valuePaint);
    }

    private void DrawStripeOverlay(SKCanvas canvas, SKRect rect, float radius)
    {
        var save = canvas.Save();
        using var roundRect = new SKRoundRect(rect, radius, radius);
        canvas.ClipRoundRect(roundRect, antialias: true);

        using var paint = new SKPaint
        {
            IsAntialias = true,
            Color = _valuePaint.Color.Determine().WithAlpha(28),
            StrokeWidth = Math.Max(2f, 2f * ScaleFactor),
            Style = SKPaintStyle.Stroke
        };

        var spacing = Math.Max(10f, 12f * ScaleFactor);
        for (var x = rect.Left - rect.Height; x < rect.Right + rect.Height; x += spacing)
            canvas.DrawLine(x, rect.Bottom, x + rect.Height, rect.Top, paint);

        canvas.RestoreToCount(save);
    }

    private bool ShouldDrawHatchFill()
    {
        return UseHatchFill;
    }
}
