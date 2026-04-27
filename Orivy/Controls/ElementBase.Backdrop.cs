using SkiaSharp;
using System;
using System.ComponentModel;

namespace Orivy.Controls;

public abstract partial class ElementBase
{
    private readonly SKPaint _renderBackdropPaint = new() { IsAntialias = true, Style = SKPaintStyle.Fill };
    private readonly SKPaint _renderBackdropAccentPaint = new() { IsAntialias = false, Style = SKPaintStyle.Fill };
    private readonly SKPaint _renderBackdropBorderPaint = new() { IsAntialias = true, Style = SKPaintStyle.Stroke };
    private readonly SKPaint _renderBackdropNoisePaint = new() { IsAntialias = false };

    // Gradient and specular shaders — rebuilt only when bounds/color/mode/theme change.
    private SKShader? _backdropFillShader;
    private SKShader? _backdropTopGradientShader;
    private SKShader? _backdropNoiseShader;
    private SKRect _backdropShaderBounds;
    private SKColor _backdropShaderBaseColor;
    private ElementBackdropMode _backdropShaderMode;
    private bool _backdropShaderIsDark;

    private ElementBackdropMode _backdropMode;
    private SKColor _backdropColor = SKColors.Empty;

    [Category("Appearance")]
    [DefaultValue(ElementBackdropMode.None)]
    public ElementBackdropMode BackdropMode
    {
        get => _backdropMode;
        set
        {
            if (_backdropMode == value)
                return;

            _backdropMode = value;
            Invalidate();
        }
    }

    [Category("Appearance")]
    public SKColor BackdropColor
    {
        get => _backdropColor;
        set
        {
            if (_backdropColor == value)
                return;

            _backdropColor = value;
            Invalidate();
        }
    }

    private void RenderBackdropMaterial(SKCanvas canvas, SKRect bounds, bool hasRadius)
    {
        if (_backdropMode == ElementBackdropMode.None || bounds.Width <= 0f || bounds.Height <= 0f)
            return;

        var baseColor = ResolveBackdropBaseColor();
        var isDark = ColorScheme.IsDarkMode;

        EnsureBackdropShaders(bounds, baseColor, _backdropMode, isDark);

        var contrast = isDark ? SKColors.White : SKColors.Black;
        var scale = Math.Max(1f, ScaleFactor);

        switch (_backdropMode)
        {
            case ElementBackdropMode.Tint:
                DrawBackdropTint(canvas, bounds, hasRadius, isDark, contrast, scale);
                break;
            case ElementBackdropMode.Glass:
                DrawBackdropGlass(canvas, bounds, hasRadius, isDark, scale);
                break;
            case ElementBackdropMode.Acrylic:
                DrawBackdropAcrylic(canvas, bounds, hasRadius, isDark, scale);
                break;
            case ElementBackdropMode.Mica:
                DrawBackdropMica(canvas, bounds, hasRadius, isDark, contrast, scale);
                break;
        }
    }

    private void EnsureBackdropShaders(SKRect bounds, SKColor baseColor, ElementBackdropMode mode, bool isDark)
    {
        bool needsRebuild =
            _backdropFillShader == null ||
            _backdropShaderBounds != bounds ||
            _backdropShaderBaseColor != baseColor ||
            _backdropShaderMode != mode ||
            _backdropShaderIsDark != isDark;

        if (!needsRebuild)
            return;

        _backdropFillShader?.Dispose();
        _backdropFillShader = null;
        _backdropTopGradientShader?.Dispose();
        _backdropTopGradientShader = null;

        var start = new SKPoint(bounds.Left, bounds.Top);
        var end = new SKPoint(bounds.Left, bounds.Bottom);

        switch (mode)
        {
            case ElementBackdropMode.Tint:
            {
                byte alpha = ResolveBackdropAlpha(baseColor, isDark ? (byte)88 : (byte)100);
                _backdropFillShader = SKShader.CreateLinearGradient(start, end,
                    new SKColor[]
                    {
                        baseColor.Brightness(0.06f).WithAlpha(alpha),
                        baseColor.WithAlpha(alpha),
                        baseColor.Brightness(-0.04f).WithAlpha(ClampByte(alpha - 10))
                    },
                    new float[] { 0f, 0.55f, 1f },
                    SKShaderTileMode.Clamp);
                break;
            }

            case ElementBackdropMode.Glass:
            {
                byte alpha = ResolveBackdropAlpha(baseColor, isDark ? (byte)80 : (byte)94);
                _backdropFillShader = SKShader.CreateLinearGradient(start, end,
                    new SKColor[]
                    {
                        baseColor.Brightness(isDark ? 0.20f : 0.16f).WithAlpha(alpha),
                        baseColor.WithAlpha(ClampByte(alpha - 6)),
                        baseColor.Brightness(isDark ? -0.08f : -0.06f).WithAlpha(ClampByte(alpha - 18))
                    },
                    new float[] { 0f, 0.48f, 1f },
                    SKShaderTileMode.Clamp);

                // Specular fade: white → transparent over the top 38% of height
                var specEnd = new SKPoint(bounds.Left, bounds.Top + bounds.Height * 0.38f);
                _backdropTopGradientShader = SKShader.CreateLinearGradient(start, specEnd,
                    new SKColor[]
                    {
                        SKColors.White.WithAlpha(isDark ? (byte)44 : (byte)70),
                        SKColors.White.WithAlpha(0)
                    },
                    new float[] { 0f, 1f },
                    SKShaderTileMode.Clamp);
                break;
            }

            case ElementBackdropMode.Acrylic:
            {
                byte alpha = ResolveBackdropAlpha(baseColor, isDark ? (byte)122 : (byte)136);
                _backdropFillShader = SKShader.CreateLinearGradient(start, end,
                    new SKColor[]
                    {
                        baseColor.Brightness(isDark ? 0.12f : 0.10f).WithAlpha(alpha),
                        baseColor.WithAlpha(ClampByte(alpha - 10)),
                        baseColor.Brightness(isDark ? -0.05f : -0.04f).WithAlpha(ClampByte(alpha - 22))
                    },
                    new float[] { 0f, 0.52f, 1f },
                    SKShaderTileMode.Clamp);

                // Specular fade over top 30% of height
                var specEnd = new SKPoint(bounds.Left, bounds.Top + bounds.Height * 0.30f);
                _backdropTopGradientShader = SKShader.CreateLinearGradient(start, specEnd,
                    new SKColor[]
                    {
                        SKColors.White.WithAlpha(isDark ? (byte)32 : (byte)54),
                        SKColors.White.WithAlpha(0)
                    },
                    new float[] { 0f, 1f },
                    SKShaderTileMode.Clamp);
                break;
            }

            case ElementBackdropMode.Mica:
            {
                byte alpha = ResolveBackdropAlpha(baseColor, isDark ? (byte)150 : (byte)172);
                _backdropFillShader = SKShader.CreateLinearGradient(start, end,
                    new SKColor[]
                    {
                        baseColor.Brightness(isDark ? 0.07f : 0.09f).WithAlpha(alpha),
                        baseColor.WithAlpha(ClampByte(alpha - 4)),
                        baseColor.Brightness(isDark ? -0.03f : -0.02f).WithAlpha(ClampByte(alpha - 8))
                    },
                    new float[] { 0f, 0.45f, 1f },
                    SKShaderTileMode.Clamp);
                break;
            }
        }

        _backdropShaderBounds = bounds;
        _backdropShaderBaseColor = baseColor;
        _backdropShaderMode = mode;
        _backdropShaderIsDark = isDark;
    }

    private void DrawBackdropTint(SKCanvas canvas, SKRect bounds, bool hasRadius, bool isDark, SKColor contrast, float scale)
    {
        _renderBackdropPaint.Shader = _backdropFillShader;
        _renderBackdropPaint.Color = SKColors.White;
        DrawBackdropFill(canvas, bounds, hasRadius, _renderBackdropPaint);
        _renderBackdropPaint.Shader = null;
        DrawBackdropBorder(canvas, bounds, hasRadius, contrast.WithAlpha(isDark ? (byte)22 : (byte)16), scale);
    }

    private void DrawBackdropGlass(SKCanvas canvas, SKRect bounds, bool hasRadius, bool isDark, float scale)
    {
        _renderBackdropPaint.Shader = _backdropFillShader;
        _renderBackdropPaint.Color = SKColors.White;
        DrawBackdropFill(canvas, bounds, hasRadius, _renderBackdropPaint);
        _renderBackdropPaint.Shader = null;

        if (_backdropTopGradientShader != null)
        {
            _renderBackdropAccentPaint.Shader = _backdropTopGradientShader;
            _renderBackdropAccentPaint.Color = SKColors.White;
            _renderBackdropAccentPaint.BlendMode = SKBlendMode.SrcOver;
            DrawBackdropFill(canvas, bounds, hasRadius, _renderBackdropAccentPaint);
            _renderBackdropAccentPaint.Shader = null;
        }

        DrawSpecularEdge(canvas, bounds, hasRadius, SKColors.White.WithAlpha(isDark ? (byte)64 : (byte)96), scale);
        DrawBackdropBorder(canvas, bounds, hasRadius, SKColors.White.WithAlpha(isDark ? (byte)30 : (byte)50), scale);
    }

    private void DrawBackdropAcrylic(SKCanvas canvas, SKRect bounds, bool hasRadius, bool isDark, float scale)
    {
        _renderBackdropPaint.Shader = _backdropFillShader;
        _renderBackdropPaint.Color = SKColors.White;
        DrawBackdropFill(canvas, bounds, hasRadius, _renderBackdropPaint);
        _renderBackdropPaint.Shader = null;

        if (_backdropTopGradientShader != null)
        {
            _renderBackdropAccentPaint.Shader = _backdropTopGradientShader;
            _renderBackdropAccentPaint.Color = SKColors.White;
            _renderBackdropAccentPaint.BlendMode = SKBlendMode.SrcOver;
            DrawBackdropFill(canvas, bounds, hasRadius, _renderBackdropAccentPaint);
            _renderBackdropAccentPaint.Shader = null;
        }

        // Grain noise layer — Perlin fractal noise at Overlay blend mode creates the
        // Acrylic texture feel without requiring a live blur pass.
        _backdropNoiseShader ??= SKShader.CreatePerlinNoiseFractalNoise(0.65f, 0.65f, 2, 0f);
        _renderBackdropNoisePaint.Shader = _backdropNoiseShader;
        _renderBackdropNoisePaint.Color = SKColors.White.WithAlpha(isDark ? (byte)22 : (byte)30);
        _renderBackdropNoisePaint.BlendMode = SKBlendMode.Overlay;
        DrawBackdropFill(canvas, bounds, hasRadius, _renderBackdropNoisePaint);
        _renderBackdropNoisePaint.Shader = null;

        DrawSpecularEdge(canvas, bounds, hasRadius, SKColors.White.WithAlpha(isDark ? (byte)56 : (byte)80), scale);
        DrawBackdropBorder(canvas, bounds, hasRadius, SKColors.White.WithAlpha(isDark ? (byte)32 : (byte)44), Math.Max(1f, scale * 1.1f));
    }

    private void DrawBackdropMica(SKCanvas canvas, SKRect bounds, bool hasRadius, bool isDark, SKColor contrast, float scale)
    {
        _renderBackdropPaint.Shader = _backdropFillShader;
        _renderBackdropPaint.Color = SKColors.White;
        DrawBackdropFill(canvas, bounds, hasRadius, _renderBackdropPaint);
        _renderBackdropPaint.Shader = null;
        DrawBackdropBorder(canvas, bounds, hasRadius, contrast.WithAlpha(isDark ? (byte)18 : (byte)16), scale);
    }

    private void DrawBackdropFill(SKCanvas canvas, SKRect bounds, bool hasRadius, SKPaint paint)
    {
        if (!hasRadius)
        {
            canvas.DrawRect(bounds, paint);
            return;
        }

        canvas.Save();
        var clipRRect = GetScratchRoundRect(bounds, _radius);
        canvas.ClipRoundRect(clipRRect, SKClipOperation.Intersect, true);
        canvas.DrawRect(bounds, paint);
        canvas.Restore();
    }

    private void DrawSpecularEdge(SKCanvas canvas, SKRect bounds, bool hasRadius, SKColor color, float scale)
    {
        float thickness = Math.Max(1f, scale);
        var edgeRect = new SKRect(bounds.Left, bounds.Top, bounds.Right, bounds.Top + thickness);
        _renderBackdropAccentPaint.Shader = null;
        _renderBackdropAccentPaint.Color = color;
        _renderBackdropAccentPaint.BlendMode = SKBlendMode.SrcOver;

        if (!hasRadius)
        {
            canvas.DrawRect(edgeRect, _renderBackdropAccentPaint);
            return;
        }

        canvas.Save();
        var clipRRect = GetScratchRoundRect(bounds, _radius);
        canvas.ClipRoundRect(clipRRect, SKClipOperation.Intersect, false);
        canvas.DrawRect(edgeRect, _renderBackdropAccentPaint);
        canvas.Restore();
    }

    private SKColor ResolveBackdropBaseColor()
    {
        if (_backdropColor != SKColors.Empty)
            return _backdropColor;

        if (BackColor != SKColors.Transparent)
            return BackColor;

        return ColorScheme.Surface;
    }

    private static byte ResolveBackdropAlpha(SKColor color, byte fallbackAlpha)
    {
        if (color.Alpha == 0 || color.Alpha == 255)
            return fallbackAlpha;

        return color.Alpha;
    }

    private static byte ClampByte(int value) => (byte)Math.Clamp(value, 0, 255);

    private void DrawBackdropBorder(SKCanvas canvas, SKRect bounds, bool hasRadius, SKColor color, float strokeWidth)
    {
        _renderBackdropBorderPaint.Color = color;
        _renderBackdropBorderPaint.StrokeWidth = strokeWidth;

        if (hasRadius)
        {
            var inset = strokeWidth * 0.5f;
            var borderRect = new SKRect(bounds.Left + inset, bounds.Top + inset, bounds.Right - inset, bounds.Bottom - inset);
            var borderRoundRect = GetScratchRoundRect(borderRect, _radius);
            canvas.DrawRoundRect(borderRoundRect, _renderBackdropBorderPaint);
            return;
        }

        canvas.DrawRect(bounds, _renderBackdropBorderPaint);
    }

    private void DisposeBackdropMaterialSystem()
    {
        _renderBackdropPaint.Dispose();
        _renderBackdropAccentPaint.Dispose();
        _renderBackdropBorderPaint.Dispose();
        _renderBackdropNoisePaint.Dispose();
        _backdropFillShader?.Dispose();
        _backdropTopGradientShader?.Dispose();
        _backdropNoiseShader?.Dispose();
    }
}
