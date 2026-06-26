using Orivy.Animation;
using Orivy.Layout;
using SkiaSharp;
using System;
using System.ComponentModel;

namespace Orivy.Controls;

public class Collapse : Container
{
    private readonly AnimationManager _animation;
    private readonly SKPaint _textPaint = new() { IsAntialias = true, Style = SKPaintStyle.Fill };
    private readonly SKPaint _chevronPaint = new() { IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeCap = SKStrokeCap.Round, StrokeJoin = SKStrokeJoin.Round };
    private bool _isExpanded = true;
    private float _progress = 1f;

    public Collapse()
    {
        AutoSize = false;
        Height = 140;
        Padding = new Thickness(14, 46, 14, 0);
        Radius = new Radius(12);
        Border = new Thickness(1);
        BackColor = ColorScheme.Surface;
        BorderColor = ColorScheme.Outline.WithAlpha(90);
        HeaderText = "Collapse";

        _animation = new AnimationManager(true)
        {
            AnimationType = AnimationType.CubicEaseOut,
            InterruptAnimation = true,
            Increment = 16d / 160d,
            SecondaryIncrement = 16d / 140d
        };
        _animation.OnAnimationProgress += HandleAnimationProgress;
        _animation.OnAnimationFinished += HandleAnimationFinished;
    }

    [DefaultValue("Collapse")]
    public string HeaderText { get; set; }

    [DefaultValue(true)]
    public bool IsExpanded
    {
        get => _isExpanded;
        set
        {
            if (_isExpanded == value)
                return;

            _isExpanded = value;
            _animation.StartNewAnimation(value ? AnimationDirection.In : AnimationDirection.Out);
            ExpandedChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    [DefaultValue(42)]
    public int HeaderHeight { get; set; } = 42;

    public event EventHandler? ExpandedChanged;

    public float CurrentExpandedProgress => _progress;

    public float CurrentDisplayHeight => HeaderHeight + Math.Max(0f, GetContentHeight()) * _progress;

    public override SKSize GetPreferredSize(SKSize proposedSize)
    {
        var width = proposedSize.Width > 1 ? proposedSize.Width : Width;
        var contentHeight = GetContentHeight();
        return new SKSize(Math.Max(MinimumSize.Width, width), HeaderHeight + contentHeight * _progress);
    }

    public override void  OnPaint(SKCanvas canvas)
    {
        base.OnPaint(canvas);

        var header = new SKRect(0, 0, Width, Math.Min(HeaderHeight, Height));
        using var font = CreateRenderFont(Font);
        _textPaint.Color = ColorScheme.ForeColor;
        var metrics = font.Metrics;
        var y = header.MidY - (metrics.Ascent + metrics.Descent) / 2f;
        canvas.DrawText(HeaderText, header.Left + 16f * ScaleFactor, y, SKTextAlign.Left, font, _textPaint);
        DrawChevron(canvas, header);
    }

    public override void  OnLayout(LayoutEventArgs e)
    {
        base.OnLayout(e);

        var y = (float)HeaderHeight;
        var width = Math.Max(0f, Width - Padding.Left - Padding.Right);
        for (var i = 0; i < Controls.Count; i++)
        {
            if (Controls[i] is not ElementBase child || !child.Visible)
                continue;

            var height = child.AutoSize ? child.GetPreferredSize(new SKSize(width, short.MaxValue)).Height : child.Height;
            child.Bounds = SKRect.Create(Padding.Left, y, width, height);
            y += height + GetTrailingMargin(i);
        }
    }

    public override void  OnMouseClick(MouseEventArgs e)
    {
        if (e.Button == MouseButtons.Left && e.Y <= HeaderHeight)
        {
            IsExpanded = !IsExpanded;
            e.Handled = true;
            return;
        }

        base.OnMouseClick(e);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _animation.OnAnimationProgress -= HandleAnimationProgress;
            _animation.OnAnimationFinished -= HandleAnimationFinished;
            _animation.Dispose();
            _textPaint.Dispose();
            _chevronPaint.Dispose();
        }

        base.Dispose(disposing);
    }

    private float GetContentHeight()
    {
        var height = 0f;
        var width = Math.Max(0f, DisplayRectangle.Width - Padding.Left - Padding.Right);
        for (var i = 0; i < Controls.Count; i++)
        {
            if (Controls[i] is not ElementBase child || !child.Visible)
                continue;

            height += child.AutoSize ? child.GetPreferredSize(new SKSize(width, short.MaxValue)).Height : child.Height;
            height += GetTrailingMargin(i);
        }

        return height;
    }

    private float GetTrailingMargin(int index)
    {
        for (var i = index + 1; i < Controls.Count; i++)
        {
            if (Controls[i] is ElementBase { Visible: true })
                return Controls[index] is ElementBase child ? child.Margin.Bottom : 0f;
        }

        return 0f;
    }

    private void DrawChevron(SKCanvas canvas, SKRect header)
    {
        var progress = Math.Clamp(_progress, 0f, 1f);
        var size = 5f * ScaleFactor;
        var cx = header.Right - 22f * ScaleFactor;
        var cy = header.MidY;
        _chevronPaint.Color = ColorScheme.ForeColor.WithAlpha(170);
        _chevronPaint.StrokeWidth = Math.Max(1.6f, 1.7f * ScaleFactor);

        var save = canvas.Save();
        canvas.RotateDegrees(progress * 90f, cx, cy);
        canvas.DrawLine(cx - size * 0.45f, cy - size, cx + size * 0.45f, cy, _chevronPaint);
        canvas.DrawLine(cx + size * 0.45f, cy, cx - size * 0.45f, cy + size, _chevronPaint);
        canvas.RestoreToCount(save);
    }

    private void HandleAnimationProgress(object _)
    {
        _progress = Math.Clamp((float)_animation.GetProgress(), 0f, 1f);
        Parent?.PerformLayout();
        PerformLayout();
        Parent?.Invalidate();
        Invalidate();
    }

    private void HandleAnimationFinished(object _)
    {
        _progress = _isExpanded ? 1f : 0f;
        Parent?.PerformLayout();
        PerformLayout();
        Parent?.Invalidate();
        Invalidate();
    }
}
