using Orivy;
using Orivy.Animation;
using SkiaSharp;
using System;
using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;

namespace Orivy.Controls;

public abstract partial class ElementBase
{
    private readonly AnimationManager _remoteImageLoadingAnimation = new(true);
    private readonly SKPaint _remoteImageLoadingTrackPaint = new() { IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeCap = SKStrokeCap.Round };
    private readonly SKPaint _remoteImageLoadingArcPaint = new() { IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeCap = SKStrokeCap.Round };

    private CancellationTokenSource? _imageLoadCts;
    private CancellationTokenSource? _backgroundImageLoadCts;
    private long _imageLoadVersion;
    private long _backgroundImageLoadVersion;
    private bool _isImageLoading;
    private bool _isBackgroundImageLoading;

    [Browsable(false)]
    public bool IsImageLoading
    {
        get => _isImageLoading;
        private set
        {
            if (_isImageLoading == value)
                return;

            _isImageLoading = value;
            ImageLoadingChanged?.Invoke(this, EventArgs.Empty);
            UpdateRemoteImageLoadingAnimation();
            Invalidate();
        }
    }

    [Browsable(false)]
    public bool IsBackgroundImageLoading
    {
        get => _isBackgroundImageLoading;
        private set
        {
            if (_isBackgroundImageLoading == value)
                return;

            _isBackgroundImageLoading = value;
            BackgroundImageLoadingChanged?.Invoke(this, EventArgs.Empty);
            UpdateRemoteImageLoadingAnimation();
            Invalidate();
        }
    }

    [Browsable(false)]
    public bool HasImage => _image != null || IsImageLoading;

    public event EventHandler? ImageLoadingChanged;

    public event EventHandler? BackgroundImageLoadingChanged;

    public event EventHandler<Exception>? ImageLoadFailed;

    public event EventHandler<Exception>? BackgroundImageLoadFailed;

    public Task SetImageFromUrlAsync(string url, CancellationToken cancellationToken = default)
    {
        return SetImageFromUrlAsync(token => SKImageExtensions.FromUrlAsync(url, token), cancellationToken);
    }

    public Task SetImageFromUrlAsync(Uri uri, CancellationToken cancellationToken = default)
    {
        return SetImageFromUrlAsync(token => SKImageExtensions.FromUrlAsync(uri, token), cancellationToken);
    }

    public Task SetBackgroundImageFromUrlAsync(string url, CancellationToken cancellationToken = default)
    {
        return SetBackgroundImageFromUrlAsync(token => SKImageExtensions.FromUrlAsync(url, token), cancellationToken);
    }

    public Task SetBackgroundImageFromUrlAsync(Uri uri, CancellationToken cancellationToken = default)
    {
        return SetBackgroundImageFromUrlAsync(token => SKImageExtensions.FromUrlAsync(uri, token), cancellationToken);
    }

    public async Task SetImageFromUrlAsync(Task<SKImage> imageTask, CancellationToken cancellationToken = default)
    {
        if (imageTask == null)
            throw new ArgumentNullException(nameof(imageTask));

        await SetImageFromUrlAsync(_ => imageTask, cancellationToken).ConfigureAwait(false);
    }

    private async Task SetImageFromUrlAsync(Func<CancellationToken, Task<SKImage>> imageFactory, CancellationToken cancellationToken)
    {
        if (imageFactory == null)
            throw new ArgumentNullException(nameof(imageFactory));

        CancelImageUrlLoad();
        var version = Interlocked.Increment(ref _imageLoadVersion);
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _imageLoadCts = linkedCts;
        IsImageLoading = true;

        try
        {
            var image = await imageFactory(linkedCts.Token).WaitAsync(linkedCts.Token).ConfigureAwait(false);
            if (version != Interlocked.Read(ref _imageLoadVersion) || linkedCts.IsCancellationRequested)
            {
                image.Dispose();
                return;
            }

            Image = image;
        }
        catch (OperationCanceledException) when (linkedCts.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            if (version == Interlocked.Read(ref _imageLoadVersion))
                ImageLoadFailed?.Invoke(this, ex);
            throw;
        }
        finally
        {
            if (ReferenceEquals(_imageLoadCts, linkedCts))
                _imageLoadCts = null;

            if (version == Interlocked.Read(ref _imageLoadVersion))
                IsImageLoading = false;
        }
    }

    public async Task SetBackgroundImageFromUrlAsync(Task<SKImage> imageTask, CancellationToken cancellationToken = default)
    {
        if (imageTask == null)
            throw new ArgumentNullException(nameof(imageTask));

        await SetBackgroundImageFromUrlAsync(_ => imageTask, cancellationToken).ConfigureAwait(false);
    }

    private async Task SetBackgroundImageFromUrlAsync(Func<CancellationToken, Task<SKImage>> imageFactory, CancellationToken cancellationToken)
    {
        if (imageFactory == null)
            throw new ArgumentNullException(nameof(imageFactory));

        CancelBackgroundImageUrlLoad();
        var version = Interlocked.Increment(ref _backgroundImageLoadVersion);
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _backgroundImageLoadCts = linkedCts;
        IsBackgroundImageLoading = true;

        try
        {
            var image = await imageFactory(linkedCts.Token).WaitAsync(linkedCts.Token).ConfigureAwait(false);
            if (version != Interlocked.Read(ref _backgroundImageLoadVersion) || linkedCts.IsCancellationRequested)
            {
                image.Dispose();
                return;
            }

            BackgroundImage = image;
        }
        catch (OperationCanceledException) when (linkedCts.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            if (version == Interlocked.Read(ref _backgroundImageLoadVersion))
                BackgroundImageLoadFailed?.Invoke(this, ex);
            throw;
        }
        finally
        {
            if (ReferenceEquals(_backgroundImageLoadCts, linkedCts))
                _backgroundImageLoadCts = null;

            if (version == Interlocked.Read(ref _backgroundImageLoadVersion))
                IsBackgroundImageLoading = false;
        }
    }

    internal void RenderImageSlot(SKCanvas canvas, SKRect bounds)
    {
        if (_image != null)
            canvas.DrawImage(_image, bounds);

        if (IsImageLoading)
            RenderRemoteImageLoadingSpinner(canvas, bounds);
    }

    internal void RenderRemoteImageLoadingSpinner(SKCanvas canvas, SKRect bounds)
    {
        if (bounds.Width <= 0f || bounds.Height <= 0f)
            return;

        var scale = Math.Max(1f, ScaleFactor);
        var diameter = Math.Clamp(Math.Min(bounds.Width, bounds.Height) * 0.38f, 12f * scale, 26f * scale);
        var stroke = Math.Clamp(diameter * 0.12f, 1.6f * scale, 3f * scale);
        var rect = SKRect.Create(bounds.MidX - diameter / 2f, bounds.MidY - diameter / 2f, diameter, diameter);
        var progress = _remoteImageLoadingAnimation.Running
            ? (float)_remoteImageLoadingAnimation.GetProgress()
            : 0f;
        var startAngle = -90f + progress * 360f;

        _remoteImageLoadingTrackPaint.StrokeWidth = stroke;
        _remoteImageLoadingTrackPaint.Color = ColorScheme.Outline.WithAlpha(70);
        _remoteImageLoadingArcPaint.StrokeWidth = stroke;
        _remoteImageLoadingArcPaint.Color = ColorScheme.Primary.WithAlpha(220);

        canvas.DrawArc(rect, 0f, 360f, false, _remoteImageLoadingTrackPaint);
        canvas.DrawArc(rect, startAngle, 92f, false, _remoteImageLoadingArcPaint);
    }

    private void InitializeRemoteImageLoadingSystem()
    {
        _remoteImageLoadingAnimation.AnimationType = AnimationType.Linear;
        _remoteImageLoadingAnimation.InterruptAnimation = true;
        _remoteImageLoadingAnimation.Increment = 16d / 760d;
        _remoteImageLoadingAnimation.SecondaryIncrement = 16d / 760d;
        _remoteImageLoadingAnimation.OnAnimationProgress += HandleRemoteImageLoadingAnimationProgress;
        _remoteImageLoadingAnimation.OnAnimationFinished += HandleRemoteImageLoadingAnimationFinished;
    }

    private void DisposeRemoteImageLoadingSystem()
    {
        CancelImageUrlLoad();
        CancelBackgroundImageUrlLoad();
        _remoteImageLoadingAnimation.OnAnimationProgress -= HandleRemoteImageLoadingAnimationProgress;
        _remoteImageLoadingAnimation.OnAnimationFinished -= HandleRemoteImageLoadingAnimationFinished;
        _remoteImageLoadingAnimation.Dispose();
        _remoteImageLoadingTrackPaint.Dispose();
        _remoteImageLoadingArcPaint.Dispose();
    }

    private void HandleRemoteImageLoadingAnimationProgress(object _)
    {
        if (IsImageLoading || IsBackgroundImageLoading)
            Invalidate();
    }

    private void HandleRemoteImageLoadingAnimationFinished(object _)
    {
        if (!IsImageLoading && !IsBackgroundImageLoading)
        {
            Invalidate();
            return;
        }

        _remoteImageLoadingAnimation.SetProgress(0d);
        _remoteImageLoadingAnimation.StartNewAnimation(AnimationDirection.In);
    }

    private void UpdateRemoteImageLoadingAnimation()
    {
        if (IsImageLoading || IsBackgroundImageLoading)
        {
            if (!_remoteImageLoadingAnimation.Running)
            {
                _remoteImageLoadingAnimation.SetProgress(0d);
                _remoteImageLoadingAnimation.StartNewAnimation(AnimationDirection.In);
            }

            return;
        }

        _remoteImageLoadingAnimation.Stop();
    }

    private void CancelImageUrlLoad()
    {
        Interlocked.Increment(ref _imageLoadVersion);
        CancelRemoteImageLoad(ref _imageLoadCts);
        IsImageLoading = false;
    }

    private void CancelBackgroundImageUrlLoad()
    {
        Interlocked.Increment(ref _backgroundImageLoadVersion);
        CancelRemoteImageLoad(ref _backgroundImageLoadCts);
        IsBackgroundImageLoading = false;
    }

    private static void CancelRemoteImageLoad(ref CancellationTokenSource? cts)
    {
        var current = cts;
        cts = null;
        if (current == null)
            return;

        try
        {
            current.Cancel();
        }
        catch
        {
        }
    }
}
