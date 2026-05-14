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

    public override void OnClick(EventArgs e)
    {
        if (CheckOnClick)
            Checked = !Checked;

        base.OnClick(e);
    }

    protected virtual void OnCheckedChanged(EventArgs e)
    {
        CheckedChanged?.Invoke(this, e);
    }

    protected override bool GetVisualCheckedState() => Checked;

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

        var desiredWidth = textSize.Width + Padding.Left + Padding.Right + Border.Left + Border.Right;
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
}
