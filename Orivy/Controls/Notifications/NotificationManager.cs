using Orivy;
using Orivy.Animation;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Orivy.Controls;

internal sealed class NotificationManager : IDisposable
{
	private const int MaxVisibleStackDepth = 3;
	private const int ExpandedVisibleStackDepth = 4;
	private const int MouseWheelStepDelta = 120;
	private const double MaxStackWheelMomentum = 3.0d;
	private const double StackWheelStepIncrement = 0.135d;
	private const double StackWheelMomentumDecayFactor = 0.70d;
	private const double StackWheelMomentumBias = 0.75d;
	private const double StackWheelMomentumStopThreshold = 0.25d;

	private float MarginRight  => 4f * _owner.ScaleFactor;
	private float MarginLeft   => 4f * _owner.ScaleFactor;
	private float MarginTop    => 4f * _owner.ScaleFactor;
	private float MarginBottom => 4f * _owner.ScaleFactor;
	private float ToastSpacing       => 8f * _owner.ScaleFactor;
	private float ToastShadowPadding => NotificationToast.BaseShadowPadding * _owner.ScaleFactor;

	private static bool IsTopAlignment(ContentAlignment alignment) =>
		alignment is ContentAlignment.TopLeft or ContentAlignment.TopCenter or ContentAlignment.TopRight;

	private static bool IsMiddleAlignment(ContentAlignment alignment) =>
		alignment is ContentAlignment.MiddleLeft or ContentAlignment.MiddleCenter or ContentAlignment.MiddleRight;

	private static bool IsBottomAlignment(ContentAlignment alignment) =>
		alignment is ContentAlignment.BottomLeft or ContentAlignment.BottomCenter or ContentAlignment.BottomRight;

	private readonly WindowBase _owner;
	private readonly Dictionary<(ContentAlignment Alignment, NotificationToastLayoutMode LayoutMode, NotificationToastPresentationMode PresentationMode), NotificationTray> _trays = new();
	private readonly Dictionary<(ContentAlignment Alignment, NotificationToastLayoutMode LayoutMode, NotificationToastPresentationMode PresentationMode), List<NotificationToast>> _activeByAlignment = new();
	private readonly HashSet<(ContentAlignment Alignment, NotificationToastLayoutMode LayoutMode, NotificationToastPresentationMode PresentationMode)> _expandedStacks = new();
	private readonly Dictionary<(ContentAlignment Alignment, NotificationToastLayoutMode LayoutMode, NotificationToastPresentationMode PresentationMode), int> _stackFrontIndices = new();
	private readonly Dictionary<(ContentAlignment Alignment, NotificationToastLayoutMode LayoutMode, NotificationToastPresentationMode PresentationMode), int> _stackWheelRemainders = new();
	private readonly Dictionary<(ContentAlignment Alignment, NotificationToastLayoutMode LayoutMode, NotificationToastPresentationMode PresentationMode), double> _stackWheelMomentum = new();
	private readonly Dictionary<(ContentAlignment Alignment, NotificationToastLayoutMode LayoutMode, NotificationToastPresentationMode PresentationMode), AnimationManager> _stackWheelDrivers = new();
	private bool _disposed;
	private NotificationToastPalette? _customPalette;
	private NotificationToastLayoutMode _defaultLayoutMode = NotificationToastLayoutMode.List;

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

	public NotificationToastLayoutMode DefaultLayoutMode
	{
		get => _defaultLayoutMode;
		set
		{
			if (_defaultLayoutMode == value)
				return;

			_defaultLayoutMode = value;
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

	public NotificationHandle ShowDialog(
		string title,
		string message,
		NotificationKind kind,
		NotificationOptions? options = null)
	{
		var resolvedOptions = options ?? new NotificationOptions();
		resolvedOptions.PresentationMode = NotificationToastPresentationMode.Dialog;
		resolvedOptions.Position = ContentAlignment.MiddleCenter;
		resolvedOptions.LayoutMode = NotificationToastLayoutMode.List;
		if (resolvedOptions.DurationMs == 4000)
			resolvedOptions.DurationMs = 0;
		resolvedOptions.ShowProgressBar = resolvedOptions.DurationMs > 0 && resolvedOptions.ShowProgressBar;
		return ShowCore(title, message, kind, resolvedOptions, null);
	}

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
			LayoutMode = NotificationToastLayoutMode.List,
			Position = ContentAlignment.MiddleCenter,
			PresentationMode = NotificationToastPresentationMode.Dialog,
		}, () => tcs.TrySetResult(string.Empty));
		return tcs.Task;
	}

	public void DismissAll()
	{
		foreach (var list in _activeByAlignment.Values)
			for (var i = list.Count - 1; i >= 0; i--)
				list[i].BeginDismiss();
	}

	internal bool TryDismissActiveDialog()
	{
		if (_disposed)
			return false;

		NotificationToast? activeDialog = null;
		foreach (var entry in _activeByAlignment)
		{
			if (entry.Key.PresentationMode != NotificationToastPresentationMode.Dialog || entry.Value.Count == 0)
				continue;

			activeDialog = entry.Value[entry.Value.Count - 1];
		}

		if (activeDialog == null)
			return false;

		activeDialog.BeginDismiss();
		return true;
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

		var presentationMode = options.PresentationMode;
		var alignment = options.Position ?? ContentAlignment.BottomRight;
		var layoutMode = options.LayoutMode ?? _defaultLayoutMode;
		if (presentationMode == NotificationToastPresentationMode.Dialog)
		{
			alignment = ContentAlignment.MiddleCenter;
			layoutMode = NotificationToastLayoutMode.List;
		}

		var resolvedCustomPalette = options.CustomPalette ?? _customPalette;
		var key = (alignment, layoutMode, presentationMode);

		var tray = GetOrCreateTray(key);

		var toast = new NotificationToast(
			title,
			message,
			kind,
			options.DurationMs,
			options.ShowProgressBar,
			options.Progress,
			resolvedCustomPalette,
			options.Actions,
			alignment,
			layoutMode,
			presentationMode)
		{
			OnDismissWithoutAction = onDismissWithoutAction,
		};
		toast.StackBodyClickRequested += OnStackBodyClickRequested;
		toast.StackWheelRequested += OnStackWheelRequested;
		var handle = new NotificationHandle(toast);
		toast.AttachHandle(handle);

		toast.DismissCompleted += OnToastDismissed;

		tray.Controls.Add(toast);
		toast.RefreshForScale();

		if (!_activeByAlignment.TryGetValue(key, out var activeList))
		{
			activeList = new List<NotificationToast>();
			_activeByAlignment[key] = activeList;
		}
		activeList.Add(toast);
		if (layoutMode == NotificationToastLayoutMode.Stack)
			SetStackFrontIndex(key, activeList.Count, activeList.Count - 1);

		tray.Controls.SetChildIndex(toast, tray.Controls.Count - 1);
		tray.UpdateZOrder();
		toast.BringToFront();

		SyncTray(key);
		tray.Invalidate();
		return handle;
	}

	private void OnToastDismissed(object? sender, EventArgs e)
	{
		if (sender is not NotificationToast dismissed)
			return;

		dismissed.DismissCompleted -= OnToastDismissed;
		dismissed.StackBodyClickRequested -= OnStackBodyClickRequested;
		dismissed.StackWheelRequested -= OnStackWheelRequested;

		var key = (dismissed._alignment, dismissed._layoutMode, dismissed._presentationMode);
		if (!_activeByAlignment.TryGetValue(key, out var activeList))
			return;

		var previousFrontIndex = dismissed._layoutMode == NotificationToastLayoutMode.Stack
			? ResolveStackFrontIndex(key, activeList.Count)
			: -1;
		var index = activeList.IndexOf(dismissed);
		if (index < 0)
			return;

		activeList.RemoveAt(index);

		if (_trays.TryGetValue(key, out var tray))
			tray.Controls.Remove(dismissed);

		var deferredCompletion = dismissed.TakeDeferredCompletionAction();
		var activeCountBeforeDeferred = activeList.Count;
		dismissed.DetachHandle();
		dismissed.Dispose();

		deferredCompletion?.Invoke();

		if (_disposed)
			return;

		if (activeList.Count == 0)
		{
			_expandedStacks.Remove(key);
			_stackFrontIndices.Remove(key);
			ClearStackWheelState(key);
		}
		else if (dismissed._layoutMode == NotificationToastLayoutMode.Stack)
		{
			if (index < previousFrontIndex)
				SetStackFrontIndex(key, activeList.Count, previousFrontIndex - 1);
			else if (index == previousFrontIndex)
				SetStackFrontIndex(key, activeList.Count, Math.Min(previousFrontIndex, activeList.Count - 1));
		}

		if (activeList.Count == activeCountBeforeDeferred)
			SyncTray(key);
	}

	private void OnStackBodyClickRequested(NotificationToast toast)
	{
		if (_disposed || toast._layoutMode != NotificationToastLayoutMode.Stack)
			return;

		var key = (toast._alignment, toast._layoutMode, toast._presentationMode);
		if (!_activeByAlignment.TryGetValue(key, out var activeList) || activeList.Count <= 1)
		{
			_stackFrontIndices.Remove(key);
			ClearStackWheelState(key);
			return;
		}

		if (!_expandedStacks.Contains(key))
		{
			ClearStackWheelState(key);
			_expandedStacks.Add(key);
			SyncTray(key);
			return;
		}

		var frontToast = activeList[ResolveStackFrontIndex(key, activeList.Count)];
		if (ReferenceEquals(frontToast, toast))
		{
			if (activeList.Count <= 1)
			{
				_stackFrontIndices.Remove(key);
				ClearStackWheelState(key);
				_expandedStacks.Remove(key);
				SyncTray(key);
				return;
			}

			ClearStackWheelState(key);
			ShiftStackFrontIndex(key, activeList.Count, -1);
			SyncTray(key);
			return;
		}

		var index = activeList.IndexOf(toast);
		if (index < 0)
			return;

		SetStackFrontIndex(key, activeList.Count, index);
		ClearStackWheelState(key);
		SyncTray(key);
	}

	private void OnStackWheelRequested(NotificationToast toast, int delta)
	{
		if (_disposed || toast._layoutMode != NotificationToastLayoutMode.Stack || delta == 0)
			return;

		var key = (toast._alignment, toast._layoutMode, toast._presentationMode);
		if (!_activeByAlignment.TryGetValue(key, out var activeList) || activeList.Count <= 1)
		{
			_stackFrontIndices.Remove(key);
			ClearStackWheelState(key);
			return;
		}

		var accumulatedDelta = _stackWheelRemainders.TryGetValue(key, out var remainder) ? remainder : 0;
		if (accumulatedDelta != 0 && Math.Sign(accumulatedDelta) != Math.Sign(delta))
			accumulatedDelta = 0;

		accumulatedDelta += delta;
		var queuedStepCount = accumulatedDelta / MouseWheelStepDelta;
		if (queuedStepCount == 0)
		{
			_stackWheelRemainders[key] = accumulatedDelta;
			return;
		}

		accumulatedDelta -= queuedStepCount * MouseWheelStepDelta;

		if (accumulatedDelta == 0)
			_stackWheelRemainders.Remove(key);
		else
			_stackWheelRemainders[key] = accumulatedDelta;

		AccumulateStackWheelMomentum(key, activeList.Count, -queuedStepCount);
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

	private NotificationTray GetOrCreateTray((ContentAlignment Alignment, NotificationToastLayoutMode LayoutMode, NotificationToastPresentationMode PresentationMode) key)
	{
		if (_trays.TryGetValue(key, out var existing))
		{
			existing.SetLayoutMode(key.LayoutMode);
			existing.SetPresentationMode(key.PresentationMode);
			return existing;
		}

		var tray = new NotificationTray
		{
			Visible  = false,
			Size     = new SKSize((NotificationToast.BaseToastWidth * _owner.ScaleFactor) + (ToastShadowPadding * 2f), 1),
			Location = SKPoint.Empty,
		};
		tray.SetLayoutMode(key.LayoutMode);
		tray.SetPresentationMode(key.PresentationMode);

		_trays[key] = tray;
		_owner.Controls.Add(tray);
		EnsureAllTraysTopMost();
		return tray;
	}

	private void SyncAllTrays(bool snap = false)
	{
		foreach (var key in _trays.Keys)
			SyncTray(key, snap);
	}

	private void SyncTray((ContentAlignment Alignment, NotificationToastLayoutMode LayoutMode, NotificationToastPresentationMode PresentationMode) key, bool snap = false)
	{
		if (!_trays.TryGetValue(key, out var tray))
			return;

		tray.SetLayoutMode(key.LayoutMode);
		tray.SetPresentationMode(key.PresentationMode);

		if (!_activeByAlignment.TryGetValue(key, out var activeList) || activeList.Count == 0)
		{
			var animateDialogFadeOut = key.PresentationMode == NotificationToastPresentationMode.Dialog && !snap;
			tray.SetDialogScrimVisible(false, immediate: !animateDialogFadeOut);
			tray.Visible = animateDialogFadeOut;
			_expandedStacks.Remove(key);
			_stackFrontIndices.Remove(key);
			ClearStackWheelState(key);
			return;
		}

		var alignment = key.Alignment;
		var isStack = key.LayoutMode == NotificationToastLayoutMode.Stack;
		var isDialog = key.PresentationMode == NotificationToastPresentationMode.Dialog;
		if (!isStack)
		{
			_stackFrontIndices.Remove(key);
			ClearStackWheelState(key);
		}

		var isExpandedStack = isStack && _expandedStacks.Contains(key);
		var stackFrontIndex = isStack ? ResolveStackFrontIndex(key, activeList.Count) : -1;
		var topInset = 0f;
		if (_owner is Window window && window.ShowTitle)
			topInset = _owner.Padding.Top;
		var isTop = IsTopAlignment(alignment);
		var isMiddle = IsMiddleAlignment(alignment);

		Dictionary<NotificationToast, float>? previousScreenY = null;
		if (!snap)
			previousScreenY = CaptureScreenPositions(activeList, tray.Location.Y, tray.VerticalDisplayOffset);

		var availableHeight = Math.Max(1f, _owner.Height - topInset - MarginTop - MarginBottom);

		var totalHeight = isDialog
			? availableHeight
			: isStack
				? TotalStackHeight(activeList, stackFrontIndex, isExpandedStack)
				: TotalContentHeight(activeList);
		if (isDialog)
			tray.ResizeForDialog(_owner.Width, availableHeight + MarginTop + MarginBottom);
		else
			tray.Resize(totalHeight, availableHeight);
		var trayHeight = tray.Height;
		var targetScrollOffset = isStack || isDialog ? 0f : ResolveTargetScrollOffset(totalHeight, trayHeight, alignment);

		float trayX = alignment switch
		{
			ContentAlignment.TopLeft   or ContentAlignment.BottomLeft   => MarginLeft,
			ContentAlignment.MiddleLeft                              => MarginLeft,
			ContentAlignment.TopCenter or ContentAlignment.BottomCenter => (_owner.Width - tray.Width) / 2f,
			ContentAlignment.MiddleCenter                              => (_owner.Width - tray.Width) / 2f,
			_                                                           => _owner.Width - MarginRight - tray.Width,
		};

		float trayY;
		if (isDialog)
		{
			trayX = 0f;
			trayY = topInset;
		}
		else if (isStack)
		{
			var frontToast = activeList[stackFrontIndex];
			var stackExtent = GetStackExtent(activeList, stackFrontIndex, isExpandedStack);
			var frontLocalY = IsBottomAlignment(alignment) ? ToastShadowPadding + stackExtent : ToastShadowPadding;
			var desiredFrontScreenY = ResolveStackFrontScreenY(frontToast.Height, availableHeight, topInset, alignment);
			trayY = desiredFrontScreenY - frontLocalY;
		}
		else if (isTop)
		{
			trayY = topInset + MarginTop;
		}
		else if (isMiddle)
		{
			trayY = topInset + MarginTop + ((availableHeight - trayHeight) / 2f);
		}
		else
		{
			trayY = topInset + MarginTop + (availableHeight - trayHeight);
		}

		tray.Location = new SKPoint(trayX, trayY);
		if (isDialog)
			tray.Visible = true;
		tray.SetDialogScrimVisible(isDialog, immediate: snap);
		if (isStack)
			SyncStackVisualOrder(key, activeList, stackFrontIndex);

		EnsureAllTraysTopMost();
		if (isDialog)
			ArrangeDialogToasts(activeList, tray, snap, previousScreenY);
		else if (isStack)
			ArrangeStackToasts(activeList, stackFrontIndex, alignment, trayY, snap, previousScreenY, isExpandedStack);
		else
			ArrangeToasts(activeList, totalHeight, trayHeight, trayY, targetScrollOffset, snap, previousScreenY);
		tray.RefreshScrollMetrics();
		tray.SetVerticalScrollOffset(targetScrollOffset, immediate: true);

		tray.Visible = true;
		tray.Invalidate();
	}

	private void ArrangeStackToasts(
		List<NotificationToast> activeList,
		int frontIndex,
		ContentAlignment alignment,
		float trayY,
		bool snap,
		Dictionary<NotificationToast, float>? previousScreenY,
		bool expanded)
	{
		var stackExtent = GetStackExtent(activeList, frontIndex, expanded);
		var frontLocalY = IsBottomAlignment(alignment) ? ToastShadowPadding + stackExtent : ToastShadowPadding;

		for (var displayIndex = 0; displayIndex < activeList.Count; displayIndex++)
		{
			var actualIndex = GetStackDisplayIndex(frontIndex, activeList.Count, displayIndex);
			var toast = activeList[actualIndex];
			var depth = (activeList.Count - 1) - displayIndex;
			var clampedDepth = Math.Min(depth, MaxVisibleStackDepth);
			var stackOffset = expanded ? GetExpandedStackOffset(activeList, frontIndex, depth) : GetStackOffset(clampedDepth);
			var y = IsBottomAlignment(alignment)
				? frontLocalY - stackOffset
				: frontLocalY + stackOffset;

			toast.UpdateStackPresentation(depth);

			if (!toast._hasBeenPlaced)
				toast.Place(ToastShadowPadding, y);
			else if (snap)
				toast.SnapTo(y);
			else
			{
				var oldScreenY = toast.GetVisualScreenY(trayY, 0f);
				if (previousScreenY != null)
					previousScreenY.TryGetValue(toast, out oldScreenY);

				var newScreenY = trayY + y;
				toast.MoveTo(y, oldScreenY, newScreenY);
			}
		}
	}

	private void ShiftStackFrontIndex(
		(ContentAlignment Alignment, NotificationToastLayoutMode LayoutMode, NotificationToastPresentationMode PresentationMode) key,
		int itemCount,
		int offset)
	{
		if (itemCount <= 1 || offset == 0)
			return;

		SetStackFrontIndex(key, itemCount, ResolveStackFrontIndex(key, itemCount) + offset);
	}

	private void SyncStackVisualOrder(
		(ContentAlignment Alignment, NotificationToastLayoutMode LayoutMode, NotificationToastPresentationMode PresentationMode) key,
		List<NotificationToast> activeList,
		int frontIndex)
	{
		if (!_trays.TryGetValue(key, out var tray))
			return;

		for (var displayIndex = 0; displayIndex < activeList.Count; displayIndex++)
		{
			var actualIndex = GetStackDisplayIndex(frontIndex, activeList.Count, displayIndex);
			tray.Controls.SetChildIndex(activeList[actualIndex], displayIndex);
		}

		tray.UpdateZOrder();
		activeList[frontIndex].BringToFront();
	}

	private void ArrangeDialogToasts(
		List<NotificationToast> activeList,
		NotificationTray tray,
		bool snap,
		Dictionary<NotificationToast, float>? previousScreenY)
	{
		for (var i = 0; i < activeList.Count; i++)
		{
			var toast = activeList[i];
			var x = Math.Max(ToastShadowPadding, (tray.Width - toast.Width) / 2f);
			var y = Math.Max(ToastShadowPadding, (tray.Height - toast.Height) / 2f);

			toast.UpdateStackPresentation(0);

			if (!toast._hasBeenPlaced)
				toast.Place(x, y);
			else
			{
				toast.SetTargetX(x);
				if (snap)
					toast.SnapTo(y);
				else
				{
					var oldScreenY = toast.GetVisualScreenY(tray.Location.Y, 0f);
					if (previousScreenY != null)
						previousScreenY.TryGetValue(toast, out oldScreenY);

					var newScreenY = tray.Location.Y + y;
					toast.MoveTo(y, oldScreenY, newScreenY);
				}
			}
		}
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

	private static float ResolveTargetScrollOffset(float totalHeight, float trayHeight, ContentAlignment alignment)
	{
		if (totalHeight <= trayHeight)
			return 0f;

		if (IsTopAlignment(alignment))
			return 0f;

		if (IsMiddleAlignment(alignment))
			return (totalHeight - trayHeight) / 2f;

		return totalHeight - trayHeight;
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

	private float TotalStackHeight(List<NotificationToast> activeList, int frontIndex, bool expanded)
	{
		if (activeList.Count == 0)
			return 0f;

		var frontToast = activeList[frontIndex];
		return (ToastShadowPadding * 2f) + frontToast.Height + GetStackExtent(activeList, frontIndex, expanded);
	}

	private float GetStackExtent(List<NotificationToast> activeList, int frontIndex, bool expanded)
	{
		if (activeList.Count <= 1)
			return 0f;

		if (expanded)
			return GetExpandedStackOffset(activeList, frontIndex, activeList.Count - 1);

		var visibleDepth = Math.Min(activeList.Count - 1, MaxVisibleStackDepth);
		return GetStackOffset(visibleDepth);
	}

	private float GetExpandedStackOffset(List<NotificationToast> activeList, int frontIndex, int depth)
	{
		if (depth <= 0)
			return 0f;

		var offset = 0f;
		var visibleDepth = Math.Min(depth, ExpandedVisibleStackDepth);
		for (var visibleIndex = 0; visibleIndex < visibleDepth; visibleIndex++)
		{
			var toast = GetStackToastByDepth(activeList, frontIndex, visibleIndex);
			offset += Math.Max(40f * _owner.ScaleFactor, toast.Height - (34f * _owner.ScaleFactor));
		}

		return offset;
	}

	private int ResolveStackFrontIndex(
		(ContentAlignment Alignment, NotificationToastLayoutMode LayoutMode, NotificationToastPresentationMode PresentationMode) key,
		int itemCount)
	{
		if (itemCount <= 0)
			return -1;

		if (_stackFrontIndices.TryGetValue(key, out var storedIndex))
			return NormalizeStackIndex(storedIndex, itemCount);

		return itemCount - 1;
	}

	private void SetStackFrontIndex(
		(ContentAlignment Alignment, NotificationToastLayoutMode LayoutMode, NotificationToastPresentationMode PresentationMode) key,
		int itemCount,
		int index)
	{
		if (itemCount <= 0)
		{
			_stackFrontIndices.Remove(key);
			return;
		}

		_stackFrontIndices[key] = NormalizeStackIndex(index, itemCount);
	}

	private static int NormalizeStackIndex(int index, int itemCount)
	{
		var normalized = index % itemCount;
		return normalized < 0 ? normalized + itemCount : normalized;
	}

	private static int GetStackDisplayIndex(int frontIndex, int itemCount, int displayIndex)
	{
		return NormalizeStackIndex(frontIndex - (itemCount - 1 - displayIndex), itemCount);
	}

	private static NotificationToast GetStackToastByDepth(List<NotificationToast> activeList, int frontIndex, int depth)
	{
		var actualIndex = NormalizeStackIndex(frontIndex - depth, activeList.Count);
		return activeList[actualIndex];
	}

	private void AccumulateStackWheelMomentum(
		(ContentAlignment Alignment, NotificationToastLayoutMode LayoutMode, NotificationToastPresentationMode PresentationMode) key,
		int itemCount,
		int requestedSteps)
	{
		if (itemCount <= 1 || requestedSteps == 0)
			return;

		// Clamp bursty wheel/touchpad input into a short-lived momentum value so stack reflow
		// stays stable and direction changes do not have to drain a long queued step list.
		var momentum = _stackWheelMomentum.TryGetValue(key, out var existingMomentum) ? existingMomentum : 0d;
		if (Math.Abs(momentum) > 0.001d && Math.Sign(momentum) != Math.Sign(requestedSteps))
			momentum = 0d;

		momentum = Math.Clamp(momentum + requestedSteps, -MaxStackWheelMomentum, MaxStackWheelMomentum);
		if (Math.Abs(momentum) < StackWheelMomentumStopThreshold)
		{
			_stackWheelMomentum.Remove(key);
			return;
		}

		_stackWheelMomentum[key] = momentum;

		var driver = GetOrCreateStackWheelDriver(key);
		if (!driver.IsAnimating())
			ProcessNextStackWheelMomentumStep(key);
	}

	private void ProcessNextStackWheelMomentumStep(
		(ContentAlignment Alignment, NotificationToastLayoutMode LayoutMode, NotificationToastPresentationMode PresentationMode) key)
	{
		if (_disposed)
			return;

		if (!_activeByAlignment.TryGetValue(key, out var activeList) || activeList.Count <= 1)
		{
			ClearStackWheelState(key);
			return;
		}

		if (!_stackWheelMomentum.TryGetValue(key, out var momentum) || Math.Abs(momentum) < StackWheelMomentumStopThreshold)
		{
			_stackWheelMomentum.Remove(key);
			return;
		}

		var step = Math.Sign(momentum);

		ShiftStackFrontIndex(key, activeList.Count, step);
		SyncTray(key);

		var nextMomentum = DecayStackWheelMomentum(momentum);
		if (Math.Abs(nextMomentum) < StackWheelMomentumStopThreshold)
		{
			_stackWheelMomentum.Remove(key);
			return;
		}

		_stackWheelMomentum[key] = nextMomentum;

		var driver = GetOrCreateStackWheelDriver(key);
		driver.SetData(new object[] { key });
		driver.StartNewAnimation(AnimationDirection.In);
	}

	private static double DecayStackWheelMomentum(double momentum)
	{
		var magnitude = (Math.Abs(momentum) - StackWheelMomentumBias) * StackWheelMomentumDecayFactor;
		if (magnitude < StackWheelMomentumStopThreshold)
			return 0d;

		return Math.CopySign(magnitude, momentum);
	}

	private AnimationManager GetOrCreateStackWheelDriver(
		(ContentAlignment Alignment, NotificationToastLayoutMode LayoutMode, NotificationToastPresentationMode PresentationMode) key)
	{
		if (_stackWheelDrivers.TryGetValue(key, out var existingDriver))
			return existingDriver;

		var driver = new AnimationManager
		{
			Singular = true,
			InterruptAnimation = true,
			Increment = StackWheelStepIncrement,
			SecondaryIncrement = StackWheelStepIncrement,
			AnimationType = AnimationType.QuarticEaseOut,
		};
		driver.SetData(new object[] { key });
		driver.OnAnimationFinished += HandleStackWheelDriverFinished;
		_stackWheelDrivers[key] = driver;
		return driver;
	}

	private void HandleStackWheelDriverFinished(object sender)
	{
		if (_disposed || sender is not AnimationManager driver)
			return;

		var data = driver.GetData();
		if (data.Length == 0 || data[0] is not ValueTuple<ContentAlignment, NotificationToastLayoutMode, NotificationToastPresentationMode> key)
			return;

		ProcessNextStackWheelMomentumStep(key);
	}

	private void ClearStackWheelState((ContentAlignment Alignment, NotificationToastLayoutMode LayoutMode, NotificationToastPresentationMode PresentationMode) key)
	{
		_stackWheelRemainders.Remove(key);
		_stackWheelMomentum.Remove(key);

		if (_stackWheelDrivers.Remove(key, out var driver))
		{
			driver.OnAnimationFinished -= HandleStackWheelDriverFinished;
			driver.Stop();
			driver.Dispose();
		}
	}

	private float GetStackOffset(int depth)
	{
		var scale = _owner.ScaleFactor;
		return depth switch
		{
			<= 0 => 0f,
			1 => 10f * scale,
			2 => 18f * scale,
			_ => 24f * scale,
		};
	}

	private float ResolveStackFrontScreenY(float toastHeight, float availableHeight, float topInset, ContentAlignment alignment)
	{
		var originY = topInset + MarginTop;

		if (IsTopAlignment(alignment))
			return originY + ToastShadowPadding;

		if (IsMiddleAlignment(alignment))
			return originY + ((availableHeight - toastHeight) / 2f);

		return originY + availableHeight - toastHeight - ToastShadowPadding;
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
				toast.StackBodyClickRequested -= OnStackBodyClickRequested;
				if (_trays.TryGetValue(alignment, out var t))
					t.Controls.Remove(toast);
				toast.Dispose();
			}
		}

		_activeByAlignment.Clear();
		_expandedStacks.Clear();
		_stackFrontIndices.Clear();
		_stackWheelRemainders.Clear();
		_stackWheelMomentum.Clear();

		foreach (var driver in _stackWheelDrivers.Values)
		{
			driver.OnAnimationFinished -= HandleStackWheelDriverFinished;
			driver.Stop();
			driver.Dispose();
		}

		_stackWheelDrivers.Clear();

		foreach (var tray in _trays.Values)
		{
			_owner.Controls.Remove(tray);
			tray.Dispose();
		}

		_trays.Clear();
	}
}
