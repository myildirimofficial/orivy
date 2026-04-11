using SkiaSharp;
using System;
using System.Runtime.CompilerServices;

namespace Orivy;

public static class ColorExtensions
{
    public static bool IsEmpty(this SKColor color)
    {
        return color == SKColors.Empty;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static SKColor InterpolateColor(this SKColor start, SKColor end, float progress)
    {
        var t = Math.Clamp(progress, 0f, 1f);

        byte r = (byte)Math.Round(start.Red + (end.Red - start.Red) * t);
        byte g = (byte)Math.Round(start.Green + (end.Green - start.Green) * t);
        byte b = (byte)Math.Round(start.Blue + (end.Blue - start.Blue) * t);
        byte a = (byte)Math.Round(start.Alpha + (end.Alpha - start.Alpha) * t);

        return new SKColor(r, g, b, a);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static SKColor Determine(this SKColor color)
    {
        var value = 0;

        var luminance = (0.299 * color.Red + 0.587 * color.Green + 0.114 * color.Blue) / 255;

        if (luminance > 0.5)
            value = 0; // bright colors - black font
        else
            value = 255; // dark colors - white font

        return new SKColor((byte)value, (byte)value, (byte)value).WithAlpha(color.Alpha);
    }

    /// <summary>
    ///     Creates color with corrected brightness.
    /// </summary>
    /// <param name="color">Color to correct.</param>
    /// <param name="correctionFactor">
    ///     The brightness correction factor. Must be between -1 and 1.
    ///     Negative values produce darker colors.
    /// </param>
    /// <returns>
    ///     Corrected <see cref="Color" /> structure.
    /// </returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static SKColor Brightness(this SKColor color, float factor)
    {
        // factor: -1 .. +1

        float r = color.Red / 255f;
        float g = color.Green / 255f;
        float b = color.Blue / 255f;

        // Gamma expand (sRGB -> linear)
        r = MathF.Pow(r, 2.2f);
        g = MathF.Pow(g, 2.2f);
        b = MathF.Pow(b, 2.2f);

        // Apply brightness
        r = Math.Clamp(r + factor, 0f, 1f);
        g = Math.Clamp(g + factor, 0f, 1f);
        b = Math.Clamp(b + factor, 0f, 1f);

        // Gamma compress (linear -> sRGB)
        r = MathF.Pow(r, 1f / 2.2f);
        g = MathF.Pow(g, 1f / 2.2f);
        b = MathF.Pow(b, 1f / 2.2f);

        return new SKColor(
            (byte)(r * 255),
            (byte)(g * 255),
            (byte)(b * 255),
            color.Alpha
        );
    }

    /// <summary>
    ///     Is the color dark <c>true</c>; otherwise <c>false</c>
    /// </summary>
    /// <param name="color">The color</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsDark(this SKColor color)
    {
        return 384 - color.Red - color.Green - color.Blue > 0;
    }

    /// <summary>
    ///     Removes the alpha component of a color.
    /// </summary>
    /// <param name="color"></param>
    /// <returns></returns>
    public static SKColor RemoveAlpha(this SKColor color)
    {
        color.WithAlpha(255);
        return color;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static SKColor BlendWith(this SKColor bg, SKColor fg, double blend)
    {
        double fgA = (fg.Alpha / 255.0) * blend;
        double bgA = bg.Alpha / 255.0;

        double outA = fgA + bgA * (1 - fgA);

        if (outA <= 0)
            return new SKColor(0, 0, 0, 0);

        // normalize RGB
        double fgR = fg.Red / 255.0;
        double fgG = fg.Green / 255.0;
        double fgB = fg.Blue / 255.0;

        double bgR = bg.Red / 255.0;
        double bgG = bg.Green / 255.0;
        double bgB = bg.Blue / 255.0;

        double outR = (fgR * fgA + bgR * bgA * (1 - fgA)) / outA;
        double outG = (fgG * fgA + bgG * bgA * (1 - fgA)) / outA;
        double outB = (fgB * fgA + bgB * bgA * (1 - fgA)) / outA;

        return new SKColor(
            (byte)(outR * 255),
            (byte)(outG * 255),
            (byte)(outB * 255),
            (byte)(outA * 255)
        );
    }

    public static float HueToRgb(float p, float q, float t)
    {
        if (t < 0) t += 1;
        if (t > 1) t -= 1;
        if (t < 1f / 6f) return p + (q - p) * 6f * t;
        if (t < 1f / 2f) return q;
        if (t < 2f / 3f) return p + (q - p) * (2f / 3f - t) * 6f;
        return p;
    }

    public static float NormalizeHue(float hue)
    {
        var h = hue % 360f;
        return h < 0f ? h + 360f : h;
    }

    public static SKColor HsvToColor(float hue, float saturation, float value, byte alpha)
    {
        hue = NormalizeHue(hue);
        saturation = Math.Clamp(saturation, 0f, 1f);
        value = Math.Clamp(value, 0f, 1f);

        if (saturation <= 0.0001f)
        {
            var gray = (byte)Math.Round(value * 255f);
            return new SKColor(gray, gray, gray, alpha);
        }

        var sector = hue / 60f;
        var index = (int)Math.Floor(sector);
        var fraction = sector - index;
        var p = value * (1f - saturation);
        var q = value * (1f - saturation * fraction);
        var t = value * (1f - saturation * (1f - fraction));

        var (r, g, b) = index switch
        {
            0 => (value, t, p),
            1 => (q, value, p),
            2 => (p, value, t),
            3 => (p, q, value),
            4 => (t, p, value),
            _ => (value, p, q)
        };

        return new SKColor(
            (byte)Math.Round(r * 255f),
            (byte)Math.Round(g * 255f),
            (byte)Math.Round(b * 255f),
            alpha);
    }

    public static void RgbToHsv(SKColor color,
        out float hue, out float saturation, out float value)
    {
        color.ToHsv(out hue, out saturation, out value);

        var r = color.Red / 255f;
        var g = color.Green / 255f;
        var b = color.Blue / 255f;

        var max = Math.Max(r, Math.Max(g, b));
        var min = Math.Min(r, Math.Min(g, b));
        var delta = max - min;

        value = max;
        saturation = max <= 0.0001f ? 0f : delta / max;

        if (delta <= 0.0001f) { hue = 0f; return; }

        if (Math.Abs(max - r) < 0.0001f) hue = 60f * (((g - b) / delta) % 6f);
        else if (Math.Abs(max - g) < 0.0001f) hue = 60f * (((b - r) / delta) + 2f);
        else hue = 60f * (((r - g) / delta) + 4f);

        hue = NormalizeHue(hue);
    }
}