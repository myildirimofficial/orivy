using Orivy.Animation;
using Orivy.Helpers;
using SkiaSharp;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Orivy.Controls;

public sealed class NotificationToast : ElementBase
{
	public static NotificationToastThemeMode ThemeMode
	{
		get => ResolveCurrentManager().ThemeMode;
		set => ResolveCurrentManager().ThemeMode = value;
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
	internal const float BaseShadowPadding = 6f;

	internal float ToastWidth => BaseToastWidth * ScaleFactor;
	private Radius CardRadius => new(16f * ScaleFactor, 16f * ScaleFactor, 16f * ScaleFactor, 16f * ScaleFactor);
	private Radius BadgeRadius => new(14f * ScaleFactor, 14f * ScaleFactor, 14f * ScaleFactor, 14f * ScaleFactor);
	private Radius ActionRadius => new(8f * ScaleFactor, 8f * ScaleFactor, 8f * ScaleFactor, 8f * ScaleFactor);

	private float PaddingH => 14f * ScaleFactor;
	private float PaddingV => 10f * ScaleFactor;
	private float CloseSize => 24f * ScaleFactor;
	private float CloseRightGap => 10f * ScaleFactor;
	private float IconDiameter => 28f * ScaleFactor;
	private float IconRadius => 14f * ScaleFactor;
	private float IconGap => 12f * ScaleFactor;
	private float TitleFontSize => 13f * ScaleFactor;
	private float MessageFontSize => 12f * ScaleFactor;
	private float ActionFontSize => 13f * ScaleFactor;
	private float TitleLineHeight => MathF.Ceiling(TitleFontSize * 1.18f);
	private float MessageLineHeight => MathF.Ceiling(MessageFontSize * 1.28f);
	private float TitleMessageGap => 6f * ScaleFactor;
	private float ActionTopGap => 10f * ScaleFactor;
	private float ActionHeight => MathF.Max(26f * ScaleFactor, MathF.Ceiling(ActionFontSize * 2.2f));
	private float ActionGap => 6f * ScaleFactor;
	private float ProgressHeight => 3f * ScaleFactor;
	private const int MaxMessageLines = 4;
	private float EnterDurationMs => 120f;
	private float ExitDurationMs => 180f;

	private float ContentLeft => PaddingH + IconDiameter + IconGap;
	private float ContentRight => ToastWidth - PaddingH - CloseSize - CloseRightGap;
	private float ContentWidth => ContentRight - ContentLeft;

	private SKTypeface? _cachedBoldTypeface;
	private bool _ownsCachedBoldTypeface;
	private SKFont? _cachedTitleFont;
	private SKFont? _cachedMessageFont;
	private SKFont? _cachedActionFont;
	private string? _cachedTypefaceName;

	private readonly AnimationManager _countdownAnimation;
	private readonly AnimationManager _visibilityAnimation;
	private readonly AnimationManager _slideAnimation;
	private readonly NotificationAction[] _actions;
	private readonly SKRect[] _actionRects;
	private readonly NotificationToastPalette? _customPalette;
	private readonly NotificationToastThemeMode _themeMode;
	private NotificationHandle? _handle;

	internal float _targetX;
	internal float _targetY;
	private float _startY;

	private float _remainingMs;
	private float? _manualProgress;
	private int _dismissState;
	private bool _countdownPaused;
	private bool _closeHovered;
	private bool _closePressed;
	private bool _consumeNextLeftClick;
	internal bool _hasBeenPlaced;
	private bool _showProgressBar;
	private int _hoveredAction = -1;
	private int _pressedAction = -1;
	private Action? _deferredCompletionAction;
	private SKRect _closeRect;
	private ToastPalette _palette;
	private ToastVisualState _visualState;

	internal Action? OnDismissWithoutAction;

	private enum ToastVisualState
	{
		Entering,
		Steady,
		Exiting,
	}

	private readonly struct ToastPalette
	{
		public ToastPalette(SKColor backgroundColor, SKColor accentColor, SKColor foregroundColor)
		{
			BackgroundColor = backgroundColor;
			AccentColor = accentColor;
			ForegroundColor = foregroundColor;
			IsDarkSurface = backgroundColor.IsDark();

			var baseSurface = IsDarkSurface
				? backgroundColor.Brightness(0.08f).WithAlpha(216)
				: backgroundColor.Brightness(-0.05f).WithAlpha(164);

			SurfaceVariantColor = baseSurface;
			OutlineColor = foregroundColor.WithAlpha(IsDarkSurface ? (byte)96 : (byte)72);
			PrimaryActionBackgroundColor = accentColor;
			PrimaryActionForegroundColor = accentColor.Determine().WithAlpha(255);
			SecondaryActionForegroundColor = foregroundColor;
			CloseButtonIdleForegroundColor = foregroundColor.WithAlpha(IsDarkSurface ? (byte)160 : (byte)148);
			CloseButtonActiveForegroundColor = foregroundColor.WithAlpha(228);
		}

		public SKColor BackgroundColor { get; }
		public SKColor AccentColor { get; }
		public SKColor ForegroundColor { get; }
		public SKColor OutlineColor { get; }
		public SKColor PrimaryActionBackgroundColor { get; }
		public SKColor PrimaryActionForegroundColor { get; }
		public SKColor SecondaryActionForegroundColor { get; }
		public SKColor SurfaceVariantColor { get; }
		public SKColor CloseButtonIdleForegroundColor { get; }
		public SKColor CloseButtonActiveForegroundColor { get; }
		public bool IsDarkSurface { get; }
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
		SnapTo(_targetY);
		Invalidate();
	}

	protected override void InvalidateFontCache()
	{
		ClearFontCache();
		base.InvalidateFontCache();
	}

	private void EnsureFontCache()
	{
		var defaultTypeface = Application.SharedDefaultFont.Typeface ?? SKTypeface.Default;
		var familyName = defaultTypeface.FamilyName;

		if (_cachedTitleFont != null && _cachedMessageFont != null && _cachedActionFont != null && _cachedTypefaceName == familyName)
			return;

		ClearFontCache();

		_cachedTypefaceName = familyName;
		_cachedBoldTypeface = SKTypeface.FromFamilyName(familyName, SKFontStyle.Bold);
		_ownsCachedBoldTypeface = _cachedBoldTypeface != null;
		var titleTypeface = _cachedBoldTypeface ?? defaultTypeface;
		_cachedTitleFont = new SKFont(titleTypeface, TitleFontSize) { Subpixel = true, Edging = SKFontEdging.SubpixelAntialias };
		_cachedMessageFont = new SKFont(defaultTypeface, MessageFontSize) { Subpixel = true, Edging = SKFontEdging.SubpixelAntialias };
		_cachedActionFont = new SKFont(defaultTypeface, ActionFontSize) { Subpixel = true, Edging = SKFontEdging.SubpixelAntialias };
	}

	private void ClearFontCache()
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


	public NotificationToast(
		string title,
		string message,
		NotificationKind kind,
		int durationMs,
		bool showProgressBar,
		float? progress,
		NotificationToastThemeMode themeMode,
		NotificationToastPalette? customPalette,
		NotificationAction[]? actions = null)
	{
		Title = title;
		Message = message;
		Kind = kind;
		DurationMs = durationMs;
		_themeMode = themeMode;
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
		ColorScheme.ThemeChanged += OnThemeChanged;

		_visibilityAnimation = new AnimationManager(true)
		{
			AnimationType = AnimationType.CubicEaseOut,
			InterruptAnimation = true,
			Increment = Math.Max(0.01, 16d / EnterDurationMs),
			SecondaryIncrement = Math.Max(0.01, 16d / ExitDurationMs),
		};
		_visibilityAnimation.OnAnimationProgress += HandleVisibilityAnimationProgress;
		_visibilityAnimation.OnAnimationFinished += HandleVisibilityAnimationFinished;

		_countdownAnimation = new AnimationManager(true)
		{
			AnimationType = AnimationType.Linear,
			InterruptAnimation = true,
			Increment = DurationMs > 0 ? Math.Max(0.0001, 16d / DurationMs) : 1d,
			SecondaryIncrement = DurationMs > 0 ? Math.Max(0.0001, 16d / DurationMs) : 1d,
		};
		_countdownAnimation.OnAnimationProgress += HandleCountdownAnimationProgress;
		_countdownAnimation.OnAnimationFinished += HandleCountdownAnimationFinished;

		_slideAnimation = new AnimationManager(true)
		{
			AnimationType = AnimationType.CubicEaseOut,
			InterruptAnimation = true,
			Increment = Math.Max(0.01, 16d / 150d), // 150ms sliding up
			SecondaryIncrement = Math.Max(0.01, 16d / 150d),
		};
		_slideAnimation.OnAnimationProgress += HandleSlideAnimationProgress;

		LayoutInteractiveElements();

		_remainingMs = durationMs;
		_visualState = ToastVisualState.Entering;
	}

	internal void Place(float targetX, float targetY)
	{
		_targetX = targetX;
		_targetY = targetY;
		_startY = targetY;
		_hasBeenPlaced = true;
		SetAnimatedOpacity(0f);
		SetAnimatedLocation(new SKPoint(targetX, targetY));
		_remainingMs = DurationMs;
		_visualState = ToastVisualState.Entering;
		_visibilityAnimation.AnimationType = AnimationType.CubicEaseOut;
		_visibilityAnimation.Increment = Math.Max(0.01, 16d / EnterDurationMs);
		_visibilityAnimation.SecondaryIncrement = _visibilityAnimation.Increment;
		_visibilityAnimation.SetProgress(0d);
		_visibilityAnimation.StartNewAnimation(AnimationDirection.In);
	}


	internal void SnapTo(float newTargetY)
	{
		_startY = newTargetY;
		_targetY = newTargetY;
		_slideAnimation.Stop();
		SetAnimatedLocation(new SKPoint(_targetX, newTargetY));
	}

	internal void MoveTo(float newTargetY)
	{
		if (Math.Abs(newTargetY - _targetY) < 0.5f)
			return;

		_startY = Location.Y;
		_targetY = newTargetY;

		if (_visualState == ToastVisualState.Steady && !_visibilityAnimation.IsAnimating())
		{
			_slideAnimation.SetProgress(0d);
			_slideAnimation.StartNewAnimation(AnimationDirection.In);
		}
		else
		{
			// If it's already entering or exiting, we just update targetY and it will glide there.
		}
	}

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
		_visualState = ToastVisualState.Exiting;
		_visibilityAnimation.AnimationType = AnimationType.CubicEaseIn;
		_visibilityAnimation.Increment = Math.Max(0.01, 16d / ExitDurationMs);
		_visibilityAnimation.SecondaryIncrement = _visibilityAnimation.Increment;
		_visibilityAnimation.StartNewAnimation(AnimationDirection.Out);
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

		Invalidate();
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
		base.OnPaint(canvas);

		var width = (float)Width;
		var height = (float)Height;
		var accent = GetAccentColor();

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

	private void OnThemeChanged(object? sender, EventArgs e)
	{
		if (_themeMode != NotificationToastThemeMode.Auto)
			return;

		ApplyTheme();
		Invalidate();
	}

	private void ApplyTheme()
	{
		_palette = ResolvePalette();

		BackColor = _palette.BackgroundColor;
		//Border = new Thickness(2);
		ForeColor = _palette.ForegroundColor;
		//BorderColor = background.BlendWith(ForeColor, 0.5f).WithAlpha(150);
		Radius = CardRadius;
		//Shadow = new BoxShadow(0f, 0, BaseShadowPadding, 0, ColorScheme.ShadowColor);
	}

	private void DrawIconBadge(SKCanvas canvas, SKColor accent)
	{
		var centerX = PaddingH + IconRadius;
		var centerY = PaddingV + (1f * ScaleFactor) + IconRadius;
		var badgeRect = new SKRect(centerX - IconRadius, centerY - IconRadius, centerX + IconRadius, centerY + IconRadius);

		using var fillPaint = new SKPaint { IsAntialias = true, Style = SKPaintStyle.Fill, Color = accent.WithAlpha(22) };
		using var strokePaint = new SKPaint { IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = MathF.Max(1f, ScaleFactor), Color = accent.WithAlpha(82) };
		using var iconPaint = new SKPaint
		{
			IsAntialias = true,
			Style = SKPaintStyle.Stroke,
			StrokeWidth = 1.8f * ScaleFactor,
			StrokeCap = SKStrokeCap.Round,
			StrokeJoin = SKStrokeJoin.Round,
			Color = accent,
		};

		canvas.DrawRoundRect(BadgeRadius.ToRoundRect(badgeRect), fillPaint);
		canvas.DrawRoundRect(BadgeRadius.ToRoundRect(badgeRect), strokePaint);

		switch (Kind)
		{
			case NotificationKind.Success:
				DrawCheckIcon(canvas, centerX, centerY, iconPaint);
				break;
			case NotificationKind.Warning:
				DrawWarningIcon(canvas, centerX, centerY, iconPaint);
				break;
			case NotificationKind.Error:
				DrawErrorIcon(canvas, centerX, centerY, iconPaint);
				break;
			default:
				DrawInfoIcon(canvas, centerX, centerY, iconPaint);
				break;
		}
	}

	private void DrawCheckIcon(SKCanvas canvas, float centerX, float centerY, SKPaint iconPaint)
	{
		var leftX = 5.4f * ScaleFactor;
		var midX = 1.2f * ScaleFactor;
		var lowY = 4.6f * ScaleFactor;
		var offsetY = 0.5f * ScaleFactor;

		using var path = new SKPath();
		path.MoveTo(centerX - leftX, centerY + offsetY);
		path.LineTo(centerX - midX, centerY + lowY);
		path.LineTo(centerX + leftX, centerY - lowY);
		canvas.DrawPath(path, iconPaint);
	}

	private void DrawWarningIcon(SKCanvas canvas, float centerX, float centerY, SKPaint iconPaint)
	{
		var lineTop = 5.2f * ScaleFactor;
		var lineBottom = 1f * ScaleFactor;
		var dotOffset = 5.2f * ScaleFactor;
		var dotRadius = 1.5f * ScaleFactor;

		canvas.DrawLine(centerX, centerY - lineTop, centerX, centerY + lineBottom, iconPaint);
		using var fillPaint = new SKPaint
		{
			IsAntialias = true,
			Style = SKPaintStyle.Fill,
			Color = iconPaint.Color,
			StrokeCap = iconPaint.StrokeCap,
			StrokeJoin = iconPaint.StrokeJoin,
		};
		canvas.DrawCircle(centerX, centerY + dotOffset, dotRadius, fillPaint);
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

		using var fillPaint = new SKPaint
		{
			IsAntialias = true,
			Style = SKPaintStyle.Fill,
			Color = iconPaint.Color,
			StrokeCap = iconPaint.StrokeCap,
			StrokeJoin = iconPaint.StrokeJoin,
		};
		canvas.DrawCircle(centerX, centerY - dotOffset, dotRadius, fillPaint);
		canvas.DrawLine(centerX, centerY - lineTop, centerX, centerY + lineBottom, iconPaint);
	}

	private void DrawTitle(SKCanvas canvas)
	{
		var titleTop = PaddingV;
		var titleRect = new SKRect(ContentLeft, titleTop, ContentRight, titleTop + TitleLineHeight);
		using var textPaint = new SKPaint { IsAntialias = true, Style = SKPaintStyle.Fill, Color = ForeColor };
		TextRenderer.DrawText(canvas, Title, titleRect, textPaint, _cachedTitleFont!, ContentAlignment.MiddleLeft, autoEllipsis: true);
	}

	private void DrawMessage(SKCanvas canvas, float height)
	{
		var messageTop = PaddingV + TitleLineHeight + TitleMessageGap;
		var reservedBottom = PaddingV + ProgressHeight + (_actions.Length > 0 ? ActionTopGap + ActionHeight : 0);
		var messageBottom = height - reservedBottom;
		if (messageBottom <= messageTop)
			return;

		using var textPaint = new SKPaint { IsAntialias = true, Style = SKPaintStyle.Fill, Color = ForeColor };
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
			textPaint,
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
		using var fillPaint = new SKPaint { IsAntialias = true, Style = SKPaintStyle.Fill };
		using var strokePaint = new SKPaint { IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = MathF.Max(1f, ScaleFactor) };
		using var textPaint = new SKPaint { IsAntialias = true, Style = SKPaintStyle.Fill };

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

			fillPaint.Color = background;
			strokePaint.Color = borderColor;
			textPaint.Color = foreground;

			var rr = ActionRadius.ToRoundRect(rect);
			canvas.DrawRoundRect(rr, fillPaint);
			canvas.DrawRoundRect(rr, strokePaint);
			TextRenderer.DrawText(canvas, _actions[i].Label, rect, textPaint, _cachedActionFont!, ContentAlignment.MiddleCenter, autoEllipsis: false);
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

		using var fillPaint = new SKPaint { IsAntialias = true, Style = SKPaintStyle.Fill, Color = background };
		using var outlinePaint = new SKPaint { IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = MathF.Max(1f, ScaleFactor), Color = outline };
		using var linePaint = new SKPaint
		{
			IsAntialias = true,
			Style = SKPaintStyle.Stroke,
			StrokeWidth = 1.55f * ScaleFactor,
			StrokeCap = SKStrokeCap.Round,
			Color = foreground,
		};

		var centerX = _closeRect.MidX;
		var centerY = _closeRect.MidY;
		var radius = Math.Max(6f * ScaleFactor, (_closeRect.Width / 2f) - (2f * ScaleFactor));
		canvas.DrawCircle(centerX, centerY, radius, fillPaint);
		if (outline != SKColors.Transparent)
			canvas.DrawCircle(centerX, centerY, radius - 0.5f, outlinePaint);

		var delta = 3.9f * ScaleFactor;
		canvas.DrawLine(centerX - delta, centerY - delta, centerX + delta, centerY + delta, linePaint);
		canvas.DrawLine(centerX + delta, centerY - delta, centerX - delta, centerY + delta, linePaint);
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

		using var fillPaint = new SKPaint { IsAntialias = true, Style = SKPaintStyle.Fill, Color = accent.WithAlpha(168) };
		canvas.DrawRect(barRect, fillPaint);
	}

	private void HandleSlideAnimationProgress(object _)
	{
		var progress = (float)_slideAnimation.GetProgress();
		var currentY = _startY + (_targetY - _startY) * progress;

		if (SetAnimatedLocation(new SKPoint(_targetX, currentY)))
			Invalidate();
	}

	private void HandleVisibilityAnimationProgress(object _)
	{
		var progress = (float)_visibilityAnimation.GetProgress();

		var currentY = _targetY;
		if (_slideAnimation.IsAnimating())
		{
			var slideProgress = (float)_slideAnimation.GetProgress();
			currentY = _startY + (_targetY - _startY) * slideProgress;
		}

		var changed = false;
		changed |= SetAnimatedOpacity(progress);
		changed |= SetAnimatedLocation(new SKPoint(_targetX, currentY));

		if (changed)
			Invalidate();
	}

	private void HandleVisibilityAnimationFinished(object _)
	{
		if (_visualState == ToastVisualState.Entering)
		{
			_visualState = ToastVisualState.Steady;
			var changed = false;
			changed |= SetAnimatedOpacity(1f);
			changed |= SetAnimatedLocation(new SKPoint(_targetX, _targetY));
			ResumeCountdownIfNeeded();
			if (changed)
				Invalidate();
			return;
		}

		if (_visualState == ToastVisualState.Exiting)
		{
			_hasBeenPlaced = false;
			SetAnimatedOpacity(0f);

			if (DismissCompleted != null)
			{
				DismissCompleted.Invoke(this, EventArgs.Empty);
				return;
			}

			Visible = false;
		}
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
		if (_dismissState != 0 || _countdownPaused || DurationMs <= 0)
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

	private ToastPalette ResolvePalette()
	{
		var resolvedMode = ResolveThemeMode();
		var palette = resolvedMode == NotificationToastThemeMode.Custom
			? _customPalette ?? throw new InvalidOperationException("Custom notification theme requires a CustomPalette.")
			: NotificationToastPalette.FromKind(Kind, resolvedMode);

		return new ToastPalette(palette.BackgroundColor, palette.AccentColor, palette.ForegroundColor);
	}

	private NotificationToastThemeMode ResolveThemeMode()
	{
		return _themeMode switch
		{
			NotificationToastThemeMode.Auto => ColorScheme.IsDarkMode ? NotificationToastThemeMode.Dark : NotificationToastThemeMode.Light,
			_ => _themeMode
		};
	}

	private SKColor GetBackgroundColor() => _palette.BackgroundColor;

	private SKColor GetAccentColor() => _palette.AccentColor;

	private SKColor GetTextColor() => _palette.ForegroundColor;

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
			ClearFontCache();
			ColorScheme.ThemeChanged -= OnThemeChanged;
			_countdownAnimation.OnAnimationProgress -= HandleCountdownAnimationProgress;
			_countdownAnimation.OnAnimationFinished -= HandleCountdownAnimationFinished;
			_countdownAnimation.Dispose();
			_visibilityAnimation.OnAnimationProgress -= HandleVisibilityAnimationProgress;
			_visibilityAnimation.OnAnimationFinished -= HandleVisibilityAnimationFinished;
			_visibilityAnimation.Dispose();

			_slideAnimation.OnAnimationProgress -= HandleSlideAnimationProgress;
			_slideAnimation.Dispose();

			DetachHandle();
		}

		base.Dispose(disposing);
	}
}
