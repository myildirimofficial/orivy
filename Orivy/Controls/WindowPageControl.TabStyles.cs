using Orivy.Extensions;
using SkiaSharp;
using System;

namespace Orivy.Controls;

public partial class WindowPageControl
{
    private void ApplyCustomTabPalette(
        ref SKColor headerBackground,
        ref SKColor headerBorderColor,
        ref SKColor inactiveBackground,
        ref SKColor hoverBackground,
        ref SKColor selectedBackground,
        ref SKColor inactiveBorderColor,
        ref SKColor selectedBorderColor,
        ref SKColor activeTextColor,
        ref SKColor inactiveTextColor)
    {
        if (!_customTabStyle.HasValue)
            return;

        var style = _customTabStyle.Value;
        headerBackground = ResolveCustomColor(style.Header.BackgroundColor, headerBackground);
        headerBorderColor = ResolveCustomColor(style.Header.BorderColor, headerBorderColor);
        inactiveBackground = ResolveCustomColor(style.Normal.BackgroundColor, inactiveBackground);
        hoverBackground = ResolveCustomColor(style.Hover.BackgroundColor, hoverBackground);
        selectedBackground = ResolveCustomColor(style.Selected.BackgroundColor, selectedBackground);
        inactiveBorderColor = ResolveCustomColor(style.Normal.BorderColor, inactiveBorderColor);
        selectedBorderColor = ResolveCustomColor(style.Selected.BorderColor, selectedBorderColor);
        inactiveTextColor = ResolveCustomForeground(style.Normal.ForegroundColor, inactiveBackground, inactiveTextColor);
        activeTextColor = ResolveCustomForeground(style.Selected.ForegroundColor, selectedBackground, activeTextColor);
    }

    private bool TryDrawCustomTabBackground(SKCanvas canvas, SKRect rect, bool isSelected, bool isHovered, float hoverProgress,
        SKColor normalBackgroundFallback, SKColor hoverBackgroundFallback, SKColor selectedBackgroundFallback,
        SKColor normalBorderFallback, SKColor hoverBorderFallback, SKColor selectedBorderFallback,
        float defaultIndicatorThickness)
    {
        if (!_customTabStyle.HasValue)
            return false;

        var style = _customTabStyle.Value;
        if (!HasCustomTabSurface(style))
            return false;

        hoverProgress = Math.Clamp(hoverProgress, 0f, 1f);
        var visual = isSelected ? style.Selected : style.Normal;
        var borderThickness = visual.BorderThickness ?? style.Normal.BorderThickness ?? 0f;
        var blur = visual.Blur ?? style.Normal.Blur ?? 0f;
        SKColor backgroundColor;
        SKColor borderColor;
        if (isSelected)
        {
            backgroundColor = ResolveCustomColor(style.Selected.BackgroundColor, selectedBackgroundFallback);
            borderColor = ResolveCustomColor(style.Selected.BorderColor, selectedBorderFallback);
        }
        else
        {
            var normalBackground = ResolveCustomColor(style.Normal.BackgroundColor, normalBackgroundFallback);
            var hoverBackground = ResolveCustomColor(style.Hover.BackgroundColor, hoverBackgroundFallback);
            var normalBorder = ResolveCustomColor(style.Normal.BorderColor, normalBorderFallback);
            var hoverBorder = ResolveCustomColor(style.Hover.BorderColor, hoverBorderFallback);

            backgroundColor = normalBackground.InterpolateColor(hoverBackground, hoverProgress);
            borderColor = normalBorder.InterpolateColor(hoverBorder, hoverProgress);
            borderThickness = LerpNullable(style.Normal.BorderThickness, style.Hover.BorderThickness, borderThickness, hoverProgress);
            blur = LerpNullable(style.Normal.Blur, style.Hover.Blur, blur, hoverProgress);
        }

        var inset = (style.Metrics.SurfaceInset ?? Thickness.Empty).Scale(ScaleFactor);
        var surfaceRect = new SKRect(
            MathF.Round(rect.Left + inset.Left),
            MathF.Round(rect.Top + inset.Top),
            MathF.Round(rect.Right - inset.Right),
            MathF.Round(rect.Bottom - inset.Bottom));
        if (surfaceRect.Width <= 0f || surfaceRect.Height <= 0f)
            return true;

        var radiusValue = visual.BorderRadius ?? style.Normal.BorderRadius ?? 0f;
        if (!isSelected && isHovered)
        {
            var normalRadius = style.Normal.BorderRadius ?? radiusValue;
            var hoverRadius = style.Hover.BorderRadius ?? normalRadius;
            radiusValue = normalRadius + ((hoverRadius - normalRadius) * hoverProgress);
        }

        var radius = radiusValue * ScaleFactor;
        radius = Math.Clamp(radius, 0f, MathF.Min(surfaceRect.Width, surfaceRect.Height) * 0.5f);

        var scaledBlur = Math.Max(0f, blur) * ScaleFactor;
        if (scaledBlur > 0f && backgroundColor.Alpha > 0)
        {
            using var shadowFilter = SKMaskFilter.CreateBlur(SKBlurStyle.Normal, scaledBlur);
            using var shadowPaint = new SKPaint
            {
                IsAntialias = true,
                Color = backgroundColor.WithAlpha((byte)Math.Min(96, (int)backgroundColor.Alpha)),
                MaskFilter = shadowFilter,
                Style = SKPaintStyle.Fill
            };
            canvas.DrawRoundRect(surfaceRect, radius, radius, shadowPaint);
        }

        if (backgroundColor.Alpha > 0)
        {
            _tabBackgroundPaint.Color = backgroundColor;
            canvas.DrawRoundRect(surfaceRect, radius, radius, _tabBackgroundPaint);
        }

        var scaledBorderThickness = MathF.Max(0f, borderThickness) * ScaleFactor;
        if (scaledBorderThickness > 0f && borderColor.Alpha > 0)
        {
            _tabBorderPaint.Color = borderColor;
            _tabBorderPaint.StrokeWidth = MathF.Max(1f, MathF.Round(scaledBorderThickness));
            canvas.DrawRoundRect(surfaceRect, radius, radius, _tabBorderPaint);
        }

        if (isSelected)
        {
            var indicatorThickness = MathF.Max(0f, style.Indicator.Thickness ?? defaultIndicatorThickness / ScaleFactor) * ScaleFactor;
            var indicatorColor = ResolveCustomColor(style.Indicator.Color, ColorScheme.Primary);
            if (indicatorThickness > 0f && indicatorColor.Alpha > 0)
                DrawContentEdgeIndicator(canvas, rect, indicatorThickness, indicatorColor);
        }

        return true;
    }

    private static bool HasCustomTabSurface(in WindowTabStyle style)
    {
        return HasCustomVisual(style.Normal) ||
               HasCustomVisual(style.Hover) ||
               HasCustomVisual(style.Selected) ||
               style.Metrics.SurfaceInset.HasValue ||
               style.Indicator.Color != SKColors.Empty ||
               style.Indicator.Thickness.HasValue;
    }

    private static bool HasCustomVisual(in WindowTabVisual visual)
    {
        return visual.BackgroundColor != SKColors.Empty ||
               visual.BorderColor != SKColors.Empty ||
               visual.BorderRadius.HasValue ||
               visual.BorderThickness.HasValue ||
               visual.Blur.HasValue;
    }

    private static SKColor ResolveCustomColor(SKColor customColor, SKColor fallback)
    {
        return customColor == SKColors.Empty ? fallback : customColor;
    }

    private static float LerpNullable(float? from, float? to, float fallback, float progress)
    {
        var start = Math.Max(0f, from ?? fallback);
        var end = Math.Max(0f, to ?? start);
        return start + ((end - start) * Math.Clamp(progress, 0f, 1f));
    }

    private static SKColor ResolveCustomForeground(SKColor customColor, SKColor backgroundColor, SKColor fallback)
    {
        if (customColor != SKColors.Empty)
            return customColor;

        return backgroundColor != SKColors.Empty && backgroundColor.Alpha > 0
            ? backgroundColor.Determine()
            : fallback;
    }

    private SKColor ResolveCustomTabTextColor(bool isSelected, float hoverProgress,
        SKColor activeTextColor, SKColor inactiveTextColor,
        SKColor normalBackground, SKColor hoverBackground, SKColor selectedBackground)
    {
        if (!_customTabStyle.HasValue)
            return isSelected ? activeTextColor : inactiveTextColor;

        var style = _customTabStyle.Value;
        if (isSelected)
            return ResolveCustomForeground(style.Selected.ForegroundColor, selectedBackground, activeTextColor);

        hoverProgress = Math.Clamp(hoverProgress, 0f, 1f);
        var normalForeground = ResolveCustomForeground(style.Normal.ForegroundColor, normalBackground, inactiveTextColor);
        if (style.Hover.ForegroundColor == SKColors.Empty)
            return normalForeground;

        var hoverForeground = ResolveCustomForeground(style.Hover.ForegroundColor, hoverBackground, normalForeground);
        return normalForeground.InterpolateColor(hoverForeground, hoverProgress);
    }

    private void EnsureTabHoverState(int tabCount)
    {
        lock (_tabHoverSync)
            EnsureHoverStateArrays(ref _tabHoverProgress, ref _tabHoverTargets, tabCount);
    }

    private void EnsureWindowChromeTabHoverState(int tabCount)
    {
        lock (_tabHoverSync)
            EnsureHoverStateArrays(ref _windowChromeTabHoverProgress, ref _windowChromeTabHoverTargets, tabCount);
    }

    private void SetTabHoverTarget(int tabCount, int hoveredIndex)
    {
        SetHoverTargets(ref _tabHoverProgress, ref _tabHoverTargets, tabCount, hoveredIndex);
    }

    private void SetWindowChromeTabHoverTarget(int tabCount, int hoveredIndex)
    {
        SetHoverTargets(ref _windowChromeTabHoverProgress, ref _windowChromeTabHoverTargets, tabCount, hoveredIndex);
    }

    private void InvalidateTabHoverFrame()
    {
        InvalidateRenderTree();
        Invalidate();
    }

    private static void EnsureHoverStateArrays(ref float[] progress, ref float[] targets, int tabCount)
    {
        if (tabCount < 0)
            return;

        if (progress.Length != tabCount)
            Array.Resize(ref progress, tabCount);

        if (targets.Length != tabCount)
            Array.Resize(ref targets, tabCount);
    }

    private void SetHoverTargets(ref float[] progress, ref float[] targets, int tabCount, int hoveredIndex)
    {
        lock (_tabHoverSync)
        {
            EnsureHoverStateArrays(ref progress, ref targets, tabCount);
            for (var index = 0; index < targets.Length; index++)
                targets[index] = index == hoveredIndex ? 1f : 0f;
        }

        StartTabHoverTimer();
        InvalidateTabHoverFrame();
    }

    private void StartTabHoverTimer()
    {
        if (!_tabHoverTimer.Enabled)
            _tabHoverTimer.Start();
    }

    private void UpdateTabHoverAnimationFrame()
    {
        var stillAnimating = false;
        lock (_tabHoverSync)
        {
            stillAnimating |= StepHoverArray(_tabHoverProgress, _tabHoverTargets);
            stillAnimating |= StepHoverArray(_windowChromeTabHoverProgress, _windowChromeTabHoverTargets);
        }

        InvalidateTabHoverFrame();

        if (!stillAnimating)
            _tabHoverTimer.Stop();
    }

    private static bool StepHoverArray(float[] progress, float[] targets)
    {
        var stillAnimating = false;
        var count = Math.Min(progress.Length, targets.Length);

        for (var index = 0; index < count; index++)
        {
            var delta = targets[index] - progress[index];
            if (MathF.Abs(delta) <= 0.001f)
            {
                progress[index] = targets[index];
                continue;
            }

            progress[index] += delta * TabHoverAnimationStep;
            stillAnimating = true;
        }

        return stillAnimating;
    }

    private float GetTabHoverProgress(int tabIndex, bool isSelected)
    {
        if (isSelected || tabIndex < 0)
            return 0f;

        lock (_tabHoverSync)
        {
            return tabIndex < _tabHoverProgress.Length
                ? Math.Clamp(_tabHoverProgress[tabIndex], 0f, 1f)
                : 0f;
        }
    }

    private float GetWindowChromeTabHoverProgress(int tabIndex, bool isSelected)
    {
        if (isSelected || tabIndex < 0)
            return 0f;

        lock (_tabHoverSync)
        {
            return tabIndex < _windowChromeTabHoverProgress.Length
                ? Math.Clamp(_windowChromeTabHoverProgress[tabIndex], 0f, 1f)
                : 0f;
        }
    }

    private bool UsesStackedCenteredWindowChromeIcon()
    {
        return DrawTabIcons && ImageAlign is ContentAlignment.TopCenter or ContentAlignment.BottomCenter;
    }

    private float GetWindowChromeTabHorizontalContentPadding()
    {
        if (_customTabStyle.HasValue && _customTabStyle.Value.Metrics.Padding is { } customPadding)
            return Math.Max(customPadding.Left, customPadding.Right) * ScaleFactor;

        var padding = UsesStackedCenteredWindowChromeIcon()
            ? 12f
            : WindowChromeTabHorizontalPadding;

        return padding * ScaleFactor;
    }

    private float GetWindowChromeTabVerticalContentPadding()
    {
        if (_customTabStyle.HasValue && _customTabStyle.Value.Metrics.Padding is { } customPadding)
            return Math.Max(customPadding.Top, customPadding.Bottom) * ScaleFactor;

        var padding = UsesStackedCenteredWindowChromeIcon()
            ? 6.5f
            : WindowChromeTabCloseButtonInset;

        return padding * ScaleFactor;
    }

    private float GetTabHorizontalContentPadding()
    {
        if (_customTabStyle.HasValue && _customTabStyle.Value.Metrics.Padding is { } padding)
            return Math.Max(padding.Left, padding.Right) * ScaleFactor;

        return TabHorizontalPadding * ScaleFactor;
    }

    private float GetTabVerticalContentPadding()
    {
        if (_customTabStyle.HasValue && _customTabStyle.Value.Metrics.Padding is { } padding)
            return Math.Max(padding.Top, padding.Bottom) * ScaleFactor;

        return TabVerticalInset * ScaleFactor;
    }
}
