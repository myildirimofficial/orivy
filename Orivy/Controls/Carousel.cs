using Orivy.Animation;
using Orivy.Helpers;
using SkiaSharp;
using System;
using System.ComponentModel;

namespace Orivy.Controls;

/// <summary>
/// Host surface for <see cref="ElementBase.BackgroundImages"/> with styled previous/next navigation chrome.
/// </summary>
public class Carousel : ElementBase
{
    private const float DefaultNavButtonSize = 40f;
    private const float DefaultNavInset = 18f;

    // Floating light control — readable on most photography.
    private static readonly SKColor PlateLight = new(248, 249, 251, 235);
    private static readonly SKColor PlateLightHover = new(255, 255, 255, 250);
    private static readonly SKColor PlateLightPressed = new(236, 238, 242, 245);
    private static readonly SKColor PlateDark = new(28, 28, 30, 230);
    private static readonly SKColor PlateDarkHover = new(38, 38, 42, 245);
    private static readonly SKColor PlateDarkPressed = new(20, 20, 22, 245);
    private static readonly SKColor IconOnLight = new(28, 28, 30, 235);
    private static readonly SKColor IconOnDark = new(255, 255, 255, 245);
    private static readonly SKColor RimLight = new(255, 255, 255, 90);
    private static readonly SKColor RimDark = new(0, 0, 0, 28);

    private readonly SKPaint _plateFillPaint = new() { IsAntialias = true, Style = SKPaintStyle.Fill };
    private readonly SKPaint _plateStrokePaint = new() { IsAntialias = true, Style = SKPaintStyle.Stroke };
    private readonly SKPaint _shadowPaint = new() { IsAntialias = true, Style = SKPaintStyle.Fill };
    private readonly SKPaint _iconStrokePaint = new()
    {
        IsAntialias = true,
        Style = SKPaintStyle.Stroke,
        StrokeCap = SKStrokeCap.Round,
        StrokeJoin = SKStrokeJoin.Round,
    };
    private readonly SKPath _iconPath = new();

    private SKMaskFilter? _shadowBlur;
    private float _shadowBlurSigma = -1f;

    private bool _showNavigation = true;
    private bool _navigationVisibleOnHoverOnly;
    private float _navigationButtonSize = DefaultNavButtonSize;
    private float _navigationInset = DefaultNavInset;
    private CarouselNavigationStyle _navigationStyle = CarouselNavigationStyle.Glass;
    private CarouselNavHit _pressedNav = CarouselNavHit.None;
    private CarouselNavHit _hoveredNav = CarouselNavHit.None;

    public Carousel()
    {
        CanSelect = false;
        TabStop = false;
        Size = new SKSize(320, 200);
        Padding = new Thickness(0);
        Border = new Thickness(0);
        Radius = new Radius(16);
        BackgroundImageLayout = ImageLayout.Cover;
        BackgroundImageSlideshowEnabled = true;
        BackgroundImageSlideshowRepeat = true;
        BackgroundImageTransitionEffect = BackgroundImageTransitionEffect.ScaleFade;

        ConfigureVisualStyles(styles => styles
            .DefaultTransition(TimeSpan.FromMilliseconds(160), AnimationType.CubicEaseOut)
            .Base(baseStyle => baseStyle
                .Background(ColorScheme.SurfaceContainerHigh)
                .Border(0)
                .Radius(16)
                .Shadow(BoxShadow.None))
            .OnDisabled(rule => rule
                .Opacity(0.78f)));
    }

    [Category("Behavior")]
    [DefaultValue(true)]
    [Description("Shows previous/next navigation controls over the carousel surface.")]
    public bool ShowNavigation
    {
        get => _showNavigation;
        set
        {
            if (_showNavigation == value)
                return;

            _showNavigation = value;
            Invalidate();
        }
    }

    [Category("Behavior")]
    [DefaultValue(false)]
    [Description("When true, navigation chrome is only drawn while the pointer is over the control.")]
    public bool NavigationVisibleOnHoverOnly
    {
        get => _navigationVisibleOnHoverOnly;
        set
        {
            if (_navigationVisibleOnHoverOnly == value)
                return;

            _navigationVisibleOnHoverOnly = value;
            Invalidate();
        }
    }

    [Category("Appearance")]
    [DefaultValue(CarouselNavigationStyle.Glass)]
    [Description("Visual treatment for previous/next navigation controls.")]
    public CarouselNavigationStyle NavigationStyle
    {
        get => _navigationStyle;
        set
        {
            if (_navigationStyle == value)
                return;

            _navigationStyle = value;
            Invalidate();
        }
    }

    [Category("Appearance")]
    [DefaultValue(DefaultNavButtonSize)]
    public float NavigationButtonSize
    {
        get => _navigationButtonSize;
        set
        {
            var normalized = Math.Clamp(value, 28f, 72f);
            if (Math.Abs(_navigationButtonSize - normalized) < 0.01f)
                return;

            _navigationButtonSize = normalized;
            Invalidate();
        }
    }

    [Category("Appearance")]
    [DefaultValue(DefaultNavInset)]
    public float NavigationInset
    {
        get => _navigationInset;
        set
        {
            var normalized = Math.Max(4f, value);
            if (Math.Abs(_navigationInset - normalized) < 0.01f)
                return;

            _navigationInset = normalized;
            Invalidate();
        }
    }

    protected override bool ShouldRenderDefaultText => false;

    protected override bool TryRenderChildContent(SKCanvas canvas)
    {
        RenderChildren(canvas);

        if (ShouldDrawNavigation())
        {
            DrawNavControl(canvas, GetPreviousNavBounds(), CarouselNavHit.Previous, previous: true);
            DrawNavControl(canvas, GetNextNavBounds(), CarouselNavHit.Next, previous: false);
        }

        return true;
    }

    public override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);

        if (!_showNavigation || !Enabled)
        {
            SetHoveredNav(CarouselNavHit.None);
            return;
        }

        SetHoveredNav(HitTestNav(e.Location));
    }

    public override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        SetHoveredNav(CarouselNavHit.None);
        _pressedNav = CarouselNavHit.None;
    }

    public override void OnMouseDown(MouseEventArgs e)
    {
        if (e.Button == MouseButtons.Left && ShouldDrawNavigation())
        {
            var hit = HitTestNav(e.Location);
            if (hit != CarouselNavHit.None)
            {
                _pressedNav = hit;
                e.Handled = true;
                Invalidate();
                return;
            }
        }

        base.OnMouseDown(e);
    }

    public override void OnMouseUp(MouseEventArgs e)
    {
        if (e.Button == MouseButtons.Left && _pressedNav != CarouselNavHit.None)
        {
            var pressed = _pressedNav;
            _pressedNav = CarouselNavHit.None;
            e.Handled = true;

            if (HitTestNav(e.Location) == pressed)
            {
                if (pressed == CarouselNavHit.Previous)
                    ShowPreviousBackgroundImage();
                else
                    ShowNextBackgroundImage();
            }

            Invalidate();
            return;
        }

        base.OnMouseUp(e);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _plateFillPaint.Dispose();
            _plateStrokePaint.Dispose();
            _shadowPaint.Dispose();
            _iconStrokePaint.Dispose();
            _iconPath.Dispose();
            _shadowBlur?.Dispose();
            _shadowBlur = null;
        }

        base.Dispose(disposing);
    }

    private bool ShouldDrawNavigation()
    {
        if (!_showNavigation || !Enabled)
            return false;

        if (_navigationVisibleOnHoverOnly && !IsPointerOver && _pressedNav == CarouselNavHit.None)
            return false;

        return BackgroundImageCount > 1;
    }

    private void SetHoveredNav(CarouselNavHit hit)
    {
        if (_hoveredNav == hit)
            return;

        _hoveredNav = hit;
        Cursor = hit == CarouselNavHit.None ? Cursors.Default : Cursors.Hand;
        Invalidate();
    }

    private CarouselNavHit HitTestNav(SKPoint location)
    {
        if (!ShouldDrawNavigation())
            return CarouselNavHit.None;

        if (GetPreviousNavBounds().Contains(location))
            return CarouselNavHit.Previous;

        if (GetNextNavBounds().Contains(location))
            return CarouselNavHit.Next;

        return CarouselNavHit.None;
    }

    private SKRect GetPreviousNavBounds()
    {
        var size = GetNavPlateSize();
        var inset = _navigationInset * Math.Max(1f, ScaleFactor);
        var y = (Height - size.Height) * 0.5f;
        return SKRect.Create(inset, y, size.Width, size.Height);
    }

    private SKRect GetNextNavBounds()
    {
        var size = GetNavPlateSize();
        var inset = _navigationInset * Math.Max(1f, ScaleFactor);
        var y = (Height - size.Height) * 0.5f;
        return SKRect.Create(Width - inset - size.Width, y, size.Width, size.Height);
    }

    private SKSize GetNavPlateSize()
    {
        var scale = Math.Max(1f, ScaleFactor);
        var size = _navigationButtonSize * scale;

        return _navigationStyle switch
        {
            CarouselNavigationStyle.Pill => new SKSize(size * 0.70f, size * 1.55f),
            CarouselNavigationStyle.Minimal => new SKSize(size * 0.85f, size * 0.85f),
            CarouselNavigationStyle.Arrow => new SKSize(size, size),
            _ => new SKSize(size, size),
        };
    }

    private void DrawNavControl(SKCanvas canvas, SKRect bounds, CarouselNavHit hit, bool previous)
    {
        var hovered = _hoveredNav == hit;
        var pressed = _pressedNav == hit;

        switch (_navigationStyle)
        {
            case CarouselNavigationStyle.Minimal:
                DrawMinimal(canvas, bounds, previous, hovered, pressed);
                break;
            case CarouselNavigationStyle.Pill:
                DrawPill(canvas, bounds, previous, hovered, pressed);
                break;
            case CarouselNavigationStyle.Solid:
                DrawSolid(canvas, bounds, previous, hovered, pressed);
                break;
            case CarouselNavigationStyle.Outline:
                DrawOutline(canvas, bounds, previous, hovered, pressed);
                break;
            case CarouselNavigationStyle.Arrow:
                DrawArrow(canvas, bounds, previous, hovered, pressed);
                break;
            default:
                DrawGlass(canvas, bounds, previous, hovered, pressed);
                break;
        }
    }

    private void DrawGlass(SKCanvas canvas, SKRect bounds, bool previous, bool hovered, bool pressed)
    {
        DrawElevatedPlate(canvas, bounds, cornerRadius: bounds.Width * 0.5f, circular: true, hovered, pressed, dark: false);
        DrawChevron(canvas, bounds, previous, IconOnLight, hovered, pressed);
    }

    private void DrawSolid(SKCanvas canvas, SKRect bounds, bool previous, bool hovered, bool pressed)
    {
        DrawElevatedPlate(canvas, bounds, cornerRadius: bounds.Width * 0.5f, circular: true, hovered, pressed, dark: true);
        DrawChevron(canvas, bounds, previous, IconOnDark, hovered, pressed);
    }

    private void DrawPill(SKCanvas canvas, SKRect bounds, bool previous, bool hovered, bool pressed)
    {
        var radius = bounds.Width * 0.5f;
        DrawElevatedPlate(canvas, bounds, cornerRadius: radius, circular: false, hovered, pressed, dark: false);
        DrawChevron(canvas, bounds, previous, IconOnLight, hovered, pressed, sizeScale: 0.88f);
    }

    private void DrawArrow(SKCanvas canvas, SKRect bounds, bool previous, bool hovered, bool pressed)
    {
        var radius = bounds.Width * 0.30f;
        DrawElevatedPlate(canvas, bounds, cornerRadius: radius, circular: false, hovered, pressed, dark: false);
        DrawChevron(canvas, bounds, previous, IconOnLight, hovered, pressed);
    }

    private void DrawOutline(SKCanvas canvas, SKRect bounds, bool previous, bool hovered, bool pressed)
    {
        var ring = InflateCentered(bounds, -0.5f * ScaleFactor);

        if (hovered || pressed)
        {
            _plateFillPaint.Color = pressed
                ? new SKColor(255, 255, 255, 48)
                : new SKColor(255, 255, 255, 32);
            canvas.DrawOval(ring, _plateFillPaint);
        }

        // Soft outer contrast so the ring survives bright highlights.
        _plateStrokePaint.Color = new SKColor(0, 0, 0, hovered ? (byte)50 : (byte)34);
        _plateStrokePaint.StrokeWidth = Math.Max(3.2f, 3.2f * ScaleFactor);
        canvas.DrawOval(ring, _plateStrokePaint);

        _plateStrokePaint.Color = pressed
            ? new SKColor(255, 255, 255, 245)
            : hovered
                ? new SKColor(255, 255, 255, 230)
                : new SKColor(255, 255, 255, 200);
        _plateStrokePaint.StrokeWidth = Math.Max(1.5f, 1.5f * ScaleFactor);
        canvas.DrawOval(ring, _plateStrokePaint);

        DrawChevron(canvas, bounds, previous, IconOnDark, hovered, pressed, halo: true);
    }

    private void DrawMinimal(SKCanvas canvas, SKRect bounds, bool previous, bool hovered, bool pressed)
    {
        // Quiet hit target wash on hover only.
        if (hovered || pressed)
        {
            _plateFillPaint.Color = pressed
                ? new SKColor(0, 0, 0, 56)
                : new SKColor(0, 0, 0, 36);
            canvas.DrawOval(bounds, _plateFillPaint);
        }

        DrawChevron(canvas, bounds, previous, IconOnDark, hovered, pressed, halo: true, sizeScale: 1.08f);
    }

    private void DrawElevatedPlate(
        SKCanvas canvas,
        SKRect bounds,
        float cornerRadius,
        bool circular,
        bool hovered,
        bool pressed,
        bool dark)
    {
        var scale = Math.Max(1f, ScaleFactor);
        DrawSoftShadow(canvas, bounds, cornerRadius, circular, hovered, pressed);

        SKColor fill;
        if (dark)
        {
            fill = pressed ? PlateDarkPressed : hovered ? PlateDarkHover : PlateDark;
        }
        else
        {
            fill = pressed ? PlateLightPressed : hovered ? PlateLightHover : PlateLight;
        }

        _plateFillPaint.Color = fill;
        if (circular)
            canvas.DrawOval(bounds, _plateFillPaint);
        else
            canvas.DrawRoundRect(bounds, cornerRadius, cornerRadius, _plateFillPaint);

        // Inner top highlight (light plates only).
        if (!dark && !pressed)
        {
            var sheenHeight = bounds.Height * 0.46f;
            var sheen = new SKRect(bounds.Left + 1.5f * scale, bounds.Top + 1.2f * scale, bounds.Right - 1.5f * scale, bounds.Top + sheenHeight);
            using var sheenShader = SKShader.CreateLinearGradient(
                new SKPoint(sheen.MidX, sheen.Top),
                new SKPoint(sheen.MidX, sheen.Bottom),
                [new SKColor(255, 255, 255, hovered ? (byte)70 : (byte)48), new SKColor(255, 255, 255, 0)],
                [0f, 1f],
                SKShaderTileMode.Clamp);
            _plateFillPaint.Shader = sheenShader;
            if (circular)
                canvas.DrawOval(bounds, _plateFillPaint);
            else
                canvas.DrawRoundRect(bounds, cornerRadius, cornerRadius, _plateFillPaint);
            _plateFillPaint.Shader = null;
        }

        _plateStrokePaint.StrokeWidth = Math.Max(1f, 1f * scale);
        _plateStrokePaint.Color = dark ? RimDark : RimLight;
        if (circular)
            canvas.DrawOval(InflateCentered(bounds, -0.5f * scale), _plateStrokePaint);
        else
            canvas.DrawRoundRect(InflateCentered(bounds, -0.5f * scale), cornerRadius, cornerRadius, _plateStrokePaint);
    }

    private void DrawSoftShadow(SKCanvas canvas, SKRect bounds, float cornerRadius, bool circular, bool hovered, bool pressed)
    {
        var scale = Math.Max(1f, ScaleFactor);
        var sigma = (pressed ? 3.2f : hovered ? 5.5f : 4.2f) * scale;
        EnsureShadowBlur(sigma);

        var offsetY = (pressed ? 1.2f : hovered ? 3.2f : 2.4f) * scale;
        var shadowBounds = bounds;
        shadowBounds.Offset(0f, offsetY);
        shadowBounds.Inflate(pressed ? 0.5f * scale : 1.2f * scale, pressed ? 0.5f * scale : 1.2f * scale);

        _shadowPaint.Color = new SKColor(0, 0, 0, pressed ? (byte)48 : hovered ? (byte)58 : (byte)44);
        _shadowPaint.MaskFilter = _shadowBlur;

        if (circular)
            canvas.DrawOval(shadowBounds, _shadowPaint);
        else
            canvas.DrawRoundRect(shadowBounds, cornerRadius, cornerRadius, _shadowPaint);

        _shadowPaint.MaskFilter = null;
    }

    private void DrawChevron(
        SKCanvas canvas,
        SKRect plate,
        bool previous,
        SKColor color,
        bool hovered,
        bool pressed,
        bool halo = false,
        float sizeScale = 1f)
    {
        var scale = Math.Max(1f, ScaleFactor);
        var size = Math.Min(plate.Width, plate.Height) * sizeScale;
        BuildChevronPath(_iconPath, plate, previous, size);

        var stroke = Math.Max(1.85f, size * 0.055f);
        if (hovered)
            stroke += 0.15f * scale;
        if (pressed)
            stroke -= 0.1f * scale;

        if (halo)
        {
            _iconStrokePaint.Color = new SKColor(0, 0, 0, pressed ? (byte)90 : (byte)70);
            _iconStrokePaint.StrokeWidth = stroke + 2.2f * scale;
            canvas.DrawPath(_iconPath, _iconStrokePaint);

            _iconStrokePaint.Color = new SKColor(255, 255, 255, pressed ? (byte)40 : (byte)28);
            _iconStrokePaint.StrokeWidth = stroke + 1.1f * scale;
            canvas.DrawPath(_iconPath, _iconStrokePaint);
        }

        var icon = color;
        if (pressed)
            icon = icon.WithAlpha((byte)Math.Clamp(icon.Alpha - 20, 160, 255));
        else if (hovered)
            icon = icon.WithAlpha((byte)Math.Min(255, icon.Alpha + 12));

        _iconStrokePaint.Color = icon;
        _iconStrokePaint.StrokeWidth = stroke;
        canvas.DrawPath(_iconPath, _iconStrokePaint);
    }

    /// <summary>
    /// Lucide/Heroicons-style chevron: open angle ~90°, optically centered toward the tip.
    /// </summary>
    private static void BuildChevronPath(SKPath path, SKRect plate, bool previous, float size)
    {
        path.Reset();

        var dir = previous ? -1f : 1f;
        var cx = plate.MidX + dir * size * 0.015f;
        var cy = plate.MidY;

        // Tip sits slightly past center; arms open behind it.
        var tipX = cx + dir * size * 0.07f;
        var backX = tipX - dir * size * 0.155f;
        var rise = size * 0.165f;

        path.MoveTo(backX, cy - rise);
        path.LineTo(tipX, cy);
        path.LineTo(backX, cy + rise);
    }

    private void EnsureShadowBlur(float sigma)
    {
        if (_shadowBlur != null && Math.Abs(_shadowBlurSigma - sigma) < 0.05f)
            return;

        _shadowBlur?.Dispose();
        _shadowBlurSigma = sigma;
        _shadowBlur = SKMaskFilter.CreateBlur(SKBlurStyle.Normal, sigma);
    }

    private static SKRect InflateCentered(SKRect bounds, float amount)
    {
        return SKRect.Create(
            bounds.Left - amount,
            bounds.Top - amount,
            Math.Max(1f, bounds.Width + amount * 2f),
            Math.Max(1f, bounds.Height + amount * 2f));
    }

    private enum CarouselNavHit
    {
        None,
        Previous,
        Next,
    }
}
