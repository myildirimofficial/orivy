using Orivy;
using Orivy.Controls;
using SkiaSharp;

namespace Orivy.Example;

internal sealed partial class CardDemoPage
{
    private const string UnsplashMountainUrl = "https://images.unsplash.com/photo-1500530855697-b586d89ba3ee?auto=format&fit=crop&w=900&q=80";
    private const string UnsplashForestUrl = "https://images.unsplash.com/photo-1448375240586-882707db888b?auto=format&fit=crop&w=900&q=80";
    private const string UnsplashDeskUrl = "https://images.unsplash.com/photo-1497366754035-f200968a6e72?auto=format&fit=crop&w=900&q=80";
    private const string UnsplashNightUrl = "https://images.unsplash.com/photo-1519681393784-d120267933ba?auto=format&fit=crop&w=900&q=80";
    private const string UnsplashWideForestUrl = "https://images.unsplash.com/photo-1448375240586-882707db888b?auto=format&fit=crop&w=1400&h=520&q=80";
    private const string UnsplashWideDeskUrl = "https://images.unsplash.com/photo-1497366754035-f200968a6e72?auto=format&fit=crop&w=1400&h=520&q=80";
    private const string UnsplashTallMountainUrl = "https://images.unsplash.com/photo-1500530855697-b586d89ba3ee?auto=format&fit=crop&w=520&h=1400&q=80";
    private const string UnsplashTallNightUrl = "https://images.unsplash.com/photo-1519681393784-d120267933ba?auto=format&fit=crop&w=520&h=1400&q=80";

    private void InitializeComponent()
    {
        Text = "Cards";
        Padding = new Thickness(18);
        AutoScroll = true;

        var grid = new Grid
        {
            Name = "cardDemoGrid",
            Dock = DockStyle.Top,
            Height = 1880,
            RowCount = 7,
            ColumnCount = 3,
            RowGap = 14,
            ColumnGap = 14,
            BackColor = SKColors.Transparent
        };

        var overviewCard = CreateCard(
            "ElementBase native",
            "Header is painted by Card; content is normal Controls.",
            new SKColor(14, 165, 233));
        overviewCard.AddContent(CreateMetric("24", "Direct child controls"));
        overviewCard.AddContent(CreateSubtleText("No hidden content panel is required. Padding and DisplayRectangle decide where children live."));

        var actionCard = CreateCard(
            "Actions",
            "Buttons, inputs and layouts can be dropped into the card directly.",
            new SKColor(34, 197, 94));
        actionCard.AddContent(CreateButton("Primary action", true));
        actionCard.AddContent(CreateButton("Secondary", false));

        var mediaCard = CreateCard(
            "Background zoom",
            "BackgroundImageLayout = Zoom, position = Center.",
            new SKColor(168, 85, 247));
        mediaCard.BackColor = new SKColor(17, 24, 39);
        mediaCard.ForeColor = SKColors.White;
        mediaCard.BackgroundImage = CreateBackgroundImage(new SKColor(20, 184, 166), new SKColor(99, 102, 241), 520, 260);
        mediaCard.BackgroundImageLayout = ImageLayout.Zoom;
        mediaCard.BackgroundImagePosition = BackgroundImagePosition.Center;
        mediaCard.AddContent(CreateOverlayText("Card background uses the same ElementBase image pipeline."));

        var compactCard = CreateCard(
            "Compact",
            "Small padding, same control.",
            new SKColor(245, 158, 11));
        compactCard.Padding = new Thickness(14);
        compactCard.HeaderGap = 10;
        compactCard.AddContent(CreateMetric("8 ms", "Lightweight paint path"));

        var contentGridCard = CreateCard(
            "Nested grid",
            "Card children can host another Grid without special APIs.",
            new SKColor(236, 72, 153));
        var nestedGrid = new Grid
        {
            Dock = DockStyle.Top,
            Height = 96,
            RowCount = 2,
            ColumnCount = 2,
            RowGap = 8,
            ColumnGap = 8,
            BackColor = SKColors.Transparent
        };
        nestedGrid.Add(CreatePill("Surface"), 0, 0);
        nestedGrid.Add(CreatePill("Radius"), 0, 1);
        nestedGrid.Add(CreatePill("Padding"), 1, 0);
        nestedGrid.Add(CreatePill("Controls"), 1, 1);
        contentGridCard.AddContent(nestedGrid);

        var themeCard = CreateCard(
            "Theme aware",
            "Surface, outline and shadow resolve from ColorScheme.",
            new SKColor(99, 102, 241));
        themeCard.AddContent(CreateSubtleText("Switch the app theme; Card will update without every demo page repainting its own colors."));

        var tileCard = CreateCard(
            "Background tile",
            "Layout = Tile, position = TopLeft.",
            new SKColor(14, 165, 233));
        tileCard.BackgroundImage = CreatePatternImage(new SKColor(14, 165, 233), new SKColor(2, 132, 199));
        tileCard.BackgroundImageLayout = ImageLayout.Tile;
        tileCard.BackgroundImagePosition = BackgroundImagePosition.TopLeft;
        tileCard.AddContent(CreateSubtleText("Tile origin follows BackgroundImagePosition, just like CSS background-position."));

        var positionCard = CreateCard(
            "Background position",
            "Layout = None, position = BottomRight.",
            new SKColor(22, 163, 74));
        positionCard.BackgroundImage = CreateBadgeImage(new SKColor(34, 197, 94));
        positionCard.BackgroundImageLayout = ImageLayout.None;
        positionCard.BackgroundImagePosition = BackgroundImagePosition.BottomRight;
        positionCard.AddContent(CreateSubtleText("None and Center layouts also respect BackgroundImagePosition."));

        var remoteCard = CreateCard(
            "Remote background",
            "Loads from URL and shows a centered loading spinner.",
            new SKColor(249, 115, 22));
        remoteCard.AddContent(CreateSubtleText("Click the button. While the image downloads, Card renders the loading state in its background area."));
        var remoteButton = CreateButton("Load remote image", true);
        remoteButton.Click += async (_, _) =>
        {
            try
            {
                await remoteCard.SetBackgroundImageFromUrlAsync("https://picsum.photos/640/360");
                remoteCard.BackgroundImageLayout = ImageLayout.Zoom;
                remoteCard.BackgroundImagePosition = BackgroundImagePosition.Center;
            }
            catch
            {
                remoteCard.AddContent(CreateSubtleText("Image could not be loaded."));
            }
        };
        remoteCard.AddContent(remoteButton);

        var objectLeftCard = CreateTailwindBackgroundCard(
            "object-left",
            "Wide image + Zoom + CenterLeft. The left side stays visible.",
            UnsplashWideForestUrl,
            ImageLayout.Zoom,
            BackgroundImagePosition.CenterLeft,
            new SKColor(59, 130, 246));

        var objectCenterCard = CreateTailwindBackgroundCard(
            "object-center",
            "Wide image + Zoom + Center. Both sides are cropped evenly.",
            UnsplashWideForestUrl,
            ImageLayout.Zoom,
            BackgroundImagePosition.Center,
            new SKColor(14, 165, 233));

        var objectRightCard = CreateTailwindBackgroundCard(
            "object-right",
            "Wide image + Zoom + CenterRight. The right side stays visible.",
            UnsplashWideForestUrl,
            ImageLayout.Zoom,
            BackgroundImagePosition.CenterRight,
            new SKColor(236, 72, 153));

        var objectTopCard = CreateTailwindBackgroundCard(
            "object-top",
            "Tall image + Zoom + TopCenter. The top of the image is pinned.",
            UnsplashTallNightUrl,
            ImageLayout.Zoom,
            BackgroundImagePosition.TopCenter,
            new SKColor(245, 158, 11));

        var objectBottomCard = CreateTailwindBackgroundCard(
            "object-bottom",
            "Tall image + Zoom + BottomCenter. The bottom of the image is pinned.",
            UnsplashTallNightUrl,
            ImageLayout.Zoom,
            BackgroundImagePosition.BottomCenter,
            new SKColor(34, 197, 94));

        var objectFillCard = CreateTailwindBackgroundCard(
            "object-fill",
            "Stretch. The image fills the card bounds exactly.",
            UnsplashWideDeskUrl,
            ImageLayout.Stretch,
            BackgroundImagePosition.Center,
            new SKColor(8, 145, 178));

        var objectNoneCard = CreateTailwindBackgroundCard(
            "object-none",
            "None + TopRight. No scaling; the card clips the raw image.",
            UnsplashTallMountainUrl,
            ImageLayout.None,
            BackgroundImagePosition.TopRight,
            new SKColor(124, 58, 237));

        var objectPercentCard = CreateTailwindBackgroundCard(
            "object-[25%_70%]",
            "Tall image + Zoom + BackgroundImagePosition.FromPercent(25, 70).",
            UnsplashTallMountainUrl,
            ImageLayout.Zoom,
            BackgroundImagePosition.FromPercent(25f, 70f),
            new SKColor(220, 38, 38));

        var unsplashSlidesCard = CreateCard(
            "Unsplash slideshow",
            "Remote BackgroundImages + ScaleFade transition.",
            new SKColor(168, 85, 247));
        unsplashSlidesCard.BackColor = new SKColor(15, 23, 42);
        unsplashSlidesCard.ForeColor = SKColors.White;
        unsplashSlidesCard.BackgroundImageLayout = ImageLayout.Zoom;
        unsplashSlidesCard.BackgroundImagePosition = BackgroundImagePosition.Center;
        unsplashSlidesCard.BackgroundImageTransitionEffect = BackgroundImageTransitionEffect.ScaleFade;
        unsplashSlidesCard.BackgroundImageTransitionDurationMs = 520;
        unsplashSlidesCard.BackgroundImageSlideshowIntervalMs = 1600;
        unsplashSlidesCard.BackgroundImageSlideshowRepeat = true;
        unsplashSlidesCard.AddContent(CreateTailwindLabel("background-slideshow"));
        unsplashSlidesCard.AddContent(CreateOverlayText("Downloads multiple Unsplash images and starts the slideshow after all frames are ready."));
        StartUnsplashSlideshow(unsplashSlidesCard, [UnsplashMountainUrl, UnsplashForestUrl, UnsplashDeskUrl, UnsplashNightUrl]);

        grid.Add(overviewCard, 0, 0, 1, 2);
        grid.Add(actionCard, 0, 2);
        grid.Add(mediaCard, 1, 0);
        grid.Add(compactCard, 1, 1);
        grid.Add(contentGridCard, 1, 2);
        grid.Add(themeCard, 2, 0, 1, 3);
        grid.Add(tileCard, 3, 0);
        grid.Add(positionCard, 3, 1);
        grid.Add(remoteCard, 3, 2);
        grid.Add(objectLeftCard, 4, 0);
        grid.Add(objectCenterCard, 4, 1);
        grid.Add(objectRightCard, 4, 2);
        grid.Add(objectTopCard, 5, 0);
        grid.Add(objectBottomCard, 5, 1);
        grid.Add(objectFillCard, 5, 2);
        grid.Add(objectNoneCard, 6, 0);
        grid.Add(objectPercentCard, 6, 1);
        grid.Add(unsplashSlidesCard, 6, 2);

        Controls.Add(grid);
    }

    private static Card CreateCard(string title, string description, SKColor accent)
    {
        var card = new Card
        {
            Title = title,
            Description = description,
            Padding = new Thickness(18),
            Radius = new Radius(14),
            BackColor = ColorScheme.Surface,
            Border = new Thickness(1),
            BorderColor = ColorScheme.Outline.WithAlpha(ColorScheme.IsDarkMode ? (byte)82 : (byte)96)
        };
        card.BorderColor = accent.WithAlpha(ColorScheme.IsDarkMode ? (byte)150 : (byte)135);

        return card;
    }

    private static Card CreateTailwindBackgroundCard(string title, string description, string imageUrl, ImageLayout layout, BackgroundImagePosition position, SKColor accent)
    {
        var card = CreateCard(title, description, accent);
        card.BackColor = new SKColor(15, 23, 42);
        card.ForeColor = SKColors.White;
        card.BackgroundImageLayout = layout;
        card.BackgroundImagePosition = position;
        card.AddContent(CreateTailwindLabel(title));
        card.AddContent(CreateOverlayText(description));
        StartRemoteBackground(card, imageUrl);
        return card;
    }

    private static async void StartRemoteBackground(Card card, string imageUrl)
    {
        try
        {
            await card.SetBackgroundImageFromUrlAsync(imageUrl);
        }
        catch
        {
            card.AddContent(CreateSubtleText("Remote image could not be loaded."));
        }
    }

    private static async void StartUnsplashSlideshow(Card card, string[] imageUrls)
    {
        try
        {
            var frames = new BackgroundImageFrame[imageUrls.Length];
            for (var i = 0; i < imageUrls.Length; i++)
            {
                var image = await SKImageExtensions.FromUrlAsync(imageUrls[i]);
                frames[i] = new BackgroundImageFrame(image);
            }

            card.BackgroundImages = frames;
            card.BackgroundImageSlideshowEnabled = true;
        }
        catch
        {
            card.AddContent(CreateSubtleText("Remote slideshow images could not be loaded."));
        }
    }

    private static Element CreateMetric(string value, string label)
    {
        return new Element
        {
            Dock = DockStyle.Top,
            Height = 58,
            Margin = new Thickness(0, 0, 0, 10),
            Padding = new Thickness(12),
            Radius = new Radius(10),
            BackColor = ColorScheme.SurfaceContainer,
            Border = new Thickness(1),
            BorderColor = ColorScheme.Outline.WithAlpha(70),
            ForeColor = ColorScheme.ForeColor,
            Text = $"{value}\n{label}",
            TextAlign = ContentAlignment.MiddleLeft
        };
    }

    private static Element CreateSubtleText(string text)
    {
        return new Element
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            Padding = new Thickness(0),
            Margin = new Thickness(0),
            BackColor = SKColors.Transparent,
            Border = new Thickness(0),
            ForeColor = ColorScheme.ForeColor.WithAlpha(ColorScheme.IsDarkMode ? (byte)180 : (byte)155),
            Text = text,
            TextAlign = ContentAlignment.MiddleLeft
        };
    }

    private static Element CreateOverlayText(string text)
    {
        return new Element
        {
            Dock = DockStyle.Bottom,
            Height = 56,
            Padding = new Thickness(12),
            Margin = new Thickness(0),
            Radius = new Radius(10),
            BackColor = new SKColor(15, 23, 42, 178),
            Border = new Thickness(1),
            BorderColor = SKColors.White.WithAlpha(38),
            ForeColor = SKColors.White,
            Text = text,
            TextAlign = ContentAlignment.MiddleLeft
        };
    }

    private static Element CreateTailwindLabel(string text)
    {
        return new Element
        {
            Dock = DockStyle.Top,
            Height = 32,
            Padding = new Thickness(10, 0, 10, 0),
            Margin = new Thickness(0),
            Radius = new Radius(9),
            BackColor = SKColors.Black.WithAlpha(118),
            Border = new Thickness(1),
            BorderColor = SKColors.White.WithAlpha(44),
            ForeColor = SKColors.White,
            Text = text,
            TextAlign = ContentAlignment.MiddleLeft
        };
    }

    private static Button CreateButton(string text, bool primary)
    {
        return new Button
        {
            Dock = DockStyle.Top,
            Height = 38,
            Margin = new Thickness(0, 0, 0, 10),
            Text = text,
            BackColor = primary ? ColorScheme.Primary : ColorScheme.SurfaceContainer,
            ForeColor = primary ? SKColors.White : ColorScheme.ForeColor,
            BorderColor = primary ? ColorScheme.Primary.Brightness(-0.12f) : ColorScheme.Outline.WithAlpha(90)
        };
    }

    private static Element CreatePill(string text)
    {
        return new Element
        {
            Padding = new Thickness(10, 0, 10, 0),
            Radius = new Radius(9),
            BackColor = ColorScheme.SurfaceContainer,
            Border = new Thickness(1),
            BorderColor = ColorScheme.Outline.WithAlpha(70),
            ForeColor = ColorScheme.ForeColor,
            Text = text,
            TextAlign = ContentAlignment.MiddleCenter
        };
    }

    private static SKImage CreateBackgroundImage(SKColor from, SKColor to, int width, int height)
    {
        using var surface = SKSurface.Create(new SKImageInfo(width, height, SKColorType.Bgra8888, SKAlphaType.Premul));
        var canvas = surface.Canvas;
        canvas.Clear(from);

        using var paint = new SKPaint { IsAntialias = true };
        paint.Shader = SKShader.CreateLinearGradient(
            new SKPoint(0, 0),
            new SKPoint(width, height),
            new[] { from, to },
            null,
            SKShaderTileMode.Clamp);
        canvas.DrawRect(0, 0, width, height, paint);
        paint.Shader = null;
        paint.Color = SKColors.White.WithAlpha(42);
        canvas.DrawCircle(width * 0.78f, height * 0.28f, height * 0.46f, paint);
        paint.Color = SKColors.Black.WithAlpha(30);
        canvas.DrawCircle(width * 0.18f, height * 0.78f, height * 0.34f, paint);
        canvas.Flush();
        return surface.Snapshot();
    }

    private static SKImage CreatePatternImage(SKColor background, SKColor foreground)
    {
        using var surface = SKSurface.Create(new SKImageInfo(48, 48, SKColorType.Bgra8888, SKAlphaType.Premul));
        var canvas = surface.Canvas;
        canvas.Clear(background.WithAlpha(38));

        using var paint = new SKPaint { IsAntialias = true, Color = foreground.WithAlpha(120), StrokeWidth = 2 };
        canvas.DrawLine(0, 48, 48, 0, paint);
        paint.Color = foreground.WithAlpha(72);
        canvas.DrawCircle(24, 24, 8, paint);
        canvas.Flush();
        return surface.Snapshot();
    }

    private static SKImage CreateBadgeImage(SKColor accent)
    {
        using var surface = SKSurface.Create(new SKImageInfo(96, 96, SKColorType.Bgra8888, SKAlphaType.Premul));
        var canvas = surface.Canvas;
        canvas.Clear(SKColors.Transparent);

        using var paint = new SKPaint { IsAntialias = true, Color = accent.WithAlpha(220) };
        canvas.DrawCircle(48, 48, 34, paint);
        paint.Color = SKColors.White.WithAlpha(220);
        paint.StrokeWidth = 6;
        paint.Style = SKPaintStyle.Stroke;
        paint.StrokeCap = SKStrokeCap.Round;
        canvas.DrawLine(34, 50, 44, 60, paint);
        canvas.DrawLine(44, 60, 64, 36, paint);
        canvas.Flush();
        return surface.Snapshot();
    }
}
