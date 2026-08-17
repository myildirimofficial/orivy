using Orivy.Animation;
using Orivy.Helpers;
using Orivy.Layout;
using SkiaSharp;
using System;
using System.ComponentModel;

namespace Orivy.Controls;

public class RadioButton : ElementBase
{
    private const float DefaultRadioSize = 18f;
    private const float DefaultTextGap = 8f;

    private readonly AnimationManager _checkAnimation;
    private readonly SKPaint _outerFillPaint = new() { IsAntialias = true, Style = SKPaintStyle.Fill };
    private readonly SKPaint _outerBorderPaint = new() { IsAntialias = true, Style = SKPaintStyle.Stroke };
    private readonly SKPaint _innerFillPaint = new() { IsAntialias = true, Style = SKPaintStyle.Fill };
    private readonly SKPaint _textPaint = new() { IsAntialias = true, Style = SKPaintStyle.Fill };
    private readonly SKPaint _highlightPaint = new() { IsAntialias = true, Style = SKPaintStyle.Fill };

    private bool _checked;
    private bool _keyboardPressArmed;
    private bool _synchronizingGroup;
    private string _groupName = string.Empty;

    public RadioButton()
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
        Padding = new Thickness(4, 2, 6, 2);
        Border = new Thickness(0);
        Radius = new Radius(8);
        BackColor = SKColors.Transparent;
        ForeColor = ColorScheme.ForeColor;

        _checkAnimation = new AnimationManager(true)
        {
            AnimationType = AnimationType.CubicEaseOut,
            InterruptAnimation = true,
            Increment = 16d / 130d,
            SecondaryIncrement = 16d / 105d
        };
        _checkAnimation.OnAnimationProgress += HandleCheckAnimationProgress;
        _checkAnimation.OnAnimationFinished += HandleCheckAnimationFinished;

        ConfigureDefaultVisualStyles();
        ConfigureDefaultMotionEffects();
    }

    [DefaultValue(false)]
    public bool Checked
    {
        get => _checked;
        set => SetChecked(value, updateGroup: true, raiseChanged: true);
    }

    [DefaultValue(true)]
    public bool AutoCheck { get; set; } = true;

    [DefaultValue("")]
    public string GroupName
    {
        get => _groupName;
        set
        {
            var normalized = value ?? string.Empty;
            if (_groupName == normalized)
                return;

            _groupName = normalized;
            if (Checked)
                SynchronizeRadioGroup();
        }
    }

    [DefaultValue(typeof(ContentAlignment), nameof(ContentAlignment.MiddleLeft))]
    public ContentAlignment CheckAlign { get; set; }

    public event EventHandler? CheckedChanged;

    protected override bool ShouldRenderDefaultText => false;

    protected override bool GetVisualCheckedState() => Checked;

    public override void  OnClick(EventArgs e)
    {
        if (AutoCheck && !Checked)
            Checked = true;

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

        var radioSize = GetRadioSize();
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
        var width = Padding.Left + Padding.Right + Border.Left + Border.Right + radioSize + gap + textSize.Width;
        var height = Padding.Top + Padding.Bottom + Border.Top + Border.Bottom + Math.Max(radioSize, textSize.Height);

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

        var radioRect = GetRadioRect(content);
        DrawStateHighlight(canvas, GetHighlightRect(content, radioRect));
        DrawRadioGlyph(canvas, radioRect);
        DrawRadioText(canvas, content, radioRect);
    }

    /// <summary>
    /// The hover/checked/pressed tint is confined to the glyph+label content instead of the full
    /// element bounds. A RadioButton is frequently given a wider fixed Width than its content needs
    /// (form alignment, grid columns); painting the highlight across the whole box then leaves a
    /// highlighted dead strip stuck to one side whenever the control is wider than its content.
    /// </summary>
    private SKRect GetHighlightRect(SKRect content, SKRect radioRect)
    {
        if (string.IsNullOrEmpty(Text))
            return SKRect.Inflate(radioRect, 3f, 3f);

        using var font = CreateRenderFont(Font);
        var textWidth = font.MeasureText(Text);
        var gap = GetTextGap();

        var textRect = radioRect.MidX <= content.MidX
            ? SKRect.Create(radioRect.Right + gap, content.Top, textWidth, content.Height)
            : SKRect.Create(radioRect.Left - gap - textWidth, content.Top, textWidth, content.Height);

        return SKRect.Inflate(SKRect.Union(radioRect, textRect), 6f, 3f);
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

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _checkAnimation.OnAnimationProgress -= HandleCheckAnimationProgress;
            _checkAnimation.OnAnimationFinished -= HandleCheckAnimationFinished;
            _checkAnimation.Dispose();
            _outerFillPaint.Dispose();
            _outerBorderPaint.Dispose();
            _innerFillPaint.Dispose();
            _textPaint.Dispose();
            _highlightPaint.Dispose();
        }

        base.Dispose(disposing);
    }

    private void SetChecked(bool value, bool updateGroup, bool raiseChanged)
    {
        if (_checked == value)
            return;

        _checked = value;
        StartCheckAnimation();
        RefreshVisualStylesForStateChange();

        if (value && updateGroup)
            SynchronizeRadioGroup();

        if (raiseChanged)
            OnCheckedChanged(EventArgs.Empty);

        Invalidate();
    }

    private void SynchronizeRadioGroup()
    {
        if (_synchronizingGroup || Parent == null)
            return;

        _synchronizingGroup = true;
        try
        {
            SynchronizeRadioGroup(GetRadioGroupRoot());
        }
        finally
        {
            _synchronizingGroup = false;
        }
    }

    private ElementBase GetRadioGroupRoot()
    {
        if (string.IsNullOrEmpty(GroupName))
            return Parent!;

        var root = Parent!;
        while (root.Parent is ElementBase parent)
            root = parent;

        return root;
    }

    private void SynchronizeRadioGroup(ElementBase parent)
    {
        for (var i = 0; i < parent.Controls.Count; i++)
        {
            if (parent.Controls[i] is not ElementBase child)
                continue;

            if (child is RadioButton radio &&
                !ReferenceEquals(radio, this) &&
                radio.Checked &&
                IsInSameGroup(radio))
            {
                radio.SetChecked(false, updateGroup: false, raiseChanged: true);
            }

            if (child.Controls.Count > 0)
                SynchronizeRadioGroup(child);
        }
    }

    private bool IsInSameGroup(RadioButton other)
    {
        if (!string.IsNullOrEmpty(GroupName) || !string.IsNullOrEmpty(other.GroupName))
            return string.Equals(GroupName, other.GroupName, StringComparison.Ordinal);

        return ReferenceEquals(Parent, other.Parent);
    }

    private void StartCheckAnimation()
    {
        _checkAnimation.StartNewAnimation(_checked ? AnimationDirection.In : AnimationDirection.Out);
    }

    private void HandleCheckAnimationProgress(object _)
    {
        Invalidate();
    }

    private void HandleCheckAnimationFinished(object _)
    {
        Invalidate();
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
            .Circle(effect => effect
                .Anchor(0.08f, 0.5f)
                .Size(26f, 26f)
                .Orbit(2.5f, 1.5f)
                .Color(ColorScheme.Primary.WithAlpha(10))
                .Opacity(0.01f, 0.06f)
                .Scale(0.9f, 1.12f)
                .Duration(4.4d)
                .SpeedOnHover(1.5f)
                .SpeedOnPressed(2.2f)
                .SpeedOnFocused(1.7f)));
    }

    private SKRect GetRadioRect(SKRect content)
    {
        var size = GetRadioSize();
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

    private void DrawRadioGlyph(SKCanvas canvas, SKRect rect)
    {
        var progress = GetCheckProgress();
        var center = new SKPoint(rect.MidX, rect.MidY);
        var radius = rect.Width * 0.5f;
        var outerFill = LerpColor(ColorScheme.Surface, Enabled ? ColorScheme.Primary.WithAlpha(34) : ColorScheme.SurfaceVariant, progress);
        var border = LerpColor(ColorScheme.Outline, Enabled ? ColorScheme.Primary : ColorScheme.Outline.WithAlpha(150), progress);

        _outerFillPaint.Color = outerFill;
        _outerBorderPaint.Color = border;
        _outerBorderPaint.StrokeWidth = Math.Max(1f, ScaleFactor);

        canvas.DrawCircle(center, radius, _outerFillPaint);
        canvas.DrawCircle(center, radius - _outerBorderPaint.StrokeWidth * 0.5f, _outerBorderPaint);

        if (progress <= 0.001f)
            return;

        _innerFillPaint.Color = (Enabled ? ColorScheme.Primary : ColorScheme.Outline).WithAlpha(
            (byte)Math.Clamp((int)Math.Round(255f * progress), 0, 255));
        canvas.DrawCircle(center, radius * 0.48f * progress, _innerFillPaint);
    }

    private void DrawRadioText(SKCanvas canvas, SKRect content, SKRect radioRect)
    {
        if (string.IsNullOrEmpty(Text))
            return;

        var textRect = content;
        var gap = GetTextGap();
        if (radioRect.MidX <= content.MidX)
            textRect.Left = Math.Min(content.Right, radioRect.Right + gap);
        else
            textRect.Right = Math.Max(content.Left, radioRect.Left - gap);

        if (textRect.Width <= 0f || textRect.Height <= 0f)
            return;

        using var font = CreateRenderFont(Font);
        _textPaint.Color = Enabled ? ForeColor : ColorScheme.ForeColor.WithAlpha(150);
        TextRenderer.DrawText(canvas, Text, textRect, _textPaint, font, TextAlign, AutoEllipsis, UseMnemonic, WrapMode);
    }

    private float GetCheckProgress()
    {
        if (_checkAnimation.IsAnimating())
            return Math.Clamp((float)_checkAnimation.GetProgress(), 0f, 1f);

        return _checked ? 1f : 0f;
    }

    private float GetRadioSize() => Math.Max(12f, DefaultRadioSize * ScaleFactor);

    private float GetTextGap() => DefaultTextGap * ScaleFactor;

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
