using Orivy.Animation;
using Orivy.Extensions;
using Orivy.Helpers;
using SkiaSharp;
using System;
using System.Threading;

namespace Orivy.Controls;

public sealed class NotificationToast : ElementBase
{
	internal const int ToastWidth = 452;
	private static readonly Radius CardRadius = new(16);
	private static readonly Radius BadgeRadius = new(10);
	private static readonly Radius ActionRadius = new(8);

	private const int PaddingH = 20;
	private const int PaddingV = 18;
	private const int CloseSize = 32;
	private const int CloseRightGap = 12;
	private const int IconDiameter = 36;
	private const int IconRadius = 18;
	private const int IconGap = 14;
	private const float TitleFontSize = 17f;
	private const float MessageFontSize = 15f;
	private const float ActionFontSize = 13.5f;
	private const int TitleLineHeight = 24;
	private const int MessageLineHeight = 22;
	private const int TitleMessageGap = 6;
	private const int ActionTopGap = 14;
	private const int ActionHeight = 34;
	private const int ActionGap = 8;
	private const int ProgressHeight = 4;
	private const int MaxMessageLines = 5;
	private const float EnterDurationMs = 120f;
	private const float ExitDurationMs = 180f;
	private const float EnterOffset = 18f;

	private const float ContentLeft = PaddingH + IconDiameter + IconGap;
	private const float ContentRight = ToastWidth - PaddingH - CloseSize - CloseRightGap;
	private const float ContentWidth = ContentRight - ContentLeft;

	private static SKTypeface? _cachedBoldTypeface;
	private static SKFont? _cachedTitleFont;
	private static SKFont? _cachedMessageFont;
	private static SKFont? _cachedActionFont;
	private static string? _cachedTypefaceName;

	private readonly AnimationManager _countdownAnimation;
	private readonly AnimationManager _visibilityAnimation;
	private readonly AnimationManager _slideAnimation;
	private readonly NotificationAction[] _actions;
	private readonly SKRect[] _actionRects;
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
	private ToastVisualState _visualState;

	internal Action? OnDismissWithoutAction;

	private enum ToastVisualState
	{
		Entering,
		Steady,
		Exiting,
	}


	public string Title { get; }
	public string Message { get; }
	public NotificationKind Kind { get; }
	public int DurationMs { get; }

	internal event EventHandler? DismissCompleted;

	public NotificationToast(
		string title,
		string message,
		NotificationKind kind,
		int durationMs,
		bool showProgressBar,
		float? progress,
		NotificationAction[]? actions = null)
	{
		Title = title;
		Message = message;
		Kind = kind;
		DurationMs = durationMs;
		_showProgressBar = showProgressBar;
		_manualProgress = progress;

		_actions = actions ?? Array.Empty<NotificationAction>();
		if (_actions.Length > 0)
			_actions[0].IsPrimary = true;

		var defaultTypeface = Application.DefaultFont.Typeface;
		var familyName = defaultTypeface?.FamilyName ?? "Segoe UI Variable Display";

		if (_cachedTitleFont == null || _cachedMessageFont == null || _cachedActionFont == null || _cachedTypefaceName != familyName)
		{
			_cachedTitleFont?.Dispose();
			_cachedMessageFont?.Dispose();
			_cachedActionFont?.Dispose();
			_cachedBoldTypeface?.Dispose();

			_cachedTypefaceName = familyName;
			_cachedBoldTypeface = SKTypeface.FromFamilyName(familyName, SKFontStyle.Bold) ?? defaultTypeface ?? SKTypeface.Default;

			_cachedTitleFont = new SKFont(_cachedBoldTypeface, TitleFontSize) { Subpixel = true, Edging = SKFontEdging.SubpixelAntialias };
			_cachedMessageFont = new SKFont(defaultTypeface, MessageFontSize) { Subpixel = true, Edging = SKFontEdging.SubpixelAntialias };
			_cachedActionFont = new SKFont(_cachedBoldTypeface, ActionFontSize) { Subpixel = true, Edging = SKFontEdging.SubpixelAntialias };
		}

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
		SetAnimatedOpacity(1f);
		Location = new SKPoint(targetX + EnterOffset, targetY);
		_remainingMs = DurationMs;
		_visualState = ToastVisualState.Entering;
		_visibilityAnimation.AnimationType = AnimationType.CubicEaseOut;
		_visibilityAnimation.Increment = Math.Max(0.01, 16d / EnterDurationMs);
		_visibilityAnimation.SecondaryIncrement = _visibilityAnimation.Increment;
		_visibilityAnimation.SetProgress(0d);
		_visibilityAnimation.StartNewAnimation(AnimationDirection.In);
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
		var width = (float)Width;
		var height = (float)Height;
		var accent = GetAccentColor();

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
		ApplyTheme();
	}

	private void ApplyTheme()
	{
		var accent = GetAccentColor();
		var mixFactor = ColorScheme.IsDarkMode ? 0.095f : 0.045f;
		var backgroundAlpha = ColorScheme.IsDarkMode ? (byte)236 : (byte)244;
		var borderAlpha = ColorScheme.IsDarkMode ? (byte)76 : (byte)44;
		var shadow = ColorScheme.FlatDesign
			? BoxShadow.None
			: new BoxShadow(0f, 8f, 18f, CardRadius, ColorScheme.ShadowColor.WithAlpha(ColorScheme.IsDarkMode ? (byte)46 : (byte)20));

		BackColor = MixWithSurface(accent, mixFactor, backgroundAlpha);
		Border = new Thickness(1);
		BorderColor = accent.WithAlpha(borderAlpha);
		Radius = CardRadius;
		Shadow = shadow;
	}

	private void DrawIconBadge(SKCanvas canvas, SKColor accent)
	{
		var centerX = PaddingH + IconRadius;
		var centerY = PaddingV + 1f + IconRadius;
		var badgeRect = new SKRect(centerX - IconRadius, centerY - IconRadius, centerX + IconRadius, centerY + IconRadius);

		using var fillPaint = new SKPaint { IsAntialias = true, Style = SKPaintStyle.Fill, Color = accent.WithAlpha(22) };
		using var strokePaint = new SKPaint { IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 1f, Color = accent.WithAlpha(82) };
		using var iconPaint = new SKPaint
		{
			IsAntialias = true,
			Style = SKPaintStyle.Stroke,
			StrokeWidth = 2.1f,
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

	private static void DrawCheckIcon(SKCanvas canvas, float centerX, float centerY, SKPaint iconPaint)
	{
		using var path = new SKPath();
		path.MoveTo(centerX - 6.5f, centerY + 0.5f);
		path.LineTo(centerX - 1.5f, centerY + 5.5f);
		path.LineTo(centerX + 6.5f, centerY - 5.5f);
		canvas.DrawPath(path, iconPaint);
	}

	private static void DrawWarningIcon(SKCanvas canvas, float centerX, float centerY, SKPaint iconPaint)
	{
		canvas.DrawLine(centerX, centerY - 6f, centerX, centerY + 1f, iconPaint);
		using var fillPaint = new SKPaint
		{
			IsAntialias = true,
			Style = SKPaintStyle.Fill,
			Color = iconPaint.Color,
			StrokeCap = iconPaint.StrokeCap,
			StrokeJoin = iconPaint.StrokeJoin,
		};
		canvas.DrawCircle(centerX, centerY + 6f, 1.8f, fillPaint);
	}

	private static void DrawErrorIcon(SKCanvas canvas, float centerX, float centerY, SKPaint iconPaint)
	{
		const float delta = 5.5f;
		canvas.DrawLine(centerX - delta, centerY - delta, centerX + delta, centerY + delta, iconPaint);
		canvas.DrawLine(centerX + delta, centerY - delta, centerX - delta, centerY + delta, iconPaint);
	}

	private static void DrawInfoIcon(SKCanvas canvas, float centerX, float centerY, SKPaint iconPaint)
	{
		using var fillPaint = new SKPaint
		{
			IsAntialias = true,
			Style = SKPaintStyle.Fill,
			Color = iconPaint.Color,
			StrokeCap = iconPaint.StrokeCap,
			StrokeJoin = iconPaint.StrokeJoin,
		};
		canvas.DrawCircle(centerX, centerY - 5.5f, 1.8f, fillPaint);
		canvas.DrawLine(centerX, centerY - 1f, centerX, centerY + 6f, iconPaint);
	}

	private void DrawTitle(SKCanvas canvas)
	{
		var titleTop = PaddingV;
		var titleRect = new SKRect(ContentLeft, titleTop, ContentRight, titleTop + TitleLineHeight);
		using var textPaint = new SKPaint { IsAntialias = true, Style = SKPaintStyle.Fill, Color = ColorScheme.ForeColor };
		TextRenderer.DrawText(canvas, Title, titleRect, textPaint, _cachedTitleFont!, ContentAlignment.MiddleLeft, autoEllipsis: true);
	}

	private void DrawMessage(SKCanvas canvas, float height)
	{
		var messageTop = PaddingV + TitleLineHeight + TitleMessageGap;
		var reservedBottom = PaddingV + ProgressHeight + (_actions.Length > 0 ? ActionTopGap + ActionHeight : 0);
		var messageBottom = height - reservedBottom;
		if (messageBottom <= messageTop)
			return;

		using var textPaint = new SKPaint { IsAntialias = true, Style = SKPaintStyle.Fill, Color = ColorScheme.ForeColor.WithAlpha(184) };
		var baselineY = messageTop + 4f - _cachedMessageFont!.Metrics.Ascent;
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
		using var strokePaint = new SKPaint { IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 1f };
		using var textPaint = new SKPaint { IsAntialias = true, Style = SKPaintStyle.Fill };

		for (var i = 0; i < _actions.Length; i++)
		{
			var rect = _actionRects[i];
			var isPrimary = _actions[i].IsPrimary;
			var hovered = _hoveredAction == i;
			var pressed = _pressedAction == i;

			var background = isPrimary
				? (pressed ? ColorScheme.Primary.Brightness(-0.08f) : hovered ? ColorScheme.Primary.Brightness(0.06f) : ColorScheme.Primary)
				: (pressed ? ColorScheme.SurfaceVariant.WithAlpha(190) : hovered ? ColorScheme.SurfaceVariant.WithAlpha(150) : ColorScheme.SurfaceVariant.WithAlpha(120));

			var borderColor = isPrimary
				? (pressed ? ColorScheme.Primary.Brightness(-0.24f) : hovered ? ColorScheme.Primary.Brightness(-0.08f) : ColorScheme.Primary.Brightness(-0.18f))
				: (pressed ? ColorScheme.Outline.WithAlpha(190) : hovered ? ColorScheme.Outline.WithAlpha(160) : ColorScheme.Outline.WithAlpha(100));

			var foreground = isPrimary ? SKColors.White : ColorScheme.ForeColor;

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
			? (ColorScheme.IsDarkMode ? ColorScheme.SurfaceVariant.WithAlpha(230) : ColorScheme.SurfaceVariant.WithAlpha(200))
			: _closeHovered ? (ColorScheme.IsDarkMode ? ColorScheme.SurfaceVariant.WithAlpha(190) : ColorScheme.SurfaceVariant.WithAlpha(140)) : SKColors.Transparent;
		var foreground = _closeHovered || _closePressed
			? (ColorScheme.IsDarkMode ? ColorScheme.ForeColor : ColorScheme.ForeColor.WithAlpha(200))
			: ColorScheme.ForeColor.WithAlpha(150);
		var outline = _closeHovered || _closePressed
			? (ColorScheme.IsDarkMode ? ColorScheme.Outline.WithAlpha(110) : ColorScheme.Outline.WithAlpha(80))
			: SKColors.Transparent;

		using var fillPaint = new SKPaint { IsAntialias = true, Style = SKPaintStyle.Fill, Color = background };
		using var outlinePaint = new SKPaint { IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 1f, Color = outline };
		using var linePaint = new SKPaint
		{
			IsAntialias = true,
			Style = SKPaintStyle.Stroke,
			StrokeWidth = 1.8f,
			StrokeCap = SKStrokeCap.Round,
			Color = foreground,
		};

		var centerX = _closeRect.MidX;
		var centerY = _closeRect.MidY;
		var radius = _closeRect.Width / 2f;
		canvas.DrawCircle(centerX, centerY, radius, fillPaint);
		if (outline != SKColors.Transparent)
			canvas.DrawCircle(centerX, centerY, radius - 0.5f, outlinePaint);

		const float delta = 5.2f;
		canvas.DrawLine(centerX - delta, centerY - delta, centerX + delta, centerY + delta, linePaint);
		canvas.DrawLine(centerX + delta, centerY - delta, centerX - delta, centerY + delta, linePaint);
	}

	private void LayoutInteractiveElements()
	{
		_closeRect = new SKRect(
			ToastWidth - CloseRightGap - CloseSize,
			PaddingV - 3f,
			ToastWidth - CloseRightGap,
			PaddingV - 3f + CloseSize);

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

	private static float EstimateActionButtonWidth(string? text)
	{
		if (string.IsNullOrWhiteSpace(text))
			return 80f;

		var length = text.Length;
		var estimated = 34f + (length * 9.2f);
		return Math.Clamp(estimated, 80f, 196f);
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
		
		// If visibility is also animating, it might step on _targetX. 
		// But visibility only touches targetY when finished.
		// However, SetAnimatedLocation overrides X entirely when called.
		// Since Visibility animates X, let's keep X steady based on visibility state.
		
		var currentX = _targetX;
		if (_visualState == ToastVisualState.Entering)
		{
			var visProgress = (float)_visibilityAnimation.GetProgress();
			currentX = _targetX + (1f - visProgress) * EnterOffset;
		}
		else if (_visualState == ToastVisualState.Exiting)
		{
			var visProgress = (float)_visibilityAnimation.GetProgress();
			currentX = _targetX + (1f - visProgress) * EnterOffset;
		}

		if (SetAnimatedLocation(new SKPoint(currentX, currentY)))
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
		changed |= SetAnimatedLocation(new SKPoint(_targetX + (1f - progress) * EnterOffset, currentY));

		if (changed)
			Invalidate();
	}

	private void HandleVisibilityAnimationFinished(object _)
	{
		if (_visualState == ToastVisualState.Entering)
		{
			_visualState = ToastVisualState.Steady;
			var changed = SetAnimatedLocation(new SKPoint(_targetX, _targetY));
			ResumeCountdownIfNeeded();
			if (changed)
				Invalidate();
			return;
		}

		if (_visualState == ToastVisualState.Exiting)
		{
			Visible = false;
			DismissCompleted?.Invoke(this, EventArgs.Empty);
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

	private SKColor GetAccentColor() => Kind switch
	{
		NotificationKind.Success => new SKColor(34, 197, 94),
		NotificationKind.Warning => new SKColor(234, 179, 8),
		NotificationKind.Error => ColorScheme.Error,
		_ => ColorScheme.Primary,
	};

	private SKColor MixWithSurface(SKColor accent, float factor, byte alpha)
	{
		var surface = ColorScheme.SurfaceContainerHigh;

		return new SKColor(
			(byte)Math.Clamp(surface.Red + (accent.Red - surface.Red) * factor, 0, 255),
			(byte)Math.Clamp(surface.Green + (accent.Green - surface.Green) * factor, 0, 255),
			(byte)Math.Clamp(surface.Blue + (accent.Blue - surface.Blue) * factor, 0, 255),
			alpha);
	}

	private static int ComputeToastHeight(string message, bool hasActions)
	{
		var lines = EstimateMessageLines(message);
		var contentHeight = TitleLineHeight + TitleMessageGap + lines * MessageLineHeight;
		if (hasActions)
			contentHeight += ActionTopGap + ActionHeight;

		var bodyHeight = Math.Max(contentHeight, IconDiameter);
		return PaddingV + bodyHeight + PaddingV + ProgressHeight;
	}

	private static int EstimateMessageLines(string message)
	{
		if (string.IsNullOrEmpty(message))
			return 1;

		var lines = 1;
		var lineLength = 0;

		for (var i = 0; i < message.Length; i++)
		{
			var c = message[i];
			if (c == '\n')
			{
				lines++;
				lineLength = 0;
				continue;
			}

			lineLength++;
			if (lineLength > 42)
			{
				lines++;
				lineLength = 1;
			}
		}

		return Math.Min(lines, MaxMessageLines);
	}

	protected override void Dispose(bool disposing)
	{
		if (disposing)
		{
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
