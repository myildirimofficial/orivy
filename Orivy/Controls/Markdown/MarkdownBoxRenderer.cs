using System;
using System.Collections.Generic;
using SkiaSharp;

namespace Orivy.Controls.Markdown;

internal static class MarkdownBoxRenderer
{
    // Reusable paints — allocated once per Draw call (not static: SKPaint is not thread-safe)
    // Using structs would be ideal but SKPaint is a class; allocate outside inner loops.
    public static void Draw(
        SKCanvas canvas,
        List<MdBox> boxes,
        float viewTop,
        float viewBottom,
        MarkdownTheme theme,
        MarkdownHoverState hover,
        IMarkdownImageProvider? imageProvider,
        MarkdownSelectionState? selection)
    {
        using var fillPaint   = new SKPaint { IsAntialias = true, Style = SKPaintStyle.Fill };
        using var strokePaint = new SKPaint { IsAntialias = true, Style = SKPaintStyle.Stroke };
        using var textPaint   = new SKPaint { IsAntialias = true, Style = SKPaintStyle.Fill };

        // Pre-compute ordered selection bounds for O(1) per-box check
        bool hasSelection = selection?.HasSelection == true;
        TextPosition selFrom = default, selTo = default;
        if (hasSelection) (selFrom, selTo) = selection!.Ordered();

        // ── Pass 1: all non-CodeOwner boxes ─────────────────────────────
        for (int idx = 0; idx < boxes.Count; idx++)
        {
            var box = boxes[idx];
            if (box.Bounds.Bottom < viewTop || box.Bounds.Top > viewBottom) continue;

            switch (box)
            {
                case RectBox r:
                    DrawRect(canvas, r, fillPaint, strokePaint);
                    break;
                case TextRunBox t:
                    if (t.IsNewlineSentinel) break;
                    if (t.CodeOwner != null)
                    {
                        // Drawn below in the grouped code-owner pass; skip here.
                        // (We batch all runs from the same owner under one clip+translate.)
                        break;
                    }
                    DrawTextRun(canvas, t, idx, theme, hover, textPaint,
                        fillPaint, hasSelection, selFrom, selTo);
                    break;
                case ImageBox img:
                    DrawImage(canvas, img, theme, fillPaint, imageProvider);
                    break;
                case MathFormulaBox math:
                    DrawMathFormula(canvas, math, textPaint, strokePaint);
                    break;
                case CheckboxBox cb:
                    DrawCheckbox(canvas, cb, theme, fillPaint, strokePaint);
                    break;
                case CodeBlockBox code:
                    DrawCodeBlock(canvas, code, theme, hover, fillPaint, strokePaint, textPaint);
                    break;
                case TableBox tbl:
                    DrawTable(canvas, tbl, theme, hover, imageProvider, fillPaint, strokePaint, textPaint, viewTop, viewBottom, hasSelection, selFrom, selTo);
                    break;
                case ContainerBox cb2:
                    // Container children are in the main boxes list — drawn by the main loop already
                    break;
                case AlertHeaderBox alert:
                    DrawAlertIcon(canvas, alert, theme, fillPaint);
                    break;
                case DetailsHeaderBox details:
                    DrawDetailsToggle(canvas, details, theme, fillPaint);
                    break;
            }
        }

        // ── Pass 2: CodeOwner runs grouped by owner (one clip+translate per owner) ──
        CodeBlockBox? currentOwner = null;
        int           ownerSave    = 0;
        for (int idx = 0; idx < boxes.Count; idx++)
        {
            if (boxes[idx] is not TextRunBox t || t.CodeOwner == null || t.IsNewlineSentinel) continue;
            if (t.Bounds.Bottom < viewTop || t.Bounds.Top > viewBottom) continue;

            var owner = t.CodeOwner;
            if (!ReferenceEquals(owner, currentOwner))
            {
                if (currentOwner != null) canvas.RestoreToCount(ownerSave);
                // Quick visibility check: any run from this owner in the view?
                ownerSave    = canvas.Save();
                canvas.ClipRect(owner.BodyRect);
                canvas.Translate(-owner.Scroll.ScrollX, 0f);
                currentOwner = owner;
            }

            // Visibility check in scroll-space
            float visibleLeft  = owner.BodyRect.Left  + owner.Scroll.ScrollX;
            float visibleRight = owner.BodyRect.Right + owner.Scroll.ScrollX;
            if (t.Bounds.Right < visibleLeft || t.Bounds.Left > visibleRight) continue;

            DrawTextRun(canvas, t, idx, theme, hover, textPaint,
                fillPaint, hasSelection, selFrom, selTo);
        }

        if (currentOwner != null) 
            canvas.RestoreToCount(ownerSave);
    }

    private static void DrawRect(SKCanvas canvas, RectBox r, SKPaint fillPaint, SKPaint strokePaint)
    {
        if (r.Fill.HasValue)
        {
            fillPaint.Color = r.Fill.Value;
            if (r.CornerRadius > 0) canvas.DrawRoundRect(r.Bounds, r.CornerRadius, r.CornerRadius, fillPaint);
            else canvas.DrawRect(r.Bounds, fillPaint);
        }
        if (r.Stroke.HasValue)
        {
            strokePaint.Color = r.Stroke.Value;
            strokePaint.StrokeWidth = r.StrokeWidth > 0 ? r.StrokeWidth : 1f;
            if (r.CornerRadius > 0) canvas.DrawRoundRect(r.Bounds, r.CornerRadius, r.CornerRadius, strokePaint);
            else canvas.DrawRect(r.Bounds, strokePaint);
        }
    }

    private static void DrawTextRun(SKCanvas canvas, TextRunBox t, int boxIdx,
        MarkdownTheme theme, MarkdownHoverState hover, SKPaint textPaint, SKPaint fillPaint,
        bool hasSelection, TextPosition selFrom, TextPosition selTo)
    {
        if (string.IsNullOrEmpty(t.Text) || t.Font == null)
            return;

        bool isHoveredLink = t.Link != null && ReferenceEquals(t.Link, hover.HoveredLink);

        // ── Selection highlight ──
        if (hasSelection && boxIdx >= selFrom.BoxIndex && boxIdx <= selTo.BoxIndex)
        {
            int startChar = boxIdx == selFrom.BoxIndex ? selFrom.CharOffset : 0;
            int endChar   = boxIdx == selTo.BoxIndex   ? selTo.CharOffset   : t.Text.Length;
            if (startChar < endChar && endChar <= t.Text.Length && startChar >= 0)
            {
                float selX0 = t.Bounds.Left + t.GetXAtOffset(startChar);
                float selX1 = t.Bounds.Left + t.GetXAtOffset(endChar);
                fillPaint.Color = theme.SelectionBackground;
                canvas.DrawRect(new SKRect(selX0, t.Bounds.Top, selX1, t.Bounds.Bottom), fillPaint);
            }
        }

        // ── Text ──
        textPaint.Color = isHoveredLink ? theme.LinkHoverColor : t.Color;
        textPaint.Style = SKPaintStyle.Fill;
        canvas.DrawText(t.Text, t.Baseline.X, t.Baseline.Y, SKTextAlign.Left, t.Font, textPaint);

        // ── Underline: always for <ins>/++ ; on hover for links ──
        bool drawUnderline = t.Underline && (t.Link == null || isHoveredLink);
        if (drawUnderline)
        {
            float w = t.Font.MeasureText(t.Text);
            float uy = t.Baseline.Y + MathF.Max(1f, t.Font.Size * 0.08f);
            textPaint.StrokeWidth = MathF.Max(1f, t.Font.Size * 0.06f);
            textPaint.Style = SKPaintStyle.Stroke;
            canvas.DrawLine(t.Baseline.X, uy, t.Baseline.X + w, uy, textPaint);
            textPaint.Style = SKPaintStyle.Fill;
        }

    }

    private static void DrawImage(SKCanvas canvas, ImageBox box, MarkdownTheme theme,
        SKPaint fillPaint, IMarkdownImageProvider? provider)
    {
        var img = provider?.TryGetCached(box.Source.Url);
        if (img != null)
        {
            canvas.DrawImage(img, box.Bounds);
        }
        else
        {
            fillPaint.Color = theme.CodeBackground;
            float r = Math.Min(box.Bounds.Width, box.Bounds.Height) * 0.04f;
            canvas.DrawRoundRect(box.Bounds, r, r, fillPaint);
        }
    }

    private static void DrawMathFormula(SKCanvas canvas, MathFormulaBox box, SKPaint textPaint, SKPaint strokePaint)
    {
        textPaint.Style = SKPaintStyle.Fill;
        foreach (var run in box.Runs)
        {
            if (string.IsNullOrEmpty(run.Text) || run.Font == null)
                continue;
            textPaint.Color = run.Color;
            canvas.DrawText(run.Text, run.Baseline.X, run.Baseline.Y, SKTextAlign.Left, run.Font, textPaint);
        }

        strokePaint.Color = box.Color;
        strokePaint.StrokeCap = SKStrokeCap.Round;
        strokePaint.StrokeJoin = SKStrokeJoin.Round;
        foreach (var line in box.Lines)
        {
            strokePaint.StrokeWidth = line.StrokeWidth;
            canvas.DrawLine(line.Start, line.End, strokePaint);
        }

        foreach (var brace in box.Braces)
        {
            strokePaint.StrokeWidth = brace.StrokeWidth;
            using var path = new SKPath();
            var r = brace.Bounds;
            float mid = r.MidY;
            float curl = r.Width * 0.72f;
            path.MoveTo(r.Right, r.Top);
            path.CubicTo(r.Left + curl, r.Top, r.Left + curl, mid - r.Height * 0.12f, r.Left, mid);
            path.CubicTo(r.Left + curl, mid + r.Height * 0.12f, r.Left + curl, r.Bottom, r.Right, r.Bottom);
            canvas.DrawPath(path, strokePaint);
        }

        strokePaint.StrokeCap = SKStrokeCap.Butt;
        strokePaint.StrokeJoin = SKStrokeJoin.Miter;
    }

    private static void DrawCheckbox(SKCanvas canvas, CheckboxBox box, MarkdownTheme theme,
        SKPaint fillPaint, SKPaint strokePaint)
    {
        float r = MathF.Min(box.Bounds.Width, box.Bounds.Height) * 0.22f;
        if (box.Checked)
        {
            fillPaint.Color = theme.CheckboxFillColor;
            canvas.DrawRoundRect(box.Bounds, r, r, fillPaint);
            using var path = new SKPath();
            float w = box.Bounds.Width, h = box.Bounds.Height;
            path.MoveTo(box.Bounds.Left + w * 0.22f, box.Bounds.Top + h * 0.52f);
            path.LineTo(box.Bounds.Left + w * 0.42f, box.Bounds.Top + h * 0.72f);
            path.LineTo(box.Bounds.Left + w * 0.80f, box.Bounds.Top + h * 0.28f);
            strokePaint.Color = theme.CheckmarkColor;
            strokePaint.StrokeWidth = MathF.Max(1.5f, w * 0.12f);
            strokePaint.StrokeCap = SKStrokeCap.Round;
            strokePaint.StrokeJoin = SKStrokeJoin.Round;
            canvas.DrawPath(path, strokePaint);
            strokePaint.StrokeCap = SKStrokeCap.Butt;
            strokePaint.StrokeJoin = SKStrokeJoin.Miter;
        }
        else
        {
            strokePaint.Color = theme.CheckboxBorderColor;
            strokePaint.StrokeWidth = MathF.Max(1f, box.Bounds.Width * 0.08f);
            canvas.DrawRoundRect(box.Bounds, r, r, strokePaint);
        }
    }

    private static void DrawCodeBlock(SKCanvas canvas, CodeBlockBox box, MarkdownTheme theme,
        MarkdownHoverState hover, SKPaint fillPaint, SKPaint strokePaint, SKPaint textPaint)
    {
        float cornerR = theme.CornerRadius;

        // Background
        fillPaint.Color = theme.CodeBackground;
        canvas.DrawRoundRect(box.Bounds, cornerR, cornerR, fillPaint);

        // Header strip (clipped to top of rounded rect)
        int headerClip = canvas.Save();
        canvas.ClipRoundRect(new SKRoundRect(box.Bounds, cornerR, cornerR), antialias: true);
        fillPaint.Color = theme.CodeBlockHeaderBackground;
        canvas.DrawRect(box.HeaderRect, fillPaint);
        canvas.RestoreToCount(headerClip);

        // Copy button
        bool isHoveredBlock  = ReferenceEquals(hover.HoveredCodeBlock, box);
        bool isCopyHovered   = isHoveredBlock && hover.HoveredCopyButton;
        DrawCopyButton(canvas, box.CopyButtonRect, theme, fillPaint, strokePaint, isCopyHovered);

        // Code text (clipped to body, translated by horizontal scroll)
        int bodyClip = canvas.Save();
        canvas.ClipRect(box.BodyRect);
        canvas.Translate(box.BodyOrigin.X - box.Scroll.ScrollX, box.BodyOrigin.Y);
        foreach (var lineRuns in box.Lines)
        {
            foreach (var run in lineRuns)
            {
                textPaint.Color = run.Color;
                canvas.DrawText(run.Text, run.Baseline.X, run.Baseline.Y, SKTextAlign.Left, run.Font, textPaint);
            }
            canvas.Translate(0, box.LineHeight);
        }
        canvas.RestoreToCount(bodyClip);

        // Horizontal scroll thumb
        if (box.NeedsHorizontalScroll && box.ContentWidth > 0.5f)
        {
            float trackW  = box.ViewportWidth;
            float thumbW  = MathF.Max(32f, trackW * MathF.Min(1f, trackW / box.ContentWidth));
            float maxScrl = MathF.Max(1f, box.ContentWidth - trackW);
            float thumbX  = box.BodyOrigin.X + (trackW - thumbW) * (box.Scroll.ScrollX / maxScrl);
            float thumbY  = box.Bounds.Bottom - 6f;
            fillPaint.Color = theme.ScrollIndicatorColor;
            canvas.DrawRoundRect(new SKRect(thumbX, thumbY, thumbX + thumbW, thumbY + 4f), 2f, 2f, fillPaint);
        }
    }

    /// <summary>
    /// A clear, recognizable "copy" icon: two overlapping squares (content behind,
    /// empty page in front). Both are outlines only so no color fill is needed.
    /// </summary>
    private static void DrawCopyButton(SKCanvas canvas, SKRect rect, MarkdownTheme theme,
        SKPaint fillPaint, SKPaint strokePaint, bool hovered)
    {
        if (rect.IsEmpty) return;

        // Optional hover background
        if (hovered)
        {
            fillPaint.Color = theme.BorderColor.WithAlpha(120);
            canvas.DrawRoundRect(rect, 4f, 4f, fillPaint);
        }

        float iconSize = MathF.Min(rect.Width, rect.Height) * 0.52f;
        float cx = rect.MidX - iconSize * 0.06f;
        float cy = rect.MidY + iconSize * 0.06f;
        float sq = iconSize * 0.64f;   // side of each square
        float off = iconSize * 0.28f;  // offset between back and front square

        // Back square (content / source)
        var back  = new SKRect(cx - sq / 2f - off * 0.5f, cy - sq / 2f - off * 0.5f,
                               cx + sq / 2f - off * 0.5f, cy + sq / 2f - off * 0.5f);
        // Front square (blank page / clipboard)
        var front = new SKRect(cx - sq / 2f + off * 0.5f, cy - sq / 2f + off * 0.5f,
                               cx + sq / 2f + off * 0.5f, cy + sq / 2f + off * 0.5f);

        float sw = MathF.Max(1.2f, iconSize * 0.1f);
        strokePaint.StrokeWidth = sw;
        strokePaint.StrokeCap   = SKStrokeCap.Round;
        strokePaint.StrokeJoin  = SKStrokeJoin.Round;

        // Draw back square (outline)
        strokePaint.Color = theme.MutedColor.WithAlpha(180);
        canvas.DrawRoundRect(back, 2.5f, 2.5f, strokePaint);

        // White-fill the front square first so it "occludes" the back square
        fillPaint.Color = theme.CodeBackground;
        canvas.DrawRoundRect(front, 2.5f, 2.5f, fillPaint);

        // Draw front square outline
        strokePaint.Color = hovered ? theme.BodyColor : theme.MutedColor;
        canvas.DrawRoundRect(front, 2.5f, 2.5f, strokePaint);

        // Three "text lines" inside the front square to suggest document content
        strokePaint.Color = strokePaint.Color.WithAlpha(100);
        strokePaint.StrokeWidth = MathF.Max(1f, sw * 0.7f);
        float lineInset = front.Width * 0.18f;
        float lineStep  = front.Height * 0.22f;
        float lx0 = front.Left  + lineInset;
        float lx1 = front.Right - lineInset;
        float ly0 = front.Top   + lineStep;
        for (int k = 0; k < 3; k++)
        {
            float lxEnd = k == 1 ? lx0 + (lx1 - lx0) * 0.65f : lx1; // middle line shorter
            canvas.DrawLine(lx0, ly0 + k * lineStep, lxEnd, ly0 + k * lineStep, strokePaint);
        }

        strokePaint.StrokeCap  = SKStrokeCap.Butt;
        strokePaint.StrokeJoin = SKStrokeJoin.Miter;
    }

    /// <summary>Modern table: rounded outer corners, alternating rows, sticky header, optional scroll.</summary>
    private static void DrawTable(SKCanvas canvas, TableBox tbl, MarkdownTheme theme,
        MarkdownHoverState hover, IMarkdownImageProvider? imageProvider, SKPaint fillPaint, SKPaint strokePaint, SKPaint textPaint,
        float viewTop, float viewBottom, bool hasSelection, TextPosition selFrom, TextPosition selTo)
    {
        float radius = theme.CornerRadius;
        float hair   = 1f;

        int clipSave = canvas.Save();
        canvas.ClipRoundRect(new SKRoundRect(tbl.Bounds, radius, radius), antialias: true);

        // Children have coordinates relative to (0,0) = table content origin.
        // Translate to the table's on-screen top-left, then apply horizontal scroll.
        canvas.Translate(tbl.Bounds.Left - tbl.Scroll.ScrollX, tbl.Bounds.Top);
        foreach (var child in tbl.Children)
        {
            // viewTop/viewBottom are in content-space (before translation), so compare using table-relative Y
            float absTop    = child.Bounds.Top    + tbl.Bounds.Top;
            float absBottom = child.Bounds.Bottom + tbl.Bounds.Top;
            if (absBottom < viewTop || absTop > viewBottom) continue;
            switch (child)
            {
                case RectBox r:  DrawRect(canvas, r, fillPaint, strokePaint); break;
                case TextRunBox t:
                    DrawTextRun(canvas, t, -1, theme, hover, textPaint,
                        fillPaint, hasSelection, selFrom, selTo); break;
                case ImageBox img:
                    DrawImage(canvas, img, theme, fillPaint, imageProvider); break;
                case MathFormulaBox math:
                    DrawMathFormula(canvas, math, textPaint, strokePaint); break;
            }
        }
        canvas.RestoreToCount(clipSave);

        // Outer border
        strokePaint.Color       = theme.TableBorderColor;
        strokePaint.StrokeWidth = hair;
        canvas.DrawRoundRect(tbl.Bounds, radius, radius, strokePaint);

        // Horizontal scroll thumb
        if (tbl.NeedsHorizontalScroll && tbl.ContentWidth > 0.5f)
        {
            float trackW  = tbl.ViewportWidth;
            float maxRaw  = MathF.Max(0f, tbl.ContentWidth - trackW);
            float maxS    = Math.Max(1f, maxRaw);
            float thumbW  = MathF.Max(32f, trackW * MathF.Min(1f, trackW / Math.Max(1f, tbl.ContentWidth)));
            float thumbX  = tbl.Bounds.Left + (trackW - thumbW) * (tbl.Scroll.ScrollX / maxS);
            float thumbY  = tbl.Bounds.Bottom - 6f;
            fillPaint.Color = theme.ScrollIndicatorColor;
            canvas.DrawRoundRect(new SKRect(thumbX, thumbY, thumbX + thumbW, thumbY + 4f), 2f, 2f, fillPaint);
        }
    }

    private static void DrawAlertIcon(SKCanvas canvas, AlertHeaderBox box, MarkdownTheme theme, SKPaint fillPaint)
    {
        var color = box.Kind switch
        {
            AlertKind.Note      => theme.AlertNote,
            AlertKind.Tip       => theme.AlertTip,
            AlertKind.Important => theme.AlertImportant,
            AlertKind.Warning   => theme.AlertWarning,
            AlertKind.Caution   => theme.AlertCaution,
            _                   => theme.MutedColor
        };

        var r  = box.Bounds;
        float cx = r.MidX, cy = r.MidY, radius = MathF.Min(r.Width, r.Height) / 2f;

        if (box.Kind is AlertKind.Warning or AlertKind.Caution)
        {
            fillPaint.Color = color;
            using var tri = new SKPath();
            tri.MoveTo(cx, r.Top);
            tri.LineTo(r.Right, r.Bottom);
            tri.LineTo(r.Left, r.Bottom);
            tri.Close();
            canvas.DrawPath(tri, fillPaint);
            fillPaint.Color = SKColors.White;
            canvas.DrawRect(SKRect.Create(cx - radius * 0.09f, cy - radius * 0.05f, radius * 0.18f, radius * 0.42f), fillPaint);
            canvas.DrawCircle(cx, cy + radius * 0.58f, radius * 0.09f, fillPaint);
        }
        else
        {
            fillPaint.Color = color;
            canvas.DrawCircle(cx, cy, radius, fillPaint);
            fillPaint.Color = SKColors.White;
            if (box.Kind == AlertKind.Note)
            {
                canvas.DrawCircle(cx, cy - radius * 0.35f, radius * 0.12f, fillPaint);
                canvas.DrawRect(SKRect.Create(cx - radius * 0.1f, cy - radius * 0.05f, radius * 0.2f, radius * 0.55f), fillPaint);
            }
            else
            {
                canvas.DrawRect(SKRect.Create(cx - radius * 0.1f, cy - radius * 0.5f, radius * 0.2f, radius * 0.55f), fillPaint);
                canvas.DrawCircle(cx, cy + radius * 0.42f, radius * 0.12f, fillPaint);
            }
        }
    }

    private static void DrawDetailsToggle(SKCanvas canvas, DetailsHeaderBox box, MarkdownTheme theme, SKPaint fillPaint)
    {
        float sz = 10f;
        float cx = box.Bounds.Left + 16f;
        float cy = box.Bounds.MidY;
        using var path = new SKPath();
        if (box.Expanded)
        {
            path.MoveTo(cx - sz / 2f, cy - sz / 4f);
            path.LineTo(cx + sz / 2f, cy - sz / 4f);
            path.LineTo(cx, cy + sz / 2f);
        }
        else
        {
            path.MoveTo(cx - sz / 4f, cy - sz / 2f);
            path.LineTo(cx - sz / 4f, cy + sz / 2f);
            path.LineTo(cx + sz / 2f, cy);
        }
        path.Close();
        fillPaint.Color = theme.MutedColor;
        canvas.DrawPath(path, fillPaint);
    }
}
