using SkiaSharp;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Orivy.Controls;

public sealed class NotificationManager : IDisposable
{
	private const int MarginRight = 24;
	private const int MarginBottom = 24;
	private const int ToastSpacing = 12;
	private const float MaxTrayHeightFraction = 0.72f;

	private readonly WindowBase _owner;
	private readonly NotificationTray _tray;
	private readonly List<NotificationToast> _active = new();
	private bool _disposed;

	public NotificationManager(WindowBase owner)
	{
		_owner = owner ?? throw new ArgumentNullException(nameof(owner));

		_tray = new NotificationTray
		{
			Visible = false,
			Size = new SKSize(NotificationToast.ToastWidth, 1),
			Location = SKPoint.Empty,
		};

		_owner.Controls.Add(_tray);
		_tray.BringToFront();

		_owner.SizeChanged += OnOwnerSizeChanged;
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
			buttonLabels = new[] { "OK" };

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
		for (var i = _active.Count - 1; i >= 0; i--)
			_active[i].BeginDismiss();
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

		var toast = new NotificationToast(
			title,
			message,
			kind,
			options.DurationMs,
			options.ShowProgressBar,
			options.Progress,
			options.Actions)
		{
			OnDismissWithoutAction = onDismissWithoutAction,
		};
		var handle = new NotificationHandle(toast);
		toast.AttachHandle(handle);

		toast.DismissCompleted += OnToastDismissed;

		var shift = toast.Height + ToastSpacing;
		for (var i = 0; i < _active.Count; i++)
			_active[i].MoveTo(_active[i]._targetY + shift);

		_active.Insert(0, toast);
		_tray.Controls.Add(toast);
		_tray.BringToFront();
		toast.BringToFront();

		SyncTray();
		toast.Place(0f, 0f);
		_tray.ScrollToTop();
		_tray.Invalidate();
		return handle;
	}

	private void OnToastDismissed(object? sender, EventArgs e)
	{
		if (sender is not NotificationToast dismissed)
			return;

		dismissed.DismissCompleted -= OnToastDismissed;

		var index = _active.IndexOf(dismissed);
		if (index < 0)
			return;

		var freedHeight = dismissed.Height + ToastSpacing;
		_active.RemoveAt(index);

		for (var i = index; i < _active.Count; i++)
			_active[i].MoveTo(_active[i]._targetY - freedHeight);

		_tray.Controls.Remove(dismissed);
		dismissed.DetachHandle();
		dismissed.Dispose();

		SyncTray();
	}

	private void OnOwnerSizeChanged(object? sender, EventArgs e)
	{
		if (_disposed)
			return;

		SyncTray();
	}

	private void SyncTray()
	{
		if (_active.Count == 0)
		{
			_tray.Visible = false;
			return;
		}

		var totalHeight = TotalContentHeight();
		var maxHeight = Math.Max(72f, _owner.Height * MaxTrayHeightFraction);
		_tray.Resize(totalHeight, maxHeight);

		_tray.Location = new SKPoint(
			_owner.Width - MarginRight - _tray.Width,
			_owner.Height - MarginBottom - _tray.Height);

		_tray.Visible = true;
		_tray.BringToFront();
		_tray.Invalidate();
	}

	private float TotalContentHeight()
	{
		var total = 0f;
		for (var i = 0; i < _active.Count; i++)
		{
			if (i > 0)
				total += ToastSpacing;
			total += _active[i].Height;
		}

		return total;
	}

	public void Dispose()
	{
		if (_disposed)
			return;

		_disposed = true;
		_owner.SizeChanged -= OnOwnerSizeChanged;

		for (var i = _active.Count - 1; i >= 0; i--)
		{
			var toast = _active[i];
			toast.DismissCompleted -= OnToastDismissed;
			_tray.Controls.Remove(toast);
			toast.Dispose();
		}

		_active.Clear();
		_owner.Controls.Remove(_tray);
		_tray.Dispose();
	}
}
