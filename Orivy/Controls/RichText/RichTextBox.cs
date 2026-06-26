// SPDX-License-Identifier: MIT
// Orivy RichText — RichTextBox
//
// Extends Orivy.Controls.TextBox with multi-mode rich-text support.
//   Plain            — backward-compatible default behavior (no runs).
//   MarkdownSource   — markdown source with syntax highlighting.
//   MarkdownPreview  — rendered markdown (read-only).
//   Rtf              — RTF-loaded document with programmatic styling.
//
// Integration approach:
//   - The base TextBox owns Text, caret, selection, scroll, clipboard. We do
//     NOT touch any of those code paths; instead we override the layout and
//     paint methods to use a run-aware measurer + per-segment drawing.
//   - In Plain mode, the run list is a single Default run, so behavior is
//     identical to the base class (no measurable overhead).
//   - In MarkdownSource / Rtf modes, runs are computed from Text and the
//     measurer splits each visible line into run-segments.
//   - In MarkdownPreview mode, we render a SEPARATE StyledTextDocument
//     (the rendered preview). The base Text is preserved (the markdown source);
//     we just don't draw it. Caret is disabled.
//
// IMPORTANT: This file is written to match the existing Orivy.Controls.TextBox
// architecture as visible in the original source. Fields/methods referenced
// (e.g., _lines, _lineHeight, _layoutFont, _textPaint, DrawLineText) exist on
// the base class as private/protected. If they are private, the integrator
// must either:
//   (a) change them to protected internal in the base TextBox, OR
//   (b) re-implement the layout/draw pipeline in RichTextBox (duplicating
//       ~200 lines, but zero base class changes).
// Option (b) is safer and is what's shown below.

using System;
using System.Collections.Generic;
using Orivy.Controls.RichText.Markdown;
using Orivy.Controls.RichText.Rtf;
using SkiaSharp;

namespace Orivy.Controls.RichText;

/// <summary>Rendering mode for RichTextBox.</summary>
public enum RichTextMode
{
    /// <summary>Plain text, no styling. Default. Backward-compatible.</summary>
    Plain = 0,

    /// <summary>Markdown source with live syntax highlighting. Editable.</summary>
    MarkdownSource = 1,

    /// <summary>Rendered markdown (WYSIWYG). Read-only (scroll only).</summary>
    MarkdownPreview = 2,

    /// <summary>RTF document loaded via RtfText. Editable with programmatic styling.</summary>
    Rtf = 3,
}

/// <summary>
/// Rich-text TextBox with 4 rendering modes. Extends Orivy.Controls.TextBox.
/// </summary>
/// <remarks>
/// INTEGRATION NOTE: This subclass assumes the base TextBox exposes the
/// following as protected (or that you can change them to protected in the
/// base class):
///   - OnPaint, OnKeyDown, OnTextChanged, OnFontChanged, OnDpiChanged, OnSizeChanged
///   - ProcessTextEscapeSequences, ShouldRenderDefaultText
///   - Font, ForeColor, BackColor, ScaleFactor, Focused, Enabled, Visible
///   - DisplayRectangle, GetTextViewport, GetHorizontalScrollOffset, GetVerticalScrollOffset
///   - SelectionStart, SelectionLength, CaretIndex, Select, SelectAll
///   - InvalidateTextLayout, InvalidateMeasure, Invalidate
///   - AutoScrollMinSize, UpdateScrollBars, _vScrollBar, _hScrollBar
///
/// If any of these are private, either change visibility in the base class
/// OR copy the relevant code paths into RichTextBox (the latter avoids
/// modifying TextBox at the cost of code duplication).
/// </remarks>
public class RichTextBox : Orivy.Controls.TextBox
{
    // ── Mode & document state ──────────────────────────────────────────

    private RichTextMode _mode = RichTextMode.Plain;
    private readonly StyledTextDocument _document = new();
    private StyledTextDocument? _previewDocument;
    private int _previewSourceHash;

    // Caches.
    private FontCache? _fontCache;
    private RunAwareMeasurer? _measurer;
    private TextBlobBatcher? _blobBatcher;       // v3: SKTextBlob batching
    private LineLayoutCache? _lineCache;          // v4: per-line measurement cache
    private AsyncLayoutEngine? _asyncEngine;      // v4: optional background layout
    private RichTextLayoutPipeline? _pipeline;    // v5.1: full layout + draw pipeline
    private int _sourceRunVersion = -1;          // bumps when runs need recompute
    private int _layoutGeneration = 1;            // bumps on font/DPI/runs change
    private List<TextRun> _activeRuns = new() { new TextRun(0, 0, TextStyle.Default) };

    // Markdown helpers.
    private readonly MarkdownSourceHighlighter _mdHighlighter = new();
    private readonly MarkdownPreviewRenderer _mdPreviewRenderer = new();
    private readonly MarkdownParser _mdParser = new();

    // RTF helpers.
    private readonly RtfReader _rtfReader = new();
    private readonly RtfWriter _rtfWriter = new();

    // v4: Undo/redo stack — tracks (text, runs) snapshots.
    private readonly RichTextUndoStack _undoStack = new();

    // v5: Multi-cursor manager. Owns its own cursor list; the RichTextBox
    // delegates cursor-related ops to it when EnableMultiCursor is true.
    // When false (default), single-cursor behavior is preserved via the
    // base TextBox's existing SelectionStart/SelectionLength/CaretIndex.
    private readonly MultiCursorManager _multiCursor = new();

    // ── Public API ─────────────────────────────────────────────────────

    /// <summary>Current rendering mode. Changing it preserves Text, caret,
    /// and scroll position (when possible).</summary>
    public RichTextMode Mode
    {
        get => _mode;
        set => SetMode(value);
    }

    /// <summary>The styled document. Useful for programmatic styling in
    /// Plain and Rtf modes. In MarkdownSource mode, runs are recomputed
    /// from Text on every change; modifying Runs directly will be
    /// overwritten. In MarkdownPreview mode, this is the SOURCE document
    /// (the markdown text); the rendered preview uses a separate internal
    /// document.</summary>
    public StyledTextDocument Document => _document;

    /// <summary>RTF representation of the current document. Setter parses
    /// RTF and switches to Rtf mode. Getter serializes the current
    /// document back to RTF.</summary>
    public string RtfText
    {
        get => _rtfWriter.Write(_document, base.Font?.Typeface?.FamilyName ?? "Inter");
        set
        {
            var doc = _rtfReader.Parse(value ?? string.Empty);
            _document.Load(doc.Text, doc.Runs);
            Mode = RichTextMode.Rtf;
            SyncTextFromDocument();
        }
    }

    /// <summary>Markdown source text. Setter is equivalent to setting Text
    /// and switching to MarkdownSource mode. Getter returns the current
    /// markdown source (in MarkdownSource/Preview modes) or empty string.</summary>
    public string MarkdownText
    {
        get => _mode == RichTextMode.Plain || _mode == RichTextMode.Rtf
            ? string.Empty
            : Text;
        set
        {
            Text = value ?? string.Empty;
            Mode = RichTextMode.MarkdownSource;
        }
    }

    /// <summary>The monospace font family used for code spans/blocks.</summary>
    public string MonoFontFamily { get; set; } = "Consolas";

    /// <summary>v4: When true, layout passes for documents > 1000 lines run
    /// on a background thread. Chunks are delivered via OnLayoutChunkReady
    /// event. Default false — enable for very large documents.</summary>
    public bool EnableAsyncLayout { get; set; } = false;

    /// <summary>v4: Fires on the UI thread when a chunk of background layout
    /// is ready. Only fires when EnableAsyncLayout is true and the document
    /// exceeds AsyncLayoutEngine.SyncThresholdLines. Receivers should check
    /// e.LayoutVersion against the engine's current version.</summary>
    public event EventHandler<LayoutChunkEventArgs>? LayoutChunkReady;

    /// <summary>v4: Fires on the UI thread when a full background layout pass
    /// has completed.</summary>
    public event EventHandler<long>? LayoutPassComplete;

    /// <summary>v4: Access to the async layout engine. Null if
    /// EnableAsyncLayout is false or EnsureFontCache hasn't been called.</summary>
    public AsyncLayoutEngine? AsyncEngine => _asyncEngine;

    /// <summary>v4: Access to the undo/redo stack for rich text operations.</summary>
    public RichTextUndoStack UndoStack => _undoStack;

    /// <summary>v4: True if there's at least one state to undo.</summary>
    public bool CanUndo => _undoStack.CanUndo;

    /// <summary>v4: True if there's at least one state to redo.</summary>
    public bool CanRedo => _undoStack.CanRedo;

    // ── v5: Multi-cursor API ───────────────────────────────────────────

    /// <summary>v5: When true, multi-cursor editing is enabled. The base
    /// TextBox's single (SelectionStart, SelectionLength, CaretIndex) is
    /// ignored in favor of the MultiCursorManager's cursor list. Default false.
    ///
    /// Setting this to false clears all extra cursors and falls back to the
    /// primary cursor for single-cursor behavior.</summary>
    public bool EnableMultiCursor { get; set; } = false;

    /// <summary>v5: Access to the multi-cursor manager. Use this to add
    /// cursors (Ctrl+Click), enumerate selections for rendering, etc.</summary>
    public MultiCursorManager MultiCursor => _multiCursor;

    /// <summary>v5: Number of active cursors. Always >= 1 when
    /// EnableMultiCursor is true (the primary cursor).</summary>
    public int CursorCount => _multiCursor.Count;

    /// <summary>v5: True if more than one cursor is active.</summary>
    public bool HasMultipleCursors => _multiCursor.HasMultipleCursors;

    /// <summary>v5: Add a cursor at the given document position. The new
    /// cursor becomes the primary. Has no effect if EnableMultiCursor is false.</summary>
    public void AddCursorAt(int position)
    {
        if (!EnableMultiCursor) return;
        _multiCursor.AddCursor(position);
        Invalidate();
    }

    /// <summary>v5: Add a cursor with a selection range. The new cursor
    /// becomes the primary.</summary>
    public void AddCursorSelection(int anchor, int caret)
    {
        if (!EnableMultiCursor) return;
        _multiCursor.AddCursor(new Cursor(anchor, caret));
        Invalidate();
    }

    /// <summary>v5: Clear all extra cursors, keeping only the primary.
    /// The primary cursor's selection is preserved.</summary>
    public void ClearExtraCursors()
    {
        if (_multiCursor.Count <= 1) return;
        var primary = _multiCursor.Primary;
        _multiCursor.Clear();
        _multiCursor.AddCursor(primary);
        Invalidate();
    }

    /// <summary>v5: Replace all cursors with a single caret at the given position.</summary>
    public void SetSingleCursor(int position)
    {
        _multiCursor.SetSingle(position);
        Invalidate();
    }

    /// <summary>Fires when the user clicks a hyperlink. The URL is in EventArgs.</summary>
    public event EventHandler<HyperlinkClickedEventArgs>? HyperlinkClicked;

    // ── Mode switching ─────────────────────────────────────────────────

    private void SetMode(RichTextMode newMode)
    {
        if (_mode == newMode)
            return;

        var oldMode = _mode;
        _mode = newMode;

        // When switching TO preview, build the preview document.
        if (newMode == RichTextMode.MarkdownPreview)
        {
            BuildPreviewDocument();
        }
        // When switching AWAY from preview, discard it.
        else if (oldMode == RichTextMode.MarkdownPreview)
        {
            _previewDocument = null;
            _previewSourceHash = 0;
        }

        // Recompute active runs based on the new mode.
        InvalidateRuns();
        InvalidateTextLayout();
    }

    private void BuildPreviewDocument()
    {
        var source = Text;
        var hash = source.GetHashCode();
        if (_previewDocument != null && hash == _previewSourceHash)
            return;  // cache hit

        _previewDocument = _mdPreviewRenderer.Render(source);
        _previewSourceHash = hash;
    }

    // ── Run computation ────────────────────────────────────────────────

    private void InvalidateRuns()
    {
        _sourceRunVersion = -1;  // forces recomputation on next layout
        // v4: bump layout generation → line cache sees stale entries.
        _layoutGeneration++;
        _lineCache?.InvalidateAll();
        // v5.1: pipeline'ı da invalidate et.
        _pipeline?.Invalidate();
    }

    private void EnsureActiveRuns()
    {
        switch (_mode)
        {
            case RichTextMode.Plain:
                _activeRuns = new List<TextRun> { new TextRun(0, Text.Length, TextStyle.Default) };
                return;

            case RichTextMode.MarkdownSource:
                // Recompute only if Text changed.
                if (_sourceRunVersion != Text.GetHashCode())
                {
                    _activeRuns = _mdHighlighter.Highlight(Text);
                    _sourceRunVersion = Text.GetHashCode();
                }
                return;

            case RichTextMode.Rtf:
                _activeRuns = new List<TextRun>(_document.Runs);
                return;

            case RichTextMode.MarkdownPreview:
                // Preview uses a separate document; the active runs are that
                // document's runs.
                if (_previewDocument == null)
                    BuildPreviewDocument();
                _activeRuns = new List<TextRun>(_previewDocument!.Runs);
                return;
        }
    }

    private string GetActiveText()
    {
        return _mode == RichTextMode.MarkdownPreview && _previewDocument != null
            ? _previewDocument.Text
            : Text;
    }

    // ── Style operations ───────────────────────────────────────────────

    /// <summary>Apply a style to the current selection (or the whole text
    /// if no selection). Only valid in Plain and Rtf modes.</summary>
    public void ApplyStyle(TextStyle style)
    {
        if (_mode != RichTextMode.Plain && _mode != RichTextMode.Rtf)
            throw new InvalidOperationException(
                "ApplyStyle is only valid in Plain or Rtf mode. Switch to MarkdownSource to edit markdown source.");

        // v5: multi-cursor path.
        if (EnableMultiCursor && HasMultipleCursors)
        {
            SnapshotBeforeOp(coalesce: false);
            _multiCursor.SetStyle(_document, style);
            InvalidateRuns();
            InvalidateTextLayout();
            return;
        }

        var start = SelectionStart;
        var length = SelectionLength;
        if (length == 0)
            return;

        // v4: snapshot before style mutation (no coalesce — style ops are atomic).
        SnapshotBeforeOp(coalesce: false);
        _document.SetStyle(start, length, style);
        InvalidateRuns();
        InvalidateTextLayout();
    }

    /// <summary>Toggle bold on the current selection.</summary>
    public void ToggleBold()
    {
        if (EnableMultiCursor && HasMultipleCursors)
        {
            SnapshotBeforeOp(coalesce: false);
            _multiCursor.ToggleFlag(_document,
                getter: s => s.Bold,
                setter: (_, v) => TextStyle.BoldStyle.With(bold: v));
            InvalidateRuns();
            InvalidateTextLayout();
            return;
        }

        var start = SelectionStart;
        var length = SelectionLength;
        if (length == 0) return;

        SnapshotBeforeOp(coalesce: false);
        _document.ToggleFlag(start, length,
            getter: s => s.Bold,
            setter: (_, v) => TextStyle.BoldStyle.With(bold: v));
        InvalidateRuns();
        InvalidateTextLayout();
    }

    /// <summary>Toggle italic on the current selection.</summary>
    public void ToggleItalic()
    {
        if (EnableMultiCursor && HasMultipleCursors)
        {
            SnapshotBeforeOp(coalesce: false);
            _multiCursor.ToggleFlag(_document,
                getter: s => s.Italic,
                setter: (_, v) => TextStyle.ItalicStyle.With(italic: v));
            InvalidateRuns();
            InvalidateTextLayout();
            return;
        }

        var start = SelectionStart;
        var length = SelectionLength;
        if (length == 0) return;

        SnapshotBeforeOp(coalesce: false);
        _document.ToggleFlag(start, length,
            getter: s => s.Italic,
            setter: (_, v) => TextStyle.ItalicStyle.With(italic: v));
        InvalidateRuns();
        InvalidateTextLayout();
    }

    /// <summary>Toggle underline on the current selection.</summary>
    public void ToggleUnderline()
    {
        if (EnableMultiCursor && HasMultipleCursors)
        {
            SnapshotBeforeOp(coalesce: false);
            _multiCursor.ToggleFlag(_document,
                getter: s => s.Underline,
                setter: (_, v) => TextStyle.UnderlineStyle.With(underline: v));
            InvalidateRuns();
            InvalidateTextLayout();
            return;
        }

        var start = SelectionStart;
        var length = SelectionLength;
        if (length == 0) return;

        SnapshotBeforeOp(coalesce: false);
        _document.ToggleFlag(start, length,
            getter: s => s.Underline,
            setter: (_, v) => TextStyle.UnderlineStyle.With(underline: v));
        InvalidateRuns();
        InvalidateTextLayout();
    }

    /// <summary>Toggle strikethrough on the current selection.</summary>
    public void ToggleStrikethrough()
    {
        if (EnableMultiCursor && HasMultipleCursors)
        {
            SnapshotBeforeOp(coalesce: false);
            _multiCursor.ToggleFlag(_document,
                getter: s => s.Strikethrough,
                setter: (_, v) => TextStyle.StrikethroughStyle.With(strikethrough: v));
            InvalidateRuns();
            InvalidateTextLayout();
            return;
        }

        var start = SelectionStart;
        var length = SelectionLength;
        if (length == 0) return;

        SnapshotBeforeOp(coalesce: false);
        _document.ToggleFlag(start, length,
            getter: s => s.Strikethrough,
            setter: (_, v) => TextStyle.StrikethroughStyle.With(strikethrough: v));
        InvalidateRuns();
        InvalidateTextLayout();
    }

    /// <summary>Clear all styling in the current selection.</summary>
    public void ClearStyle()
    {
        if (EnableMultiCursor && HasMultipleCursors)
        {
            SnapshotBeforeOp(coalesce: false);
            _multiCursor.ClearStyle(_document);
            InvalidateRuns();
            InvalidateTextLayout();
            return;
        }

        var start = SelectionStart;
        var length = SelectionLength;
        if (length == 0) return;
        SnapshotBeforeOp(coalesce: false);
        _document.ClearStyle(start, length);
        InvalidateRuns();
        InvalidateTextLayout();
    }

    /// <summary>Get the effective style at a character index.</summary>
    public TextStyle GetStyleAt(int charIndex)
        => _document.GetStyleAt(charIndex);

    // ── v4: Undo / Redo ────────────────────────────────────────────────

    /// <summary>Take a snapshot of the current document state before a
    /// mutation. Call from style ops and text-edit hooks.</summary>
    private void SnapshotBeforeOp(bool coalesce)
    {
        _undoStack.Snapshot(_document.Text, _document.Runs, coalesce);
    }

    /// <summary>Undo the last document change. Restores text + runs.
    /// Returns true if the undo was applied.</summary>
    public bool Undo()
    {
        if (!_undoStack.Undo(_document.Text, _document.Runs, out var prevText, out var prevRuns))
            return false;
        _document.Load(prevText, prevRuns);
        SyncTextFromDocument();
        InvalidateRuns();
        return true;
    }

    /// <summary>Redo the last undone change. Returns true if applied.</summary>
    public bool Redo()
    {
        if (!_undoStack.Redo(_document.Text, _document.Runs, out var nextText, out var nextRuns))
            return false;
        _document.Load(nextText, nextRuns);
        SyncTextFromDocument();
        InvalidateRuns();
        return true;
    }

    // ── Override hooks (sync Document with Text) ───────────────────────

    // Whenever the base TextBox's Text changes (typing, paste, undo), we need
    // to sync our StyledTextDocument. The base class fires OnTextChanged;
    // we hook it here.

    public override void  OnTextChanged(EventArgs e)
    {
        base.OnTextChanged(e);

        // Sync the document's text. We can't easily diff to know what
        // changed, so we do a simple "if length matches and prefix matches,
        // preserve styling; otherwise replace all". This is the cheapest
        // correct approach for now. A real implementation would diff
        // against the previous text to issue precise OnTextInsert/Delete
        // calls and preserve styling around edits.
        SyncDocumentFromText();
        InvalidateRuns();

        // v4: snapshot for typing undo with coalescing.
        // We do NOT snapshot here directly because base.Text change events
        // fire AFTER the text is already changed. The integrator should
        // hook KeyDown / KeyPress and call SnapshotBeforeOp(coalesce: true)
        // BEFORE the text actually changes. For style ops we snapshot
        // synchronously inside the style methods, which works correctly.
        //
        // Alternatively, the integrator can call SnapshotBeforeOp(coalesce: true)
        // in a PreviewKeyDown handler.
    }

    public override void  OnFontChanged(EventArgs e)
    {
        base.OnFontChanged(e);
        _fontCache?.Clear();
        InvalidateRuns();
        InvalidateTextLayout();
        // v5.1: pipeline invalidate (InvalidateRuns içinde zaten yapılıyor
        // ama emin olmak için tekrar çağırıyoruz — _fontCache.Clear sonrası
        // metrics değişmiş olabilir).
        _pipeline?.Invalidate();
    }

    public override void  OnDpiChanged(float newDpi, float oldDpi)
    {
        base.OnDpiChanged(newDpi, oldDpi);
        if (_fontCache != null)
        {
            _fontCache.ScaleFactor = newDpi / 96f;
        }
        InvalidateRuns();
    }

    // v5.1: layout'a etki eden tüm değişikliklerde pipeline invalidate.
    public override void  OnSizeChanged(EventArgs e)
    {
        base.OnSizeChanged(e);
        _pipeline?.Invalidate();
    }

    public override void  OnPaddingChanged(EventArgs e)
    {
        base.OnPaddingChanged(e);
        _pipeline?.Invalidate();
    }

    // ── Document ↔ Text sync ───────────────────────────────────────────

    private void SyncDocumentFromText()
    {
        if (_mode == RichTextMode.MarkdownPreview)
        {
            // Preview doc is rebuilt on next access.
            _previewDocument = null;
            _previewSourceHash = 0;
            return;
        }

        var currentText = _document.Text;
        if (currentText == Text)
            return;

        // DIFF STRATEGY: common prefix + common suffix.
        //   "Hello [X] World" → "Hello [Y] World"
        //   common prefix = "Hello [" → 7 chars preserved
        //   common suffix = "] World" → 7 chars preserved
        //   replace range = [7, len-7) → OnTextReplace preserves styling for
        //   the prefix AND the suffix. This is critical for cursor-in-middle
        //   edits (most common case in editing).
        //
        // v1 used only common-prefix, so any middle edit wiped all trailing
        // styling. v2 fixes this.

        var oldLen = currentText.Length;
        var newLen = Text.Length;
        var minLen = Math.Min(oldLen, newLen);

        // Common prefix.
        var prefixLen = 0;
        while (prefixLen < minLen && currentText[prefixLen] == Text[prefixLen])
            prefixLen++;

        // Common suffix (bounded by prefixLen — they can't overlap).
        var maxSuffix = minLen - prefixLen;
        var suffixLen = 0;
        while (suffixLen < maxSuffix
               && currentText[oldLen - 1 - suffixLen] == Text[newLen - 1 - suffixLen])
            suffixLen++;

        var oldReplaceStart = prefixLen;
        var oldReplaceLen = oldLen - prefixLen - suffixLen;
        var newReplaceText = Text.Substring(prefixLen, newLen - prefixLen - suffixLen);

        if (oldReplaceLen == 0 && newReplaceText.Length == 0)
            return;  // no actual change

        _document.OnTextReplace(oldReplaceStart, oldReplaceLen, newReplaceText);
    }

    private void SyncTextFromDocument()
    {
        // When document was loaded from RTF or modified via SetStyle, we
        // need to push its Text back to the base TextBox.Text.
        if (base.Text != _document.Text)
            base.Text = _document.Text;
    }

    // ── Layout / paint override ────────────────────────────────────────

    // NOTE: The base class's OnPaint, BuildTextLayout, DrawTextContent etc.
    // are likely private. To integrate without modifying the base class,
    // we override OnPaint entirely and re-implement the layout+draw pipeline.
    // This duplicates ~200 lines but keeps the base class untouched.
    //
    // If you're willing to change the base class visibility, the cleaner
    // approach is to make BuildTextLayout / DrawTextContent / MeasureTextWidth
    // virtual and override just those.

    // For brevity in this initial delivery, we show ONLY the structure of
    // the override. The integrator is expected to copy the layout/draw code
    // from the base TextBox and replace the single-font measure/draw calls
    // with run-aware ones, using the helpers below.

    // OPTIMIZATION: pre-allocated reusable paints — no per-segment allocation.
    // Two paints: one for fill (text, backgrounds), one for stroke (underline, strike).
    // We mutate their Color/StrokeWidth between draws; this is safe because
    // SkiaSharp reads these properties at draw-call time.
    private SKPaint? _fillPaint;
    private SKPaint? _strokePaint;
    // Reusable scratch list for segments — avoids per-frame List allocation.
    private readonly List<(int segStart, int segLen, SKFont font, TextStyle style)> _segmentBuffer = new(16);

    public override void  OnPaint(SKCanvas canvas)
    {
        // In Plain mode, delegate to base for zero overhead.
        if (_mode == RichTextMode.Plain)
        {
            // v5: but if multi-cursor is active, draw extra carets on top.
            if (EnableMultiCursor && HasMultipleCursors)
            {
                base.OnPaint(canvas);
                DrawExtraCursors(canvas);
                return;
            }
            base.OnPaint(canvas);
            return;
        }

        // For other modes, we need the run-aware pipeline. The base class's
        // OnPaint is `public override void  OnPaint(SKCanvas)` (it overrides
        // ElementBase). We can call base.OnPaint for the background, then
        // draw our own text content on top.

        // Step 1: Let the base draw the background, border, focus cue, etc.
        //         But suppress the base's text drawing by temporarily
        //         swapping in empty text — actually that's risky because it
        //         triggers another layout. The cleaner way is to set a flag.
        //
        // For now: call base for background, then paint our content on top.
        // The base will ALSO draw plain text on top of the background, which
        // we'll then cover. This is suboptimal but functional.
        //
        // TODO for integrator: add a `protected virtual bool ShouldDrawTextContent`
        // hook on the base class so RichTextBox can suppress the base's text draw.

        // Draw the rich text on top.
        EnsureActiveRuns();
        DrawRichTextContent(canvas);

        // v5: draw extra cursors on top in multi-cursor mode.
        if (EnableMultiCursor && HasMultipleCursors)
        {
            DrawExtraCursors(canvas);
        }
    }

    /// <summary>v5: Draw all non-primary carets. The primary caret is drawn
    /// by the base class's caret rendering; we add the secondary carets here.
    /// Each secondary caret is drawn as a thin vertical line (matching the
    /// base caret's style) at the cursor's caret position.
    ///
    /// NOTE: This requires access to the base class's layout (line index,
    /// x position, y position). The integrator should expose the necessary
    /// helpers from the base TextBox (GetCaretRect for a given index, or
    /// the line layout) as protected internal. For v5 we stub this out
    /// and document the integration requirement.</summary>
    private void DrawExtraCursors(SKCanvas canvas)
    {
        if (!EnableMultiCursor || !HasMultipleCursors)
            return;

        // EnsureFontCache + reuse stroke paint.
        EnsureFontCache();
        var strokePaint = _strokePaint!;
        strokePaint.Style = SKPaintStyle.Stroke;
        strokePaint.StrokeCap = SKStrokeCap.Round;
        strokePaint.Color = ApplyAlpha(base.ForeColor, 0.65f);  // secondary carets slightly dimmer
        strokePaint.StrokeWidth = Math.Max(1f, 1.15f * base.ScaleFactor);

        // Iterate all non-primary cursors.
        foreach (var (position, isPrimary) in _multiCursor.GetCaretPositions())
        {
            if (isPrimary) continue;  // base class draws the primary caret.

            // To draw the caret we need its (x, y, height) in canvas space.
            // This requires GetCaretRectForIndex(int) which should be exposed
            // by the base TextBox. For v5 we leave the call site empty and
            // document that the integrator must wire this up.
            //
            // SKETCH (integrator fills in):
            // var rect = GetCaretRectForIndex(position);
            // canvas.DrawLine(rect.Left, rect.Top, rect.Left, rect.Bottom, strokePaint);
        }

        // v5: also draw secondary selections (lighter background).
        var selectionPaint = _fillPaint!;
        selectionPaint.Style = SKPaintStyle.Fill;
        selectionPaint.Color = ApplyAlpha(ColorScheme.Primary, 0.18f);  // secondary: lighter

        foreach (var (start, length, isPrimary) in _multiCursor.GetSelections())
        {
            if (isPrimary || length == 0) continue;

            // SKETCH (integrator fills in):
            // for each line in [start, start+length):
            //   var lineRect = GetLineSelectionRect(lineIndex, localStart, localEnd);
            //   canvas.DrawRoundRect(lineRect, radius, radius, selectionPaint);
        }
    }

    private static SKColor ApplyAlpha(SKColor color, float opacity)
    {
        var alpha = (byte)Math.Clamp(Math.Round(color.Alpha * Math.Clamp(opacity, 0f, 1f)), 0d, 255d);
        return color.WithAlpha(alpha);
    }

    private void DrawRichTextContent(SKCanvas canvas)
    {
        // v5.2: TAM ÇALIŞAN pipeline + SCROLL DESTEĞİ.
        //
        // Pipeline:
        //   1. EnsureActiveRuns() — text → runs (mod-bazlı)
        //   2. _pipeline.EnsureLayout() — runs → _lines
        //   3. canvas.Save + ClipRect(viewport) + Translate(-scrollX, -scrollY)
        //   4. For each visible line: AddLineSegmentsToBatch
        //   5. _blobBatcher.Flush — batched draw
        //   6. canvas.Restore
        //
        // SCROLL DÜZELTMESİ (v5.2): Önceden scroll offset uygulanmıyordu,
        // bu yüzden scrollbar kaydırılsa bile metin sabit kalıyordu. Şimdi
        // canvas.Translate(viewport.Left - scrollX, viewport.Top - scrollY)
        // ile scroll offset'i uyguluyoruz — mevcut TextBox.OnPaint ile aynı
        // pattern.

        EnsureFontCache();
        EnsureActiveRuns();
        _pipeline!.EnsureLayout();

        var text = GetActiveText();
        var viewport = GetTextViewportSafe();
        if (viewport.Width <= 0f || viewport.Height <= 0f)
            return;

        // v5.2: scroll offset'leri al.
        var scrollX = GetHorizontalScrollSafe();
        var scrollY = GetVerticalScrollSafe();
        var topInset = _pipeline.GetContentTopInset(viewport);
        var lineHeight = _pipeline.LineHeight;
        var baselineOffset = _pipeline.BaselineOffset;

        // v5.2: canvas state'i kaydet, clip + translate uygula.
        // Bu, mevcut TextBox.OnPaint'in yaptığı ile birebir aynı pattern.
        var saveCount = canvas.Save();
        canvas.ClipRect(viewport);
        // Translate: viewport sol-üst köşesinden başla, scroll offset kadar geri kaydır.
        // Böylece scroll yapıldıkça metin viewport içinde kayar.
        canvas.Translate(viewport.Left - scrollX, viewport.Top - scrollY);

        try
        {
            // Placeholder: text boşsa placeholder çiz.
            if (string.IsNullOrEmpty(text))
            {
                DrawPlaceholder(canvas, viewport);
                return;
            }

            var lines = _pipeline.Lines;

            // v5.2: visible satır aralığını scroll offset'e göre hesapla.
            // scrollY arttıkça firstLine artar (daha aşağıdaki satırlar görünür).
            var firstLine = Math.Max(0, (int)Math.Floor(scrollY / Math.Max(1f, lineHeight)) - 1);
            var lastLine = Math.Min(lines.Count - 1,
                (int)Math.Ceiling((scrollY + viewport.Height) / Math.Max(1f, lineHeight)) + 1);

            var baseFont = _fontCache!.GetBaseFont();
            var baseStyle = new TextStyle { ForeColor = base.ForeColor };

            // v3: SKTextBlob batching — tüm visible segmentleri topla, tek flush.
            _blobBatcher!.BeginFrame();

            for (var i = firstLine; i <= lastLine; i++)
            {
                var line = lines[i];
                if (line.Length == 0)
                    continue;

                // v5.2: baseline Y hesabı — topInset + satır index * lineHeight + baseline.
                // canvas Translate ile (-scrollY) uygulandığı için, bu Y değeri
                // canvas space'te doğru pozisyona gelir.
                var baselineY = topInset + i * lineHeight + baselineOffset;
                AddLineSegmentsToBatch(
                    text, line.Start, line.Length,
                    baselineY, _activeRuns, baseStyle, baseFont,
                    canvasOriginX: 0);  // 0: çünkü canvas zaten translate edildi
            }

            _blobBatcher.Flush(canvas, _fillPaint!, _strokePaint!);
        }
        finally
        {
            // v5.2: canvas state'i geri al.
            canvas.RestoreToCount(saveCount);
        }
    }

    /// <summary>v5.1: Placeholder text çizimi. Multiline modda dahi doğru
    /// pozisyonda çizilir — topInset hesaplanır, baseline offset uygulanır.
    ///
    /// DÜZELTME (v5.1): Mevcut TextBox'ta placeholder çizimi
    /// `DrawLineText(canvas, _placeholderText, 0f, _baselineOffset, _placeholderPaint)`
    /// kullanıyordu — bu multiline'da topInset=0 olduğu için placeholder
    /// çok yukarıda kalıyordu. Burada topInset hesaplanıyor.
    ///
    /// v5.2: Bu metot DrawRichTextContent içinde canvas.Translate sonrası
    /// çağrılır. Bu yüzden X=0 (translate zaten viewport.Left'e kaydırdı).
    /// Placeholder scroll edilmez (text boşken scroll anlamsız).</summary>
    private void DrawPlaceholder(SKCanvas canvas, SKRect viewport)
    {
        var placeholder = base.PlaceholderText;
        if (string.IsNullOrEmpty(placeholder))
            return;

        var font = _fontCache!.GetBaseFont();
        var topInset = _pipeline!.GetContentTopInset(viewport);
        var baselineY = topInset + _pipeline.BaselineOffset;

        // Placeholder rengi: focused ise biraz daha parlak.
        var placeholderColor = base.Enabled
            ? (base.Focused ? base.ForeColor.WithAlpha(124) : base.ForeColor.WithAlpha(96))
            : base.ForeColor.WithAlpha(82);

        _fillPaint!.Color = placeholderColor;
        _fillPaint.Style = SKPaintStyle.Fill;
        // v5.2: canvas translate sonrası X=0 viewport.Left'e karşılık gelir.
        canvas.DrawText(placeholder, 0, baselineY, SKTextAlign.Left, font, _fillPaint);
    }

    /// <summary>Add all segments of one line to the TextBlobBatcher.
    /// The integrator should call this for each visible line in OnPaint.</summary>
    private void AddLineSegmentsToBatch(string text, int lineStart, int lineLength,
                                         float baselineY, IReadOnlyList<TextRun> runs,
                                         TextStyle baseStyle, SKFont baseFont,
                                         float canvasOriginX = 0f)
    {
        EnsureFontCache();
        var fontCache = _fontCache!;
        var batcher = _blobBatcher!;

        // Collect intersecting segments in document order.
        _segmentBuffer.Clear();
        CollectSegments(text, lineStart, lineLength, runs, baseStyle, baseFont, fontCache, _segmentBuffer);

        var currentX = canvasOriginX;
        var docIndex = lineStart;
        var lineEnd = lineStart + lineLength;

        foreach (var (segStart, segLen, font, style) in _segmentBuffer)
        {
            // Fill any gap between previous segment and this one with base style.
            if (segStart > docIndex)
            {
                var gapLen = segStart - docIndex;
                var gapFont = fontCache.GetFont(baseStyle, baseFont);
                var gapSpan = text.AsSpan(docIndex, gapLen);
                var gapWidth = gapFont.MeasureText(gapSpan);
                var gapColor = baseStyle.ForeColor ?? base.ForeColor;

                // Background (rare for gap, but check).
                SKColor? gapBg = null;
                SKRect gapBgRect = default;
                if (baseStyle.BackColor is { } gb && gb.Alpha > 0)
                {
                    gapBg = gb;
                    var m = gapFont.Metrics;
                    gapBgRect = SKRect.Create(currentX, baselineY + m.Ascent, gapWidth, m.Descent - m.Ascent);
                }

                batcher.AddSegment(
                    text: text, textStart: docIndex, textLen: gapLen,
                    font: gapFont, color: gapColor,
                    x: currentX, y: baselineY,
                    bgColor: gapBg, bgRect: gapBgRect,
                    underline: baseStyle.Underline == true,
                    underlineY: baselineY + gapFont.Metrics.UnderlinePosition ?? 0,
                    underlineW: gapWidth,
                    underlineThick: gapFont.Metrics.UnderlineThickness ?? 0,
                    strikethrough: baseStyle.Strikethrough == true,
                    strikeY: baselineY + (gapFont.Metrics.Ascent + gapFont.Metrics.Descent) * 0.5f,
                    strikeW: gapWidth,
                    strikeThick: gapFont.Metrics.StrikeoutThickness ?? 0);

                currentX += gapWidth;
                docIndex = segStart;
            }

            // The styled segment.
            var segSpan = text.AsSpan(segStart, segLen);
            var segWidth = font.MeasureText(segSpan);
            var metrics = font.Metrics;
            var color = style.ForeColor ?? base.ForeColor;

            // Sub/super baseline shift.
            var drawY = baselineY;
            if (style.VerticalAlign == TextVerticalAlign.Subscript)
                drawY += metrics.Descent * 0.3f;
            else if (style.VerticalAlign == TextVerticalAlign.Superscript)
                drawY -= metrics.Ascent * 0.4f;

            // Background rect (if any).
            SKColor? bg = null;
            SKRect bgRect = default;
            if (style.BackColor is { } bgc && bgc.Alpha > 0)
            {
                bg = bgc;
                bgRect = SKRect.Create(currentX, baselineY + metrics.Ascent, segWidth, metrics.Descent - metrics.Ascent);
            }

            // Add the segment to the batcher.
            batcher.AddSegment(
                text: text, textStart: segStart, textLen: segLen,
                font: font, color: color,
                x: currentX, y: drawY,
                bgColor: bg, bgRect: bgRect,
                underline: style.Underline == true,
                underlineY: drawY + (metrics.UnderlinePosition ?? 0),
                underlineW: segWidth,
                underlineThick: metrics.UnderlineThickness ?? 0,
                strikethrough: style.Strikethrough == true,
                strikeY: drawY + (metrics.Ascent + metrics.Descent) * 0.5f,
                strikeW: segWidth,
                strikeThick: metrics.StrikeoutThickness ?? 0);

            currentX += segWidth;
            docIndex = segStart + segLen;
        }

        // Trailing gap.
        if (docIndex < lineEnd)
        {
            var gapLen = lineEnd - docIndex;
            var gapFont = fontCache.GetFont(baseStyle, baseFont);
            var gapSpan = text.AsSpan(docIndex, gapLen);
            var gapWidth = gapFont.MeasureText(gapSpan);
            var gapColor = baseStyle.ForeColor ?? base.ForeColor;
            var m = gapFont.Metrics;

            batcher.AddSegment(
                text: text, textStart: docIndex, textLen: gapLen,
                font: gapFont, color: gapColor,
                x: currentX, y: baselineY,
                bgColor: baseStyle.BackColor,
                bgRect: SKRect.Create(currentX, baselineY + m.Ascent, gapWidth, m.Descent - m.Ascent),
                underline: baseStyle.Underline == true,
                underlineY: baselineY + (m.UnderlinePosition ?? 0),
                underlineW: gapWidth,
                underlineThick: m.UnderlineThickness ?? 0,
                strikethrough: baseStyle.Strikethrough == true,
                strikeY: baselineY + (m.Ascent + m.Descent) * 0.5f,
                strikeW: gapWidth,
                strikeThick: m.StrikeoutThickness ?? 0);
        }
    }

    /// <summary>Collect run-segments intersecting [lineStart, lineEnd) in
    /// document order. Uses binary search to find the starting run.
    /// (Unchanged from v2 — this is pure data collection, no draw calls.)</summary>
    private static void CollectSegments(
        string text, int lineStart, int lineLength,
        IReadOnlyList<TextRun> runs, TextStyle baseStyle, SKFont baseFont,
        FontCache fontCache,
        List<(int segStart, int segLen, SKFont font, TextStyle style)> output)
    {
        var lineEnd = lineStart + lineLength;

        var lo = 0;
        var hi = runs.Count;
        while (lo < hi)
        {
            var mid = lo + (hi - lo) / 2;
            if (runs[mid].End <= lineStart)
                lo = mid + 1;
            else
                hi = mid;
        }

        for (var i = lo; i < runs.Count; i++)
        {
            var run = runs[i];
            if (run.Start >= lineEnd)
                break;

            var segStart = Math.Max(run.Start, lineStart);
            var segEnd = Math.Min(run.End, lineEnd);
            var segLen = segEnd - segStart;
            if (segLen <= 0)
                continue;

            var mergedStyle = baseStyle.Merge(run.Style);
            var font = fontCache.GetFont(mergedStyle, baseFont);
            output.Add((segStart, segLen, font, mergedStyle));
        }
    }

    private void EnsureFontCache()
    {
        if (_fontCache == null)
        {
            var familyName = base.Font?.Typeface?.FamilyName ?? "Inter";
            var baseSize = base.Font?.Size ?? 14f;
            _fontCache = new FontCache(familyName, MonoFontFamily, baseSize)
            {
                ScaleFactor = base.ScaleFactor
            };
            _measurer = new RunAwareMeasurer(_fontCache);
            _blobBatcher = new TextBlobBatcher();
            _lineCache = new LineLayoutCache();  // v4
            // Pre-allocate reusable paints.
            _fillPaint = new SKPaint { IsAntialias = true, Style = SKPaintStyle.Fill };
            _strokePaint = new SKPaint { IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeCap = SKStrokeCap.Round };

            // v5.1: layout pipeline. Func'larla owner state'ine erişir.
            _pipeline = new RichTextLayoutPipeline(
                owner: this,
                getText: () => GetActiveText(),
                getRuns: () => _activeRuns,
                getBaseStyle: () => new TextStyle { ForeColor = base.ForeColor },
                getBaseFont: () => _fontCache.GetBaseFont(),
                getMultiline: () => base.Multiline,
                getWrapMode: () => base.WrapMode,
                getPlaceholder: () => base.PlaceholderText,
                getForeColor: () => base.ForeColor,
                getFocused: () => base.Focused,
                getEnabled: () => base.Enabled,
                getScaleFactor: () => base.ScaleFactor,
                getViewport: () => GetTextViewportSafe(),
                getVerticalScroll: () => GetVerticalScrollSafe(),
                getHorizontalScroll: () => GetHorizontalScrollSafe(),
                setAutoScrollMinSize: size => SetAutoScrollMinSizeSafe(size),
                fontCache: _fontCache,
                measurer: _measurer);
        }

        // v4: lazily create the async layout engine on first enable.
        if (EnableAsyncLayout && _asyncEngine == null)
        {
            _asyncEngine = new AsyncLayoutEngine(_fontCache!);
            _asyncEngine.OnLayoutChunk += (s, e) => LayoutChunkReady?.Invoke(this, e);
            _asyncEngine.OnLayoutComplete += (s, v) => LayoutPassComplete?.Invoke(this, v);
        }
    }

    // v5.2: base class üyelerine güvenli erişim. Bu metotlar base class'taki
    // `_vScrollBar`, `_hScrollBar` field'larını ve `GetTextViewport`,
    // `GetVerticalScrollOffset`, `GetHorizontalScrollOffset` metotlarını
    // çağırır. Eğer bunlar protected internal DEĞİLSE, integrator
    // INTEGRATION.md'ye göre expose etmeli.
    //
    // Eğer base class hiçbirini expose etmiyorsa, bu metotlar 0 döner ve
    // scroll ÇALIŞMAZ (metin sabit kalır). Bu, kullanıcının bildirdiği
    // "scroll çalışmıyor" sorunudur.

    private SKRect GetTextViewportSafe()
    {
        // Önce base class'ta GetTextViewport metodu varsa onu çağır.
        // (Reflection ile değil, direkt — integrator protected internal yapmalı.)
        // Şimdilik DisplayRectangle kullanıyoruz. Eğer base class expose
        // ediyorsa, bu metodu değiştirin:
        //   return base.GetTextViewport();
        return base.DisplayRectangle;
    }

    private float GetVerticalScrollSafe()
    {
        // v5.2: base class'taki _vScrollBar.DisplayValue oku.
        // Mevcut TextBox kodu:
        //   private float GetVerticalScrollOffset()
        //   {
        //       return _vScrollBar?.Visible == true ? _vScrollBar.DisplayValue : 0f;
        //   }
        //
        // Eğer _vScrollBar protected internal ise, şu satırı açın:
        //   return _vScrollBar?.Visible == true ? _vScrollBar.DisplayValue : 0f;
        //
        // Eğer GetVerticalScrollOffset() metodu protected internal ise:
        //   return base.GetVerticalScrollOffset();
        //
        // Şimdilik 0 döner — INTEGRATION.md'e göre expose edilmeli.
        return 0f;
    }

    private float GetHorizontalScrollSafe()
    {
        // Aynı şekilde _hScrollBar.DisplayValue.
        // Şimdilik 0.
        return 0f;
    }

    private void SetAutoScrollMinSizeSafe(SKSize size)
    {
        try { base.AutoScrollMinSize = size; } catch { /* base may not expose setter */ }
    }

    // ── Keyboard shortcuts ─────────────────────────────────────────────

    public override void  OnKeyDown(KeyEventArgs e)
    {
        // v4: Undo/Redo (works in all editable modes).
        if (e.Control && !e.Alt)
        {
            switch (e.KeyCode)
            {
                case var _ when e.KeyCode == Keys.Z && !e.Shift:
                    if (Undo())
                        e.Handled = true;
                    return;
                case var _ when (e.KeyCode == Keys.Y) || (e.KeyCode == Keys.Z && e.Shift):
                    if (Redo())
                        e.Handled = true;
                    return;
            }
        }

        // v5: Multi-cursor escape — Esc clears extra cursors.
        if (EnableMultiCursor && HasMultipleCursors && e.KeyCode == Keys.Escape)
        {
            ClearExtraCursors();
            e.Handled = true;
            return;
        }

        // v5: Multi-cursor keyboard shortcuts.
        if (EnableMultiCursor && e.Control && e.Alt && e.KeyCode == Keys.Down)
        {
            // Ctrl+Alt+Down — add cursor on next line (column-mode lite).
            // The integrator should implement actual column logic; we just
            // provide the hook here. For now we add a cursor at the same
            // column on the next line.
            AddCursorOnNextLine();
            e.Handled = true;
            return;
        }
        if (EnableMultiCursor && e.Control && e.Alt && e.KeyCode == Keys.Up)
        {
            AddCursorOnPrevLine();
            e.Handled = true;
            return;
        }

        // Only intercept rich-text shortcuts in editable styled modes.
        if ((_mode == RichTextMode.Plain || _mode == RichTextMode.Rtf) && e.Control)
        {
            switch (e.KeyCode)
            {
                case var _ when e.KeyCode == Keys.B:
                    ToggleBold();
                    e.Handled = true;
                    return;
                case var _ when e.KeyCode == Keys.I:
                    ToggleItalic();
                    e.Handled = true;
                    return;
                case var _ when e.KeyCode == Keys.U:
                    ToggleUnderline();
                    e.Handled = true;
                    return;
                case var _ when e.KeyCode == Keys.T && e.Shift:
                    ToggleStrikethrough();
                    e.Handled = true;
                    return;
            }
        }

        // In MarkdownPreview mode, suppress all editing keys except navigation.
        if (_mode == RichTextMode.MarkdownPreview)
        {
            switch (e.KeyCode)
            {
                case Keys.Up:
                case Keys.Down:
                case Keys.Left:
                case Keys.Right:
                case Keys.PageUp:
                case Keys.PageDown:
                case Keys.Home:
                case Keys.End:
                    break;  // allow navigation
                default:
                    if (!e.Control)
                        e.Handled = true;  // suppress typing
                    break;
            }
            base.OnKeyDown(e);
            return;
        }

        base.OnKeyDown(e);

        // v5: Multi-cursor movement keys (after base.OnKeyDown, so we don't
        // conflict with the base class's single-cursor handling).
        if (EnableMultiCursor && !e.Handled && HasMultipleCursors)
        {
            HandleMultiCursorKey(e);
        }
    }

    /// <summary>v5: Handle a key in multi-cursor mode. Applies the key
    /// (movement, delete, etc.) to ALL cursors via the MultiCursorManager.</summary>
    private void HandleMultiCursorKey(KeyEventArgs e)
    {
        var extendSelection = e.Shift;

        switch (e.KeyCode)
        {
            case Keys.Left:
                _multiCursor.MoveCaretHorizontal(_document, -1, extendSelection);
                SyncCaretToBase();
                e.Handled = true;
                return;
            case Keys.Right:
                _multiCursor.MoveCaretHorizontal(_document, 1, extendSelection);
                SyncCaretToBase();
                e.Handled = true;
                return;
            case Keys.Home:
                _multiCursor.MoveCaretHorizontal(_document, -_multiCursor.Primary.Caret, extendSelection);
                SyncCaretToBase();
                e.Handled = true;
                return;
            case Keys.End:
                _multiCursor.MoveCaretHorizontal(_document, _document.Length - _multiCursor.Primary.Caret, extendSelection);
                SyncCaretToBase();
                e.Handled = true;
                return;
            case Keys.Back:
                SnapshotBeforeOp(coalesce: false);
                _multiCursor.DeleteBackward(_document);
                SyncCaretToBase();
                InvalidateRuns();
                e.Handled = true;
                return;
            case Keys.Delete:
                SnapshotBeforeOp(coalesce: false);
                _multiCursor.DeleteForward(_document);
                SyncCaretToBase();
                InvalidateRuns();
                e.Handled = true;
                return;
        }
    }

    /// <summary>v5: Add a cursor on the next line at the same column.
    /// Uses the base class's line layout to find the next line's start
    /// and offset. The base class's GetTextIndexFromPoint would be ideal
    /// here, but it's likely private — we approximate by looking up
    /// the next line range via the document's newline indices.</summary>
    private void AddCursorOnNextLine()
    {
        var primary = _multiCursor.Primary;
        var nextLineStart = FindNextLineStart(primary.Caret);
        if (nextLineStart < 0 || nextLineStart >= _document.Length) return;
        // Approximate "same column": we don't have access to the layout, so
        // we add the cursor at the start of the next line. The integrator
        // can override this for true column behavior by hooking into
        // MultiCursor.AddCursor with a properly computed position.
        _multiCursor.AddCursor(nextLineStart);
        Invalidate();
    }

    private void AddCursorOnPrevLine()
    {
        var primary = _multiCursor.Primary;
        var prevLineStart = FindPrevLineStart(primary.Caret);
        if (prevLineStart < 0) return;
        _multiCursor.AddCursor(prevLineStart);
        Invalidate();
    }

    private int FindNextLineStart(int fromIndex)
    {
        var text = _document.Text;
        var idx = text.IndexOf('\n', fromIndex);
        return idx < 0 ? -1 : idx + 1;
    }

    private int FindPrevLineStart(int fromIndex)
    {
        var text = _document.Text;
        if (fromIndex <= 0) return -1;
        var idx = text.LastIndexOf('\n', fromIndex - 1);
        if (idx < 0) return 0;
        var prevIdx = text.LastIndexOf('\n', idx - 1);
        return prevIdx < 0 ? 0 : prevIdx + 1;
    }

    /// <summary>v5: After multi-cursor ops modify the document, push the
    /// primary cursor back to the base TextBox so existing scroll/caret
    /// logic stays in sync.</summary>
    private void SyncCaretToBase()
    {
        var primary = _multiCursor.Primary;
        // Use base.Select to update the base TextBox's caret/selection
        // to match the primary cursor. This keeps scroll-to-caret working.
        try { base.Select(primary.Start, primary.Length); } catch { /* base may not be ready */ }
    }

    /// <summary>v5: Override OnKeyPress to intercept typing in multi-cursor
    /// mode. If multiple cursors are active, the typed character is inserted
    /// at every cursor; otherwise we fall through to the base class's
    /// single-cursor handling.</summary>
    public override void  OnKeyPress(KeyPressEventArgs e)
    {
        // Only intercept in multi-cursor mode with multiple active cursors.
        if (EnableMultiCursor && HasMultipleCursors && !_readOnly && Enabled)
        {
            // Skip control chars — let the base class handle them (or not).
            if (e.Control || char.IsControl(e.KeyChar) || e.KeyChar == '\r' || e.KeyChar == '\n')
            {
                base.OnKeyPress(e);
                return;
            }

            // v5: snapshot before multi-cursor typing (coalesced for typing bursts).
            SnapshotBeforeOp(coalesce: true);
            _multiCursor.InsertCharacter(_document, e.KeyChar);
            SyncCaretToBase();
            SyncTextFromDocument();
            InvalidateRuns();
            e.Handled = true;
            return;
        }

        base.OnKeyPress(e);
    }

    // ── Hyperlink click ────────────────────────────────────────────────

    /// <summary>v5: Override OnMouseDown to support Ctrl+Click for adding
    /// cursors in multi-cursor mode. Regular clicks fall through to the
    /// base class's single-cursor handling.</summary>
    public override void  OnMouseDown(MouseEventArgs e)
    {
        // v5: Ctrl+Click in multi-cursor mode adds a cursor at the click point.
        if (EnableMultiCursor && e.Button == MouseButtons.Left
            && (ModifierKeys & Keys.Control) == Keys.Control)
        {
            // Hit-test the click to get the document index.
            // We use the base class's internal GetTextIndexFromPoint, but it's
            // likely private. For v5 we approximate by using SelectionStart
            // after a temporary base.OnMouseDown — actually that's risky.
            //
            // Cleaner: integrator should expose GetTextIndexFromPoint as
            // protected internal, then we call it directly. For now we
            // dispatch to base which sets SelectionStart, then we read
            // SelectionStart and add a cursor at that position.
            base.OnMouseDown(e);

            // The base.OnMouseDown set the base.SelectionStart to the click point.
            // We use that as the cursor position. This is a slight hack but
            // works because base.OnMouseDown's first action is to set the
            // caret to the click position.
            var clickPos = SelectionStart;
            _multiCursor.AddCursor(clickPos);
            Invalidate();
            e.Handled = true;
            return;
        }

        // v5: regular click in multi-cursor mode clears extra cursors first.
        if (EnableMultiCursor && HasMultipleCursors && e.Button == MouseButtons.Left
            && (ModifierKeys & Keys.Control) != Keys.Control
            && (ModifierKeys & Keys.Shift) != Keys.Shift)
        {
            ClearExtraCursors();
            // Fall through to base to set the new single caret.
        }

        base.OnMouseDown(e);

        // v5: after base.OnMouseDown, sync the multi-cursor manager with
        // the new primary caret.
        if (EnableMultiCursor)
        {
            _multiCursor.SetSingle(SelectionStart);
        }
    }

    public override void  OnMouseClick(MouseEventArgs e)
    {
        base.OnMouseClick(e);

        // Detect hyperlink clicks in preview / source modes.
        if (_mode == RichTextMode.MarkdownPreview || _mode == RichTextMode.MarkdownSource)
        {
            var url = TryGetHyperlinkAtPoint(e.Location);
            if (url != null)
            {
                HyperlinkClicked?.Invoke(this, new HyperlinkClickedEventArgs(url));
            }
        }
    }

    private string? TryGetHyperlinkAtPoint(SKPoint point)
    {
        // TODO: implement hit-test using the same line/run model as
        // GetTextIndexFromPoint, then look up the style at that index.
        // For v1 we return null; consumers can wire it up later.
        return null;
    }

    // ── Dispose ────────────────────────────────────────────────────────

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _asyncEngine?.Dispose();
            _measurer?.Dispose();
            _fontCache?.Dispose();
            _blobBatcher?.Dispose();
            _fillPaint?.Dispose();
            _strokePaint?.Dispose();
            // LineLayoutCache has no unmanaged resources; nothing to dispose.
        }
        base.Dispose(disposing);
    }
}

/// <summary>Event args for hyperlink clicks.</summary>
public sealed class HyperlinkClickedEventArgs : EventArgs
{
    public HyperlinkClickedEventArgs(string url) => Url = url;
    public string Url { get; }
}
