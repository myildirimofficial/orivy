using Orivy.Animation;
using Orivy.Helpers;
using Orivy.Layout;
using SkiaSharp;
using System;
using System.ComponentModel;

namespace Orivy.Controls;

public class CheckBox : ElementBase
{
    private const float DefaultBoxSize = 18f;
    private const float DefaultTextGap = 8f;

    private readonly AnimationManager _checkAnimation;
    private readonly SKPaint _boxFillPaint = new() { IsAntialias = true, Style = SKPaintStyle.Fill };
    private readonly SKPaint _boxBorderPaint = new() { IsAntialias = true, Style = SKPaintStyle.Stroke };
    private readonly SKPaint _checkPaint = new() { IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeCap = SKStrokeCap.Round, StrokeJoin = SKStrokeJoin.Round };
    private readonly SKPaint _textPaint = new() { IsAntialias = true, Style = SKPaintStyle.Fill };
    private readonly SKPaint _highlightPaint = new() { IsAntialias = true, Style = SKPaintStyle.Fill };

    private CheckState _checkState;
    private bool _keyboardPressArmed;

    public CheckBox()
    {
        AutoSize = true;
        CommonProperties.SetSelfAutoSizeInDefaultLayout(this, true);
        AutoSizeMode = AutoSizeMode.GrowOnly;
        AutoEllipsis = true;
        WrapMode = TextWrap.None;
        CanSelect = true;
        TabStop = true;
        TextAlign = ContentAlignment.MiddleLeft;
        CheckAlign = ContentAlignment.MiddleLeft;
        MinimumSize = new SKSize(24, 24);
        Size = new SKSize(120, 28);
        Padding = new Thickness(0);
        Border = new Thickness(0);
        BackColor = SKColors.Transparent;
        ForeColor = ColorScheme.ForeColor;

        _checkAnimation = new AnimationManager(true)
        {
            AnimationType = AnimationType.CubicEaseOut,
            InterruptAnimation = true,
            Increment = 16d / 120d,
            SecondaryIncrement = 16d / 100d
        };
        _checkAnimation.OnAnimationProgress += HandleCheckAnimationProgress;
        _checkAnimation.OnAnimationFinished += HandleCheckAnimationFinished;

        ConfigureDefaultVisualStyles();
        ConfigureDefaultMotionEffects();
    }

    [DefaultValue(false)]
    public bool Checked
    {
        get => _checkState == CheckState.Checked;
        set => CheckState = value ? CheckState.Checked : CheckState.Unchecked;
    }

    [DefaultValue(CheckState.Unchecked)]
    public CheckState CheckState
    {
        get => _checkState;
        set
        {
            if (_checkState == value)
                return;

            var wasChecked = Checked;
            _checkState = value;
            StartCheckAnimation();
            RefreshVisualStylesForStateChange();
            OnCheckStateChanged(EventArgs.Empty);

            if (wasChecked != Checked)
                OnCheckedChanged(EventArgs.Empty);

            Invalidate();
        }
    }

    [DefaultValue(false)]
    public bool ThreeState { get; set; }

    [DefaultValue(typeof(ContentAlignment), nameof(ContentAlignment.MiddleLeft))]
    public ContentAlignment CheckAlign { get; set; }

    public event EventHandler? CheckedChanged;
    public event EventHandler? CheckStateChanged;

    protected override bool ShouldRenderDefaultText => false;

    protected override bool GetVisualCheckedState() => Checked;

    public override void  OnClick(EventArgs e)
    {
        ToggleCheckState();
        base.OnClick(e);
    }

    public override SKSize GetPreferredSize(SKSize proposedSize)
    {
        using var font = CreateRenderFont(Font);
        var constraints = proposedSize;
        if (constraints.Width <= 1f)
            constraints.Width = short.MaxValue;
        if (constraints.Height <= 1f)
            constraints.Height = short.MaxValue;

        var boxSize = GetBoxSize();
        var textSize = string.IsNullOrEmpty(Text)
            ? SKSize.Empty
            : TextRenderer.MeasureText(
                Text,
                font,
                constraints,
                new TextRenderOptions
                {
                    MaxWidth = constraints.Width,
                    Trimming = AutoEllipsis ? TextTrimming.CharacterEllipsis : TextTrimming.None,
                    UseMnemonic = UseMnemonic,
                    Wrap = TextWrap.None
                });

        var gap = textSize.Width > 0f ? GetTextGap() : 0f;
        var width = Padding.Left + Padding.Right + Border.Left + Border.Right + boxSize + gap + textSize.Width;
        var height = Padding.Top + Padding.Bottom + Border.Top + Border.Bottom + Math.Max(boxSize, textSize.Height);

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

        var boxRect = GetCheckBoxRect(content);
        DrawStateHighlight(canvas, GetHighlightRect(content, boxRect));
        DrawCheckBox(canvas, boxRect);
        DrawCheckBoxText(canvas, content, boxRect);
    }

    /// <summary>
    /// The hover/checked/pressed tint is confined to the glyph+label content instead of the full
    /// element bounds. A CheckBox is frequently given a wider fixed Width than its content needs
    /// (form alignment, grid columns); painting the highlight across the whole box then leaves a
    /// highlighted dead strip stuck to one side whenever the control is wider than its content.
    /// </summary>
    private SKRect GetHighlightRect(SKRect content, SKRect boxRect)
    {
        if (string.IsNullOrEmpty(Text))
            return SKRect.Inflate(boxRect, 3f, 3f);

        using var font = CreateRenderFont(Font);
        var textWidth = font.MeasureText(Text);
        var gap = GetTextGap();

        var textRect = boxRect.MidX <= content.MidX
            ? SKRect.Create(boxRect.Right + gap, content.Top, textWidth, content.Height)
            : SKRect.Create(boxRect.Left - gap - textWidth, content.Top, textWidth, content.Height);

        return SKRect.Inflate(SKRect.Union(boxRect, textRect), 6f, 3f);
    }

    private void DrawStateHighlight(SKCanvas canvas, SKRect rect)
    {
        if (!Enabled)
            return;

        SKColor color;
        if (IsPressed) color = ColorScheme.Primary.WithAlpha(24);
        else if (Checked) color = ColorScheme.Primary.WithAlpha(18);
        else if (IsPointerOver) color = ColorScheme.Primary.WithAlpha(14);
        else return;

        _highlightPaint.Color = color;
        canvas.DrawRoundRect(rect, 8f, 8f, _highlightPaint);
    }

    public override void  OnKeyDown(KeyEventArgs e)
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
            PerformClick();
    }

    public override void  OnLostFocus(EventArgs e)
    {
        _keyboardPressArmed = false;
        base.OnLostFocus(e);
    }

    public virtual void  OnCheckedChanged(EventArgs e)
    {
        CheckedChanged?.Invoke(this, e);
    }

    public virtual void  OnCheckStateChanged(EventArgs e)
    {
        CheckStateChanged?.Invoke(this, e);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _checkAnimation.OnAnimationProgress -= HandleCheckAnimationProgress;
            _checkAnimation.OnAnimationFinished -= HandleCheckAnimationFinished;
            _checkAnimation.Dispose();
            _boxFillPaint.Dispose();
            _boxBorderPaint.Dispose();
            _checkPaint.Dispose();
            _textPaint.Dispose();
            _highlightPaint.Dispose();
        }

        base.Dispose(disposing);
    }

    private void ToggleCheckState()
    {
        CheckState = ThreeState
            ? CheckState switch
            {
                CheckState.Unchecked => CheckState.Checked,
                CheckState.Checked => CheckState.Indeterminate,
                _ => CheckState.Unchecked
            }
            : Checked ? CheckState.Unchecked : CheckState.Checked;
    }

    private void StartCheckAnimation()
    {
        var direction = _checkState == CheckState.Unchecked
            ? AnimationDirection.Out
            : AnimationDirection.In;
        _checkAnimation.StartNewAnimation(direction);
    }

    private void HandleCheckAnimationProgress(object _)
    {
        Invalidate();
    }

    private void HandleCheckAnimationFinished(object _)
    {
        Invalidate();
    }

    private SKRect GetCheckBoxRect(SKRect content)
    {
        var size = GetBoxSize();
        var x = CheckAlign switch
        {
            ContentAlignment.TopRight or ContentAlignment.MiddleRight or ContentAlignment.BottomRight =>
                content.Right - size,
            ContentAlignment.TopCenter or ContentAlignment.MiddleCenter or ContentAlignment.BottomCenter =>
                content.Left + Math.Max(0f, (content.Width - size) / 2f),
            _ => content.Left
        };
        var y = CheckAlign switch
        {
            ContentAlignment.TopLeft or ContentAlignment.TopCenter or ContentAlignment.TopRight =>
                content.Top,
            ContentAlignment.BottomLeft or ContentAlignment.BottomCenter or ContentAlignment.BottomRight =>
                content.Bottom - size,
            _ => content.Top + Math.Max(0f, (content.Height - size) / 2f)
        };

        return SKRect.Create(x, y, size, size);
    }

    private void DrawCheckBox(SKCanvas canvas, SKRect rect)
    {
        var progress = GetCheckProgress();
        var radius = Math.Max(4f * ScaleFactor, rect.Height * 0.22f);
        var fillColor = LerpColor(ColorScheme.Surface, Enabled ? ColorScheme.Primary : ColorScheme.SurfaceVariant, progress);
        var borderColor = LerpColor(ColorScheme.Outline, Enabled ? ColorScheme.Primary : ColorScheme.Outline.WithAlpha(150), progress);

        _boxFillPaint.Color = fillColor;
        _boxBorderPaint.Color = borderColor;
        _boxBorderPaint.StrokeWidth = Math.Max(1f, ScaleFactor);

        canvas.DrawRoundRect(rect, radius, radius, _boxFillPaint);
        canvas.DrawRoundRect(rect, radius, radius, _boxBorderPaint);

        if (progress <= 0.001f)
            return;

        _checkPaint.Color = SKColors.White.WithAlpha((byte)Math.Clamp((int)Math.Round(255f * progress), 0, 255));
        _checkPaint.StrokeWidth = Math.Max(2f, 2.2f * ScaleFactor);

        if (_checkState == CheckState.Indeterminate)
            DrawIndeterminate(canvas, rect, progress);
        else
            DrawCheckMark(canvas, rect, progress);
    }

    private void DrawCheckMark(SKCanvas canvas, SKRect rect, float progress)
    {
        var p1 = new SKPoint(rect.Left + rect.Width * 0.26f, rect.Top + rect.Height * 0.54f);
        var p2 = new SKPoint(rect.Left + rect.Width * 0.43f, rect.Top + rect.Height * 0.70f);
        var p3 = new SKPoint(rect.Left + rect.Width * 0.75f, rect.Top + rect.Height * 0.32f);

        var len1 = Distance(p1, p2);
        var len2 = Distance(p2, p3);
        var drawLength = (len1 + len2) * Math.Clamp(progress, 0f, 1f);

        if (drawLength <= len1)
        {
            var end = LerpPoint(p1, p2, drawLength / len1);
            canvas.DrawLine(p1, end, _checkPaint);
            return;
        }

        canvas.DrawLine(p1, p2, _checkPaint);
        var secondProgress = Math.Clamp((drawLength - len1) / len2, 0f, 1f);
        canvas.DrawLine(p2, LerpPoint(p2, p3, secondProgress), _checkPaint);
    }

    private void DrawIndeterminate(SKCanvas canvas, SKRect rect, float progress)
    {
        var centerY = rect.MidY;
        var half = rect.Width * 0.25f * progress;
        canvas.DrawLine(rect.MidX - half, centerY, rect.MidX + half, centerY, _checkPaint);
    }

    private void DrawCheckBoxText(SKCanvas canvas, SKRect content, SKRect boxRect)
    {
        if (string.IsNullOrEmpty(Text))
            return;

        var textRect = content;
        var gap = GetTextGap();
        if (boxRect.MidX <= content.MidX)
            textRect.Left = Math.Min(content.Right, boxRect.Right + gap);
        else
            textRect.Right = Math.Max(content.Left, boxRect.Left - gap);

        if (textRect.Width <= 0f || textRect.Height <= 0f)
            return;

        using var font = CreateRenderFont(Font);
        _textPaint.Color = Enabled ? ForeColor : ColorScheme.ForeColor.WithAlpha(150);
        TextRenderer.DrawText(canvas, Text, textRect, _textPaint, font, TextAlign, AutoEllipsis, UseMnemonic, WrapMode);
    }

    private void ConfigureDefaultVisualStyles()
    {
        // Background is intentionally left out of the hover/pressed/checked rules — that highlight
        // is painted by hand in OnPaint, confined to the glyph+label content (see DrawStateHighlight),
        // instead of the full element bounds which a wider-than-content Width would leave stranded.
        ConfigureVisualStyles(styles => styles
            .DefaultTransition(TimeSpan.FromMilliseconds(140), AnimationType.CubicEaseOut)
            .Base(baseStyle => baseStyle
                .Background(SKColors.Transparent)
                .Foreground(ColorScheme.ForeColor)
                .Border(0)
                .Radius(8)
                .Shadow(BoxShadow.None))
            .OnPressed(rule => rule.Scale(0.985f))
            .OnDisabled(rule => rule
                .Foreground(ColorScheme.ForeColor.WithAlpha(140))
                .Opacity(0.72f)));
    }

    private void ConfigureDefaultMotionEffects()
    {
        ConfigureMotionEffects(scene => scene
            .Rectangle(effect => effect
                .Anchor(0.08f, 0.5f)
                .Size(26f, 18f)
                .Drift(2f, 1f)
                .CornerRadius(6f)
                .Color(ColorScheme.Primary.WithAlpha(10))
                .Opacity(0.01f, 0.06f)
                .Scale(0.9f, 1.1f)
                .Duration(4.6d)
                .SpeedOnHover(1.5f)
                .SpeedOnPressed(2.2f)
                .SpeedOnFocused(1.7f)));
    }

    private float GetCheckProgress()
    {
        if (_checkAnimation.IsAnimating())
            return Math.Clamp((float)_checkAnimation.GetProgress(), 0f, 1f);

        return _checkState == CheckState.Unchecked ? 0f : 1f;
    }

    private float GetBoxSize() => Math.Max(12f, DefaultBoxSize * ScaleFactor);

    private float GetTextGap() => DefaultTextGap * ScaleFactor;

    private static float Distance(SKPoint a, SKPoint b)
    {
        var dx = b.X - a.X;
        var dy = b.Y - a.Y;
        return MathF.Sqrt(dx * dx + dy * dy);
    }

    private static SKPoint LerpPoint(SKPoint from, SKPoint to, float progress)
    {
        return new SKPoint(
            from.X + (to.X - from.X) * progress,
            from.Y + (to.Y - from.Y) * progress);
    }

    private static SKColor LerpColor(SKColor from, SKColor to, float progress)
    {
        progress = Math.Clamp(progress, 0f, 1f);
        return new SKColor(
            (byte)Math.Clamp((int)Math.Round(from.Red + (to.Red - from.Red) * progress), 0, 255),
            (byte)Math.Clamp((int)Math.Round(from.Green + (to.Green - from.Green) * progress), 0, 255),
            (byte)Math.Clamp((int)Math.Round(from.Blue + (to.Blue - from.Blue) * progress), 0, 255),
            (byte)Math.Clamp((int)Math.Round(from.Alpha + (to.Alpha - from.Alpha) * progress), 0, 255));
    }
}
