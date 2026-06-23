// SPDX-License-Identifier: MIT
// Orivy RichText — StyledTextDocument
//
// The document model: a plain text + a sorted, non-overlapping, contiguous
// list of TextRuns covering every character. Edit operations (insert/delete)
// automatically shift run offsets, so the run list is always consistent with
// the text. This is the bridge between the existing char-index based
// TextBox code (caret, selection, scroll) and the rich-text rendering layer.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;

namespace Orivy.Controls.RichText;

/// <summary>
/// Mutable document holding plain text + styled runs. Maintains the
/// invariants: runs sorted by Start, non-overlapping, full coverage.
/// </summary>
public sealed class StyledTextDocument
{
    private string _text = string.Empty;
    // _runs always covers [0, _text.Length] with no gaps and no overlaps.
    // Empty runs are not allowed (except transiently during ops; we normalize
    // at the end of every public mutation).
    private readonly List<TextRun> _runs = new();

    public StyledTextDocument()
    {
        // Start with one Default run over the empty text.
        _runs.Add(new TextRun(0, 0, TextStyle.Default));
    }

    /// <summary>Plain text of the document. Setting it preserves no styling
    /// (treats as a full replacement with Default style).</summary>
    public string Text
    {
        get => _text;
        set
        {
            var next = value ?? string.Empty;
            if (_text == next)
                return;

            ReplaceAll(next, preserveStyling: false);
        }
    }

    /// <summary>Read-only view of the current runs.</summary>
    public IReadOnlyList<TextRun> Runs => _runs;

    /// <summary>Number of characters in the document.</summary>
    public int Length => _text.Length;

    /// <summary>True if there are no non-Default runs.</summary>
    public bool HasStyling
    {
        get
        {
            foreach (var run in _runs)
                if (!run.Style.IsDefault)
                    return true;
            return false;
        }
    }

    // ── Bulk operations ────────────────────────────────────────────────

    /// <summary>Replace the entire text. If preserveStyling is false (default),
    /// all runs are reset to Default. If true, an attempt is made to keep
    /// style for the prefix that matches.</summary>
    public void ReplaceAll(string newText, bool preserveStyling = false)
    {
        newText ??= string.Empty;

        if (!preserveStyling)
        {
            _text = newText;
            _runs.Clear();
            _runs.Add(new TextRun(0, _text.Length, TextStyle.Default));
            return;
        }

        // Find common prefix length.
        var commonPrefix = 0;
        var minLen = Math.Min(_text.Length, newText.Length);
        while (commonPrefix < minLen && _text[commonPrefix] == newText[commonPrefix])
            commonPrefix++;

        var keptRuns = new List<TextRun>();
        foreach (var run in _runs)
        {
            if (run.End <= commonPrefix)
                keptRuns.Add(run);
            else if (run.Start < commonPrefix)
                keptRuns.Add(new TextRun(run.Start, commonPrefix - run.Start, run.Style));
            else
                break;
        }

        _text = newText;
        _runs.Clear();
        _runs.AddRange(keptRuns);
        if (_runs.Count == 0 || _runs[^1].End < _text.Length)
            _runs.Add(new TextRun(_runs.Count == 0 ? 0 : _runs[^1].End,
                                  _text.Length - (_runs.Count == 0 ? 0 : _runs[^1].End),
                                  TextStyle.Default));
        Normalize();
    }

    /// <summary>Replace the document with a new text + run list.
    /// Used by parsers (Markdown preview, RTF reader). The runs MUST cover
    /// [0, text.Length] with no gaps; this method normalizes them.</summary>
    public void Load(string text, IEnumerable<TextRun> runs)
    {
        text ??= string.Empty;
        _text = text;

        _runs.Clear();
        foreach (var run in runs)
            _runs.Add(run);

        // Sort by start.
        _runs.Sort((a, b) => a.Start.CompareTo(b.Start));

        // Ensure full coverage, trim overlaps, drop empties.
        Normalize();

        // Guarantee at least one run.
        if (_runs.Count == 0)
            _runs.Add(new TextRun(0, 0, TextStyle.Default));
    }

    // ── Style operations ───────────────────────────────────────────────

    /// <summary>Apply a style to [start, start+length). Existing runs in the
    /// range are split and the inner portion gets the merged style.</summary>
    public void SetStyle(int start, int length, TextStyle style)
    {
        if (length <= 0)
            return;

        start = Clamp(start);
        var end = Clamp(start + length);
        if (end <= start)
            return;

        // Split runs at the boundaries.
        SplitAt(start);
        SplitAt(end);

        // Find all runs entirely inside [start, end) and merge style.
        for (var i = 0; i < _runs.Count; i++)
        {
            var run = _runs[i];
            if (run.Start >= start && run.End <= end)
                _runs[i] = run.WithStyle(run.Style.Merge(style));
            else if (run.Start >= end)
                break;
        }

        Normalize();
    }

    /// <summary>Clear all non-Default styling in the range (reset to base).</summary>
    public void ClearStyle(int start, int length)
    {
        if (length <= 0)
            return;

        start = Clamp(start);
        var end = Clamp(start + length);
        if (end <= start)
            return;

        SplitAt(start);
        SplitAt(end);

        for (var i = 0; i < _runs.Count; i++)
        {
            var run = _runs[i];
            if (run.Start >= start && run.End <= end)
                _runs[i] = run.WithStyle(TextStyle.Default);
            else if (run.Start >= end)
                break;
        }

        Normalize();
    }

    /// <summary>Clear all styling in the entire document.</summary>
    public void ClearAllStyles()
    {
        _runs.Clear();
        _runs.Add(new TextRun(0, _text.Length, TextStyle.Default));
    }

    /// <summary>Returns the merged style effective at the given character index.</summary>
    public TextStyle GetStyleAt(int charIndex)
    {
        charIndex = Clamp(charIndex);
        foreach (var run in _runs)
            if (run.Contains(charIndex) || (run.Length == 0 && run.Start == charIndex))
                return run.Style;
        return TextStyle.Default;
    }

    /// <summary>Toggle a boolean style flag (Bold/Italic/etc.) on the range.
    /// If the majority of the range already has the flag, it's cleared;
    /// otherwise it's set.</summary>
    public void ToggleFlag(int start, int length,
                           Func<TextStyle, bool?> getter,
                           Func<TextStyle, bool, TextStyle> setter)
    {
        if (length <= 0)
            return;

        start = Clamp(start);
        var end = Clamp(start + length);
        if (end <= start)
            return;

        // Sample the range: if every char has the flag true, clear; else set.
        var allSet = true;
        for (var i = 0; i < _runs.Count; i++)
        {
            var run = _runs[i];
            if (!run.Overlaps(start, length))
                continue;
            var flag = getter(run.Style);
            if (flag != true)
            {
                allSet = false;
                break;
            }
        }

        var newValue = !allSet;
        SetStyle(start, length, setter(TextStyle.Default, newValue));
    }

    // ── Edit operations (called by RichTextBox when text changes) ──────

    /// <summary>Insert text at the given index. Runs after the index are
    /// shifted right; the inserted text inherits the style of the run that
    /// preceded the insertion point.</summary>
    public void OnTextInsert(int index, string inserted)
    {
        if (string.IsNullOrEmpty(inserted))
            return;

        index = Clamp(index);
        var insertLen = inserted.Length;

        // The inserted text inherits the style at index (the run just before).
        var inheritedStyle = index == 0 ? TextStyle.Default : GetStyleAt(index - 1);

        // Split at index so we can insert cleanly.
        SplitAt(index);

        // Shift every run that starts at or after index.
        for (var i = 0; i < _runs.Count; i++)
        {
            var run = _runs[i];
            if (run.Start >= index)
                _runs[i] = run.WithStart(run.Start + insertLen);
        }

        // Insert the new run for the inserted text.
        var newRun = new TextRun(index, insertLen, inheritedStyle);
        var insertPos = _runs.Count;
        for (var i = 0; i < _runs.Count; i++)
        {
            if (_runs[i].Start > index)
            {
                insertPos = i;
                break;
            }
        }
        _runs.Insert(insertPos, newRun);

        _text = _text.Insert(index, inserted);
        Normalize();
    }

    /// <summary>Delete [start, start+length) from text and runs.</summary>
    public void OnTextDelete(int start, int length)
    {
        if (length <= 0)
            return;

        start = Clamp(start);
        var end = Clamp(start + length);
        if (end <= start)
            return;

        // Split at boundaries so we don't cut runs.
        SplitAt(start);
        SplitAt(end);

        // Remove runs entirely inside [start, end), trim those that overlap.
        var kept = new List<TextRun>(_runs.Count);
        foreach (var run in _runs)
        {
            if (run.End <= start)
            {
                kept.Add(run);
            }
            else if (run.Start >= end)
            {
                kept.Add(run.WithStart(run.Start - length));
            }
            else if (run.Start < start && run.End > end)
            {
                // Straddles the deleted range.
                kept.Add(new TextRun(run.Start, start - run.Start, run.Style));
                kept.Add(new TextRun(start, run.End - end, run.Style)
                    .WithStart(start));
            }
            else if (run.Start < start)
            {
                // Trim right side.
                kept.Add(new TextRun(run.Start, start - run.Start, run.Style));
            }
            else if (run.End > end)
            {
                // Trim left side, shift.
                kept.Add(new TextRun(start, run.End - end, run.Style));
            }
            // else: entirely inside the deleted range — drop.
        }

        _runs.Clear();
        _runs.AddRange(kept);
        _text = _text.Remove(start, length);
        Normalize();
    }

    /// <summary>Replace [start, start+length) with the inserted text.
    /// Equivalent to OnTextDelete + OnTextInsert but in one pass.</summary>
    public void OnTextReplace(int start, int length, string inserted)
    {
        if (length > 0)
            OnTextDelete(start, length);
        if (!string.IsNullOrEmpty(inserted))
            OnTextInsert(start, inserted);
    }

    // ── Internal helpers ───────────────────────────────────────────────

    private int Clamp(int index) => Math.Clamp(index, 0, _text.Length);

    /// <summary>Ensure a run boundary exists at the given index by splitting
    /// any run that straddles it.</summary>
    private void SplitAt(int index)
    {
        if (index < 0 || index > _text.Length)
            return;

        for (var i = 0; i < _runs.Count; i++)
        {
            var run = _runs[i];
            if (run.Start < index && run.End > index)
            {
                var left = new TextRun(run.Start, index - run.Start, run.Style);
                var right = new TextRun(index, run.End - index, run.Style);
                _runs.RemoveAt(i);
                _runs.Insert(i, left);
                _runs.Insert(i + 1, right);
                return;
            }
        }
    }

    /// <summary>Normalize the run list:
    /// 1. Drop empty runs.
    /// 2. Merge adjacent runs with equal styles.
    /// 3. Fill gaps with Default runs.
    /// 4. Sort by start (only if needed).
    ///
    /// OPTIMIZATION: in most edit paths runs are already sorted and contiguous
    /// (we only modified a local range). Skip the O(n log n) sort in that case
    /// and just do a single-pass merge. Falls back to sort if any inversion
    /// is detected during the merge pass.</summary>
    private void Normalize()
    {
        // Drop empties (rare in practice; cheap O(n)).
        for (var i = _runs.Count - 1; i >= 0; i--)
            if (_runs[i].Length == 0)
                _runs.RemoveAt(i);

        if (_runs.Count == 0)
        {
            _runs.Add(new TextRun(0, _text.Length, TextStyle.Default));
            return;
        }

        // Single-pass merge with sort detection. We build the merged list
        // in-place into a new list. If we encounter a run whose Start is
        // less than the previous run's End + 1, we mark needsSort and bail
        // to a separate sort-then-merge pass.
        var merged = new List<TextRun>(_runs.Count);
        var needsSort = false;

        // Ensure coverage starts at 0.
        if (_runs[0].Start > 0)
            merged.Add(new TextRun(0, _runs[0].Start, TextStyle.Default));

        merged.Add(_runs[0]);
        var prevEnd = _runs[0].End;

        for (var i = 1; i < _runs.Count; i++)
        {
            var run = _runs[i];

            // Detect inversion → needs sort.
            if (run.Start < prevEnd)
            {
                // Could be overlap or out-of-order. Mark and bail.
                needsSort = true;
                break;
            }

            // Fill gap.
            if (run.Start > prevEnd)
            {
                merged.Add(new TextRun(prevEnd, run.Start - prevEnd, TextStyle.Default));
            }

            // Merge if adjacent and same style.
            var last = merged[^1];
            if (last.End == run.Start && last.Style.Equals(run.Style))
            {
                merged[^1] = last.WithLength(last.Length + run.Length);
            }
            else
            {
                merged.Add(run);
            }
            prevEnd = run.End;
        }

        if (needsSort)
        {
            // Slow path: sort + redo merge.
            _runs.Sort((a, b) => a.Start.CompareTo(b.Start));
            merged.Clear();

            // Ensure coverage starts at 0.
            if (_runs[0].Start > 0)
                merged.Add(new TextRun(0, _runs[0].Start, TextStyle.Default));
            merged.Add(_runs[0]);

            for (var i = 1; i < _runs.Count; i++)
            {
                var run = _runs[i];
                var last = merged[^1];

                if (run.Start > last.End)
                    merged.Add(new TextRun(last.End, run.Start - last.End, TextStyle.Default));

                var adjacent = merged[^1];
                if (adjacent.End == run.Start && adjacent.Style.Equals(run.Style))
                    merged[^1] = adjacent.WithLength(adjacent.Length + run.Length);
                else
                    merged.Add(run);
            }
        }

        // Ensure coverage ends at _text.Length.
        if (merged[^1].End < _text.Length)
            merged.Add(new TextRun(merged[^1].End, _text.Length - merged[^1].End, TextStyle.Default));
        else if (merged[^1].End > _text.Length)
            merged[^1] = merged[^1].WithLength(_text.Length - merged[^1].Start);

        _runs.Clear();
        _runs.AddRange(merged);
    }

    /// <summary>Returns a plain-text representation with run boundaries
    /// marked. Useful for debugging.</summary>
    public string ToDebugString()
    {
        var sb = new StringBuilder();
        sb.Append("Text: ").AppendLine(_text);
        sb.AppendLine("Runs:");
        foreach (var run in _runs)
        {
            var segment = _text.Substring(run.Start, run.Length);
            sb.Append("  [").Append(run.Start).Append("..").Append(run.End).Append(") ")
              .Append(run.Style).Append("  \"").Append(segment).AppendLine("\"");
        }
        return sb.ToString();
    }
}
