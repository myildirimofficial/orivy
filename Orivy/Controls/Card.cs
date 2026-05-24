using SkiaSharp;
using System;
using System.ComponentModel;

namespace Orivy.Controls;

public class Card : Container
{
    private readonly SKPaint _titlePaint = new() { IsAntialias = true, Style = SKPaintStyle.Fill };
    private readonly SKPaint _descriptionPaint = new() { IsAntialias = true, Style = SKPaintStyle.Fill };
    private string _title = string.Empty;
    private string _description = string.Empty;
    private bool _useThemeColors = true;
    private float _headerGap = 14f;
    private float _mediaHeight;
    private CardHeaderPlacement _headerPlacement = CardHeaderPlacement.Overlay;

    public Card()
    {
        Radius = new Radius(12);
        Border = new Thickness(1);
        Padding = new Thickness(18);
        BackColor = ColorScheme.Surface;
        ForeColor = ColorScheme.ForeColor;
        BorderColor = ColorScheme.Outline.WithAlpha(ColorScheme.IsDarkMode ? (byte)78 : (byte)92);
        Shadow = new BoxShadow(0f, 1f, 2f, 0, ColorScheme.ShadowColor.WithAlpha(ColorScheme.IsDarkMode ? (byte)0 : (byte)16));
        BackgroundImageLayout = ImageLayout.Zoom;
        ApplyThemeColors();
        ColorScheme.ThemeChanged += HandleThemeChanged;
    }

    [Browsable(false)]
    public Container Content => this;

    public override SKRect DisplayRectangle
    {
        get
        {
            var rect = base.DisplayRectangle;
            var headerHeight = GetHeaderHeight();
            if (headerHeight <= 0f)
                return rect;

            rect.Top = Math.Min(rect.Bottom, GetHeaderTop(rect, headerHeight) + headerHeight);
            return rect;
        }
    }

    [DefaultValue("")]
    public string Title
    {
        get => _title;
        set
        {
            var normalized = value ?? string.Empty;
            if (_title == normalized)
                return;

            _title = normalized;
            InvalidateMeasure();
            Invalidate();
        }
    }

    [DefaultValue("")]
    public string Description
    {
        get => _description;
        set
        {
            var normalized = value ?? string.Empty;
            if (_description == normalized)
                return;

            _description = normalized;
            InvalidateMeasure();
            Invalidate();
        }
    }

    [DefaultValue(true)]
    public bool UseThemeColors
    {
        get => _useThemeColors;
        set
        {
            if (_useThemeColors == value)
                return;

            _useThemeColors = value;
            if (value)
                ApplyThemeColors();
        }
    }

    [DefaultValue(14f)]
    public float HeaderGap
    {
        get => _headerGap;
        set
        {
            var normalized = Math.Max(0f, value);
            if (Math.Abs(_headerGap - normalized) < 0.001f)
                return;

            _headerGap = normalized;
            InvalidateMeasure();
            Invalidate();
        }
    }

    [DefaultValue(0f)]
    public float MediaHeight
    {
        get => _mediaHeight;
        set
        {
            var normalized = Math.Max(0f, value);
            if (Math.Abs(_mediaHeight - normalized) < 0.001f)
                return;

            _mediaHeight = normalized;
            InvalidateMeasure();
            PerformLayout();
            Invalidate();
        }
    }

    [DefaultValue(CardHeaderPlacement.Overlay)]
    public CardHeaderPlacement HeaderPlacement
    {
        get => _headerPlacement;
        set
        {
            if (_headerPlacement == value)
                return;

            _headerPlacement = value;
            InvalidateMeasure();
            PerformLayout();
            Invalidate();
        }
    }

    public void AddContent(ElementBase content)
    {
        ArgumentNullException.ThrowIfNull(content);
        Controls.Add(content);
    }

    public override void OnPaint(SKCanvas canvas)
    {
        RenderHeader(canvas);
        base.OnPaint(canvas);
    }

    protected override SKRect GetBackgroundImageRenderBounds(SKRect elementBounds)
    {
        if (HeaderPlacement != CardHeaderPlacement.BelowImage || BackgroundImageLayout == ImageLayout.Stretch)
            return base.GetBackgroundImageRenderBounds(elementBounds);

        var mediaHeight = GetResolvedMediaHeight();
        if (mediaHeight <= 0f)
            return base.GetBackgroundImageRenderBounds(elementBounds);

        return SKRect.Create(elementBounds.Left, elementBounds.Top, elementBounds.Width, mediaHeight);
    }

    protected override SKSize GetPreferredSizeCore(SKSize proposedSize)
    {
        var size = base.GetPreferredSizeCore(proposedSize);
        var headerHeight = GetHeaderHeight();
        if (headerHeight <= 0f)
            return size;

        return new SKSize(size.Width, MathF.Ceiling(size.Height + headerHeight));
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            ColorScheme.ThemeChanged -= HandleThemeChanged;
            _titlePaint.Dispose();
            _descriptionPaint.Dispose();
        }

        base.Dispose(disposing);
    }

    private void HandleThemeChanged(object? sender, EventArgs e)
    {
        if (UseThemeColors)
            ApplyThemeColors();
    }

    private void ApplyThemeColors()
    {
        BackColor = ColorScheme.Surface;
        ForeColor = ColorScheme.ForeColor;
        BorderColor = ColorScheme.Outline.WithAlpha(ColorScheme.IsDarkMode ? (byte)78 : (byte)92);
        Shadow = new BoxShadow(
            0,
            ColorScheme.IsDarkMode ? 0.5f : 1f,
            ColorScheme.IsDarkMode ? 0f : 2f,
            0,
            ColorScheme.ShadowColor.WithAlpha(ColorScheme.IsDarkMode ? (byte)0 : (byte)16));
        Invalidate();
    }

    private float GetHeaderHeight()
    {
        var hasTitle = !string.IsNullOrWhiteSpace(Title);
        var hasDescription = !string.IsNullOrWhiteSpace(Description);
        if (!hasTitle && !hasDescription)
            return 0f;

        using var titleFont = CreateHeaderFont(true);
        using var descriptionFont = CreateHeaderFont(false);
        var height = 0f;

        if (hasTitle)
            height += GetFontLineHeight(titleFont);

        if (hasDescription)
        {
            if (height > 0f)
                height += 4f * ScaleFactor;
            height += GetFontLineHeight(descriptionFont);
        }

        return MathF.Ceiling(height + HeaderGap * ScaleFactor);
    }

    private float GetResolvedMediaHeight()
    {
        var scaledMediaHeight = MediaHeight > 0f
            ? MediaHeight * ScaleFactor
            : Height * 0.54f;

        var minHeight = Math.Min(64f * ScaleFactor, Math.Max(0f, Height - Padding.Top - Padding.Bottom));
        var maxHeight = Math.Max(minHeight, Height - Padding.Bottom - GetHeaderHeight());
        return Math.Clamp(scaledMediaHeight, minHeight, maxHeight);
    }

    private float GetHeaderTop(SKRect displayRect, float headerHeight)
    {
        if (HeaderPlacement != CardHeaderPlacement.BelowImage || BackgroundImageLayout == ImageLayout.Stretch)
            return displayRect.Top;

        var mediaBottom = GetResolvedMediaHeight();
        var top = mediaBottom + HeaderGap * ScaleFactor;
        var maxTop = Math.Max(displayRect.Top, displayRect.Bottom - headerHeight);
        return Math.Clamp(top, displayRect.Top, maxTop);
    }

    private void RenderHeader(SKCanvas canvas)
    {
        var hasTitle = !string.IsNullOrWhiteSpace(Title);
        var hasDescription = !string.IsNullOrWhiteSpace(Description);
        if (!hasTitle && !hasDescription)
            return;

        var rect = base.DisplayRectangle;
        if (rect.Width <= 0f || rect.Height <= 0f)
            return;

        using var titleFont = CreateHeaderFont(true);
        using var descriptionFont = CreateHeaderFont(false);

        _titlePaint.Color = ForeColor;
        _descriptionPaint.Color = ForeColor.WithAlpha(ColorScheme.IsDarkMode ? (byte)170 : (byte)145);

        var headerHeight = GetHeaderHeight();
        var y = GetHeaderTop(rect, headerHeight);
        if (hasTitle)
        {
            y += -titleFont.Metrics.Ascent;
            canvas.DrawText(Title, rect.Left, y, SKTextAlign.Left, titleFont, _titlePaint);
            y += titleFont.Metrics.Descent + 4f * ScaleFactor;
        }

        if (hasDescription)
        {
            y += -descriptionFont.Metrics.Ascent;
            canvas.DrawText(Description, rect.Left, y, SKTextAlign.Left, descriptionFont, _descriptionPaint);
        }
    }

    private SKFont CreateHeaderFont(bool title)
    {
        var font = CreateRenderFont(Font);
        if (title)
        {
            font.Embolden = true;
            font.Size += 1.5f * ScaleFactor;
        }
        else
        {
            font.Size = Math.Max(10f * ScaleFactor, font.Size * 0.9f);
        }

        return font;
    }

    private static float GetFontLineHeight(SKFont font)
    {
        var metrics = font.Metrics;
        return MathF.Ceiling(metrics.Descent - metrics.Ascent + metrics.Leading);
    }
}
