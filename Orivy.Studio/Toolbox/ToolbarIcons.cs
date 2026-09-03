using SkiaSharp;
using System;

namespace Orivy.Studio.Toolbox;

/// <summary>
/// A tiny library of flat, uniform-stroke line icons (SF Symbols-style) for the toolbar and panel
/// headers, drawn directly with SkiaSharp paths on a normalized 24×24 grid. Keeps the shell icon-first
/// instead of text-button-heavy, without pulling in an image asset pipeline.
/// </summary>
internal static class ToolbarIcons
{
    /// <summary>
    /// Rasterizes a glyph into a standalone <see cref="SKImage"/> at <paramref name="pixelSize"/> —
    /// for hosts that need a bitmap rather than a live paint callback (e.g. <c>TabView</c> page icons,
    /// which read <c>ElementBase.Image</c> instead of calling back into application paint code).
    /// </summary>
    public static SKImage CreateImage(string name, float pixelSize, SKColor color, float strokeWidth = 1.6f)
    {
        var size = Math.Max(1, (int)MathF.Ceiling(pixelSize));
        using var surface = SKSurface.Create(new SKImageInfo(size, size, SKColorType.Rgba8888, SKAlphaType.Premul));
        var canvas = surface.Canvas;
        canvas.Clear(SKColors.Transparent);

        using var paint = new SKPaint
        {
            IsAntialias = true,
            Style = SKPaintStyle.Stroke,
            StrokeCap = SKStrokeCap.Round,
            StrokeJoin = SKStrokeJoin.Round,
            StrokeWidth = strokeWidth,
            Color = color,
        };
        Draw(canvas, name, new SKRect(0, 0, size, size), paint);
        canvas.Flush();
        return surface.Snapshot();
    }

    public static void Draw(SKCanvas canvas, string name, SKRect bounds, SKPaint paint)
    {
        var size = Math.Min(bounds.Width, bounds.Height);
        var scale = size / 24f;
        var ox = bounds.MidX - size / 2f;
        var oy = bounds.MidY - size / 2f;

        var saved = canvas.Save();
        canvas.Translate(ox, oy);
        canvas.Scale(scale);

        switch (name)
        {
            case "undo": Undo(canvas, paint); break;
            case "redo": Redo(canvas, paint); break;
            case "new-doc": NewDoc(canvas, paint); break;
            case "new": New(canvas, paint); break;
            case "open": Open(canvas, paint); break;
            case "save": Save(canvas, paint); break;
            case "export": Export(canvas, paint); break;
            case "play": Play(canvas, paint); break;
            case "stop": Stop(canvas, paint); break;
            case "zoom-in": ZoomIn(canvas, paint); break;
            case "zoom-out": ZoomOut(canvas, paint); break;
            case "zoom-fit": ZoomFit(canvas, paint); break;
            case "grid": Grid(canvas, paint); break;
            case "snap": Snap(canvas, paint); break;
            case "guides": Guides(canvas, paint); break;
            case "moon": Moon(canvas, paint); break;
            case "toolbox": ToolboxGlyph(canvas, paint); break;
            case "sliders": Sliders(canvas, paint); break;
            case "layout": Layout(canvas, paint); break;
            case "layers": Layers(canvas, paint); break;
            case "bring-front": BringFront(canvas, paint); break;
            case "send-back": SendBack(canvas, paint); break;
            case "design-view": DesignView(canvas, paint); break;
            case "code-view": CodeView(canvas, paint); break;
            case "explorer": Explorer(canvas, paint); break;
            case "folder": Folder(canvas, paint); break;
            case "file": FileGlyph(canvas, paint); break;
            case "group": Group(canvas, paint); break;
            case "eye": Eye(canvas, paint); break;
            case "lock": Lock(canvas, paint); break;
            case "shuffle": Shuffle(canvas, paint); break;
        }

        canvas.RestoreToCount(saved);
    }

    // Each icon is drawn on the 24×24 grid, roughly inset by 3-4 units so strokes never clip.

    private static void Undo(SKCanvas c, SKPaint p)
    {
        using var path = new SKPath();
        path.MoveTo(9, 8); path.LineTo(4.5f, 12); path.LineTo(9, 16);
        c.DrawPath(path, p);
        using var arc = new SKPath();
        arc.MoveTo(4.5f, 12); arc.LineTo(14, 12);
        arc.ArcTo(new SKPoint(19.5f, 12), new SKPoint(19.5f, 17.5f), 5.5f);
        c.DrawPath(arc, p);
    }

    private static void Redo(SKCanvas c, SKPaint p)
    {
        using var path = new SKPath();
        path.MoveTo(15, 8); path.LineTo(19.5f, 12); path.LineTo(15, 16);
        c.DrawPath(path, p);
        using var arc = new SKPath();
        arc.MoveTo(19.5f, 12); arc.LineTo(10, 12);
        arc.ArcTo(new SKPoint(4.5f, 12), new SKPoint(4.5f, 17.5f), 5.5f);
        c.DrawPath(arc, p);
    }

    private static void NewDoc(SKCanvas c, SKPaint p)
    {
        var r = new SKRect(5, 4, 16, 20);
        c.DrawRoundRect(r, 1.5f, 1.5f, p);
        c.DrawLine(19, 9, 19, 15, p);
        c.DrawLine(16, 12, 22, 12, p);
    }

    private static void New(SKCanvas c, SKPaint p)
    {
        using var doc = new SKPath();
        doc.MoveTo(6, 3); doc.LineTo(14, 3); doc.LineTo(19, 8); doc.LineTo(19, 21); doc.LineTo(6, 21); doc.Close();
        c.DrawPath(doc, p);
        using var fold = new SKPath();
        fold.MoveTo(14, 3); fold.LineTo(14, 8); fold.LineTo(19, 8);
        c.DrawPath(fold, p);
    }

    private static void Open(SKCanvas c, SKPaint p)
    {
        using var path = new SKPath();
        path.MoveTo(3.5f, 8); path.LineTo(3.5f, 19); path.LineTo(20.5f, 19); path.LineTo(20.5f, 9.5f); path.LineTo(11, 9.5f); path.LineTo(9, 6.5f); path.LineTo(3.5f, 6.5f); path.Close();
        c.DrawPath(path, p);
    }

    private static void Save(SKCanvas c, SKPaint p)
    {
        using var body = new SKPath();
        body.MoveTo(5, 4); body.LineTo(16, 4); body.LineTo(19.5f, 7.5f); body.LineTo(19.5f, 20); body.LineTo(5, 20); body.Close();
        c.DrawPath(body, p);
        c.DrawRect(new SKRect(8, 4, 15, 9.5f), p);
        c.DrawRect(new SKRect(7.5f, 13, 16.5f, 20), p);
    }

    private static void Export(SKCanvas c, SKPaint p)
    {
        using var path = new SKPath();
        path.MoveTo(12, 15); path.LineTo(12, 3.5f);
        path.MoveTo(7.5f, 8); path.LineTo(12, 3.5f); path.LineTo(16.5f, 8);
        c.DrawPath(path, p);
        using var tray = new SKPath();
        tray.MoveTo(4.5f, 14); tray.LineTo(4.5f, 20); tray.LineTo(19.5f, 20); tray.LineTo(19.5f, 14);
        c.DrawPath(tray, p);
    }

    private static void Play(SKCanvas c, SKPaint p)
    {
        var fill = new SKPaint { IsAntialias = true, Style = SKPaintStyle.Fill, Color = p.Color };
        using var tri = new SKPath();
        tri.MoveTo(7.5f, 4.5f); tri.LineTo(19, 12); tri.LineTo(7.5f, 19.5f); tri.Close();
        c.DrawPath(tri, fill);
        fill.Dispose();
    }

    private static void Stop(SKCanvas c, SKPaint p)
    {
        var fill = new SKPaint { IsAntialias = true, Style = SKPaintStyle.Fill, Color = p.Color };
        c.DrawRoundRect(new SKRect(6, 6, 18, 18), 3f, 3f, fill);
        fill.Dispose();
    }

    private static void ZoomIn(SKCanvas c, SKPaint p)
    {
        c.DrawCircle(10.5f, 10.5f, 6.5f, p);
        c.DrawLine(15.3f, 15.3f, 20.5f, 20.5f, p);
        c.DrawLine(7.5f, 10.5f, 13.5f, 10.5f, p);
        c.DrawLine(10.5f, 7.5f, 10.5f, 13.5f, p);
    }

    private static void ZoomOut(SKCanvas c, SKPaint p)
    {
        c.DrawCircle(10.5f, 10.5f, 6.5f, p);
        c.DrawLine(15.3f, 15.3f, 20.5f, 20.5f, p);
        c.DrawLine(7.5f, 10.5f, 13.5f, 10.5f, p);
    }

    private static void ZoomFit(SKCanvas c, SKPaint p)
    {
        DrawCorner(c, p, 4, 4, 1, 1);
        DrawCorner(c, p, 20, 4, -1, 1);
        DrawCorner(c, p, 4, 20, 1, -1);
        DrawCorner(c, p, 20, 20, -1, -1);

        static void DrawCorner(SKCanvas c, SKPaint p, float x, float y, float dx, float dy)
        {
            using var path = new SKPath();
            path.MoveTo(x + dx * 5.5f, y);
            path.LineTo(x, y);
            path.LineTo(x, y + dy * 5.5f);
            c.DrawPath(path, p);
        }
    }

    private static void Grid(SKCanvas c, SKPaint p)
    {
        c.DrawRoundRect(new SKRect(4, 4, 20, 20), 2f, 2f, p);
        c.DrawLine(4, 12, 20, 12, p);
        c.DrawLine(12, 4, 12, 20, p);
    }

    private static void Snap(SKCanvas c, SKPaint p)
    {
        // Horseshoe magnet, built from straight legs + a cubic bottom (avoids ArcTo's sweep-direction
        // ambiguity, which previously rendered this as a pinched flag shape instead of a magnet).
        using var path = new SKPath();
        path.MoveTo(7, 4);
        path.LineTo(7, 12);
        path.CubicTo(7, 16.5f, 9.4f, 18.5f, 12, 18.5f);
        path.CubicTo(14.6f, 18.5f, 17, 16.5f, 17, 12);
        path.LineTo(17, 4);
        c.DrawPath(path, p);
        c.DrawLine(7, 4, 10.2f, 4, p);
        c.DrawLine(13.8f, 4, 17, 4, p);
    }

    private static void Guides(SKCanvas c, SKPaint p)
    {
        c.DrawRoundRect(new SKRect(4, 4, 20, 20), 2f, 2f, p);
        for (var i = 1; i < 4; i++)
        {
            var x = 4 + 16f * i / 4f;
            c.DrawLine(x, 4, x, i % 2 == 0 ? 9 : 7, p);
            c.DrawLine(x, 20, x, i % 2 == 0 ? 15 : 17, p);
        }
    }

    private static void Moon(SKCanvas c, SKPaint p)
    {
        var fill = new SKPaint { IsAntialias = true, Style = SKPaintStyle.Fill, Color = p.Color };
        using var path = new SKPath();
        path.AddCircle(12, 12, 8);
        using var bite = new SKPath();
        bite.AddCircle(16, 9, 7);
        using var crescent = path.Op(bite, SKPathOp.Difference);
        c.DrawPath(crescent, fill);
        fill.Dispose();
    }

    private static void ToolboxGlyph(SKCanvas c, SKPaint p)
    {
        DrawSquare(c, p, 4, 4);
        DrawSquare(c, p, 14, 4);
        DrawSquare(c, p, 4, 14);
        DrawSquare(c, p, 14, 14);

        static void DrawSquare(SKCanvas c, SKPaint p, float x, float y) =>
            c.DrawRoundRect(new SKRect(x, y, x + 6, y + 6), 1.5f, 1.5f, p);
    }

    private static void Sliders(SKCanvas c, SKPaint p)
    {
        c.DrawLine(5, 7, 19, 7, p);
        c.DrawLine(5, 12, 19, 12, p);
        c.DrawLine(5, 17, 19, 17, p);
        var fill = new SKPaint { IsAntialias = true, Style = SKPaintStyle.Fill, Color = p.Color };
        c.DrawCircle(9, 7, 2f, fill);
        c.DrawCircle(16, 12, 2f, fill);
        c.DrawCircle(11, 17, 2f, fill);
        fill.Dispose();
    }

    private static void Layout(SKCanvas c, SKPaint p)
    {
        c.DrawRoundRect(new SKRect(4, 4, 20, 20), 2f, 2f, p);
        c.DrawLine(10, 4, 10, 20, p);
    }

    /// <summary>A little wireframe "page" — the design/canvas view.</summary>
    private static void DesignView(SKCanvas c, SKPaint p)
    {
        c.DrawRoundRect(new SKRect(4, 3, 20, 21), 2f, 2f, p);
        c.DrawLine(4, 8.5f, 20, 8.5f, p);
        c.DrawLine(9, 8.5f, 9, 21, p);
    }

    /// <summary>"&lt;/&gt;" — the generated-code view.</summary>
    private static void CodeView(SKCanvas c, SKPaint p)
    {
        using var left = new SKPath();
        left.MoveTo(9.5f, 6); left.LineTo(4, 12); left.LineTo(9.5f, 18);
        c.DrawPath(left, p);

        using var right = new SKPath();
        right.MoveTo(14.5f, 6); right.LineTo(20, 12); right.LineTo(14.5f, 18);
        c.DrawPath(right, p);
    }

    /// <summary>A small file tree — the project/solution explorer view.</summary>
    private static void Explorer(SKCanvas c, SKPaint p)
    {
        c.DrawLine(5, 4, 5, 20, p);
        c.DrawLine(5, 7, 10, 7, p);
        c.DrawLine(5, 13, 10, 13, p);
        c.DrawLine(5, 19, 10, 19, p);
        c.DrawRoundRect(new SKRect(12, 4.5f, 20, 9.5f), 1.5f, 1.5f, p);
        c.DrawRoundRect(new SKRect(12, 10.5f, 20, 15.5f), 1.5f, 1.5f, p);
        c.DrawRoundRect(new SKRect(12, 16.5f, 20, 21f), 1.5f, 1.5f, p);
    }

    private static void Folder(SKCanvas c, SKPaint p)
    {
        using var path = new SKPath();
        path.MoveTo(4, 7); path.LineTo(9.5f, 7); path.LineTo(11, 9); path.LineTo(20, 9);
        path.LineTo(20, 18); path.LineTo(4, 18); path.Close();
        c.DrawPath(path, p);
    }

    private static void FileGlyph(SKCanvas c, SKPaint p)
    {
        using var path = new SKPath();
        path.MoveTo(6, 3.5f); path.LineTo(14, 3.5f); path.LineTo(18, 7.5f); path.LineTo(18, 20.5f);
        path.LineTo(6, 20.5f); path.Close();
        c.DrawPath(path, p);
        c.DrawLine(14, 3.5f, 14, 7.5f, p);
        c.DrawLine(14, 7.5f, 18, 7.5f, p);
    }

    /// <summary>An open eye — visibility toggle, matching the Layers panel's visibility column.</summary>
    private static void Eye(SKCanvas c, SKPaint p)
    {
        using var path = new SKPath();
        path.MoveTo(3, 12);
        path.CubicTo(new SKPoint(6.5f, 6), new SKPoint(17.5f, 6), new SKPoint(21, 12));
        path.CubicTo(new SKPoint(17.5f, 18), new SKPoint(6.5f, 18), new SKPoint(3, 12));
        path.Close();
        c.DrawPath(path, p);
        c.DrawCircle(12, 12, 2.6f, p);
    }

    /// <summary>A padlock — the Layers panel's lock-toggle column and the canvas's locked-control badge.</summary>
    private static void Lock(SKCanvas c, SKPaint p)
    {
        c.DrawRoundRect(new SKRect(5.5f, 11, 18.5f, 20), 2f, 2f, p);
        using var shackle = new SKPath();
        shackle.MoveTo(8, 11);
        shackle.LineTo(8, 8);
        shackle.ArcTo(new SKPoint(12, 4), new SKPoint(16, 8), 4);
        shackle.LineTo(16, 11);
        c.DrawPath(shackle, p);
    }

    /// <summary>Two crossing diagonal paths with arrowheads — the standard media-player "shuffle"
    /// glyph, reused here for "randomize".</summary>
    private static void Shuffle(SKCanvas c, SKPaint p)
    {
        using var top = new SKPath();
        top.MoveTo(3.5f, 7f);
        top.LineTo(20.5f, 17f);
        c.DrawPath(top, p);
        using var topArrow = new SKPath();
        topArrow.MoveTo(15.5f, 17f);
        topArrow.LineTo(20.5f, 17f);
        topArrow.LineTo(20.5f, 12.5f);
        c.DrawPath(topArrow, p);

        using var bottom = new SKPath();
        bottom.MoveTo(3.5f, 17f);
        bottom.LineTo(20.5f, 7f);
        c.DrawPath(bottom, p);
        using var bottomArrow = new SKPath();
        bottomArrow.MoveTo(15.5f, 7f);
        bottomArrow.LineTo(20.5f, 7f);
        bottomArrow.LineTo(20.5f, 11.5f);
        c.DrawPath(bottomArrow, p);
    }

    /// <summary>A dashed bounding box around two child shapes — a control group / nesting container.</summary>
    private static void Group(SKCanvas c, SKPaint p)
    {
        using var dashed = new SKPaint { IsAntialias = true, Style = p.Style, StrokeWidth = p.StrokeWidth, StrokeCap = p.StrokeCap, StrokeJoin = p.StrokeJoin, Color = p.Color, PathEffect = SKPathEffect.CreateDash(new[] { 2.5f, 2.5f }, 0f) };
        c.DrawRoundRect(new SKRect(3.5f, 3.5f, 20.5f, 20.5f), 3f, 3f, dashed);
        c.DrawRoundRect(new SKRect(7, 7, 13, 13), 1.5f, 1.5f, p);
        c.DrawRoundRect(new SKRect(13.5f, 13.5f, 19.5f, 19.5f), 1.5f, 1.5f, p);
    }

    /// <summary>Two overlapping squares — the frontmost one solid, matching "bring to front".</summary>
    private static void BringFront(SKCanvas c, SKPaint p)
    {
        using var dim = new SKPaint { IsAntialias = true, Style = p.Style, StrokeWidth = p.StrokeWidth, StrokeCap = p.StrokeCap, StrokeJoin = p.StrokeJoin, Color = p.Color.WithAlpha((byte)(p.Color.Alpha * 0.4f)) };
        c.DrawRoundRect(new SKRect(4, 4, 15, 15), 2f, 2f, dim);
        c.DrawRoundRect(new SKRect(9, 9, 20, 20), 2f, 2f, p);
    }

    /// <summary>Two overlapping squares — the backmost one solid, matching "send to back".</summary>
    private static void SendBack(SKCanvas c, SKPaint p)
    {
        using var dim = new SKPaint { IsAntialias = true, Style = p.Style, StrokeWidth = p.StrokeWidth, StrokeCap = p.StrokeCap, StrokeJoin = p.StrokeJoin, Color = p.Color.WithAlpha((byte)(p.Color.Alpha * 0.4f)) };
        c.DrawRoundRect(new SKRect(9, 9, 20, 20), 2f, 2f, dim);
        c.DrawRoundRect(new SKRect(4, 4, 15, 15), 2f, 2f, p);
    }

    private static void Layers(SKCanvas c, SKPaint p)
    {
        DrawDiamond(c, p, 12, 4.5f);
        DrawDiamond(c, p, 12, 10.5f);
        DrawDiamond(c, p, 12, 16.5f);

        static void DrawDiamond(SKCanvas c, SKPaint p, float cx, float cy)
        {
            using var path = new SKPath();
            path.MoveTo(cx, cy - 3.4f);
            path.LineTo(cx + 8, cy);
            path.LineTo(cx, cy + 3.4f);
            path.LineTo(cx - 8, cy);
            path.Close();
            c.DrawPath(path, p);
        }
    }
}
