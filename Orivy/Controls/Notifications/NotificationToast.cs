using Orivy;
using Orivy.Animation;
using Orivy.Helpers;
using Orivy.Styling;
using SkiaSharp;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Orivy.Controls;

public sealed class NotificationToast : ElementBase
{
	public static bool StackModeEnabled
	{
		get => ResolveCurrentManager().DefaultLayoutMode == NotificationToastLayoutMode.Stack;
		set => ResolveCurrentManager().DefaultLayoutMode = value ? NotificationToastLayoutMode.Stack : NotificationToastLayoutMode.List;
	}

	public static NotificationToastLayoutMode DefaultLayoutMode
	{
		get => ResolveCurrentManager().DefaultLayoutMode;
		set => ResolveCurrentManager().DefaultLayoutMode = value;
	}

	public static NotificationToastPalette? CustomPalette
	{
		get => ResolveCurrentManager().CustomPalette;
		set => ResolveCurrentManager().CustomPalette = value;
	}

	public static NotificationHandle Show(string title, string message)
		=> ResolveCurrentManager().Show(title, message);

	public static NotificationHandle Show(string title, string message, NotificationKind kind, int durationMs = 4000)
		=> ResolveCurrentManager().Show(title, message, kind, durationMs);

	public static NotificationHandle Show(
		string title,
		string message,
		NotificationKind kind,
		int durationMs,
		params NotificationAction[] actions)
		=> ResolveCurrentManager().Show(title, message, kind, durationMs, actions);

	public static NotificationHandle Show(
		string title,
		string message,
		NotificationKind kind,
		NotificationOptions? options)
		=> ResolveCurrentManager().Show(title, message, kind, options);

	public static NotificationHandle ShowDialog(
		string title,
		string message,
		NotificationKind kind,
		NotificationOptions? options = null)
		=> ResolveCurrentManager().ShowDialog(title, message, kind, options);

	public static Task<string> ConfirmAsync(
		string title,
		string message,
		NotificationKind kind = NotificationKind.Info,
		int durationMs = 0,
		params string[] buttonLabels)
		=> ResolveCurrentManager().ConfirmAsync(title, message, kind, durationMs, buttonLabels);

	public static void DismissAll()
		=> ResolveCurrentManager().DismissAll();

	private static NotificationManager ResolveCurrentManager()
		=> ResolveCurrentWindow().Notifications;

	private static WindowBase ResolveCurrentWindow()
	{
		var activeWindow = Application.ActiveForm;
		if (activeWindow != null && !activeWindow.IsDisposed)
			return activeWindow;

		var openForms = Application.OpenForms;
		for (var i = openForms.Count - 1; i >= 0; i--)
		{
			var candidate = openForms[i];
			if (candidate != null && !candidate.IsDisposed)
				return candidate;
		}

		throw new InvalidOperationException("No active Orivy window is available for notifications.");
	}

	internal const float BaseToastWidth = 380f;
	internal const float BaseDialogWidth = 468f;
	internal const float BaseShadowPadding = 12f;

	internal float ToastWidth => (_presentationMode == NotificationToastPresentationMode.Dialog ? BaseDialogWidth : BaseToastWidth) * ScaleFactor;
	private Radius CardRadius
		=> _presentationMode == NotificationToastPresentationMode.Dialog
			? new(22f * ScaleFactor, 22f * ScaleFactor, 22f * ScaleFactor, 22f * ScaleFactor)
			: new(16f * ScaleFactor, 16f * ScaleFactor, 16f * ScaleFactor, 16f * ScaleFactor);
	private Radius BadgeRadius => new(14f * ScaleFactor, 14f * ScaleFactor, 14f * ScaleFactor, 14f * ScaleFactor);
	private Radius ActionRadius => new(8f * ScaleFactor, 8f * ScaleFactor, 8f * ScaleFactor, 8f * ScaleFactor);

	private float PaddingH => (_presentationMode == NotificationToastPresentationMode.Dialog ? 18f : 14f) * ScaleFactor;
	private float PaddingV => (_presentationMode == NotificationToastPresentationMode.Dialog ? 14f : 10f) * ScaleFactor;
	private float CloseSize => 24f * ScaleFactor;
	private float CloseRightGap => 10f * ScaleFactor;
	private float IconDiameter => (_presentationMode == NotificationToastPresentationMode.Dialog ? 32f : 28f) * ScaleFactor;
	private float IconRadius => IconDiameter / 2f;
	private float IconGap => 12f * ScaleFactor;
	private float TitleFontSize => (_presentationMode == NotificationToastPresentationMode.Dialog ? 14f : 13f) * ScaleFactor;
	private float MessageFontSize => (_presentationMode == NotificationToastPresentationMode.Dialog ? 12.5f : 12f) * ScaleFactor;
	private float ActionFontSize => 13f * ScaleFactor;
	private float TitleLineHeight => MathF.Ceiling(TitleFontSize * 1.18f);
	private float MessageLineHeight => MathF.Ceiling(MessageFontSize * 1.28f);
	private float TitleMessageGap => 6f * ScaleFactor;
	private float ActionTopGap => (_presentationMode == NotificationToastPresentationMode.Dialog ? 14f : 10f) * ScaleFactor;
	private float ActionHeight => MathF.Max(26f * ScaleFactor, MathF.Ceiling(ActionFontSize * 2.2f));
	private float ActionGap => 6f * ScaleFactor;
	private float ProgressHeight => 3f * ScaleFactor;
	private int MaxMessageLines => _presentationMode == NotificationToastPresentationMode.Dialog ? 6 : 4;
	private float ContentLeft => PaddingH + IconDiameter + IconGap;
	private float ContentRight => ToastWidth - PaddingH - CloseSize - CloseRightGap;
	private float ContentWidth => ContentRight - ContentLeft;

	private SKTypeface? _cachedBoldTypeface;
	private bool _ownsCachedBoldTypeface;
	private SKFont? _cachedTitleFont;
	private SKFont? _cachedMessageFont;
	private SKFont? _cachedActionFont;
	private string? _cachedTypefaceName;
	private readonly SKPaint _badgeFillPaint = new() { IsAntialias = true, Style = SKPaintStyle.Fill };
	private readonly SKPaint _badgeStrokePaint = new() { IsAntialias = true, Style = SKPaintStyle.Stroke };
	private readonly SKPaint _iconStrokePaint = new() { IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeCap = SKStrokeCap.Round, StrokeJoin = SKStrokeJoin.Round };
	private readonly SKPaint _iconFillPaint = new() { IsAntialias = true, Style = SKPaintStyle.Fill };
	private readonly SKPaint _titleTextPaint = new() { IsAntialias = true, Style = SKPaintStyle.Fill };
	private readonly SKPaint _messageTextPaint = new() { IsAntialias = true, Style = SKPaintStyle.Fill };
	private readonly SKPaint _actionFillPaint = new() { IsAntialias = true, Style = SKPaintStyle.Fill };
	private readonly SKPaint _actionStrokePaint = new() { IsAntialias = true, Style = SKPaintStyle.Stroke };
	private readonly SKPaint _actionTextPaint = new() { IsAntialias = true, Style = SKPaintStyle.Fill };
	private readonly SKPaint _closeFillPaint = new() { IsAntialias = true, Style = SKPaintStyle.Fill };
	private readonly SKPaint _closeOutlinePaint = new() { IsAntialias = true, Style = SKPaintStyle.Stroke };
	private readonly SKPaint _closeLinePaint = new() { IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeCap = SKStrokeCap.Round };
	private readonly SKPaint _progressPaint = new() { IsAntialias = true, Style = SKPaintStyle.Fill };
	private readonly SKPath _iconPath = new();
	private readonly object _renderResourceSync = new();

	private readonly AnimationManager _countdownAnimation;
	private readonly NotificationAction[] _actions;
	private readonly SKRect[] _actionRects;
	private readonly NotificationToastPalette? _customPalette;
	internal readonly ContentAlignment _alignment;
	internal readonly NotificationToastLayoutMode _layoutMode;
	internal readonly NotificationToastPresentationMode _presentationMode;
	private bool IsTopPosition => _alignment is ContentAlignment.TopLeft or ContentAlignment.TopCenter or ContentAlignment.TopRight;
	private bool IsMiddlePosition => _alignment is ContentAlignment.MiddleLeft or ContentAlignment.MiddleCenter or ContentAlignment.MiddleRight;
	private ToastTransitionPhase _transitionPhase = ToastTransitionPhase.Enter;
	private NotificationHandle? _handle;

	internal float _targetX;
	internal float _targetY;

	private float _remainingMs;
	private float? _manualProgress;
	private int _dismissState;
	private int _stackDepth;
	private bool _countdownPaused;
	private bool _closeHovered;
	private bool _closePressed;
	private bool _consumeNextLeftClick;
	internal bool _hasBeenPlaced;
	private bool _showProgressBar;
	private int _hoveredAction = -1;
	private int _pressedAction = -1;
	private Action? _deferredCompletionAction;
	private float _steadyOpacity = 1f;
	private float _steadyScale = 1f;
	private SKRect _closeRect;
	private NotificationToastPalette _palette = new NotificationToastPalette(SKColors.White, SKColors.Black, SKColors.Black);
	internal Action? OnDismissWithoutAction;
	internal Action<NotificationToast>? StackBodyClickRequested;
	internal Action<NotificationToast, int>? StackWheelRequested;

	private enum ToastTransitionPhase
	{
		Steady,
		Enter,
		StackReflow,
	}


	public string Title { get; }
	public string Message { get; }
	public NotificationKind Kind { get; }
	public int DurationMs { get; }

	internal event EventHandler? DismissCompleted;

	internal void RefreshTheme()
	{
		ApplyTheme();
		Invalidate();
	}

	internal void RefreshForScale()
	{
		ClearFontCache();
		EnsureFontCache();

		var height = ComputeToastHeight(Message, _actions.Length > 0);
		Size = new SKSize(ToastWidth, height);
		ApplyTheme();
		LayoutInteractiveElements();
		if (_dismissState == 0)
			_transitionPhase = ToastTransitionPhase.Steady;
		SnapTo(_targetY);
		ConfigureEntryExitStyles();
		Invalidate();
	}

	protected override bool HandlesMouseWheelScroll => false;

	protected override void InvalidateFontCache()
	{
		ClearFontCache();
		base.InvalidateFontCache();
	}

	private void EnsureFontCache()
	{
		lock (_renderResourceSync)
		{
			var defaultTypeface = Application.SharedDefaultFont.Typeface ?? SKTypeface.Default;
			var familyName = defaultTypeface.FamilyName;

			if (_cachedTitleFont != null && _cachedMessageFont != null && _cachedActionFont != null && _cachedTypefaceName == familyName)
				return;

			ClearFontCacheCore();

			_cachedTypefaceName = familyName;
			_cachedBoldTypeface = SKTypeface.FromFamilyName(familyName, SKFontStyle.Bold);
			_ownsCachedBoldTypeface = _cachedBoldTypeface != null;
			var titleTypeface = _cachedBoldTypeface ?? defaultTypeface;
			_cachedTitleFont = Application.CreateUiFont(titleTypeface, TitleFontSize);
			_cachedMessageFont = Application.CreateUiFont(defaultTypeface, MessageFontSize);
			_cachedActionFont = Application.CreateUiFont(defaultTypeface, ActionFontSize);
		}
	}

	private static void EnsureStackMouseWheelRouting(object? sender, MouseEventArgs e)
	{
	}

	private void ClearFontCache()
	{
		lock (_renderResourceSync)
			ClearFontCacheCore();
	}

	private void ClearFontCacheCore()
	{
		_cachedTitleFont?.Dispose();
		_cachedMessageFont?.Dispose();
		_cachedActionFont?.Dispose();
		if (_ownsCachedBoldTypeface)
			_cachedBoldTypeface?.Dispose();

		_cachedTitleFont = null;
		_cachedMessageFont = null;
		_cachedActionFont = null;
		_cachedBoldTypeface = null;
		_ownsCachedBoldTypeface = false;
		_cachedTypefaceName = null;
	}

	internal NotificationToast(
		string title,
		string message,
		NotificationKind kind,
		int durationMs,
		bool showProgressBar,
		float? progress,
		NotificationToastPalette? customPalette,
		NotificationAction[]? actions = null,
		ContentAlignment alignment = ContentAlignment.BottomRight,
		NotificationToastLayoutMode layoutMode = NotificationToastLayoutMode.List,
		NotificationToastPresentationMode presentationMode = NotificationToastPresentationMode.Toast)
	{
		_alignment = alignment;
		_layoutMode = layoutMode;
		_presentationMode = presentationMode;
		Title = title;
		Message = message;
		Kind = kind;
		DurationMs = durationMs;
		_customPalette = customPalette;
		_showProgressBar = showProgressBar;
		_manualProgress = progress;

		_actions = actions ?? Array.Empty<NotificationAction>();
		if (_actions.Length > 0)
			_actions[0].IsPrimary = true;

		EnsureFontCache();

		_actionRects = new SKRect[_actions.Length];

		var height = ComputeToastHeight(message, _actions.Length > 0);

		Size = new SKSize(ToastWidth, height);
		ApplyTheme();
		ZOrder = 9999;
		Opacity = 0f;
		if (_layoutMode == NotificationToastLayoutMode.Stack)
			MouseWheel += EnsureStackMouseWheelRouting;
		ColorScheme.ThemeChanged += OnThemeChanged;

		ConfigureEntryExitStyles();

		_countdownAnimation = new AnimationManager(true)
		{
			AnimationType = AnimationType.Linear,
			InterruptAnimation = true,
			Increment = DurationMs > 0 ? Math.Max(0.0001, 16d / DurationMs) : 1d,
			SecondaryIncrement = DurationMs > 0 ? Math.Max(0.0001, 16d / DurationMs) : 1d,
		};
		_countdownAnimation.OnAnimationProgress += HandleCountdownAnimationProgress;
		_countdownAnimation.OnAnimationFinished += HandleCountdownAnimationFinished;

		LayoutInteractiveElements();

		_remainingMs = durationMs;
	}

	internal void UpdateStackPresentation(int depth)
	{
		var normalizedDepth = Math.Max(0, depth);
		var resolvedScale = 1f;
		var resolvedOpacity = 1f;

		if (_layoutMode == NotificationToastLayoutMode.Stack)
		{
			var visibleDepth = Math.Min(normalizedDepth, 3);
			resolvedScale = 1f - (visibleDepth * 0.045f);
			resolvedOpacity = normalizedDepth > 3 ? 0f : 1f - (visibleDepth * 0.12f);
		}

		if (_stackDepth == normalizedDepth
			&& Math.Abs(_steadyScale - resolvedScale) < 0.001f
			&& Math.Abs(_steadyOpacity - resolvedOpacity) < 0.001f)
			return;

		_stackDepth = normalizedDepth;
		_steadyScale = resolvedScale;
		_steadyOpacity = resolvedOpacity;
		ConfigureEntryExitStyles();

		if (_dismissState == 0 && _hasBeenPlaced)
			ReevaluateVisualStyles();
		else
			Invalidate();
	}

	internal void Place(float targetX, float targetY)
	{
		_targetX = targetX;
		_targetY = targetY;
		_hasBeenPlaced = true;
		_transitionPhase = ToastTransitionPhase.Enter;
		SetAnimatedLocation(new SKPoint(targetX, targetY));
		_remainingMs = DurationMs;
		ReevaluateVisualStyles();
	}

	internal void SetTargetX(float newTargetX)
	{
		if (Math.Abs(_targetX - newTargetX) < 0.5f)
			return;

		_targetX = newTargetX;
		SetAnimatedLocation(new SKPoint(newTargetX, _targetY));
	}


	internal void SnapTo(float newTargetY)
	{
		_targetY = newTargetY;
		if (_dismissState == 0)
			_transitionPhase = ToastTransitionPhase.Steady;
		SetAnimatedLocation(new SKPoint(_targetX, newTargetY));
	}

	internal void MoveTo(float newTargetY, float previousScreenY, float newScreenY)
	{
		var sameTarget = Math.Abs(newTargetY - _targetY) < 0.5f;
		if (sameTarget && Math.Abs(previousScreenY - newScreenY) < 0.5f)
			return;

		_targetY = newTargetY;
		
		SetAnimatedLocation(new SKPoint(_targetX, newTargetY));

		if (_hasBeenPlaced && _dismissState == 0)
		{
			// FLIP against actual screen-space movement so bottom-anchored trays
			// animate correctly when the tray itself shifts after removal.
			var screenDelta = previousScreenY - newScreenY;
			if (Math.Abs(screenDelta) < 0.5f)
			{
				_transitionPhase = ToastTransitionPhase.Steady;
				return;
			}

			_transitionPhase = ToastTransitionPhase.StackReflow;
			SetEffectiveTranslateY(screenDelta);
			ReevaluateVisualStyles();	
		}
	}

	internal float GetVisualScreenY(float trayY, float scrollOffset)
		=> trayY + Location.Y - scrollOffset + _renderTranslateY;

	internal void BeginDismiss()
	{
		if (Interlocked.CompareExchange(ref _dismissState, 1, 0) != 0)
			return;

		_closeHovered = false;
		_closePressed = false;
		_hoveredAction = -1;
		_pressedAction = -1;
		Cursor = Cursors.Default;
		_countdownAnimation.Stop();
		ReevaluateVisualStyles();
	}

	internal override void OnMouseEnter(EventArgs e)
	{
		base.OnMouseEnter(e);

		if (_dismissState == 0)
		{
			_countdownPaused = true;
			_countdownAnimation.Stop();
		}
	}

	internal override void OnMouseLeave(EventArgs e)
	{
		base.OnMouseLeave(e);

		_closeHovered = false;
		_closePressed = false;
		_hoveredAction = -1;
		_pressedAction = -1;
		Cursor = Cursors.Default;
		Invalidate();

		if (_dismissState == 0)
		{
			_countdownPaused = false;
			ResumeCountdownIfNeeded();
		}
	}

	internal override void OnMouseMove(MouseEventArgs e)
	{
		if (_dismissState != 0)
			return;

		var point = new SKPoint(e.X, e.Y);
		var previousCloseHovered = _closeHovered;
		var previousHoveredAction = _hoveredAction;

		_closeHovered = _closeRect.Contains(point);
		_hoveredAction = HitTestAction(point);
		Cursor = _closeHovered || _hoveredAction >= 0 ? Cursors.Hand : Cursors.Default;

		if (previousCloseHovered != _closeHovered || previousHoveredAction != _hoveredAction)
			Invalidate();
	}

	internal override void OnMouseDown(MouseEventArgs e)
	{
		if (_dismissState != 0)
			return;

		if (e.Button != MouseButtons.Left)
			return;

		var point = new SKPoint(e.X, e.Y);
		_closePressed = _closeRect.Contains(point);
		_pressedAction = HitTestAction(point);
		_consumeNextLeftClick = _closePressed || _pressedAction >= 0;
		if (_closePressed || _pressedAction >= 0)
			Invalidate();
	}

	internal override void OnMouseUp(MouseEventArgs e)
	{
		if (_dismissState != 0)
			return;

		if (e.Button != MouseButtons.Left)
			return;

		var point = new SKPoint(e.X, e.Y);
		var shouldDismiss = _closePressed && _closeRect.Contains(point);
		var actionIndex = _pressedAction >= 0 && _pressedAction == HitTestAction(point) ? _pressedAction : -1;

		_closePressed = false;
		_pressedAction = -1;

		if (shouldDismiss)
		{
			_deferredCompletionAction = OnDismissWithoutAction;
			BeginDismiss();
			return;
		}

		if (actionIndex >= 0)
		{
			_deferredCompletionAction = _actions[actionIndex].Invoke;
			BeginDismiss();
			return;
		}

		if (_layoutMode == NotificationToastLayoutMode.Stack)
			StackBodyClickRequested?.Invoke(this);

		Invalidate();
	}

	internal override void OnMouseWheel(MouseEventArgs e)
	{
		if (_dismissState != 0)
			return;

		if (_layoutMode == NotificationToastLayoutMode.Stack && e.Delta != 0)
		{
			StackWheelRequested?.Invoke(this, e.Delta);
			return;
		}

		if (Parent is NotificationTray tray)
		{
			var trayEvent = new MouseEventArgs(
				e.Button,
				e.Clicks,
				(int)(Location.X + e.X),
				(int)(Location.Y + e.Y),
				e.Delta,
				e.IsHorizontalWheel);
			tray.OnMouseWheel(trayEvent);
			return;
		}

		base.OnMouseWheel(e);
	}

	protected internal override void OnMouseClick(MouseEventArgs e)
	{
		if (e.Button == MouseButtons.Left && (_consumeNextLeftClick || _dismissState != 0))
		{
			_consumeNextLeftClick = false;
			return;
		}

		base.OnMouseClick(e);
	}

	internal void AttachHandle(NotificationHandle handle)
	{
		_handle = handle;
	}

	internal void DetachHandle()
	{
		_handle?.Detach();
		_handle = null;
	}

	internal Action? TakeDeferredCompletionAction()
	{
		var deferred = _deferredCompletionAction;
		_deferredCompletionAction = null;
		return deferred;
	}

	internal void SetProgressVisible(bool visible)
	{
		_showProgressBar = visible;
		Invalidate();
	}

	internal void SetManualProgress(float? progress)
	{
		_manualProgress = progress.HasValue ? Math.Clamp(progress.Value, 0f, 1f) : null;
		if (progress.HasValue)
			_showProgressBar = true;
		Invalidate();
	}

	public override void OnPaint(SKCanvas canvas)
	{
		if (Disposing || IsDisposed)
			return;

		base.OnPaint(canvas);
		if (Disposing || IsDisposed)
			return;

		lock (_renderResourceSync)
		{
			if (Disposing || IsDisposed)
				return;

			EnsureFontCache();

			var width = (float)Width;
			var height = (float)Height;
			var accent =  _palette.AccentColor;

			//ElevationHelper.DrawStateLayer(canvas, new SKRect(0f, 0f, width, height), CardRadius.TopLeft, GetBackgroundColor().WithAlpha(100));
			//ElevationHelper.DrawFluentGlass(canvas, new SKRect(0f, 0f, width, height), CardRadius.TopLeft, GetBackgroundColor());
			DrawIconBadge(canvas, accent);
			DrawTitle(canvas);
			DrawMessage(canvas, height);
			DrawCloseButton(canvas);
			if (_actions.Length > 0)
				DrawActions(canvas);
			if (_showProgressBar)
				DrawProgress(canvas, width, height, accent);
		}
	}

	private void OnThemeChanged(object? sender, EventArgs e)
	{
		ApplyTheme();
		Invalidate();
	}

	private void ConfigureEntryExitStyles()
	{
		var steadyOpacity = _steadyOpacity;
		var steadyScale = _steadyScale;
		var directionSign = _presentationMode == NotificationToastPresentationMode.Dialog ? 0f : IsTopPosition ? -1f : IsMiddlePosition ? 0f : 1f;
		var entranceOffset = directionSign * 16f * ScaleFactor;
		var exitOffset = directionSign * 8f * ScaleFactor;
		var entranceScale = _presentationMode == NotificationToastPresentationMode.Dialog ? 0.94f : 0.88f;
		const float exitScale = 0.95f;

		if (!_hasBeenPlaced)
		{
			// Establish the before-entry visual state so the animation interpolates FROM it.
			// ApplyVisualStyleBase updates both the base snapshot and applies immediately
			// (forceImmediate), ensuring _styleEffectiveSnapshot reflects this state
			// and the enter animation starts from the correct offset + scale.
			ApplyVisualStyleBase(new ElementVisualStyle
			{
				Opacity = 0f,
				TranslateY = entranceOffset,
				ScaleX = steadyScale * entranceScale,
				ScaleY = steadyScale * entranceScale,
			});
		}

		ConfigureVisualStyles(styles => styles
			.When(
				(el, _) => el is NotificationToast t && t._dismissState != 0,
				rule => rule
					.Opacity(0f)
					.Scale(steadyScale * exitScale)
					.TranslateY(exitOffset)
					.Transition(TimeSpan.FromMilliseconds(180), AnimationType.CubicEaseIn))
			.When(
				(el, _) => el is NotificationToast t
					&& t._hasBeenPlaced
					&& t._dismissState == 0
					&& t._transitionPhase == ToastTransitionPhase.Enter,
				rule => rule
					.Opacity(steadyOpacity)
					.Scale(steadyScale)
					.TranslateY(0f)
					.Transition(TimeSpan.FromMilliseconds(320), AnimationType.BackOut))
			.When(
				(el, _) => el is NotificationToast t
					&& t._hasBeenPlaced
					&& t._dismissState == 0
					&& t._transitionPhase == ToastTransitionPhase.StackReflow,
				rule => rule
					.Opacity(steadyOpacity)
					.Scale(steadyScale)
					.TranslateY(0f)
					.Transition(TimeSpan.FromMilliseconds(190), AnimationType.QuarticEaseOut, ElementVisualTransitionMode.ReplayOnReevaluate))
			.When(
				(el, _) => el is NotificationToast t && t._hasBeenPlaced && t._dismissState == 0,
				rule => rule
					.Opacity(steadyOpacity)
					.Scale(steadyScale)
					.TranslateY(0f)),
			clearExisting: true);
	}

	private void ApplyTheme()
	{
		_palette = ResolvePalette();

		BackColor = _palette.BackgroundColor;
		ForeColor = _palette.ForegroundColor;
		Radius = CardRadius;
		Shadows = CreateShadows();
	}

	private BoxShadow[] CreateShadows()
	{
		if (_presentationMode == NotificationToastPresentationMode.Dialog)
		{
			var ambientDialogShadow = SKColors.Black.WithAlpha(_palette.IsDarkSurface ? (byte)124 : (byte)58);
			var liftDialogShadow = SKColors.Black.WithAlpha(_palette.IsDarkSurface ? (byte)64 : (byte)22);
			return
			[
				new BoxShadow(0f, 14f * ScaleFactor, 32f * ScaleFactor, 0, ambientDialogShadow),
				new BoxShadow(0f, 4f * ScaleFactor, 12f * ScaleFactor, 0, liftDialogShadow),
			];
		}

		var ambientShadow = SKColors.Black.WithAlpha(_palette.IsDarkSurface ? (byte)88 : (byte)34);
		var liftShadow = SKColors.Black.WithAlpha(_palette.IsDarkSurface ? (byte)42 : (byte)18);

		return
		[
			new BoxShadow(0f, 8f * ScaleFactor, 18f * ScaleFactor, 0, ambientShadow),
			new BoxShadow(0f, 2f * ScaleFactor, 6f * ScaleFactor, 0, liftShadow),
		];
	}

	private void DrawIconBadge(SKCanvas canvas, SKColor accent)
	{
		var centerX = PaddingH + IconRadius;
		var centerY = PaddingV + (1f * ScaleFactor) + IconRadius;
		var badgeRect = new SKRect(centerX - IconRadius, centerY - IconRadius, centerX + IconRadius, centerY + IconRadius);

		_badgeFillPaint.Color = accent.WithAlpha(22);
		_badgeStrokePaint.StrokeWidth = MathF.Max(1f, ScaleFactor);
		_badgeStrokePaint.Color = accent.WithAlpha(82);
		_iconStrokePaint.StrokeWidth = 1.8f * ScaleFactor;
		_iconStrokePaint.Color = accent;

		var badgeRadius = BadgeRadius.All >= 0f ? BadgeRadius.All : BadgeRadius.TopLeft;
		canvas.DrawRoundRect(badgeRect, badgeRadius, badgeRadius, _badgeFillPaint);
		canvas.DrawRoundRect(badgeRect, badgeRadius, badgeRadius, _badgeStrokePaint);

		switch (Kind)
		{
			case NotificationKind.Success:
				DrawCheckIcon(canvas, centerX, centerY, _iconStrokePaint);
				break;
			case NotificationKind.Warning:
				DrawWarningIcon(canvas, centerX, centerY, _iconStrokePaint);
				break;
			case NotificationKind.Error:
				DrawErrorIcon(canvas, centerX, centerY, _iconStrokePaint);
				break;
			default:
				DrawInfoIcon(canvas, centerX, centerY, _iconStrokePaint);
				break;
		}
	}

	private void DrawCheckIcon(SKCanvas canvas, float centerX, float centerY, SKPaint iconPaint)
	{
		var leftX = 5.4f * ScaleFactor;
		var midX = 1.2f * ScaleFactor;
		var lowY = 4.6f * ScaleFactor;
		var offsetY = 0.5f * ScaleFactor;

		_iconPath.Reset();
		_iconPath.MoveTo(centerX - leftX, centerY + offsetY);
		_iconPath.LineTo(centerX - midX, centerY + lowY);
		_iconPath.LineTo(centerX + leftX, centerY - lowY);
		canvas.DrawPath(_iconPath, iconPaint);
	}

	private void DrawWarningIcon(SKCanvas canvas, float centerX, float centerY, SKPaint iconPaint)
	{
		var lineTop = 5.2f * ScaleFactor;
		var lineBottom = 1f * ScaleFactor;
		var dotOffset = 5.2f * ScaleFactor;
		var dotRadius = 1.5f * ScaleFactor;

		canvas.DrawLine(centerX, centerY - lineTop, centerX, centerY + lineBottom, iconPaint);
		_iconFillPaint.Color = iconPaint.Color;
		canvas.DrawCircle(centerX, centerY + dotOffset, dotRadius, _iconFillPaint);
	}

	private void DrawErrorIcon(SKCanvas canvas, float centerX, float centerY, SKPaint iconPaint)
	{
		var delta = 4.5f * ScaleFactor;
		canvas.DrawLine(centerX - delta, centerY - delta, centerX + delta, centerY + delta, iconPaint);
		canvas.DrawLine(centerX + delta, centerY - delta, centerX - delta, centerY + delta, iconPaint);
	}

	private void DrawInfoIcon(SKCanvas canvas, float centerX, float centerY, SKPaint iconPaint)
	{
		var dotOffset = 4.8f * ScaleFactor;
		var dotRadius = 1.5f * ScaleFactor;
		var lineTop = 1f * ScaleFactor;
		var lineBottom = 5.2f * ScaleFactor;

		_iconFillPaint.Color = iconPaint.Color;
		canvas.DrawCircle(centerX, centerY - dotOffset, dotRadius, _iconFillPaint);
		canvas.DrawLine(centerX, centerY - lineTop, centerX, centerY + lineBottom, iconPaint);
	}

	private void DrawTitle(SKCanvas canvas)
	{
		var titleTop = PaddingV;
		var titleRect = new SKRect(ContentLeft, titleTop, ContentRight, titleTop + TitleLineHeight);
		_titleTextPaint.Color = ForeColor;
		TextRenderer.DrawText(canvas, Title, titleRect, _titleTextPaint, _cachedTitleFont!, ContentAlignment.MiddleLeft, autoEllipsis: true);
	}

	private void DrawMessage(SKCanvas canvas, float height)
	{
		var messageTop = PaddingV + TitleLineHeight + TitleMessageGap;
		var reservedBottom = PaddingV + ProgressHeight + (_actions.Length > 0 ? ActionTopGap + ActionHeight : 0);
		var messageBottom = height - reservedBottom;
		if (messageBottom <= messageTop)
			return;

		_messageTextPaint.Color = ForeColor;
		var baseLineHeight = _cachedMessageFont!.Metrics.Descent - _cachedMessageFont.Metrics.Ascent;
		var lineSpacing = MessageLineHeight / Math.Max(1f, baseLineHeight);
		var baselineY = messageTop - _cachedMessageFont.Metrics.Ascent;
		TextRenderer.DrawText(
			canvas,
			Message,
			ContentLeft,
			baselineY,
			SKTextAlign.Left,
			_cachedMessageFont!,
			_messageTextPaint,
			new TextRenderOptions
			{
				Wrap = TextWrap.WordWrap,
				MaxWidth = ContentWidth,
				MaxHeight = messageBottom - messageTop,
				LineSpacing = lineSpacing,
			});
	}


	internal override void OnSizeChanged(EventArgs e)
	{
		base.OnSizeChanged(e);
		LayoutInteractiveElements();
	}

	private void DrawActions(SKCanvas canvas)
	{
		_actionStrokePaint.StrokeWidth = MathF.Max(1f, ScaleFactor);

		for (var i = 0; i < _actions.Length; i++)
		{
			var rect = _actionRects[i];
			var isPrimary = _actions[i].IsPrimary;
			var hovered = _hoveredAction == i;
			var pressed = _pressedAction == i;

			var background = isPrimary
				? (pressed ? _palette.PrimaryActionBackgroundColor.Brightness(-0.08f) : hovered ? _palette.PrimaryActionBackgroundColor.Brightness(0.06f) : _palette.PrimaryActionBackgroundColor)
				: ResolveSecondaryActionBackground(hovered, pressed);

			var borderColor = isPrimary
				? (pressed ? _palette.PrimaryActionBackgroundColor.Brightness(-0.24f) : hovered ? _palette.PrimaryActionBackgroundColor.Brightness(-0.08f) : _palette.PrimaryActionBackgroundColor.Brightness(-0.18f))
				: ResolveSecondaryActionBorder(hovered, pressed);

			var foreground = isPrimary ? _palette.PrimaryActionForegroundColor : _palette.SecondaryActionForegroundColor;

			_actionFillPaint.Color = background;
			_actionStrokePaint.Color = borderColor;
			_actionTextPaint.Color = foreground;

			var actionRadius = ActionRadius.All >= 0f ? ActionRadius.All : ActionRadius.TopLeft;
			canvas.DrawRoundRect(rect, actionRadius, actionRadius, _actionFillPaint);
			canvas.DrawRoundRect(rect, actionRadius, actionRadius, _actionStrokePaint);
			TextRenderer.DrawText(canvas, _actions[i].Label, rect, _actionTextPaint, _cachedActionFont!, ContentAlignment.MiddleCenter, autoEllipsis: false);
		}
	}

	private void DrawCloseButton(SKCanvas canvas)
	{
		var background = _closePressed
			? ResolveCloseButtonBackground(pressed: true)
			: _closeHovered ? ResolveCloseButtonBackground(pressed: false) : SKColors.Transparent;
		var foreground = _closeHovered || _closePressed
			? _palette.CloseButtonActiveForegroundColor
			: _palette.CloseButtonIdleForegroundColor;
		var outline = _closeHovered || _closePressed
			? _palette.OutlineColor.WithAlpha(_palette.IsDarkSurface ? (byte)110 : (byte)80)
			: SKColors.Transparent;

		_closeFillPaint.Color = background;
		_closeOutlinePaint.StrokeWidth = MathF.Max(1f, ScaleFactor);
		_closeOutlinePaint.Color = outline;
		_closeLinePaint.StrokeWidth = 1.55f * ScaleFactor;
		_closeLinePaint.Color = foreground;

		var centerX = _closeRect.MidX;
		var centerY = _closeRect.MidY;
		var radius = Math.Max(6f * ScaleFactor, (_closeRect.Width / 2f) - (2f * ScaleFactor));
		canvas.DrawCircle(centerX, centerY, radius, _closeFillPaint);
		if (outline != SKColors.Transparent)
			canvas.DrawCircle(centerX, centerY, radius - 0.5f, _closeOutlinePaint);

		var delta = 3.9f * ScaleFactor;
		canvas.DrawLine(centerX - delta, centerY - delta, centerX + delta, centerY + delta, _closeLinePaint);
		canvas.DrawLine(centerX + delta, centerY - delta, centerX - delta, centerY + delta, _closeLinePaint);
	}

	private void LayoutInteractiveElements()
	{
		_closeRect = new SKRect(
			ToastWidth - CloseRightGap - CloseSize,
			PaddingV - (1f * ScaleFactor),
			ToastWidth - CloseRightGap,
			PaddingV - (1f * ScaleFactor) + CloseSize);

		var actionY = Height - PaddingV - ProgressHeight - ActionHeight;
		var x = ContentRight;

		for (var i = _actions.Length - 1; i >= 0; i--)
		{
			var width = EstimateActionButtonWidth(_actions[i].Label);
			x -= width;
			_actionRects[i] = new SKRect(x, actionY, x + width, actionY + ActionHeight);
			x -= ActionGap;
		}
	}

	private int HitTestAction(SKPoint point)
	{
		for (var i = 0; i < _actionRects.Length; i++)
		{
			if (_actionRects[i].Contains(point))
				return i;
		}

		return -1;
	}

	private float EstimateActionButtonWidth(string? text)
	{
		var minWidth = 64f * ScaleFactor;
		var maxWidth = 156f * ScaleFactor;

		if (string.IsNullOrWhiteSpace(text))
			return minWidth;

		var length = text.Length;
		var estimated = (20f * ScaleFactor) + (length * (ActionFontSize * 0.68f));
		return Math.Clamp(estimated, minWidth, maxWidth);
	}

	private void DrawProgress(SKCanvas canvas, float width, float height, SKColor accent)
	{
		if (_dismissState != 0)
			return;

		var fraction = _manualProgress.HasValue
			? _manualProgress.Value
			: (DurationMs > 0 ? Math.Clamp(_remainingMs / DurationMs, 0f, 1f) : 0f);

		if (fraction <= 0f)
			return;

		var barRect = new SKRect(0, height - ProgressHeight, width * fraction, height);
		_progressPaint.Color = accent.WithAlpha(168);
		canvas.DrawRect(barRect, _progressPaint);
	}

	protected override void OnVisualStyleTransitionCompleted()
	{
		if (_dismissState != 0 && Opacity <= 0.001f)
		{
			_hasBeenPlaced = false;
			if (DismissCompleted != null)
			{
				DismissCompleted.Invoke(this, EventArgs.Empty);
				return;
			}
			Visible = false;
			return;
		}

		if (_dismissState == 0)
			_transitionPhase = ToastTransitionPhase.Steady;

		if (_hasBeenPlaced && _dismissState == 0 && Opacity >= 0.999f)
			ResumeCountdownIfNeeded();
	}

	private void HandleCountdownAnimationProgress(object _)
	{
		if (DurationMs <= 0)
			return;

		_remainingMs = (float)(_countdownAnimation.GetProgress() * DurationMs);

		if (_showProgressBar && !_manualProgress.HasValue)
			Invalidate();
	}

	private void HandleCountdownAnimationFinished(object _)
	{
		if (_dismissState != 0 || DurationMs <= 0)
			return;

		_remainingMs = 0f;
		_deferredCompletionAction = OnDismissWithoutAction;
		BeginDismiss();
	}

	private void ResumeCountdownIfNeeded()
	{
		if (_dismissState != 0 || _countdownPaused || DurationMs <= 0 || _countdownAnimation.IsAnimating())
			return;

		var progress = Math.Clamp(DurationMs > 0 ? _remainingMs / DurationMs : 0f, 0f, 1f);
		if (progress <= 0f)
			return;

		_countdownAnimation.AnimationType = AnimationType.Linear;
		_countdownAnimation.Increment = Math.Max(0.0001, 16d / DurationMs);
		_countdownAnimation.SecondaryIncrement = _countdownAnimation.Increment;
		_countdownAnimation.SetProgress(progress);
		_countdownAnimation.StartNewAnimation(AnimationDirection.Out);
	}

	private SKColor ResolveSecondaryActionBackground(bool hovered, bool pressed)
	{
		var baseColor = _palette.SurfaceVariantColor;
		if (pressed)
			return baseColor.Brightness(_palette.IsDarkSurface ? -0.05f : -0.03f).WithAlpha(_palette.IsDarkSurface ? (byte)214 : (byte)184);

		if (hovered)
			return baseColor.WithAlpha(_palette.IsDarkSurface ? (byte)182 : (byte)152);

		return baseColor.WithAlpha(_palette.IsDarkSurface ? (byte)150 : (byte)128);
	}

	private SKColor ResolveSecondaryActionBorder(bool hovered, bool pressed)
	{
		if (pressed)
			return _palette.OutlineColor.WithAlpha(_palette.IsDarkSurface ? (byte)176 : (byte)144);

		if (hovered)
			return _palette.OutlineColor.WithAlpha(_palette.IsDarkSurface ? (byte)146 : (byte)120);

		return _palette.OutlineColor.WithAlpha(_palette.IsDarkSurface ? (byte)112 : (byte)92);
	}

	private SKColor ResolveCloseButtonBackground(bool pressed)
	{
		var baseColor = _palette.SurfaceVariantColor;
		if (pressed)
			return baseColor.Brightness(_palette.IsDarkSurface ? -0.05f : -0.03f).WithAlpha(_palette.IsDarkSurface ? (byte)228 : (byte)188);

		return baseColor.WithAlpha(_palette.IsDarkSurface ? (byte)188 : (byte)144);
	}

	private NotificationToastPalette ResolvePalette()
	{
		return NotificationToastPalette.FromKind(Kind, _customPalette);
	}

	private float ComputeToastHeight(string message, bool hasActions)
	{
		var lines = EstimateMessageLines(message);
		var contentHeight = TitleLineHeight + TitleMessageGap + lines * MessageLineHeight;
		if (hasActions)
			contentHeight += ActionTopGap + ActionHeight;

		var bodyHeight = Math.Max(contentHeight, IconDiameter);
		return PaddingV + bodyHeight + PaddingV + ProgressHeight;
	}

	private int EstimateMessageLines(string message)
	{
		if (string.IsNullOrEmpty(message))
			return 1;

		EnsureFontCache();

		var maxWidth = Math.Max(1f, ContentWidth);
		var wrappedLines = TextWrapper.WrapText(message, _cachedMessageFont!, maxWidth, TextWrap.WordWrap);
		return Math.Clamp(wrappedLines.Count, 1, MaxMessageLines);
	}

	protected override void Dispose(bool disposing)
	{
		if (disposing)
		{
			MouseWheel -= EnsureStackMouseWheelRouting;
			ColorScheme.ThemeChanged -= OnThemeChanged;
			_countdownAnimation.OnAnimationProgress -= HandleCountdownAnimationProgress;
			_countdownAnimation.OnAnimationFinished -= HandleCountdownAnimationFinished;
			_countdownAnimation.Dispose();

			lock (_renderResourceSync)
			{
				ClearFontCacheCore();
				_badgeFillPaint.Dispose();
				_badgeStrokePaint.Dispose();
				_iconStrokePaint.Dispose();
				_iconFillPaint.Dispose();
				_titleTextPaint.Dispose();
				_messageTextPaint.Dispose();
				_actionFillPaint.Dispose();
				_actionStrokePaint.Dispose();
				_actionTextPaint.Dispose();
				_closeFillPaint.Dispose();
				_closeOutlinePaint.Dispose();
				_closeLinePaint.Dispose();
				_progressPaint.Dispose();
				_iconPath.Dispose();
			}

			DetachHandle();
		}

		base.Dispose(disposing);
	}
}
