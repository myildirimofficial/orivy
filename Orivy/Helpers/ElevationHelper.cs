
using SkiaSharp;

namespace Orivy.Helpers;

/// <summary>
///     Modern elevation system for Material Design 3 style depth
/// </summary>
public static class ElevationHelper
{
    /// <summary>
    ///     Draws elevation shadow and tint for a surface
    /// </summary>
    public static void DrawElevation(SKCanvas canvas, SkiaSharp.SKRect bounds, float cornerRadius, int elevation)
    {
        if (elevation <= 0) return;

        var blur = ColorScheme.GetElevationBlur(elevation);
        var offset = ColorScheme.GetElevationOffset(elevation);
        var shadowColor = ColorScheme.ShadowColor.WithAlpha((byte)(ColorScheme.IsDarkMode ? 40 : 15));

        // Draw shadow
        using (var shadowMaskFilter = SKMaskFilter.CreateBlur(SKBlurStyle.Normal, blur / 2))
        using (var shadowPaint = new SKPaint
        {
            IsAntialias = true,
            Color = shadowColor,
            MaskFilter = shadowMaskFilter
        })
        {
            var shadowBounds = new SkiaSharp.SKRect(
                bounds.Left,
                bounds.Top + offset,
                bounds.Right,
                bounds.Bottom + offset
            );
            canvas.DrawRoundRect(shadowBounds, cornerRadius, cornerRadius, shadowPaint);
        }

        // Draw elevation tint (for dark mode)
        if (ColorScheme.IsDarkMode && elevation > 0)
        {
            var tint = ColorScheme.GetElevationTint(elevation);
            using var tintPaint = new SKPaint
            {
                IsAntialias = true,
                Color = tint
            };
            canvas.DrawRoundRect(bounds, cornerRadius, cornerRadius, tintPaint);
        }
    }

    public static void DrawFluentGlass(SKCanvas canvas, SKRect bounds, float cornerRadius, SKColor? tintColor = null)
    {
        var baseColor = tintColor ?? new SKColor(220, 220, 220, 180);
        float frequencyX = 0.1f;
        float frequencyY = 0.1f;

        using var backdropFilterPaint = new SKPaint
        {
            FilterQuality = SKFilterQuality.High,
            ImageFilter = SKImageFilter.CreateBlur(15, 15, SKShaderTileMode.Clamp)
        };

        canvas.SaveLayer(bounds, backdropFilterPaint);
        canvas.DrawRect(bounds, new SKPaint { Color = baseColor });

        using (var noiseShader = SKShader.CreatePerlinNoiseFractalNoise(frequencyX, frequencyY, 1, 0))
        using (var noisePaint = new SKPaint
        {
            Shader = noiseShader,
            BlendMode = SKBlendMode.SoftLight,
            Color = SKColors.White.WithAlpha(40)
        })
        {
            canvas.DrawRoundRect(bounds, cornerRadius, cornerRadius, noisePaint);
        }

        using (var shineShader = SKShader.CreateLinearGradient(
            new SKPoint(bounds.Left, bounds.Top),
            new SKPoint(bounds.Left, bounds.Top + bounds.Height * 0.5f),
            new[] { new SKColor(255, 255, 255, 100), new SKColor(255, 255, 255, 0) },
            new[] { 0f, 1f },
            SKShaderTileMode.Clamp))
        using (var shinePaint = new SKPaint { Shader = shineShader, BlendMode = SKBlendMode.SrcOver })
        {
            canvas.DrawRoundRect(bounds, cornerRadius, cornerRadius, shinePaint);
        }

        using (var borderPaint = new SKPaint
        {
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 1.5f,
            IsAntialias = true,
            Color = SKColors.White.WithAlpha(80),
            PathEffect = SKPathEffect.CreateDash(new[] { 1f, 0.5f }, 0)
        })
        {
            canvas.DrawRoundRect(bounds, cornerRadius, cornerRadius, borderPaint);
        }

        canvas.Restore();
    }


    /// <summary>
    ///     Draws a smooth gradient overlay for glassmorphism effect
    /// </summary>
    public static void DrawGlassEffect(SKCanvas canvas, SKRect bounds, float cornerRadius)
    {
        using var paint = new SKPaint
        {
            IsAntialias = true
        };

        var shader = SKShader.CreateLinearGradient(
            new SKPoint(bounds.Left, bounds.Top),
            new SKPoint(bounds.Left, bounds.Bottom),
            new[] {
            new SKColor(255, 255, 255, 160),
            new SKColor(220, 230, 240, 100)
            },
            null,
            SKShaderTileMode.Clamp
        );

        paint.Shader = shader;
        paint.Style = SKPaintStyle.Fill;

        canvas.DrawRoundRect(bounds, cornerRadius, cornerRadius, paint);

        using var strokePaint = new SKPaint
        {
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 1f,
            IsAntialias = true,
            Color = SKColors.White.WithAlpha(180)
        };

        var strokeBounds = bounds;
        strokeBounds.Inflate(-0.5f, -0.5f);
        canvas.DrawRoundRect(strokeBounds, cornerRadius, cornerRadius, strokePaint);
    }


    /// <summary>
    ///     Draws modern ripple effect at specified position
    /// </summary>
    public static void DrawRipple(SKCanvas canvas, SKPoint center, float radius, float progress, SKColor color)
    {
        var alpha = (byte)(255 * (1 - progress));
        using var paint = new SKPaint
        {
            IsAntialias = true,
            Color = color.WithAlpha(alpha)
        };

        canvas.DrawCircle(center, radius * progress, paint);
    }

    /// <summary>
    ///     Creates a smooth state layer for hover/focus/press states
    /// </summary>
    public static void DrawStateLayer(SKCanvas canvas, SkiaSharp.SKRect bounds, float cornerRadius, SKColor stateColor)
    {
        using var paint = new SKPaint
        {
            IsAntialias = true,
            Color = stateColor
        };

        canvas.DrawRoundRect(bounds, cornerRadius, cornerRadius, paint);
    }
}