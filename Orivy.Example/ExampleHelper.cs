using SkiaSharp;
using System;

namespace Orivy.Example;

internal enum ExampleIconKind
{
    Healthy,
    Warning,
    Locked,
    Pulse
}

internal static class ExampleHelper
{
    internal static SKImage CreateIcon(SKColor accent, ExampleIconKind kind)
    {
        const float designSize = 18f;
        var info = new SKImageInfo(24, 24);
        using var surface = SKSurface.Create(info);
        var canvas = surface.Canvas;
        canvas.Clear(SKColors.Transparent);

        var scale = Math.Min(info.Width, info.Height) / designSize;
        var offsetX = (info.Width - (designSize * scale)) * 0.5f;
        var offsetY = (info.Height - (designSize * scale)) * 0.5f;
        var saveCount = canvas.Save();
        canvas.Translate(offsetX, offsetY);
        canvas.Scale(scale, scale);

        using var fill = new SKPaint { IsAntialias = true, Style = SKPaintStyle.Fill, Color = accent };
        using var stroke = new SKPaint { IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 1.8f, StrokeCap = SKStrokeCap.Round, Color = SKColors.White.WithAlpha(230) };

        switch (kind)
        {
            case ExampleIconKind.Healthy:
                canvas.DrawCircle(9f, 9f, 7f, fill);
                canvas.DrawLine(5.3f, 9.1f, 8f, 11.7f, stroke);
                canvas.DrawLine(8f, 11.7f, 13.2f, 6.4f, stroke);
                break;

            case ExampleIconKind.Warning:
                using (var path = new SKPath())
                {
                    path.MoveTo(9f, 2.5f);
                    path.LineTo(16f, 15.5f);
                    path.LineTo(2f, 15.5f);
                    path.Close();
                    canvas.DrawPath(path, fill);
                }

                canvas.DrawLine(9f, 6f, 9f, 10f, stroke);
                using (var dotPaint = new SKPaint { IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeCap = SKStrokeCap.Round, StrokeWidth = 1.8f, Color = SKColors.White.WithAlpha(220) })
                    canvas.DrawPoint(9f, 12.7f, dotPaint);
                break;

            case ExampleIconKind.Locked:
                canvas.DrawRoundRect(new SKRect(4.2f, 8f, 13.8f, 15f), 2.2f, 2.2f, fill);
                using (var path = new SKPath())
                {
                    path.MoveTo(5.8f, 8f);
                    path.ArcTo(new SKRect(5.8f, 3.2f, 12.2f, 9.8f), 180f, -180f, false);
                    canvas.DrawPath(path, stroke);
                }

                break;

            default:
                canvas.DrawCircle(9f, 9f, 7f, fill);
                using (var pulse = new SKPaint { IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 1.6f, Color = SKColors.White.WithAlpha(220) })
                using (var path = new SKPath())
                {
                    path.MoveTo(3.2f, 9.1f);
                    path.LineTo(6.2f, 9.1f);
                    path.LineTo(7.8f, 6.2f);
                    path.LineTo(10.1f, 12.1f);
                    path.LineTo(11.6f, 8.6f);
                    path.LineTo(14.8f, 8.6f);
                    canvas.DrawPath(path, pulse);
                }

                break;
        }

        canvas.RestoreToCount(saveCount);
        return surface.Snapshot();
    }
}
