using Orivy.Layout;
using SkiaSharp;
using System;
using System.ComponentModel;

namespace Orivy.Controls;

public enum BadgeVariant
{
    Primary,
    Secondary,
    Success,
    Warning,
    Danger,
    Outline
}

public class Badge : ElementBase
{
    public Badge()
    {
        AutoSize = true;
        CommonProperties.SetSelfAutoSizeInDefaultLayout(this, true);
        AutoSizeMode = AutoSizeMode.GrowOnly;
        MinimumSize = new SKSize(20, 18);
        Padding = new Thickness(9, 3, 9, 3);
        Radius = new Radius(999);
        Border = new Thickness(0);
        TextAlign = ContentAlignment.MiddleCenter;
        Text = "Badge";
        Variant = BadgeVariant.Primary;
        ColorScheme.ThemeChanged += HandleThemeChanged;
    }

    private BadgeVariant _variant = (BadgeVariant)(-1);

    [DefaultValue(BadgeVariant.Primary)]
    public BadgeVariant Variant
    {
        get => _variant;
        set
        {
            if (_variant == value)
                return;

            _variant = value;
            ApplyVariant();
        }
    }

    public override SKSize GetPreferredSize(SKSize proposedSize)
    {
        using var font = CreateRenderFont(Font);
        var width = MathF.Ceiling(font.MeasureText(Text) + Padding.Left + Padding.Right + Border.Left + Border.Right);
        var metrics = font.Metrics;
        var height = MathF.Ceiling(metrics.Descent - metrics.Ascent + Padding.Top + Padding.Bottom + Border.Top + Border.Bottom);
        return new SKSize(Math.Max(MinimumSize.Width, width), Math.Max(MinimumSize.Height, height));
    }

    private void ApplyVariant()
    {
        var primary = ColorScheme.Primary;
        var surface = ColorScheme.SurfaceContainerHigh;
        var fore = ColorScheme.ForeColor;

        (BackColor, ForeColor, Border, BorderColor) = Variant switch
        {
            BadgeVariant.Secondary => (surface.WithAlpha(180), fore.WithAlpha(210), new Thickness(0), SKColors.Transparent),
            BadgeVariant.Success => (new SKColor(220, 252, 231), new SKColor(21, 128, 61), new Thickness(0), SKColors.Transparent),
            BadgeVariant.Warning => (new SKColor(254, 243, 199), new SKColor(180, 83, 9), new Thickness(0), SKColors.Transparent),
            BadgeVariant.Danger => (new SKColor(254, 226, 226), new SKColor(185, 28, 28), new Thickness(0), SKColors.Transparent),
            BadgeVariant.Outline => (SKColors.Transparent, primary, new Thickness(1), primary.WithAlpha(150)),
            _ => (primary.WithAlpha(32), primary.Brightness(-0.12f), new Thickness(0), SKColors.Transparent)
        };

        InvalidateMeasure();
        Invalidate();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
            ColorScheme.ThemeChanged -= HandleThemeChanged;

        base.Dispose(disposing);
    }

    private void HandleThemeChanged(object? sender, EventArgs e) => ApplyVariant();
}
