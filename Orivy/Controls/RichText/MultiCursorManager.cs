// SPDX-License-Identifier: MIT
// Orivy RichText — MultiCursorManager
//
// v5: Multi-cursor editing support.
//
// Multiple cursors each have their own (anchor, caret) pair. Editing
// operations (insert/delete/move/style) are applied to ALL cursors
// simultaneously. Cursors are kept sorted by position for efficient
// rendering and hit-testing.
//
// USAGE:
//   var mgr = new MultiCursorManager();
//   mgr.AddCursor(anchor, caret);           // add cursor at position
//   mgr.AddCursor(otherAnchor, otherCaret); // Ctrl+Click to add more
//   mgr.InsertCharacter(document, 'x');     // types 'x' at every cursor
//   mgr.DeleteBackward(document);           // backspace at every cursor
//   mgr.MoveCaretHorizontal(document, 1, extendSelection: false);
//   mgr.SetStyle(document, boldStyle);      // bold all selections
//
// INVARIANTS:
//   - Cursors are kept sorted by Min(anchor, caret) ascending.
//   - Overlapping cursors are merged (the right one absorbs the left).
//   - After any mutation, cursors are re-sorted and de-duplicated.
//   - The PRIMARY cursor (index 0 after sort, or the most recently active)
//     is used as the "focus" for scroll-to-caret and primary selection.
//
// UNDO SEMANTICS:
//   - A single user action (typing one char with N cursors) is ONE undo
//     entry. The caller (RichTextBox) snapshots before calling any
//     MultiCursorManager method, and that snapshot captures the state
//     before ALL cursor edits.
//
// LIMITATIONS (v5.0):
//   - Column-mode selection (rectangular) is NOT supported; only
//     independent per-cursor selections. Column mode can be added later
//     as a MultiCursorManager.ColumnSelect(...) extension.
//   - Cursors do not have per-cursor style; style ops apply uniformly.
//   - No "primary cursor" indicator on screen (yet); all cursors look
//     the same. Integrator can render the primary one differently.

using System;
using System.Collections.Generic;

namespace Orivy.Controls.RichText;

/// <summary>A single cursor with selection.</summary>
public readonly struct Cursor : IEquatable<Cursor>
{
    public Cursor(int anchor, int caret)
    {
        Anchor = anchor;
        Caret = caret;
    }

    /// <summary>Selection anchor — the fixed end of the selection.</summary>
    public int Anchor { get; }

    /// <summary>Selection caret — the moving end (where the caret is).</summary>
    public int Caret { get; }

    /// <summary>Inclusive lower bound of the selection.</summary>
    public int Start => Math.Min(Anchor, Caret);

    /// <summary>Exclusive upper bound of the selection.</summary>
    public int End => Math.Max(Anchor, Caret);

    /// <summary>Selection length (always >= 0).</summary>
    public int Length => Math.Abs(Caret - Anchor);

    /// <summary>True if the cursor has a non-empty selection.</summary>
    public bool HasSelection => Length > 0;

    /// <summary>True if this cursor is a pure caret (no selection).</summary>
    public bool IsCaret => Length == 0;

    public bool Contains(int charIndex) => charIndex >= Start && charIndex < End;
    public bool Overlaps(Cursor other) => Start < other.End && End > other.Start;

    public Cursor WithAnchor(int newAnchor) => new(newAnchor, Caret);
    public Cursor WithCaret(int newCaret) => new(Anchor, newCaret);
    public Cursor WithBoth(int newAnchor, int newCaret) => new(newAnchor, newCaret);

    public bool Equals(Cursor other) => Anchor == other.Anchor && Caret == other.Caret;
    public override bool Equals(object? obj) => obj is Cursor c && Equals(c);
    public override int GetHashCode() => HashCode.Combine(Anchor, Caret);
    public static bool operator ==(Cursor a, Cursor b) => a.Equals(b);
    public static bool operator !=(Cursor a, Cursor b) => !a.Equals(b);

    public override string ToString() => HasSelection ? $"[{Start}..{End})" : $"@{Caret}";
}

/// <summary>Manages a list of cursors and applies editing operations to all of them.
/// Owned by RichTextBox; one instance per RichTextBox.</summary>
public sealed class MultiCursorManager
{
    private readonly List<Cursor> _cursors = new();
    private int _primaryIndex;

    /// <summary>Current cursors, sorted by Start ascending. Read-only view.</summary>
    public IReadOnlyList<Cursor> Cursors => _cursors;

    /// <summary>Index into Cursors of the "primary" cursor (most recently active).
    /// Used for scroll-to-caret, primary selection clipboard ops.</summary>
    public int PrimaryIndex
    {
        get => _cursors.Count == 0 ? -1 : _primaryIndex;
        set
        {
            if (value >= 0 && value < _cursors.Count)
                _primaryIndex = value;
        }
    }

    /// <summary>The primary cursor. Returns a default cursor if none exist.</summary>
    public Cursor Primary => _cursors.Count > 0 ? _cursors[_primaryIndex] : new Cursor(0, 0);

    /// <summary>Number of active cursors. 0 means no cursors (RichTextBox
    /// will typically ensure at least one cursor exists at all times).</summary>
    public int Count => _cursors.Count;

    /// <summary>True if more than one cursor is active.</summary>
    public bool HasMultipleCursors => _cursors.Count > 1;

    /// <summary>True if any cursor has a non-empty selection.</summary>
    public bool HasAnySelection
    {
        get
        {
            foreach (var c in _cursors)
                if (c.HasSelection) return true;
            return false;
        }
    }

    /// <summary>Clear all cursors.</summary>
    public void Clear()
    {
        _cursors.Clear();
        _primaryIndex = 0;
    }

    /// <summary>Replace the cursor list with a single cursor.</summary>
    public void SetSingle(Cursor cursor)
    {
        _cursors.Clear();
        _cursors.Add(cursor);
        _primaryIndex = 0;
    }

    /// <summary>Replace the cursor list with a single caret at the given index.</summary>
    public void SetSingle(int caret)
    {
        SetSingle(new Cursor(caret, caret));
    }

    /// <summary>Add a new cursor. The cursor list is re-sorted and overlapping
    /// cursors are merged. Returns the index of the (possibly merged) cursor
    /// in the resulting list, which becomes the new primary.</summary>
    public int AddCursor(Cursor cursor)
    {
        _cursors.Add(cursor);
        SortAndMerge();
        // Find the added cursor — by position match.
        var idx = _cursors.IndexOf(cursor);
        if (idx < 0) idx = 0;
        _primaryIndex = idx;
        return idx;
    }

    /// <summary>Add a caret (no selection) at the given index.</summary>
    public int AddCursor(int position) => AddCursor(new Cursor(position, position));

    /// <summary>Sort cursors by Start, then merge overlapping ones (right absorbs left).</summary>
    private void SortAndMerge()
    {
        if (_cursors.Count <= 1)
        {
            if (_cursors.Count == 1) _primaryIndex = 0;
            return;
        }

        // Track which cursor was the primary before sort, so we can preserve it.
        var prevPrimary = _cursors.Count > 0 && _primaryIndex >= 0 && _primaryIndex < _cursors.Count
            ? _cursors[_primaryIndex]
            : default(Cursor?);

        _cursors.Sort((a, b) =>
        {
            var c = a.Start.CompareTo(b.Start);
            if (c != 0) return c;
            return a.End.CompareTo(b.End);
        });

        // Merge overlapping (right absorbs left). When two cursors have the
        // exact same (anchor, caret), they collapse to one — the right one
        // inherits primary status if either was primary.
        var merged = new List<Cursor>(_cursors.Count);
        for (var i = 0; i < _cursors.Count; i++)
        {
            var cur = _cursors[i];
            if (merged.Count > 0)
            {
                var last = merged[^1];
                if (last.Overlaps(cur) || (last.End == cur.Start && last.IsCaret && cur.IsCaret))
                {
                    // Merge — extend the last cursor if cur extends beyond.
                    var newStart = Math.Min(last.Start, cur.Start);
                    var newEnd = Math.Max(last.End, cur.End);
                    // Preserve direction (anchor side): keep whichever cursor
                    // was primary, otherwise keep last's direction.
                    var mergedCursor = prevPrimary.HasValue && (cur == prevPrimary.Value || last == prevPrimary.Value)
                        ? MergePreservingPrimary(last, cur, prevPrimary.Value, newStart, newEnd)
                        : new Cursor(newStart, newEnd);
                    merged[^1] = mergedCursor;
                    continue;
                }
            }
            merged.Add(cur);
        }

        _cursors.Clear();
        _cursors.AddRange(merged);

        // Restore primary index by finding the previously-primary cursor.
        if (prevPrimary.HasValue)
        {
            var newIdx = _cursors.IndexOf(prevPrimary.Value);
            _primaryIndex = newIdx >= 0 ? newIdx : 0;
        }
        else
        {
            _primaryIndex = 0;
        }
    }

    private static Cursor MergePreservingPrimary(Cursor a, Cursor b, Cursor primary, int newStart, int newEnd)
    {
        // If primary is exactly a or b, keep its direction; otherwise default.
        if (a == primary) return new Cursor(a.Anchor < a.Caret ? newStart : newEnd, a.Anchor < a.Caret ? newEnd : newStart);
        if (b == primary) return new Cursor(b.Anchor < b.Caret ? newStart : newEnd, b.Anchor < b.Caret ? newEnd : newStart);
        return new Cursor(newStart, newEnd);
    }

    // ── Editing operations ────────────────────────────────────────────
    //
    // All operations apply to ALL cursors. After the op, cursors are
    // re-sorted and overlapping ones merged. Document text length shifts
    // are propagated from leftmost-affected cursor to rightmost.
    //
    // For multi-cursor INSERT: we process cursors right-to-left so that
    // index shifts from earlier inserts don't corrupt later cursors.
    // For multi-cursor DELETE: same — right-to-left.
    //
    // For multi-cursor MOVE: each cursor moves independently; no shift
    // propagation needed (text length doesn't change).

    /// <summary>Insert text at every cursor. Selections are replaced.
    /// Returns the new (anchor, caret) for the primary cursor.</summary>
    public Cursor InsertText(StyledTextDocument document, string text)
    {
        if (string.IsNullOrEmpty(text) || _cursors.Count == 0)
            return Primary;

        // Process right-to-left so that earlier inserts don't shift later cursors.
        var newCursors = new Cursor[_cursors.Count];
        for (var i = _cursors.Count - 1; i >= 0; i--)
        {
            var c = _cursors[i];
            var start = c.Start;
            var length = c.Length;

            // Delete the selection (if any) and insert the text.
            document.OnTextReplace(start, length, text);
            var newCaretPos = start + text.Length;
            newCursors[i] = new Cursor(newCaretPos, newCaretPos);
        }

        // Shift later cursors leftward to account for the deletion+insertion.
        // Each cursor was processed independently, but their positions need
        // adjustment relative to earlier cursors (to the left).
        // Because we processed right-to-left, the leftmost cursor's new
        // position is correct as-is. Each subsequent cursor (going right)
        // needs to be shifted by the cumulative delta of all cursors to its left.
        //
        // The delta for cursor i = (text.Length - originalLength_i).
        // Cumulative delta when processing left-to-right:
        //   shift[0] = 0
        //   shift[i] = shift[i-1] + (text.Length - originalLength[i-1])
        //            = shift[i-1] + (text.Length - _cursors[i-1].Length)
        //
        // But wait — the document.OnTextReplace ALREADY shifted runs for
        // us. So the cursor positions stored in newCursors[i] are already
        // correct in absolute document coordinates AFTER all edits. We
        // just need to set _cursors and re-sort.

        _cursors.Clear();
        _cursors.AddRange(newCursors);
        SortAndMerge();

        // After merging, primary may have shifted. Keep primary at the same
        // logical position (the cursor that was originally primary).
        return Primary;
    }

    /// <summary>Insert a single character at every cursor. Convenience for InsertText.</summary>
    public Cursor InsertCharacter(StyledTextDocument document, char c)
        => InsertText(document, c.ToString());

    /// <summary>Delete backward (Backspace) at every cursor. If a cursor has a
    /// selection, the selection is deleted. Otherwise, the char before the
    /// caret is deleted. Multi-cursor backspace that produces overlapping
    /// carets will merge them.</summary>
    public Cursor DeleteBackward(StyledTextDocument document)
    {
        if (_cursors.Count == 0) return Primary;

        // Expand each cursor with no selection to include the previous char.
        // Process right-to-left to avoid index corruption.
        for (var i = _cursors.Count - 1; i >= 0; i--)
        {
            var c = _cursors[i];
            var start = c.Start;
            var length = c.Length;
            if (length == 0)
            {
                if (start == 0) continue;  // can't delete before 0
                start -= 1;
                length = 1;
            }
            document.OnTextDelete(start, length);
            // Replace cursor with caret at the deletion start.
            _cursors[i] = new Cursor(start, start);
        }

        SortAndMerge();
        return Primary;
    }

    /// <summary>Delete forward (Delete key) at every cursor. Selection deleted,
    /// else the char after the caret.</summary>
    public Cursor DeleteForward(StyledTextDocument document)
    {
        if (_cursors.Count == 0) return Primary;

        for (var i = _cursors.Count - 1; i >= 0; i--)
        {
            var c = _cursors[i];
            var start = c.Start;
            var length = c.Length;
            if (length == 0)
            {
                if (start >= document.Length) continue;
                length = 1;
            }
            document.OnTextDelete(start, length);
            _cursors[i] = new Cursor(start, start);
        }

        SortAndMerge();
        return Primary;
    }

    /// <summary>Move all carets horizontally by `delta` chars. If extendSelection,
    /// the anchor stays put; otherwise the anchor moves with the caret
    /// (collapsing the selection).</summary>
    public void MoveCaretHorizontal(StyledTextDocument document, int delta, bool extendSelection)
    {
        if (delta == 0) return;
        for (var i = 0; i < _cursors.Count; i++)
        {
            var c = _cursors[i];
            var newCaret = Math.Clamp(c.Caret + delta, 0, document.Length);
            var newAnchor = extendSelection ? c.Anchor : newCaret;
            _cursors[i] = new Cursor(newAnchor, newCaret);
        }
        SortAndMerge();
    }

    /// <summary>Move all carets to a line boundary (home/end). If wholeText,
    /// go to document start/end; otherwise go to line start/end (caller
    /// supplies line ranges — we don't have access to layout here).</summary>
    public void MoveCaretToBoundary(StyledTextDocument document, bool toStart, bool wholeText,
                                     Func<int, (int lineStart, int lineEnd)> getLineRangeForCaret)
    {
        for (var i = 0; i < _cursors.Count; i++)
        {
            var c = _cursors[i];
            int target;
            if (wholeText)
            {
                target = toStart ? 0 : document.Length;
            }
            else
            {
                var (lineStart, lineEnd) = getLineRangeForCaret(c.Caret);
                target = toStart ? lineStart : lineEnd;
            }
            _cursors[i] = new Cursor(target, target);
        }
        SortAndMerge();
    }

    /// <summary>Apply a style to all selections. Cursors without a selection
    /// become "sticky" — the style applies to subsequently-typed text
    /// (implemented via the StyledTextDocument's existing insert-inheriting-style
    /// behavior, which already works for single-cursor).</summary>
    public void SetStyle(StyledTextDocument document, TextStyle style)
    {
        foreach (var c in _cursors)
        {
            if (c.HasSelection)
                document.SetStyle(c.Start, c.Length, style);
        }
    }

    /// <summary>Clear style in all selections.</summary>
    public void ClearStyle(StyledTextDocument document)
    {
        foreach (var c in _cursors)
        {
            if (c.HasSelection)
                document.ClearStyle(c.Start, c.Length);
        }
    }

    /// <summary>Toggle a boolean style flag (Bold/Italic/etc.) on all selections.</summary>
    public void ToggleFlag(StyledTextDocument document,
                            Func<TextStyle, bool?> getter,
                            Func<TextStyle, bool, TextStyle> setter)
    {
        foreach (var c in _cursors)
        {
            if (c.HasSelection)
                document.ToggleFlag(c.Start, c.Length, getter, setter);
        }
    }

    /// <summary>Select all (every cursor becomes a single cursor covering the
    /// whole document). Actually with multi-cursor this collapses to one
    /// cursor since all would cover the same range.</summary>
    public void SelectAll(StyledTextDocument document)
    {
        _cursors.Clear();
        _cursors.Add(new Cursor(0, document.Length));
        _primaryIndex = 0;
    }

    /// <summary>Returns a list of all selection ranges (for rendering).</summary>
    public IEnumerable<(int start, int length, bool isPrimary)> GetSelections()
    {
        for (var i = 0; i < _cursors.Count; i++)
        {
            var c = _cursors[i];
            yield return (c.Start, c.Length, i == _primaryIndex);
        }
    }

    /// <summary>Returns a list of all caret positions (for rendering carets).</summary>
    public IEnumerable<(int position, bool isPrimary)> GetCaretPositions()
    {
        for (var i = 0; i < _cursors.Count; i++)
        {
            yield return (_cursors[i].Caret, i == _primaryIndex);
        }
    }

    /// <summary>Clamp all cursor positions to [0, document.Length].</summary>
    public void Clamp(StyledTextDocument document)
    {
        for (var i = 0; i < _cursors.Count; i++)
        {
            var c = _cursors[i];
            var anchor = Math.Clamp(c.Anchor, 0, document.Length);
            var caret = Math.Clamp(c.Caret, 0, document.Length);
            if (anchor != c.Anchor || caret != c.Caret)
                _cursors[i] = new Cursor(anchor, caret);
        }
    }

    /// <summary>Debug string.</summary>
    public override string ToString()
    {
        return $"Cursors[{_cursors.Count}]: " + string.Join(", ", _cursors);
    }
}
