using Orivy.Animation;
using Orivy.Helpers;
using Orivy.Layout;
using SkiaSharp;
using System;
using System.ComponentModel;

namespace Orivy.Controls;

public class Button : ElementBase
{
    private bool _checked;
    private bool _keyboardPressArmed;  
    private ContextMenuStrip? _dropDownMenu;
    private readonly AnimationManager _dropDownChevronAnimation;
    private readonly SKPaint _dropDownTextPaint = new() { IsAntialias = true, Style = SKPaintStyle.Fill };
    private readonly SKPaint _dropDownChevronPaint = new() { IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeCap = SKStrokeCap.Round, StrokeJoin = SKStrokeJoin.Round };
    private readonly SKPaint _dropDownDividerPaint = new() { IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeCap = SKStrokeCap.Round };
    private readonly SKPath _dropDownChevronPath = new();

    public Button()
    {
        AutoSize = true;
        CommonProperties.SetSelfAutoSizeInDefaultLayout(this, true);
        AutoSizeMode = AutoSizeMode.GrowOnly;
        AutoEllipsis = true;
        WrapMode = TextWrap.None;
        CanSelect = true;
        MinimumSize = new SKSize(45, 24);
        Padding = new Thickness(8);
        Radius = new Radius(12);
        Size = new SKSize(45, 24);
        TabStop = true;
        TextAlign = ContentAlignment.MiddleCenter;

        _dropDownChevronAnimation = new AnimationManager(true)
        {
            AnimationType = AnimationType.CubicEaseOut,
            InterruptAnimation = true,
            Increment = 16d / 120d,
            SecondaryIncrement = 16d / 100d
        };
        _dropDownChevronAnimation.OnAnimationProgress += HandleDropDownChevronAnimationProgress;
        _dropDownChevronAnimation.OnAnimationFinished += HandleDropDownChevronAnimationFinished;

        ConfigureVisualStyles(styles =>
        {
            styles
                .DefaultTransition(TimeSpan.FromMilliseconds(140), AnimationType.CubicEaseOut)
                .Base(baseStyle => baseStyle
                    .Background(ColorScheme.Primary)
                    .Foreground(SKColors.White)
                    .Border(1)
                    .BorderColor(ColorScheme.Primary.Brightness(-0.18f))
                    .Radius(12)
                    .Shadow(new BoxShadow(0f, 6f, 14f, 0, ColorScheme.ShadowColor.WithAlpha(26))))
                .OnHover(rule => rule
                    .Background(ColorScheme.Primary.Brightness(0.06f))
                    .BorderColor(ColorScheme.Primary.Brightness(-0.08f))
                    .Shadow(new BoxShadow(0f, 10f, 20f, 0, ColorScheme.Primary.WithAlpha(34))))
                .OnPressed(rule => rule
                    .Background(ColorScheme.Primary.Brightness(-0.08f))
                    .BorderColor(ColorScheme.Primary.Brightness(-0.24f))
                    .Opacity(0.94f)
                    .Shadow(new BoxShadow(0f, 3f, 10f, 0, ColorScheme.Primary.WithAlpha(22))))
                .OnChecked(rule => rule
                    .Background(ColorScheme.Primary.Brightness(-0.04f))
                    .Foreground(SKColors.White)
                    .BorderColor(ColorScheme.Primary.Brightness(-0.2f))
                    .Shadow(new BoxShadow(0f, 3f, 10f, 0, ColorScheme.Primary.WithAlpha(28))))
                .OnFocused(rule => rule
                    .Border(2)
                    .BorderColor(ColorScheme.Primary.Brightness(0.18f)))
                .OnDisabled(rule => rule
                    .Background(ColorScheme.SurfaceVariant)
                    .Foreground(ColorScheme.ForeColor.WithAlpha(170))
                    .BorderColor(ColorScheme.Outline.WithAlpha(140))
                    .Opacity(0.8f)
                    .Shadow(BoxShadow.None));
        });
    }

    [DefaultValue(false)]
    public bool CheckOnClick { get; set; }

    [DefaultValue(false)]
    public bool Checked
    {
        get => _checked;
        set
        {
            if (_checked == value)
                return;

            _checked = value;
            RefreshVisualStylesForStateChange();
            OnCheckedChanged(EventArgs.Empty);
            Invalidate();
        }
    }

    public event EventHandler? CheckedChanged;

    public event EventHandler? DropDownOpening;

    [DefaultValue(false)]
    public bool ShowDropDownArrow { get; set; }

    [DefaultValue(true)]
    public bool OpenDropDownOnClick { get; set; } = true;

    [DefaultValue(true)]
    public bool SplitDropDownButton { get; set; } = true;

    [DefaultValue(true)]
    public bool ShowDropDownDivider { get; set; } = true;

    public ContextMenuStrip? DropDownMenu
    {
        get => _dropDownMenu;
        set
        {
            if (ReferenceEquals(_dropDownMenu, value))
                return;

            if (_dropDownMenu != null)
                _dropDownMenu.Closed -= HandleDropDownMenuClosed;

            _dropDownMenu = value;

            if (_dropDownMenu != null)
                _dropDownMenu.Closed += HandleDropDownMenuClosed;

            InvalidateMeasure();
            Invalidate();
        }
    }

    protected override bool ShouldRenderDefaultText => !ShouldDrawDropDownGlyph;

    public override void OnClick(EventArgs e)
    {
        if (CheckOnClick)
            Checked = !Checked;

        base.OnClick(e);

        if (OpenDropDownOnClick && !UseSplitDropDownHitTest)
            ShowDropDown();
    }

    protected virtual void OnCheckedChanged(EventArgs e)
    {
        CheckedChanged?.Invoke(this, e);
    }

    protected override bool GetVisualCheckedState() => Checked;

    public void ShowDropDown()
    {
        if (DropDownMenu == null || !Enabled || !Visible)
            return;

        if (DropDownMenu.IsOpen)
        {
            DropDownMenu.Hide();
            StartDropDownChevronAnimation(false);
            return;
        }

        DropDownOpening?.Invoke(this, EventArgs.Empty);
        DropDownMenu.ShowAnchoredBelow(this, ClientRectangle);
        if (DropDownMenu.IsOpen)
            StartDropDownChevronAnimation(true);
    }

    public override SKSize GetPreferredSize(SKSize proposedSize)
    {
        using var font = CreateRenderFont(Font);
        var measurementConstraints = proposedSize;
        if (measurementConstraints.Width <= 1)
            measurementConstraints.Width = short.MaxValue;
        if (measurementConstraints.Height <= 1)
            measurementConstraints.Height = short.MaxValue;

        var textSize = TextRenderer.MeasureText(
            Text,
            font,
            measurementConstraints,
            new TextRenderOptions
            {
                MaxWidth = measurementConstraints.Width,
                Trimming = AutoEllipsis ? TextTrimming.CharacterEllipsis : TextTrimming.None,
                UseMnemonic = UseMnemonic,
                Wrap = TextWrap.None
            });

        var desiredWidth = textSize.Width + Padding.Left + Padding.Right + Border.Left + Border.Right + GetDropDownGlyphWidth();
        var desiredHeight = textSize.Height + Padding.Top + Padding.Bottom + Border.Top + Border.Bottom;

        if (AutoSizeMode == AutoSizeMode.GrowOnly)
        {
            desiredWidth = Math.Max(desiredWidth, Size.Width);
            desiredHeight = Math.Max(desiredHeight, Size.Height);
        }

        if (MinimumSize.Width > 0)
            desiredWidth = Math.Max(desiredWidth, MinimumSize.Width);
        if (MinimumSize.Height > 0)
            desiredHeight = Math.Max(desiredHeight, MinimumSize.Height);

        if (MaximumSize.Width > 0)
            desiredWidth = Math.Min(desiredWidth, MaximumSize.Width);
        if (MaximumSize.Height > 0)
            desiredHeight = Math.Min(desiredHeight, MaximumSize.Height);

        return new SKSize((float)Math.Ceiling(desiredWidth), (float)Math.Ceiling(desiredHeight));
    }

    public override void OnPaint(SKCanvas canvas)
    {
        base.OnPaint(canvas);

        if (!ShouldDrawDropDownGlyph)
            return;

        var content = DisplayRectangle;
        var chevronWidth = GetDropDownGlyphWidth();
        var chevronRect = new SKRect(
            Math.Max(content.Left, content.Right - chevronWidth),
            content.Top,
            content.Right,
            content.Bottom);
        var textRect = new SKRect(
            content.Left,
            content.Top,
            Math.Max(content.Left, chevronRect.Left - 2f * ScaleFactor),
            content.Bottom);

        DrawDropDownButtonText(canvas, textRect);
        DrawDropDownDivider(canvas, chevronRect);
        DrawDropDownChevron(canvas, chevronRect);
    }

    protected internal override void OnMouseClick(MouseEventArgs e)
    {
        if (UseSplitDropDownHitTest && e.Button == MouseButtons.Left && GetDropDownChevronRect().Contains(e.Location))
        {
            ShowDropDown();
            e.Handled = true;
            return;
        }

        base.OnMouseClick(e);
    }

    internal override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
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
            PerformClick();
    }

    internal override void OnLostFocus(EventArgs e)
    {
        _keyboardPressArmed = false;
        base.OnLostFocus(e);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _dropDownChevronAnimation.OnAnimationProgress -= HandleDropDownChevronAnimationProgress;
            _dropDownChevronAnimation.OnAnimationFinished -= HandleDropDownChevronAnimationFinished;
            if (_dropDownMenu != null)
                _dropDownMenu.Closed -= HandleDropDownMenuClosed;
            _dropDownChevronAnimation.Dispose();
            _dropDownTextPaint.Dispose();
            _dropDownChevronPaint.Dispose();
            _dropDownDividerPaint.Dispose();
            _dropDownChevronPath.Dispose();
        }

        base.Dispose(disposing);
    }

    private bool ShouldDrawDropDownGlyph => ShowDropDownArrow || DropDownMenu != null;

    private float GetDropDownGlyphWidth()
    {
        if (!ShouldDrawDropDownGlyph)
            return 0f;

        return UseSplitDropDownHitTest || ShowDropDownDivider
            ? Math.Max(32f, 34f * ScaleFactor)
            : Math.Max(18f, 22f * ScaleFactor);
    }

    private bool UseSplitDropDownHitTest => SplitDropDownButton && ShouldDrawDropDownGlyph && DropDownMenu != null;

    private SKRect GetDropDownChevronRect()
    {
        var content = DisplayRectangle;
        var chevronWidth = GetDropDownGlyphWidth();
        return new SKRect(
            Math.Max(content.Left, content.Right - chevronWidth),
            content.Top,
            content.Right,
            content.Bottom);
    }

    private void DrawDropDownButtonText(SKCanvas canvas, SKRect textRect)
    {
        if (string.IsNullOrEmpty(Text) || textRect.Width <= 0f || textRect.Height <= 0f)
            return;

        using var font = CreateRenderFont(Font);
        _dropDownTextPaint.Color = ForeColor;
        TextRenderer.DrawText(canvas, Text, textRect, _dropDownTextPaint, font, TextAlign, AutoEllipsis, UseMnemonic, WrapMode);
    }

    private void DrawDropDownChevron(SKCanvas canvas, SKRect rect)
    {
        if (rect.Width <= 0f || rect.Height <= 0f)
            return;

        var progress = Math.Clamp((float)_dropDownChevronAnimation.GetProgress(), 0f, 1f);
        var size = Math.Max(4f, 4.5f * ScaleFactor);
        var centerX = rect.MidX;
        var centerY = rect.MidY + progress * 1.5f * ScaleFactor;
        var rotation = progress * 180f;

        _dropDownChevronPaint.Color = ForeColor.WithAlpha((byte)Math.Clamp(ForeColor.Alpha * 0.86f, 0f, 255f));
        _dropDownChevronPaint.StrokeWidth = Math.Max(1.4f, 1.5f * ScaleFactor);
        _dropDownChevronPath.Reset();
        _dropDownChevronPath.MoveTo(centerX - size, centerY - size * 0.35f);
        _dropDownChevronPath.LineTo(centerX, centerY + size * 0.55f);
        _dropDownChevronPath.LineTo(centerX + size, centerY - size * 0.35f);

        var save = canvas.Save();
        canvas.RotateDegrees(rotation, centerX, centerY);
        canvas.DrawPath(_dropDownChevronPath, _dropDownChevronPaint);
        canvas.RestoreToCount(save);
    }

    private void DrawDropDownDivider(SKCanvas canvas, SKRect chevronRect)
    {
        if (!ShowDropDownDivider || chevronRect.Width <= 0f || chevronRect.Height <= 0f)
            return;

        var x = MathF.Round(chevronRect.Left) + 0.5f;
        var inset = Math.Max(7f, 8f * ScaleFactor);
        _dropDownDividerPaint.Color = ForeColor.WithAlpha((byte)Math.Clamp(ForeColor.Alpha * 0.32f, 72f, 150f));
        _dropDownDividerPaint.StrokeWidth = Math.Max(1f, ScaleFactor);
        canvas.DrawLine(x, chevronRect.Top + inset, x, chevronRect.Bottom - inset, _dropDownDividerPaint);
    }

    private void StartDropDownChevronAnimation(bool opening)
    {
        _dropDownChevronAnimation.StartNewAnimation(opening ? AnimationDirection.In : AnimationDirection.Out);
    }

    private void HandleDropDownChevronAnimationProgress(object _)
    {
        Invalidate();
    }

    private void HandleDropDownChevronAnimationFinished(object _)
    {
        Invalidate();
    }

    private void HandleDropDownMenuClosed(object? sender, EventArgs e)
    {
        StartDropDownChevronAnimation(false);
    }
}
