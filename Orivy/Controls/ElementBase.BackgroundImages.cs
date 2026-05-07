using Orivy.Animation;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;

namespace Orivy.Controls;

public abstract partial class ElementBase
{
    private const int DefaultBackgroundImageTransitionDurationMs = 280;
    private const int MaxBackgroundBlurCacheEntries = 4;

    private readonly AnimationManager _backgroundImageTransitionAnimation = new(true);
    private readonly SKPaint _backgroundImagePaint = new() { IsAntialias = true };
    private readonly object _backgroundBlurCacheSync = new();
    private readonly List<BackgroundBlurCacheEntry> _backgroundBlurCacheEntries = new();

    private BackgroundImageFrame[] _backgroundImages = Array.Empty<BackgroundImageFrame>();
    private BackgroundImageCaption _backgroundImageCaption = BackgroundImageCaption.Empty;
    private ContentAlignment _backgroundImageCaptionLayout = ContentAlignment.MiddleLeft;
    private BackgroundImageCaptionDesignMode _backgroundImageCaptionDesignMode = BackgroundImageCaptionDesignMode.Overlay;
    private SKImageFilter? _backgroundImageBlurFilter;
    private int _backgroundImageIndex;
    private int _backgroundImageTransitionDirection = 1;
    private bool _useBackgroundImageCollection;
    private SKImage? _backgroundTransitionFromImage;
    private SKImage? _backgroundTransitionToImage;
    private CancellationTokenSource? _backgroundImageSlideshowCts;
    private BackgroundImageTransitionEffect _backgroundImageTransitionEffect = BackgroundImageTransitionEffect.None;
    private int _backgroundImageTransitionDurationMs = DefaultBackgroundImageTransitionDurationMs;
    private int _backgroundImageSlideshowIntervalMs = 2600;
    private bool _backgroundImageSlideshowEnabled;
    private bool _backgroundImageSlideshowRepeat = true;
    private float _backgroundImageBlurAmount;
    private BackgroundImageBlurMode _backgroundImageBlurMode = BackgroundImageBlurMode.Normal;

    [Category("Appearance")]
    public BackgroundImageFrame[] BackgroundImages
    {
        get => CopyBackgroundImages(_backgroundImages);
        set
        {
            var nextImages = CopyBackgroundImages(value);
            var nextUsesCollection = nextImages.Length > 0;

            if (AreSameBackgroundImages(_backgroundImages, nextImages) && _useBackgroundImageCollection == nextUsesCollection)
                return;

            var previousImage = GetDisplayedBackgroundImage();
            var previousFrame = GetDisplayedBackgroundImageFrame();

            _backgroundImages = nextImages;
            _useBackgroundImageCollection = nextUsesCollection;
            _backgroundImageIndex = _backgroundImages.Length == 0
                ? 0
                : Math.Clamp(_backgroundImageIndex, 0, _backgroundImages.Length - 1);

            var nextImage = GetDisplayedBackgroundImage();
            ApplyBackgroundImageChange(previousImage, nextImage, 1, allowTransition: nextImages.Length > 1);
            OnBackgroundImageChanged(EventArgs.Empty);
            NotifyBackgroundImageMetadataChange(previousFrame);
            UpdateBackgroundImageSlideshowState();
            Invalidate();
        }
    }

    [Category("Appearance")]
    [DefaultValue(ContentAlignment.MiddleLeft)]
    public ContentAlignment BackgroundImageCaptionLayout
    {
        get
        {
            if (_useBackgroundImageCollection && _backgroundImages.Length > 0)
                return _backgroundImages[Math.Clamp(_backgroundImageIndex, 0, _backgroundImages.Length - 1)].CaptionLayout;

            return _backgroundImageCaptionLayout;
        }
        set
        {
            var previousFrame = GetDisplayedBackgroundImageFrame();

            if (_useBackgroundImageCollection && _backgroundImages.Length > 0)
            {
                var currentIndex = Math.Clamp(_backgroundImageIndex, 0, _backgroundImages.Length - 1);
                var currentFrame = _backgroundImages[currentIndex];
                if (currentFrame.CaptionLayout == value)
                    return;

                var nextImages = CopyBackgroundImages(_backgroundImages);
                nextImages[currentIndex] = new BackgroundImageFrame(currentFrame.Image, currentFrame.Caption, value, currentFrame.CaptionDesignMode);
                _backgroundImages = nextImages;
                NotifyBackgroundImageMetadataChange(previousFrame);
                Invalidate();
                return;
            }

            if (_backgroundImageCaptionLayout == value)
                return;

            _backgroundImageCaptionLayout = value;
            NotifyBackgroundImageMetadataChange(previousFrame);
            Invalidate();
        }
    }

    [Category("Appearance")]
    [DefaultValue(BackgroundImageCaptionDesignMode.Overlay)]
    public BackgroundImageCaptionDesignMode BackgroundImageCaptionDesignMode
    {
        get
        {
            if (_useBackgroundImageCollection && _backgroundImages.Length > 0)
                return _backgroundImages[Math.Clamp(_backgroundImageIndex, 0, _backgroundImages.Length - 1)].CaptionDesignMode;

            return _backgroundImageCaptionDesignMode;
        }
        set
        {
            var previousFrame = GetDisplayedBackgroundImageFrame();

            if (_useBackgroundImageCollection && _backgroundImages.Length > 0)
            {
                var currentIndex = Math.Clamp(_backgroundImageIndex, 0, _backgroundImages.Length - 1);
                var currentFrame = _backgroundImages[currentIndex];
                if (currentFrame.CaptionDesignMode == value)
                    return;

                var nextImages = CopyBackgroundImages(_backgroundImages);
                nextImages[currentIndex] = new BackgroundImageFrame(currentFrame.Image, currentFrame.Caption, currentFrame.CaptionLayout, value);
                _backgroundImages = nextImages;
                NotifyBackgroundImageMetadataChange(previousFrame);
                Invalidate();
                return;
            }

            if (_backgroundImageCaptionDesignMode == value)
                return;

            _backgroundImageCaptionDesignMode = value;
            NotifyBackgroundImageMetadataChange(previousFrame);
            Invalidate();
        }
    }

    [Browsable(false)]
    public BackgroundImageFrame? CurrentBackgroundImageFrame => GetDisplayedBackgroundImageFrame();

    [Browsable(false)]
    public BackgroundImageCaption? CurrentBackgroundImageCaption => GetDisplayedBackgroundImageCaption();

    [Browsable(false)]
    public ContentAlignment CurrentBackgroundImageCaptionLayout
        => GetDisplayedBackgroundImageFrame()?.CaptionLayout ?? _backgroundImageCaptionLayout;

    [Browsable(false)]
    public BackgroundImageCaptionDesignMode CurrentBackgroundImageCaptionDesignMode
        => GetDisplayedBackgroundImageFrame()?.CaptionDesignMode ?? _backgroundImageCaptionDesignMode;

    [Category("Appearance")]
    [DefaultValue(0f)]
    public float BackgroundImageBlurAmount
    {
        get => _backgroundImageBlurAmount;
        set
        {
            var normalizedAmount = Math.Max(0f, value);
            if (Math.Abs(_backgroundImageBlurAmount - normalizedAmount) < 0.01f)
                return;

            _backgroundImageBlurAmount = normalizedAmount;
            UpdateBackgroundImageBlurFilter();
            Invalidate();
        }
    }

    [Category("Appearance")]
    [DefaultValue(BackgroundImageBlurMode.Normal)]
    public BackgroundImageBlurMode BackgroundImageBlurMode
    {
        get => _backgroundImageBlurMode;
        set
        {
            if (_backgroundImageBlurMode == value)
                return;

            _backgroundImageBlurMode = value;
            UpdateBackgroundImageBlurFilter();
            Invalidate();
        }
    }

    [Category("Appearance")]
    [DefaultValue(0)]
    public int BackgroundImageIndex
    {
        get => _backgroundImageIndex;
        set
        {
            if (_backgroundImages.Length == 0)
            {
                if (_backgroundImageIndex == 0 && !_useBackgroundImageCollection)
                    return;

                var previousCollectionFrame = GetDisplayedBackgroundImageFrame();
                _backgroundImageIndex = 0;
                _useBackgroundImageCollection = false;
                StopBackgroundImageTransition();
                UpdateBackgroundImageSlideshowState();
                NotifyBackgroundImageMetadataChange(previousCollectionFrame);
                Invalidate();
                return;
            }

            var normalizedIndex = Math.Clamp(value, 0, _backgroundImages.Length - 1);
            if (_backgroundImageIndex == normalizedIndex && _useBackgroundImageCollection)
                return;

            var previousImage = GetDisplayedBackgroundImage();
            var previousFrame = GetDisplayedBackgroundImageFrame();
            var previousIndex = _backgroundImageIndex;

            _backgroundImageIndex = normalizedIndex;
            _useBackgroundImageCollection = true;

            var direction = normalizedIndex >= previousIndex ? 1 : -1;
            var nextImage = GetDisplayedBackgroundImage();

            ApplyBackgroundImageChange(previousImage, nextImage, direction, allowTransition: true);
            OnBackgroundImageChanged(EventArgs.Empty);
            NotifyBackgroundImageMetadataChange(previousFrame);
            RestartBackgroundImageSlideshowIfRunning();
            Invalidate();
        }
    }

    [Category("Appearance")]
    [DefaultValue(false)]
    public bool BackgroundImageSlideshowEnabled
    {
        get => _backgroundImageSlideshowEnabled;
        set
        {
            if (_backgroundImageSlideshowEnabled == value)
                return;

            _backgroundImageSlideshowEnabled = value;
            UpdateBackgroundImageSlideshowState();
            Invalidate();
        }
    }

    [Category("Appearance")]
    [DefaultValue(2600)]
    public int BackgroundImageSlideshowIntervalMs
    {
        get => _backgroundImageSlideshowIntervalMs;
        set
        {
            var normalizedInterval = Math.Max(250, value);
            if (_backgroundImageSlideshowIntervalMs == normalizedInterval)
                return;

            _backgroundImageSlideshowIntervalMs = normalizedInterval;
            RestartBackgroundImageSlideshowIfRunning();
            Invalidate();
        }
    }

    [Category("Appearance")]
    [DefaultValue(true)]
    public bool BackgroundImageSlideshowRepeat
    {
        get => _backgroundImageSlideshowRepeat;
        set
        {
            if (_backgroundImageSlideshowRepeat == value)
                return;

            _backgroundImageSlideshowRepeat = value;
            UpdateBackgroundImageSlideshowState();
            Invalidate();
        }
    }

    [Category("Appearance")]
    [DefaultValue(BackgroundImageTransitionEffect.None)]
    public BackgroundImageTransitionEffect BackgroundImageTransitionEffect
    {
        get => _backgroundImageTransitionEffect;
        set
        {
            if (_backgroundImageTransitionEffect == value)
                return;

            _backgroundImageTransitionEffect = value;
            if (value == BackgroundImageTransitionEffect.None)
                StopBackgroundImageTransition();

            Invalidate();
        }
    }

    [Category("Appearance")]
    [DefaultValue(DefaultBackgroundImageTransitionDurationMs)]
    public int BackgroundImageTransitionDurationMs
    {
        get => _backgroundImageTransitionDurationMs;
        set
        {
            var normalizedDuration = Math.Max(0, value);
            if (_backgroundImageTransitionDurationMs == normalizedDuration)
                return;

            _backgroundImageTransitionDurationMs = normalizedDuration;
            UpdateBackgroundImageTransitionTiming();

            if (normalizedDuration == 0)
                StopBackgroundImageTransition();

            Invalidate();
        }
    }

    private void InitializeBackgroundImageTransitionSystem()
    {
        _backgroundImageTransitionAnimation.InterruptAnimation = true;
        _backgroundImageTransitionAnimation.AnimationType = AnimationType.CubicEaseOut;
        UpdateBackgroundImageTransitionTiming();
        UpdateBackgroundImageBlurFilter();
        _backgroundImageTransitionAnimation.OnAnimationProgress += HandleBackgroundImageTransitionProgress;
        _backgroundImageTransitionAnimation.OnAnimationFinished += HandleBackgroundImageTransitionFinished;
    }

    private void DisposeBackgroundImageTransitionSystem()
    {
        StopBackgroundImageSlideshowLoop();
        _backgroundImageTransitionAnimation.OnAnimationProgress -= HandleBackgroundImageTransitionProgress;
        _backgroundImageTransitionAnimation.OnAnimationFinished -= HandleBackgroundImageTransitionFinished;
        _backgroundImageTransitionAnimation.Dispose();
        ClearBackgroundBlurCache();
        _backgroundImageBlurFilter?.Dispose();
        _backgroundImageBlurFilter = null;
        _backgroundImagePaint.Dispose();
        _backgroundTransitionFromImage = null;
        _backgroundTransitionToImage = null;
    }

    private void UpdateBackgroundImageBlurFilter()
    {
        ClearBackgroundBlurCache();
        _backgroundImageBlurFilter?.Dispose();
        _backgroundImageBlurFilter = null;

        if (_backgroundImageBlurAmount <= 0f)
            return;

        var (sigmaX, sigmaY) = GetBackgroundImageBlurSigma(_backgroundImageBlurAmount, _backgroundImageBlurMode);

        if (sigmaX <= 0f && sigmaY <= 0f)
            return;

        _backgroundImageBlurFilter = SKImageFilter.CreateBlur(sigmaX, sigmaY, SKShaderTileMode.Clamp);
    }

    private static (float SigmaX, float SigmaY) GetBackgroundImageBlurSigma(float blurAmount, BackgroundImageBlurMode blurMode)
    {
        var amount = Math.Max(0f, blurAmount);
        return blurMode switch
        {
            BackgroundImageBlurMode.Horizontal => (amount, 0f),
            BackgroundImageBlurMode.Vertical => (0f, amount),
            BackgroundImageBlurMode.Wide => (amount * 1.8f, amount * 0.55f),
            BackgroundImageBlurMode.Tall => (amount * 0.55f, amount * 1.8f),
            BackgroundImageBlurMode.Cinematic => (amount * 2.4f, amount * 0.35f),
            BackgroundImageBlurMode.Portrait => (amount * 0.35f, amount * 2.4f),
            _ => (amount, amount)
        };
    }

    private void HandleBackgroundImageTransitionProgress(object _)
    {
        if (IsDisposed || Disposing)
            return;

        Invalidate();
    }

    private void HandleBackgroundImageTransitionFinished(object _)
    {
        _backgroundTransitionFromImage = null;
        _backgroundTransitionToImage = null;

        if (IsDisposed || Disposing)
            return;

        Invalidate();
    }

    private void UpdateBackgroundImageTransitionTiming()
    {
        var increment = GetBackgroundImageTransitionIncrement(_backgroundImageTransitionDurationMs);
        _backgroundImageTransitionAnimation.Increment = increment;
        _backgroundImageTransitionAnimation.SecondaryIncrement = increment;
    }

    private static double GetBackgroundImageTransitionIncrement(int durationMs)
    {
        if (durationMs <= 0)
            return 1d;

        return Math.Clamp(16d / Math.Max(16, durationMs), 0.01d, 1d);
    }

    private void ApplyBackgroundImageChange(SKImage? previousImage, SKImage? nextImage, int direction, bool allowTransition)
    {
        _backgroundImageTransitionDirection = direction >= 0 ? 1 : -1;
        ClearBackgroundBlurCache();

        if (ReferenceEquals(previousImage, nextImage))
        {
            StopBackgroundImageTransition();
            return;
        }

        StopBackgroundImageTransition();

        if (!allowTransition
            || _backgroundImageTransitionEffect == BackgroundImageTransitionEffect.None
            || _backgroundImageTransitionDurationMs <= 0
            || previousImage == null
            || nextImage == null)
        {
            return;
        }

        _backgroundTransitionFromImage = previousImage;
        _backgroundTransitionToImage = nextImage;
        _backgroundImageTransitionAnimation.SetProgress(0d);
        _backgroundImageTransitionAnimation.StartNewAnimation(AnimationDirection.In);
    }

    private void StopBackgroundImageTransition()
    {
        _backgroundImageTransitionAnimation.Stop();
        _backgroundTransitionFromImage = null;
        _backgroundTransitionToImage = null;
    }

    private void UpdateBackgroundImageSlideshowState()
    {
        var shouldRun = !IsDisposed
            && !Disposing
            && Visible
            && _backgroundImageSlideshowEnabled
            && _backgroundImages.Length > 1
            && (_backgroundImageSlideshowRepeat || _backgroundImageIndex < _backgroundImages.Length - 1);

        if (!shouldRun)
        {
            StopBackgroundImageSlideshowLoop();
            return;
        }

        if (_backgroundImageSlideshowCts != null)
            return;

        StartBackgroundImageSlideshowLoop();
    }

    private void RestartBackgroundImageSlideshowIfRunning()
    {
        var wasRunning = _backgroundImageSlideshowCts != null;
        StopBackgroundImageSlideshowLoop();

        if (wasRunning || _backgroundImageSlideshowEnabled)
            UpdateBackgroundImageSlideshowState();
    }

    private void StartBackgroundImageSlideshowLoop()
    {
        if (_backgroundImageSlideshowCts != null)
            return;

        var cts = new CancellationTokenSource();
        _backgroundImageSlideshowCts = cts;
        _ = RunBackgroundImageSlideshowAsync(cts);
    }

    private void StopBackgroundImageSlideshowLoop()
    {
        var cts = _backgroundImageSlideshowCts;
        if (cts == null)
            return;

        _backgroundImageSlideshowCts = null;
        cts.Cancel();
    }

    private async Task RunBackgroundImageSlideshowAsync(CancellationTokenSource cts)
    {
        try
        {
            while (!cts.IsCancellationRequested)
            {
                await Task.Delay(_backgroundImageSlideshowIntervalMs, cts.Token);

                if (cts.IsCancellationRequested)
                    break;

                AdvanceBackgroundImageSlideshow();
            }
        }
        catch (OperationCanceledException)
        {
        }
        finally
        {
            if (ReferenceEquals(_backgroundImageSlideshowCts, cts))
                _backgroundImageSlideshowCts = null;

            cts.Dispose();
        }
    }

    private void AdvanceBackgroundImageSlideshow()
    {
        if (IsDisposed || Disposing || !_backgroundImageSlideshowEnabled || _backgroundImages.Length <= 1)
        {
            StopBackgroundImageSlideshowLoop();
            return;
        }

        if (_backgroundImageIndex >= _backgroundImages.Length - 1)
        {
            if (!_backgroundImageSlideshowRepeat)
            {
                StopBackgroundImageSlideshowLoop();
                return;
            }

            BackgroundImageIndex = 0;
            return;
        }

        BackgroundImageIndex = _backgroundImageIndex + 1;
    }

    private BackgroundImageFrame? GetDisplayedBackgroundImageFrame()
    {
        if (_useBackgroundImageCollection && _backgroundImages.Length > 0)
            return _backgroundImages[Math.Clamp(_backgroundImageIndex, 0, _backgroundImages.Length - 1)];

        if (_backgroundImage == null)
            return null;

        return new BackgroundImageFrame(_backgroundImage, _backgroundImageCaption, _backgroundImageCaptionLayout, _backgroundImageCaptionDesignMode);
    }

    private SKImage? GetDisplayedBackgroundImage()
    {
        return GetDisplayedBackgroundImageFrame()?.Image;
    }

    private BackgroundImageCaption? GetDisplayedBackgroundImageCaption()
    {
        var frame = GetDisplayedBackgroundImageFrame();
        if (frame == null || frame.Caption.IsEmpty)
            return null;

        return frame.Caption;
    }

    protected void RenderBackgroundImages(SKCanvas canvas, SKRect bounds)
    {
        var activeImage = GetDisplayedBackgroundImage();
        if (activeImage == null && _backgroundTransitionFromImage == null && _backgroundTransitionToImage == null)
            return;

        var saved = canvas.Save();
        canvas.ClipRect(bounds, antialias: true);

        if (_backgroundTransitionFromImage != null && _backgroundTransitionToImage != null)
            DrawBackgroundImageTransition(canvas, bounds);
        else if (activeImage != null)
            DrawBackgroundImage(canvas, activeImage, bounds, 255);

        canvas.RestoreToCount(saved);
    }

    protected bool TryGetBackgroundImageSampleColor(SKRect contentBounds, SKRect sampleBounds, out SKColor color)
    {
        color = SKColors.Empty;

        var image = GetDisplayedBackgroundImage();
        if (image == null || contentBounds.Width <= 0f || contentBounds.Height <= 0f || sampleBounds.Width <= 0f || sampleBounds.Height <= 0f)
            return false;

        using var pixmap = image.PeekPixels();
        if (pixmap == null)
            return false;

        long red = 0;
        long green = 0;
        long blue = 0;
        long alpha = 0;
        var sampleCount = 0;
        const int columns = 5;
        const int rows = 3;

        for (var row = 0; row < rows; row++)
        {
            var y = sampleBounds.Top + (sampleBounds.Height * ((row + 0.5f) / rows));
            for (var column = 0; column < columns; column++)
            {
                var x = sampleBounds.Left + (sampleBounds.Width * ((column + 0.5f) / columns));
                if (!TryMapBackgroundPointToPixel(new SKPoint(x, y), image, contentBounds, out var pixelX, out var pixelY))
                    continue;

                var sampledColor = pixmap.GetPixelColor(pixelX, pixelY);
                if (sampledColor.Alpha == 0)
                    continue;

                red += sampledColor.Red;
                green += sampledColor.Green;
                blue += sampledColor.Blue;
                alpha += sampledColor.Alpha;
                sampleCount++;
            }
        }

        if (sampleCount == 0)
            return false;

        color = new SKColor(
            (byte)(red / sampleCount),
            (byte)(green / sampleCount),
            (byte)(blue / sampleCount),
            (byte)(alpha / sampleCount));
        return true;
    }

    private void DrawBackgroundImageTransition(SKCanvas canvas, SKRect bounds)
    {
        var fromImage = _backgroundTransitionFromImage;
        var toImage = _backgroundTransitionToImage;

        if (fromImage == null || toImage == null)
        {
            var activeImage = GetDisplayedBackgroundImage();
            if (activeImage != null)
                DrawBackgroundImage(canvas, activeImage, bounds, 255);
            return;
        }

        var progress = Math.Clamp((float)_backgroundImageTransitionAnimation.GetProgress(), 0f, 1f);
        switch (_backgroundImageTransitionEffect)
        {
            case BackgroundImageTransitionEffect.Fade:
                DrawBackgroundImage(canvas, fromImage, bounds, ToAlpha(1f - progress), bounds);
                DrawBackgroundImage(canvas, toImage, bounds, ToAlpha(progress), bounds);
                break;

            case BackgroundImageTransitionEffect.SlideHorizontal:
                DrawBackgroundImage(canvas, fromImage, OffsetRect(bounds, -_backgroundImageTransitionDirection * bounds.Width * progress, 0f), 255, bounds);
                DrawBackgroundImage(canvas, toImage, OffsetRect(bounds, _backgroundImageTransitionDirection * bounds.Width * (1f - progress), 0f), 255, bounds);
                break;

            case BackgroundImageTransitionEffect.SlideVertical:
                DrawBackgroundImage(canvas, fromImage, OffsetRect(bounds, 0f, -_backgroundImageTransitionDirection * bounds.Height * progress), 255, bounds);
                DrawBackgroundImage(canvas, toImage, OffsetRect(bounds, 0f, _backgroundImageTransitionDirection * bounds.Height * (1f - progress)), 255, bounds);
                break;

            case BackgroundImageTransitionEffect.ScaleFade:
                DrawBackgroundImage(canvas, fromImage, ScaleRect(bounds, 1f - (0.04f * progress)), ToAlpha(1f - progress), bounds);
                DrawBackgroundImage(canvas, toImage, ScaleRect(bounds, 1.04f - (0.04f * progress)), ToAlpha(progress), bounds);
                break;

            default:
                DrawBackgroundImage(canvas, toImage, bounds, 255);
                break;
        }
    }

    private void DrawBackgroundImage(SKCanvas canvas, SKImage image, SKRect bounds, byte alpha, SKRect? blurCacheBounds = null)
    {
        if (image == null || alpha == 0 || bounds.Width <= 0f || bounds.Height <= 0f)
            return;

        _backgroundImagePaint.Color = SKColors.White.WithAlpha(alpha);

        if (_backgroundImageBlurFilter != null)
        {
            lock (_backgroundBlurCacheSync)
            {
                var cachedBlurredImage = GetOrCreateBlurredBackgroundImage(image, blurCacheBounds ?? bounds);
                if (cachedBlurredImage != null)
                {
                    _backgroundImagePaint.ImageFilter = null;
                    canvas.DrawImage(cachedBlurredImage, bounds, _backgroundImagePaint);
                    return;
                }
            }
        }

        _backgroundImagePaint.ImageFilter = _backgroundImageBlurFilter;
        DrawBackgroundImageCore(canvas, image, bounds, _backgroundImagePaint);
        _backgroundImagePaint.ImageFilter = null;
    }

    private void DrawBackgroundImageCore(SKCanvas canvas, SKImage image, SKRect bounds, SKPaint paint)
    {
        if (image == null || bounds.Width <= 0f || bounds.Height <= 0f)
            return;

        switch (BackgroundImageLayout)
        {
            case ImageLayout.None:
                canvas.DrawImage(image, SKRect.Create(bounds.Left, bounds.Top, image.Width, image.Height), paint);
                break;

            case ImageLayout.Center:
                canvas.DrawImage(image, CreateCenteredImageRect(image, bounds), paint);
                break;

            case ImageLayout.Stretch:
                canvas.DrawImage(image, bounds, paint);
                break;

            case ImageLayout.Zoom:
                canvas.DrawImage(image, CreateZoomImageRect(image, bounds), paint);
                break;

            case ImageLayout.Tile:
                DrawTiledBackgroundImage(canvas, image, bounds, paint);
                break;

            default:
                canvas.DrawImage(image, bounds, paint);
                break;
        }
    }

    private void DrawTiledBackgroundImage(SKCanvas canvas, SKImage image, SKRect bounds, SKPaint paint)
    {
        var tileWidth = Math.Max(1f, image.Width);
        var tileHeight = Math.Max(1f, image.Height);

        for (var y = bounds.Top; y < bounds.Bottom; y += tileHeight)
        {
            for (var x = bounds.Left; x < bounds.Right; x += tileWidth)
            {
                canvas.DrawImage(image, SKRect.Create(x, y, tileWidth, tileHeight), paint);
            }
        }
    }

    private SKImage? GetOrCreateBlurredBackgroundImage(SKImage sourceImage, SKRect bounds)
    {
        if (_backgroundImageBlurFilter == null || bounds.Width <= 0f || bounds.Height <= 0f)
            return null;

        var width = Math.Max(1, (int)Math.Ceiling(bounds.Width));
        var height = Math.Max(1, (int)Math.Ceiling(bounds.Height));
        var blurAmountKey = Math.Max(0, (int)Math.Round(_backgroundImageBlurAmount * 100f));
        var layout = BackgroundImageLayout;

        for (var i = 0; i < _backgroundBlurCacheEntries.Count; i++)
        {
            var entry = _backgroundBlurCacheEntries[i];
            if (!entry.Matches(sourceImage, width, height, layout, blurAmountKey, _backgroundImageBlurMode))
                continue;

            if (i < _backgroundBlurCacheEntries.Count - 1)
            {
                _backgroundBlurCacheEntries.RemoveAt(i);
                _backgroundBlurCacheEntries.Add(entry);
            }

            return entry.BlurredImage;
        }

        var blurredImage = BuildBlurredBackgroundImage(sourceImage, width, height);
        if (blurredImage == null)
            return null;

        _backgroundBlurCacheEntries.Add(new BackgroundBlurCacheEntry(
            sourceImage,
            blurredImage,
            width,
            height,
            layout,
            blurAmountKey,
            _backgroundImageBlurMode));

        while (_backgroundBlurCacheEntries.Count > MaxBackgroundBlurCacheEntries)
        {
            _backgroundBlurCacheEntries[0].Dispose();
            _backgroundBlurCacheEntries.RemoveAt(0);
        }

        return blurredImage;
    }

    private SKImage? BuildBlurredBackgroundImage(SKImage sourceImage, int width, int height)
    {
        var downsample = GetBackgroundBlurDownsampleFactor(_backgroundImageBlurAmount);
        var surfaceWidth = Math.Max(1, (int)Math.Ceiling(width / (double)downsample));
        var surfaceHeight = Math.Max(1, (int)Math.Ceiling(height / (double)downsample));
        var info = new SKImageInfo(surfaceWidth, surfaceHeight, SKColorType.Bgra8888, SKAlphaType.Premul);
        using var surface = SKSurface.Create(info);
        if (surface == null)
            return null;

        var (sigmaX, sigmaY) = GetBackgroundImageBlurSigma(_backgroundImageBlurAmount, _backgroundImageBlurMode);
        sigmaX /= downsample;
        sigmaY /= downsample;

        using var paint = new SKPaint
        {
            IsAntialias = true,
            Color = SKColors.White
        };
        using var blurFilter = sigmaX > 0f || sigmaY > 0f
            ? SKImageFilter.CreateBlur(sigmaX, sigmaY, SKShaderTileMode.Clamp)
            : null;

        paint.ImageFilter = blurFilter;

        var canvas = surface.Canvas;
        canvas.Clear(SKColors.Transparent);
        var saveCount = canvas.Save();
        canvas.Scale(1f / downsample, 1f / downsample);
        DrawBackgroundImageCore(canvas, sourceImage, SKRect.Create(0f, 0f, width, height), paint);
        canvas.RestoreToCount(saveCount);
        paint.ImageFilter = null;
        canvas.Flush();

        return surface.Snapshot();
    }

    private static int GetBackgroundBlurDownsampleFactor(float blurAmount)
    {
        if (blurAmount <= 3f)
            return 1;

        if (blurAmount <= 8f)
            return 2;

        if (blurAmount <= 14f)
            return 3;

        if (blurAmount <= 22f)
            return 4;

        return 5;
    }

    private void ClearBackgroundBlurCache()
    {
        lock (_backgroundBlurCacheSync)
        {
            for (var i = 0; i < _backgroundBlurCacheEntries.Count; i++)
                _backgroundBlurCacheEntries[i].Dispose();

            _backgroundBlurCacheEntries.Clear();
        }
    }

    private static SKRect CreateCenteredImageRect(SKImage image, SKRect bounds)
    {
        var left = bounds.Left + ((bounds.Width - image.Width) * 0.5f);
        var top = bounds.Top + ((bounds.Height - image.Height) * 0.5f);
        return SKRect.Create(left, top, image.Width, image.Height);
    }

    private static SKRect CreateZoomImageRect(SKImage image, SKRect bounds)
    {
        if (image.Width <= 0 || image.Height <= 0 || bounds.Width <= 0f || bounds.Height <= 0f)
            return bounds;

        var scale = Math.Max(bounds.Width / image.Width, bounds.Height / image.Height);
        if (scale <= 0f || float.IsNaN(scale) || float.IsInfinity(scale))
            return bounds;

        var scaledWidth = image.Width * scale;
        var scaledHeight = image.Height * scale;
        var left = bounds.Left + ((bounds.Width - scaledWidth) * 0.5f);
        var top = bounds.Top + ((bounds.Height - scaledHeight) * 0.5f);
        return SKRect.Create(left, top, scaledWidth, scaledHeight);
    }

    private bool TryMapBackgroundPointToPixel(SKPoint point, SKImage image, SKRect bounds, out int pixelX, out int pixelY)
    {
        pixelX = 0;
        pixelY = 0;

        if (image.Width <= 0 || image.Height <= 0 || bounds.Width <= 0f || bounds.Height <= 0f)
            return false;

        switch (BackgroundImageLayout)
        {
            case ImageLayout.Tile:
                var localX = point.X - bounds.Left;
                var localY = point.Y - bounds.Top;
                pixelX = Mod((int)MathF.Floor(localX), image.Width);
                pixelY = Mod((int)MathF.Floor(localY), image.Height);
                return true;

            case ImageLayout.Stretch:
                return TryMapPointWithinRect(point, image, bounds, out pixelX, out pixelY);

            case ImageLayout.Center:
                return TryMapPointWithinRect(point, image, CreateCenteredImageRect(image, bounds), out pixelX, out pixelY);

            case ImageLayout.Zoom:
                return TryMapPointWithinRect(point, image, CreateZoomImageRect(image, bounds), out pixelX, out pixelY);

            case ImageLayout.None:
                return TryMapPointWithinRect(point, image, SKRect.Create(bounds.Left, bounds.Top, image.Width, image.Height), out pixelX, out pixelY);

            default:
                return TryMapPointWithinRect(point, image, bounds, out pixelX, out pixelY);
        }
    }

    private static bool TryMapPointWithinRect(SKPoint point, SKImage image, SKRect imageRect, out int pixelX, out int pixelY)
    {
        pixelX = 0;
        pixelY = 0;

        if (imageRect.Width <= 0f || imageRect.Height <= 0f || !imageRect.Contains(point))
            return false;

        var normalizedX = (point.X - imageRect.Left) / imageRect.Width;
        var normalizedY = (point.Y - imageRect.Top) / imageRect.Height;

        pixelX = Math.Clamp((int)MathF.Floor(normalizedX * image.Width), 0, image.Width - 1);
        pixelY = Math.Clamp((int)MathF.Floor(normalizedY * image.Height), 0, image.Height - 1);
        return true;
    }

    private static int Mod(int value, int modulus)
    {
        if (modulus <= 0)
            return 0;

        var result = value % modulus;
        return result < 0 ? result + modulus : result;
    }

    private static SKRect OffsetRect(SKRect rect, float dx, float dy)
    {
        rect.Offset(dx, dy);
        return rect;
    }

    private static SKRect ScaleRect(SKRect rect, float scale)
    {
        var width = rect.Width * scale;
        var height = rect.Height * scale;
        return SKRect.Create(rect.MidX - (width * 0.5f), rect.MidY - (height * 0.5f), width, height);
    }

    private static byte ToAlpha(float progress)
    {
        return (byte)Math.Clamp((int)Math.Round(progress * 255f), 0, 255);
    }

    private static bool AreSameBackgroundImages(BackgroundImageFrame[] left, BackgroundImageFrame[] right)
    {
        if (ReferenceEquals(left, right))
            return true;

        if (left.Length != right.Length)
            return false;

        for (var i = 0; i < left.Length; i++)
        {
            if (!AreSameBackgroundImage(left[i], right[i]))
                return false;
        }

        return true;
    }

    private void NotifyBackgroundImageMetadataChange(BackgroundImageFrame? previousFrame)
    {
        var currentFrame = GetDisplayedBackgroundImageFrame();
        if (AreSameBackgroundImageMetadata(previousFrame, currentFrame))
            return;

        OnBackgroundImageCaptionChanged(EventArgs.Empty);
    }

    private static bool AreSameBackgroundImage(BackgroundImageFrame? left, BackgroundImageFrame? right)
    {
        if (ReferenceEquals(left, right))
            return true;

        if (left == null || right == null)
            return false;

        return ReferenceEquals(left.Image, right.Image)
            && AreSameBackgroundImageMetadata(left, right);
    }

    private static bool AreSameBackgroundImageMetadata(BackgroundImageFrame? left, BackgroundImageFrame? right)
    {
        if (ReferenceEquals(left, right))
            return true;

        if (left == null && right == null)
            return true;

        var leftCaption = left?.Caption ?? BackgroundImageCaption.Empty;
        var rightCaption = right?.Caption ?? BackgroundImageCaption.Empty;
        var leftLayout = left?.CaptionLayout ?? ContentAlignment.MiddleLeft;
        var rightLayout = right?.CaptionLayout ?? ContentAlignment.MiddleLeft;
        var leftDesignMode = left?.CaptionDesignMode ?? BackgroundImageCaptionDesignMode.Overlay;
        var rightDesignMode = right?.CaptionDesignMode ?? BackgroundImageCaptionDesignMode.Overlay;

        return AreSameBackgroundImageMetadata(leftCaption, leftLayout, leftDesignMode, rightCaption, rightLayout, rightDesignMode);
    }

    private static bool AreSameBackgroundImageMetadata(BackgroundImageCaption leftCaption, ContentAlignment leftLayout,
        BackgroundImageCaptionDesignMode leftDesignMode, BackgroundImageCaption rightCaption, ContentAlignment rightLayout,
        BackgroundImageCaptionDesignMode rightDesignMode)
    {
        return string.Equals(leftCaption.Caption, rightCaption.Caption, StringComparison.Ordinal)
            && string.Equals(leftCaption.Summary, rightCaption.Summary, StringComparison.Ordinal)
            && leftLayout == rightLayout
            && leftDesignMode == rightDesignMode;
    }

    private static BackgroundImageFrame[] CopyBackgroundImages(BackgroundImageFrame[]? images)
    {
        if (images == null || images.Length == 0)
            return Array.Empty<BackgroundImageFrame>();

        var copy = new BackgroundImageFrame[images.Length];
        Array.Copy(images, copy, images.Length);
        return copy;
    }

    private sealed class BackgroundBlurCacheEntry : IDisposable
    {
        public BackgroundBlurCacheEntry(
            SKImage sourceImage,
            SKImage blurredImage,
            int width,
            int height,
            ImageLayout layout,
            int blurAmountKey,
            BackgroundImageBlurMode blurMode)
        {
            SourceImage = sourceImage;
            BlurredImage = blurredImage;
            Width = width;
            Height = height;
            Layout = layout;
            BlurAmountKey = blurAmountKey;
            BlurMode = blurMode;
        }

        public SKImage SourceImage { get; }
        public SKImage BlurredImage { get; }
        public int Width { get; }
        public int Height { get; }
        public ImageLayout Layout { get; }
        public int BlurAmountKey { get; }
        public BackgroundImageBlurMode BlurMode { get; }

        public bool Matches(
            SKImage sourceImage,
            int width,
            int height,
            ImageLayout layout,
            int blurAmountKey,
            BackgroundImageBlurMode blurMode)
        {
            return ReferenceEquals(SourceImage, sourceImage)
                && Width == width
                && Height == height
                && Layout == layout
                && BlurAmountKey == blurAmountKey
                && BlurMode == blurMode;
        }

        public void Dispose()
        {
            BlurredImage.Dispose();
        }
    }

}