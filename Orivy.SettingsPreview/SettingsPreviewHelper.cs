using Orivy.Controls;
using SkiaSharp;
using System;

namespace Orivy.SettingsPreview;

internal enum PreviewIconKind
{
    Home,
    System,
    Bluetooth,
    Network,
    Personalization,
    Apps,
    Accounts,
    Privacy,
    Update,
    Storage,
    Display,
    Security
}

internal static class SettingsPreviewHelper
{
    public static SKImage CreateIcon(SKColor accent, PreviewIconKind kind, int size = 28)
    {
        var info = new SKImageInfo(size, size);
        using var surface = SKSurface.Create(info);
        var canvas = surface.Canvas;
        canvas.Clear(SKColors.Transparent);

        using var backPaint = new SKPaint { IsAntialias = true, Style = SKPaintStyle.Fill, Color = accent.WithAlpha(30) };
        using var paint = new SKPaint { IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeCap = SKStrokeCap.Round, StrokeJoin = SKStrokeJoin.Round, StrokeWidth = Math.Max(1.8f, size / 12f), Color = accent };
        using var fillPaint = new SKPaint { IsAntialias = true, Style = SKPaintStyle.Fill, Color = accent };
        using var path = new SKPath();

        var s = size;
        canvas.DrawRoundRect(SKRect.Create(1, 1, s - 2, s - 2), s * .28f, s * .28f, backPaint);

        switch (kind)
        {
            case PreviewIconKind.Home:
                path.MoveTo(s * .22f, s * .50f);
                path.LineTo(s * .50f, s * .25f);
                path.LineTo(s * .78f, s * .50f);
                path.MoveTo(s * .31f, s * .48f);
                path.LineTo(s * .31f, s * .76f);
                path.LineTo(s * .69f, s * .76f);
                path.LineTo(s * .69f, s * .48f);
                canvas.DrawPath(path, paint);
                break;
            case PreviewIconKind.System:
            case PreviewIconKind.Display:
                canvas.DrawRoundRect(SKRect.Create(s * .23f, s * .28f, s * .54f, s * .34f), 3, 3, paint);
                canvas.DrawLine(s * .50f, s * .63f, s * .50f, s * .78f, paint);
                canvas.DrawLine(s * .36f, s * .79f, s * .64f, s * .79f, paint);
                break;
            case PreviewIconKind.Bluetooth:
                path.MoveTo(s * .45f, s * .18f);
                path.LineTo(s * .67f, s * .36f);
                path.LineTo(s * .39f, s * .60f);
                path.LineTo(s * .45f, s * .66f);
                path.LineTo(s * .67f, s * .84f);
                path.LineTo(s * .45f, s * .66f);
                path.LineTo(s * .45f, s * .18f);
                canvas.DrawPath(path, paint);
                break;
            case PreviewIconKind.Network:
                canvas.DrawArc(SKRect.Create(s * .20f, s * .22f, s * .60f, s * .60f), 220, 100, false, paint);
                canvas.DrawArc(SKRect.Create(s * .32f, s * .38f, s * .36f, s * .36f), 220, 100, false, paint);
                canvas.DrawCircle(s * .50f, s * .76f, s * .045f, fillPaint);
                break;
            case PreviewIconKind.Personalization:
                canvas.DrawRoundRect(SKRect.Create(s * .23f, s * .23f, s * .54f, s * .54f), 5, 5, paint);
                canvas.DrawCircle(s * .64f, s * .34f, s * .06f, fillPaint);
                canvas.DrawLine(s * .30f, s * .65f, s * .45f, s * .51f, paint);
                canvas.DrawLine(s * .45f, s * .51f, s * .57f, s * .62f, paint);
                break;
            case PreviewIconKind.Apps:
                for (var y = 0; y < 2; y++)
                for (var x = 0; x < 2; x++)
                    canvas.DrawRoundRect(SKRect.Create(s * (.27f + x * .25f), s * (.27f + y * .25f), s * .14f, s * .14f), 3, 3, fillPaint);
                break;
            case PreviewIconKind.Accounts:
                canvas.DrawCircle(s * .50f, s * .36f, s * .12f, paint);
                canvas.DrawArc(SKRect.Create(s * .28f, s * .48f, s * .44f, s * .35f), 205, 130, false, paint);
                break;
            case PreviewIconKind.Privacy:
            case PreviewIconKind.Security:
                path.MoveTo(s * .50f, s * .20f);
                path.LineTo(s * .72f, s * .30f);
                path.LineTo(s * .68f, s * .62f);
                path.LineTo(s * .50f, s * .80f);
                path.LineTo(s * .32f, s * .62f);
                path.LineTo(s * .28f, s * .30f);
                path.Close();
                canvas.DrawPath(path, paint);
                break;
            case PreviewIconKind.Update:
                canvas.DrawArc(SKRect.Create(s * .25f, s * .25f, s * .50f, s * .50f), 30, 285, false, paint);
                path.MoveTo(s * .70f, s * .31f);
                path.LineTo(s * .76f, s * .49f);
                path.LineTo(s * .59f, s * .44f);
                canvas.DrawPath(path, paint);
                break;
            case PreviewIconKind.Storage:
                canvas.DrawOval(SKRect.Create(s * .25f, s * .24f, s * .50f, s * .18f), paint);
                canvas.DrawLine(s * .25f, s * .33f, s * .25f, s * .68f, paint);
                canvas.DrawLine(s * .75f, s * .33f, s * .75f, s * .68f, paint);
                canvas.DrawArc(SKRect.Create(s * .25f, s * .59f, s * .50f, s * .18f), 0, 180, false, paint);
                break;
        }

        return surface.Snapshot();
    }

    public static SKImage CreateAvatar(int size = 52)
    {
        var info = new SKImageInfo(size, size);
        using var surface = SKSurface.Create(info);
        var canvas = surface.Canvas;
        canvas.Clear(SKColors.Transparent);
        using var paint = new SKPaint { IsAntialias = true, Style = SKPaintStyle.Fill };

        paint.Color = new SKColor(0x00, 0x78, 0xD4);
        canvas.DrawCircle(size / 2f, size / 2f, size * .48f, paint);
        paint.Color = SKColors.White.WithAlpha(235);
        canvas.DrawCircle(size * .50f, size * .38f, size * .14f, paint);
        canvas.DrawOval(SKRect.Create(size * .27f, size * .54f, size * .46f, size * .24f), paint);
        return surface.Snapshot();
    }
}
