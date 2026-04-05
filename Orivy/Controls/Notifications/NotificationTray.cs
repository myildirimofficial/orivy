using SkiaSharp;
using System;

namespace Orivy.Controls;

internal sealed class NotificationTray : ElementBase
{
	internal NotificationTray()
	{
		ZOrder = 9999;
		BackColor = SKColors.Transparent;
		Border = new Thickness(0);
		Radius = new Radius(0);
		AutoScroll = true;
		AutoScrollMargin = new SKSize(0, 12);
		AutoScrollMinSize = new SKSize(NotificationToast.ToastWidth, 1);
	}

	internal void ScrollToTop()
	{
		if (_vScrollBar != null)
			_vScrollBar.Value = 0;
	}

	internal void Resize(float totalContentHeight, float maxHeight)
	{
		var clampedHeight = Math.Min(totalContentHeight, Math.Max(1f, maxHeight));
		Size = new SKSize(NotificationToast.ToastWidth, clampedHeight);
		AutoScrollMinSize = new SKSize(NotificationToast.ToastWidth, totalContentHeight);
		PerformLayout();
	}
}
