using Orivy.Layout;
using SkiaSharp;
using System;
using System.ComponentModel;

namespace Orivy.Controls;

public sealed class BreadcrumbItemClickedEventArgs : EventArgs
{
    public BreadcrumbItemClickedEventArgs(int index, ElementBase item)
    {
        Index = index;
        Item = item;
    }

    public int Index { get; }

    public ElementBase Item { get; }

    public string Text => Item.Text;
}

public class Breadcrumb : Container
{
    private readonly SKPaint _chevronPaint = new() { IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeCap = SKStrokeCap.Round, StrokeJoin = SKStrokeJoin.Round };
    private int _selectedIndex = -1;

    public Breadcrumb()
    {
        Height = 36;
        Padding = new Thickness(0);
        BackColor = SKColors.Transparent;
        Border = new Thickness(0);
        Radius = new Radius(0);
        Gap = 8;
        SeparatorWidth = 14;
        AutoSize = true;
        CommonProperties.SetSelfAutoSizeInDefaultLayout(this, true);
        ColorScheme.ThemeChanged += HandleThemeChanged;
    }

    [DefaultValue(-1)]
    public int SelectedIndex
    {
        get => _selectedIndex;
        set
        {
            var next = Math.Clamp(value, -1, Controls.Count - 1);
            if (_selectedIndex == next)
                return;

            _selectedIndex = next;
            ApplyItemStates();
            Invalidate();
        }
    }

    [DefaultValue(8)]
    public int Gap { get; set; }

    [DefaultValue(14)]
    public int SeparatorWidth { get; set; }

    public event EventHandler<BreadcrumbItemClickedEventArgs>? ItemClicked;

    public Element AddItem(string text)
    {
        var item = CreateTextItem(text);
        Controls.Add(item);
        return item;
    }

    public void SetItems(params string[] items)
    {
        Controls.Clear();
        for (var i = 0; i < items.Length; i++)
            Controls.Add(CreateTextItem(items[i]));

        SelectedIndex = Controls.Count - 1;
        PerformLayout();
        InvalidateMeasure();
        Invalidate();
    }

    public override SKSize GetPreferredSize(SKSize proposedSize)
    {
        var width = (float)(Padding.Left + Padding.Right);
        var height = (float)(Padding.Top + Padding.Bottom);
        var visibleCount = 0;

        for (var i = 0; i < Controls.Count; i++)
        {
            if (Controls[i] is not ElementBase child || !child.Visible)
                continue;

            var size = GetChildSize(child);
            width += size.Width;
            height = Math.Max(height, size.Height + Padding.Top + Padding.Bottom);
            visibleCount++;
        }

        if (visibleCount > 1)
            width += (visibleCount - 1) * (Gap * 2 + SeparatorWidth);

        return new SKSize(Math.Max(MinimumSize.Width, width), Math.Max(MinimumSize.Height, Math.Max(32f, height)));
    }

    public override void  OnPaint(SKCanvas canvas)
    {
        base.OnPaint(canvas);

        var visibleIndex = 0;
        ElementBase? previous = null;
        for (var i = 0; i < Controls.Count; i++)
        {
            if (Controls[i] is not ElementBase child || !child.Visible)
                continue;

            if (previous != null)
                DrawChevron(canvas, previous.Bounds.Right + Gap + SeparatorWidth / 2f, child.Bounds.MidY);

            previous = child;
            visibleIndex++;
        }
    }

    public override void  OnLayout(LayoutEventArgs e)
    {
        base.OnLayout(e);

        var x = DisplayRectangle.Left + Padding.Left;
        var availableHeight = Math.Max(1f, DisplayRectangle.Height - Padding.Top - Padding.Bottom);
        var visibleIndex = 0;

        for (var i = 0; i < Controls.Count; i++)
        {
            if (Controls[i] is not ElementBase child || !child.Visible)
                continue;

            var size = GetChildSize(child);
            var y = DisplayRectangle.Top + Padding.Top + Math.Max(0f, (availableHeight - size.Height) / 2f);
            child.Bounds = SKRect.Create(x, y, size.Width, size.Height);
            x += size.Width;

            visibleIndex++;
            if (visibleIndex < GetVisibleChildCount())
                x += Gap * 2 + SeparatorWidth;
        }

        ApplyItemStates();
    }

    public override void  OnMouseClick(MouseEventArgs e)
    {
        base.OnMouseClick(e);
        if (e.Handled)
            return;

        var index = HitTest(e.Location);
        if (index < 0)
            return;

        SelectedIndex = index;
        ItemClicked?.Invoke(this, new BreadcrumbItemClickedEventArgs(index, (ElementBase)Controls[index]));
        e.Handled = true;
    }

    public override void  OnControlAdded(ElementEventArgs e)
    {
        base.OnControlAdded(e);
        if (e.Element is ElementBase child)
            PrepareItem(child);

        if (SelectedIndex < 0)
            SelectedIndex = Controls.Count - 1;

        InvalidateMeasure();
        PerformLayout();
        Invalidate();
    }

    public override void  OnControlRemoved(ElementEventArgs e)
    {
        base.OnControlRemoved(e);
        if (SelectedIndex >= Controls.Count)
            SelectedIndex = Controls.Count - 1;

        InvalidateMeasure();
        PerformLayout();
        Invalidate();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            ColorScheme.ThemeChanged -= HandleThemeChanged;
            _chevronPaint.Dispose();
        }

        base.Dispose(disposing);
    }

    private static Element CreateTextItem(string text)
    {
        return new Element
        {
            Text = text,
            AutoSize = true,
            MinimumSize = new SKSize(32, 28),
            Padding = new Thickness(9, 5, 9, 5),
            BackColor = SKColors.Transparent,
            Border = new Thickness(0),
            Radius = new Radius(7),
            TextAlign = ContentAlignment.MiddleCenter,
            WrapMode = TextWrap.None,
            CanSelect = false,
            TabStop = false
        };
    }

    private void PrepareItem(ElementBase child)
    {
        child.AutoSize = child.AutoSize || child.Width <= 0 || child.Height <= 0;
        child.Margin = new Thickness(0);
    }

    private SKSize GetChildSize(ElementBase child)
    {
        if (!child.AutoSize)
            return child.Size;

        var preferred = child.GetPreferredSize(new SKSize(short.MaxValue, Math.Max(32f, Height)));
        return new SKSize(Math.Max(1f, preferred.Width), Math.Max(1f, preferred.Height));
    }

    private int GetVisibleChildCount()
    {
        var count = 0;
        for (var i = 0; i < Controls.Count; i++)
        {
            if (Controls[i] is ElementBase child && child.Visible)
                count++;
        }

        return count;
    }

    private int HitTest(SKPoint location)
    {
        var visibleIndex = 0;
        for (var i = 0; i < Controls.Count; i++)
        {
            if (Controls[i] is not ElementBase child || !child.Visible)
                continue;

            if (child.Bounds.Contains(location))
                return i;

            visibleIndex++;
        }

        return -1;
    }

    private void ApplyItemStates()
    {
        for (var i = 0; i < Controls.Count; i++)
        {
            if (Controls[i] is not ElementBase child)
                continue;

            var selected = i == SelectedIndex || (SelectedIndex < 0 && i == Controls.Count - 1);
            child.BackColor = selected ? ColorScheme.SurfaceContainer.WithAlpha(92) : SKColors.Transparent;
            child.Border = selected ? new Thickness(1) : new Thickness(0);
            child.BorderColor = selected ? ColorScheme.Outline.WithAlpha(82) : SKColors.Transparent;
            child.ForeColor = selected ? ColorScheme.ForeColor : ColorScheme.ForeColor.WithAlpha(158);
            if (child is Button button)
                button.Checked = selected;
        }
    }

    private void HandleThemeChanged(object? sender, EventArgs e)
    {
        ApplyItemStates();
        Invalidate();
    }

    private void DrawChevron(SKCanvas canvas, float x, float centerY)
    {
        var size = 4.5f * ScaleFactor;
        _chevronPaint.Color = ColorScheme.Outline.WithAlpha(150);
        _chevronPaint.StrokeWidth = Math.Max(1.35f, 1.45f * ScaleFactor);
        canvas.DrawLine(x - size * 0.35f, centerY - size, x + size * 0.45f, centerY, _chevronPaint);
        canvas.DrawLine(x + size * 0.45f, centerY, x - size * 0.35f, centerY + size, _chevronPaint);
    }
}
