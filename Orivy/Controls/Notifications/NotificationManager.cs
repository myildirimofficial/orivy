using Orivy;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Orivy.Controls;

internal sealed class NotificationManager : IDisposable
{
	private float MarginRight  => 4f * _owner.ScaleFactor;
	private float MarginLeft   => 4f * _owner.ScaleFactor;
	private float MarginTop    => 4f * _owner.ScaleFactor;
	private float MarginBottom => 4f * _owner.ScaleFactor;
	private float ToastSpacing       => 8f * _owner.ScaleFactor;
	private float ToastShadowPadding => NotificationToast.BaseShadowPadding * _owner.ScaleFactor;

	private static bool IsTopAlignment(ContentAlignment alignment) =>
		alignment is ContentAlignment.TopLeft or ContentAlignment.TopCenter or ContentAlignment.TopRight;

	private readonly WindowBase _owner;
	private readonly Dictionary<ContentAlignment, NotificationTray> _trays = new();
	private readonly Dictionary<ContentAlignment, List<NotificationToast>> _activeByAlignment = new();
	private bool _disposed;
	private NotificationToastPalette? _customPalette;

	public NotificationToastPalette? CustomPalette
	{
		get => _customPalette;
		set
		{
			if (ReferenceEquals(_customPalette, value))
				return;

			_customPalette = value;
			RefreshActiveToastThemes();
		}
	}

	public NotificationManager(WindowBase owner)
	{
		_owner = owner ?? throw new ArgumentNullException(nameof(owner));
		_owner.SizeChanged  += OnOwnerSizeChanged;
		_owner.DpiChanged   += OnOwnerDpiChanged;
		_owner.ControlAdded += OnOwnerControlAdded;
	}

	public NotificationHandle Show(string title, string message)
		=> Show(title, message, NotificationKind.Info, new NotificationOptions { DurationMs = 4000, ShowProgressBar = true });

	public NotificationHandle Show(string title, string message, NotificationKind kind, int durationMs = 4000)
		=> Show(title, message, kind, new NotificationOptions { DurationMs = durationMs, ShowProgressBar = durationMs > 0 });

	public NotificationHandle Show(
		string title,
		string message,
		NotificationKind kind,
		int durationMs,
		params NotificationAction[] actions)
		=> Show(title, message, kind, new NotificationOptions
		{
			DurationMs = durationMs,
			ShowProgressBar = durationMs > 0,
			Actions = actions ?? Array.Empty<NotificationAction>()
		});

	public NotificationHandle Show(
		string title,
		string message,
		NotificationKind kind,
		NotificationOptions? options)
		=> ShowCore(title, message, kind, options ?? new NotificationOptions(), null);

	public Task<string> ConfirmAsync(
		string title,
		string message,
		NotificationKind kind = NotificationKind.Info,
		int durationMs = 0,
		params string[] buttonLabels)
	{
		if (buttonLabels.Length == 0)
			buttonLabels = ["OK"];

		var tcs = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
		var actions = new NotificationAction[buttonLabels.Length];

		for (var i = 0; i < buttonLabels.Length; i++)
		{
			var label = buttonLabels[i];
			actions[i] = new NotificationAction(label, () => tcs.TrySetResult(label));
		}

		ShowCore(title, message, kind, new NotificationOptions
		{
			DurationMs = durationMs,
			ShowProgressBar = false,
			Actions = actions,
		}, () => tcs.TrySetResult(string.Empty));
		return tcs.Task;
	}

	public void DismissAll()
	{
		foreach (var list in _activeByAlignment.Values)
			for (var i = list.Count - 1; i >= 0; i--)
				list[i].BeginDismiss();
	}

	private NotificationHandle ShowCore(
		string title,
		string message,
		NotificationKind kind,
		NotificationOptions options,
		Action? onDismissWithoutAction)
	{
		if (_disposed)
			throw new ObjectDisposedException(nameof(NotificationManager));

		var alignment           = options.Position ?? ContentAlignment.BottomRight;
		var resolvedCustomPalette = options.CustomPalette ?? _customPalette;

		var tray = GetOrCreateTray(alignment);

		var toast = new NotificationToast(
			title,
			message,
			kind,
			options.DurationMs,
			options.ShowProgressBar,
			options.Progress,
			resolvedCustomPalette,
			options.Actions,
			alignment)
		{
			OnDismissWithoutAction = onDismissWithoutAction,
		};
		var handle = new NotificationHandle(toast);
		toast.AttachHandle(handle);

		toast.DismissCompleted += OnToastDismissed;

		tray.Controls.Add(toast);
		toast.RefreshForScale();

		if (!_activeByAlignment.TryGetValue(alignment, out var activeList))
		{
			activeList = new List<NotificationToast>();
			_activeByAlignment[alignment] = activeList;
		}
		activeList.Add(toast);

		tray.Controls.SetChildIndex(toast, tray.Controls.Count - 1);
		tray.UpdateZOrder();
		toast.BringToFront();

		SyncTray(alignment);
		tray.Invalidate();
		return handle;
	}

	private void OnToastDismissed(object? sender, EventArgs e)
	{
		if (sender is not NotificationToast dismissed)
			return;

		dismissed.DismissCompleted -= OnToastDismissed;

		var alignment = dismissed._alignment;
		if (!_activeByAlignment.TryGetValue(alignment, out var activeList))
			return;

		var index = activeList.IndexOf(dismissed);
		if (index < 0)
			return;

		activeList.RemoveAt(index);

		if (_trays.TryGetValue(alignment, out var tray))
			tray.Controls.Remove(dismissed);

		var deferredCompletion = dismissed.TakeDeferredCompletionAction();
		var activeCountBeforeDeferred = activeList.Count;
		dismissed.DetachHandle();
		dismissed.Dispose();

		deferredCompletion?.Invoke();

		if (_disposed)
			return;

		if (activeList.Count == activeCountBeforeDeferred)
			SyncTray(alignment);
	}

	private void OnOwnerDpiChanged(object? sender, EventArgs e)
	{
		if (_disposed)
			return;

		foreach (var list in _activeByAlignment.Values)
			foreach (var toast in list)
				toast.RefreshForScale();

		SyncAllTrays(true);
	}

	private void OnOwnerSizeChanged(object? sender, EventArgs e)
	{
		if (_disposed)
			return;

		SyncAllTrays();
	}

	private void OnOwnerControlAdded(object? sender, ElementEventArgs e)
	{
		if (_disposed)
			return;

		foreach (var tray in _trays.Values)
		{
			if (ReferenceEquals(e.Element, tray))
				return;
		}

		EnsureAllTraysTopMost();
	}

	private NotificationTray GetOrCreateTray(ContentAlignment alignment)
	{
		if (_trays.TryGetValue(alignment, out var existing))
			return existing;

		var tray = new NotificationTray
		{
			Visible  = false,
			Size     = new SKSize((NotificationToast.BaseToastWidth * _owner.ScaleFactor) + (ToastShadowPadding * 2f), 1),
			Location = SKPoint.Empty,
		};

		_trays[alignment] = tray;
		_owner.Controls.Add(tray);
		EnsureAllTraysTopMost();
		return tray;
	}

	private void SyncAllTrays(bool snap = false)
	{
		foreach (var alignment in _trays.Keys)
			SyncTray(alignment, snap);
	}

	private void SyncTray(ContentAlignment alignment, bool snap = false)
	{
		if (!_trays.TryGetValue(alignment, out var tray))
			return;

		if (!_activeByAlignment.TryGetValue(alignment, out var activeList) || activeList.Count == 0)
		{
			tray.Visible = false;
			return;
		}

		var topInset = 0f;
		if (_owner is Window window && window.ShowTitle)
			topInset = _owner.Padding.Top;
		var isTop = IsTopAlignment(alignment);

		Dictionary<NotificationToast, float>? previousScreenY = null;
		if (!snap)
			previousScreenY = CaptureScreenPositions(activeList, tray.Location.Y, tray.VerticalDisplayOffset);

		var totalHeight     = TotalContentHeight(activeList);
		var availableHeight = Math.Max(1f, _owner.Height - topInset - MarginTop - MarginBottom);
		tray.Resize(totalHeight, availableHeight);
		var trayHeight = tray.Height;
		var targetScrollOffset = ResolveTargetScrollOffset(totalHeight, trayHeight, isTop);

		float trayX = alignment switch
		{
			ContentAlignment.TopLeft   or ContentAlignment.BottomLeft   => MarginLeft,
			ContentAlignment.TopCenter or ContentAlignment.BottomCenter => (_owner.Width - tray.Width) / 2f,
			_                                                           => _owner.Width - MarginRight - tray.Width,
		};

		float trayY = isTop
			? topInset + MarginTop
			: topInset + MarginTop + (availableHeight - trayHeight);

		tray.Location = new SKPoint(trayX, trayY);

		EnsureAllTraysTopMost();
		ArrangeToasts(activeList, totalHeight, trayHeight, trayY, targetScrollOffset, snap, previousScreenY);
		tray.RefreshScrollMetrics();
		tray.SetVerticalScrollOffset(targetScrollOffset, immediate: true);

		tray.Visible = true;
		tray.Invalidate();
	}

	private void ArrangeToasts(
		List<NotificationToast> activeList,
		float totalHeight,
		float viewportHeight,
		float trayY,
		float targetScrollOffset,
		bool snap,
		Dictionary<NotificationToast, float>? previousScreenY)
	{
		var startY = Math.Max(0f, viewportHeight - totalHeight) + ToastShadowPadding;
		var y = startY;

		for (var i = 0; i < activeList.Count; i++)
		{
			var toast = activeList[i];
			if (!toast._hasBeenPlaced)
				toast.Place(ToastShadowPadding, y);
			else if (snap)
				toast.SnapTo(y);
			else
			{
				var oldScreenY = toast.GetVisualScreenY(trayY, targetScrollOffset);
				if (previousScreenY != null)
					previousScreenY.TryGetValue(toast, out oldScreenY);

				var newScreenY = trayY + y - targetScrollOffset;
				toast.MoveTo(y, oldScreenY, newScreenY);
			}

			y += toast.Height + ToastSpacing;
		}
	}

	private static Dictionary<NotificationToast, float> CaptureScreenPositions(
		List<NotificationToast> activeList,
		float trayY,
		float scrollOffset)
	{
		var positions = new Dictionary<NotificationToast, float>(activeList.Count);
		for (var i = 0; i < activeList.Count; i++)
		{
			var toast = activeList[i];
			if (!toast._hasBeenPlaced)
				continue;

			positions[toast] = toast.GetVisualScreenY(trayY, scrollOffset);
		}

		return positions;
	}

	private static float ResolveTargetScrollOffset(float totalHeight, float trayHeight, bool isTopAlignment)
	{
		if (totalHeight <= trayHeight)
			return 0f;

		return isTopAlignment ? 0f : totalHeight - trayHeight;
	}

	private void RefreshActiveToastThemes()
	{
		foreach (var list in _activeByAlignment.Values)
			for (var i = 0; i < list.Count; i++)
				list[i].RefreshTheme();
	}

	private void EnsureAllTraysTopMost()
	{
		foreach (var tray in _trays.Values)
		{
			if (_owner.Controls.Count > 0)
				_owner.Controls.SetChildIndex(tray, _owner.Controls.Count - 1);

			_owner.UpdateZOrder();
			tray.BringToFront();
		}
	}

	private float TotalContentHeight(List<NotificationToast> activeList)
	{
		var total = activeList.Count > 0 ? ToastShadowPadding * 2f : 0f;
		for (var i = 0; i < activeList.Count; i++)
		{
			if (i > 0)
				total += ToastSpacing;
			total += activeList[i].Height;
		}

		return total;
	}

	public void Dispose()
	{
		if (_disposed)
			return;

		_disposed = true;
		_owner.SizeChanged  -= OnOwnerSizeChanged;
		_owner.DpiChanged   -= OnOwnerDpiChanged;
		_owner.ControlAdded -= OnOwnerControlAdded;

		foreach (var (alignment, list) in _activeByAlignment)
		{
			for (var i = list.Count - 1; i >= 0; i--)
			{
				var toast = list[i];
				toast.DismissCompleted -= OnToastDismissed;
				if (_trays.TryGetValue(alignment, out var t))
					t.Controls.Remove(toast);
				toast.Dispose();
			}
		}

		_activeByAlignment.Clear();

		foreach (var tray in _trays.Values)
		{
			_owner.Controls.Remove(tray);
			tray.Dispose();
		}

		_trays.Clear();
	}
}
