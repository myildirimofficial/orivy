using Orivy;
using Orivy.Controls;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.IO;

namespace Orivy.Example;

internal partial class MainWindow
{
    private const int BackgroundAssetMaxWidth = 1600;

    private const int BackgroundAssetMaxHeight = 900;

    private readonly List<BackgroundImageFrame> _backgroundSlides = new();

    private Container? _backgroundPanel;

    private Container? _backgroundHero;

    private Element? _backgroundHeroCaption;

    private Container? _backgroundBackdropDeck;

    private Element? _backgroundStatusCard;

    private Button? _backgroundPlayPauseButton;

    private int _backgroundTransitionDurationPreset = 420;

    private int _backgroundIntervalPreset = 2600;

    private void InitializeBackgroundImageShowcase()
    {
        if (TabView == null)
            return;

        _backgroundPanel = new Container
        {
            Name = "backgroundPanel",
            Text = "Backgrounds",
            Dock = DockStyle.Fill,
            Padding = new Thickness(28),
            Radius = new Radius(0),
            Border = new Thickness(0),
        };

        var tabIcon = CreateExampleIcon(new SKColor(0x06, 0xB6, 0xD4), ExampleIconKind.Pulse);
        _backgroundPanel.Image = tabIcon;

        var header = new Element
        {
            Dock = DockStyle.Top,
            Height = 92,
            Margin = new Thickness(0, 0, 0, 18),
            Padding = new Thickness(18),
            BackColor = SKColors.Transparent,
            TextAlign = ContentAlignment.MiddleLeft,
            Text = "Background Image Showcase\nAsset-backed imagery, caption metadata, transition duration, slideshow interval, repeat behavior and backdrop materials are all driven directly by ElementBase.",
        };

        _backgroundHero = new Container
        {
            Name = "backgroundHero",
            Dock = DockStyle.Top,
            Height = 292,
            BackColor = SKColors.Transparent,
            Margin = new Thickness(0, 0, 0, 18),
            Padding = new Thickness(18),
            Radius = new Radius(22),
            Border = new Thickness(0),
            BorderColor = ColorScheme.BackColor.WithAlpha(25),
            BackgroundImageLayout = ImageLayout.Zoom,
            BackgroundImageTransitionEffect = BackgroundImageTransitionEffect.ScaleFade,
            BackgroundImageTransitionDurationMs = _backgroundTransitionDurationPreset,
            BackgroundImageSlideshowEnabled = true,
            BackgroundImageSlideshowRepeat = true,
            BackgroundImageSlideshowIntervalMs = _backgroundIntervalPreset,
        };

        _backgroundHero.ConfigureVisualStyles(s => s.Base(b => b
            .Background(ColorScheme.SurfaceContainerHigh)
            .BorderColor(ColorScheme.Outline.WithAlpha(110))
            .Radius(22)
            .Shadow(new BoxShadow(0f, 14f, 28f, 0, ColorScheme.ShadowColor.WithAlpha(30)))));

        _backgroundHeroCaption = new Element
        {
            Name = "backgroundHeroCaption",
            Dock = DockStyle.Bottom,
            Height = 108,
            Padding = new Thickness(16),
            Radius = new Radius(18),
            Border = new Thickness(1),
            BackColor = SKColors.Black.WithAlpha(118),
            BorderColor = SKColors.White.WithAlpha(42),
            ForeColor = SKColors.White,
            TextAlign = ContentAlignment.MiddleLeft,
        };

        _backgroundHero.Controls.Add(_backgroundHeroCaption);

        _backgroundBackdropDeck = CreateBackdropDemoDeck();
        _backgroundHero.Controls.Add(_backgroundBackdropDeck);

        var actionRow = new Container
        {
            Dock = DockStyle.Top,
            Height = 46,
            Margin = new Thickness(0, 0, 0, 14),
            Radius = new Radius(0),
            Border = new Thickness(0),
            BackColor = SKColors.Transparent,
        };

        var previousButton = new Button
        {
            Text = "Previous",
            Dock = DockStyle.Left,
            Width = 128,
            Height = 38,
            Margin = new Thickness(0, 0, 10, 0),
        };
        previousButton.Click += BackgroundPreviousButton_Click;

        _backgroundPlayPauseButton = new Button
        {
            Text = "Pause Slideshow",
            Dock = DockStyle.Left,
            Width = 168,
            Height = 38,
            Margin = new Thickness(0, 0, 10, 0),
        };
        _backgroundPlayPauseButton.Click += BackgroundPlayPauseButton_Click;

        var nextButton = new Button
        {
            Text = "Next",
            Dock = DockStyle.Left,
            Width = 112,
            Height = 38,
        };
        nextButton.Click += BackgroundNextButton_Click;

        actionRow.Controls.Add(previousButton);
        actionRow.Controls.Add(_backgroundPlayPauseButton);
        actionRow.Controls.Add(nextButton);

        _backgroundStatusCard = new Element
        {
            Name = "backgroundStatusCard",
            Dock = DockStyle.Top,
            Height = 108,
            Padding = new Thickness(18),
            Radius = new Radius(18),
            Border = new Thickness(1),
            TextAlign = ContentAlignment.MiddleLeft,
        };
        _backgroundStatusCard.ConfigureVisualStyles(s => s.Base(b => b
            .Background(ColorScheme.SurfaceContainer)
            .Foreground(ColorScheme.ForeColor)
            .BorderColor(ColorScheme.Outline.WithAlpha(94))
            .Radius(18)));

        _backgroundPanel.Controls.Add(_backgroundStatusCard);
        _backgroundPanel.Controls.Add(actionRow);
        _backgroundPanel.Controls.Add(_backgroundHero);
        _backgroundPanel.Controls.Add(header);

        var slides = LoadBackgroundShowcaseSlides();
        for (var i = 0; i < slides.Count; i++)
            _backgroundSlides.Add(slides[i]);

        if (_backgroundSlides.Count > 0)
            _backgroundHero.BackgroundImages = _backgroundSlides.ToArray();

        _backgroundHero.BackgroundImageChanged += BackgroundHero_BackgroundImageChanged;
        _backgroundHero.BackgroundImageCaptionChanged += BackgroundHero_BackgroundImageCaptionChanged;

        tabView.Controls.Add(_backgroundPanel);
        SyncWindowBackgroundWithShowcase();
        UpdateBackgroundShowcaseStatus();
    }

    private List<BackgroundImageFrame> LoadBackgroundShowcaseSlides()
    {
        var assetDirectory = Path.Combine(AppContext.BaseDirectory, "assets", "images");
        if (Directory.Exists(assetDirectory))
        {
            var candidateFiles = Directory.GetFiles(assetDirectory, "*.*", SearchOption.TopDirectoryOnly);
            Array.Sort(candidateFiles, StringComparer.OrdinalIgnoreCase);

            var assetSlides = new List<BackgroundImageFrame>(candidateFiles.Length);
            for (var i = 0; i < candidateFiles.Length; i++)
            {
                if (!IsSupportedBackgroundImageAsset(candidateFiles[i]))
                    continue;

                var image = LoadBackgroundShowcaseAssetImage(candidateFiles[i]);
                if (image == null)
                    continue;

                assetSlides.Add(CreateBackgroundShowcaseFrame(image, candidateFiles[i], assetSlides.Count));
            }

            if (assetSlides.Count > 0)
                return assetSlides;
        }

        return new List<BackgroundImageFrame>();
    }

    private static bool IsSupportedBackgroundImageAsset(string path)
    {
        var extension = Path.GetExtension(path);
        return string.Equals(extension, ".jpg", StringComparison.OrdinalIgnoreCase)
            || string.Equals(extension, ".jpeg", StringComparison.OrdinalIgnoreCase)
            || string.Equals(extension, ".png", StringComparison.OrdinalIgnoreCase)
            || string.Equals(extension, ".webp", StringComparison.OrdinalIgnoreCase);
    }

    private static SKImage? LoadBackgroundShowcaseAssetImage(string path)
    {
        var sourceImage = SKImage.FromEncodedData(path);
        if (sourceImage == null)
            return null;

        var targetSize = GetBackgroundAssetTargetSize(sourceImage.Width, sourceImage.Height);
        if (targetSize.Width >= sourceImage.Width && targetSize.Height >= sourceImage.Height)
            return sourceImage;

        using (sourceImage)
        using (var surface = SKSurface.Create(new SKImageInfo(targetSize.Width, targetSize.Height, SKColorType.Bgra8888, SKAlphaType.Premul)))
        {
            if (surface == null)
                return null;

            surface.Canvas.Clear(SKColors.Transparent);
            surface.Canvas.DrawImage(sourceImage, SKRect.Create(targetSize.Width, targetSize.Height));
            return surface.Snapshot();
        }
    }

    private static SKSizeI GetBackgroundAssetTargetSize(int sourceWidth, int sourceHeight)
    {
        if (sourceWidth <= 0 || sourceHeight <= 0)
            return new SKSizeI(1, 1);

        var scale = Math.Min(
            1f,
            Math.Min(
                BackgroundAssetMaxWidth / (float)sourceWidth,
                BackgroundAssetMaxHeight / (float)sourceHeight));

        var width = Math.Max(1, (int)Math.Round(sourceWidth * scale));
        var height = Math.Max(1, (int)Math.Round(sourceHeight * scale));
        return new SKSizeI(width, height);
    }

    private static BackgroundImageFrame CreateBackgroundShowcaseFrame(SKImage image, string imagePath, int index)
    {
        var fileName = Path.GetFileNameWithoutExtension(imagePath);
        return fileName.ToLowerInvariant() switch
        {
            "bg1" => new BackgroundImageFrame(
                image,
                new BackgroundImageCaption(
                    "Gallery Entrance",
                    "Warm highlights and layered foreground depth establish the opening scene for the asset-backed slideshow.\n\nThe caption is now defined directly in code, so sample content stays deterministic."),
                ContentAlignment.MiddleLeft,
                BackgroundImageCaptionDesignMode.Overlay),
            "bg2" => new BackgroundImageFrame(
                image,
                new BackgroundImageCaption(
                    "Material Study",
                    "A denser frame helps verify caption overlays and transition pacing against a real-world composition.\n\nGlass mode keeps the panel readable while still preserving the image beneath it."),
                ContentAlignment.MiddleCenter,
                BackgroundImageCaptionDesignMode.Glass),
            "bg3" => new BackgroundImageFrame(
                image,
                new BackgroundImageCaption(
                    "Studio Corridor",
                    "Long horizontal structure makes slide motion and zoom cropping easier to judge.\n\nSolid mode gives stronger separation for brighter photography and busier image regions."),
                ContentAlignment.MiddleRight,
                BackgroundImageCaptionDesignMode.Solid),
            "bg4" => new BackgroundImageFrame(
                image,
                new BackgroundImageCaption(
                    "Ambient Lounge",
                    "Soft contrast is useful when checking readability of caption and summary text over photos.\n\nMinimal mode keeps the content light and unobtrusive while preserving more of the scene."),
                ContentAlignment.BottomLeft,
                BackgroundImageCaptionDesignMode.Minimal),
            "bg5" => new BackgroundImageFrame(
                image,
                new BackgroundImageCaption(
                    "Night Passage",
                    "The final frame gives repeat mode a clear end-state before the slideshow loops back to the start.\n\nHidden mode suppresses the caption panel entirely so the image can stand on its own."),
                ContentAlignment.BottomRight,
                BackgroundImageCaptionDesignMode.Hidden),
            _ => new BackgroundImageFrame(
                image,
                CreateDefaultBackgroundCaption(imagePath, index),
                GetDefaultBackgroundCaptionLayout(index),
                GetDefaultBackgroundCaptionDesignMode(index))
        };
    }

    private static BackgroundImageCaptionDesignMode GetDefaultBackgroundCaptionDesignMode(int index)
    {
        return (index % 4) switch
        {
            1 => BackgroundImageCaptionDesignMode.Glass,
            2 => BackgroundImageCaptionDesignMode.Solid,
            3 => BackgroundImageCaptionDesignMode.Minimal,
            _ => BackgroundImageCaptionDesignMode.Overlay,
        };
    }

    private static ContentAlignment GetDefaultBackgroundCaptionLayout(int index)
    {
        return (index % 4) switch
        {
            1 => ContentAlignment.MiddleCenter,
            2 => ContentAlignment.MiddleRight,
            3 => ContentAlignment.BottomLeft,
            _ => ContentAlignment.MiddleLeft,
        };
    }

    private static BackgroundImageCaption CreateDefaultBackgroundCaption(string imagePath, int index)
    {
        return new BackgroundImageCaption($"Scene {index + 1}", $"Loaded from assets/images/{Path.GetFileName(imagePath)}.");
    }

    private void BackgroundHero_BackgroundImageChanged(object? sender, EventArgs e)
    {
        SyncActiveBackgroundFrameFromHero();
        SyncWindowBackgroundWithShowcase();
        UpdateBackgroundShowcaseStatus();
    }

    private void BackgroundHero_BackgroundImageCaptionChanged(object? sender, EventArgs e)
    {
        SyncActiveBackgroundFrameFromHero();
        SyncWindowBackgroundWithShowcase();
        UpdateBackgroundShowcaseStatus();
    }

    private void SyncActiveBackgroundFrameFromHero()
    {
        if (_backgroundHero == null || _backgroundSlides.Count == 0)
            return;

        var activeFrame = _backgroundHero.CurrentBackgroundImageFrame;
        if (activeFrame == null)
            return;

        var index = Math.Clamp(_backgroundHero.BackgroundImageIndex, 0, _backgroundSlides.Count - 1);
        _backgroundSlides[index] = activeFrame;
    }

    private void ApplyBackgroundCaptionVisuals(BackgroundImageFrame activeFrame)
    {
        if (_backgroundHeroCaption == null)
            return;

        var hasCaption = !activeFrame.Caption.IsEmpty;
        var designMode = activeFrame.CaptionDesignMode;
        _backgroundHeroCaption.Visible = hasCaption && designMode != BackgroundImageCaptionDesignMode.Hidden;

        if (!hasCaption || designMode == BackgroundImageCaptionDesignMode.Hidden)
            return;

        _backgroundHeroCaption.TextAlign = activeFrame.CaptionLayout;
        _backgroundHeroCaption.Height = designMode == BackgroundImageCaptionDesignMode.Minimal ? 82 : 108;
        _backgroundHeroCaption.Padding = designMode == BackgroundImageCaptionDesignMode.Minimal ? new Thickness(4) : new Thickness(16);
        _backgroundHeroCaption.Radius = designMode == BackgroundImageCaptionDesignMode.Minimal ? new Radius(0) : new Radius(18);

        switch (designMode)
        {
            case BackgroundImageCaptionDesignMode.Glass:
                _backgroundHeroCaption.BackColor = ColorScheme.Surface.WithAlpha(ColorScheme.IsDarkMode ? (byte)154 : (byte)194);
                _backgroundHeroCaption.Border = new Thickness(1);
                _backgroundHeroCaption.BorderColor = SKColors.White.WithAlpha(ColorScheme.IsDarkMode ? (byte)72 : (byte)112);
                _backgroundHeroCaption.ForeColor = ColorScheme.ForeColor;
                _backgroundHeroCaption.Shadow = new BoxShadow(0f, 10f, 24f, 0, ColorScheme.ShadowColor.WithAlpha(24));
                break;

            case BackgroundImageCaptionDesignMode.Solid:
                _backgroundHeroCaption.BackColor = ColorScheme.SurfaceContainerHigh.WithAlpha(236);
                _backgroundHeroCaption.Border = new Thickness(1);
                _backgroundHeroCaption.BorderColor = ColorScheme.Outline.WithAlpha(116);
                _backgroundHeroCaption.ForeColor = ColorScheme.ForeColor;
                _backgroundHeroCaption.Shadow = new BoxShadow(0f, 8f, 18f, 0, ColorScheme.ShadowColor.WithAlpha(22));
                break;

            case BackgroundImageCaptionDesignMode.Minimal:
                _backgroundHeroCaption.BackColor = SKColors.Transparent;
                _backgroundHeroCaption.Border = new Thickness(0);
                _backgroundHeroCaption.BorderColor = SKColors.Transparent;
                _backgroundHeroCaption.ForeColor = SKColors.White.WithAlpha(236);
                _backgroundHeroCaption.Shadow = BoxShadow.None;
                break;

            default:
                _backgroundHeroCaption.BackColor = SKColors.Black.WithAlpha(118);
                _backgroundHeroCaption.Border = new Thickness(1);
                _backgroundHeroCaption.BorderColor = SKColors.White.WithAlpha(42);
                _backgroundHeroCaption.ForeColor = SKColors.White;
                _backgroundHeroCaption.Shadow = BoxShadow.None;
                break;
        }
    }

    private void BackgroundPreviousButton_Click(object? sender, EventArgs e)
    {
        MoveBackgroundSlide(-1);
    }

    private void BackgroundNextButton_Click(object? sender, EventArgs e)
    {
        MoveBackgroundSlide(1);
    }

    private void BackgroundPlayPauseButton_Click(object? sender, EventArgs e)
    {
        if (_backgroundHero == null)
            return;

        SetBackgroundSlideshowEnabled(!_backgroundHero.BackgroundImageSlideshowEnabled);
    }

    private void MoveBackgroundSlide(int delta)
    {
        if (_backgroundHero == null || _backgroundSlides.Count == 0)
            return;

        var nextIndex = _backgroundHero.BackgroundImageIndex + delta;
        if (_backgroundHero.BackgroundImageSlideshowRepeat)
        {
            nextIndex = (nextIndex % _backgroundSlides.Count + _backgroundSlides.Count) % _backgroundSlides.Count;
        }
        else
        {
            nextIndex = Math.Clamp(nextIndex, 0, _backgroundSlides.Count - 1);
        }

        _backgroundHero.BackgroundImageIndex = nextIndex;
        UpdateBackgroundShowcaseStatus();
    }

    private void UpdateBackgroundShowcaseStatus()
    {
        if (_backgroundHero == null || _backgroundHeroCaption == null || _backgroundStatusCard == null)
            return;

        if (_backgroundSlides.Count == 0)
        {
            _backgroundHeroCaption.Visible = true;
            _backgroundHeroCaption.Text = "No background assets were loaded from assets/images.";
            _backgroundHeroCaption.TextAlign = ContentAlignment.MiddleCenter;
            _backgroundHeroCaption.BackColor = SKColors.Black.WithAlpha(118);
            _backgroundHeroCaption.Border = new Thickness(1);
            _backgroundHeroCaption.BorderColor = SKColors.White.WithAlpha(42);
            _backgroundHeroCaption.ForeColor = SKColors.White;
            _backgroundHeroCaption.Padding = new Thickness(16);
            _backgroundHeroCaption.Radius = new Radius(18);
            _backgroundHeroCaption.Shadow = BoxShadow.None;
            _backgroundStatusCard.Text =
                "Background Slideshow\nNo background assets are available. Add images under assets/images to populate the showcase and window background mirror. The backdrop deck above still demonstrates Tint, Glass, Acrylic and Mica element surfaces.";

            if (_backgroundPlayPauseButton != null)
                _backgroundPlayPauseButton.Text = "No Assets";

            RefreshBackgroundMenuChecks();
            return;
        }

        var index = Math.Clamp(_backgroundHero.BackgroundImageIndex, 0, _backgroundSlides.Count - 1);
        var activeFrame = _backgroundHero.CurrentBackgroundImageFrame ?? _backgroundSlides[index];
        var caption = activeFrame.Caption;

        _backgroundHeroCaption.Text = caption.ToString();
        ApplyBackgroundCaptionVisuals(activeFrame);
        _backgroundStatusCard.Text =
            $"Background Slideshow\nScene {index + 1}/{_backgroundSlides.Count} - Layout: {_backgroundHero.BackgroundImageLayout} - Effect: {_backgroundHero.BackgroundImageTransitionEffect} - Duration: {_backgroundTransitionDurationPreset} ms\nCaption: {activeFrame.CaptionDesignMode} - Align: {activeFrame.CaptionLayout} - Slideshow: {(_backgroundHero.BackgroundImageSlideshowEnabled ? "Active" : "Passive")} - Repeat: {(_backgroundHero.BackgroundImageSlideshowRepeat ? "Active" : "Passive")} - Interval: {_backgroundIntervalPreset} ms - Window Background: {(_windowBackgroundEnabled ? "Active" : "Passive")} ({_windowBackgroundMode})\nWindow Blur: {_windowBackgroundBlurAmountPreset} px - Mode: {BackgroundImageBlurMode} - Backdrop Deck: Tint / Glass / Acrylic / Mica\nUse the Backgrounds menu to switch layout, effect, duration, caption design and slideshow mode in real time. Use the Window Background menu to mirror the active scene across the window and test blur modes on the root window surface.";

        if (_backgroundPlayPauseButton != null)
            _backgroundPlayPauseButton.Text = _backgroundHero.BackgroundImageSlideshowEnabled ? "Pause Slideshow" : "Start Slideshow";

        RefreshBackgroundMenuChecks();
    }

    private static Container CreateBackdropDemoDeck()
    {
        var deck = new Container
        {
            Name = "backgroundBackdropDeck",
            Dock = DockStyle.Top,
            Height = 126,
            Margin = new Thickness(0, 0, 0, 14),
            Radius = new Radius(0),
            Border = new Thickness(0),
            BackColor = SKColors.Transparent,
        };

        deck.Controls.Add(CreateBackdropDemoCard("Mica", "Layered wash for quiet surfaces.", ElementBackdropMode.Mica, new SKColor(228, 232, 240, 164), 166, false));
        deck.Controls.Add(CreateBackdropDemoCard("Acrylic", "Denser material with stronger body.", ElementBackdropMode.Acrylic, new SKColor(214, 222, 236, 150), 166));
        deck.Controls.Add(CreateBackdropDemoCard("Glass", "Light glass treatment over imagery.", ElementBackdropMode.Glass, new SKColor(245, 247, 250, 110), 166));
        deck.Controls.Add(CreateBackdropDemoCard("Tint", "Simple colored wash over the host.", ElementBackdropMode.Tint, ColorScheme.Primary.WithAlpha(110), 166));
        return deck;
    }

    private static Element CreateBackdropDemoCard(string title, string body, ElementBackdropMode mode, SKColor backdropColor, int width, bool withTrailingMargin = true)
    {
        return new Element
        {
            Text = $"{title}\n{body}",
            Dock = DockStyle.Left,
            Width = width,
            Margin = withTrailingMargin ? new Thickness(0, 0, 12, 0) : new Thickness(0),
            Padding = new Thickness(14),
            Radius = new Radius(18),
            Border = new Thickness(0),
            BackColor = SKColors.Transparent,
            ForeColor = SKColors.White,
            TextAlign = ContentAlignment.MiddleLeft,
            BackdropMode = mode,
            BackdropColor = backdropColor,
            Shadow = new BoxShadow(0f, 10f, 20f, 0, ColorScheme.ShadowColor.WithAlpha(24)),
        };
    }
}
