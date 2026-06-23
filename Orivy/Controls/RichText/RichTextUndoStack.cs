// SPDX-License-Identifier: MIT
// Orivy RichText — RichTextUndoStack
//
// v4: Undo/redo for the rich text document. Tracks (text, runs) snapshots.
//
// The base TextBox presumably already has text undo. This stack extends it
// with run snapshots, so that bold/italic/clear-style operations can also
// be undone. The integrator wires this in by calling Snapshot() before each
// mutating op (typing, paste, SetStyle, ToggleBold, etc.) and Undo() / Redo()
// from keyboard handlers.
//
// DESIGN:
//   - Snapshots are deep copies of (text + runs).
//   - Bounded ring buffer (default 100 entries); oldest evicted.
//   - "Coalescing" — consecutive single-char typing within 500ms collapses
//     into one undo entry (avoids 1-keystroke-per-undo explosion).
//   - "Marks" — explicit barrier to prevent coalescing (e.g. before SetStyle).
//
// USAGE:
//   _undoStack.Snapshot(text, runs, coalesce: true);   // before typing
//   ...typing happens...
//   _undoStack.Snapshot(text, runs, coalesce: true);   // before next typing
//
//   _undoStack.Snapshot(text, runs, coalesce: false);  // before ToggleBold
//   ...ToggleBold happens...
//
//   _undoStack.Undo(out var prevText, out var prevRuns);  // restore
//   _undoStack.Redo(out var nextText, out var nextRuns);

using System;
using System.Collections.Generic;

namespace Orivy.Controls.RichText;

/// <summary>Snapshot of (text, runs) at a point in time. Immutable.</summary>
public readonly struct DocumentSnapshot
{
    public DocumentSnapshot(string text, IReadOnlyList<TextRun> runs, long timestamp)
    {
        Text = text;
        Runs = runs;
        Timestamp = timestamp;
    }

    public string Text { get; }
    public IReadOnlyList<TextRun> Runs { get; }
    public long Timestamp { get; }  // Environment.TickCount64 ms
}

/// <summary>Undo/redo stack for the rich text document. Bounded ring buffer
/// with optional coalescing for typing bursts.</summary>
public sealed class RichTextUndoStack
{
    private readonly LinkedList<DocumentSnapshot> _undoStack = new();
    private readonly LinkedList<DocumentSnapshot> _redoStack = new();
    private readonly int _maxEntries;
    private readonly long _coalesceWindowMs;

    private DocumentSnapshot? _lastSnapshot;
    private bool _lastWasCoalesced;

    public RichTextUndoStack(int maxEntries = 100, long coalesceWindowMs = 500)
    {
        _maxEntries = maxEntries;
        _coalesceWindowMs = coalesceWindowMs;
    }

    /// <summary>Take a snapshot of the current document state BEFORE applying
    /// a mutation. If coalesce=true and the previous snapshot was within
    /// _coalesceWindowMs and was also coalesced, the previous snapshot is
    /// REPLACED (instead of pushing a new entry) — effectively merging the
    /// two edits into one undo step.</summary>
    public void Snapshot(string text, IReadOnlyList<TextRun> runs, bool coalesce = false)
    {
        // Defensively copy runs so the snapshot is immutable.
        var runsCopy = new List<TextRun>(runs);
        var now = Environment.TickCount64;
        var snapshot = new DocumentSnapshot(text, runsCopy, now);

        // Coalesce: if last snapshot was also coalesced and within the time
        // window, replace it instead of pushing.
        if (coalesce && _lastSnapshot.HasValue && _lastWasCoalesced)
        {
            var last = _lastSnapshot.Value;
            if (now - last.Timestamp <= _coalesceWindowMs)
            {
                // Replace the tail of the undo stack.
                if (_undoStack.Count > 0)
                    _undoStack.Last!.Value = snapshot;
                else
                    _undoStack.AddLast(snapshot);
                _lastSnapshot = snapshot;
                return;
            }
        }

        // Push new entry.
        _undoStack.AddLast(snapshot);

        // Clear redo (any new edit invalidates the redo chain).
        _redoStack.Clear();

        // Enforce bound.
        while (_undoStack.Count > _maxEntries)
            _undoStack.RemoveFirst();

        _lastSnapshot = snapshot;
        _lastWasCoalesced = coalesce;
    }

    /// <summary>Undo: pop from undo stack, push current state to redo stack.
    /// Returns the previous state. Returns false if undo stack is empty.</summary>
    public bool Undo(string currentText, IReadOnlyList<TextRun> currentRuns,
                     out string prevText, out IReadOnlyList<TextRun> prevRuns)
    {
        prevText = currentText;
        prevRuns = currentRuns;

        if (_undoStack.Count == 0)
            return false;

        // Push the CURRENT state to redo (so Redo can restore it).
        var currentSnapshot = new DocumentSnapshot(
            currentText, new List<TextRun>(currentRuns), Environment.TickCount64);
        _redoStack.AddLast(currentSnapshot);

        // Pop from undo.
        var prev = _undoStack.Last!.Value;
        _undoStack.RemoveLast();

        prevText = prev.Text;
        prevRuns = prev.Runs;

        _lastSnapshot = null;
        _lastWasCoalesced = false;
        return true;
    }

    /// <summary>Redo: pop from redo stack, push current state to undo stack.
    /// Returns false if redo stack is empty.</summary>
    public bool Redo(string currentText, IReadOnlyList<TextRun> currentRuns,
                     out string nextText, out IReadOnlyList<TextRun> nextRuns)
    {
        nextText = currentText;
        nextRuns = currentRuns;

        if (_redoStack.Count == 0)
            return false;

        var currentSnapshot = new DocumentSnapshot(
            currentText, new List<TextRun>(currentRuns), Environment.TickCount64);
        _undoStack.AddLast(currentSnapshot);

        var next = _redoStack.Last!.Value;
        _redoStack.RemoveLast();

        nextText = next.Text;
        nextRuns = next.Runs;

        _lastSnapshot = null;
        _lastWasCoalesced = false;
        return true;
    }

    /// <summary>Clear all undo/redo history. Call on document load / reset.</summary>
    public void Clear()
    {
        _undoStack.Clear();
        _redoStack.Clear();
        _lastSnapshot = null;
        _lastWasCoalesced = false;
    }

    /// <summary>True if there's at least one state to undo.</summary>
    public bool CanUndo => _undoStack.Count > 0;

    /// <summary>True if there's at least one state to redo.</summary>
    public bool CanRedo => _redoStack.Count > 0;

    /// <summary>Number of undo entries.</summary>
    public int UndoCount => _undoStack.Count;

    /// <summary>Number of redo entries.</summary>
    public int RedoCount => _redoStack.Count;
}
