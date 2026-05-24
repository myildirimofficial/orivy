using Orivy;
using Orivy.Controls;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Orivy.Example;

internal sealed partial class CardDemoPage
{
    private const string MountainUrl = "https://images.unsplash.com/photo-1500530855697-b586d89ba3ee?auto=format&fit=crop&w=920&q=74";
    private const string ForestUrl = "https://images.unsplash.com/photo-1448375240586-882707db888b?auto=format&fit=crop&w=920&q=74";
    private const string CoastUrl = "https://images.unsplash.com/photo-1507525428034-b723cf961d3e?auto=format&fit=crop&w=920&q=74";
    private const string CabinUrl = "https://images.unsplash.com/photo-1470770903676-69b98201ea1c?auto=format&fit=crop&w=920&q=74";
    private const string SnowUrl = "https://images.unsplash.com/photo-1519681393784-d120267933ba?auto=format&fit=crop&w=1080&q=74";
    private const string DesertUrl = "https://images.unsplash.com/photo-1500534314209-a25ddb2bd429?auto=format&fit=crop&w=1080&q=74";
    private readonly Dictionary<string, Task<SKImage>> _remoteImages = new(StringComparer.Ordinal);

    private void InitializeComponent()
    {
        Text = "Cards";
        Padding = new Thickness(20);
        AutoScroll = true;
        AutoScrollMargin = new SKSize(0, 20);

        var gallery = new Grid
        {
            Name = "cardGallery",
            Dock = DockStyle.Top,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            RowCount = 12,
            ColumnCount = 2,
            RowGap = 18,
            ColumnGap = 18,
            BackColor = SKColors.Transparent
        };

        gallery.Add(CreateSideMediaCard(
            "Weekend escapes",
            "Mountain cabins and cold morning light, curated for quiet weekends.",
            "FEATURED",
            "Explore stays",
            MountainUrl,
            imageOnLeft: true,
            new SKColor(59, 130, 246)), 0, 0);

        gallery.Add(CreateSideMediaCard(
            "Forest journal",
            "A slow trail through old pines, mossy paths and softened light.",
            "EDITOR'S PICK",
            "Read story",
            ForestUrl,
            imageOnLeft: false,
            new SKColor(16, 185, 129)), 0, 1);

        gallery.Add(CreateVerticalMediaCard(
            "Coastal mornings",
            "Wake with salt air and clear water just beyond the terrace.",
            "SUMMER COLLECTION",
            "View retreat",
            CoastUrl,
            imageOnTop: true,
            new SKColor(14, 165, 233)), 1, 0);

        gallery.Add(CreateVerticalMediaCard(
            "Above the cloud line",
            "Warm timber rooms surrounded by mist, silence and open sky.",
            "PRIVATE CABINS",
            "Reserve now",
            CabinUrl,
            imageOnTop: false,
            new SKColor(245, 158, 11)), 1, 1);

        gallery.Add(CreateOverlayHeroCard(), 2, 0, 1, 2);
        gallery.Add(CreateOverlayCard(
            "Desert roads",
            "Follow the last light beyond the ridge.",
            "ROAD TRIP",
            "Open route",
            DesertUrl,
            BackgroundImagePosition.Center,
            new SKColor(249, 115, 22)), 3, 0);
        gallery.Add(CreateOverlayCard(
            "Winter stillness",
            "Cabins under stars, reserved for two.",
            "LIMITED DATES",
            "See dates",
            SnowUrl,
            BackgroundImagePosition.BottomCenter,
            new SKColor(99, 102, 241)), 3, 1);
        gallery.Add(CreateSlideshowCard(), 4, 0, 1, 2);
        gallery.Add(CreateSettingsPreviewCard(), 5, 0, 1, 2);
        gallery.Add(CreateBlurSampleCard("Normal blur", BackgroundImageBlurMode.Normal, 7f, ForestUrl, new SKColor(59, 130, 246)), 6, 0);
        gallery.Add(CreateBlurSampleCard("Horizontal blur", BackgroundImageBlurMode.Horizontal, 9f, CoastUrl, new SKColor(14, 165, 233)), 6, 1);
        gallery.Add(CreateBlurSampleCard("Vertical blur", BackgroundImageBlurMode.Vertical, 9f, MountainUrl, new SKColor(168, 85, 247)), 7, 0);
        gallery.Add(CreateBlurSampleCard("Wide blur", BackgroundImageBlurMode.Wide, 8f, DesertUrl, new SKColor(249, 115, 22)), 7, 1);
        gallery.Add(CreateBlurSampleCard("Tall blur", BackgroundImageBlurMode.Tall, 8f, CabinUrl, new SKColor(245, 158, 11)), 8, 0);
        gallery.Add(CreateBlurSampleCard("Cinematic blur", BackgroundImageBlurMode.Cinematic, 6f, SnowUrl, new SKColor(236, 72, 153)), 8, 1);
        gallery.Add(CreateBlurSampleCard("Portrait blur", BackgroundImageBlurMode.Portrait, 7f, MountainUrl, new SKColor(16, 185, 129)), 9, 0);
        gallery.Add(CreateLayoutPositionCard(), 9, 1);
        gallery.Add(CreateTransitionCaptionCard(), 10, 0, 1, 2);
        gallery.Add(CreateManualLoadingCard(), 11, 0, 1, 2);

        Controls.Add(gallery);
    }

    private Card CreateSideMediaCard(
        string title,
        string description,
        string eyebrow,
        string action,
        string imageUrl,
        bool imageOnLeft,
        SKColor accent)
    {
        var card = CreateShell(270, accent);
        var layout = new Grid
        {
            Dock = DockStyle.Fill,
            RowCount = 1,
            ColumnCount = 4,
            ColumnGap = 0,
            BackColor = SKColors.Transparent
        };

        var media = CreateRemoteMedia(imageUrl, imageOnLeft ? BackgroundImagePosition.CenterLeft : BackgroundImagePosition.CenterRight);
        var body = CreateBody(title, description, eyebrow, action, accent);

        if (imageOnLeft)
        {
            layout.Add(media, 0, 0);
            layout.Add(body, 0, 1, 1, 3);
        }
        else
        {
            layout.Add(body, 0, 0, 1, 3);
            layout.Add(media, 0, 3);
        }

        card.AddContent(layout);
        return card;
    }

    private Card CreateVerticalMediaCard(
        string title,
        string description,
        string eyebrow,
        string action,
        string imageUrl,
        bool imageOnTop,
        SKColor accent)
    {
        var card = CreateShell(364, accent);
        var layout = new Grid
        {
            Dock = DockStyle.Fill,
            RowCount = 4,
            ColumnCount = 1,
            RowGap = 0,
            BackColor = SKColors.Transparent
        };

        var media = CreateRemoteMedia(imageUrl, imageOnTop ? BackgroundImagePosition.TopCenter : BackgroundImagePosition.BottomCenter);
        var body = CreateBody(title, description, eyebrow, action, accent);

        if (imageOnTop)
        {
            layout.Add(media, 0, 0);
            layout.Add(body, 1, 0, 3, 1);
        }
        else
        {
            layout.Add(body, 0, 0, 3, 1);
            layout.Add(media, 3, 0);
        }

        card.AddContent(layout);
        return card;
    }

    private Card CreateOverlayHeroCard()
    {
        var accent = new SKColor(34, 197, 94);
        var card = CreateShell(344, accent);
        card.BackgroundImageLayout = ImageLayout.Cover;
        card.BackgroundImagePosition = BackgroundImagePosition.Center;
        StartRemoteBackground(card, ForestUrl);

        var content = CreateOverlayContent(
            "Discover untouched places",
            "Private trails, forest lodges and fresh morning air. Build a weekend around the view.",
            "NEW COLLECTION",
            "Explore collection",
            accent);
        content.Dock = DockStyle.Left;
        content.Width = 510;
        card.AddContent(content);
        return card;
    }

    private Card CreateOverlayCard(
        string title,
        string description,
        string eyebrow,
        string action,
        string imageUrl,
        BackgroundImagePosition position,
        SKColor accent)
    {
        var card = CreateShell(292, accent);
        card.BackgroundImageLayout = ImageLayout.Cover;
        card.BackgroundImagePosition = position;
        StartRemoteBackground(card, imageUrl);
        card.AddContent(CreateOverlayContent(title, description, eyebrow, action, accent));
        return card;
    }

    private Card CreateSlideshowCard()
    {
        var accent = new SKColor(139, 92, 246);
        var card = CreateShell(320, accent);
        var layout = new Grid
        {
            Dock = DockStyle.Fill,
            RowCount = 1,
            ColumnCount = 5,
            ColumnGap = 0,
            BackColor = SKColors.Transparent
        };

        var media = CreateRemoteMedia(SnowUrl, BackgroundImagePosition.Center);
        media.BackgroundImageTransitionEffect = BackgroundImageTransitionEffect.ScaleFade;
        media.BackgroundImageTransitionDurationMs = 460;
        media.BackgroundImageSlideshowIntervalMs = 2300;
        media.BackgroundImageSlideshowRepeat = true;

        var button = CreateActionButton("Start slideshow", accent);
        var started = false;
        button.Click += (_, _) =>
        {
            if (started)
                return;

            started = true;
            button.Enabled = false;
            button.Text = "Loading...";
            StartSlideshow(media, button, [SnowUrl, MountainUrl, ForestUrl, CoastUrl]);
        };

        var body = CreateBody(
            "Changing horizons",
            "A quiet collection of seasonal landscapes, presented with a soft image transition.",
            "PHOTO SERIES",
            string.Empty,
            accent);
        body.Add(button, 4, 0);

        layout.Add(media, 0, 0, 1, 3);
        layout.Add(body, 0, 3, 1, 2);
        card.AddContent(layout);
        return card;
    }

    private Card CreateSettingsPreviewCard()
    {
        var accent = new SKColor(0, 120, 212);
        var card = CreateShell(430, accent);
        card.BackColor = ColorScheme.SurfaceContainer;

        var frame = new Grid
        {
            Dock = DockStyle.Fill,
            Padding = new Thickness(16),
            RowCount = 1,
            ColumnCount = 6,
            ColumnGap = 14,
            BackColor = SKColors.Transparent
        };

        var nav = CreateSettingsNav();
        var preview = CreateSettingsPreviewSurface();
        var controls = CreateSettingsControls(preview);

        frame.Add(nav, 0, 0);
        frame.Add(preview, 0, 1, 1, 3);
        frame.Add(controls, 0, 4, 1, 2);
        card.AddContent(frame);
        return card;
    }

    private Grid CreateSettingsNav()
    {
        var nav = new Grid
        {
            Dock = DockStyle.Fill,
            Padding = new Thickness(12),
            RowCount = 7,
            ColumnCount = 1,
            RowGap = 8,
            Radius = new Radius(12),
            BackColor = ColorScheme.Surface.WithAlpha(ColorScheme.IsDarkMode ? (byte)160 : (byte)220),
            Border = new Thickness(1),
            BorderColor = ColorScheme.Outline.WithAlpha(72)
        };

        nav.Add(CreateText("Settings", ColorScheme.ForeColor, 18f, true), 0, 0);
        nav.Add(CreateNavItem("Personalization", true), 1, 0);
        nav.Add(CreateNavItem("Background", false), 2, 0);
        nav.Add(CreateNavItem("Effects", false), 3, 0);
        nav.Add(CreateNavItem("Captions", false), 4, 0);
        nav.Add(CreateNavItem("Motion", false), 5, 0);
        nav.Add(CreateNavItem("Advanced", false), 6, 0);
        return nav;
    }

    private Element CreateSettingsPreviewSurface()
    {
        var preview = new Element
        {
            Dock = DockStyle.Fill,
            Padding = new Thickness(22),
            Radius = new Radius(16),
            Border = new Thickness(1),
            BorderColor = ColorScheme.Outline.WithAlpha(90),
            BackColor = new SKColor(15, 23, 42),
            ForeColor = SKColors.White,
            BackgroundImageLayout = ImageLayout.Cover,
            BackgroundImagePosition = BackgroundImagePosition.Center,
            BackgroundImageBlurAmount = 0,
            BackgroundImageBlurMode = BackgroundImageBlurMode.Normal,
            BackgroundImageTransitionEffect = BackgroundImageTransitionEffect.ScaleFade,
            BackgroundImageTransitionDurationMs = 420,
            Text = "Live background preview\nCover + Center + ScaleFade\nUse the settings on the right to change layout, blur and transition.",
            TextAlign = ContentAlignment.BottomLeft
        };

        StartRemoteBackground(preview, CoastUrl);
        return preview;
    }

    private Grid CreateSettingsControls(Element preview)
    {
        var controls = new Grid
        {
            Dock = DockStyle.Fill,
            Padding = new Thickness(14),
            RowCount = 9,
            ColumnCount = 2,
            RowGap = 8,
            ColumnGap = 8,
            Radius = new Radius(12),
            BackColor = ColorScheme.Surface.WithAlpha(ColorScheme.IsDarkMode ? (byte)166 : (byte)230),
            Border = new Thickness(1),
            BorderColor = ColorScheme.Outline.WithAlpha(72)
        };

        controls.Add(CreateText("Background settings", ColorScheme.ForeColor, 17f, true), 0, 0, 1, 2);
        controls.Add(CreateSettingsButton("Cover", () =>
        {
            preview.BackgroundImageLayout = ImageLayout.Cover;
            preview.Text = "Live background preview\nCover crops the image while keeping the surface filled.";
        }), 1, 0);
        controls.Add(CreateSettingsButton("Zoom", () =>
        {
            preview.BackgroundImageLayout = ImageLayout.Zoom;
            preview.Text = "Live background preview\nZoom keeps the entire image visible.";
        }), 1, 1);
        controls.Add(CreateSettingsButton("Left", () => preview.BackgroundImagePosition = BackgroundImagePosition.CenterLeft), 2, 0);
        controls.Add(CreateSettingsButton("Right", () => preview.BackgroundImagePosition = BackgroundImagePosition.CenterRight), 2, 1);
        controls.Add(CreateSettingsButton("Blur", () =>
        {
            preview.BackgroundImageBlurMode = BackgroundImageBlurMode.Cinematic;
            preview.BackgroundImageBlurAmount = 7f;
        }), 3, 0);
        controls.Add(CreateSettingsButton("Clear", () => preview.BackgroundImageBlurAmount = 0f), 3, 1);
        controls.Add(CreateSettingsButton("Fade", () => preview.BackgroundImageTransitionEffect = BackgroundImageTransitionEffect.Fade), 4, 0);
        controls.Add(CreateSettingsButton("Slide", () => preview.BackgroundImageTransitionEffect = BackgroundImageTransitionEffect.SlideHorizontal), 4, 1);
        controls.Add(CreateSettingsButton("Forest", () => StartRemoteBackground(preview, ForestUrl)), 5, 0);
        controls.Add(CreateSettingsButton("Snow", () => StartRemoteBackground(preview, SnowUrl)), 5, 1);
        controls.Add(CreateSettingsButton("Stretch", () => preview.BackgroundImageLayout = ImageLayout.Stretch), 6, 0);
        controls.Add(CreateSettingsButton("Tile", () =>
        {
            preview.BackgroundImageLayout = ImageLayout.Tile;
            preview.BackgroundImagePosition = BackgroundImagePosition.TopLeft;
        }), 6, 1);
        controls.Add(CreateText("This panel mirrors a Win11 personalization page: navigation, preview surface and compact command tiles.", ColorScheme.ForeColor.WithAlpha(165), 11.5f, false), 7, 0, 2, 2);
        return controls;
    }

    private Card CreateBlurSampleCard(string title, BackgroundImageBlurMode mode, float amount, string imageUrl, SKColor accent)
    {
        var card = CreateOverlayCard(title, $"{mode} mode, amount {amount:0}.", "BLUR MODE", "Preview", imageUrl, BackgroundImagePosition.Center, accent);
        card.BackgroundImageBlurMode = mode;
        card.BackgroundImageBlurAmount = amount;
        return card;
    }

    private Card CreateLayoutPositionCard()
    {
        var accent = new SKColor(99, 102, 241);
        var card = CreateShell(292, accent);
        var grid = new Grid
        {
            Dock = DockStyle.Fill,
            Padding = new Thickness(14),
            RowCount = 2,
            ColumnCount = 3,
            RowGap = 10,
            ColumnGap = 10,
            BackColor = SKColors.Transparent
        };

        grid.Add(CreateMiniBackgroundTile("Cover", ImageLayout.Cover, BackgroundImagePosition.CenterLeft, MountainUrl, accent), 0, 0);
        grid.Add(CreateMiniBackgroundTile("Zoom", ImageLayout.Zoom, BackgroundImagePosition.Center, MountainUrl, accent), 0, 1);
        grid.Add(CreateMiniBackgroundTile("Stretch", ImageLayout.Stretch, BackgroundImagePosition.Center, MountainUrl, accent), 0, 2);
        grid.Add(CreateMiniBackgroundTile("Tile", ImageLayout.Tile, BackgroundImagePosition.TopLeft, CoastUrl, accent), 1, 0);
        grid.Add(CreateMiniBackgroundTile("None", ImageLayout.None, BackgroundImagePosition.TopRight, DesertUrl, accent), 1, 1);
        grid.Add(CreateMiniBackgroundTile("25% 70%", ImageLayout.Cover, BackgroundImagePosition.FromPercent(25f, 70f), CabinUrl, accent), 1, 2);
        card.AddContent(grid);
        return card;
    }

    private Card CreateTransitionCaptionCard()
    {
        var accent = new SKColor(236, 72, 153);
        var card = CreateShell(330, accent);
        var layout = new Grid
        {
            Dock = DockStyle.Fill,
            Padding = new Thickness(14),
            RowCount = 1,
            ColumnCount = 5,
            ColumnGap = 10,
            BackColor = SKColors.Transparent
        };

        layout.Add(CreateCaptionPreviewTile("Overlay", BackgroundImageCaptionDesignMode.Overlay, ContentAlignment.BottomLeft, ForestUrl, accent), 0, 0);
        layout.Add(CreateCaptionPreviewTile("Glass", BackgroundImageCaptionDesignMode.Glass, ContentAlignment.MiddleCenter, CoastUrl, accent), 0, 1);
        layout.Add(CreateCaptionPreviewTile("Solid", BackgroundImageCaptionDesignMode.Solid, ContentAlignment.MiddleRight, MountainUrl, accent), 0, 2);
        layout.Add(CreateCaptionPreviewTile("Minimal", BackgroundImageCaptionDesignMode.Minimal, ContentAlignment.BottomRight, CabinUrl, accent), 0, 3);
        layout.Add(CreateTransitionPreviewTile("ScaleFade", BackgroundImageTransitionEffect.ScaleFade, accent), 0, 4);
        card.AddContent(layout);
        return card;
    }

    private static Card CreateManualLoadingCard()
    {
        var accent = new SKColor(34, 197, 94);
        var card = CreateShell(210, accent);
        var body = CreateBody(
            "Manual loading state",
            "Any ElementBase can show the same spinner with IsLoading = true, then hide it with IsLoading = false.",
            "PUBLIC API",
            string.Empty,
            accent);

        var toggleButton = CreateActionButton("Toggle IsLoading", accent);
        toggleButton.Click += (_, _) =>
        {
            card.IsLoading = !card.IsLoading;
            toggleButton.Text = card.IsLoading ? "Stop loading" : "Start loading";
        };
        body.Add(toggleButton, 4, 0);
        card.AddContent(body);
        return card;
    }

    private static Card CreateShell(int height, SKColor accent)
    {
        return new Card
        {
            Height = height,
            MinimumSize = new SKSize(0, height),
            Padding = new Thickness(0),
            Radius = new Radius(14),
            Border = new Thickness(1),
            BorderColor = accent.WithAlpha(ColorScheme.IsDarkMode ? (byte)124 : (byte)86),
            BackColor = ColorScheme.Surface,
            Shadow = new BoxShadow(0, 2, 7, 0, ColorScheme.ShadowColor.WithAlpha(ColorScheme.IsDarkMode ? (byte)10 : (byte)22))
        };
    }

    private static Grid CreateBody(string title, string description, string eyebrow, string action, SKColor accent)
    {
        var body = new Grid
        {
            Dock = DockStyle.Fill,
            Padding = new Thickness(22, 18, 22, 18),
            RowCount = 5,
            ColumnCount = 1,
            RowGap = 0,
            BackColor = SKColors.Transparent
        };

        body.Add(CreateText(eyebrow, accent, 11f, true), 0, 0);
        body.Add(CreateText(title, ColorScheme.ForeColor, 18f, true), 1, 0);
        body.Add(CreateText(description, ColorScheme.ForeColor.WithAlpha(ColorScheme.IsDarkMode ? (byte)188 : (byte)160), 12.5f, false), 2, 0, 2, 1);

        if (!string.IsNullOrEmpty(action))
            body.Add(CreateActionButton(action, accent), 4, 0);

        return body;
    }

    private static Grid CreateOverlayContent(string title, string description, string eyebrow, string action, SKColor accent)
    {
        var content = new Grid
        {
            Dock = DockStyle.Bottom,
            Height = 160,
            Padding = new Thickness(22, 16, 22, 16),
            RowCount = 5,
            ColumnCount = 1,
            RowGap = 0,
            BackColor = new SKColor(8, 15, 30, 176)
        };

        content.Add(CreateText(eyebrow, accent, 11f, true), 0, 0);
        content.Add(CreateText(title, SKColors.White, 19f, true), 1, 0);
        content.Add(CreateText(description, SKColors.White.WithAlpha(204), 12.5f, false), 2, 0, 2, 1);
        content.Add(CreateOverlayActionButton(action), 4, 0);
        return content;
    }

    private Element CreateMiniBackgroundTile(string label, ImageLayout layout, BackgroundImagePosition position, string imageUrl, SKColor accent)
    {
        var tile = new Element
        {
            Radius = new Radius(10),
            Border = new Thickness(1),
            BorderColor = accent.WithAlpha(100),
            BackColor = new SKColor(15, 23, 42),
            ForeColor = SKColors.White,
            Padding = new Thickness(10),
            Text = label,
            TextAlign = ContentAlignment.BottomLeft,
            BackgroundImageLayout = layout,
            BackgroundImagePosition = position
        };

        StartRemoteBackground(tile, imageUrl);
        return tile;
    }

    private Element CreateCaptionPreviewTile(string label, BackgroundImageCaptionDesignMode mode, ContentAlignment alignment, string imageUrl, SKColor accent)
    {
        var tile = CreateMiniBackgroundTile(label, ImageLayout.Cover, BackgroundImagePosition.Center, imageUrl, accent);
        tile.Text = $"{label}\n{alignment}";
        tile.BackgroundImageBlurAmount = mode == BackgroundImageCaptionDesignMode.Glass ? 2f : 0f;
        AddCaptionOverlay(tile, label, mode, alignment);
        return tile;
    }

    private Element CreateTransitionPreviewTile(string label, BackgroundImageTransitionEffect effect, SKColor accent)
    {
        var tile = CreateMiniBackgroundTile(label, ImageLayout.Cover, BackgroundImagePosition.Center, SnowUrl, accent);
        tile.BackgroundImageTransitionEffect = effect;
        tile.BackgroundImageTransitionDurationMs = 420;
        tile.BackgroundImageSlideshowIntervalMs = 1900;
        tile.BackgroundImageSlideshowRepeat = true;
        StartTileSlideshow(tile, [SnowUrl, ForestUrl, CoastUrl]);
        return tile;
    }

    private static void AddCaptionOverlay(Element tile, string label, BackgroundImageCaptionDesignMode mode, ContentAlignment alignment)
    {
        if (mode == BackgroundImageCaptionDesignMode.Hidden)
            return;

        var overlay = new Element
        {
            Dock = mode == BackgroundImageCaptionDesignMode.Minimal ? DockStyle.Top : DockStyle.Bottom,
            Height = mode == BackgroundImageCaptionDesignMode.Minimal ? 34 : 54,
            Padding = mode == BackgroundImageCaptionDesignMode.Minimal ? new Thickness(8, 0, 8, 0) : new Thickness(8),
            Radius = mode == BackgroundImageCaptionDesignMode.Minimal ? new Radius(0) : new Radius(8),
            Text = label,
            TextAlign = alignment,
            ForeColor = mode == BackgroundImageCaptionDesignMode.Solid ? ColorScheme.ForeColor : SKColors.White,
            BackColor = mode switch
            {
                BackgroundImageCaptionDesignMode.Glass => ColorScheme.Surface.WithAlpha(178),
                BackgroundImageCaptionDesignMode.Solid => ColorScheme.SurfaceContainerHigh.WithAlpha(235),
                BackgroundImageCaptionDesignMode.Minimal => SKColors.Transparent,
                _ => SKColors.Black.WithAlpha(132)
            },
            Border = mode == BackgroundImageCaptionDesignMode.Minimal ? new Thickness(0) : new Thickness(1),
            BorderColor = SKColors.White.WithAlpha(50)
        };

        tile.Controls.Add(overlay);
    }

    private static Element CreateText(string text, SKColor color, float size, bool bold)
    {
        return new Element
        {
            Padding = new Thickness(0),
            Border = new Thickness(0),
            BackColor = SKColors.Transparent,
            ForeColor = color,
            Font = new SKFont(SKTypeface.Default, size) { Embolden = bold },
            Text = text,
            TextAlign = ContentAlignment.MiddleLeft
        };
    }

    private static Button CreateActionButton(string text, SKColor accent)
    {
        return new Button
        {
            Margin = new Thickness(0, 5, 0, 0),
            Padding = new Thickness(12, 0, 12, 0),
            Radius = new Radius(9),
            BackColor = accent.WithAlpha(ColorScheme.IsDarkMode ? (byte)46 : (byte)22),
            ForeColor = accent,
            Border = new Thickness(1),
            BorderColor = accent.WithAlpha(70),
            Text = text
        };
    }

    private static Element CreateNavItem(string text, bool selected)
    {
        return new Element
        {
            Padding = new Thickness(10, 0, 10, 0),
            Radius = new Radius(8),
            BackColor = selected ? ColorScheme.Primary.WithAlpha(34) : SKColors.Transparent,
            Border = new Thickness(selected ? 1 : 0),
            BorderColor = selected ? ColorScheme.Primary.WithAlpha(90) : SKColors.Transparent,
            ForeColor = selected ? ColorScheme.Primary : ColorScheme.ForeColor.WithAlpha(185),
            Text = text,
            TextAlign = ContentAlignment.MiddleLeft
        };
    }

    private static Button CreateSettingsButton(string text, Action action)
    {
        var button = new Button
        {
            Radius = new Radius(9),
            Padding = new Thickness(10, 0, 10, 0),
            BackColor = ColorScheme.SurfaceContainer,
            ForeColor = ColorScheme.ForeColor,
            Border = new Thickness(1),
            BorderColor = ColorScheme.Outline.WithAlpha(76),
            Text = text
        };

        button.Click += (_, _) => action();
        return button;
    }

    private static Button CreateOverlayActionButton(string text)
    {
        return new Button
        {
            Margin = new Thickness(0, 5, 0, 0),
            Padding = new Thickness(12, 0, 12, 0),
            Radius = new Radius(9),
            BackColor = SKColors.White.WithAlpha(32),
            ForeColor = SKColors.White,
            Border = new Thickness(1),
            BorderColor = SKColors.White.WithAlpha(70),
            Text = text
        };
    }

    private Element CreateRemoteMedia(string imageUrl, BackgroundImagePosition position)
    {
        var media = new Element
        {
            BackColor = ColorScheme.SurfaceContainer,
            BackgroundImageLayout = ImageLayout.Cover,
            BackgroundImagePosition = position
        };

        StartRemoteBackground(media, imageUrl);
        return media;
    }

    private async void StartRemoteBackground(ElementBase element, string imageUrl)
    {
        try
        {
            await element.SetBackgroundImageFromUrlAsync(GetRemoteImageAsync(imageUrl));
        }
        catch
        {
            _remoteImages.Remove(imageUrl);
            element.BackColor = ColorScheme.SurfaceContainer;
        }
    }

    private async void StartSlideshow(ElementBase media, Button button, string[] imageUrls)
    {
        try
        {
            var frames = new BackgroundImageFrame[imageUrls.Length];
            for (var i = 0; i < imageUrls.Length; i++)
                frames[i] = new BackgroundImageFrame(await GetRemoteImageAsync(imageUrls[i]));

            media.BackgroundImages = frames;
            media.BackgroundImageSlideshowEnabled = true;
            button.Text = "Playing";
        }
        catch
        {
            for (var i = 0; i < imageUrls.Length; i++)
                _remoteImages.Remove(imageUrls[i]);

            button.Enabled = true;
            button.Text = "Try again";
        }
    }

    private async void StartTileSlideshow(ElementBase media, string[] imageUrls)
    {
        try
        {
            var frames = new BackgroundImageFrame[imageUrls.Length];
            for (var i = 0; i < imageUrls.Length; i++)
                frames[i] = new BackgroundImageFrame(await GetRemoteImageAsync(imageUrls[i]));

            media.BackgroundImages = frames;
            media.BackgroundImageSlideshowEnabled = true;
        }
        catch
        {
            for (var i = 0; i < imageUrls.Length; i++)
                _remoteImages.Remove(imageUrls[i]);
        }
    }

    private Task<SKImage> GetRemoteImageAsync(string imageUrl)
    {
        if (_remoteImages.TryGetValue(imageUrl, out var task))
            return task;

        task = SKImageExtensions.FromUrlAsync(imageUrl);
        _remoteImages.Add(imageUrl, task);
        return task;
    }
}
