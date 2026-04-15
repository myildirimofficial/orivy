using Orivy.Animation;
using SkiaSharp;
using System;

namespace Orivy.Controls;

internal sealed class NotificationTray : ElementBase
{
	private const float DialogScrimAnimationSpeed = 0.08f;
	private readonly AnimationManager _dialogScrimAnimation;
	private bool _dialogScrimVisible;
	private NotificationToastLayoutMode _layoutMode;
	private NotificationToastPresentationMode _presentationMode;
	private float ScrollShadowHeight => 18f * ScaleFactor;
	private const float ScrollShadowThreshold = 0.5f;

	internal NotificationTray()
	{
		_dialogScrimAnimation = new AnimationManager
		{
			Singular = true,
			InterruptAnimation = true,
			Increment = DialogScrimAnimationSpeed,
			SecondaryIncrement = DialogScrimAnimationSpeed,
			AnimationType = AnimationType.CubicEaseOut,
		};
		_dialogScrimAnimation.OnAnimationProgress += HandleDialogScrimAnimationProgress;
		_dialogScrimAnimation.OnAnimationFinished += HandleDialogScrimAnimationFinished;

		ZOrder = 9999;
		BackColor = SKColors.Transparent;
		Border = new Thickness(0);
		Radius = new Radius(0);
		AutoScroll = true;
		AutoScrollMargin = SKSize.Empty;
		AutoScrollMinSize = new SKSize((NotificationToast.BaseToastWidth * ScaleFactor) + (NotificationToast.BaseShadowPadding * ScaleFactor * 2f), 1);
	}

	internal bool ParticipatesInHitTesting
	{
		get
		{
			if (!Visible)
				return false;

			if (HasVisibleToastChildren())
				return true;

			if (_vScrollBar?.Visible == true || _hScrollBar?.Visible == true)
				return true;

			return _presentationMode == NotificationToastPresentationMode.Dialog &&
				(_dialogScrimVisible || _dialogScrimAnimation.IsAnimating() || _dialogScrimAnimation.GetProgress() > 0.001d);
		}
	}

	internal float VerticalDisplayOffset => _vScrollBar?.Visible == true ? _vScrollBar.DisplayValue : 0f;

	internal void SetLayoutMode(NotificationToastLayoutMode layoutMode)
	{
		if (_layoutMode == layoutMode)
			return;

		_layoutMode = layoutMode;
		AutoScroll = layoutMode != NotificationToastLayoutMode.Stack;
		if (layoutMode == NotificationToastLayoutMode.Stack)
			ResetScrollBars();
	}

	internal void SetPresentationMode(NotificationToastPresentationMode presentationMode)
	{
		if (_presentationMode == presentationMode)
			return;

		_presentationMode = presentationMode;
		if (_presentationMode == NotificationToastPresentationMode.Dialog)
			ResetScrollBars();

		if (_presentationMode != NotificationToastPresentationMode.Dialog)
			SetDialogScrimVisible(false, immediate: true);

		Invalidate();
	}

	internal void SetDialogScrimVisible(bool visible, bool immediate)
	{
		if (!immediate && _dialogScrimVisible == visible)
			return;

		_dialogScrimVisible = visible;
		if (visible)
			Visible = true;

		if (immediate)
		{
			_dialogScrimAnimation.SetProgress(visible ? 1d : 0d);
			if (!visible && !HasToastChildren())
				Visible = false;
			Invalidate();
			return;
		}

		_dialogScrimAnimation.StartNewAnimation(visible ? AnimationDirection.In : AnimationDirection.Out);
		Invalidate();
	}

	protected override bool TryRenderChildContent(SKCanvas canvas)
	{
		if (_presentationMode == NotificationToastPresentationMode.Dialog)
			DrawDialogScrim(canvas);

		RenderChildren(canvas);
		if (_layoutMode != NotificationToastLayoutMode.Stack && _presentationMode != NotificationToastPresentationMode.Dialog)
			DrawScrollShadows(canvas);
		return true;
	}

	internal void ScrollToTop()
	{
		if (_vScrollBar != null)
			_vScrollBar.Value = 0;
	}

	internal void ScrollToBottom()
	{
		if (_vScrollBar != null)
			_vScrollBar.Value = _vScrollBar.Maximum;
	}

	internal void SetVerticalScrollOffset(float value, bool immediate)
	{
		if (_layoutMode == NotificationToastLayoutMode.Stack || _presentationMode == NotificationToastPresentationMode.Dialog)
			return;

		if (_vScrollBar == null)
			return;

		if (immediate)
			_vScrollBar.SetValueImmediate(value);
		else
			_vScrollBar.Value = value;
	}

	internal void Resize(float totalContentHeight, float maxHeight)
	{
		var viewportHeight = Math.Max(1f, Math.Min(totalContentHeight, maxHeight));
		var totalWidth = (NotificationToast.BaseToastWidth * ScaleFactor) + (NotificationToast.BaseShadowPadding * ScaleFactor * 2f);
		Size = new SKSize(totalWidth, viewportHeight);
		AutoScrollMinSize = new SKSize(totalWidth, _layoutMode == NotificationToastLayoutMode.Stack ? viewportHeight : totalContentHeight);
		PerformLayout();
	}

	internal void ResizeForDialog(float width, float height)
	{
		Size = new SKSize(Math.Max(1f, width), Math.Max(1f, height));
		AutoScrollMinSize = Size;
		PerformLayout();
	}

	internal void RefreshScrollMetrics()
	{
		if (_layoutMode == NotificationToastLayoutMode.Stack || _presentationMode == NotificationToastPresentationMode.Dialog)
		{
			ResetScrollBars();
			return;
		}

		UpdateScrollBars();

		if (_vScrollBar != null && !_vScrollBar.Visible)
		{
			_vScrollBar.Maximum = 0;
			_vScrollBar.Value = 0;
		}

		if (_hScrollBar != null && !_hScrollBar.Visible)
		{
			_hScrollBar.Maximum = 0;
			_hScrollBar.Value = 0;
		}
	}

	private void ResetScrollBars()
	{
		if (_vScrollBar != null)
		{
			_vScrollBar.Visible = false;
			_vScrollBar.Maximum = 0;
			_vScrollBar.Value = 0;
			_vScrollBar.SetValueImmediate(0f);
		}

		if (_hScrollBar != null)
		{
			_hScrollBar.Visible = false;
			_hScrollBar.Maximum = 0;
			_hScrollBar.Value = 0;
			_hScrollBar.SetValueImmediate(0f);
		}
	}

	private void DrawDialogScrim(SKCanvas canvas)
	{
		var progress = Math.Clamp((float)_dialogScrimAnimation.GetProgress(), 0f, 1f);
		if (progress <= 0.001f)
			return;

		var easedProgress = 1f - MathF.Pow(1f - progress, 1.85f);

		var scrim = ColorScheme.IsDarkMode
			? new SKColor(2, 6, 23, 138)
			: new SKColor(15, 23, 42, 92);
		scrim = scrim.WithAlpha((byte)Math.Clamp(MathF.Round(scrim.Alpha * easedProgress), 0f, 255f));

		using var scrimPaint = new SKPaint
		{
			IsAntialias = true,
			Style = SKPaintStyle.Fill,
			Color = scrim,
		};

		canvas.DrawRect(new SKRect(0f, 0f, Width, Height), scrimPaint);

		var vignetteColor = ColorScheme.IsDarkMode
			? new SKColor(1, 3, 9, (byte)Math.Clamp(MathF.Round(86f * easedProgress), 0f, 255f))
			: new SKColor(15, 23, 42, (byte)Math.Clamp(MathF.Round(54f * easedProgress), 0f, 255f));
		var center = new SKPoint(Width * 0.5f, Height * 0.5f);
		var radius = MathF.Max(Width, Height) * 0.58f;
		using var vignettePaint = new SKPaint
		{
			IsAntialias = true,
			Style = SKPaintStyle.Fill,
			Shader = SKShader.CreateRadialGradient(
				center,
				radius,
				new[]
				{
					SKColors.Transparent,
					vignetteColor.WithAlpha((byte)(vignetteColor.Alpha * 0.55f)),
					vignetteColor
				},
				new[] { 0f, 0.68f, 1f },
				SKShaderTileMode.Clamp)
		};

		canvas.DrawRect(new SKRect(0f, 0f, Width, Height), vignettePaint);
	}

	private void HandleDialogScrimAnimationProgress(object _)
	{
		Invalidate();
	}

	private void HandleDialogScrimAnimationFinished(object _)
	{
		if (!_dialogScrimVisible && _presentationMode == NotificationToastPresentationMode.Dialog && !HasToastChildren())
			Visible = false;

		Invalidate();
	}

	private bool HasToastChildren()
	{
		for (var i = 0; i < Controls.Count; i++)
		{
			if (Controls[i] is NotificationToast)
				return true;
		}

		return false;
	}

	private bool HasVisibleToastChildren()
	{
		for (var i = 0; i < Controls.Count; i++)
		{
			if (Controls[i] is NotificationToast toast && toast.Visible)
				return true;
		}

		return false;
	}

	private void DrawScrollShadows(SKCanvas canvas)
	{
		if (_vScrollBar == null || !_vScrollBar.Visible)
			return;

		var displayValue = _vScrollBar.DisplayValue;
		var remainingScroll = _vScrollBar.Maximum - displayValue;

		if (displayValue > ScrollShadowThreshold)
			DrawScrollShadow(canvas, isTopShadow: true);

		if (remainingScroll > ScrollShadowThreshold)
			DrawScrollShadow(canvas, isTopShadow: false);
	}

	private void DrawScrollShadow(SKCanvas canvas, bool isTopShadow)
	{
		var shadowHeight = MathF.Min(ScrollShadowHeight, Height * 0.24f);
		if (shadowHeight <= 0f)
			return;

		var top = isTopShadow ? 0f : MathF.Max(0f, Height - shadowHeight);
		var bottom = isTopShadow ? shadowHeight : Height;
		var shadowRect = new SKRect(0f, top, Width, bottom);
		var shadowColor = ColorScheme.ShadowColor.WithAlpha(ColorScheme.IsDarkMode ? (byte)74 : (byte)52);

		var startPoint = isTopShadow
			? new SKPoint(0f, shadowRect.Top)
			: new SKPoint(0f, shadowRect.Bottom);

		var endPoint = isTopShadow
			? new SKPoint(0f, shadowRect.Bottom)
			: new SKPoint(0f, shadowRect.Top);

		using var paint = new SKPaint
		{
			IsAntialias = true,
			Style = SKPaintStyle.Fill,
			Shader = SKShader.CreateLinearGradient(
				startPoint,
				endPoint,
				new[]
				{
					shadowColor,
					shadowColor.WithAlpha((byte)(shadowColor.Alpha * 0.55f)),
					shadowColor.WithAlpha(0)
				},
				new[] { 0f, 0.34f, 1f },
				SKShaderTileMode.Clamp)
		};

		canvas.DrawRect(shadowRect, paint);
	}

	protected override void Dispose(bool disposing)
	{
		if (disposing)
		{
			_dialogScrimAnimation.OnAnimationProgress -= HandleDialogScrimAnimationProgress;
			_dialogScrimAnimation.OnAnimationFinished -= HandleDialogScrimAnimationFinished;
			_dialogScrimAnimation.Dispose();
		}

		base.Dispose(disposing);
	}
}
