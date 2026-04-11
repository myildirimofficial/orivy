using SkiaSharp;
using System;

namespace Orivy.Controls;

public sealed class NotificationToastPalette
{
	public NotificationToastPalette(SKColor backgroundColor, SKColor accentColor, SKColor foregroundColor)
	{
		BackgroundColor = backgroundColor;
		AccentColor = accentColor;
		ForegroundColor = foregroundColor;
	}

	public SKColor BackgroundColor { get; }

	public SKColor AccentColor { get; }

	public SKColor ForegroundColor { get; }

	public NotificationToastPalette WithForeground(SKColor foregroundColor)
		=> new(BackgroundColor, AccentColor, foregroundColor);

	public NotificationToastPalette WithAccent(SKColor accentColor)
		=> new(BackgroundColor, accentColor, ForegroundColor);

	public NotificationToastPalette WithBackground(SKColor backgroundColor)
		=> new(backgroundColor, AccentColor, ForegroundColor);

	public static NotificationToastPalette FromKind(NotificationKind kind, NotificationToastThemeMode mode)
	{
		var resolvedMode = mode switch
		{
			NotificationToastThemeMode.Auto => throw new ArgumentException("Auto mode must be resolved before requesting a preset palette.", nameof(mode)),
			NotificationToastThemeMode.Custom => throw new ArgumentException("Custom mode requires an explicit palette.", nameof(mode)),
			_ => mode
		};

		return resolvedMode == NotificationToastThemeMode.Dark
			? CreateDarkPalette(kind)
			: CreateLightPalette(kind);
	}

	private static NotificationToastPalette CreateDarkPalette(NotificationKind kind)
	{
		return kind switch
		{
			NotificationKind.Info => new NotificationToastPalette(new SKColor(30, 58, 138), new SKColor(96, 165, 250), new SKColor(219, 234, 254)),
			NotificationKind.Success => new NotificationToastPalette(new SKColor(22, 101, 52), new SKColor(74, 222, 128), SKColors.White),
			NotificationKind.Warning => new NotificationToastPalette(new SKColor(113, 63, 18), new SKColor(251, 191, 36), SKColors.White),
			NotificationKind.Error => new NotificationToastPalette(new SKColor(127, 29, 29), new SKColor(248, 113, 113), SKColors.White),
			_ => new NotificationToastPalette(new SKColor(39, 39, 42), new SKColor(96, 165, 250), SKColors.White)
		};
	}

	private static NotificationToastPalette CreateLightPalette(NotificationKind kind)
	{
		return kind switch
		{
			NotificationKind.Info => new NotificationToastPalette(new SKColor(219, 234, 254), new SKColor(37, 99, 235), new SKColor(23, 37, 84)),
			NotificationKind.Success => new NotificationToastPalette(new SKColor(220, 252, 231), new SKColor(22, 163, 74), new SKColor(22, 78, 45)),
			NotificationKind.Warning => new NotificationToastPalette(new SKColor(254, 243, 199), new SKColor(180, 130, 0), new SKColor(113, 63, 18)),
			NotificationKind.Error => new NotificationToastPalette(new SKColor(254, 226, 226), new SKColor(220, 38, 38), new SKColor(127, 29, 29)),
			_ => new NotificationToastPalette(SKColors.White, ColorScheme.Primary, ColorScheme.ForeColor)
		};
	}
}