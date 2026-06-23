// SPDX-License-Identifier: MIT
// Orivy RichText — TextBlobBatcher
//
// SKTextBlob batching for rich-text rendering.
//
// PROBLEM (v2): DrawLineWithRuns called canvas.DrawText once per segment.
//   For a 100-line visible window with ~5 segments/line and ~3 distinct
//   (font, color) combinations per line, that's 500 DrawText calls per
//   frame. Each DrawText goes through Skia's text pipeline independently —
//   no batching, no shared glyph cache lookup.
//
// SOLUTION (v3): collect ALL visible segments first, group them by
//   (font, color), build ONE SKTextBlob per group, then issue ONE
//   canvas.DrawText per blob. Reduces 500 draw calls → ~3-5 draw calls.
//
// THREE-PASS RENDERING:
//   Pass 1: background fills (rects — one DrawRect per segment with bg color)
//   Pass 2: text blobs (one DrawText per (font, color) group)
//   Pass 3: underline + strikethrough strokes (one DrawLine per decoration)
//
// The stroke pass could also be batched (build SKPath with all strokes for
// a given (color, strokeWidth)) but v3 keeps it simple — strokes are
// typically < 20% of segments so the gain is marginal.
//
// ALLOCATION STRATEGY:
//   - TextBlobBatcher is owned by RichTextBox and reused across frames.
//   - Internal buffers (segment list, group dictionary) are cleared, not
//     reallocated, between frames.
//   - SKTextBlobBuilder is created per group per frame (its buffer is
//     sized to the group's total glyph count). Disposing the builder
//     returns its allocation to the pool.
//   - SKTextBlob itself is disposed after the draw call.
//
// GLYPH HANDLING:
//   SKTextBlob requires glyph IDs (not text). We use SKFont.GetGlyphs +
//   SKFont.GetGlyphWidths to convert text → glyphs → positions. SkiaSharp
//   caches glyph metrics internally per-font, so this is cheap after warm-up.

using System;
using System.Collections.Generic;
using SkiaSharp;

namespace Orivy.Controls.RichText;

/// <summary>
/// Collects text segments across an entire frame, then flushes them as
/// batched SKTextBlob draw calls grouped by (font, color).
///
/// Usage:
///   batcher.BeginFrame();
///   foreach (segment in visibleSegments)
///       batcher.AddSegment(...);
///   batcher.Flush(canvas, fillPaint, strokePaint);
/// </summary>
public sealed class TextBlobBatcher : IDisposable
{
    // All segments collected this frame.
    private readonly List<Segment> _segments = new(256);

    // Group key (font, color packed) → list of segment indices.
    private readonly Dictionary<long, List<int>> _groups = new();

    // v4: Stroke batching — group by (color, thickness) and accumulate into a
    // single SKPath, then issue ONE DrawPath per group. For a typical frame
    // with 50 underline + 10 strikethrough decorations sharing the same
    // (color, thickness), this reduces 60 DrawLine calls → 1 DrawPath call.
    private readonly Dictionary<ulong, SKPath> _strokePaths = new();
    private readonly Dictionary<ulong, (SKColor color, float thickness)> _strokeGroupMeta = new();

    // v4: reusable glyph buffer for BuildBlob — avoids per-segment ushort[]
    // allocation. Grown on demand; never shrinks.
    private ushort[]? _reusableGlyphBuffer;

    private bool _disposed;

    private readonly struct Segment
    {
        public Segment(string text, int textStart, int textLen,
                       SKFont font, SKColor color,
                       float x, float y,
                       SKColor? bgColor, SKRect bgRect,
                       bool underline, float underlineY, float underlineW, float underlineThick,
                       bool strikethrough, float strikeY, float strikeW, float strikeThick,
                       float ascent, float descent)
        {
            Text = text;
            TextStart = textStart;
            TextLen = textLen;
            Font = font;
            Color = color;
            X = x;
            Y = y;
            BgColor = bgColor;
            BgRect = bgRect;
            Underline = underline;
            UnderlineY = underlineY;
            UnderlineW = underlineW;
            UnderlineThick = underlineThick;
            Strikethrough = strikethrough;
            StrikeY = strikeY;
            StrikeW = strikeW;
            StrikeThick = strikeThick;
            Ascent = ascent;
            Descent = descent;
        }

        public readonly string Text;
        public readonly int TextStart;
        public readonly int TextLen;
        public readonly SKFont Font;
        public readonly SKColor Color;
        public readonly float X, Y;
        public readonly SKColor? BgColor;
        public readonly SKRect BgRect;
        public readonly bool Underline;
        public readonly float UnderlineY, UnderlineW, UnderlineThick;
        public readonly bool Strikethrough;
        public readonly float StrikeY, StrikeW, StrikeThick;
        public readonly float Ascent, Descent;
    }

    /// <summary>Clear buffers and start a new frame. Call at the top of OnPaint.</summary>
    public void BeginFrame()
    {
        _segments.Clear();
        _groups.Clear();

        // v4: clear stroke path cache (we reuse SKPath instances, just reset them).
        foreach (var path in _strokePaths.Values)
            path.Dispose();
        _strokePaths.Clear();
        _strokeGroupMeta.Clear();
    }

    /// <summary>Add a text segment to be drawn this frame.
    /// Coordinates (x, y) are the baseline-left of the segment in canvas space.</summary>
    public void AddSegment(
        string text, int textStart, int textLen,
        SKFont font, SKColor color,
        float x, float y,
        SKColor? bgColor = null, SKRect bgRect = default,
        bool underline = false, float underlineY = 0, float underlineW = 0, float underlineThick = 0,
        bool strikethrough = false, float strikeY = 0, float strikeW = 0, float strikeThick = 0)
    {
        if (textLen <= 0)
            return;

        var metrics = font.Metrics;
        var segment = new Segment(
            text, textStart, textLen, font, color, x, y,
            bgColor, bgRect,
            underline, underlineY, underlineW, underlineThick,
            strikethrough, strikeY, strikeW, strikeThick,
            metrics.Ascent, metrics.Descent);

        var segIndex = _segments.Count;
        _segments.Add(segment);

        // Group key: pack font pointer (lower 48 bits) + color (lower 16 bits of hash).
        // Same (font, color) → same group → same blob.
        var groupKey = MakeGroupKey(font, color);
        if (!_groups.TryGetValue(groupKey, out var list))
        {
            list = new List<int>(8);
            _groups[groupKey] = list;
        }
        list.Add(segIndex);
    }

    /// <summary>Draw all collected segments in 3 passes.</summary>
    public void Flush(SKCanvas canvas, SKPaint fillPaint, SKPaint strokePaint)
    {
        // ── Pass 1: backgrounds ────────────────────────────────────────
        // Background rects can have different colors per segment, so we can't
        // batch them into one path. But DrawRect is very cheap; the cost is
        // negligible compared to DrawText. We set color per call.
        fillPaint.Style = SKPaintStyle.Fill;
        foreach (var seg in _segments)
        {
            if (seg.BgColor is { } bg && bg.Alpha > 0)
            {
                fillPaint.Color = bg;
                canvas.DrawRect(seg.BgRect, fillPaint);
            }
        }

        // ── Pass 2: text blobs ─────────────────────────────────────────
        // One SKTextBlob per (font, color) group. Each blob contains all
        // segments in that group as separate runs (one run per segment,
        // positioned absolutely via the run's (x, y) offset).
        fillPaint.Style = SKPaintStyle.Fill;
        foreach (var (groupKey, segmentIndices) in _groups)
        {
            if (segmentIndices.Count == 0)
                continue;

            // Extract the color from the first segment (all in group share it).
            var color = _segments[segmentIndices[0]].Color;
            fillPaint.Color = color;

            // Build the blob.
            using var blob = BuildBlob(segmentIndices);
            if (blob != null)
            {
                // DrawText(SKTextBlob, 0, 0, paint) — the blob's runs already
                // contain absolute positions, so we draw at origin.
                canvas.DrawText(blob, 0, 0, fillPaint);
            }
        }

        // ── Pass 3: underline + strikethrough (v4: SKPath-batched) ──────
        // Group by (color, thickness) → accumulate into SKPath → single DrawPath.
        // For 60 underline+strike decorations sharing the same (color, thickness),
        // this reduces 60 DrawLine calls → 1 DrawPath call.
        //
        // Quantize thickness to 0.5px buckets to maximize group hits — visual
        // difference is imperceptible but cache hit rate doubles.
        foreach (var seg in _segments)
        {
            if (seg.Underline)
            {
                var quantizedThick = MathF.Round(seg.UnderlineThick * 2f) * 0.5f;
                AddStrokeToPath(seg.Color, quantizedThick, seg.X, seg.UnderlineY, seg.X + seg.UnderlineW, seg.UnderlineY);
            }
            if (seg.Strikethrough)
            {
                var quantizedThick = MathF.Round(seg.StrikeThick * 2f) * 0.5f;
                AddStrokeToPath(seg.Color, quantizedThick, seg.X, seg.StrikeY, seg.X + seg.StrikeW, seg.StrikeY);
            }
        }

        // Issue one DrawPath per group.
        strokePaint.Style = SKPaintStyle.Stroke;
        strokePaint.StrokeCap = SKStrokeCap.Butt;
        foreach (var (groupKey, path) in _strokePaths)
        {
            if (path.IsEmpty)
                continue;
            var (color, thickness) = _strokeGroupMeta[groupKey];
            strokePaint.Color = color;
            strokePaint.StrokeWidth = thickness;
            canvas.DrawPath(path, strokePaint);
        }
    }

    /// <summary>Add a horizontal line to the stroke group identified by
    /// (color, quantizedThickness). Each group accumulates into a single
    /// SKPath, which is drawn with one DrawPath call in Flush.</summary>
    private void AddStrokeToPath(SKColor color, float thickness, float x1, float y1, float x2, float y2)
    {
        var key = MakeStrokeGroupKey(color, thickness);
        if (!_strokePaths.TryGetValue(key, out var path))
        {
            path = new SKPath();
            _strokePaths[key] = path;
            _strokeGroupMeta[key] = (color, thickness);
        }
        path.MoveTo(x1, y1);
        path.LineTo(x2, y2);
    }

    /// <summary>Pack (color, quantized thickness) into a 64-bit key for
    /// stroke group lookup. Color uses 32 bits; thickness uses 16 bits
    /// (after quantization to 0.5px buckets → range ~0.5 to ~32.0 fits in 16 bits).</summary>
    private static ulong MakeStrokeGroupKey(SKColor color, float thickness)
    {
        var colorValue = (uint)color;
        var thicknessBucket = (ushort)Math.Clamp((int)MathF.Round(thickness * 2f), 0, 65535);
        return ((ulong)colorValue << 16) | thicknessBucket;
    }

    /// <summary>Build a single SKTextBlob from all segments in a group.
    /// Each segment becomes a separate run in the blob, positioned absolutely.
    ///
    /// v4 OPTIMIZATION: reuses a single ushort[] buffer for glyph IDs across
    /// all segments in the group. Avoids per-segment array allocation
    /// (was: ~500 allocs/frame for a 100-line window; now: 0).
    ///
    /// v5.2 FIX: SkiaSharp 3.119'da SKFont.GetGlyphs(ReadOnlySpan<char>, Span<ushort>)
    /// void döner. CountGlyphs ile gerçek glyph sayısını al, sonra span overload
    /// ile buffer'ı doldur.</summary>
    private SKTextBlob? BuildBlob(List<int> segmentIndices)
    {
        if (segmentIndices.Count == 0)
            return null;

        using var builder = new SKTextBlobBuilder();

        foreach (var idx in segmentIndices)
        {
            var seg = _segments[idx];
            var textSpan = seg.Text.AsSpan(seg.TextStart, seg.TextLen);

            // v5.2: CountGlyphs ile gerçek glyph sayısını al.
            // (Combine marks vs simple text farkı için. Latin için == TextLen.)
            var glyphCount = seg.Font.CountGlyphs(textSpan);
            if (glyphCount == 0)
                continue;

            // Buffer'ı gerekiyorsa büyüt.
            if (_reusableGlyphBuffer == null || _reusableGlyphBuffer.Length < glyphCount)
                _reusableGlyphBuffer = new ushort[glyphCount];

            // v5.2: void overload — buffer'ı doldurur, count döndürmez.
            seg.Font.GetGlyphs(textSpan, _reusableGlyphBuffer.AsSpan(0, glyphCount));

            // Allocate run with absolute (X, Y) baseline offset.
            var run = builder.AllocateRun(seg.Font, glyphCount, seg.X, seg.Y, null);

            // Copy glyph IDs from reusable buffer into the run's glyph buffer.
            var glyphSpan = run.Glyphs;
            for (var i = 0; i < glyphCount; i++)
                glyphSpan[i] = _reusableGlyphBuffer[i];
        }

        return builder.Build();
    }

    /// <summary>Pack (font, color) into a single long for dictionary key.
    /// Font is identified by its runtime hash (instance-stable). Color is
    /// packed into the lower 32 bits.</summary>
    private static long MakeGroupKey(SKFont font, SKColor color)
    {
        // RuntimeHelpers.GetHashCode gives a stable per-instance hash.
        // We use the lower 32 bits of font hash + 32 bits of color.
        var fontHash = (uint)System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(font);
        var colorValue = (uint)color;
        return ((long)fontHash << 32) | colorValue;
    }

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;
        _segments.Clear();
        _groups.Clear();
        // v4: dispose stroke paths.
        foreach (var path in _strokePaths.Values)
            path.Dispose();
        _strokePaths.Clear();
        _strokeGroupMeta.Clear();
        _reusableGlyphBuffer = null;
    }
}
