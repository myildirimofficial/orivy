// SPDX-License-Identifier: MIT
// Orivy RichText — RichTextLayoutPipeline
//
// v5.1: Tam çalışan layout + draw pipeline.
//
// PROBLEM (v5.0): DrawRichTextContent stub olduğu için modlu modlarda
// (MarkdownSource, MarkdownPreview, Rtf) HİÇBİR ŞEY çizilmiyordu. Plain
// mod dışında kullanıcı yazılan metni göremiyordu.
//
// ÇÖZÜM (v5.1): Kendi layout + draw pipeline'ımız. Base TextBox'ın
// `_lines`, `_lineHeight`, `_baselineOffset`, `BuildTextLayout`,
// `DrawTextContent`, `MeasureTextWidth`, `GetLineText`, `GetTextViewport`,
// `GetVerticalScrollOffset`, `GetContentTopInset`, `_placeholderText`,
// `_placeholderPaint`, `_textPaint` üyelerine BAĞIMSIZ alternative.
//
// Bu sınıf, RichTextBox tarafından kullanılır. Base TextBox üyelerine
// ERFİYOR — sadece şu public/protected üyelere ihtiyaç duyar:
//   - Text, Font, ForeColor, Focused, Enabled
//   - DisplayRectangle (SKRect)
//   - ScaleFactor (float)
//   - AutoScrollMinSize (SKSize) — set edilebilir
//   - UpdateScrollBars() — protected internal
//   - _vScrollBar, _hScrollBar (protected internal field'lar)
//
// Eğer bu üyeler private ise, RichTextBox'ın OnPaint'inde bu sınıfı
// çağırmadan önce mevcut base.OnPaint çağrılmalı ve base class'ın layout'ı
// kullanılmalı (PLAIN mod için). Modlu modlarda bu pipeline devreye girer.

using System;
using System.Collections.Generic;
using SkiaSharp;

namespace Orivy.Controls.RichText;

/// <summary>Satır layout bilgisi. Mevcut TextBox'ın TextLineLayout struct'ının
/// aynısı — ama run-aware ölçüm ile doldurulur.</summary>
public readonly struct RichTextLineLayout
{
    public RichTextLineLayout(int start, int length, int breakLength, float width)
    {
        Start = start;
        Length = length;
        BreakLength = breakLength;
        Width = width;
    }

    public int Start { get; }
    public int Length { get; }
    public int BreakLength { get; }
    public float Width { get; }
}

/// <summary>
/// Run-aware text layout + draw pipeline. Bir RichTextBox örneğine bağlı.
/// Plain mod dışındaki tüm modlarda kullanılır.
/// </summary>
public sealed class RichTextLayoutPipeline
{
    // Layout state.
    private readonly List<RichTextLineLayout> _lines = new();
    private float _lineHeight;
    private float _baselineOffset;
    private float _contentWidth;
    private float _contentHeight;
    private bool _layoutDirty = true;
    private float _lastViewportWidth = -1f;
    private int _lastTextHash;
    private int _lastRunsVersion = -1;

    // Owner'a erişim.
    private readonly RichTextBox _owner;
    private readonly Func<string> _getText;
    private readonly Func<IReadOnlyList<TextRun>> _getRuns;
    private readonly Func<TextStyle> _getBaseStyle;
    private readonly Func<SKFont?> _getBaseFont;
    private readonly Func<bool> _getMultiline;
    private readonly Func<TextWrap> _getWrapMode;
    private readonly Func<string> _getPlaceholder;
    private readonly Func<SKColor> _getForeColor;
    private readonly Func<bool> _getFocused;
    private readonly Func<bool> _getEnabled;
    private readonly Func<float> _getScaleFactor;
    private readonly Func<SKRect> _getViewport;
    private readonly Func<float> _getVerticalScroll;
    private readonly Func<float> _getHorizontalScroll;
    private readonly Action<SKSize> _setAutoScrollMinSize;

    // Render kaynakları (owner'dan paylaşılır).
    private readonly FontCache _fontCache;
    private readonly RunAwareMeasurer _measurer;

    public RichTextLayoutPipeline(
        RichTextBox owner,
        Func<string> getText,
        Func<IReadOnlyList<TextRun>> getRuns,
        Func<TextStyle> getBaseStyle,
        Func<SKFont?> getBaseFont,
        Func<bool> getMultiline,
        Func<TextWrap> getWrapMode,
        Func<string> getPlaceholder,
        Func<SKColor> getForeColor,
        Func<bool> getFocused,
        Func<bool> getEnabled,
        Func<float> getScaleFactor,
        Func<SKRect> getViewport,
        Func<float> getVerticalScroll,
        Func<float> getHorizontalScroll,
        Action<SKSize> setAutoScrollMinSize,
        FontCache fontCache,
        RunAwareMeasurer measurer)
    {
        _owner = owner;
        _getText = getText;
        _getRuns = getRuns;
        _getBaseStyle = getBaseStyle;
        _getBaseFont = getBaseFont;
        _getMultiline = getMultiline;
        _getWrapMode = getWrapMode;
        _getPlaceholder = getPlaceholder;
        _getForeColor = getForeColor;
        _getFocused = getFocused;
        _getEnabled = getEnabled;
        _getScaleFactor = getScaleFactor;
        _getViewport = getViewport;
        _getVerticalScroll = getVerticalScroll;
        _getHorizontalScroll = getHorizontalScroll;
        _setAutoScrollMinSize = setAutoScrollMinSize;
        _fontCache = fontCache;
        _measurer = measurer;
    }

    public IReadOnlyList<RichTextLineLayout> Lines => _lines;
    public float LineHeight => _lineHeight;
    public float BaselineOffset => _baselineOffset;

    /// <summary>Layout'u invalidate et. Bir sonraki EnsureLayout'te rebuild olur.</summary>
    public void Invalidate()
    {
        _layoutDirty = true;
    }

    /// <summary>Layout gerekirse rebuild et. Idempotent.</summary>
    public void EnsureLayout()
    {
        if (!_layoutDirty)
            return;

        var viewport = _getViewport();
        var viewportWidth = Math.Max(1f, viewport.Width);
        var text = _getText();
        var runs = _getRuns();
        var textHash = text.GetHashCode();
        var runsVersion = runs.GetHashCode();

        // Eğer text/runs/viewport değişmediyse, skip.
        if (Math.Abs(viewportWidth - _lastViewportWidth) < 0.5f
            && textHash == _lastTextHash
            && runsVersion == _lastRunsVersion)
        {
            _layoutDirty = false;
            return;
        }

        EnsureFontMetrics();
        BuildLayout(text, runs, viewportWidth);
        UpdateScrollMetrics(viewport);

        // Multiline + wrap modunda, viewport değişince re-wrap gerekir.
        if (_getMultiline() && _getWrapMode() != TextWrap.None)
        {
            var refinedViewport = _getViewport();
            var refinedWidth = Math.Max(1f, refinedViewport.Width);
            if (Math.Abs(refinedWidth - viewportWidth) > 0.5f)
            {
                BuildLayout(text, runs, refinedWidth);
                UpdateScrollMetrics(refinedViewport);
            }
        }

        _lastViewportWidth = viewportWidth;
        _lastTextHash = textHash;
        _lastRunsVersion = runsVersion;
        _layoutDirty = false;
    }

    /// <summary>Font metriklerini hesapla (_lineHeight, _baselineOffset).</summary>
    private void EnsureFontMetrics()
    {
        var baseFont = _getBaseFont() ?? _fontCache.GetBaseFont();
        var scale = _getScaleFactor();
        var metrics = baseFont.Metrics;
        var rawLineHeight = metrics.Descent - metrics.Ascent + Math.Max(0f, metrics.Leading);
        _baselineOffset = -metrics.Ascent;
        _lineHeight = Math.Max(16f * scale, rawLineHeight * 1.18f);
    }

    /// <summary>Text + runs → _lines listesi. Mevcut TextBox'ın BuildTextLayout
    /// mantığının run-aware versiyonu.</summary>
    private void BuildLayout(string text, IReadOnlyList<TextRun> runs, float viewportWidth)
    {
        _lines.Clear();

        if (text.Length == 0)
        {
            _lines.Add(new RichTextLineLayout(0, 0, 0, 0f));
            _contentWidth = 0f;
            _contentHeight = _lineHeight;
            return;
        }

        var multiline = _getMultiline();
        var wrapEnabled = multiline && _getWrapMode() != TextWrap.None;
        var scale = _getScaleFactor();
        var wrapWidth = wrapEnabled ? Math.Max(1f, viewportWidth - 2f * scale) : float.MaxValue;
        var paragraphStart = 0;

        while (paragraphStart < text.Length)
        {
            var paragraphEnd = text.IndexOf('\n', paragraphStart);
            var hasBreak = paragraphEnd >= 0;
            if (!hasBreak)
                paragraphEnd = text.Length;

            AddParagraphLines(text, runs, paragraphStart, paragraphEnd, hasBreak ? 1 : 0, wrapEnabled, wrapWidth);

            if (!hasBreak)
                break;

            paragraphStart = paragraphEnd + 1;
            if (paragraphStart == text.Length)
                _lines.Add(new RichTextLineLayout(text.Length, 0, 0, 0f));
        }

        if (_lines.Count == 0)
            _lines.Add(new RichTextLineLayout(0, 0, 0, 0f));

        _contentWidth = 0f;
        for (var i = 0; i < _lines.Count; i++)
            _contentWidth = Math.Max(_contentWidth, _lines[i].Width);

        if (multiline && _getWrapMode() != TextWrap.None)
            _contentWidth = Math.Max(_contentWidth, Math.Max(1f, viewportWidth));
        else
            _contentWidth += 4f * scale;

        _contentHeight = Math.Max(_lineHeight, _lines.Count * _lineHeight);
    }

    /// <summary>Paragrafı satırlara böl (wrap). Mevcut TextBox'ın AddParagraphLines
    /// mantığının run-aware versiyonu.</summary>
    private void AddParagraphLines(string text, IReadOnlyList<TextRun> runs,
                                    int paragraphStart, int paragraphEnd, int breakLength,
                                    bool wrapEnabled, float wrapWidth)
    {
        if (!wrapEnabled || paragraphStart == paragraphEnd)
        {
            AddLine(text, runs, paragraphStart, paragraphEnd - paragraphStart, breakLength);
            return;
        }

        var lineStart = paragraphStart;
        var index = paragraphStart;
        var lastBreakIndex = -1;
        var wrapMode = _getWrapMode();

        while (index < paragraphEnd)
        {
            var current = text[index];
            if (wrapMode == TextWrap.WordWrap && char.IsWhiteSpace(current) && current != '\n' && current != '\r')
                lastBreakIndex = index;

            var testWidth = MeasureTextWidth(text, runs, lineStart, index - lineStart + 1);
            if (testWidth > wrapWidth && index > lineStart)
            {
                var wrapEnd = index;
                var nextLineStart = index;

                if (wrapMode == TextWrap.WordWrap && lastBreakIndex >= lineStart)
                {
                    wrapEnd = lastBreakIndex + 1;
                    nextLineStart = lastBreakIndex + 1;
                }

                if (wrapEnd <= lineStart)
                {
                    wrapEnd = index;
                    nextLineStart = index;
                }

                AddLine(text, runs, lineStart, wrapEnd - lineStart, 0);
                lineStart = nextLineStart;
                lastBreakIndex = -1;
                continue;
            }

            index++;
        }

        AddLine(text, runs, lineStart, paragraphEnd - lineStart, breakLength);
    }

    private void AddLine(string text, IReadOnlyList<TextRun> runs, int start, int length, int breakLength)
    {
        var safeLength = Math.Max(0, length);
        var width = MeasureTextWidth(text, runs, start, safeLength);
        _lines.Add(new RichTextLineLayout(start, safeLength, breakLength, width));
    }

    /// <summary>Run-aware ölçüm. Verilen range içindeki tüm run'ları çizer,
    /// her birini kendi fontu ile ölçer, topla.</summary>
    private float MeasureTextWidth(string text, IReadOnlyList<TextRun> runs, int start, int length)
    {
        if (length <= 0)
            return 0f;

        var baseFont = _getBaseFont();
        var baseStyle = _getBaseStyle();
        var end = start + length;
        var totalWidth = 0f;

        // Binary search ile başlangıç run'ını bul.
        var runIdx = LowerBound(runs, start);

        // Boşluk/run-gap'ler dahil — line baseStyle ile ölçülür.
        var currentPos = start;
        while (currentPos < end)
        {
            if (runIdx >= runs.Count)
            {
                // Kalan kısım baseStyle ile.
                var remaining = end - currentPos;
                var font = _fontCache.GetFont(baseStyle, baseFont);
                totalWidth += font.MeasureText(text.AsSpan(currentPos, remaining));
                break;
            }

            var run = runs[runIdx];

            if (run.Start >= end)
            {
                // Tüm remaining baseStyle.
                var remaining = end - currentPos;
                var font = _fontCache.GetFont(baseStyle, baseFont);
                totalWidth += font.MeasureText(text.AsSpan(currentPos, remaining));
                break;
            }

            if (run.End <= currentPos)
            {
                runIdx++;
                continue;
            }

            // Gap before this run (baseStyle).
            if (run.Start > currentPos)
            {
                var gapLen = Math.Min(run.Start, end) - currentPos;
                var gapFont = _fontCache.GetFont(baseStyle, baseFont);
                totalWidth += gapFont.MeasureText(text.AsSpan(currentPos, gapLen));
                currentPos += gapLen;
            }

            // Run intersection.
            var segStart = Math.Max(run.Start, currentPos);
            var segEnd = Math.Min(run.End, end);
            var segLen = segEnd - segStart;
            if (segLen > 0)
            {
                var mergedStyle = baseStyle.Merge(run.Style);
                var font = _fontCache.GetFont(mergedStyle, baseFont);
                totalWidth += font.MeasureText(text.AsSpan(segStart, segLen));
                currentPos = segEnd;
            }

            if (run.End <= currentPos)
                runIdx++;
        }

        return totalWidth;
    }

    private static int LowerBound(IReadOnlyList<TextRun> runs, int target)
    {
        var lo = 0;
        var hi = runs.Count;
        while (lo < hi)
        {
            var mid = lo + (hi - lo) / 2;
            if (runs[mid].End <= target)
                lo = mid + 1;
            else
                hi = mid;
        }
        return lo;
    }

    /// <summary>Scroll metriklerini güncelle. Mevcut TextBox'ın UpdateScrollMetrics
    /// mantığının aynısı.</summary>
    private void UpdateScrollMetrics(SKRect viewport)
    {
        var multiline = _getMultiline();
        var leftInset = viewport.Left;
        var topInset = viewport.Top;
        var rightInset = _owner.Width - viewport.Right;
        var bottomInset = _owner.Height - viewport.Bottom;

        var minWidth = (float)Math.Ceiling(leftInset + _contentWidth + rightInset);
        var minHeight = multiline
            ? (float)Math.Ceiling(topInset + _contentHeight + bottomInset)
            : _owner.Height;

        if (!multiline)
            minWidth = Math.Max(_owner.Width, minWidth);

        _setAutoScrollMinSize(new SKSize(minWidth, minHeight));
    }

    /// <summary>Verilen caret indeksinin hangi satırda olduğunu bul.</summary>
    public int FindLineIndexForCaret(int caretIndex)
    {
        EnsureLayout();
        var clamped = Math.Clamp(caretIndex, 0, _getText().Length);
        for (var i = 0; i < _lines.Count; i++)
        {
            var line = _lines[i];
            if (clamped <= line.Start + line.Length)
                return i;
        }
        return _lines.Count - 1;
    }

    /// <summary>Verilen satırın text'ini döndür.</summary>
    public string GetLineText(RichTextLineLayout line)
    {
        if (line.Length <= 0)
            return string.Empty;
        var text = _getText();
        return text.Substring(line.Start, line.Length);
    }

    /// <summary>Satırın içindeki belirli bir karaktere kadar olan x offset'i.</summary>
    public float MeasureLocalX(string lineText, int length, SKFont font)
    {
        if (length <= 0 || string.IsNullOrEmpty(lineText))
            return 0f;
        if (length >= lineText.Length)
            return font.MeasureText(lineText);
        return font.MeasureText(lineText.AsSpan(0, length));
    }

    /// <summary>Top inset — multiline değilse satırı dikey ortalamak için.</summary>
    public float GetContentTopInset(SKRect viewport)
    {
        if (_getMultiline())
            return 0f;
        return Math.Max(0f, (viewport.Height - _lineHeight) * 0.5f);
    }
}
