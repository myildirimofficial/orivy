// SPDX-License-Identifier: MIT
// Orivy RichText — Markdown Source Highlighter
//
// Used in MarkdownSource mode: the source text is visible (markdown syntax
// characters included), but styled. E.g. typing `**bold**` shows the four
// asterisks but the word "bold" is rendered bold. This is the typical
// "syntax-highlighted markdown editor" experience (VS Code, Obsidian source mode).
//
// The highlighter produces a List<TextRun> covering the full source text,
// with character indices aligned 1:1 with the source. The RichTextBox then
// uses RunAwareMeasurer to render each run with its appropriate font.

using System;
using System.Collections.Generic;
using System.Text;
using SkiaSharp;

namespace Orivy.Controls.RichText.Markdown;

/// <summary>
/// Produces styled runs over the markdown SOURCE text. Used in MarkdownSource mode.
/// </summary>
public sealed class MarkdownSourceHighlighter
{
    // Palette — these can be themed by the consumer by replacing the colors.
    public SKColor HeadingColor { get; set; } = new(0x1F, 0x6F, 0xEB);        // blue
    public SKColor BoldColor { get; set; } = new(0xE4, 0x5B, 0x4B);           // red
    public SKColor ItalicColor { get; set; } = new(0xE4, 0x5B, 0x4B);
    public SKColor StrikethroughColor { get; set; } = new(0x80, 0x80, 0x80);  // gray
    public SKColor CodeColor { get; set; } = new(0xC5, 0x76, 0x33);           // orange
    public SKColor CodeBgColor { get; set; } = new(0xF4, 0xF4, 0xF4, 0x80);   // light gray
    public SKColor LinkColor { get; set; } = new(0x1F, 0x6F, 0xEB);
    public SKColor LinkUrlColor { get; set; } = new(0x80, 0x80, 0x80, 0xC0);  // dimmed
    public SKColor MarkerColor { get; set; } = new(0x80, 0x80, 0x80, 0xB0);   // dimmed asterisks, # etc.
    public SKColor BlockquoteColor { get; set; } = new(0x80, 0x80, 0x80);
    public SKColor ListItemColor { get; set; } = new(0x60, 0x60, 0x60);
    public SKColor HrColor { get; set; } = new(0xB0, 0xB0, 0xB0);

    // ── Incremental paragraph cache ────────────────────────────────────
    //
    // Markdown editing is dominated by single-keystroke edits that touch
    // only one paragraph. Caching per-paragraph highlight results and
    // reusing them when the paragraph text is unchanged reduces highlight
    // cost from O(N) per keystroke to O(changed_paragraph_len).
    //
    // Cache key: paragraph text hash. Cache value: list of (offset, length, style)
    // tuples — offsets are RELATIVE to the paragraph start, so the same
    // paragraph text always produces the same relative runs and can be
    // shifted by the paragraph's absolute start in the document.
    //
    // Fence state ("inFencedCode") is tracked per-paragraph: a paragraph is
    // a maximal run of consecutive lines that aren't separated by a blank line.

    private readonly Dictionary<int, CachedParagraph> _paragraphCache = new();
    private const int MaxParagraphCacheEntries = 256;
    private string? _lastSource;
    private List<TextRun>? _lastResult;

    private readonly struct CachedParagraph
    {
        public CachedParagraph(int hash, List<(int offset, int length, TextStyle style)> runs, bool changesFenceState, bool endsInFence)
        {
            Hash = hash;
            Runs = runs;
            ChangesFenceState = changesFenceState;
            EndsInFence = endsInFence;
        }
        public int Hash { get; }
        public List<(int offset, int length, TextStyle style)> Runs { get; }
        public bool ChangesFenceState { get; }  // paragraph contains a fence open/close
        public bool EndsInFence { get; }         // paragraph ends while still inside a fence
    }

    /// <summary>Tokenize the source text into styled runs covering every char.
    ///
    /// OPTIMIZATION: incremental paragraph cache. When called with a source
    /// that has changed only in one paragraph (typical editing), only the
    /// changed paragraph is re-tokenized. Other paragraphs reuse cached
    /// relative runs shifted to their new absolute offsets.</summary>
    public List<TextRun> Highlight(string source)
    {
        if (string.IsNullOrEmpty(source))
        {
            _lastSource = source;
            _lastResult = new List<TextRun> { new TextRun(0, 0, TextStyle.Default) };
            return _lastResult;
        }

        // Fast path: same source as last call → return cached result.
        if (ReferenceEquals(source, _lastSource) || source == _lastSource)
        {
            if (_lastResult != null)
                return _lastResult;
        }

        var runs = new List<TextRun>();
        var lines = source.Replace("\r\n", "\n", System.StringComparison.Ordinal)
                          .Replace('\r', '\n')
                          .Split('\n');

        // Walk paragraph-by-paragraph. A "paragraph" here is a maximal run
        // of consecutive non-empty lines (we treat blank lines as separators).
        var pos = 0;
        var lineIdx = 0;
        var inFencedCode = false;
        var fenceChar = '`';
        var fenceLen = 0;

        while (lineIdx < lines.Length)
        {
            // Collect paragraph lines.
            var paraStartLine = lineIdx;
            var paraStartPos = pos;
            var paraSb = new System.Text.StringBuilder();

            while (lineIdx < lines.Length)
            {
                var line = lines[lineIdx];
                if (string.IsNullOrWhiteSpace(line) && !inFencedCode)
                    break;  // paragraph separator

                if (paraSb.Length > 0)
                    paraSb.Append('\n');
                paraSb.Append(line);
                pos += line.Length;
                if (lineIdx < lines.Length - 1)
                    pos++;  // for the \n separator we'll add below
                lineIdx++;

                // Track fence state.
                var trimmed = line.TrimStart();
                if (!inFencedCode && (trimmed.StartsWith("```") || trimmed.StartsWith("~~~")))
                {
                    fenceChar = trimmed[0];
                    fenceLen = 0;
                    while (fenceLen < trimmed.Length && trimmed[fenceLen] == fenceChar) fenceLen++;
                    inFencedCode = true;
                }
                else if (inFencedCode && trimmed.Length >= fenceLen && trimmed.Length >= 3 && AllChars(trimmed, fenceChar))
                {
                    inFencedCode = false;
                }

                // If we're inside a fence, keep consuming lines regardless of blanks.
                if (inFencedCode)
                    continue;
                if (string.IsNullOrWhiteSpace(line))
                    break;
            }

            var paraText = paraSb.ToString();
            var paraHash = StringComparer.Ordinal.GetHashCode(paraText);

            // Cache lookup.
            if (_paragraphCache.TryGetValue(paraHash, out var cached) && cached.Hash == paraHash)
            {
                // Cache hit: shift relative offsets to absolute.
                foreach (var (offset, length, style) in cached.Runs)
                    runs.Add(new TextRun(paraStartPos + offset, length, style));
                inFencedCode = cached.EndsInFence;
            }
            else
            {
                // Cache miss: re-tokenize this paragraph from scratch.
                var relativeRuns = new List<(int offset, int length, TextStyle style)>();
                HighlightParagraph(relativeRuns, paraText, ref inFencedCode, ref fenceChar, ref fenceLen);

                // Add to absolute runs list.
                foreach (var (offset, length, style) in relativeRuns)
                    runs.Add(new TextRun(paraStartPos + offset, length, style));

                // Cache it.
                if (_paragraphCache.Count >= MaxParagraphCacheEntries)
                    EvictParagraphCache();
                _paragraphCache[paraHash] = new CachedParagraph(paraHash, relativeRuns, false, inFencedCode);
            }

            // Add a newline-styled run for the paragraph separator (if any).
            if (lineIdx < lines.Length && string.IsNullOrWhiteSpace(lines[lineIdx]))
            {
                runs.Add(new TextRun(pos, 1, TextStyle.Default));
                pos++;
                lineIdx++;
            }
        }

        NormalizeRuns(runs, source.Length);
        _lastSource = source;
        _lastResult = runs;
        return runs;
    }

    /// <summary>Highlight a single paragraph (a sequence of consecutive
    /// non-blank lines, possibly multi-line). inFencedCode is by-ref
    /// because entering/leaving a fence spans paragraphs.</summary>
    private void HighlightParagraph(List<(int offset, int length, TextStyle style)> output,
                                     string paragraph, ref bool inFencedCode,
                                     ref char fenceChar, ref int fenceLen)
    {
        var lines = paragraph.Split('\n');
        var pos = 0;

        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i];

            if (inFencedCode)
            {
                var trimmed = line.TrimStart();
                if (trimmed.Length >= fenceLen && trimmed.Length >= 3 && AllChars(trimmed, fenceChar))
                {
                    AddRelRun(output, pos, line.Length, CodeStyle());
                    inFencedCode = false;
                }
                else
                {
                    AddRelRun(output, pos, line.Length, CodeStyle());
                }
            }
            else
            {
                var trimmed = line.TrimStart();

                if (trimmed.StartsWith("```") || trimmed.StartsWith("~~~"))
                {
                    fenceChar = trimmed[0];
                    fenceLen = 0;
                    while (fenceLen < trimmed.Length && trimmed[fenceLen] == fenceChar) fenceLen++;
                    AddRelRun(output, pos, line.Length, CodeStyle());
                    inFencedCode = true;
                }
                else if (IsHeading(trimmed, out var level))
                {
                    var hashCount = level;
                    AddRelRun(output, pos, hashCount, MarkerStyle());
                    AddRelRun(output, pos + hashCount, line.Length - hashCount, HeadingStyle(level));
                }
                else if (IsHorizontalRule(trimmed))
                {
                    AddRelRun(output, pos, line.Length, HrStyle());
                }
                else if (trimmed.StartsWith("> ", System.StringComparison.Ordinal) || trimmed == ">")
                {
                    AddRelRun(output, pos, line.Length, BlockquoteStyle());
                }
                else if (IsBulletListItem(trimmed))
                {
                    var bulletLen = trimmed.Length >= 2 ? 2 : trimmed.Length;
                    AddRelRun(output, pos, bulletLen, MarkerStyle());
                    HighlightInlinesRel(output, pos + bulletLen, line.Length - bulletLen, line, bulletLen);
                }
                else if (IsOrderedListItem(trimmed, out _))
                {
                    var j = 0;
                    while (j < trimmed.Length && char.IsDigit(trimmed[j])) j++;
                    j++; j++;
                    AddRelRun(output, pos, j, MarkerStyle());
                    HighlightInlinesRel(output, pos + j, line.Length - j, line, j);
                }
                else if (IsTableLine(trimmed))
                {
                    AddRelRun(output, pos, line.Length, ListItemStyle());
                }
                else
                {
                    HighlightInlinesRel(output, pos, line.Length, line, 0);
                }
            }

            pos += line.Length;
            if (i < lines.Length - 1)
            {
                AddRelRun(output, pos, 1, TextStyle.Default);
                pos++;
            }
        }
    }

    private static void AddRelRun(List<(int offset, int length, TextStyle style)> output,
                                   int offset, int length, TextStyle style)
    {
        if (length > 0)
            output.Add((offset, length, style));
    }

    private void HighlightInlinesRel(List<(int offset, int length, TextStyle style)> output,
                                      int start, int length, string text, int offsetInLine)
    {
        // Delegate to the existing HighlightInlines logic but write to a
        // relative-offset list. We use a temp List<TextRun> and convert.
        var temp = new List<TextRun>();
        HighlightInlines(temp, start, length, text, offsetInLine);
        foreach (var run in temp)
            output.Add((run.Start, run.Length, run.Style));
    }

    private static bool AllChars(string s, char ch)
    {
        foreach (var c in s) if (c != ch) return false;
        return true;
    }

    private void EvictParagraphCache()
    {
        // Simple eviction: clear half. Cache is small; LRU not worth the overhead.
        var target = _paragraphCache.Count / 2;
        var removed = 0;
        var keysToRemove = new List<int>(_paragraphCache.Count);
        foreach (var key in _paragraphCache.Keys)
        {
            if (removed >= target) break;
            keysToRemove.Add(key);
            removed++;
        }
        foreach (var key in keysToRemove)
            _paragraphCache.Remove(key);
    }

    /// <summary>Clear the paragraph cache. Call when the theme colors change
    /// (otherwise cached runs would have stale colors).</summary>
    public void ClearCache()
    {
        _paragraphCache.Clear();
        _lastSource = null;
        _lastResult = null;
    }

    private void HighlightInlines(List<TextRun> runs, int start, int length, string text, int offsetInLine)
    {
        // Simple inline highlighter: scans for **, *, ~~, `, [..](..), ![..](..)
        var end = start + length;
        var i = start;
        var segmentStart = start;

        void FlushPlain(int to)
        {
            if (to > segmentStart)
                AddRun(runs, segmentStart, to - segmentStart, TextStyle.Default);
            segmentStart = to;
        }

        while (i < end)
        {
            // Map document index to line-local index.
            var localI = i - start + offsetInLine;
            if (localI >= text.Length) break;
            var ch = text[localI];

            // Escape: \char
            if (ch == '\\' && localI + 1 < text.Length)
            {
                FlushPlain(i);
                AddRun(runs, i, 2, MarkerStyle());
                i += 2;
                segmentStart = i;
                continue;
            }

            // Bold **
            if ((ch == '*' || ch == '_') && localI + 1 < text.Length && text[localI + 1] == ch)
            {
                var close = FindClosingMarker(text, localI + 2, ch, 2);
                if (close > 0)
                {
                    FlushPlain(i);
                    AddRun(runs, i, 2, MarkerStyle());           // opening **
                    var innerStart = i + 2;
                    var innerLen = close - (localI + 2);
                    AddRun(runs, innerStart, innerLen, BoldStyle());
                    AddRun(runs, innerStart + innerLen, 2, MarkerStyle()); // closing **
                    i = innerStart + innerLen + 2;
                    segmentStart = i;
                    continue;
                }
            }

            // Italic * or _
            if (ch == '*' || ch == '_')
            {
                var close = FindClosingMarker(text, localI + 1, ch, 1);
                if (close > 0)
                {
                    FlushPlain(i);
                    AddRun(runs, i, 1, MarkerStyle());
                    var innerStart = i + 1;
                    var innerLen = close - (localI + 1);
                    AddRun(runs, innerStart, innerLen, ItalicStyle());
                    AddRun(runs, innerStart + innerLen, 1, MarkerStyle());
                    i = innerStart + innerLen + 1;
                    segmentStart = i;
                    continue;
                }
            }

            // Strikethrough ~~
            if (ch == '~' && localI + 1 < text.Length && text[localI + 1] == '~')
            {
                var close = FindClosingMarker(text, localI + 2, '~', 2);
                if (close > 0)
                {
                    FlushPlain(i);
                    AddRun(runs, i, 2, MarkerStyle());
                    var innerStart = i + 2;
                    var innerLen = close - (localI + 2);
                    AddRun(runs, innerStart, innerLen, StrikeStyle());
                    AddRun(runs, innerStart + innerLen, 2, MarkerStyle());
                    i = innerStart + innerLen + 2;
                    segmentStart = i;
                    continue;
                }
            }

            // Inline code `
            if (ch == '`')
            {
                var tickCount = 1;
                while (localI + tickCount < text.Length && text[localI + tickCount] == '`') tickCount++;
                var close = FindClosingRun(text, localI + tickCount, '`', tickCount);
                if (close > 0)
                {
                    FlushPlain(i);
                    AddRun(runs, i, tickCount, MarkerStyle());
                    var innerStart = i + tickCount;
                    var innerLen = close - (localI + tickCount);
                    AddRun(runs, innerStart, innerLen, CodeStyle());
                    AddRun(runs, innerStart + innerLen, tickCount, MarkerStyle());
                    i = innerStart + innerLen + tickCount;
                    segmentStart = i;
                    continue;
                }
            }

            // Link [text](url) — text is link-colored, url is dimmed.
            if (ch == '[')
            {
                var close = FindClosingBracket(text, localI);
                if (close > 0 && close + 1 < text.Length && text[close + 1] == '(')
                {
                    var urlEnd = text.IndexOf(')', close + 2);
                    if (urlEnd > 0)
                    {
                        FlushPlain(i);
                        AddRun(runs, i, 1, MarkerStyle());  // [
                        var textStart = i + 1;
                        var textLen = close - localI;
                        AddRun(runs, textStart, textLen, LinkStyle());
                        AddRun(runs, textStart + textLen, 1, MarkerStyle());  // ]
                        var urlStart = textStart + textLen + 1;
                        var urlLen = urlEnd - close - 1;
                        AddRun(runs, urlStart, urlLen + 1, LinkUrlStyle());  // (url
                        AddRun(runs, urlStart + urlLen + 1, 1, MarkerStyle());  // )
                        i = urlStart + urlLen + 2;
                        segmentStart = i;
                        continue;
                    }
                }
            }

            // Image ![alt](url) — treat similar to link.
            if (ch == '!' && localI + 1 < text.Length && text[localI + 1] == '[')
            {
                var close = FindClosingBracket(text, localI + 1);
                if (close > 0 && close + 1 < text.Length && text[close + 1] == '(')
                {
                    var urlEnd = text.IndexOf(')', close + 2);
                    if (urlEnd > 0)
                    {
                        FlushPlain(i);
                        AddRun(runs, i, 2, MarkerStyle());  // ![
                        var altStart = i + 2;
                        var altLen = close - (localI + 1);
                        AddRun(runs, altStart, altLen, LinkStyle());
                        AddRun(runs, altStart + altLen, 1, MarkerStyle());  // ]
                        var urlStart = altStart + altLen + 1;
                        var urlLen = urlEnd - close - 1;
                        AddRun(runs, urlStart, urlLen + 1, LinkUrlStyle());
                        AddRun(runs, urlStart + urlLen + 1, 1, MarkerStyle());
                        i = urlStart + urlLen + 2;
                        segmentStart = i;
                        continue;
                    }
                }
            }

            i++;
        }

        FlushPlain(end);
    }

    private static int FindClosingMarker(string text, int from, char marker, int count)
    {
        for (var i = from; i + count <= text.Length; i++)
        {
            if (text[i] != marker) continue;
            var ok = true;
            for (var j = 1; j < count; j++)
            {
                if (text[i + j] != marker) { ok = false; break; }
            }
            if (ok) return i;
        }
        return -1;
    }

    private static int FindClosingRun(string text, int from, char ch, int count)
    {
        return FindClosingMarker(text, from, ch, count);
    }

    private static int FindClosingBracket(string text, int openPos)
    {
        var depth = 1;
        for (var i = openPos + 1; i < text.Length; i++)
        {
            if (text[i] == '[') depth++;
            else if (text[i] == ']')
            {
                depth--;
                if (depth == 0) return i;
            }
        }
        return -1;
    }

    private static bool IsHeading(string trimmed, out int level)
    {
        level = 0;
        if (trimmed.Length == 0 || trimmed[0] != '#') return false;
        while (level < trimmed.Length && level < 6 && trimmed[level] == '#') level++;
        return level > 0 && level < trimmed.Length && trimmed[level] == ' ';
    }

    private static bool IsHorizontalRule(string trimmed)
    {
        if (trimmed.Length < 3) return false;
        var ch = trimmed[0];
        if (ch != '-' && ch != '*' && ch != '_') return false;
        var count = 0;
        foreach (var c in trimmed)
        {
            if (c == ch) count++;
            else if (c != ' ' && c != '\t') return false;
        }
        return count >= 3;
    }

    private static bool IsBulletListItem(string trimmed)
    {
        return trimmed.Length >= 2
            && (trimmed[0] == '-' || trimmed[0] == '*' || trimmed[0] == '+')
            && trimmed[1] == ' ';
    }

    private static bool IsOrderedListItem(string trimmed, out int start)
    {
        start = 0;
        var i = 0;
        while (i < trimmed.Length && char.IsDigit(trimmed[i])) i++;
        if (i == 0 || i + 1 >= trimmed.Length) return false;
        if (trimmed[i] != '.' && trimmed[i] != ')') return false;
        if (trimmed[i + 1] != ' ') return false;
        return int.TryParse(trimmed[..i], out start);
    }

    private static bool IsTableLine(string trimmed)
        => trimmed.Contains('|') && (trimmed.StartsWith('|') || trimmed.EndsWith('|'));

    // ── Style factories ────────────────────────────────────────────────

    private TextStyle HeadingStyle(int level) => new()
    {
        Bold = true,
        ForeColor = HeadingColor,
        FontSize = level switch
        {
            1 => 24f,
            2 => 20f,
            3 => 17f,
            4 => 15f,
            5 => 14f,
            _ => 13f,
        },
    };

    private TextStyle BoldStyle() => new() { Bold = true, ForeColor = BoldColor };
    private TextStyle ItalicStyle() => new() { Italic = true, ForeColor = ItalicColor };
    private TextStyle StrikeStyle() => new() { Strikethrough = true, ForeColor = StrikethroughColor };
    private TextStyle CodeStyle() => new() { Monospace = true, ForeColor = CodeColor, BackColor = CodeBgColor };
    private TextStyle LinkStyle() => new() { Underline = true, ForeColor = LinkColor, Hyperlink = "" };
    private TextStyle LinkUrlStyle() => new() { ForeColor = LinkUrlColor };
    private TextStyle MarkerStyle() => new() { ForeColor = MarkerColor };
    private TextStyle BlockquoteStyle() => new() { Italic = true, ForeColor = BlockquoteColor };
    private TextStyle ListItemStyle() => new() { ForeColor = ListItemColor };
    private TextStyle HrStyle() => new() { ForeColor = HrColor };

    // ── Run list helpers ───────────────────────────────────────────────

    private static void AddRun(List<TextRun> runs, int start, int length, TextStyle style)
    {
        if (length <= 0) return;
        runs.Add(new TextRun(start, length, style));
    }

    private static void NormalizeRuns(List<TextRun> runs, int totalLength)
    {
        if (runs.Count == 0)
        {
            runs.Add(new TextRun(0, totalLength, TextStyle.Default));
            return;
        }

        runs.Sort((a, b) => a.Start.CompareTo(b.Start));

        // Fill gaps with default-styled runs.
        var merged = new List<TextRun>(runs.Count);
        merged.Add(runs[0]);
        for (var i = 1; i < runs.Count; i++)
        {
            var prev = merged[^1];
            var cur = runs[i];

            if (prev.End > cur.Start)
            {
                // Overlap: trim previous.
                merged[^1] = prev.WithLength(cur.Start - prev.Start);
                if (merged[^1].Length > 0)
                    merged.Add(cur);
                else
                    merged[^1] = cur;
            }
            else if (prev.End < cur.Start)
            {
                merged.Add(new TextRun(prev.End, cur.Start - prev.End, TextStyle.Default));
                merged.Add(cur);
            }
            else
            {
                if (prev.Style.Equals(cur.Style))
                    merged[^1] = prev.WithLength(prev.Length + cur.Length);
                else
                    merged.Add(cur);
            }
        }

        // Pad to total length.
        if (merged[^1].End < totalLength)
            merged.Add(new TextRun(merged[^1].End, totalLength - merged[^1].End, TextStyle.Default));

        runs.Clear();
        runs.AddRange(merged);
    }
}