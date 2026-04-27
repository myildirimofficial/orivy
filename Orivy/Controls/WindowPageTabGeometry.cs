using SkiaSharp;
using System;
using System.Collections.Generic;

namespace Orivy.Controls;

internal static class WindowPageTabGeometry
{
    /// <summary>
    /// Returns the content block dimensions (WITHOUT padding) for a tab.
    /// Inline layout (MiddleLeft / MiddleRight): icon beside text horizontally.
    /// Stacked layout (all other alignments): icon and text stacked vertically.
    /// </summary>
    public static (float blockW, float blockH) MeasureContentBlockSize(
        string? text, bool hasIcon, SKFont font,
        float iconSize, float iconSpacing, ContentAlignment imageAlign)
    {
        var textW = string.IsNullOrEmpty(text) ? 0f : font.MeasureText(text);
        var metrics = font.Metrics;
        var textH = Math.Max(1f, metrics.Descent - metrics.Ascent);

        if (!hasIcon)
            return (Math.Max(0f, textW), textH);

        var isInline = imageAlign is ContentAlignment.MiddleLeft or ContentAlignment.MiddleRight;
        if (isInline)
            return (Math.Max(0f, iconSize + iconSpacing + textW), Math.Max(iconSize, textH));

        // Stacked: width = wider of icon or text; height = icon + spacing + text line
        return (Math.Max(iconSize, Math.Max(0f, textW)),
                Math.Max(0f, iconSize + iconSpacing + textH));
    }

    public static float MeasureDesiredTabWidth(ElementBase page, SKFont font, float horizontalPadding,
        float iconSize, float iconSpacing, float closeButtonAllowance, float minWidth, float maxWidth,
        bool includeIcon, bool includeCloseButton,
        ContentAlignment imageAlign = ContentAlignment.MiddleLeft)
    {
        var hasIcon = includeIcon && page.Image != null;
        var (blockW, _) = MeasureContentBlockSize(page.Text, hasIcon, font, iconSize, iconSpacing, imageAlign);

        var width = blockW + horizontalPadding * 2f;
        if (includeCloseButton)
            width += closeButtonAllowance;

        return Math.Clamp(width, minWidth, maxWidth);
    }

    public static float MeasureDesiredTabHeight(string? text, bool hasIcon, SKFont font, float verticalPadding,
        float iconSize, float iconSpacing, float trailingButtonSize, float minHeight, float maxHeight,
        ContentAlignment imageAlign = ContentAlignment.MiddleLeft)
    {
        var (_, blockH) = MeasureContentBlockSize(text, hasIcon, font, iconSize, iconSpacing, imageAlign);
        var height = Math.Max(blockH, trailingButtonSize) + verticalPadding * 2f;
        return Math.Clamp(height, minHeight, maxHeight);
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
        SKRect trailingRect, float trailingSpacing,
        ContentAlignment imageAlign = ContentAlignment.MiddleLeft)
    {
        var textRect = new SKRect(
            tabRect.Left + horizontalPadding,
            tabRect.Top,
            tabRect.Right - horizontalPadding,
            tabRect.Bottom);

        if (iconRect.Width > 0f)
        {
            var isLeft  = imageAlign is ContentAlignment.TopLeft   or ContentAlignment.MiddleLeft   or ContentAlignment.BottomLeft;
            var isRight = imageAlign is ContentAlignment.TopRight  or ContentAlignment.MiddleRight  or ContentAlignment.BottomRight;

            if (isLeft)
                textRect.Left = iconRect.Right + iconSpacing;
            else if (isRight)
                textRect.Right = Math.Min(textRect.Right, iconRect.Left - iconSpacing);
            // center: icon floats independently, text uses its full horizontal span
        }

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