using SkiaSharp;
using System;

namespace Orivy.Controls;

internal sealed class NotificationTray : ElementBase
{
	private float ScrollShadowHeight => 18f * ScaleFactor;
	private const float ScrollShadowThreshold = 0.5f;

	internal NotificationTray()
	{
		ZOrder = 9999;
		BackColor = SKColors.Transparent;
		Border = new Thickness(0);
		Radius = new Radius(0);
		AutoScroll = true;
		AutoScrollMargin = SKSize.Empty;
		AutoScrollMinSize = new SKSize((NotificationToast.BaseToastWidth * ScaleFactor) + (NotificationToast.BaseShadowPadding * ScaleFactor * 2f), 1);
	}

	internal float VerticalDisplayOffset => _vScrollBar?.Visible == true ? _vScrollBar.DisplayValue : 0f;

	protected override bool TryRenderChildContent(SKCanvas canvas)
	{
		RenderChildren(canvas);
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
		AutoScrollMinSize = new SKSize(totalWidth, totalContentHeight);
		PerformLayout();
	}

	internal void RefreshScrollMetrics()
	{
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
}
