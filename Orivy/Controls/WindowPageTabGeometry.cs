using SkiaSharp;
using System;
using System.Collections.Generic;

namespace Orivy.Controls;

internal static class WindowPageTabGeometry
{
    public static float MeasureDesiredTabWidth(ElementBase page, SKFont font, float horizontalPadding,
        float iconAllowance, float closeButtonAllowance, float minWidth, float maxWidth,
        bool includeIcon, bool includeCloseButton)
    {
        font.MeasureText(page.Text ?? string.Empty, out var bounds);

        var width = bounds.Width + horizontalPadding * 2f;
        if (includeIcon && page.Image != null)
            width += iconAllowance;

        if (includeCloseButton)
            width += closeButtonAllowance;

        return Math.Clamp(width, minWidth, maxWidth);
    }

    public static void LayoutTabs(IReadOnlyList<float> desiredWidths, float startX, float top, float height,
        float availableWidth, float gap, float maxWidth, bool distributeExtraSpace, List<SKRect> destination)
    {
        destination.Clear();

        if (desiredWidths.Count == 0 || height <= 0f || availableWidth <= 0f)
            return;

        var totalDesiredWidth = 0f;
        for (var i = 0; i < desiredWidths.Count; i++)
            totalDesiredWidth += desiredWidths[i];

        var totalGapWidth = gap * Math.Max(0, desiredWidths.Count - 1);
        var widthBudget = Math.Max(0f, availableWidth - totalGapWidth);

        var scale = 1f;
        var extraPerTab = 0f;

        if (totalDesiredWidth > widthBudget && totalDesiredWidth > 0f)
        {
            scale = widthBudget / totalDesiredWidth;
        }
        else if (distributeExtraSpace && totalDesiredWidth < widthBudget)
        {
            extraPerTab = (widthBudget - totalDesiredWidth) / desiredWidths.Count;
        }

        var currentX = startX;
        for (var i = 0; i < desiredWidths.Count; i++)
        {
            var width = desiredWidths[i] * scale + extraPerTab;
            width = Math.Min(width, maxWidth);
            width = Math.Max(0f, width);

            destination.Add(SKRect.Create(currentX, top, width, height));
            currentX += width + gap;
        }
    }

    public static void LayoutTabsVertical(IReadOnlyList<float> desiredHeights, float left, float startY, float width,
        float availableHeight, float gap, float maxHeight, bool distributeExtraSpace, List<SKRect> destination)
    {
        destination.Clear();

        if (desiredHeights.Count == 0 || width <= 0f || availableHeight <= 0f)
            return;

        var totalDesiredHeight = 0f;
        for (var i = 0; i < desiredHeights.Count; i++)
            totalDesiredHeight += desiredHeights[i];

        var totalGapHeight = gap * Math.Max(0, desiredHeights.Count - 1);
        var heightBudget = Math.Max(0f, availableHeight - totalGapHeight);

        var scale = 1f;
        var extraPerTab = 0f;

        if (totalDesiredHeight > heightBudget && totalDesiredHeight > 0f)
        {
            scale = heightBudget / totalDesiredHeight;
        }
        else if (distributeExtraSpace && totalDesiredHeight < heightBudget)
        {
            extraPerTab = (heightBudget - totalDesiredHeight) / desiredHeights.Count;
        }

        var currentY = startY;
        for (var i = 0; i < desiredHeights.Count; i++)
        {
            var height = desiredHeights[i] * scale + extraPerTab;
            height = Math.Min(height, maxHeight);
            height = Math.Max(0f, height);

            destination.Add(SKRect.Create(left, currentY, width, height));
            currentY += height + gap;
        }
    }

    public static SKRect CreateTrailingButtonRect(SKRect tabRect, float preferredSize, float trailingInset,
        float minimumSize, float widthFactor = 1.5f)
    {
        var availableWidth = Math.Max(0f, tabRect.Width - trailingInset * widthFactor);
        if (availableWidth <= 0f)
            return SKRect.Empty;

        var size = Math.Min(preferredSize, availableWidth);
        if (size < minimumSize)
            return SKRect.Empty;

        return SKRect.Create(
            tabRect.Right - trailingInset - size,
            tabRect.MidY - size / 2f,
            size,
            size);
    }

    public static SKRect CreateTextRect(SKRect tabRect, float horizontalPadding, SKRect iconRect, float iconSpacing,
        SKRect trailingRect, float trailingSpacing)
    {
        var textRect = new SKRect(
            tabRect.Left + horizontalPadding,
            tabRect.Top,
            tabRect.Right - horizontalPadding,
            tabRect.Bottom);

        if (iconRect.Width > 0f)
            textRect.Left = iconRect.Right + iconSpacing;

        if (trailingRect.Width > 0f)
            textRect.Right = Math.Min(textRect.Right, trailingRect.Left - trailingSpacing);

        return EnsureEllipsisTextRect(textRect);
    }

    public static SKRect EnsureEllipsisTextRect(SKRect rect)
    {
        if (rect.Width > 0f)
            return rect;

        var centerX = rect.MidX;
        return new SKRect(centerX - 0.5f, rect.Top, centerX + 0.5f, rect.Bottom);
    }

    public static SKRect InterpolateRect(SKRect from, SKRect to, float progress)
    {
        var t = Math.Clamp(progress, 0f, 1f);
        return new SKRect(
            from.Left + (to.Left - from.Left) * t,
            from.Top + (to.Top - from.Top) * t,
            from.Right + (to.Right - from.Right) * t,
            from.Bottom + (to.Bottom - from.Bottom) * t);
    }

    public static void BuildTopRoundedTabPath(SKPath path, SKRect rect, float radius)
    {
        path.Reset();

        if (rect.Width <= 0f || rect.Height <= 0f)
            return;

        var clampedRadius = Math.Min(radius, Math.Min(rect.Width / 2f, rect.Height));

        path.MoveTo(rect.Left, rect.Bottom);
        path.LineTo(rect.Left, rect.Top + clampedRadius);
        path.QuadTo(rect.Left, rect.Top, rect.Left + clampedRadius, rect.Top);
        path.LineTo(rect.Right - clampedRadius, rect.Top);
        path.QuadTo(rect.Right, rect.Top, rect.Right, rect.Top + clampedRadius);
        path.LineTo(rect.Right, rect.Bottom);
        path.Close();
    }
}