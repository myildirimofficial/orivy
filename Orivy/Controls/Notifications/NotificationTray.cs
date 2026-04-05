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
		AutoScrollMargin = SKSize.Empty;
		AutoScrollMinSize = new SKSize(NotificationToast.ToastWidth, 1);
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

	internal void Resize(float totalContentHeight, float maxHeight)
	{
		var viewportHeight = Math.Max(1f, Math.Min(totalContentHeight, maxHeight));
		Size = new SKSize(NotificationToast.ToastWidth, viewportHeight);
		AutoScrollMinSize = new SKSize(NotificationToast.ToastWidth, totalContentHeight);
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
}
