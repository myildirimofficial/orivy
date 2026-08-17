using Orivy.Animation;
using Orivy.Controls;
using Orivy.Studio.Toolbox;
using SkiaSharp;
using System;

namespace Orivy.Studio.Panels;

/// <summary>
/// A flat, icon-only toolbar action (or toggle, via <see cref="Button.CheckOnClick"/>) — the
/// Xcode/Interface-Builder style of chrome: no filled pill, no text label, just a glyph that tints
/// on hover/press and picks up the accent color while checked. Wraps <see cref="Button"/> so focus,
/// keyboard activation and click/checked plumbing are inherited; only the skin and paint differ.
/// </summary>
public sealed class ToolbarButton : Button
{
    private readonly SKPaint _iconPaint = new() { IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeCap = SKStrokeCap.Round, StrokeJoin = SKStrokeJoin.Round };
    private string _icon;

    /// <summary>The glyph name (see <see cref="ToolbarIcons"/>). Settable so a button can swap its
    /// icon in place, e.g. Preview toggling between "play" and "stop".</summary>
    public string Icon
    {
        get => _icon;
        set
        {
            if (_icon == value)
                return;
            _icon = value;
            Invalidate();
        }
    }

    public ToolbarButton(string icon, string tooltip, float size = 32f)
    {
        _icon = icon;

        Dock = DockStyle.Left;
        AutoSize = false;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;
        Size = new SKSize(size, size);
        MinimumSize = new SKSize(size, size);
        Padding = new Thickness(0);
        Margin = new Thickness(0, 0, 2, 0);
        Text = string.Empty;
        SetToolTip(tooltip);

        // Wipe Button's bold filled default and replace with a flat icon-toolbar skin.
        ConfigureVisualStyles(styles => styles
            .DefaultTransition(TimeSpan.FromMilliseconds(110), AnimationType.CubicEaseOut)
            .Base(b => b
                .Background(SKColors.Transparent)
                .Border(0)
                .Radius(7)
                .Shadow(BoxShadow.None))
            .OnHover(r => r.Background(ColorScheme.ForeColor.WithAlpha(22)))
            .OnPressed(r => r.Background(ColorScheme.ForeColor.WithAlpha(38)).Scale(0.92f))
            .OnChecked(r => r.Background(ColorScheme.Primary.WithAlpha(34)))
            .OnFocused(r => r.BorderColor(ColorScheme.Primary.WithAlpha(160)).Border(1))
            .OnDisabled(r => r.Opacity(0.32f)),
            clearExisting: true);
    }

    protected override bool ShouldRenderDefaultText => false;

    public override SKSize GetPreferredSize(SKSize proposedSize) => Size;

    public override void OnPaint(SKCanvas canvas)
    {
        base.OnPaint(canvas);

        var glyphColor = Checked ? ColorScheme.Primary : ColorScheme.ForeColor.WithAlpha(215);
        _iconPaint.Color = glyphColor;
        _iconPaint.StrokeWidth = 1.6f;

        var content = DisplayRectangle;
        var inset = SKRect.Inflate(content, -6f, -6f);
        ToolbarIcons.Draw(canvas, _icon, inset, _iconPaint);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
            _iconPaint.Dispose();
        base.Dispose(disposing);
    }
}

/// <summary>A small, non-interactive icon glyph — used ahead of panel section titles.</summary>
public sealed class IconGlyph : Element
{
    private readonly SKPaint _paint = new() { IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeCap = SKStrokeCap.Round, StrokeJoin = SKStrokeJoin.Round, StrokeWidth = 1.6f };
    private readonly string _icon;

    public IconGlyph(string icon, float size = 16f)
    {
        _icon = icon;
        Dock = DockStyle.Left;
        Width = (int)size;
        Border = new Thickness(0);
        Radius = new Radius(0);
        Margin = new Thickness(0, 0, 6, 0);
    }

    public override void OnPaint(SKCanvas canvas)
    {
        base.OnPaint(canvas);
        _paint.Color = ColorScheme.ForeColor.WithAlpha(170);
        ToolbarIcons.Draw(canvas, _icon, DisplayRectangle, _paint);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
            _paint.Dispose();
        base.Dispose(disposing);
    }
}
