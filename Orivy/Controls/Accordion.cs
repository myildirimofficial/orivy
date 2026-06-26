using Orivy.Layout;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.ComponentModel;

namespace Orivy.Controls;

public class Accordion : Container
{
    private bool _allowMultipleExpanded;
    private int _gap = 8;
    private readonly Dictionary<Collapse, Radius> _itemRadii = new();

    public Accordion()
    {
        AutoSize = true;
        BackColor = SKColors.Transparent;
        Border = new Thickness(0);
    }

    [DefaultValue(false)]
    public bool AllowMultipleExpanded
    {
        get => _allowMultipleExpanded;
        set
        {
            if (_allowMultipleExpanded == value)
                return;

            _allowMultipleExpanded = value;
            if (!value)
                CollapseExtraExpandedItems(null);
        }
    }

    [DefaultValue(8)]
    public int Gap
    {
        get => _gap;
        set
        {
            var next = Math.Max(0, value);
            if (_gap == next)
                return;

            _gap = next;
            InvalidateMeasure();
            PerformLayout();
            Invalidate();
        }
    }

    public override void  OnControlAdded(ElementEventArgs e)
    {
        base.OnControlAdded(e);
        if (e.Element is Collapse collapse)
        {
            _itemRadii[collapse] = collapse.Radius;
            collapse.ExpandedChanged += HandleCollapseExpandedChanged;
        }

        PerformLayout();
    }

    public override void  OnControlRemoved(ElementEventArgs e)
    {
        if (e.Element is Collapse collapse)
        {
            collapse.ExpandedChanged -= HandleCollapseExpandedChanged;
            if (_itemRadii.Remove(collapse, out var radius))
                collapse.Radius = radius;
        }

        base.OnControlRemoved(e);
        PerformLayout();
    }

    public override void  OnLayout(LayoutEventArgs e)
    {
        base.OnLayout(e);

        var y = DisplayRectangle.Top + Padding.Top;
        var width = Math.Max(0f, DisplayRectangle.Width - Padding.Left - Padding.Right);
        var visibleCount = GetVisibleCollapseCount();
        var visibleIndex = 0;
        var borderOverlap = Gap == 0 && visibleCount > 1 ? 1f : 0f;
        for (var i = 0; i < Controls.Count; i++)
        {
            if (Controls[i] is not Collapse collapse || !collapse.Visible)
                continue;

            ApplyJoinedRadius(collapse, visibleIndex, visibleCount);
            var height = Math.Max(collapse.HeaderHeight, collapse.CurrentDisplayHeight);
            collapse.Bounds = SKRect.Create(DisplayRectangle.Left + Padding.Left, y, width, height);
            y += height + Gap - borderOverlap;
            visibleIndex++;
        }
    }

    public override SKSize GetPreferredSize(SKSize proposedSize)
    {
        var width = proposedSize.Width > 1 ? proposedSize.Width : Width;
        var height = (float)(Padding.Top + Padding.Bottom);
        var count = 0;
        for (var i = 0; i < Controls.Count; i++)
        {
            if (Controls[i] is not Collapse collapse || !collapse.Visible)
                continue;

            height += collapse.CurrentDisplayHeight;
            count++;
        }

        height += Gap * Math.Max(0, count - 1);
        if (Gap == 0 && count > 1)
            height -= count - 1;
        return new SKSize(width, height);
    }

    private int GetVisibleCollapseCount()
    {
        var count = 0;
        for (var i = 0; i < Controls.Count; i++)
        {
            if (Controls[i] is Collapse { Visible: true })
                count++;
        }

        return count;
    }

    private void ApplyJoinedRadius(Collapse collapse, int visibleIndex, int visibleCount)
    {
        if (!_itemRadii.TryGetValue(collapse, out var original))
            _itemRadii[collapse] = original = collapse.Radius;

        if (Gap != 0 || visibleCount <= 1)
        {
            collapse.Radius = original;
            return;
        }

        var isFirst = visibleIndex == 0;
        var isLast = visibleIndex == visibleCount - 1;
        collapse.Radius = new Radius(
            isFirst ? original.TopLeft : 0,
            isFirst ? original.TopRight : 0,
            isLast ? original.BottomLeft : 0,
            isLast ? original.BottomRight : 0);
    }

    private void HandleCollapseExpandedChanged(object? sender, EventArgs e)
    {
        if (!AllowMultipleExpanded && sender is Collapse opened && opened.IsExpanded)
            CollapseExtraExpandedItems(opened);

        PerformLayout();
        Invalidate();
    }

    private void CollapseExtraExpandedItems(Collapse? keep)
    {
        for (var i = 0; i < Controls.Count; i++)
        {
            if (Controls[i] is Collapse collapse && !ReferenceEquals(collapse, keep) && collapse.IsExpanded)
                collapse.IsExpanded = false;
        }
    }
}
