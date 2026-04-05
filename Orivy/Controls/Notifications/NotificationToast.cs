using Orivy.Extensions;
using Orivy.Helpers;
using SkiaSharp;
using System;
using System.Threading;
using SystemTimer = System.Timers.Timer;
using TimerElapsedEventArgs = System.Timers.ElapsedEventArgs;

namespace Orivy.Controls;

public sealed class NotificationToast : ElementBase
{
	internal const int ToastWidth = 468;

	private const int PaddingH = 20;
	private const int PaddingV = 18;
	private const int CloseSize = 30;
	private const int CloseRightGap = 12;
	private const int IconDiameter = 44;
	private const int IconRadius = 22;
	private const int IconGap = 14;
	private const int KindFontSize = 12;
	private const int TitleFontSize = 16;
	private const int MessageFontSize = 14;
	private const int ActionFontSize = 13;
	private const int KindChipHeight = 22;
	private const int KindChipPadding = 10;
	private const int KindTitleGap = 8;
	private const int TitleLineHeight = 24;
	private const int MessageLineHeight = 21;
	private const int TitleMessageGap = 8;
	private const int ActionTopGap = 14;
	private const int ActionHeight = 34;
	private const int ActionHorizontalPadding = 14;
	private const int ActionGap = 8;
	private const int ProgressHeight = 4;
	private const int MaxMessageLines = 5;
	private const float EnterDurationMs = 150f;
	private const float ExitDurationMs = 110f;
	private const float EnterOffset = 22f;

	private const float ContentLeft = PaddingH + IconDiameter + IconGap;
	private const float ContentRight = ToastWidth - PaddingH - CloseSize - CloseRightGap;
	private const float ContentWidth = ContentRight - ContentLeft;

	private readonly SystemTimer _frameTimer;
	private readonly SKTypeface _boldTypeface;
	private readonly SKFont _titleFont;
	private readonly SKFont _kindFont;
	private readonly SKFont _messageFont;
	private readonly SKFont _actionFont;
	private readonly SKPaint _fillPaint;
	private readonly SKPaint _textPaint;
	private readonly SKPaint _iconPaint;
	private readonly NotificationAction[] _actions;
	private readonly float[] _actionWidths;
	private readonly SKRect[] _actionRects;
	private readonly float _kindChipWidth;
	private readonly string _kindLabel;

	internal float _targetX;
	internal float _targetY;

	private float _animationElapsedMs;
	private float _remainingMs;
	private float? _manualProgress;
	private int _dismissState;
	private bool _closeHovered;
	private int _hoveredAction = -1;
	private bool _countdownPaused;
	private bool _showProgressBar;
	private SKRect _closeRect;
	private NotificationHandle? _handle;
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

		_kindLabel = kind switch
		{
			NotificationKind.Success => "SUCCESS",
			NotificationKind.Warning => "WARNING",
			NotificationKind.Error => "ERROR",
			_ => "INFO",
		};

		var defaultTypeface = Application.DefaultFont.Typeface;
		_boldTypeface = SKTypeface.FromFamilyName(
			defaultTypeface?.FamilyName ?? "Segoe UI",
			SKFontStyle.Bold) ?? defaultTypeface ?? SKTypeface.Default;

		_kindFont = new SKFont(_boldTypeface, KindFontSize) { Subpixel = true, Edging = SKFontEdging.SubpixelAntialias };
		_titleFont = new SKFont(_boldTypeface, TitleFontSize) { Subpixel = true, Edging = SKFontEdging.SubpixelAntialias };
		_messageFont = new SKFont(defaultTypeface, MessageFontSize) { Subpixel = true, Edging = SKFontEdging.SubpixelAntialias };
		_actionFont = new SKFont(defaultTypeface, ActionFontSize) { Subpixel = true, Edging = SKFontEdging.SubpixelAntialias };

		_fillPaint = new SKPaint { IsAntialias = true, Style = SKPaintStyle.Fill };
		_textPaint = new SKPaint { IsAntialias = true, Style = SKPaintStyle.Fill };
		_iconPaint = new SKPaint { IsAntialias = true, StrokeCap = SKStrokeCap.Round, StrokeJoin = SKStrokeJoin.Round };

		var kindSize = TextRenderer.MeasureText(
			_kindLabel,
			_kindFont,
			new SKSize(short.MaxValue, KindChipHeight),
			new TextRenderOptions { Wrap = TextWrap.None, MaxWidth = short.MaxValue });
		_kindChipWidth = (float)Math.Ceiling(kindSize.Width) + KindChipPadding * 2;

		_actionWidths = new float[_actions.Length];
		_actionRects = new SKRect[_actions.Length];
		for (var i = 0; i < _actions.Length; i++)
		{
			var measured = TextRenderer.MeasureText(
				_actions[i].Label,
				_actionFont,
				new SKSize(short.MaxValue, ActionHeight),
				new TextRenderOptions { Wrap = TextWrap.None, MaxWidth = short.MaxValue });
			_actionWidths[i] = (float)Math.Ceiling(measured.Width) + ActionHorizontalPadding * 2;
		}

		var height = ComputeToastHeight(message, _actions.Length > 0);

		Size = new SKSize(ToastWidth, height);
		BackColor = SKColors.Transparent;
		Border = new Thickness(1);
		BorderColor = ColorScheme.Outline.WithAlpha(82);
		Radius = new Radius(20);
		Shadows = new[]
		{
			new BoxShadow(0f, 8f, 22f, 0, ColorScheme.ShadowColor.WithAlpha(26)),
			new BoxShadow(0f, 18f, 36f, 0, ColorScheme.ShadowColor.WithAlpha(18)),
		};
		ZOrder = 9999;
		Opacity = 0f;

		_remainingMs = durationMs;
		_visualState = ToastVisualState.Entering;
		_frameTimer = new SystemTimer(16) { AutoReset = true };
		_frameTimer.Elapsed += OnFrameElapsed;
	}

	internal void Place(float targetX, float targetY)
	{
		_targetX = targetX;
		_targetY = targetY;
		Location = new SKPoint(targetX + EnterOffset, targetY);
		_animationElapsedMs = 0f;
		_visualState = ToastVisualState.Entering;
		_frameTimer.Start();
	}

	internal void MoveTo(float newTargetY)
	{
		if (Math.Abs(newTargetY - _targetY) < 0.5f)
			return;

		_targetY = newTargetY;
		Location = new SKPoint(Location.X, newTargetY);
		Invalidate();
	}

	internal void BeginDismiss()
	{
		if (Interlocked.CompareExchange(ref _dismissState, 1, 0) != 0)
			return;

		_animationElapsedMs = 0f;
		_visualState = ToastVisualState.Exiting;
		_frameTimer.Start();
	}

	internal override void OnMouseEnter(EventArgs e)
	{
		base.OnMouseEnter(e);

		if (_dismissState == 0)
			_countdownPaused = true;
	}

	internal override void OnMouseLeave(EventArgs e)
	{
		base.OnMouseLeave(e);

		_closeHovered = false;
		_hoveredAction = -1;

		if (_dismissState == 0)
			_countdownPaused = false;

		Invalidate();
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

	internal override void OnMouseMove(MouseEventArgs e)
	{
		base.OnMouseMove(e);

		var point = new SKPoint(e.X, e.Y);
		var previousClose = _closeHovered;
		var previousAction = _hoveredAction;

		_closeHovered = _closeRect.Contains(point);
		_hoveredAction = -1;
		for (var i = 0; i < _actionRects.Length; i++)
		{
			if (_actionRects[i].Contains(point))
			{
				_hoveredAction = i;
				break;
			}
		}

		if (previousClose != _closeHovered || previousAction != _hoveredAction)
			Invalidate();
	}

	protected internal override void OnMouseClick(MouseEventArgs e)
	{
		base.OnMouseClick(e);

		if (e.Button != MouseButtons.Left)
			return;

		var point = new SKPoint(e.X, e.Y);

		if (_closeRect.Contains(point))
		{
			OnDismissWithoutAction?.Invoke();
			BeginDismiss();
			return;
		}

		for (var i = 0; i < _actionRects.Length; i++)
		{
			if (!_actionRects[i].Contains(point))
				continue;

			_actions[i].Invoke();
			BeginDismiss();
			return;
		}
	}

	public override void OnPaint(SKCanvas canvas)
	{
		var width = (float)Width;
		var height = (float)Height;
		var accent = GetAccentColor();

		DrawCardSurface(canvas, width, height, accent);
		DrawIconBadge(canvas, accent);
		DrawKindChip(canvas, accent);
		DrawTitle(canvas);
		DrawMessage(canvas, height);

		if (_actions.Length > 0)
		{
			DrawFooterDivider(canvas, height, accent);
			DrawActions(canvas, height, accent);
		}

		DrawCloseButton(canvas);
		if (_showProgressBar)
			DrawProgress(canvas, width, height, accent);
	}

	private void DrawCardSurface(SKCanvas canvas, float width, float height, SKColor accent)
	{
		var surfacePaint = new SKPaint
		{
			IsAntialias = true,
			Style = SKPaintStyle.Fill,
			Color = MixWithSurface(accent),
		};

		var topWashPaint = new SKPaint
		{
			IsAntialias = true,
			Style = SKPaintStyle.Fill,
			Color = accent.WithAlpha(18),
		};

		var edgePaint = new SKPaint
		{
			IsAntialias = true,
			Style = SKPaintStyle.Fill,
			Color = accent.WithAlpha(110),
		};

		canvas.DrawRect(new SKRect(0, 0, width, height), surfacePaint);
		canvas.DrawRoundRect(new Radius(20).ToRoundRect(new SKRect(0, 0, width, 54f)), topWashPaint);
		canvas.DrawRoundRect(new Radius(3).ToRoundRect(new SKRect(22f, 0, width - 22f, 3f)), edgePaint);

		surfacePaint.Dispose();
		topWashPaint.Dispose();
		edgePaint.Dispose();
	}

	private void DrawIconBadge(SKCanvas canvas, SKColor accent)
	{
		var centerX = PaddingH + IconRadius;
		var centerY = PaddingV + IconRadius;

		_fillPaint.Style = SKPaintStyle.Fill;
		_fillPaint.Color = accent.WithAlpha(28);
		canvas.DrawCircle(centerX, centerY, IconRadius, _fillPaint);

		_fillPaint.Color = accent.WithAlpha(72);
		canvas.DrawCircle(centerX, centerY, IconRadius - 6f, _fillPaint);

		_iconPaint.Color = accent;
		_iconPaint.StrokeWidth = 2.4f;

		switch (Kind)
		{
			case NotificationKind.Success:
				DrawCheckIcon(canvas, centerX, centerY);
				break;
			case NotificationKind.Warning:
				DrawWarningIcon(canvas, centerX, centerY);
				break;
			case NotificationKind.Error:
				DrawErrorIcon(canvas, centerX, centerY);
				break;
			default:
				DrawInfoIcon(canvas, centerX, centerY);
				break;
		}
	}

	private void DrawKindChip(SKCanvas canvas, SKColor accent)
	{
		var chipRect = new SKRect(ContentLeft, PaddingV, ContentLeft + _kindChipWidth, PaddingV + KindChipHeight);
		var rr = new Radius(9).ToRoundRect(chipRect);

		_fillPaint.Style = SKPaintStyle.Fill;
		_fillPaint.Color = accent.WithAlpha(26);
		canvas.DrawRoundRect(rr, _fillPaint);

		_fillPaint.Style = SKPaintStyle.Stroke;
		_fillPaint.StrokeWidth = 1f;
		_fillPaint.Color = accent.WithAlpha(110);
		canvas.DrawRoundRect(rr, _fillPaint);

		_textPaint.Color = accent;
		TextRenderer.DrawText(canvas, _kindLabel, chipRect, _textPaint, _kindFont, ContentAlignment.MiddleCenter, autoEllipsis: false);
	}

	private void DrawCheckIcon(SKCanvas canvas, float centerX, float centerY)
	{
		_iconPaint.Style = SKPaintStyle.Stroke;
		using var path = new SKPath();
		path.MoveTo(centerX - 6.5f, centerY + 0.5f);
		path.LineTo(centerX - 1.5f, centerY + 5.5f);
		path.LineTo(centerX + 6.5f, centerY - 5.5f);
		canvas.DrawPath(path, _iconPaint);
	}

	private void DrawWarningIcon(SKCanvas canvas, float centerX, float centerY)
	{
		_iconPaint.Style = SKPaintStyle.Stroke;
		canvas.DrawLine(centerX, centerY - 6f, centerX, centerY + 1f, _iconPaint);
		_iconPaint.Style = SKPaintStyle.Fill;
		canvas.DrawCircle(centerX, centerY + 6f, 1.8f, _iconPaint);
	}

	private void DrawErrorIcon(SKCanvas canvas, float centerX, float centerY)
	{
		_iconPaint.Style = SKPaintStyle.Stroke;
		const float delta = 5.5f;
		canvas.DrawLine(centerX - delta, centerY - delta, centerX + delta, centerY + delta, _iconPaint);
		canvas.DrawLine(centerX + delta, centerY - delta, centerX - delta, centerY + delta, _iconPaint);
	}

	private void DrawInfoIcon(SKCanvas canvas, float centerX, float centerY)
	{
		_iconPaint.Style = SKPaintStyle.Fill;
		canvas.DrawCircle(centerX, centerY - 5.5f, 1.8f, _iconPaint);
		_iconPaint.Style = SKPaintStyle.Stroke;
		canvas.DrawLine(centerX, centerY - 1f, centerX, centerY + 6f, _iconPaint);
	}

	private void DrawTitle(SKCanvas canvas)
	{
		var titleTop = PaddingV + KindChipHeight + KindTitleGap;
		var titleRect = new SKRect(ContentLeft, titleTop, ContentRight, titleTop + TitleLineHeight);
		_textPaint.Color = ColorScheme.ForeColor;
		TextRenderer.DrawText(canvas, Title, titleRect, _textPaint, _titleFont, ContentAlignment.MiddleLeft, autoEllipsis: true);
	}

	private void DrawMessage(SKCanvas canvas, float height)
	{
		var messageTop = PaddingV + KindChipHeight + KindTitleGap + TitleLineHeight + TitleMessageGap;
		var reservedBottom = PaddingV + ProgressHeight + (_actions.Length > 0 ? ActionTopGap + ActionHeight : 0);
		var messageBottom = height - reservedBottom;
		if (messageBottom <= messageTop)
			return;

		_textPaint.Color = ColorScheme.ForeColor.WithAlpha(190);
		var baselineY = messageTop + 4f - _messageFont.Metrics.Ascent;
		TextRenderer.DrawText(
			canvas,
			Message,
			ContentLeft,
			baselineY,
			SKTextAlign.Left,
			_messageFont,
			_textPaint,
			new TextRenderOptions
			{
				Wrap = TextWrap.WordWrap,
				MaxWidth = ContentWidth,
				MaxHeight = messageBottom - messageTop,
			});
	}

	private void DrawFooterDivider(SKCanvas canvas, float height, SKColor accent)
	{
		var dividerY = height - PaddingV - ProgressHeight - ActionHeight - (ActionTopGap * 0.55f);
		_fillPaint.Style = SKPaintStyle.Fill;
		_fillPaint.Color = accent.WithAlpha(46);
		canvas.DrawRect(new SKRect(ContentLeft, dividerY, ContentRight, dividerY + 1f), _fillPaint);
	}

	private void DrawActions(SKCanvas canvas, float height, SKColor accent)
	{
		var actionY = height - PaddingV - ProgressHeight - ActionHeight;
		var totalWidth = 0f;

		for (var i = 0; i < _actions.Length; i++)
		{
			if (i > 0)
				totalWidth += ActionGap;
			totalWidth += _actionWidths[i];
		}

		var x = ContentRight - totalWidth;

		for (var i = 0; i < _actions.Length; i++)
		{
			var width = _actionWidths[i];
			var rect = new SKRect(x, actionY, x + width, actionY + ActionHeight);
			_actionRects[i] = rect;

			var roundRect = new Radius(9).ToRoundRect(rect);
			var hovered = _hoveredAction == i;
			var primary = _actions[i].IsPrimary;

			if (primary)
			{
				_fillPaint.Style = SKPaintStyle.Fill;
				_fillPaint.Color = hovered ? accent.Brightness(0.05f) : accent;
				canvas.DrawRoundRect(roundRect, _fillPaint);
				_textPaint.Color = SKColors.White;
			}
			else
			{
				_fillPaint.Style = SKPaintStyle.Fill;
				_fillPaint.Color = hovered ? ColorScheme.SurfaceVariant.WithAlpha(220) : ColorScheme.SurfaceVariant.WithAlpha(132);
				canvas.DrawRoundRect(roundRect, _fillPaint);

				_fillPaint.Style = SKPaintStyle.Stroke;
				_fillPaint.StrokeWidth = 1f;
				_fillPaint.Color = hovered ? accent.WithAlpha(170) : ColorScheme.Outline.WithAlpha(120);
				canvas.DrawRoundRect(roundRect, _fillPaint);
				_textPaint.Color = ColorScheme.ForeColor;
			}

			TextRenderer.DrawText(canvas, _actions[i].Label, rect, _textPaint, _actionFont, ContentAlignment.MiddleCenter, autoEllipsis: false);
			x += width + ActionGap;
		}
	}

	private void DrawCloseButton(SKCanvas canvas)
	{
		var centerX = ToastWidth - CloseRightGap - CloseSize / 2f;
		var centerY = PaddingV + KindChipHeight + (KindTitleGap * 0.5f);
		var half = CloseSize / 2f;

		_closeRect = new SKRect(centerX - half, centerY - half, centerX + half, centerY + half);

		if (_closeHovered)
		{
			_fillPaint.Style = SKPaintStyle.Fill;
			_fillPaint.Color = ColorScheme.Outline.WithAlpha(44);
			canvas.DrawCircle(centerX, centerY, half, _fillPaint);
		}

		_iconPaint.Style = SKPaintStyle.Stroke;
		_iconPaint.StrokeWidth = 1.8f;
		_iconPaint.Color = _closeHovered ? ColorScheme.ForeColor : ColorScheme.ForeColor.WithAlpha(150);

		const float delta = 4.8f;
		canvas.DrawLine(centerX - delta, centerY - delta, centerX + delta, centerY + delta, _iconPaint);
		canvas.DrawLine(centerX + delta, centerY - delta, centerX - delta, centerY + delta, _iconPaint);
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

		_fillPaint.Style = SKPaintStyle.Fill;
		_fillPaint.Color = accent.WithAlpha(145);
		canvas.DrawRect(barRect, _fillPaint);
	}

	private void OnFrameElapsed(object? sender, TimerElapsedEventArgs e)
	{
		const float dt = 16f;

		switch (_visualState)
		{
			case ToastVisualState.Entering:
				_animationElapsedMs += dt;
				var enterT = Math.Clamp(_animationElapsedMs / EnterDurationMs, 0f, 1f);
				var enterP = EaseOutCubic(enterT);
				Location = new SKPoint(_targetX + (1f - enterP) * EnterOffset, _targetY);
				Opacity = enterP;
				if (enterT >= 1f)
				{
					_visualState = ToastVisualState.Steady;
					Location = new SKPoint(_targetX, _targetY);
					Opacity = 1f;
				}
				break;

			case ToastVisualState.Steady:
				Location = new SKPoint(_targetX, _targetY);
				Opacity = 1f;
				if (DurationMs > 0 && !_countdownPaused)
				{
					_remainingMs -= dt;
					if (_remainingMs <= 0f)
					{
						_remainingMs = 0f;
						OnDismissWithoutAction?.Invoke();
						BeginDismiss();
					}
				}
				break;

			case ToastVisualState.Exiting:
				_animationElapsedMs += dt;
				var exitT = Math.Clamp(_animationElapsedMs / ExitDurationMs, 0f, 1f);
				var exitP = EaseInCubic(exitT);
				Location = new SKPoint(_targetX + exitP * EnterOffset, _targetY);
				Opacity = 1f - exitP;
				if (exitT >= 1f)
				{
					_frameTimer.Stop();
					Visible = false;
					DismissCompleted?.Invoke(this, EventArgs.Empty);
					return;
				}
				break;
		}

		if (_visualState == ToastVisualState.Steady && DurationMs <= 0 && !_showProgressBar)
			_frameTimer.Stop();

		Invalidate();
	}

	private SKColor GetAccentColor() => Kind switch
	{
		NotificationKind.Success => new SKColor(34, 197, 94),
		NotificationKind.Warning => new SKColor(234, 179, 8),
		NotificationKind.Error => ColorScheme.Error,
		_ => ColorScheme.Primary,
	};

	private SKColor MixWithSurface(SKColor accent)
	{
		var surface = ColorScheme.SurfaceContainerHigh;
		var factor = Kind == NotificationKind.Info ? 0.03f : 0.10f;

		return new SKColor(
			(byte)Math.Clamp(surface.Red + (accent.Red - surface.Red) * factor, 0, 255),
			(byte)Math.Clamp(surface.Green + (accent.Green - surface.Green) * factor, 0, 255),
			(byte)Math.Clamp(surface.Blue + (accent.Blue - surface.Blue) * factor, 0, 255),
			255);
	}

	private static int ComputeToastHeight(string message, bool hasActions)
	{
		var lines = EstimateMessageLines(message);
		var contentHeight = KindChipHeight + KindTitleGap + TitleLineHeight + TitleMessageGap + lines * MessageLineHeight;
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
			if (lineLength > 34)
			{
				lines++;
				lineLength = 1;
			}
		}

		return Math.Min(lines, MaxMessageLines);
	}

	private static float EaseOutCubic(float t)
	{
		var inv = 1f - t;
		return 1f - inv * inv * inv;
	}

	private static float EaseInCubic(float t)
	{
		return t * t * t;
	}

	protected override void Dispose(bool disposing)
	{
		if (disposing)
		{
			_frameTimer.Stop();
			_frameTimer.Elapsed -= OnFrameElapsed;
			_frameTimer.Dispose();

			DetachHandle();
			_kindFont.Dispose();
			_titleFont.Dispose();
			_messageFont.Dispose();
			_actionFont.Dispose();
			_boldTypeface.Dispose();
			_fillPaint.Dispose();
			_textPaint.Dispose();
			_iconPaint.Dispose();
		}

		base.Dispose(disposing);
	}
}
