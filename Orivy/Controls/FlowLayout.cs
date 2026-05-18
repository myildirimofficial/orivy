using Orivy.Layout;
using SkiaSharp;
using System;
using System.ComponentModel;

namespace Orivy.Controls;

public class FlowLayout : Container
{
    private FlowLayoutDirection _flowDirection = FlowLayoutDirection.LeftToRight;
    private bool _wrapContents = true;
    private float _horizontalGap = 8f;
    private float _verticalGap = 8f;

    public FlowLayout()
    {
        AutoScroll = true;
        BackColor = SKColors.Transparent;
    }

    [DefaultValue(FlowLayoutDirection.LeftToRight)]
    public FlowLayoutDirection FlowDirection
    {
        get => _flowDirection;
        set
        {
            if (_flowDirection == value)
                return;

            _flowDirection = value;
            PerformLayout();
            Invalidate();
        }
    }

    [DefaultValue(true)]
    public bool WrapContents
    {
        get => _wrapContents;
        set
        {
            if (_wrapContents == value)
                return;

            _wrapContents = value;
            PerformLayout();
            Invalidate();
        }
    }

    [DefaultValue(8f)]
    public float HorizontalGap
    {
        get => _horizontalGap;
        set
        {
            var normalized = Math.Max(0f, value);
            if (Math.Abs(_horizontalGap - normalized) < 0.001f)
                return;

            _horizontalGap = normalized;
            PerformLayout();
            Invalidate();
        }
    }

    [DefaultValue(8f)]
    public float VerticalGap
    {
        get => _verticalGap;
        set
        {
            var normalized = Math.Max(0f, value);
            if (Math.Abs(_verticalGap - normalized) < 0.001f)
                return;

            _verticalGap = normalized;
            PerformLayout();
            Invalidate();
        }
    }

    protected override void OnLayout(LayoutEventArgs e)
    {
        if (FlowDirection is FlowLayoutDirection.LeftToRight or FlowLayoutDirection.RightToLeft)
            LayoutHorizontal();
        else
            LayoutVertical();
    }

    private void LayoutHorizontal()
    {
        var display = DisplayRectangle;
        var x = FlowDirection == FlowLayoutDirection.LeftToRight ? display.Left : display.Right;
        var y = display.Top;
        var lineHeight = 0f;
        var availableWidth = Math.Max(1f, display.Width);

        for (var i = 0; i < Controls.Count; i++)
        {
            if (Controls[i] is not ElementBase child || !child.Visible)
                continue;

            var preferred = GetChildSize(child, new SKSize(availableWidth, display.Height));
            var needsWrap = WrapContents && lineHeight > 0f &&
                (FlowDirection == FlowLayoutDirection.LeftToRight
                    ? x + preferred.Width > display.Right
                    : x - preferred.Width < display.Left);

            if (needsWrap)
            {
                x = FlowDirection == FlowLayoutDirection.LeftToRight ? display.Left : display.Right;
                y += lineHeight + VerticalGap;
                lineHeight = 0f;
            }

            if (FlowDirection == FlowLayoutDirection.LeftToRight)
            {
                child.Bounds = new SKRect(x, y, x + preferred.Width, y + preferred.Height);
                x += preferred.Width + HorizontalGap;
            }
            else
            {
                child.Bounds = new SKRect(x - preferred.Width, y, x, y + preferred.Height);
                x -= preferred.Width + HorizontalGap;
            }

            lineHeight = Math.Max(lineHeight, preferred.Height);
        }
    }

    private void LayoutVertical()
    {
        var display = DisplayRectangle;
        var x = display.Left;
        var y = FlowDirection == FlowLayoutDirection.TopDown ? display.Top : display.Bottom;
        var columnWidth = 0f;
        var availableHeight = Math.Max(1f, display.Height);

        for (var i = 0; i < Controls.Count; i++)
        {
            if (Controls[i] is not ElementBase child || !child.Visible)
                continue;

            var preferred = GetChildSize(child, new SKSize(display.Width, availableHeight));
            var needsWrap = WrapContents && columnWidth > 0f &&
                (FlowDirection == FlowLayoutDirection.TopDown
                    ? y + preferred.Height > display.Bottom
                    : y - preferred.Height < display.Top);

            if (needsWrap)
            {
                x += columnWidth + HorizontalGap;
                y = FlowDirection == FlowLayoutDirection.TopDown ? display.Top : display.Bottom;
                columnWidth = 0f;
            }

            if (FlowDirection == FlowLayoutDirection.TopDown)
            {
                child.Bounds = new SKRect(x, y, x + preferred.Width, y + preferred.Height);
                y += preferred.Height + VerticalGap;
            }
            else
            {
                child.Bounds = new SKRect(x, y - preferred.Height, x + preferred.Width, y);
                y -= preferred.Height + VerticalGap;
            }

            columnWidth = Math.Max(columnWidth, preferred.Width);
        }
    }

    private static SKSize GetChildSize(ElementBase child, SKSize proposed)
    {
        var size = child.Size;
        if (child.AutoSize)
            size = child.GetPreferredSize(proposed);

        return new SKSize(Math.Max(1f, size.Width), Math.Max(1f, size.Height));
    }
}
