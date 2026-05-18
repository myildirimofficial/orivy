using Orivy.Animation;
using Orivy.Layout;
using SkiaSharp;
using System;
using System.ComponentModel;

namespace Orivy.Controls;

public class ToggleButton : Button
{
    public ToggleButton()
    {
        CheckOnClick = true;
        Text = "Toggle";
        MinimumSize = new SKSize(74, 32);
        Size = new SKSize(96, 34);
        Padding = new Thickness(12, 7, 12, 7);
        Radius = new Radius(10);

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
                .Background(ColorScheme.SurfaceContainerHigh)
                .BorderColor(ColorScheme.Primary.WithAlpha(112)))
            .OnPressed(rule => rule
                .Background(ColorScheme.Primary.WithAlpha(42))
                .Scale(0.985f))
            .OnChecked(rule => rule
                .Background(ColorScheme.Primary)
                .Foreground(SKColors.White)
                .BorderColor(ColorScheme.Primary.Brightness(-0.14f))
                .Shadow(new BoxShadow(0f, 6f, 14f, 0, ColorScheme.Primary.WithAlpha(26))))
            .OnFocused(rule => rule
                .Border(2)
                .BorderColor(ColorScheme.Primary.WithAlpha(220)))
            .OnDisabled(rule => rule
                .Background(ColorScheme.SurfaceVariant)
                .Foreground(ColorScheme.ForeColor.WithAlpha(150))
                .BorderColor(ColorScheme.Outline.WithAlpha(70))
                .Opacity(0.72f)
                .Shadow(BoxShadow.None)),
            clearExisting: true);

        ConfigureMotionEffects(scene => scene
            .Rectangle(effect => effect
                .Anchor(0.5f, 0.5f)
                .Size(56f, 10f)
                .Drift(4f, 0f)
                .CornerRadius(8f)
                .Color(ColorScheme.Primary.WithAlpha(12))
                .Opacity(0.01f, 0.07f)
                .Scale(0.9f, 1.08f)
                .Duration(4.8d)
                .SpeedOnHover(1.5f)
                .SpeedOnPressed(2.2f)
                .SpeedOnFocused(1.8f)));
    }
}
